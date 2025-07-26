namespace SteamAutoLogin
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnReadExcel;
        private System.Windows.Forms.ListBox lstAccounts;
        private System.Windows.Forms.Button btnAutoLogin;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            btnReadExcel = new Button();
            lstAccounts = new ListBox();
            btnAutoLogin = new Button();
            SuspendLayout();
            // 
            // btnReadExcel
            // 
            btnReadExcel.Location = new Point(12, 12);
            btnReadExcel.Name = "btnReadExcel";
            btnReadExcel.Size = new Size(120, 40);
            btnReadExcel.TabIndex = 0;
            btnReadExcel.Text = "Read Excel";
            btnReadExcel.UseVisualStyleBackColor = true;
            btnReadExcel.Click += btnReadExcel_Click;
            // 
            // lstAccounts
            // 
            lstAccounts.FormattingEnabled = true;
            lstAccounts.Location = new Point(150, 12);
            lstAccounts.Name = "lstAccounts";
            lstAccounts.Size = new Size(638, 384);
            lstAccounts.TabIndex = 1;
            lstAccounts.SelectedIndexChanged += lstAccounts_SelectedIndexChanged;
            // 
            // btnAutoLogin
            // 
            btnAutoLogin.Location = new Point(12, 70);
            btnAutoLogin.Name = "btnAutoLogin";
            btnAutoLogin.Size = new Size(120, 40);
            btnAutoLogin.TabIndex = 2;
            btnAutoLogin.Text = "自动登录";
            btnAutoLogin.UseVisualStyleBackColor = true;
            btnAutoLogin.Click += btnAutoLogin_Click;
            // 
            // Form1
            // 
            ClientSize = new Size(800, 450);
            Controls.Add(btnAutoLogin);
            Controls.Add(btnReadExcel);
            Controls.Add(lstAccounts);
            Name = "Form1";
            Text = "Steam Auto Login";
            Load += Form1_Load;
            ResumeLayout(false);
        }
    }
}
