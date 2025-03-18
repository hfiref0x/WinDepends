namespace WinDepends
{
    partial class FileOpenForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FileOpenForm));
            buttonOK = new Button();
            buttonCancel = new Button();
            groupBox1 = new GroupBox();
            chBoxUseCustomImageBase = new CheckBox();
            label1 = new Label();
            textBoxCustomImageBase = new TextBox();
            labelAllocGran = new Label();
            chBoxProcessRelocs = new CheckBox();
            groupBox2 = new GroupBox();
            label3 = new Label();
            chBoxPropagateSettings = new CheckBox();
            chBoxAnalysisDefaultEnabled = new CheckBox();
            groupBox13 = new GroupBox();
            chBoxUseStats = new CheckBox();
            label2 = new Label();
            textBoxFileName = new TextBox();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox13.SuspendLayout();
            SuspendLayout();
            // 
            // buttonOK
            // 
            buttonOK.DialogResult = DialogResult.OK;
            buttonOK.Location = new Point(112, 387);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new Size(75, 24);
            buttonOK.TabIndex = 0;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += ButtonOK_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(193, 387);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(75, 24);
            buttonCancel.TabIndex = 1;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(chBoxUseCustomImageBase);
            groupBox1.Controls.Add(label1);
            groupBox1.Controls.Add(textBoxCustomImageBase);
            groupBox1.Controls.Add(labelAllocGran);
            groupBox1.Controls.Add(chBoxProcessRelocs);
            groupBox1.Location = new Point(12, 44);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(346, 151);
            groupBox1.TabIndex = 2;
            groupBox1.TabStop = false;
            groupBox1.Text = "Loader";
            // 
            // chBoxUseCustomImageBase
            // 
            chBoxUseCustomImageBase.AutoSize = true;
            chBoxUseCustomImageBase.Location = new Point(9, 55);
            chBoxUseCustomImageBase.Name = "chBoxUseCustomImageBase";
            chBoxUseCustomImageBase.Size = new Size(151, 19);
            chBoxUseCustomImageBase.TabIndex = 14;
            chBoxUseCustomImageBase.Text = "Use custom image base";
            chBoxUseCustomImageBase.UseVisualStyleBackColor = true;
            chBoxUseCustomImageBase.Click += ChBoxUseCustomImageBase;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(9, 83);
            label1.Name = "label1";
            label1.Size = new Size(97, 15);
            label1.TabIndex = 13;
            label1.Text = "Image base (hex)";
            // 
            // textBoxCustomImageBase
            // 
            textBoxCustomImageBase.Location = new Point(142, 80);
            textBoxCustomImageBase.MaxLength = 8;
            textBoxCustomImageBase.Name = "textBoxCustomImageBase";
            textBoxCustomImageBase.Size = new Size(186, 23);
            textBoxCustomImageBase.TabIndex = 12;
            textBoxCustomImageBase.Text = "0";
            textBoxCustomImageBase.KeyPress += TextBoxMinAppAddress_KeyPress;
            // 
            // labelAllocGran
            // 
            labelAllocGran.AutoSize = true;
            labelAllocGran.Location = new Point(9, 113);
            labelAllocGran.Name = "labelAllocGran";
            labelAllocGran.Size = new Size(25, 15);
            labelAllocGran.TabIndex = 11;
            labelAllocGran.Text = "0x0";
            // 
            // chBoxProcessRelocs
            // 
            chBoxProcessRelocs.AutoSize = true;
            chBoxProcessRelocs.Location = new Point(9, 31);
            chBoxProcessRelocs.Name = "chBoxProcessRelocs";
            chBoxProcessRelocs.Size = new Size(241, 19);
            chBoxProcessRelocs.TabIndex = 0;
            chBoxProcessRelocs.Text = "Process relocations while parsing images";
            chBoxProcessRelocs.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(label3);
            groupBox2.Controls.Add(chBoxPropagateSettings);
            groupBox2.Controls.Add(chBoxAnalysisDefaultEnabled);
            groupBox2.Location = new Point(12, 264);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(348, 113);
            groupBox2.TabIndex = 3;
            groupBox2.TabStop = false;
            groupBox2.Text = "Analysis";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(9, 86);
            label3.Name = "label3";
            label3.Size = new Size(276, 15);
            label3.TabIndex = 18;
            label3.Text = "*You can reset this through program Configuration";
            // 
            // chBoxPropagateSettings
            // 
            chBoxPropagateSettings.AutoSize = true;
            chBoxPropagateSettings.Location = new Point(9, 22);
            chBoxPropagateSettings.Name = "chBoxPropagateSettings";
            chBoxPropagateSettings.Size = new Size(261, 19);
            chBoxPropagateSettings.TabIndex = 17;
            chBoxPropagateSettings.Tag = "603";
            chBoxPropagateSettings.Text = "Propagate analysis settings on dependencies";
            chBoxPropagateSettings.UseVisualStyleBackColor = true;
            // 
            // chBoxAnalysisDefaultEnabled
            // 
            chBoxAnalysisDefaultEnabled.AutoSize = true;
            chBoxAnalysisDefaultEnabled.Location = new Point(9, 47);
            chBoxAnalysisDefaultEnabled.Name = "chBoxAnalysisDefaultEnabled";
            chBoxAnalysisDefaultEnabled.Size = new Size(324, 19);
            chBoxAnalysisDefaultEnabled.TabIndex = 16;
            chBoxAnalysisDefaultEnabled.Tag = "602";
            chBoxAnalysisDefaultEnabled.Text = "Make analysis settings default and do not ask everytime*";
            chBoxAnalysisDefaultEnabled.UseVisualStyleBackColor = true;
            // 
            // groupBox13
            // 
            groupBox13.Controls.Add(chBoxUseStats);
            groupBox13.Location = new Point(12, 201);
            groupBox13.Name = "groupBox13";
            groupBox13.Size = new Size(346, 57);
            groupBox13.TabIndex = 9;
            groupBox13.TabStop = false;
            groupBox13.Text = "Statistics";
            // 
            // chBoxUseStats
            // 
            chBoxUseStats.AutoSize = true;
            chBoxUseStats.Location = new Point(9, 22);
            chBoxUseStats.Name = "chBoxUseStats";
            chBoxUseStats.Size = new Size(200, 19);
            chBoxUseStats.TabIndex = 9;
            chBoxUseStats.Tag = "601";
            chBoxUseStats.Text = "Enable transport statistics display";
            chBoxUseStats.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 16);
            label2.Name = "label2";
            label2.Size = new Size(55, 15);
            label2.TabIndex = 10;
            label2.Text = "Load file:";
            // 
            // textBoxFileName
            // 
            textBoxFileName.Location = new Point(73, 13);
            textBoxFileName.Name = "textBoxFileName";
            textBoxFileName.ReadOnly = true;
            textBoxFileName.Size = new Size(285, 23);
            textBoxFileName.TabIndex = 11;
            // 
            // FileOpenForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(370, 418);
            Controls.Add(textBoxFileName);
            Controls.Add(label2);
            Controls.Add(groupBox13);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(buttonCancel);
            Controls.Add(buttonOK);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FileOpenForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Load a new file";
            Load += FileOpenForm_Load;
            KeyDown += FileOpenForm_KeyDown;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            groupBox13.ResumeLayout(false);
            groupBox13.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button buttonOK;
        private Button buttonCancel;
        private GroupBox groupBox1;
        private CheckBox chBoxProcessRelocs;
        private Label labelAllocGran;
        private TextBox textBoxCustomImageBase;
        private Label label1;
        private GroupBox groupBox2;
        private CheckBox chBoxPropagateSettings;
        private CheckBox chBoxAnalysisDefaultEnabled;
        private GroupBox groupBox13;
        private CheckBox chBoxUseStats;
        private Label label2;
        private TextBox textBoxFileName;
        private Label label3;
        private CheckBox chBoxUseCustomImageBase;
    }
}