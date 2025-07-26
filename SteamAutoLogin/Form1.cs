using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using FlaUI.Core;
using FlaUI.UIA3;

namespace SteamAutoLogin
{
    public partial class Form1 : Form
    {
        private ExcelService _excelService;
        private List<AccountInfo> _accounts; // �����ȡ�������˺�
        private string maFilesDir = @"F:\SDA\maFiles"; // ���maFiles�ļ���·��

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
                lstAccounts.Items.Add($"�˺�: {acc.Username}, ����: {acc.Password}, �Ƿ��������: {acc.IsUpgraded}");
            }
        }

        private async void btnAutoLogin_Click(object sender, EventArgs e)
        {
            if (_accounts == null || _accounts.Count == 0)
            {
                MessageBox.Show("���ȶ�ȡExcel�ļ���");
                return;
            }
            var accountToLogin = _accounts.FirstOrDefault(acc =>
                !string.IsNullOrWhiteSpace(acc.IsUpgraded) && acc.IsUpgraded.Trim() == "��");
            if (accountToLogin == null)
            {
                MessageBox.Show("�����˺Ŷ��������������¼��");
                return;
            }

            // ����Steam������CS2��������
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = @"H:\Steam\Steam.exe",  // �����㱾���� Steam ·��
                Arguments = "-applaunch 730 -sw -w 1280 -h 720 -novid",
                UseShellExecute = true
            });

            // 2. ��ѯ�ȴ�����¼ Steam�����ڵ��������120�룩
            IntPtr hwndLogin = IntPtr.Zero;
            for (int i = 0; i < 120; i++)
            {
                hwndLogin = FindWindow(null, "��¼ Steam"); // Ӣ��Ϊ "Steam Login"
                if (hwndLogin != IntPtr.Zero)
                    break;
                await Task.Delay(1000);
            }
            if (hwndLogin == IntPtr.Zero)
            {
                MessageBox.Show("δ����120���ڼ�⵽Steam��¼���ڣ�");
                return;
            }

            // 3. �Զ������˺š�����
            while (GetForegroundWindow() != hwndLogin)
            {
                SetForegroundWindow(hwndLogin);
                await Task.Delay(200);
            }

            using (var automation = new FlaUI.UIA3.UIA3Automation())
            {
                var steamLoginWin = automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("��¼ Steam"));
                if (steamLoginWin != null)
                {
                    var allElems = steamLoginWin.FindAllDescendants();
                    var allEdits = allElems.Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Edit).ToList();
                    if (allEdits.Count >= 2)
                    {
                        // �˺������
                        allEdits[0].Focus();
                        await Task.Delay(200);
                        Clipboard.SetText(accountToLogin.Username);
                        SendKeys.SendWait("^a");
                        await Task.Delay(80);
                        SendKeys.SendWait("^v");
                        await Task.Delay(200);

                        // ���������
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
                        // ���ף�ԭ������
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

            // 4. �Զ�����Steam Guard��֤��
            string maFilePath = SteamGuardHelper.FindMaFileForAccount(maFilesDir, accountToLogin.Username);
            if (maFilePath == null)
            {
                MessageBox.Show("δ�ҵ����˺ŵ�maFile���޷��Զ���ȡ��֤�룡");
                return;
            }

            using (var automation = new FlaUI.UIA3.UIA3Automation())
            {
                bool codeEntered = false;
                for (int i = 0; i < 40; i++)
                {
                    var win = automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("��¼ Steam"));
                    if (win != null)
                    {
                        var label = win.FindFirstDescendant(cf => cf.ByName("������ Steam �ֻ�Ӧ���ϵĴ���"));
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
                    MessageBox.Show("δ�ܼ�⵽��֤������δ�Զ�������֤�룬���ֶ�������");
                }
            }
        }

        // Win32�������API
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        private void lstAccounts_SelectedIndexChanged(object sender, EventArgs e)
        {
            // �ɲ�ʵ��
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            // �ɲ�ʵ��
        }
    }
}
