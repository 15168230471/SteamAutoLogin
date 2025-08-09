using ClosedXML.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteamAutoLogin
{
    public class LevelInfo
    {
        public int? currentLevel { get; set; }
        public int? currentXP { get; set; }
        public string account { get; set; }

        // 读取 Node 的错误信息
        public string error { get; set; }
        public string detail { get; set; }
    }

    public class AppConfig
    {
        public string maFilesDir { get; set; }
        public string accountsExcelPath { get; set; }
        public string nodeScriptPath { get; set; }  // 绝对路径到 get_cs2_level.js
        public string steamExePath { get; set; }
        public string cs2AIScriptPath { get; set; }
        // 如需固定 node.exe 路径可加字段：public string nodeExePath { get; set; }
    }

    public partial class Form1 : Form
    {
        private ExcelService _excelService;
        private List<AccountInfo> _accounts;
        private AppConfig _config;
        private readonly Dictionary<string, int> _accountLatestLevelDict = new Dictionary<string, int>();

        public Form1()
        {
            InitializeComponent();
            LoadConfig();
            _excelService = new ExcelService();
            _accounts = new List<AccountInfo>();
        }

        private void LoadConfig()
        {
            try
            {
                string configText = File.ReadAllText("config.json");
                _config = JsonConvert.DeserializeObject<AppConfig>(configText);
                if (_config == null ||
                    string.IsNullOrWhiteSpace(_config.maFilesDir) ||
                    string.IsNullOrWhiteSpace(_config.accountsExcelPath) ||
                    string.IsNullOrWhiteSpace(_config.nodeScriptPath) ||
                    string.IsNullOrWhiteSpace(_config.steamExePath) ||
                    string.IsNullOrWhiteSpace(_config.cs2AIScriptPath))
                {
                    MessageBox.Show("config.json 文件内容不正确或缺少字段！");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取config.json失败: " + ex.Message);
                Environment.Exit(1);
            }
        }

        // 统一构建 Node 启动信息：工作目录=脚本目录；参数全部加引号
        private ProcessStartInfo BuildNodeStartInfo(string username, string password, string maFile)
        {
            var nodeExe = "node"; // 若在配置中固定 node.exe 路径，改成 _config.nodeExePath
            string scriptPath = _config.nodeScriptPath;           // 绝对路径
            string workDir = Path.GetDirectoryName(scriptPath) ?? AppDomain.CurrentDomain.BaseDirectory;

            // 每个参数加引号，避免空格/特殊字符被 shell 解释
            string args = $"\"{scriptPath}\" \"{username}\" \"{password}\" \"{maFile}\"";

            return new ProcessStartInfo(nodeExe, args)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        private async Task QueryAccount(AccountInfo acc, int retry = 0)
        {
            // --- 小随机延迟，避免同一IP瞬时打爆 ---
            if (retry == 0)
            {
                var rnd = new Random();
                int jitter = rnd.Next(5000, 15000); // 5~15s
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} {jitter / 1000}s 后开始查询（防频控）...")));
                await Task.Delay(jitter);
            }

            // --- 启动信息 ---
            Invoke((Action)(() => lstAccounts.Items.Add($"开始查询: 账号: {acc.Username}, 密码: {acc.Password}")));
            //Invoke((Action)(() => lstAccounts.Items.Add($"使用脚本: {_config.nodeScriptPath}")));

            string maFilePath = SteamGuardHelper.FindMaFileForAccount(_config.maFilesDir, acc.Username);
            //Invoke((Action)(() => lstAccounts.Items.Add($"maFile: {maFilePath ?? "<未找到>"}")));
            if (maFilePath == null)
            {
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 未找到 maFile，跳过。")));
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询结束")));
                return;
            }

            var psi = BuildNodeStartInfo(acc.Username, acc.Password, maFilePath);

            // --- 跑 Node ---
            string stdout = null, stderr = null;
            try
            {
                (stdout, stderr) = await RunNodeScript(psi, acc.Username);
            }
            catch (Exception ex)
            {
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} Node运行异常: {ex.Message}")));
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询结束")));
                return;
            }

            if (stdout == null)
            {
                Invoke((Action)(() => lstAccounts.Items.Add(
                    $"账号: {acc.Username} 查询超时(>130s)。stderr预览: " +
                    (string.IsNullOrWhiteSpace(stderr) ? "<空>" : stderr.Split('\n').FirstOrDefault())
                )));
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询结束")));
                return;
            }

            // --- 解析 JSON ---
            LevelInfo info = null;
            try
            {
                string json = ExtractLastJson(stdout);
                info = JsonConvert.DeserializeObject<LevelInfo>(json);
            }
            catch (Exception ex)
            {
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} JSON解析失败: {ex.Message}")));
                return; // 解析失败不写0
            }

            // --- 辅助判断（本地函数） ---
            bool IsRateLimited(LevelInfo i) =>
                i != null &&
                string.Equals(i.error, "steam_error", StringComparison.OrdinalIgnoreCase) &&
                (i.detail?.IndexOf("RateLimitExceeded", StringComparison.OrdinalIgnoreCase) >= 0);

            bool IsTransient(LevelInfo i) =>
                i != null &&
                (
                    string.Equals(i.error, "gc_connect_timeout_60s", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.error, "timeout_120s", StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(i.error, "steam_error", StringComparison.OrdinalIgnoreCase) &&
                     (i.detail?.IndexOf("Request timed out", StringComparison.OrdinalIgnoreCase) >= 0))
                );

            // --- 错误处理与重试 ---
            if (!string.IsNullOrEmpty(info?.error))
            {
                // 频控：指数退避 3 次（60s -> 150s -> 300s）
                if (IsRateLimited(info) && retry < 3)
                {
                    int[] backoff = { 60_000, 150_000, 300_000 };
                    int wait = backoff[retry];
                    Invoke((Action)(() => lstAccounts.Items.Add(
                        $"账号: {acc.Username} 触发频控，{wait / 1000}s 后重试({retry + 1}/3)...")));
                    await Task.Delay(wait);
                    await QueryAccount(acc, retry + 1);
                    return;
                }

                // 瞬态超时：最多再试 1 次（30~60s 随机）
                if (IsTransient(info) && retry < 1)
                {
                    var rnd = new Random();
                    int wait = rnd.Next(30_000, 60_000);
                    Invoke((Action)(() => lstAccounts.Items.Add(
                        $"账号: {acc.Username} 网络/GC 超时，{wait / 1000}s 后重试(1/1)...")));
                    await Task.Delay(wait);
                    await QueryAccount(acc, retry + 1);
                    return;
                }

                // 其他错误：仅提示，不写 0
                Invoke((Action)(() => lstAccounts.Items.Add(
                    $"账号: {acc.Username} 查询失败: {info.error}" +
                    (string.IsNullOrEmpty(info.detail) ? "" : $" | {info.detail}")
                )));
                return;
            }

            // --- 成功：记录并显示 ---
            int level = info?.currentLevel ?? 0;
            int xp = info?.currentXP ?? 0;

            lock (_accountLatestLevelDict)
            {
                _accountLatestLevelDict[acc.Username] = level;
            }
            Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 等级: {level} 经验: {xp}")));
        }


        private async void btnReadExcel_Click(object sender, EventArgs e)
        {
            string filePath = _config.accountsExcelPath;
            _accounts = _excelService.ReadAccountsFromExcel(filePath);
            if (_accounts == null || _accounts.Count == 0)
            {
                MessageBox.Show("请先读取Excel文件！");
                return;
            }

            lstAccounts.Items.Clear();

            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(1); // 最多2个并发
            const int SingleAccountTimeout = 180 * 1000; // 外层保护 3 分钟

            foreach (var acc in _accounts.Where(a => a.IsUpgraded == "否"))
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var t = QueryAccount(acc);
                        if (await Task.WhenAny(t, Task.Delay(SingleAccountTimeout)) == t)
                            await t;
                        else
                            Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询超时(>180s)，已跳过！")));
                    }
                    finally { semaphore.Release(); }
                }));
            }

            await Task.WhenAll(tasks);

            // 只把成功的（在字典中的）写回
            UpdateAccountLevelsToExcel(_config.accountsExcelPath);
            MessageBox.Show("所有账号的查询已完成，并已写回Excel（仅成功结果）！");
        }

        // 单账号自动登录：保留原FlaUI自动操作流程
        private async void btnAutoLogin_Click(object sender, EventArgs e)
        {
            if (_accounts == null || _accounts.Count == 0)
            {
                MessageBox.Show("请先读取Excel文件！");
                return;
            }
            var accountToLogin = _accounts.FirstOrDefault(acc =>
                !string.IsNullOrWhiteSpace(acc.IsUpgraded) && acc.IsUpgraded.Trim() == "否");
            if (accountToLogin == null)
            {
                MessageBox.Show("所有账号都已升级，无需登录。");
                return;
            }
            await DoFlaUIAutoLogin(accountToLogin);
        }

        // -------------------------- 全自动刷级主流程 --------------------------
        private async void btnStartAutoLevel_Click(object sender, EventArgs e)
        {
            _accounts = _excelService.ReadAccountsFromExcel(_config.accountsExcelPath);

            foreach (var acc in _accounts.Where(a => a.IsUpgraded == "否"))
            {
                await MonitorAndLevelUp(acc);
                _accounts = _excelService.ReadAccountsFromExcel(_config.accountsExcelPath);
            }
            MessageBox.Show("所有账号已升级完成！");
        }

        private async Task MonitorAndLevelUp(AccountInfo acc)
        {
            while (true)
            {
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 开始处理账号 {acc.Username}：准备关闭旧进程...")));
                KillProcessIfExist("cs2");
                KillProcessIfExist(Path.GetFileNameWithoutExtension(_config.cs2AIScriptPath));
                KillProcessIfExist("steam");
                await Task.Delay(3000);

                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 启动Steam并自动进入CS2...")));
                Process.Start(new ProcessStartInfo
                {
                    FileName = _config.steamExePath,
                    Arguments = "-applaunch 730 -novid -sw -w 1280 -h 720",
                    UseShellExecute = true
                });

                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 等待Steam登录窗口并自动输入账号密码...")));
                await DoFlaUIAutoLogin(acc);
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 登录流程已结束，等待CS2进程启动...")));

                bool cs2Started = await WaitForProcess("cs2", 120);
                if (!cs2Started)
                {
                    Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] CS2启动失败，重试本账号。")));
                    continue;
                }
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 检测到CS2进程，准备启动AI挂机脚本...")));

                await Task.Delay(50000);
                var aiProc = StartAIProcess();
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] AI挂机脚本已启动，进入挂机监控阶段...")));

                while (true)
                {
                    await Task.Delay(60000);
                    bool steamAlive = Process.GetProcessesByName("steam").Any();
                    bool cs2Alive = Process.GetProcessesByName("cs2").Any();
                    string aiExeNoExt = Path.GetFileNameWithoutExtension(_config.cs2AIScriptPath);
                    bool aiAlive = Process.GetProcessesByName(aiExeNoExt).Any();
                    if (!steamAlive && !cs2Alive && !aiAlive)
                    {
                        Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 检测到steam/cs2/AI脚本均已关闭，准备查询当前等级...")));
                        break;
                    }
                }

                int newLevel = await QueryLevelDirectly(acc);
                if (newLevel < 0)
                {
                    Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 查询失败，跳过写回Excel（稍后可重试）。")));
                    continue; // 失败不更新、不判断升级
                }

                lock (_accountLatestLevelDict)
                {
                    _accountLatestLevelDict[acc.Username] = newLevel;
                }
                UpdateAccountLevelsToExcel(_config.accountsExcelPath);
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 查询完成，账号 {acc.Username} 当前等级：{newLevel}。")));

                int initialLevel = 0;
                _accounts = _excelService.ReadAccountsFromExcel(_config.accountsExcelPath);
                var accNow = _accounts.FirstOrDefault(a => a.Username == acc.Username);
                if (accNow != null) initialLevel = accNow.InitialLevel;

                if (newLevel == initialLevel + 1)
                {
                    Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 账号 {acc.Username} 已升级！准备切换下一个账号。")));
                    break;
                }
                else
                {
                    Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 账号 {acc.Username} 未升级，重新尝试本账号。")));
                }
            }
        }

        // 自动登录过程（原FlaUI+剪贴板+验证码逻辑）
        private async Task DoFlaUIAutoLogin(AccountInfo acc)
        {
            IntPtr hwndLogin = IntPtr.Zero;
            for (int i = 0; i < 240; i++)
            {
                hwndLogin = FindWindow(null, "登录 Steam"); // "Steam Login"
                if (hwndLogin != IntPtr.Zero)
                    break;
                await Task.Delay(1000);
            }
            if (hwndLogin == IntPtr.Zero)
            {
                MessageBox.Show("未能在240秒内检测到Steam登录窗口！即将关闭steam.exe");
                KillProcessIfExist("steam");
                return;
            }

            while (GetForegroundWindow() != hwndLogin)
            {
                SetForegroundWindow(hwndLogin);
                await Task.Delay(200);
            }

            try
            {
                using (var automation = new FlaUI.UIA3.UIA3Automation())
                {
                    var steamLoginWin = automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("登录 Steam"));
                    if (steamLoginWin != null)
                    {
                        var allElems = steamLoginWin.FindAllDescendants();
                        var allEdits = allElems.Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Edit).ToList();
                        if (allEdits.Count >= 2)
                        {
                            allEdits[0].Focus();
                            await Task.Delay(200);
                            Clipboard.SetText(acc.Username);
                            SendKeys.SendWait("^a");
                            await Task.Delay(80);
                            SendKeys.SendWait("^v");
                            await Task.Delay(200);

                            allEdits[1].Focus();
                            await Task.Delay(200);
                            Clipboard.SetText(acc.Password);
                            SendKeys.SendWait("^a");
                            await Task.Delay(80);
                            SendKeys.SendWait("^v");
                            await Task.Delay(200);

                            SendKeys.SendWait("{ENTER}");
                            await Task.Delay(500);
                        }
                        else
                        {
                            SetForegroundWindow(hwndLogin);
                            Clipboard.SetText(acc.Username);
                            SendKeys.SendWait("^a");
                            SendKeys.SendWait("^v");
                            await Task.Delay(150);

                            SendKeys.SendWait("{TAB}");
                            await Task.Delay(150);

                            Clipboard.SetText(acc.Password);
                            SendKeys.SendWait("^a");
                            SendKeys.SendWait("^v");
                            await Task.Delay(150);

                            SendKeys.SendWait("{ENTER}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"自动输入账号密码流程异常：{ex.Message}，即将关闭steam.exe");
                KillProcessIfExist("steam");
                return;
            }

            // 自动输入Steam Guard验证码
            string maFilePath = SteamGuardHelper.FindMaFileForAccount(_config.maFilesDir, acc.Username);
            if (maFilePath == null)
            {
                MessageBox.Show("未找到此账号的maFile，无法自动获取验证码！");
                KillProcessIfExist("steam");
                return;
            }
            using (var automation = new FlaUI.UIA3.UIA3Automation())
            {
                bool codeEntered = false;
                for (int i = 0; i < 100; i++)
                {
                    var win = automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("登录 Steam"));
                    if (win != null)
                    {
                        var label = win.FindFirstDescendant(cf => cf.ByName("输入您 Steam 手机应用上的代码"));
                        if (label != null)
                        {
                            win.Focus();
                            string code = SteamGuardHelper.GetSteamGuardCode(maFilePath);
                            Clipboard.SetText(code);
                            await Task.Delay(60);
                            SendKeys.SendWait("^a");
                            await Task.Delay(30);
                            SendKeys.SendWait("^v");
                            await Task.Delay(100);
                            SendKeys.SendWait("{ENTER}");
                            codeEntered = true;
                            break;
                        }
                    }
                    await Task.Delay(500);
                }
                if (!codeEntered)
                {
                    MessageBox.Show("未能检测到验证码界面或未自动填入验证码!");
                    KillProcessIfExist("steam");
                }
            }
        }

        // 进程辅助
        private void KillProcessIfExist(string procName)
        {
            foreach (var p in Process.GetProcessesByName(procName))
            {
                try { p.Kill(); } catch { }
            }
        }

        private async Task<bool> WaitForProcess(string procName, int timeoutSec = 120)
        {
            for (int i = 0; i < timeoutSec; i++)
            {
                var proc = Process.GetProcessesByName(procName).FirstOrDefault();
                if (proc != null)
                    return true;
                await Task.Delay(1000);
            }
            return false;
        }

        private Process StartAIProcess()
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = _config.cs2AIScriptPath,
                UseShellExecute = true
            });
        }

        // 失败返回 -1；只在成功时返回等级
        private async Task<int> QueryLevelDirectly(AccountInfo acc)
        {
            string maFilePath = SteamGuardHelper.FindMaFileForAccount(_config.maFilesDir, acc.Username);
            if (maFilePath == null) return -1;

            var psi = BuildNodeStartInfo(acc.Username, acc.Password, maFilePath);

            try
            {
                (string stdout, string stderr) = await RunNodeScript(psi, acc.Username);
                if (stdout == null) return -1;

                string json = ExtractLastJson(stdout);
                var info = JsonConvert.DeserializeObject<LevelInfo>(json);

                if (!string.IsNullOrEmpty(info?.error))
                    return -1;

                return info?.currentLevel ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        // —— 修正后的 RunNodeScript：对同一个 timeoutTask 比较，杜绝误判 —— 
        private async Task<(string stdout, string stderr)> RunNodeScript(ProcessStartInfo psi, string accountForLog)
        {
            using (var process = Process.Start(psi))
            {
                var readStdout = process.StandardOutput.ReadToEndAsync();
                var readStderr = process.StandardError.ReadToEndAsync();

                var timeoutTask = Task.Delay(130 * 1000);   // 与 Node 120s 硬超时配合
                var allTask = Task.WhenAll(readStdout, readStderr);

                var finished = await Task.WhenAny(allTask, timeoutTask);
                if (finished == timeoutTask)
                {
                    try { if (!process.HasExited) process.Kill(); } catch { }
                    return (null, null);
                }

                string stdout = readStdout.Result;
                string stderr = readStderr.Result;

                // 落盘日志（便于排查）
                try
                {
                    var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    Directory.CreateDirectory(logDir);
                    File.WriteAllText(Path.Combine(logDir, $"node_stdout_{accountForLog}_{DateTime.Now:yyyyMMdd_HHmmss}.log"), stdout ?? "");
                    if (!string.IsNullOrWhiteSpace(stderr))
                        File.WriteAllText(Path.Combine(logDir, $"node_stderr_{accountForLog}_{DateTime.Now:yyyyMMdd_HHmmss}.log"), stderr);
                }
                catch { /* 忽略日志写入异常 */ }

                return (stdout, stderr);
            }
        }

        private void UpdateAccountLevelsToExcel(string filePath)
        {
            var dict = _accountLatestLevelDict;
            if (dict.Count == 0) return;

            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheets.First();
                var headerRow = ws.Row(1);
                int lastCol = headerRow.LastCellUsed().Address.ColumnNumber;
                int colAccount = 0, colIsUpgraded = 0, colInitialLevel = 0, colLatestLevel = 0;
                for (int col = 1; col <= lastCol; col++)
                {
                    var cellVal = headerRow.Cell(col).GetString().Trim();
                    if (cellVal.Contains("账号")) colAccount = col;
                    else if (cellVal.Contains("是否已升级")) colIsUpgraded = col;
                    else if (cellVal.Contains("初始等级")) colInitialLevel = col;
                    else if (cellVal.Contains("最新等级")) colLatestLevel = col;
                }
                if (colAccount == 0 || colIsUpgraded == 0 || colInitialLevel == 0 || colLatestLevel == 0)
                {
                    MessageBox.Show($"表头识别失败。账号:{colAccount} 是否已升级:{colIsUpgraded} 初始等级:{colInitialLevel} 最新等级:{colLatestLevel}");
                    return;
                }
                int lastRow = ws.LastRowUsed().RowNumber();
                for (int row = 2; row <= lastRow; row++)
                {
                    var username = ws.Cell(row, colAccount).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(username)) continue;
                    if (dict.TryGetValue(username, out int newLevel))
                    {
                        ws.Cell(row, colLatestLevel).Value = newLevel;
                        int initialLevel = 0;
                        try { initialLevel = ws.Cell(row, colInitialLevel).GetValue<int>(); } catch { }
                        if (newLevel == initialLevel + 1)
                            ws.Cell(row, colIsUpgraded).Value = "是";
                    }
                }
                workbook.Save();
            }
        }

        // 提取最后一段 { ... }，防止非 JSON 噪音
        private string ExtractLastJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "{}";
            int l = text.LastIndexOf('{');
            int r = text.LastIndexOf('}');
            if (l >= 0 && r > l)
                return text.Substring(l, r - l + 1);
            return text.Trim();
        }

        // Win32辅助
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        private void lstAccounts_SelectedIndexChanged(object sender, EventArgs e) { }
        private void Form1_Load(object sender, EventArgs e) { }
    }
}
