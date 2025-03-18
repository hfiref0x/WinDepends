namespace WinDepends
{
    partial class ConfigurationForm
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
            TreeNode treeNode12 = new TreeNode("History");
            TreeNode treeNode13 = new TreeNode("Main", new TreeNode[] { treeNode12 });
            TreeNode treeNode14 = new TreeNode("Analysis (global)");
            TreeNode treeNode15 = new TreeNode("ApiSets");
            TreeNode treeNode16 = new TreeNode("External Function Help");
            TreeNode treeNode17 = new TreeNode("External Module Viewer");
            TreeNode treeNode18 = new TreeNode("Handled File Extensions");
            TreeNode treeNode19 = new TreeNode("Module Search Order");
            TreeNode treeNode20 = new TreeNode("Module Search Order (drivers)");
            TreeNode treeNode21 = new TreeNode("Server");
            TreeNode treeNode22 = new TreeNode("Symbols");
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConfigurationForm));
            splitContainer2 = new SplitContainer();
            splitContainer1 = new SplitContainer();
            TVSettings = new TreeView();
            settingsTabControl = new TabControl();
            tabMain = new TabPage();
            groupBox8 = new GroupBox();
            label12 = new Label();
            chBoxCompressSessionFiles = new CheckBox();
            groupBox3 = new GroupBox();
            chBoxClearLogOnFileOpen = new CheckBox();
            chBoxFullPaths = new CheckBox();
            label10 = new Label();
            label8 = new Label();
            nodeMaxDepthUpDown = new NumericUpDown();
            label9 = new Label();
            chBoxUpperCase = new CheckBox();
            chBoxResolveApiSets = new CheckBox();
            chBoxUndecorateSymbols = new CheckBox();
            chBoxAutoExpands = new CheckBox();
            groupBox2 = new GroupBox();
            checkBox1 = new CheckBox();
            tabMainHistory = new TabPage();
            groupBox6 = new GroupBox();
            cbHistoryFullPath = new CheckBox();
            label2 = new Label();
            historyUpDown = new NumericUpDown();
            label1 = new Label();
            tabAnalysis = new TabPage();
            groupBox14 = new GroupBox();
            chBoxPropagateSettings = new CheckBox();
            chBoxAnalysisDefaultEnabled = new CheckBox();
            groupBox13 = new GroupBox();
            chBoxUseStats = new CheckBox();
            groupBox11 = new GroupBox();
            chBoxCustomImageBase = new CheckBox();
            labelAllocGran = new Label();
            label14 = new Label();
            label13 = new Label();
            cbCustomImageBase = new ComboBox();
            chBoxProcessRelocs = new CheckBox();
            tabApiSet = new TabPage();
            groupBox15 = new GroupBox();
            labelApiSetCount = new Label();
            labelApiSetVersion = new Label();
            groupBox10 = new GroupBox();
            chBoxHighlightApiSet = new CheckBox();
            groupBox9 = new GroupBox();
            apisetTextBox = new TextBox();
            buttonApiSetBrowse = new Button();
            chBoxApiSetNamespace = new CheckBox();
            tabExternalFunctionHelp = new TabPage();
            groupBox5 = new GroupBox();
            buttonDefaultURL = new Button();
            searchOnlineTextBox = new TextBox();
            label6 = new Label();
            tabExternalViewer = new TabPage();
            groupBox4 = new GroupBox();
            buttonBrowse = new Button();
            argumentsTextBox = new TextBox();
            label5 = new Label();
            commandTextBox = new TextBox();
            label3 = new Label();
            tabHandledFileExtensions = new TabPage();
            buttonElevate = new Button();
            groupBox1 = new GroupBox();
            customExtBox = new TextBox();
            label4 = new Label();
            buttonAssociate = new Button();
            buttonSelectAll = new Button();
            LVFileExt = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            shellIntegrationWarningLabel = new Label();
            tabSearchOrder = new TabPage();
            DeleteUserDirectoryButton = new Button();
            AddUserDirectoryButton = new Button();
            TVSearchOrder = new TreeView();
            ExpandSearchOrderButton = new Button();
            MoveUpButton = new Button();
            MoveDownButton = new Button();
            tabSearchOrderDrivers = new TabPage();
            DeleteUserDirectoryDriversButton = new Button();
            AddUserDirectoryDriversButton = new Button();
            TVSearchOrderDrivers = new TreeView();
            ExpandSearchOrderDrivers = new Button();
            MoveUpButtonDrivers = new Button();
            MoveDownButtonDrivers = new Button();
            tabServer = new TabPage();
            groupBox7 = new GroupBox();
            ServerFileState = new Label();
            buttonBrowseServerApp = new Button();
            serverAppLocationTextBox = new TextBox();
            groupBox12 = new GroupBox();
            labelSrvPid = new Label();
            label7 = new Label();
            labelSrvPort = new Label();
            label16 = new Label();
            buttonServerConnect = new Button();
            labelServerStatus = new Label();
            label15 = new Label();
            tabSymbols = new TabPage();
            chBoxUseSymbols = new CheckBox();
            groupBoxSymbols = new GroupBox();
            buttonSymbolsDefault = new Button();
            buttonSymbolPickColor = new Button();
            panelSymColor = new Panel();
            label11 = new Label();
            buttonSymbolsBrowse = new Button();
            symbolsStoreTextBox = new TextBox();
            label19 = new Label();
            buttonDbghelpBrowse = new Button();
            dbghelpTextBox = new TextBox();
            label18 = new Label();
            configCancel = new Button();
            configOK = new Button();
            browseFileDialog = new OpenFileDialog();
            folderBrowserDialog = new FolderBrowserDialog();
            colorDialog = new ColorDialog();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            settingsTabControl.SuspendLayout();
            tabMain.SuspendLayout();
            groupBox8.SuspendLayout();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nodeMaxDepthUpDown).BeginInit();
            groupBox2.SuspendLayout();
            tabMainHistory.SuspendLayout();
            groupBox6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)historyUpDown).BeginInit();
            tabAnalysis.SuspendLayout();
            groupBox14.SuspendLayout();
            groupBox13.SuspendLayout();
            groupBox11.SuspendLayout();
            tabApiSet.SuspendLayout();
            groupBox15.SuspendLayout();
            groupBox10.SuspendLayout();
            groupBox9.SuspendLayout();
            tabExternalFunctionHelp.SuspendLayout();
            groupBox5.SuspendLayout();
            tabExternalViewer.SuspendLayout();
            groupBox4.SuspendLayout();
            tabHandledFileExtensions.SuspendLayout();
            groupBox1.SuspendLayout();
            tabSearchOrder.SuspendLayout();
            tabSearchOrderDrivers.SuspendLayout();
            tabServer.SuspendLayout();
            groupBox7.SuspendLayout();
            groupBox12.SuspendLayout();
            tabSymbols.SuspendLayout();
            groupBoxSymbols.SuspendLayout();
            panelSymColor.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.IsSplitterFixed = true;
            splitContainer2.Location = new Point(5, 5);
            splitContainer2.Name = "splitContainer2";
            splitContainer2.Orientation = Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(splitContainer1);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(configCancel);
            splitContainer2.Panel2.Controls.Add(configOK);
            splitContainer2.Size = new Size(693, 497);
            splitContainer2.SplitterDistance = 444;
            splitContainer2.TabIndex = 1;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.IsSplitterFixed = true;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(TVSettings);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(settingsTabControl);
            splitContainer1.Size = new Size(693, 444);
            splitContainer1.SplitterDistance = 201;
            splitContainer1.TabIndex = 1;
            // 
            // TVSettings
            // 
            TVSettings.Dock = DockStyle.Fill;
            TVSettings.HideSelection = false;
            TVSettings.Location = new Point(0, 0);
            TVSettings.Name = "TVSettings";
            treeNode12.Name = "HistoryNode";
            treeNode12.Tag = "11";
            treeNode12.Text = "History";
            treeNode13.Name = "MainWindowNode";
            treeNode13.Tag = "10";
            treeNode13.Text = "Main";
            treeNode14.Name = "AnalysisNode";
            treeNode14.Tag = "20";
            treeNode14.Text = "Analysis (global)";
            treeNode15.Name = "ApiSetsNode";
            treeNode15.Tag = "30";
            treeNode15.Text = "ApiSets";
            treeNode16.Name = "xternalFunctionHelpNode";
            treeNode16.Tag = "40";
            treeNode16.Text = "External Function Help";
            treeNode17.Name = "ExternalModuleViewerNode";
            treeNode17.Tag = "50";
            treeNode17.Text = "External Module Viewer";
            treeNode18.Name = "ShellIntegrationNode";
            treeNode18.Tag = "60";
            treeNode18.Text = "Handled File Extensions";
            treeNode19.Name = "SearchOrderNode";
            treeNode19.Tag = "70";
            treeNode19.Text = "Module Search Order";
            treeNode20.Name = "SearchOrderDriverNode";
            treeNode20.Tag = "80";
            treeNode20.Text = "Module Search Order (drivers)";
            treeNode21.Name = "ServerNode";
            treeNode21.Tag = "90";
            treeNode21.Text = "Server";
            treeNode22.Name = "SymbolsNode";
            treeNode22.Tag = "100";
            treeNode22.Text = "Symbols";
            TVSettings.Nodes.AddRange(new TreeNode[] { treeNode13, treeNode14, treeNode15, treeNode16, treeNode17, treeNode18, treeNode19, treeNode20, treeNode21, treeNode22 });
            TVSettings.Size = new Size(201, 444);
            TVSettings.TabIndex = 0;
            TVSettings.AfterSelect += TVSettings_AfterSelect;
            // 
            // settingsTabControl
            // 
            settingsTabControl.Appearance = TabAppearance.FlatButtons;
            settingsTabControl.Controls.Add(tabMain);
            settingsTabControl.Controls.Add(tabMainHistory);
            settingsTabControl.Controls.Add(tabAnalysis);
            settingsTabControl.Controls.Add(tabApiSet);
            settingsTabControl.Controls.Add(tabExternalFunctionHelp);
            settingsTabControl.Controls.Add(tabExternalViewer);
            settingsTabControl.Controls.Add(tabHandledFileExtensions);
            settingsTabControl.Controls.Add(tabSearchOrder);
            settingsTabControl.Controls.Add(tabSearchOrderDrivers);
            settingsTabControl.Controls.Add(tabServer);
            settingsTabControl.Controls.Add(tabSymbols);
            settingsTabControl.Dock = DockStyle.Fill;
            settingsTabControl.ItemSize = new Size(0, 1);
            settingsTabControl.Location = new Point(0, 0);
            settingsTabControl.Name = "settingsTabControl";
            settingsTabControl.SelectedIndex = 0;
            settingsTabControl.Size = new Size(488, 444);
            settingsTabControl.SizeMode = TabSizeMode.Fixed;
            settingsTabControl.TabIndex = 0;
            // 
            // tabMain
            // 
            tabMain.Controls.Add(groupBox8);
            tabMain.Controls.Add(groupBox3);
            tabMain.Controls.Add(groupBox2);
            tabMain.Location = new Point(4, 5);
            tabMain.Name = "tabMain";
            tabMain.Padding = new Padding(3);
            tabMain.Size = new Size(480, 435);
            tabMain.TabIndex = 0;
            tabMain.Tag = "10";
            tabMain.UseVisualStyleBackColor = true;
            // 
            // groupBox8
            // 
            groupBox8.Controls.Add(label12);
            groupBox8.Controls.Add(chBoxCompressSessionFiles);
            groupBox8.Location = new Point(6, 250);
            groupBox8.Name = "groupBox8";
            groupBox8.Size = new Size(468, 76);
            groupBox8.TabIndex = 10;
            groupBox8.TabStop = false;
            groupBox8.Text = "Session";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new Point(19, 45);
            label12.Name = "label12";
            label12.Size = new Size(277, 15);
            label12.TabIndex = 1;
            label12.Text = "Note: On by default, recommended to keep it as-is.";
            // 
            // chBoxCompressSessionFiles
            // 
            chBoxCompressSessionFiles.AutoSize = true;
            chBoxCompressSessionFiles.Location = new Point(19, 23);
            chBoxCompressSessionFiles.Name = "chBoxCompressSessionFiles";
            chBoxCompressSessionFiles.Size = new Size(144, 19);
            chBoxCompressSessionFiles.TabIndex = 0;
            chBoxCompressSessionFiles.Tag = "400";
            chBoxCompressSessionFiles.Text = "Compress session files";
            chBoxCompressSessionFiles.UseVisualStyleBackColor = true;
            chBoxCompressSessionFiles.Click += ChBox_Click;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(chBoxClearLogOnFileOpen);
            groupBox3.Controls.Add(chBoxFullPaths);
            groupBox3.Controls.Add(label10);
            groupBox3.Controls.Add(label8);
            groupBox3.Controls.Add(nodeMaxDepthUpDown);
            groupBox3.Controls.Add(label9);
            groupBox3.Controls.Add(chBoxUpperCase);
            groupBox3.Controls.Add(chBoxResolveApiSets);
            groupBox3.Controls.Add(chBoxUndecorateSymbols);
            groupBox3.Controls.Add(chBoxAutoExpands);
            groupBox3.Location = new Point(6, 71);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(468, 175);
            groupBox3.TabIndex = 8;
            groupBox3.TabStop = false;
            groupBox3.Text = "Appearance";
            // 
            // chBoxClearLogOnFileOpen
            // 
            chBoxClearLogOnFileOpen.AutoSize = true;
            chBoxClearLogOnFileOpen.Location = new Point(216, 83);
            chBoxClearLogOnFileOpen.Name = "chBoxClearLogOnFileOpen";
            chBoxClearLogOnFileOpen.Size = new Size(193, 19);
            chBoxClearLogOnFileOpen.TabIndex = 18;
            chBoxClearLogOnFileOpen.Tag = "125";
            chBoxClearLogOnFileOpen.Text = "Clear Log Window on File Open";
            chBoxClearLogOnFileOpen.UseVisualStyleBackColor = true;
            chBoxClearLogOnFileOpen.Click += ChBox_Click;
            // 
            // chBoxFullPaths
            // 
            chBoxFullPaths.AutoSize = true;
            chBoxFullPaths.Location = new Point(216, 33);
            chBoxFullPaths.Name = "chBoxFullPaths";
            chBoxFullPaths.Size = new Size(153, 19);
            chBoxFullPaths.TabIndex = 8;
            chBoxFullPaths.Tag = "121";
            chBoxFullPaths.Text = "Show Module Full Paths";
            chBoxFullPaths.UseVisualStyleBackColor = true;
            chBoxFullPaths.Click += ChBox_Click;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label10.ForeColor = Color.Black;
            label10.Location = new Point(19, 146);
            label10.Name = "label10";
            label10.Size = new Size(345, 15);
            label10.TabIndex = 16;
            label10.Text = "Warning: Increasing depth value drastically affects performance.";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(193, 120);
            label8.Name = "label8";
            label8.Size = new Size(100, 15);
            label8.TabIndex = 15;
            label8.Text = "[1..100], default: 2";
            // 
            // nodeMaxDepthUpDown
            // 
            nodeMaxDepthUpDown.Location = new Point(135, 116);
            nodeMaxDepthUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            nodeMaxDepthUpDown.Name = "nodeMaxDepthUpDown";
            nodeMaxDepthUpDown.Size = new Size(52, 23);
            nodeMaxDepthUpDown.TabIndex = 14;
            nodeMaxDepthUpDown.Value = new decimal(new int[] { 2, 0, 0, 0 });
            nodeMaxDepthUpDown.ValueChanged += NodeMaxDepth_ValueChanged;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(19, 120);
            label9.Name = "label9";
            label9.Size = new Size(112, 15);
            label9.TabIndex = 13;
            label9.Text = "Modules Tree Depth";
            // 
            // chBoxUpperCase
            // 
            chBoxUpperCase.AutoSize = true;
            chBoxUpperCase.Location = new Point(19, 83);
            chBoxUpperCase.Name = "chBoxUpperCase";
            chBoxUpperCase.Size = new Size(191, 19);
            chBoxUpperCase.TabIndex = 12;
            chBoxUpperCase.Tag = "124";
            chBoxUpperCase.Text = "Upper Case Module File Names";
            chBoxUpperCase.UseVisualStyleBackColor = true;
            chBoxUpperCase.Click += ChBox_Click;
            // 
            // chBoxResolveApiSets
            // 
            chBoxResolveApiSets.AutoSize = true;
            chBoxResolveApiSets.Location = new Point(216, 58);
            chBoxResolveApiSets.Name = "chBoxResolveApiSets";
            chBoxResolveApiSets.Size = new Size(149, 19);
            chBoxResolveApiSets.TabIndex = 11;
            chBoxResolveApiSets.Tag = "123";
            chBoxResolveApiSets.Text = "Show Resolved API sets";
            chBoxResolveApiSets.UseVisualStyleBackColor = true;
            chBoxResolveApiSets.Click += ChBox_Click;
            // 
            // chBoxUndecorateSymbols
            // 
            chBoxUndecorateSymbols.AutoSize = true;
            chBoxUndecorateSymbols.Location = new Point(19, 58);
            chBoxUndecorateSymbols.Name = "chBoxUndecorateSymbols";
            chBoxUndecorateSymbols.Size = new Size(169, 19);
            chBoxUndecorateSymbols.TabIndex = 10;
            chBoxUndecorateSymbols.Tag = "122";
            chBoxUndecorateSymbols.Text = "Undecorate C++ Functions";
            chBoxUndecorateSymbols.UseVisualStyleBackColor = true;
            chBoxUndecorateSymbols.Click += ChBox_Click;
            // 
            // chBoxAutoExpands
            // 
            chBoxAutoExpands.AutoSize = true;
            chBoxAutoExpands.Location = new Point(19, 33);
            chBoxAutoExpands.Name = "chBoxAutoExpands";
            chBoxAutoExpands.Size = new Size(162, 19);
            chBoxAutoExpands.TabIndex = 9;
            chBoxAutoExpands.Tag = "120";
            chBoxAutoExpands.Text = "Auto Expand Module Tree";
            chBoxAutoExpands.UseVisualStyleBackColor = true;
            chBoxAutoExpands.Click += ChBox_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(checkBox1);
            groupBox2.Location = new Point(6, 6);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(468, 61);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "Windows";
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(19, 30);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(178, 19);
            checkBox1.TabIndex = 1;
            checkBox1.Tag = "100";
            checkBox1.Text = "Use ESC key to close window";
            checkBox1.UseVisualStyleBackColor = true;
            checkBox1.Click += ChBox_Click;
            // 
            // tabMainHistory
            // 
            tabMainHistory.Controls.Add(groupBox6);
            tabMainHistory.Location = new Point(4, 5);
            tabMainHistory.Name = "tabMainHistory";
            tabMainHistory.Padding = new Padding(3);
            tabMainHistory.Size = new Size(480, 435);
            tabMainHistory.TabIndex = 2;
            tabMainHistory.Tag = "11";
            tabMainHistory.UseVisualStyleBackColor = true;
            // 
            // groupBox6
            // 
            groupBox6.Controls.Add(cbHistoryFullPath);
            groupBox6.Controls.Add(label2);
            groupBox6.Controls.Add(historyUpDown);
            groupBox6.Controls.Add(label1);
            groupBox6.Location = new Point(6, 6);
            groupBox6.Name = "groupBox6";
            groupBox6.Size = new Size(465, 121);
            groupBox6.TabIndex = 4;
            groupBox6.TabStop = false;
            groupBox6.Text = "Most Recently Used list";
            // 
            // cbHistoryFullPath
            // 
            cbHistoryFullPath.AutoSize = true;
            cbHistoryFullPath.Location = new Point(23, 77);
            cbHistoryFullPath.Name = "cbHistoryFullPath";
            cbHistoryFullPath.Size = new Size(249, 19);
            cbHistoryFullPath.TabIndex = 5;
            cbHistoryFullPath.Text = "Display full path in Most Recently Used list";
            cbHistoryFullPath.UseVisualStyleBackColor = true;
            cbHistoryFullPath.Click += HistoryFullPath_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(126, 41);
            label2.Name = "label2";
            label2.Size = new Size(39, 15);
            label2.TabIndex = 4;
            label2.Text = "[0..32]";
            // 
            // historyUpDown
            // 
            historyUpDown.Location = new Point(68, 36);
            historyUpDown.Maximum = new decimal(new int[] { 64, 0, 0, 0 });
            historyUpDown.Name = "historyUpDown";
            historyUpDown.Size = new Size(52, 23);
            historyUpDown.TabIndex = 3;
            historyUpDown.Value = new decimal(new int[] { 10, 0, 0, 0 });
            historyUpDown.ValueChanged += HistoryUpDown_ValueChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(23, 41);
            label1.Name = "label1";
            label1.Size = new Size(39, 15);
            label1.TabIndex = 2;
            label1.Text = "Depth";
            // 
            // tabAnalysis
            // 
            tabAnalysis.Controls.Add(groupBox14);
            tabAnalysis.Controls.Add(groupBox13);
            tabAnalysis.Controls.Add(groupBox11);
            tabAnalysis.Location = new Point(4, 5);
            tabAnalysis.Name = "tabAnalysis";
            tabAnalysis.Size = new Size(480, 435);
            tabAnalysis.TabIndex = 8;
            tabAnalysis.Tag = "20";
            tabAnalysis.UseVisualStyleBackColor = true;
            // 
            // groupBox14
            // 
            groupBox14.Controls.Add(chBoxPropagateSettings);
            groupBox14.Controls.Add(chBoxAnalysisDefaultEnabled);
            groupBox14.Location = new Point(6, 309);
            groupBox14.Name = "groupBox14";
            groupBox14.Size = new Size(468, 100);
            groupBox14.TabIndex = 10;
            groupBox14.TabStop = false;
            groupBox14.Text = "Analysis";
            // 
            // chBoxPropagateSettings
            // 
            chBoxPropagateSettings.AutoSize = true;
            chBoxPropagateSettings.Location = new Point(16, 33);
            chBoxPropagateSettings.Name = "chBoxPropagateSettings";
            chBoxPropagateSettings.Size = new Size(261, 19);
            chBoxPropagateSettings.TabIndex = 11;
            chBoxPropagateSettings.Tag = "603";
            chBoxPropagateSettings.Text = "Propagate analysis settings on dependencies";
            chBoxPropagateSettings.UseVisualStyleBackColor = true;
            chBoxPropagateSettings.Click += ChBox_Click;
            // 
            // chBoxAnalysisDefaultEnabled
            // 
            chBoxAnalysisDefaultEnabled.AutoSize = true;
            chBoxAnalysisDefaultEnabled.Location = new Point(16, 58);
            chBoxAnalysisDefaultEnabled.Name = "chBoxAnalysisDefaultEnabled";
            chBoxAnalysisDefaultEnabled.Size = new Size(319, 19);
            chBoxAnalysisDefaultEnabled.TabIndex = 10;
            chBoxAnalysisDefaultEnabled.Tag = "602";
            chBoxAnalysisDefaultEnabled.Text = "Make analysis settings default and do not ask everytime";
            chBoxAnalysisDefaultEnabled.UseVisualStyleBackColor = true;
            chBoxAnalysisDefaultEnabled.Click += ChBox_Click;
            // 
            // groupBox13
            // 
            groupBox13.Controls.Add(chBoxUseStats);
            groupBox13.Location = new Point(6, 246);
            groupBox13.Name = "groupBox13";
            groupBox13.Size = new Size(468, 57);
            groupBox13.TabIndex = 8;
            groupBox13.TabStop = false;
            groupBox13.Text = "Statistics";
            // 
            // chBoxUseStats
            // 
            chBoxUseStats.AutoSize = true;
            chBoxUseStats.Location = new Point(19, 22);
            chBoxUseStats.Name = "chBoxUseStats";
            chBoxUseStats.Size = new Size(200, 19);
            chBoxUseStats.TabIndex = 8;
            chBoxUseStats.Tag = "601";
            chBoxUseStats.Text = "Enable transport statistics display";
            chBoxUseStats.UseVisualStyleBackColor = true;
            chBoxUseStats.Click += ChBox_Click;
            // 
            // groupBox11
            // 
            groupBox11.Controls.Add(chBoxCustomImageBase);
            groupBox11.Controls.Add(labelAllocGran);
            groupBox11.Controls.Add(label14);
            groupBox11.Controls.Add(label13);
            groupBox11.Controls.Add(cbCustomImageBase);
            groupBox11.Controls.Add(chBoxProcessRelocs);
            groupBox11.Location = new Point(6, 6);
            groupBox11.Name = "groupBox11";
            groupBox11.Size = new Size(468, 234);
            groupBox11.TabIndex = 7;
            groupBox11.TabStop = false;
            groupBox11.Text = "Loader";
            // 
            // chBoxCustomImageBase
            // 
            chBoxCustomImageBase.AutoSize = true;
            chBoxCustomImageBase.Location = new Point(19, 57);
            chBoxCustomImageBase.Name = "chBoxCustomImageBase";
            chBoxCustomImageBase.Size = new Size(131, 19);
            chBoxCustomImageBase.TabIndex = 10;
            chBoxCustomImageBase.Tag = "605";
            chBoxCustomImageBase.Text = "Custom image base";
            chBoxCustomImageBase.UseVisualStyleBackColor = true;
            chBoxCustomImageBase.Click += ChBox_Click;
            // 
            // labelAllocGran
            // 
            labelAllocGran.AutoSize = true;
            labelAllocGran.Location = new Point(188, 121);
            labelAllocGran.Name = "labelAllocGran";
            labelAllocGran.Size = new Size(25, 15);
            labelAllocGran.TabIndex = 9;
            labelAllocGran.Text = "0x0";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Location = new Point(19, 121);
            label14.Name = "label14";
            label14.Size = new Size(163, 15);
            label14.TabIndex = 8;
            label14.Text = "System allocation granularity:";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Location = new Point(19, 148);
            label13.Name = "label13";
            label13.Size = new Size(389, 60);
            label13.TabIndex = 7;
            label13.Text = resources.GetString("label13.Text");
            // 
            // cbCustomImageBase
            // 
            cbCustomImageBase.Enabled = false;
            cbCustomImageBase.FormattingEnabled = true;
            cbCustomImageBase.Location = new Point(19, 86);
            cbCustomImageBase.Name = "cbCustomImageBase";
            cbCustomImageBase.Size = new Size(237, 23);
            cbCustomImageBase.TabIndex = 6;
            cbCustomImageBase.KeyUp += CbMinAppAddressKeyUp;
            // 
            // chBoxProcessRelocs
            // 
            chBoxProcessRelocs.AutoSize = true;
            chBoxProcessRelocs.Location = new Point(19, 32);
            chBoxProcessRelocs.Name = "chBoxProcessRelocs";
            chBoxProcessRelocs.Size = new Size(241, 19);
            chBoxProcessRelocs.TabIndex = 5;
            chBoxProcessRelocs.Tag = "600";
            chBoxProcessRelocs.Text = "Process relocations while parsing images";
            chBoxProcessRelocs.UseVisualStyleBackColor = true;
            chBoxProcessRelocs.Click += ChBox_Click;
            // 
            // tabApiSet
            // 
            tabApiSet.Controls.Add(groupBox15);
            tabApiSet.Controls.Add(groupBox10);
            tabApiSet.Controls.Add(groupBox9);
            tabApiSet.Location = new Point(4, 5);
            tabApiSet.Name = "tabApiSet";
            tabApiSet.Size = new Size(480, 435);
            tabApiSet.TabIndex = 7;
            tabApiSet.Tag = "30";
            tabApiSet.UseVisualStyleBackColor = true;
            // 
            // groupBox15
            // 
            groupBox15.Controls.Add(labelApiSetCount);
            groupBox15.Controls.Add(labelApiSetVersion);
            groupBox15.Location = new Point(6, 190);
            groupBox15.Name = "groupBox15";
            groupBox15.Size = new Size(468, 100);
            groupBox15.TabIndex = 7;
            groupBox15.TabStop = false;
            groupBox15.Text = "Current Namespace Information";
            // 
            // labelApiSetCount
            // 
            labelApiSetCount.AutoSize = true;
            labelApiSetCount.Location = new Point(19, 62);
            labelApiSetCount.Name = "labelApiSetCount";
            labelApiSetCount.Size = new Size(43, 15);
            labelApiSetCount.TabIndex = 11;
            labelApiSetCount.Text = "Count:";
            // 
            // labelApiSetVersion
            // 
            labelApiSetVersion.AutoSize = true;
            labelApiSetVersion.Location = new Point(19, 34);
            labelApiSetVersion.Name = "labelApiSetVersion";
            labelApiSetVersion.Size = new Size(48, 15);
            labelApiSetVersion.TabIndex = 10;
            labelApiSetVersion.Text = "Version:";
            // 
            // groupBox10
            // 
            groupBox10.Controls.Add(chBoxHighlightApiSet);
            groupBox10.Location = new Point(6, 115);
            groupBox10.Name = "groupBox10";
            groupBox10.Size = new Size(468, 69);
            groupBox10.TabIndex = 6;
            groupBox10.TabStop = false;
            groupBox10.Text = "Appearance";
            // 
            // chBoxHighlightApiSet
            // 
            chBoxHighlightApiSet.AutoSize = true;
            chBoxHighlightApiSet.Location = new Point(19, 32);
            chBoxHighlightApiSet.Name = "chBoxHighlightApiSet";
            chBoxHighlightApiSet.Size = new Size(165, 19);
            chBoxHighlightApiSet.TabIndex = 5;
            chBoxHighlightApiSet.Tag = "501";
            chBoxHighlightApiSet.Text = "Highlight ApiSet contracts";
            chBoxHighlightApiSet.UseVisualStyleBackColor = true;
            chBoxHighlightApiSet.Click += ChBox_Click;
            // 
            // groupBox9
            // 
            groupBox9.Controls.Add(apisetTextBox);
            groupBox9.Controls.Add(buttonApiSetBrowse);
            groupBox9.Controls.Add(chBoxApiSetNamespace);
            groupBox9.Location = new Point(6, 6);
            groupBox9.Name = "groupBox9";
            groupBox9.Size = new Size(468, 103);
            groupBox9.TabIndex = 5;
            groupBox9.TabStop = false;
            groupBox9.Text = "Override Used Namespace";
            // 
            // apisetTextBox
            // 
            apisetTextBox.Location = new Point(19, 57);
            apisetTextBox.Name = "apisetTextBox";
            apisetTextBox.ReadOnly = true;
            apisetTextBox.Size = new Size(351, 23);
            apisetTextBox.TabIndex = 7;
            // 
            // buttonApiSetBrowse
            // 
            buttonApiSetBrowse.Location = new Point(376, 56);
            buttonApiSetBrowse.Name = "buttonApiSetBrowse";
            buttonApiSetBrowse.Size = new Size(75, 23);
            buttonApiSetBrowse.TabIndex = 6;
            buttonApiSetBrowse.Text = "Browse";
            buttonApiSetBrowse.UseVisualStyleBackColor = true;
            buttonApiSetBrowse.Click += ButtonApiSetBrowse_Click;
            // 
            // chBoxApiSetNamespace
            // 
            chBoxApiSetNamespace.AutoSize = true;
            chBoxApiSetNamespace.Location = new Point(19, 32);
            chBoxApiSetNamespace.Name = "chBoxApiSetNamespace";
            chBoxApiSetNamespace.Size = new Size(211, 19);
            chBoxApiSetNamespace.TabIndex = 5;
            chBoxApiSetNamespace.Tag = "500";
            chBoxApiSetNamespace.Text = "Retrieve from ApiSetSchema.dll file";
            chBoxApiSetNamespace.UseVisualStyleBackColor = true;
            chBoxApiSetNamespace.Click += ChBox_Click;
            // 
            // tabExternalFunctionHelp
            // 
            tabExternalFunctionHelp.Controls.Add(groupBox5);
            tabExternalFunctionHelp.Location = new Point(4, 5);
            tabExternalFunctionHelp.Name = "tabExternalFunctionHelp";
            tabExternalFunctionHelp.Size = new Size(480, 435);
            tabExternalFunctionHelp.TabIndex = 5;
            tabExternalFunctionHelp.Tag = "40";
            tabExternalFunctionHelp.UseVisualStyleBackColor = true;
            // 
            // groupBox5
            // 
            groupBox5.Controls.Add(buttonDefaultURL);
            groupBox5.Controls.Add(searchOnlineTextBox);
            groupBox5.Controls.Add(label6);
            groupBox5.Location = new Point(6, 6);
            groupBox5.Name = "groupBox5";
            groupBox5.Size = new Size(468, 107);
            groupBox5.TabIndex = 0;
            groupBox5.TabStop = false;
            groupBox5.Text = "External Function Help settings";
            // 
            // buttonDefaultURL
            // 
            buttonDefaultURL.Location = new Point(375, 48);
            buttonDefaultURL.Name = "buttonDefaultURL";
            buttonDefaultURL.Size = new Size(81, 23);
            buttonDefaultURL.TabIndex = 12;
            buttonDefaultURL.Text = "Default URL";
            buttonDefaultURL.UseVisualStyleBackColor = true;
            buttonDefaultURL.Click += ButtonDefaultURL_Click;
            // 
            // searchOnlineTextBox
            // 
            searchOnlineTextBox.Location = new Point(12, 49);
            searchOnlineTextBox.Name = "searchOnlineTextBox";
            searchOnlineTextBox.PlaceholderText = "https://learn.microsoft.com/en-us/search/?terms=%1";
            searchOnlineTextBox.Size = new Size(353, 23);
            searchOnlineTextBox.TabIndex = 11;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(6, 31);
            label6.Name = "label6";
            label6.Size = new Size(299, 15);
            label6.TabIndex = 10;
            label6.Text = "Search Online (Use %1 to represent the function name):";
            // 
            // tabExternalViewer
            // 
            tabExternalViewer.Controls.Add(groupBox4);
            tabExternalViewer.Location = new Point(4, 5);
            tabExternalViewer.Name = "tabExternalViewer";
            tabExternalViewer.Size = new Size(480, 435);
            tabExternalViewer.TabIndex = 4;
            tabExternalViewer.Tag = "50";
            tabExternalViewer.Text = "tabPage1";
            tabExternalViewer.UseVisualStyleBackColor = true;
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(buttonBrowse);
            groupBox4.Controls.Add(argumentsTextBox);
            groupBox4.Controls.Add(label5);
            groupBox4.Controls.Add(commandTextBox);
            groupBox4.Controls.Add(label3);
            groupBox4.Location = new Point(6, 6);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new Size(468, 171);
            groupBox4.TabIndex = 5;
            groupBox4.TabStop = false;
            groupBox4.Text = "External Module Viewer settings";
            // 
            // buttonBrowse
            // 
            buttonBrowse.Location = new Point(375, 52);
            buttonBrowse.Name = "buttonBrowse";
            buttonBrowse.Size = new Size(75, 23);
            buttonBrowse.TabIndex = 9;
            buttonBrowse.Text = "Browse ...";
            buttonBrowse.UseVisualStyleBackColor = true;
            buttonBrowse.Click += ButtonBrowse_Click;
            // 
            // argumentsTextBox
            // 
            argumentsTextBox.Location = new Point(28, 113);
            argumentsTextBox.Name = "argumentsTextBox";
            argumentsTextBox.PlaceholderText = "\"%1\"";
            argumentsTextBox.Size = new Size(341, 23);
            argumentsTextBox.TabIndex = 8;
            argumentsTextBox.Text = "\"%1\"";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(28, 95);
            label5.Name = "label5";
            label5.Size = new Size(284, 15);
            label5.TabIndex = 7;
            label5.Text = "Arguments (Use a %1 to represent the module path):";
            // 
            // commandTextBox
            // 
            commandTextBox.Location = new Point(28, 52);
            commandTextBox.Name = "commandTextBox";
            commandTextBox.Size = new Size(341, 23);
            commandTextBox.TabIndex = 6;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(28, 34);
            label3.Name = "label3";
            label3.Size = new Size(67, 15);
            label3.TabIndex = 5;
            label3.Text = "Command:";
            // 
            // tabHandledFileExtensions
            // 
            tabHandledFileExtensions.Controls.Add(buttonElevate);
            tabHandledFileExtensions.Controls.Add(groupBox1);
            tabHandledFileExtensions.Controls.Add(buttonSelectAll);
            tabHandledFileExtensions.Controls.Add(LVFileExt);
            tabHandledFileExtensions.Controls.Add(shellIntegrationWarningLabel);
            tabHandledFileExtensions.Location = new Point(4, 5);
            tabHandledFileExtensions.Name = "tabHandledFileExtensions";
            tabHandledFileExtensions.Padding = new Padding(3);
            tabHandledFileExtensions.Size = new Size(480, 435);
            tabHandledFileExtensions.TabIndex = 3;
            tabHandledFileExtensions.Tag = "60";
            tabHandledFileExtensions.UseVisualStyleBackColor = true;
            // 
            // buttonElevate
            // 
            buttonElevate.Location = new Point(385, 405);
            buttonElevate.Name = "buttonElevate";
            buttonElevate.Size = new Size(89, 25);
            buttonElevate.TabIndex = 8;
            buttonElevate.Text = "Elevate";
            buttonElevate.UseVisualStyleBackColor = true;
            buttonElevate.Visible = false;
            buttonElevate.Click += ButtonElevate_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(customExtBox);
            groupBox1.Controls.Add(label4);
            groupBox1.Controls.Add(buttonAssociate);
            groupBox1.Location = new Point(6, 340);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(332, 61);
            groupBox1.TabIndex = 7;
            groupBox1.TabStop = false;
            // 
            // customExtBox
            // 
            customExtBox.Location = new Point(149, 22);
            customExtBox.MaxLength = 12;
            customExtBox.Name = "customExtBox";
            customExtBox.PlaceholderText = "bin";
            customExtBox.Size = new Size(71, 23);
            customExtBox.TabIndex = 9;
            customExtBox.KeyPress += CustomExtBox_KeyPress;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(11, 26);
            label4.Name = "label4";
            label4.Size = new Size(136, 15);
            label4.TabIndex = 8;
            label4.Text = "Specify custom file type ";
            // 
            // buttonAssociate
            // 
            buttonAssociate.Location = new Point(230, 21);
            buttonAssociate.Name = "buttonAssociate";
            buttonAssociate.Size = new Size(75, 23);
            buttonAssociate.TabIndex = 7;
            buttonAssociate.Text = "Associate";
            buttonAssociate.UseVisualStyleBackColor = true;
            buttonAssociate.Click += ButtonAssociate_Click;
            // 
            // buttonSelectAll
            // 
            buttonSelectAll.Location = new Point(385, 340);
            buttonSelectAll.Name = "buttonSelectAll";
            buttonSelectAll.Size = new Size(89, 25);
            buttonSelectAll.TabIndex = 6;
            buttonSelectAll.Text = "Select All";
            buttonSelectAll.UseVisualStyleBackColor = true;
            buttonSelectAll.Click += ButtonSelectAll_Click;
            // 
            // LVFileExt
            // 
            LVFileExt.CheckBoxes = true;
            LVFileExt.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2 });
            LVFileExt.FullRowSelect = true;
            LVFileExt.GridLines = true;
            LVFileExt.HeaderStyle = ColumnHeaderStyle.None;
            LVFileExt.Location = new Point(6, 6);
            LVFileExt.MultiSelect = false;
            LVFileExt.Name = "LVFileExt";
            LVFileExt.Size = new Size(468, 327);
            LVFileExt.TabIndex = 0;
            LVFileExt.UseCompatibleStateImageBehavior = false;
            LVFileExt.View = View.Details;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "Extension";
            columnHeader1.Width = 80;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Description";
            columnHeader2.Width = 280;
            // 
            // shellIntegrationWarningLabel
            // 
            shellIntegrationWarningLabel.AutoSize = true;
            shellIntegrationWarningLabel.Location = new Point(6, 415);
            shellIntegrationWarningLabel.Name = "shellIntegrationWarningLabel";
            shellIntegrationWarningLabel.Size = new Size(292, 15);
            shellIntegrationWarningLabel.TabIndex = 6;
            shellIntegrationWarningLabel.Text = "Shell integration requires elevated administrator rights";
            shellIntegrationWarningLabel.Visible = false;
            // 
            // tabSearchOrder
            // 
            tabSearchOrder.Controls.Add(DeleteUserDirectoryButton);
            tabSearchOrder.Controls.Add(AddUserDirectoryButton);
            tabSearchOrder.Controls.Add(TVSearchOrder);
            tabSearchOrder.Controls.Add(ExpandSearchOrderButton);
            tabSearchOrder.Controls.Add(MoveUpButton);
            tabSearchOrder.Controls.Add(MoveDownButton);
            tabSearchOrder.Location = new Point(4, 5);
            tabSearchOrder.Name = "tabSearchOrder";
            tabSearchOrder.Padding = new Padding(3);
            tabSearchOrder.Size = new Size(480, 435);
            tabSearchOrder.TabIndex = 6;
            tabSearchOrder.Tag = "70";
            // 
            // DeleteUserDirectoryButton
            // 
            DeleteUserDirectoryButton.Enabled = false;
            DeleteUserDirectoryButton.Location = new Point(49, 403);
            DeleteUserDirectoryButton.Name = "DeleteUserDirectoryButton";
            DeleteUserDirectoryButton.Size = new Size(60, 23);
            DeleteUserDirectoryButton.TabIndex = 6;
            DeleteUserDirectoryButton.Text = "Delete";
            DeleteUserDirectoryButton.UseVisualStyleBackColor = true;
            DeleteUserDirectoryButton.Click += DeleteUserDirectoryButtonClick;
            // 
            // AddUserDirectoryButton
            // 
            AddUserDirectoryButton.Location = new Point(115, 403);
            AddUserDirectoryButton.Name = "AddUserDirectoryButton";
            AddUserDirectoryButton.Size = new Size(60, 23);
            AddUserDirectoryButton.TabIndex = 5;
            AddUserDirectoryButton.Text = "Add";
            AddUserDirectoryButton.UseVisualStyleBackColor = true;
            AddUserDirectoryButton.Click += AddUserDirectoryButtonClick;
            // 
            // TVSearchOrder
            // 
            TVSearchOrder.HideSelection = false;
            TVSearchOrder.Location = new Point(6, 6);
            TVSearchOrder.Name = "TVSearchOrder";
            TVSearchOrder.ShowNodeToolTips = true;
            TVSearchOrder.Size = new Size(468, 391);
            TVSearchOrder.TabIndex = 4;
            TVSearchOrder.AfterSelect += TVSearchOrderAfterSelect;
            // 
            // ExpandSearchOrderButton
            // 
            ExpandSearchOrderButton.Location = new Point(181, 403);
            ExpandSearchOrderButton.Name = "ExpandSearchOrderButton";
            ExpandSearchOrderButton.Size = new Size(103, 23);
            ExpandSearchOrderButton.TabIndex = 3;
            ExpandSearchOrderButton.Text = "Collapse All";
            ExpandSearchOrderButton.UseVisualStyleBackColor = true;
            ExpandSearchOrderButton.Click += ExpandSearchOrderButton_Click;
            // 
            // MoveUpButton
            // 
            MoveUpButton.Enabled = false;
            MoveUpButton.Location = new Point(290, 403);
            MoveUpButton.Name = "MoveUpButton";
            MoveUpButton.Size = new Size(89, 23);
            MoveUpButton.TabIndex = 2;
            MoveUpButton.Text = "Move Up";
            MoveUpButton.UseVisualStyleBackColor = true;
            MoveUpButton.Click += TVSearchOderMoveUp;
            // 
            // MoveDownButton
            // 
            MoveDownButton.Enabled = false;
            MoveDownButton.Location = new Point(385, 403);
            MoveDownButton.Name = "MoveDownButton";
            MoveDownButton.Size = new Size(89, 23);
            MoveDownButton.TabIndex = 1;
            MoveDownButton.Text = "Move Down";
            MoveDownButton.UseVisualStyleBackColor = true;
            MoveDownButton.Click += TVSearchOderMoveDown;
            // 
            // tabSearchOrderDrivers
            // 
            tabSearchOrderDrivers.Controls.Add(DeleteUserDirectoryDriversButton);
            tabSearchOrderDrivers.Controls.Add(AddUserDirectoryDriversButton);
            tabSearchOrderDrivers.Controls.Add(TVSearchOrderDrivers);
            tabSearchOrderDrivers.Controls.Add(ExpandSearchOrderDrivers);
            tabSearchOrderDrivers.Controls.Add(MoveUpButtonDrivers);
            tabSearchOrderDrivers.Controls.Add(MoveDownButtonDrivers);
            tabSearchOrderDrivers.Location = new Point(4, 5);
            tabSearchOrderDrivers.Name = "tabSearchOrderDrivers";
            tabSearchOrderDrivers.Size = new Size(480, 435);
            tabSearchOrderDrivers.TabIndex = 9;
            tabSearchOrderDrivers.Tag = "80";
            tabSearchOrderDrivers.UseVisualStyleBackColor = true;
            // 
            // DeleteUserDirectoryDriversButton
            // 
            DeleteUserDirectoryDriversButton.Enabled = false;
            DeleteUserDirectoryDriversButton.Location = new Point(47, 404);
            DeleteUserDirectoryDriversButton.Name = "DeleteUserDirectoryDriversButton";
            DeleteUserDirectoryDriversButton.Size = new Size(60, 23);
            DeleteUserDirectoryDriversButton.TabIndex = 10;
            DeleteUserDirectoryDriversButton.Text = "Delete";
            DeleteUserDirectoryDriversButton.UseVisualStyleBackColor = true;
            DeleteUserDirectoryDriversButton.Click += DeleteUserDirectoryButtonClick;
            // 
            // AddUserDirectoryDriversButton
            // 
            AddUserDirectoryDriversButton.Location = new Point(113, 404);
            AddUserDirectoryDriversButton.Name = "AddUserDirectoryDriversButton";
            AddUserDirectoryDriversButton.Size = new Size(60, 23);
            AddUserDirectoryDriversButton.TabIndex = 9;
            AddUserDirectoryDriversButton.Text = "Add";
            AddUserDirectoryDriversButton.UseVisualStyleBackColor = true;
            AddUserDirectoryDriversButton.Click += AddUserDirectoryButtonClick;
            // 
            // TVSearchOrderDrivers
            // 
            TVSearchOrderDrivers.HideSelection = false;
            TVSearchOrderDrivers.Location = new Point(6, 6);
            TVSearchOrderDrivers.Name = "TVSearchOrderDrivers";
            TVSearchOrderDrivers.ShowNodeToolTips = true;
            TVSearchOrderDrivers.Size = new Size(466, 391);
            TVSearchOrderDrivers.TabIndex = 8;
            TVSearchOrderDrivers.AfterSelect += TVSearchOrderAfterSelect;
            // 
            // ExpandSearchOrderDrivers
            // 
            ExpandSearchOrderDrivers.Location = new Point(181, 404);
            ExpandSearchOrderDrivers.Name = "ExpandSearchOrderDrivers";
            ExpandSearchOrderDrivers.Size = new Size(103, 23);
            ExpandSearchOrderDrivers.TabIndex = 7;
            ExpandSearchOrderDrivers.Text = "Collapse All";
            ExpandSearchOrderDrivers.UseVisualStyleBackColor = true;
            ExpandSearchOrderDrivers.Click += ExpandSearchOrderButton_Click;
            // 
            // MoveUpButtonDrivers
            // 
            MoveUpButtonDrivers.Enabled = false;
            MoveUpButtonDrivers.Location = new Point(290, 404);
            MoveUpButtonDrivers.Name = "MoveUpButtonDrivers";
            MoveUpButtonDrivers.Size = new Size(89, 23);
            MoveUpButtonDrivers.TabIndex = 6;
            MoveUpButtonDrivers.Text = "Move Up";
            MoveUpButtonDrivers.UseVisualStyleBackColor = true;
            MoveUpButtonDrivers.Click += TVSearchOderMoveUp;
            // 
            // MoveDownButtonDrivers
            // 
            MoveDownButtonDrivers.Enabled = false;
            MoveDownButtonDrivers.Location = new Point(385, 404);
            MoveDownButtonDrivers.Name = "MoveDownButtonDrivers";
            MoveDownButtonDrivers.Size = new Size(89, 23);
            MoveDownButtonDrivers.TabIndex = 5;
            MoveDownButtonDrivers.Text = "Move Down";
            MoveDownButtonDrivers.UseVisualStyleBackColor = true;
            MoveDownButtonDrivers.Click += TVSearchOderMoveDown;
            // 
            // tabServer
            // 
            tabServer.Controls.Add(groupBox7);
            tabServer.Controls.Add(groupBox12);
            tabServer.Location = new Point(4, 5);
            tabServer.Name = "tabServer";
            tabServer.Size = new Size(480, 435);
            tabServer.TabIndex = 10;
            tabServer.Tag = "90";
            tabServer.UseVisualStyleBackColor = true;
            // 
            // groupBox7
            // 
            groupBox7.Controls.Add(ServerFileState);
            groupBox7.Controls.Add(buttonBrowseServerApp);
            groupBox7.Controls.Add(serverAppLocationTextBox);
            groupBox7.Location = new Point(6, 6);
            groupBox7.Name = "groupBox7";
            groupBox7.Size = new Size(468, 87);
            groupBox7.TabIndex = 11;
            groupBox7.TabStop = false;
            groupBox7.Text = "Server Application Location";
            // 
            // ServerFileState
            // 
            ServerFileState.AutoSize = true;
            ServerFileState.Location = new Point(20, 60);
            ServerFileState.Name = "ServerFileState";
            ServerFileState.Size = new Size(81, 15);
            ServerFileState.TabIndex = 11;
            ServerFileState.Text = "File not found";
            // 
            // buttonBrowseServerApp
            // 
            buttonBrowseServerApp.Location = new Point(376, 28);
            buttonBrowseServerApp.Name = "buttonBrowseServerApp";
            buttonBrowseServerApp.Size = new Size(75, 23);
            buttonBrowseServerApp.TabIndex = 10;
            buttonBrowseServerApp.Text = "Browse ...";
            buttonBrowseServerApp.UseVisualStyleBackColor = true;
            buttonBrowseServerApp.Click += BrowseForServerAppClick;
            // 
            // serverAppLocationTextBox
            // 
            serverAppLocationTextBox.Location = new Point(19, 28);
            serverAppLocationTextBox.Name = "serverAppLocationTextBox";
            serverAppLocationTextBox.ReadOnly = true;
            serverAppLocationTextBox.Size = new Size(351, 23);
            serverAppLocationTextBox.TabIndex = 0;
            // 
            // groupBox12
            // 
            groupBox12.Controls.Add(labelSrvPid);
            groupBox12.Controls.Add(label7);
            groupBox12.Controls.Add(labelSrvPort);
            groupBox12.Controls.Add(label16);
            groupBox12.Controls.Add(buttonServerConnect);
            groupBox12.Controls.Add(labelServerStatus);
            groupBox12.Controls.Add(label15);
            groupBox12.Location = new Point(6, 94);
            groupBox12.Name = "groupBox12";
            groupBox12.Size = new Size(468, 133);
            groupBox12.TabIndex = 0;
            groupBox12.TabStop = false;
            // 
            // labelSrvPid
            // 
            labelSrvPid.AutoSize = true;
            labelSrvPid.Location = new Point(175, 65);
            labelSrvPid.Name = "labelSrvPid";
            labelSrvPid.Size = new Size(12, 15);
            labelSrvPid.TabIndex = 13;
            labelSrvPid.Text = "-";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(25, 65);
            label7.Name = "label7";
            label7.Size = new Size(66, 15);
            label7.TabIndex = 12;
            label7.Text = "Process Id: ";
            // 
            // labelSrvPort
            // 
            labelSrvPort.AutoSize = true;
            labelSrvPort.Location = new Point(175, 89);
            labelSrvPort.Name = "labelSrvPort";
            labelSrvPort.Size = new Size(13, 15);
            labelSrvPort.TabIndex = 4;
            labelSrvPort.Text = "0";
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Location = new Point(25, 89);
            label16.Name = "label16";
            label16.Size = new Size(67, 15);
            label16.TabIndex = 3;
            label16.Text = "Server port:";
            // 
            // buttonServerConnect
            // 
            buttonServerConnect.Location = new Point(175, 22);
            buttonServerConnect.Name = "buttonServerConnect";
            buttonServerConnect.Size = new Size(85, 23);
            buttonServerConnect.TabIndex = 2;
            buttonServerConnect.Text = "Reconnect";
            buttonServerConnect.UseVisualStyleBackColor = true;
            buttonServerConnect.Click += ConnectServerButtonClick;
            // 
            // labelServerStatus
            // 
            labelServerStatus.AutoSize = true;
            labelServerStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelServerStatus.ForeColor = Color.Green;
            labelServerStatus.Location = new Point(73, 26);
            labelServerStatus.Name = "labelServerStatus";
            labelServerStatus.Size = new Size(67, 15);
            labelServerStatus.TabIndex = 1;
            labelServerStatus.Text = "Connected";
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new Point(25, 26);
            label15.Name = "label15";
            label15.Size = new Size(42, 15);
            label15.TabIndex = 0;
            label15.Text = "Status:";
            // 
            // tabSymbols
            // 
            tabSymbols.Controls.Add(chBoxUseSymbols);
            tabSymbols.Controls.Add(groupBoxSymbols);
            tabSymbols.Location = new Point(4, 5);
            tabSymbols.Name = "tabSymbols";
            tabSymbols.Size = new Size(480, 435);
            tabSymbols.TabIndex = 11;
            tabSymbols.Tag = "100";
            tabSymbols.UseVisualStyleBackColor = true;
            // 
            // chBoxUseSymbols
            // 
            chBoxUseSymbols.AutoSize = true;
            chBoxUseSymbols.Location = new Point(6, 10);
            chBoxUseSymbols.Name = "chBoxUseSymbols";
            chBoxUseSymbols.Size = new Size(400, 34);
            chBoxUseSymbols.TabIndex = 10;
            chBoxUseSymbols.Tag = "604";
            chBoxUseSymbols.Text = "Use debug symbols (PDB) to resolve anonymous function names when\r\ndisplaying module import/export list";
            chBoxUseSymbols.UseVisualStyleBackColor = true;
            chBoxUseSymbols.Click += ChBox_Click;
            // 
            // groupBoxSymbols
            // 
            groupBoxSymbols.Controls.Add(buttonSymbolsDefault);
            groupBoxSymbols.Controls.Add(buttonSymbolPickColor);
            groupBoxSymbols.Controls.Add(panelSymColor);
            groupBoxSymbols.Controls.Add(buttonSymbolsBrowse);
            groupBoxSymbols.Controls.Add(symbolsStoreTextBox);
            groupBoxSymbols.Controls.Add(label19);
            groupBoxSymbols.Controls.Add(buttonDbghelpBrowse);
            groupBoxSymbols.Controls.Add(dbghelpTextBox);
            groupBoxSymbols.Controls.Add(label18);
            groupBoxSymbols.Location = new Point(6, 50);
            groupBoxSymbols.Name = "groupBoxSymbols";
            groupBoxSymbols.Size = new Size(468, 208);
            groupBoxSymbols.TabIndex = 0;
            groupBoxSymbols.TabStop = false;
            // 
            // buttonSymbolsDefault
            // 
            buttonSymbolsDefault.Location = new Point(348, 155);
            buttonSymbolsDefault.Name = "buttonSymbolsDefault";
            buttonSymbolsDefault.Size = new Size(94, 23);
            buttonSymbolsDefault.TabIndex = 9;
            buttonSymbolsDefault.Text = "Set Defaults";
            buttonSymbolsDefault.UseVisualStyleBackColor = true;
            buttonSymbolsDefault.Click += ButtonSymbolsDefaults_Click;
            // 
            // buttonSymbolPickColor
            // 
            buttonSymbolPickColor.Location = new Point(219, 155);
            buttonSymbolPickColor.Name = "buttonSymbolPickColor";
            buttonSymbolPickColor.Size = new Size(75, 23);
            buttonSymbolPickColor.TabIndex = 8;
            buttonSymbolPickColor.Text = "Change";
            buttonSymbolPickColor.UseVisualStyleBackColor = true;
            buttonSymbolPickColor.Click += ButtonSymbolPickColor_Click;
            // 
            // panelSymColor
            // 
            panelSymColor.Controls.Add(label11);
            panelSymColor.Location = new Point(19, 152);
            panelSymColor.Name = "panelSymColor";
            panelSymColor.Size = new Size(194, 30);
            panelSymColor.TabIndex = 7;
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(8, 7);
            label11.Name = "label11";
            label11.Size = new Size(182, 15);
            label11.TabIndex = 0;
            label11.Text = "Resolved Symbol Highlight Color";
            // 
            // buttonSymbolsBrowse
            // 
            buttonSymbolsBrowse.Location = new Point(406, 108);
            buttonSymbolsBrowse.Name = "buttonSymbolsBrowse";
            buttonSymbolsBrowse.Size = new Size(36, 23);
            buttonSymbolsBrowse.TabIndex = 6;
            buttonSymbolsBrowse.Text = "...";
            buttonSymbolsBrowse.UseVisualStyleBackColor = true;
            buttonSymbolsBrowse.Click += SymButtons_Click;
            // 
            // symbolsStoreTextBox
            // 
            symbolsStoreTextBox.Location = new Point(19, 108);
            symbolsStoreTextBox.Name = "symbolsStoreTextBox";
            symbolsStoreTextBox.Size = new Size(378, 23);
            symbolsStoreTextBox.TabIndex = 5;
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.Location = new Point(6, 85);
            label19.Name = "label19";
            label19.Size = new Size(82, 15);
            label19.TabIndex = 4;
            label19.Text = "Symbols path:";
            // 
            // buttonDbghelpBrowse
            // 
            buttonDbghelpBrowse.Location = new Point(406, 53);
            buttonDbghelpBrowse.Name = "buttonDbghelpBrowse";
            buttonDbghelpBrowse.Size = new Size(36, 23);
            buttonDbghelpBrowse.TabIndex = 3;
            buttonDbghelpBrowse.Text = "...";
            buttonDbghelpBrowse.UseVisualStyleBackColor = true;
            buttonDbghelpBrowse.Click += SymButtons_Click;
            // 
            // dbghelpTextBox
            // 
            dbghelpTextBox.Location = new Point(19, 53);
            dbghelpTextBox.Name = "dbghelpTextBox";
            dbghelpTextBox.Size = new Size(378, 23);
            dbghelpTextBox.TabIndex = 2;
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.Location = new Point(6, 29);
            label18.Name = "label18";
            label18.Size = new Size(98, 15);
            label18.TabIndex = 1;
            label18.Text = "Dbghelp.dll path:";
            // 
            // configCancel
            // 
            configCancel.DialogResult = DialogResult.Cancel;
            configCancel.Location = new Point(608, 14);
            configCancel.Name = "configCancel";
            configCancel.Size = new Size(75, 23);
            configCancel.TabIndex = 1;
            configCancel.Text = "Cancel";
            configCancel.UseVisualStyleBackColor = true;
            // 
            // configOK
            // 
            configOK.DialogResult = DialogResult.OK;
            configOK.Location = new Point(524, 14);
            configOK.Name = "configOK";
            configOK.Size = new Size(75, 23);
            configOK.TabIndex = 0;
            configOK.Text = "OK";
            configOK.UseVisualStyleBackColor = true;
            configOK.Click += ConfigOK_Click;
            // 
            // browseFileDialog
            // 
            browseFileDialog.Filter = "All files|*.*";
            browseFileDialog.ShowHiddenFiles = true;
            // 
            // folderBrowserDialog
            // 
            folderBrowserDialog.AddToRecent = false;
            folderBrowserDialog.ShowHiddenFiles = true;
            // 
            // ConfigurationForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(703, 507);
            Controls.Add(splitContainer2);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ConfigurationForm";
            Padding = new Padding(5);
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Show;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Configuration";
            Load += ConfigurationForm_Load;
            KeyDown += ConfigurationForm_KeyDown;
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            settingsTabControl.ResumeLayout(false);
            tabMain.ResumeLayout(false);
            groupBox8.ResumeLayout(false);
            groupBox8.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nodeMaxDepthUpDown).EndInit();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            tabMainHistory.ResumeLayout(false);
            groupBox6.ResumeLayout(false);
            groupBox6.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)historyUpDown).EndInit();
            tabAnalysis.ResumeLayout(false);
            groupBox14.ResumeLayout(false);
            groupBox14.PerformLayout();
            groupBox13.ResumeLayout(false);
            groupBox13.PerformLayout();
            groupBox11.ResumeLayout(false);
            groupBox11.PerformLayout();
            tabApiSet.ResumeLayout(false);
            groupBox15.ResumeLayout(false);
            groupBox15.PerformLayout();
            groupBox10.ResumeLayout(false);
            groupBox10.PerformLayout();
            groupBox9.ResumeLayout(false);
            groupBox9.PerformLayout();
            tabExternalFunctionHelp.ResumeLayout(false);
            groupBox5.ResumeLayout(false);
            groupBox5.PerformLayout();
            tabExternalViewer.ResumeLayout(false);
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            tabHandledFileExtensions.ResumeLayout(false);
            tabHandledFileExtensions.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            tabSearchOrder.ResumeLayout(false);
            tabSearchOrderDrivers.ResumeLayout(false);
            tabServer.ResumeLayout(false);
            groupBox7.ResumeLayout(false);
            groupBox7.PerformLayout();
            groupBox12.ResumeLayout(false);
            groupBox12.PerformLayout();
            tabSymbols.ResumeLayout(false);
            tabSymbols.PerformLayout();
            groupBoxSymbols.ResumeLayout(false);
            groupBoxSymbols.PerformLayout();
            panelSymColor.ResumeLayout(false);
            panelSymColor.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private SplitContainer splitContainer2;
        private SplitContainer splitContainer1;
        private TreeView TVSettings;
        private TabControl settingsTabControl;
        private TabPage tabMain;
        private TabPage tabMainHistory;
        private TabPage tabHandledFileExtensions;
        private GroupBox groupBox1;
        private TextBox customExtBox;
        private Label label4;
        private Button buttonAssociate;
        private Label shellIntegrationWarningLabel;
        private Button buttonSelectAll;
        private ListView LVFileExt;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
        private Button configCancel;
        private Button configOK;
        private Button buttonElevate;
        private GroupBox groupBox2;
        private CheckBox checkBox1;
        private GroupBox groupBox3;
        private CheckBox chBoxResolveApiSets;
        private CheckBox chBoxUndecorateSymbols;
        private CheckBox chBoxAutoExpands;
        private CheckBox chBoxFullPaths;
        private TabPage tabExternalViewer;
        private OpenFileDialog browseFileDialog;
        private GroupBox groupBox4;
        private Button buttonBrowse;
        private TextBox argumentsTextBox;
        private Label label5;
        private TextBox commandTextBox;
        private Label label3;
        private TabPage tabExternalFunctionHelp;
        private GroupBox groupBox5;
        private Button buttonDefaultURL;
        private TextBox searchOnlineTextBox;
        private Label label6;
        private CheckBox chBoxUpperCase;
        private TabPage tabSearchOrder;
        private GroupBox groupBox6;
        private CheckBox cbHistoryFullPath;
        private Label label2;
        private NumericUpDown historyUpDown;
        private Label label1;
        private Button ExpandSearchOrderButton;
        private Button MoveUpButton;
        private Button MoveDownButton;
        private TreeView TVSearchOrder;
        private Label label8;
        private NumericUpDown nodeMaxDepthUpDown;
        private Label label9;
        private GroupBox groupBox8;
        private CheckBox chBoxCompressSessionFiles;
        private Label label10;
        private Label label12;
        private CheckBox chBoxClearLogOnFileOpen;
        private TabPage tabApiSet;
        private GroupBox groupBox9;
        private CheckBox chBoxApiSetNamespace;
        private GroupBox groupBox10;
        private CheckBox chBoxHighlightApiSet;
        private TabPage tabAnalysis;
        private GroupBox groupBox11;
        private CheckBox chBoxProcessRelocs;
        private ComboBox cbCustomImageBase;
        private Label label13;
        private Label label14;
        private Label labelAllocGran;
        private TabPage tabSearchOrderDrivers;
        private TabPage tabServer;
        private TreeView TVSearchOrderDrivers;
        private Button ExpandSearchOrderDrivers;
        private Button MoveUpButtonDrivers;
        private Button MoveDownButtonDrivers;
        private Button AddUserDirectoryButton;
        private Button AddUserDirectoryDriversButton;
        private Button DeleteUserDirectoryDriversButton;
        private Button DeleteUserDirectoryButton;
        private FolderBrowserDialog folderBrowserDialog;
        private GroupBox groupBox12;
        private Button buttonServerConnect;
        private Label labelServerStatus;
        private Label label15;
        private Label labelSrvPort;
        private Label label16;
        private GroupBox groupBox7;
        private Button buttonBrowseServerApp;
        private TextBox serverAppLocationTextBox;
        private Label ServerFileState;
        private Label label7;
        private Label labelSrvPid;
        private GroupBox groupBox13;
        private CheckBox chBoxUseStats;
        private GroupBox groupBox14;
        private CheckBox chBoxPropagateSettings;
        private CheckBox chBoxAnalysisDefaultEnabled;
        private TextBox apisetTextBox;
        private Button buttonApiSetBrowse;
        private GroupBox groupBox15;
        private Label labelApiSetCount;
        private Label labelApiSetVersion;
        private TabPage tabSymbols;
        private GroupBox groupBoxSymbols;
        private Button buttonSymbolsBrowse;
        private TextBox symbolsStoreTextBox;
        private Label label19;
        private Button buttonDbghelpBrowse;
        private TextBox dbghelpTextBox;
        private Label label18;
        private ColorDialog colorDialog;
        private Button buttonSymbolPickColor;
        private Panel panelSymColor;
        private Label label11;
        private Button buttonSymbolsDefault;
        private CheckBox chBoxUseSymbols;
        private CheckBox chBoxCustomImageBase;
    }
}