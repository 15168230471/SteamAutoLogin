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
        private List<AccountInfo> _accounts; // 保存读取的所有账号
        private string maFilesDir = @"F:\SDA\maFiles"; // 你的maFiles文件夹路径

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
                lstAccounts.Items.Add($"账号: {acc.Username}, 密码: {acc.Password}, 是否升级完成: {acc.IsUpgraded}");
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
                !string.IsNullOrWhiteSpace(acc.IsUpgraded) && acc.IsUpgraded.Trim() == "否");
            if (accountToLogin == null)
            {
                MessageBox.Show("所有账号都已升级，无需登录。");
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = @"H:\Steam\Steam.exe",
                Arguments = "-applaunch 730 -novid -sw -w 1280 -h 720",
                UseShellExecute = true
            });

            // 2. 轮询等待“登录 Steam”窗口弹出（最多120秒）
            IntPtr hwndLogin = IntPtr.Zero;
            for (int i = 0; i < 120; i++)
            {
                hwndLogin = FindWindow(null, "登录 Steam"); // 英文为 "Steam Login"
                if (hwndLogin != IntPtr.Zero)
                    break;
                await Task.Delay(1000);
            }
            if (hwndLogin == IntPtr.Zero)
            {
                MessageBox.Show("未能在120秒内检测到Steam登录窗口！");
                return;
            }

            // 3. 自动输入账号、密码
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
                        // 账号输入框
                        allEdits[0].Focus();
                        await Task.Delay(200);
                        Clipboard.SetText(accountToLogin.Username);
                        SendKeys.SendWait("^a");
                        await Task.Delay(80);
                        SendKeys.SendWait("^v");
                        await Task.Delay(200);

                        // 密码输入框
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
                        // 兜底（原方法）
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

            // 4. 自动输入Steam Guard验证码
            string maFilePath = SteamGuardHelper.FindMaFileForAccount(maFilesDir, accountToLogin.Username);
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
                {
                    MessageBox.Show("未能检测到验证码界面或未自动填入验证码，请手动操作！");
                }
            }
            // 循环检测CS2窗口，最多等120秒，每秒一次
            System.Diagnostics.Debug.WriteLine("开始循环检测CS2窗口...");
            IntPtr hwnd = IntPtr.Zero;
            for (int i = 0; i < 120; i++)
            {
                var proc = Process.GetProcessesByName("cs2").FirstOrDefault();
                hwnd = proc?.MainWindowHandle ?? IntPtr.Zero;
                System.Diagnostics.Debug.WriteLine($"[{i + 1}s] 通过进程名获取句柄: {hwnd}");
                if (hwnd != IntPtr.Zero)
                    break;

                hwnd = FindWindow(null, "Counter-Strike 2");
                System.Diagnostics.Debug.WriteLine($"[{i + 1}s] 通过窗口名查找句柄: {hwnd}");
                if (hwnd != IntPtr.Zero)
                    break;

                await Task.Delay(1000);
            }

            if (hwnd != IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("找到CS2窗口句柄，开始执行OCR自动点击。");
                string[] keywords = { "确定", "开始", "死亡竞赛", "不再显示", "关闭" };
                EasyOcrHelper.WaitAndClickButton(hwnd, new[] { "确定"}, 30, 1000);

            }
            else
            {
                System.Diagnostics.Debug.WriteLine("未找到CS2窗口！");
                MessageBox.Show("未找到CS2窗口！");
            }

        }


        // Win32 FindWindow声明（如果需要）
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        private void lstAccounts_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 可不实现
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            // 可不实现
        }
    }
}
