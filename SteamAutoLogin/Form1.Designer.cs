namespace SteamAutoLogin
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnReadExcel;
        private System.Windows.Forms.ListBox lstAccounts;
        private System.Windows.Forms.Button btnStartAutoLevel;


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
            btnStartAutoLevel = new Button();
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
            btnStartAutoLevel.Location = new Point(12, 130);
            btnStartAutoLevel.Name = "btnStartAutoLevel";
            btnStartAutoLevel.Size = new Size(120, 40);
            btnStartAutoLevel.TabIndex = 3;
            btnStartAutoLevel.Text = "自动刷级";
            btnStartAutoLevel.UseVisualStyleBackColor = true;
            btnStartAutoLevel.Click += btnStartAutoLevel_Click;

            // 
            // Form1
            // 
            ClientSize = new Size(800, 450);      
            Controls.Add(btnReadExcel);
            Controls.Add(lstAccounts);
            Controls.Add(btnStartAutoLevel);
            Name = "Form1";
            Text = "Steam Auto Login";
            Load += Form1_Load;
            ResumeLayout(false);
        }
    }
}
