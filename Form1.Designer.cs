using System.Drawing;
using System.Windows.Forms;

namespace SteamAutoLogin
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnReadExcel;
        private System.Windows.Forms.ListBox lstAccounts;
        private System.Windows.Forms.Button btnAutoLogin;
        private System.Windows.Forms.Button btnGetCs2Stats;
        private System.Windows.Forms.Label lblCs2Stats;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnReadExcel = new Button();
            lstAccounts = new ListBox();
            btnAutoLogin = new Button();
            btnGetCs2Stats = new Button();
            lblCs2Stats = new Label();
            SuspendLayout();
            // 
            // btnReadExcel
            // 
            btnReadExcel.Location = new Point(12, 12);
            btnReadExcel.Name = "btnReadExcel";
            btnReadExcel.Size = new Size(120, 40);
            btnReadExcel.TabIndex = 0;
            btnReadExcel.Text = "读取Excel";
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
            // btnGetCs2Stats
            // 
            btnGetCs2Stats.Location = new Point(12, 128);
            btnGetCs2Stats.Name = "btnGetCs2Stats";
            btnGetCs2Stats.Size = new Size(120, 40);
            btnGetCs2Stats.TabIndex = 3;
            btnGetCs2Stats.Text = "查询CS2等级";
            btnGetCs2Stats.UseVisualStyleBackColor = true;
            btnGetCs2Stats.Click += btnGetCs2Stats_Click;
            // 
            // lblCs2Stats
            // 
            lblCs2Stats.AutoSize = true;
            lblCs2Stats.Location = new Point(12, 186);
            lblCs2Stats.Name = "lblCs2Stats";
            lblCs2Stats.TabIndex = 4;
            lblCs2Stats.Text = "";
            // 
            // Form1
            // 
            ClientSize = new Size(800, 450);
            Controls.Add(lblCs2Stats);
            Controls.Add(btnGetCs2Stats);
            Controls.Add(btnAutoLogin);
            Controls.Add(btnReadExcel);
            Controls.Add(lstAccounts);
            Name = "Form1";
            Text = "Steam Auto Login";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }
    }
}