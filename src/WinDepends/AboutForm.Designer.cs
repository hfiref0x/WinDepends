namespace WinDepends
{
    partial class AboutForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutForm));
            button1 = new Button();
            groupBox2 = new GroupBox();
            AboutAssemblyLabel = new Label();
            AboutBuildLabel = new Label();
            AboutVersionLabel = new Label();
            AboutCopyrightLabel = new Label();
            pictureBox2 = new PictureBox();
            AboutNameLabel = new Label();
            label2 = new Label();
            groupBox1 = new GroupBox();
            AboutOSLabel = new Label();
            linkLabel1 = new LinkLabel();
            label3 = new Label();
            linkLabel2 = new LinkLabel();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // button1
            // 
            button1.DialogResult = DialogResult.OK;
            button1.Location = new Point(276, 321);
            button1.Name = "button1";
            button1.Size = new Size(99, 25);
            button1.TabIndex = 0;
            button1.Text = "OK";
            button1.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(AboutAssemblyLabel);
            groupBox2.Controls.Add(AboutBuildLabel);
            groupBox2.Controls.Add(AboutVersionLabel);
            groupBox2.Location = new Point(12, 139);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(363, 107);
            groupBox2.TabIndex = 2;
            groupBox2.TabStop = false;
            groupBox2.Text = "Build information";
            // 
            // AboutAssemblyLabel
            // 
            AboutAssemblyLabel.AutoSize = true;
            AboutAssemblyLabel.Location = new Point(14, 78);
            AboutAssemblyLabel.Name = "AboutAssemblyLabel";
            AboutAssemblyLabel.Size = new Size(58, 15);
            AboutAssemblyLabel.TabIndex = 5;
            AboutAssemblyLabel.Text = "Assembly";
            // 
            // AboutBuildLabel
            // 
            AboutBuildLabel.AutoSize = true;
            AboutBuildLabel.Location = new Point(14, 53);
            AboutBuildLabel.Name = "AboutBuildLabel";
            AboutBuildLabel.Size = new Size(48, 15);
            AboutBuildLabel.TabIndex = 4;
            AboutBuildLabel.Text = "Built on";
            // 
            // AboutVersionLabel
            // 
            AboutVersionLabel.AutoSize = true;
            AboutVersionLabel.Location = new Point(14, 28);
            AboutVersionLabel.Name = "AboutVersionLabel";
            AboutVersionLabel.Size = new Size(45, 15);
            AboutVersionLabel.TabIndex = 1;
            AboutVersionLabel.Text = "Version";
            // 
            // AboutCopyrightLabel
            // 
            AboutCopyrightLabel.AutoSize = true;
            AboutCopyrightLabel.Location = new Point(56, 32);
            AboutCopyrightLabel.Name = "AboutCopyrightLabel";
            AboutCopyrightLabel.Size = new Size(141, 15);
            AboutCopyrightLabel.TabIndex = 3;
            AboutCopyrightLabel.Text = "Copyright (C) 2024 - 2025";
            // 
            // pictureBox2
            // 
            pictureBox2.Image = (Image)resources.GetObject("pictureBox2.Image");
            pictureBox2.InitialImage = null;
            pictureBox2.Location = new Point(12, 12);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(32, 32);
            pictureBox2.SizeMode = PictureBoxSizeMode.AutoSize;
            pictureBox2.TabIndex = 7;
            pictureBox2.TabStop = false;
            // 
            // AboutNameLabel
            // 
            AboutNameLabel.AutoSize = true;
            AboutNameLabel.Location = new Point(56, 12);
            AboutNameLabel.Name = "AboutNameLabel";
            AboutNameLabel.Size = new Size(15, 15);
            AboutNameLabel.TabIndex = 5;
            AboutNameLabel.Text = "~";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(56, 55);
            label2.Name = "label2";
            label2.Size = new Size(257, 15);
            label2.TabIndex = 8;
            label2.Text = "This is free software, you use it at your own risk!";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(AboutOSLabel);
            groupBox1.Location = new Point(12, 252);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(363, 63);
            groupBox1.TabIndex = 9;
            groupBox1.TabStop = false;
            groupBox1.Text = "Operation system";
            // 
            // AboutOSLabel
            // 
            AboutOSLabel.AutoSize = true;
            AboutOSLabel.Location = new Point(14, 28);
            AboutOSLabel.Name = "AboutOSLabel";
            AboutOSLabel.Size = new Size(0, 15);
            AboutOSLabel.TabIndex = 1;
            // 
            // linkLabel1
            // 
            linkLabel1.AutoSize = true;
            linkLabel1.Location = new Point(56, 95);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new Size(284, 15);
            linkLabel1.TabIndex = 10;
            linkLabel1.TabStop = true;
            linkLabel1.Text = "For updates please visit WinDepends on GitHub.com";
            linkLabel1.LinkClicked += LinkLabel1_LinkClicked;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(56, 75);
            label3.Name = "label3";
            label3.Size = new Size(230, 15);
            label3.TabIndex = 12;
            label3.Text = "Licensed under MIT software license terms\r\n";
            // 
            // linkLabel2
            // 
            linkLabel2.AutoSize = true;
            linkLabel2.Location = new Point(56, 115);
            linkLabel2.Name = "linkLabel2";
            linkLabel2.Size = new Size(238, 15);
            linkLabel2.TabIndex = 13;
            linkLabel2.TabStop = true;
            linkLabel2.Text = "Based on software created by Steve P. Miller";
            linkLabel2.LinkClicked += LinkLabel2_Clicked;
            // 
            // AboutForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(385, 358);
            Controls.Add(linkLabel2);
            Controls.Add(label3);
            Controls.Add(linkLabel1);
            Controls.Add(groupBox1);
            Controls.Add(label2);
            Controls.Add(pictureBox2);
            Controls.Add(AboutCopyrightLabel);
            Controls.Add(AboutNameLabel);
            Controls.Add(groupBox2);
            Controls.Add(button1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Load += AboutForm_Load;
            KeyDown += AboutForm_KeyDown;
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private GroupBox groupBox2;
        private Label AboutBuildLabel;
        private Label AboutCopyrightLabel;
        private Label AboutVersionLabel;
        private PictureBox pictureBox2;
        private Label AboutNameLabel;
        private Label label2;
        private Label AboutAssemblyLabel;
        private GroupBox groupBox1;
        private Label AboutOSLabel;
        private LinkLabel linkLabel1;
        private Label label3;
        private LinkLabel linkLabel2;
    }
}