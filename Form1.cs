using FlaUI.Core;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteamAutoLogin
{
    public partial class Form1 : Form
    {
        private ExcelService _excelService;
        private List<AccountInfo> _accounts;
        // Path to the directory containing Steam Desktop Authenticator .maFile files.
        private string maFilesDir = @"F:\SDA\maFiles";

        // Replace this with your own Steam Web API key. Without a valid key the API call will fail.
        private readonly string steamApiKey = "YOUR_STEAM_WEB_API_KEY";

        public Form1()
        {
            InitializeComponent();
            _excelService = new ExcelService();
            _accounts = new List<AccountInfo>();
        }

        private void btnReadExcel_Click(object sender, EventArgs e)
        {
            string filePath = @"data\accounts.xlsx";
            _accounts = _excelService.ReadAccountsFromExcel(filePath);

            lstAccounts.Items.Clear();
            foreach (var acc in _accounts)
            {
                lstAccounts.Items.Add($"账号: {acc.Username}, 密码: {acc.Password}, 是否升级: {acc.IsUpgraded}");
            }
        }

        private async void btnAutoLogin_Click(object sender, EventArgs e)
        {
            if (_accounts == null || _accounts.Count == 0)
            {
                MessageBox.Show("请先读取Excel文件！");
                return;
            }
            var accountToLogin = _accounts.FirstOrDefault(acc =>
                !string.IsNullOrWhiteSpace(acc.IsUpgraded) && acc.IsUpgraded.Trim() == "是");
            if (accountToLogin == null)
            {
                MessageBox.Show("所有账号均未升级或未标记，无需登录。");
                return;
            }

            // Launch Steam and Counter‑Strike 2 with parameters. Adjust the path as needed.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = @"H:\Steam\Steam.exe",
                Arguments = "-applaunch 730 -novid -sw -w 1280 -h 720",
                UseShellExecute = true
            });

            // Wait for the Steam login window to appear (up to 120 seconds).
            IntPtr hwndLogin = IntPtr.Zero;
            for (int i = 0; i < 120; i++)
            {
                hwndLogin = FindWindow(null, "登录 Steam");
                if (hwndLogin != IntPtr.Zero)
                    break;
                await Task.Delay(1000);
            }
            if (hwndLogin == IntPtr.Zero)
            {
                MessageBox.Show("在120秒内未检测到Steam登录窗口！");
                return;
            }

            // Bring the window to the foreground and enter credentials.
            while (GetForegroundWindow() != hwndLogin)
            {
                SetForegroundWindow(hwndLogin);
                await Task.Delay(200);
            }

            using (var automation = new UIA3Automation())
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
                        Clipboard.SetText(accountToLogin.Username);
                        SendKeys.SendWait("^a");
                        await Task.Delay(80);
                        SendKeys.SendWait("^v");
                        await Task.Delay(200);

                        allEdits[1].Focus();
                        await Task.Delay(200);
                        Clipboard.SetText(accountToLogin.Password);
                        SendKeys.SendWait("^a");
                        await Task.Delay(80);
                        SendKeys.SendWait("^v");
                        await Task.Delay(200);

                        SendKeys.SendWait("{ENTER}");
                        await Task.Delay(500);
                    }
                    else
                    {
                        // fallback: type directly into the focused fields
                        SetForegroundWindow(hwndLogin);
                        Clipboard.SetText(accountToLogin.Username);
                        SendKeys.SendWait("^a");
                        SendKeys.SendWait("^v");
                        await Task.Delay(150);
                        SendKeys.SendWait("{TAB}");
                        await Task.Delay(150);
                        Clipboard.SetText(accountToLogin.Password);
                        SendKeys.SendWait("^a");
                        SendKeys.SendWait("^v");
                        await Task.Delay(150);
                        SendKeys.SendWait("{ENTER}");
                    }
                }
            }

            // Handle Steam Guard code entry
            string maFilePath = SteamGuardHelper.FindMaFileForAccount(maFilesDir, accountToLogin.Username);
            if (maFilePath == null)
            {
                MessageBox.Show("未找到对应账号的maFile，无法自动获取验证代码！");
                return;
            }

            using (var automation = new UIA3Automation())
            {
                bool codeEntered = false;
                for (int i = 0; i < 40; i++)
                {
                    var win = automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("登录 Steam"));
                    if (win != null)
                    {
                        var label = win.FindFirstDescendant(cf => cf.ByName("输入您 Steam 手机应用产生的验证码"));
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
                    MessageBox.Show("未检测到验证码输入界面或自动输入失败，请手动操作。");
                }
            }

            // Wait up to 120 seconds for the CS2 window to open, checking every second.
            Debug.WriteLine("开始循环检测CS2窗口...");
            IntPtr hwndCs2 = IntPtr.Zero;
            for (int i = 0; i < 120; i++)
            {
                var proc = Process.GetProcessesByName("cs2").FirstOrDefault();
                hwndCs2 = proc?.MainWindowHandle ?? IntPtr.Zero;
                Debug.WriteLine($"[{i + 1}s] 通过进程名获取句柄: {hwndCs2}");
                if (hwndCs2 != IntPtr.Zero)
                    break;

                hwndCs2 = FindWindow(null, "Counter-Strike 2");
                Debug.WriteLine($"[{i + 1}s] 通过窗口名查询句柄: {hwndCs2}");
                if (hwndCs2 != IntPtr.Zero)
                    break;

                await Task.Delay(1000);
            }
            if (hwndCs2 != IntPtr.Zero)
            {
                Debug.WriteLine("找到了CS2窗口，开始执行OCR自动点击。");
                string[] keywords = { "确定", "开始", "死亡竞赛", "不再显示", "关闭" };
                EasyOcrHelper.WaitAndClickButton(hwndCs2, new[] { "确定" }, 30, 1000);
            }
            else
            {
                Debug.WriteLine("未找到CS2窗口！");
                MessageBox.Show("未找到CS2窗口！");
            }
        }

        /// <summary>
        /// 点击“查询CS2等级”按钮时调用。此操作通过游戏协调器(GC)协议查询当前登录账号的CS2等级和经验，而无需使用Web API。
        /// 为了成功连接到GC，需要提供账号的用户名、密码以及与该账号关联的 .maFile 用于生成双因素验证码。
        /// </summary>
        private async void btnGetCs2Stats_Click(object sender, EventArgs e)
        {
            if (_accounts == null || _accounts.Count == 0)
            {
                MessageBox.Show("请先读取账号列表！");
                return;
            }
            if (lstAccounts.SelectedIndex < 0 || lstAccounts.SelectedIndex >= _accounts.Count)
            {
                MessageBox.Show("请在列表中选择一个账号！");
                return;
            }

            var acc = _accounts[lstAccounts.SelectedIndex];
            // Locate the maFile for the selected account.  This file stores the shared_secret used to
            // generate two‑factor codes.  Without a matching maFile the GC login will fail.
            string maFilePath;
            try
            {
                maFilePath = SteamGuardHelper.FindMaFileForAccount(maFilesDir, acc.Username);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取maFile目录时出错: {ex.Message}");
                return;
            }
            if (maFilePath == null)
            {
                MessageBox.Show("未找到对应账号的maFile，无法生成Steam令牌！");
                return;
            }

            try
            {
                // Use the GC service to fetch the player's level and XP.  This will log in to Steam
                // using the provided credentials and may briefly disconnect the account from any
                // existing sessions.  When complete it logs off automatically.
                var (level, xp) = await Cs2GcService.GetPlayerLevelAndXpAsync(acc.Username, acc.Password, maFilePath);
                lblCs2Stats.Text = $"CS2 等级: {level}\n当前经验: {xp}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("查询GC出错: " + ex.Message);
            }
        }

        // P/Invoke declarations for window management
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        private void lstAccounts_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Not used
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            // Not used
        }
    }
}