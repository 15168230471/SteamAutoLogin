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
    }

    public class AppConfig
    {
        public string maFilesDir { get; set; }
        public string accountsExcelPath { get; set; }
        public string nodeScriptPath { get; set; }
        public string steamExePath { get; set; }
        public string cs2AIScriptPath { get; set; }
    }

    public partial class Form1 : Form
    {
        private ExcelService _excelService;
        private List<AccountInfo> _accounts;
        private AppConfig _config;
        private Dictionary<string, int> _accountLatestLevelDict = new Dictionary<string, int>();

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
        private async Task QueryAccount(AccountInfo acc)
        {
            Invoke((Action)(() => lstAccounts.Items.Add($"开始查询: 账号: {acc.Username}, 密码: {acc.Password}")));

            string maFilePath = SteamGuardHelper.FindMaFileForAccount(_config.maFilesDir, acc.Username);
            if (maFilePath == null)
            {
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 未找到maFile，跳过！")));
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询结束")));
                return;
            }

            var psi = new ProcessStartInfo("node", $"{_config.nodeScriptPath} {acc.Username} {acc.Password} \"{maFilePath}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string allOut = null;
            try
            {
                allOut = await RunNodeScript(psi);
            }
            catch (Exception ex)
            {
                Invoke((Action)(() => lstAccounts.Items.Add($"Node运行异常: {ex.Message}, 账号: {acc.Username}")));
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询结束")));
                return;
            }

            if (allOut == null)
            {
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询超时！")));
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询结束")));
                return;
            }

            try
            {
                var info = JsonConvert.DeserializeObject<LevelInfo>(allOut);
                int level = info?.currentLevel ?? 0;
                int xp = info?.currentXP ?? 0;
                lock (_accountLatestLevelDict)
                {
                    _accountLatestLevelDict[acc.Username] = level;
                }
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 等级: {level} 经验: {xp}")));
            }
            catch
            {
                lock (_accountLatestLevelDict)
                {
                    _accountLatestLevelDict[acc.Username] = 0;
                }
                Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 等级: 0 经验: 0")));
            }
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
            var semaphore = new SemaphoreSlim(2); // 最多2个并发
            const int SingleAccountTimeout = 90 * 1000;

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
                            Invoke((Action)(() => lstAccounts.Items.Add($"账号: {acc.Username} 查询超时(>90s)，已跳过！")));
                    }
                    finally { semaphore.Release(); }
                }));
            }

            await Task.WhenAll(tasks);
            UpdateAccountLevelsToExcel(_config.accountsExcelPath);
            MessageBox.Show("所有账号的查询已完成，并已写回Excel！");
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
            // 重新读取所有账号
            _accounts = _excelService.ReadAccountsFromExcel(_config.accountsExcelPath);

            foreach (var acc in _accounts.Where(a => a.IsUpgraded == "否"))
            {
                await MonitorAndLevelUp(acc);
                // 刷新账号状态，避免excel未写入状态
                _accounts = _excelService.ReadAccountsFromExcel(_config.accountsExcelPath);
            }
            MessageBox.Show("所有账号已升级完成！");
        }

        private async Task MonitorAndLevelUp(AccountInfo acc)
        {
            while (true)
            {
                // ---- 1. 关闭所有旧进程 ----
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 开始处理账号 {acc.Username}：准备关闭旧进程...")));
                KillProcessIfExist("cs2");
                KillProcessIfExist(Path.GetFileNameWithoutExtension(_config.cs2AIScriptPath));
                KillProcessIfExist("steam");
                await Task.Delay(3000);

                // ---- 2. 启动 steam 并自动进 cs2 ----
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 启动Steam并自动进入CS2...")));
                Process.Start(new ProcessStartInfo
                {
                    FileName = _config.steamExePath,
                    Arguments = "-applaunch 730 -novid -sw -w 1280 -h 720",
                    UseShellExecute = true
                });

                // ---- 3. 自动登录（账号密码验证码自动填充）----
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 等待Steam登录窗口并自动输入账号密码...")));
                await DoFlaUIAutoLogin(acc);
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 登录流程已结束，等待CS2进程启动...")));

                // ---- 4. 等待CS2进程 ----
                bool cs2Started = await WaitForProcess("cs2", 120);
                if (!cs2Started)
                {
                    Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] CS2启动失败，重试本账号。")));
                    continue;
                }
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 检测到CS2进程，准备启动AI挂机脚本...")));

                // ---- 5. 启动AI挂机脚本 ----
                var aiProc = StartAIProcess();
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] AI挂机脚本已启动，进入挂机监控阶段...")));

                // ---- 6. 监控三进程 ----
                while (true)
                {
                    await Task.Delay(3000);
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

                // ---- 7. 查询等级并写回excel ----
                int newLevel = await QueryLevelDirectly(acc);
                lock (_accountLatestLevelDict)
                {
                    _accountLatestLevelDict[acc.Username] = newLevel;
                }
                UpdateAccountLevelsToExcel(_config.accountsExcelPath);
                Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 查询完成，账号 {acc.Username} 当前等级：{newLevel}。")));

                // ---- 8. 判断是否升级 ----
                int initialLevel = 0;
                _accounts = _excelService.ReadAccountsFromExcel(_config.accountsExcelPath);
                var accNow = _accounts.FirstOrDefault(a => a.Username == acc.Username);
                if (accNow != null) initialLevel = accNow.InitialLevel;

                if (newLevel == initialLevel + 1)
                {
                    Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 账号 {acc.Username} 已升级！准备切换下一个账号。")));
                    break; // 进入下一个账号
                }
                else
                {
                    Invoke((Action)(() => lstAccounts.Items.Add($"[{DateTime.Now:HH:mm:ss}] 账号 {acc.Username} 未升级，重新尝试本账号。")));
                    // 继续循环重试本账号
                }
            }
        }


        // 自动登录过程（原FlaUI+剪贴板+验证码逻辑）
        private async Task DoFlaUIAutoLogin(AccountInfo acc)
        {
           
            // 2. 等待Steam登录窗口
            IntPtr hwndLogin = IntPtr.Zero;
            for (int i = 0; i < 120; i++)
            {
                hwndLogin = FindWindow(null, "登录 Steam"); // "Steam Login" 英文
                if (hwndLogin != IntPtr.Zero)
                    break;
                await Task.Delay(1000);
            }
            if (hwndLogin == IntPtr.Zero)
            {
                MessageBox.Show("未能在120秒内检测到Steam登录窗口！");
                return;
            }

            while (GetForegroundWindow() != hwndLogin)
            {
                SetForegroundWindow(hwndLogin);
                await Task.Delay(200);
            }

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

            // 4. 自动输入Steam Guard验证码
            string maFilePath = SteamGuardHelper.FindMaFileForAccount(_config.maFilesDir, acc.Username);
            if (maFilePath == null)
            {
                MessageBox.Show("未找到此账号的maFile，无法自动获取验证码！");
                return;
            }
            using (var automation = new FlaUI.UIA3.UIA3Automation())
            {
                bool codeEntered = false;
                for (int i = 0; i < 40; i++)
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
                    MessageBox.Show("未能检测到验证码界面或未自动填入验证码，请手动操作！");
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

        private async Task<int> QueryLevelDirectly(AccountInfo acc)
        {
            string maFilePath = SteamGuardHelper.FindMaFileForAccount(_config.maFilesDir, acc.Username);
            if (maFilePath == null) return 0;
            var psi = new ProcessStartInfo("node", $"{_config.nodeScriptPath} {acc.Username} {acc.Password} \"{maFilePath}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            string allOut = null;
            try { allOut = await RunNodeScript(psi); }
            catch { return 0; }
            try
            {
                var info = JsonConvert.DeserializeObject<LevelInfo>(allOut);
                return info?.currentLevel ?? 0;
            }
            catch { return 0; }
        }

        private async Task<string> RunNodeScript(ProcessStartInfo psi)
        {
            using (var process = Process.Start(psi))
            using (var reader = process.StandardOutput)
            {
                var readTask = reader.ReadToEndAsync();
                if (await Task.WhenAny(readTask, Task.Delay(60 * 1000)) == readTask)
                    return readTask.Result;
                else
                {
                    try { process.Kill(); } catch { }
                    return null;
                }
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
