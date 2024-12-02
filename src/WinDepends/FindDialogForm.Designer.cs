namespace WinDepends
{
    partial class FindDialogForm
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
            label1 = new Label();
            FindTextBox = new TextBox();
            FindButton = new Button();
            button2 = new Button();
            MatchWholeCheckBox = new CheckBox();
            MatchCaseCheckBox = new CheckBox();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 16);
            label1.Name = "label1";
            label1.Size = new Size(62, 15);
            label1.TabIndex = 0;
            label1.Text = "Fi&nd what:";
            // 
            // FindTextBox
            // 
            FindTextBox.Location = new Point(79, 12);
            FindTextBox.Name = "FindTextBox";
            FindTextBox.Size = new Size(172, 23);
            FindTextBox.TabIndex = 1;
            FindTextBox.TextChanged += FindTextBox_TextChanged;
            // 
            // FindButton
            // 
            FindButton.Enabled = false;
            FindButton.Location = new Point(257, 12);
            FindButton.Name = "FindButton";
            FindButton.Size = new Size(75, 23);
            FindButton.TabIndex = 2;
            FindButton.Text = "&Find Next";
            FindButton.UseVisualStyleBackColor = true;
            FindButton.Click += FindButton_Click;
            // 
            // button2
            // 
            button2.DialogResult = DialogResult.Cancel;
            button2.Location = new Point(257, 41);
            button2.Name = "button2";
            button2.Size = new Size(75, 23);
            button2.TabIndex = 3;
            button2.Text = "Cancel";
            button2.UseVisualStyleBackColor = true;
            // 
            // MatchWholeCheckBox
            // 
            MatchWholeCheckBox.AutoSize = true;
            MatchWholeCheckBox.Location = new Point(11, 49);
            MatchWholeCheckBox.Name = "MatchWholeCheckBox";
            MatchWholeCheckBox.Size = new Size(156, 19);
            MatchWholeCheckBox.TabIndex = 4;
            MatchWholeCheckBox.Text = "Match &whole words only";
            MatchWholeCheckBox.UseVisualStyleBackColor = true;
            MatchWholeCheckBox.Click += MatchWholeCheckBox_Click;
            // 
            // MatchCaseCheckBox
            // 
            MatchCaseCheckBox.AutoSize = true;
            MatchCaseCheckBox.Location = new Point(11, 74);
            MatchCaseCheckBox.Name = "MatchCaseCheckBox";
            MatchCaseCheckBox.Size = new Size(86, 19);
            MatchCaseCheckBox.TabIndex = 5;
            MatchCaseCheckBox.Text = "Match &case";
            MatchCaseCheckBox.UseVisualStyleBackColor = true;
            MatchCaseCheckBox.Click += MatchCaseCheckBox_Click;
            // 
            // FindDialogForm
            // 
            AcceptButton = FindButton;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(339, 123);
            Controls.Add(MatchCaseCheckBox);
            Controls.Add(MatchWholeCheckBox);
            Controls.Add(button2);
            Controls.Add(FindButton);
            Controls.Add(FindTextBox);
            Controls.Add(label1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FindDialogForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Find";
            Load += FindDialogForm_Load;
            KeyDown += FindDialogForm_KeyDown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox FindTextBox;
        private Button FindButton;
        private Button button2;
        private CheckBox MatchWholeCheckBox;
        private CheckBox MatchCaseCheckBox;
    }
}