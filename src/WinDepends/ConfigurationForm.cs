/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CONFIGURATIONFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        29 Nov 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Diagnostics;
using System.Security;

namespace WinDepends;

public partial class ConfigurationForm : Form
{
    readonly CCoreClient _coreClient;
    readonly string m_CurrentFileName = string.Empty;
    readonly bool m_Is64bitFile;
    bool m_SearchOrderExpand = true;
    bool m_SearchOrderDriversExpand = true;
    readonly CConfiguration _config;
    TreeNode m_UserDefinedDirectoryNodeUM;
    TreeNode m_UserDefinedDirectoryNodeKM;
    readonly int m_CurrentPageIndex;

    public ConfigurationForm(string currentFileName, bool is64bitFile,
        CConfiguration currentConfiguration, CCoreClient coreClient,
        int pageIndex = 0)
    {
        InitializeComponent();
        m_CurrentFileName = currentFileName;
        m_Is64bitFile = is64bitFile;
        _config = currentConfiguration;
        _coreClient = coreClient;
        m_CurrentPageIndex = pageIndex;
    }

    private void FillSearchOrderCategoryWithItems(TreeNode rootNode, SearchOrderType soType)
    {
        int imageIndex = (int)SearchOrderIconType.Module;
        string nodeName = "Cannot query content";
        TreeNode node;

        switch (soType)
        {
            case SearchOrderType.KnownDlls:

                List<string> knownDlls = (m_Is64bitFile) ? CPathResolver.KnownDlls : CPathResolver.KnownDlls32;
                string knownDllsPath = (m_Is64bitFile) ? CPathResolver.KnownDllsPath : CPathResolver.KnownDllsPath32;

                foreach (string name in knownDlls)
                {
                    node = new TreeNode()
                    {
                        ImageIndex = imageIndex,
                        SelectedImageIndex = imageIndex,
                        Text = Path.Combine(knownDllsPath, name)
                    };

                    rootNode.Nodes.Add(node);
                }

                return;

            case SearchOrderType.EnvironmentPathDirectories:

                foreach (string path in CPathResolver.PathEnvironment)
                {
                    imageIndex = Directory.Exists(path) ? (int)SearchOrderIconType.Directory : (int)SearchOrderIconType.DirectoryBad;
                    node = new TreeNode()
                    {
                        ImageIndex = imageIndex,
                        SelectedImageIndex = imageIndex,
                        Text = path
                    };

                    rootNode.Nodes.Add(node);
                }
                return;

            case SearchOrderType.WinSXS:
                nodeName = Path.Combine(CPathResolver.WindowsDirectory, "WinSXS");
                break;

            case SearchOrderType.WindowsDirectory:
                imageIndex = (int)SearchOrderIconType.Directory;
                nodeName = CPathResolver.WindowsDirectory;
                break;

            case SearchOrderType.System32Directory:
                imageIndex = (int)SearchOrderIconType.Directory;
                if (m_Is64bitFile)
                {

                    nodeName = CPathResolver.System32Directory;
                }
                else
                {
                    nodeName = CPathResolver.SysWowDirectory;
                }
                break;

            case SearchOrderType.SystemDirectory:
                imageIndex = (int)SearchOrderIconType.Directory;
                nodeName = CPathResolver.System16Directory;
                break;

            case SearchOrderType.ApplicationDirectory:
                imageIndex = (int)SearchOrderIconType.Directory;
                nodeName = m_CurrentFileName;
                break;

            case SearchOrderType.SystemDriversDirectory:
                imageIndex = (int)SearchOrderIconType.Directory;
                nodeName = CPathResolver.SystemDriversDirectory;
                break;

        }

        node = new TreeNode()
        {
            ImageIndex = imageIndex,
            SelectedImageIndex = imageIndex,
            Text = nodeName
        };
        rootNode.Nodes.Add(node);
    }

    private bool SelectComboBoxItemByUintValue(uint value)
    {
        string hexValue = "0x" + value.ToString("X");
        for (int i = 0; i < cbCustomImageBase.Items.Count; i++)
        {
            if (cbCustomImageBase.Items[i].ToString() == hexValue)
            {
                cbCustomImageBase.SelectedIndex = i;
                return true;
            }
        }

        return false;
    }

    private void CreateSearchOrderView(TreeView view,
                                       List<SearchOrderType> soList,
                                       List<string> soDirList,
                                       ref TreeNode userNode)
    {
        TreeNode tvNode;
        view.ImageList?.Dispose();
        view.ImageList = CUtils.CreateImageList(Properties.Resources.SearchOrderIcons,
                                                CConsts.SearchOrderIconsWidth,
                                                CConsts.SearchOrderIconsHeigth,
                                                Color.Magenta);

        foreach (var sol in soList)
        {
            if (sol != SearchOrderType.UserDefinedDirectory)
            {
                tvNode = new(sol.ToDescription())
                {
                    Tag = sol
                };
                view.Nodes.Add(tvNode);
                FillSearchOrderCategoryWithItems(tvNode, sol);
            }
            else
            {
                //
                // User defined directory.
                //
                userNode = new(CConsts.CategoryUserDefinedDirectory)
                {
                    Tag = SearchOrderType.UserDefinedDirectory
                };
                view.Nodes.Add(userNode);

                foreach (var entry in soDirList)
                {
                    var imageIndex = Directory.Exists(entry) ? (int)SearchOrderIconType.Directory : (int)SearchOrderIconType.DirectoryBad;

                    var subNode = new TreeNode()
                    {
                        ImageIndex = imageIndex,
                        SelectedImageIndex = imageIndex,
                        Text = entry,
                        Tag = CConsts.SearchOrderUserValue
                    };
                    userNode.Nodes.Add(subNode);
                }
            }
        }

        view.ExpandAll();
    }

    private void ShowServerStatusAndSetControls()
    {
        labelServerStatus.Font = new Font(labelServerStatus.Font, FontStyle.Bold);

        if (_coreClient == null || _coreClient.ClientConnection == null || !_coreClient.ClientConnection.Connected)
        {
            labelServerStatus.Text = "Connection Error";
            labelServerStatus.ForeColor = Color.Red;
            labelSrvPid.Text = "-";
            buttonServerConnect.Text = "Connect";
            return;
        }

        var pid = _coreClient.ServerProcessId;
        labelSrvPid.Text = (pid < 0) ? "-" : pid.ToString();

        labelServerStatus.Text = "Connected";
        labelServerStatus.ForeColor = Color.Green;
        buttonServerConnect.Text = "Reconnect";
        labelSrvPort.Text = _coreClient.Port.ToString();
    }

    private void ShowApiSetNamespaceInformation()
    {
        if (_coreClient == null || _coreClient.ClientConnection == null || !_coreClient.ClientConnection.Connected)
        {
            return;
        }

        CCoreApiSetNamespaceInfo nsinfo = _coreClient.GetApiSetNamespaceInfo();
        if (nsinfo != null)
        {
            labelApiSetVersion.Text = $"Version: {nsinfo.Version}";
            labelApiSetCount.Text = $"Entry Count: {nsinfo.Count}";
        }
    }

    private void CheckServerFileState(string fileName)
    {
        ServerFileState.Font = new Font(ServerFileState.Font, FontStyle.Bold);
        if (string.IsNullOrEmpty(fileName))
        {
            ServerFileState.Text = "No file selected";
            ServerFileState.ForeColor = Color.Red;
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(fileName);

            if (File.Exists(fullPath))
            {
                ServerFileState.Text = "File is OK";
                ServerFileState.ForeColor = Color.Green;
            }
            else
            {
                ServerFileState.Text = "File not found!";
                ServerFileState.ForeColor = Color.Red;
            }

        }
        catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException ||
                              ex is NotSupportedException || ex is SecurityException)
        {
            ServerFileState.Text = "Invalid path";
            ServerFileState.ForeColor = Color.Red;
        }
    }

    private void ConfigurationForm_Load(object sender, EventArgs e)
    {
        TVSettings.ExpandAll();

        //
        // Setup state of controls.
        //
        foreach (Control ctrl in tabHandledFileExtensions.Controls)
        {
            ctrl.Enabled = CUtils.IsAdministrator;
        }

        //
        // Add button tooltips.
        //
        TooltipInfo[] tooltips =
        [
            new TooltipInfo(DeleteUserDirectoryButton, "Delete user directory"),
            new TooltipInfo(DeleteUserDirectoryDriversButton, "Delete user directory"),
            new TooltipInfo(AddUserDirectoryButton, "Browse and add user directory"),
            new TooltipInfo(AddUserDirectoryDriversButton, "Browse and add user directory")
        ];

        foreach (var tooltipInfo in tooltips)
        {
            ToolTip tooltip = new();
            tooltip.SetToolTip(tooltipInfo.Control, tooltipInfo.AssociatedText);
        }

        shellIntegrationWarningLabel.Enabled = !CUtils.IsAdministrator;
        shellIntegrationWarningLabel.Visible = !CUtils.IsAdministrator;
        chBoxUseESCKey.Checked = _config.EscKeyEnabled;
        cbHistoryFullPath.Checked = _config.HistoryShowFullPath;
        historyUpDown.Value = _config.HistoryDepth;
        nodeMaxDepthUpDown.Value = _config.ModuleNodeDepthMax;
        chBoxAnalysisEnableExperimentalFeatures.Checked = _config.EnableExperimentalFeatures;
        chBoxExpandForwarders.Checked = _config.ExpandForwarders;
        chBoxAutoExpands.Checked = _config.AutoExpands;
        chBoxFullPaths.Checked = _config.FullPaths;
        chBoxUndecorateSymbols.Checked = _config.ViewUndecorated;
        chBoxResolveApiSets.Checked = _config.ResolveAPIsets;
        chBoxHighlightApiSet.Checked = _config.HighlightApiSet;
        chBoxApiSetNamespace.Checked = _config.UseApiSetSchemaFile;
        chBoxUpperCase.Checked = _config.UpperCaseModuleNames;
        chBoxCompressSessionFiles.Checked = _config.CompressSessionFiles;
        chBoxClearLogOnFileOpen.Checked = _config.ClearLogOnFileOpen;
        cbCustomImageBase.Enabled = _config.UseCustomImageBase;
        if (_config.UseCustomImageBase)
        {
            _config.ProcessRelocsForImage = true;
        }
        chBoxProcessRelocs.Checked = _config.ProcessRelocsForImage;
        chBoxUseStats.Checked = _config.UseStats;
        chBoxAnalysisDefaultEnabled.Checked = _config.AnalysisSettingsUseAsDefault;
        chBoxPropagateSettings.Checked = _config.PropagateSettingsOnDependencies;
        chBoxUseSymbols.Checked = _config.UseSymbols;

        groupBoxSymbols.Enabled = _config.UseSymbols;

        commandTextBox.Text = _config.ExternalViewerCommand;
        argumentsTextBox.Text = _config.ExternalViewerArguments;

        searchOnlineTextBox.Text = _config.ExternalFunctionHelpURL;

        serverAppLocationTextBox.Text = _config.CoreServerAppLocation;

        symbolsStoreTextBox.Text = _config.SymbolsStorePath;
        dbghelpTextBox.Text = _config.SymbolsDllPath;

        panelSymColor.BackColor = _config.SymbolsHighlightColor;

        buttonApiSetBrowse.Enabled = _config.UseApiSetSchemaFile;
        if (_config.UseApiSetSchemaFile)
        {
            apisetTextBox.Text = _config.ApiSetSchemaFile;
        }

        //
        // Elevate button setup.
        //
        buttonElevate.Visible = !CUtils.IsAdministrator;
        buttonElevate.Enabled = !CUtils.IsAdministrator;
        NativeMethods.AddShieldToButton(buttonElevate);

        //
        // Search order UM/KM.
        //
        CreateSearchOrderView(TVSearchOrder,
            _config.SearchOrderListUM,
            _config.UserSearchOrderDirectoriesUM,
            ref m_UserDefinedDirectoryNodeUM);

        CreateSearchOrderView(TVSearchOrderDrivers,
            _config.SearchOrderListKM,
            _config.UserSearchOrderDirectoriesKM,
            ref m_UserDefinedDirectoryNodeKM);

        //
        // Handled file extensions.
        //
        LVFileExt.Items.Clear();
        foreach (PropertyElement el in InternalFileHandledExtensions.ExtensionList)
        {
            ListViewItem item = new()
            {
                Text = String.Concat("*.", el.Name),
                Tag = el.Name,
            };

            //
            // Check file associations.
            //
            if (CUtils.GetAssoc(el.Name))
            {
                item.Checked = true;
            }

            LVFileExt.Items.Add(item);
            item.SubItems.Add(el.Value);
        }

        //
        // Reloc settings.
        //
        cbCustomImageBase.Items.Clear();
        cbCustomImageBase.Items.Add($"0x{CUtils.MinAppAddress:X}");
        cbCustomImageBase.Items.Add($"0x{CConsts.DefaultAppStartAddress:X}");
        if (!SelectComboBoxItemByUintValue(_config.CustomImageBase))
        {
            var i = cbCustomImageBase.Items.Add($"0x{_config.CustomImageBase:X}");
            cbCustomImageBase.SelectedIndex = i;
        }

        labelAllocGran.Text = $"0x{CUtils.AllocationGranularity:X}";

        CheckServerFileState(_config.CoreServerAppLocation);
        ShowServerStatusAndSetControls();
        ShowApiSetNamespaceInformation();

        settingsTabControl.SelectedIndex = m_CurrentPageIndex;
        if (m_CurrentPageIndex != 0 && settingsTabControl.SelectedTab != null)
        {
            var tagValue = settingsTabControl.SelectedTab.Tag;
            var nodeToSelect = CUtils.FindNodeByTag(TVSettings.Nodes, tagValue);
            if (nodeToSelect != null)
            {
                TVSettings.SelectedNode = nodeToSelect;
            }
        }
        else
        {
            TVSettings.SelectedNode = TVSettings.Nodes[0];
        }
    }

    private void ConfigurationForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && _config.EscKeyEnabled)
        {
            this.Close();
        }
    }

    private void HistoryFullPath_Click(object sender, EventArgs e)
    {
        _config.HistoryShowFullPath = cbHistoryFullPath.Checked;
    }

    private void ChBox_Click(object sender, EventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag == null)
            return;

        bool isChecked = checkBox.Checked;

        switch (Convert.ToInt32(checkBox.Tag))
        {
            case CConsts.TagUseESC:
                _config.EscKeyEnabled = isChecked;
                break;

            case CConsts.TagFullPaths:
                _config.FullPaths = isChecked;
                break;

            case CConsts.TagAutoExpand:
                _config.AutoExpands = isChecked;
                break;

            case CConsts.TagViewUndecorated:
                _config.ViewUndecorated = isChecked;
                break;

            case CConsts.TagResolveAPIsets:
                _config.ResolveAPIsets = isChecked;
                break;

            case CConsts.TagUpperCaseModuleNames:
                _config.UpperCaseModuleNames = isChecked;
                break;

            case CConsts.TagClearLogOnFileOpen:
                _config.ClearLogOnFileOpen = isChecked;
                break;

            case CConsts.TagCompressSessionFiles:
                _config.CompressSessionFiles = isChecked;
                break;

            case CConsts.TagUseApiSetSchemaFile:
                _config.UseApiSetSchemaFile = isChecked;
                buttonApiSetBrowse.Enabled = isChecked;
                break;

            case CConsts.TagHighlightApiSet:
                _config.HighlightApiSet = isChecked;
                break;

            case CConsts.TagProcessRelocsForImage:
                _config.ProcessRelocsForImage = isChecked;
                break;

            case CConsts.TagUseCustomImageBase:
                _config.UseCustomImageBase = isChecked;
                if (_config.UseCustomImageBase)
                {
                    _config.ProcessRelocsForImage = true;
                    chBoxProcessRelocs.Checked = true;
                }
                cbCustomImageBase.Enabled = isChecked;
                break;

            case CConsts.TagUseStats:
                _config.UseStats = isChecked;
                break;

            case CConsts.TagAnalysisDefaultEnabled:
                _config.AnalysisSettingsUseAsDefault = isChecked;
                break;

            case CConsts.TagPropagateSettingsEnabled:
                _config.PropagateSettingsOnDependencies = isChecked;
                break;

            case CConsts.TagUseSymbols:
                _config.UseSymbols = isChecked;
                groupBoxSymbols.Enabled = isChecked;
                break;

            case CConsts.TagEnableExperimentalFeatures:
                _config.EnableExperimentalFeatures = isChecked;
                break;
            case CConsts.TagExpandForwarders:
                _config.ExpandForwarders = isChecked;
                break;
        }
    }

    private static void SetSearchOrderList(TreeView view,
                                    List<SearchOrderType> soElements,
                                    List<string> soDirs,
                                    TreeNode directoryNode)
    {
        soElements.Clear();
        foreach (TreeNode node in view.Nodes)
        {
            var entry = (SearchOrderType)node.Tag;
            soElements.Add(entry);
        }

        soDirs.Clear();
        foreach (TreeNode node in directoryNode.Nodes)
        {
            soDirs.Add(node.Text);
        }

    }

    private void ConfigOK_Click(object sender, EventArgs e)
    {
        //
        // Set file associations.
        //
        if (CUtils.IsAdministrator)
        {
            foreach (ListViewItem item in LVFileExt.Items)
            {
                string extension = item.Tag?.ToString();
                if (extension != null)
                {
                    if (item.Checked)
                    {
                        CUtils.SetAssoc(extension);
                    }
                    else
                    {
                        CUtils.RemoveAssoc(extension);
                    }
                }
            }
        }

        _config.ExternalViewerCommand = commandTextBox.Text;
        _config.ExternalViewerArguments = argumentsTextBox.Text;
        _config.ExternalFunctionHelpURL = searchOnlineTextBox.Text;
        _config.CoreServerAppLocation = serverAppLocationTextBox.Text;

        if (_config.UseApiSetSchemaFile && (!string.IsNullOrEmpty(apisetTextBox.Text)))
        {
            _config.ApiSetSchemaFile = apisetTextBox.Text;
        }
        else
        {
            _config.ApiSetSchemaFile = string.Empty;
        }

        SetSearchOrderList(TVSearchOrder,
            _config.SearchOrderListUM,
            _config.UserSearchOrderDirectoriesUM,
            m_UserDefinedDirectoryNodeUM);

        SetSearchOrderList(TVSearchOrderDrivers,
            _config.SearchOrderListKM,
            _config.UserSearchOrderDirectoriesKM,
            m_UserDefinedDirectoryNodeKM);

        if (_config.UseCustomImageBase && cbCustomImageBase.SelectedItem != null)
        {
            uint selectedValue = CUtils.ParseMinAppAddressValue(cbCustomImageBase.SelectedItem.ToString());
            _config.CustomImageBase = selectedValue;
        }

        if (_config.UseSymbols)
        {
            string symDllPath = dbghelpTextBox.Text;
            string symStorePath = symbolsStoreTextBox.Text;

            //
            // Set defaults in case if nothing selected.
            //
            if (string.IsNullOrEmpty(symDllPath))
            {
                symDllPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CConsts.DbgHelpDll);
            }
            if (string.IsNullOrEmpty(symStorePath))
            {
                string ntSymbolPath = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
                if (!string.IsNullOrWhiteSpace(ntSymbolPath))
                {
                    symStorePath = ntSymbolPath;
                }
                else
                {
                    symStorePath = $"srv*{Path.Combine(Path.GetTempPath(), CConsts.SymbolsDefaultStoreDirectory)}{CConsts.SymbolsDownloadLink}";
                }
            }

            _config.SymbolsDllPath = symDllPath;
            _config.SymbolsStorePath = symStorePath;
            _config.SymbolsHighlightColor = panelSymColor.BackColor;

        }

    }

    private void ButtonBrowse_Click(object sender, EventArgs e)
    {
        browseFileDialog.Filter = Properties.Resources.ResourceManager.GetString("AllFilesFilter");
        if (browseFileDialog.ShowDialog() == DialogResult.OK)
        {
            commandTextBox.Text = browseFileDialog.FileName;
        }
    }

    private void ButtonSelectAll_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem item in LVFileExt.Items)
        {
            item.Checked = true;
        }
    }

    private void ButtonDefaultURL_Click(object sender, EventArgs e)
    {
        searchOnlineTextBox.Text = CConsts.ExternalFunctionHelpURL;
    }

    private void ButtonAssociate_Click(object sender, EventArgs e)
    {
        string extensionValue = customExtBox.Text;

        if (!string.IsNullOrEmpty(extensionValue) && CUtils.IsAdministrator)
        {
            if (CUtils.SetAssoc(extensionValue))
            {
                MessageBox.Show($"{extensionValue} has been associated.");
            }
            else
            {
                MessageBox.Show("Cannot set extension association, check your access rights!",
                    CConsts.ShortProgramName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    private void CustomExtBox_KeyPress(object sender, KeyPressEventArgs e)
    {
        e.Handled = e.KeyChar switch
        {
            '\b' => false,
            >= 'a' and <= 'z' => false,
            >= 'A' and <= 'Z' => false,
            >= '0' and <= '9' => false,
            ' ' => true,
            _ => true
        };
    }

    private void TVSettings_AfterSelect(object sender, TreeViewEventArgs e)
    {
        TreeNode node = TVSettings.SelectedNode;

        foreach (TabPage page in settingsTabControl.TabPages)
        {
            if (node.Tag == page.Tag)
            {
                settingsTabControl.SelectedIndex = settingsTabControl.TabPages.IndexOf(page);
                TVSettings.Select();
                break;
            }
        }
    }

    private TreeView GetCurrentTVSearchOrder()
    {
        TreeView searchOrderView;

        if (settingsTabControl.SelectedTab == tabSearchOrder)
        {
            searchOrderView = TVSearchOrder;
        }
        else if (settingsTabControl.SelectedTab == tabSearchOrderDrivers)
        {
            searchOrderView = TVSearchOrderDrivers;
        }
        else
        {
            return null;
        }

        return searchOrderView;
    }

    private void MoveTreeNode(TreeView view, int direction)
    {
        if (view == null)
            return;

        TreeNode node = view.SelectedNode;
        if (node == null)
            return;

        // Get the parent node if this is a child node
        if (node.Parent != null)
            node = node.Parent;

        if (node.TreeView.Nodes.Contains(node))
        {
            int index = view.Nodes.IndexOf(node);
            int newIndex = index + direction;

            // Check bounds
            if (newIndex >= 0 && newIndex < view.Nodes.Count)
            {
                view.Nodes.RemoveAt(index);
                view.Nodes.Insert(newIndex, node);
                view.SelectedNode = node;
            }
        }

        view.Focus();
    }

    private void TVSearchOderMoveUp(object sender, EventArgs e)
    {
        MoveTreeNode(GetCurrentTVSearchOrder(), -1);
    }

    private void TVSearchOderMoveDown(object sender, EventArgs e)
    {
        MoveTreeNode(GetCurrentTVSearchOrder(), 1);
    }

    private void TVSearchOrderAfterSelect(object sender, TreeViewEventArgs e)
    {
        if (sender is not TreeView view || view.SelectedNode == null)
            return;

        var bMoveButtonsEnabled = true;
        bool bEnableDelButton = false;

        //
        // If the selected item is user defined directory then enable del button.
        //
        if (view.SelectedNode.Tag is string text &&
            text.Equals(CConsts.SearchOrderUserValue, StringComparison.OrdinalIgnoreCase))
        {
            bEnableDelButton = true;
        }

        if (view == TVSearchOrder)
        {
            MoveUpButton.Enabled = bMoveButtonsEnabled;
            MoveDownButton.Enabled = bMoveButtonsEnabled;
            DeleteUserDirectoryButton.Enabled = bEnableDelButton;
        }
        else
        {
            MoveUpButtonDrivers.Enabled = bMoveButtonsEnabled;
            MoveDownButtonDrivers.Enabled = bMoveButtonsEnabled;
            DeleteUserDirectoryDriversButton.Enabled = bEnableDelButton;
        }
    }

    private void ExpandSearchOrderButton_Click(object sender, EventArgs e)
    {
        var view = TVSearchOrder;

        if (settingsTabControl.SelectedTab == tabSearchOrder)
        {
            m_SearchOrderExpand = !m_SearchOrderExpand;
            if (m_SearchOrderExpand)
            {
                ExpandSearchOrderButton.Text = "Collapse All";
                view.ExpandAll();
            }
            else
            {
                ExpandSearchOrderButton.Text = "Expand All";
                view.CollapseAll();
            }
        }
        else
        {
            view = TVSearchOrderDrivers;
            m_SearchOrderDriversExpand = !m_SearchOrderDriversExpand;
            if (m_SearchOrderDriversExpand)
            {
                ExpandSearchOrderDrivers.Text = "Collapse All";
                view.ExpandAll();
            }
            else
            {
                ExpandSearchOrderDrivers.Text = "Expand All";
                view.CollapseAll();
            }
        }

        view.SelectedNode?.EnsureVisible();
        view.Select();
    }

    private void BrowseForServerAppClick(object sender, EventArgs e)
    {
        browseFileDialog.Filter = CConsts.ConfigBrowseFilter;
        if (browseFileDialog.ShowDialog() == DialogResult.OK)
        {
            serverAppLocationTextBox.Text = browseFileDialog.FileName;
            _coreClient?.SetServerApplication(browseFileDialog.FileName);
            CheckServerFileState(browseFileDialog.FileName);
        }
    }

    private void HistoryUpDown_ValueChanged(object sender, EventArgs e)
    {
        _config.HistoryDepth = Convert.ToInt32(historyUpDown.Value);
    }

    private void NodeMaxDepth_ValueChanged(object sender, EventArgs e)
    {
        _config.ModuleNodeDepthMax = Convert.ToInt32(nodeMaxDepthUpDown.Value);
    }

    private void CbMinAppAddressKeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (!string.IsNullOrEmpty(cbCustomImageBase.Text))
            {
                var selectedValue = CUtils.ParseMinAppAddressValue(cbCustomImageBase.Text);
                var stringValue = $"0x{selectedValue:X}";

                for (int i = 0; i < cbCustomImageBase.Items.Count; i++)
                {
                    if (stringValue == cbCustomImageBase.Items[i].ToString())
                    {
                        return;
                    }
                }
                cbCustomImageBase.SelectedIndex = cbCustomImageBase.Items.Add(stringValue);
            }
        }
    }

    private void AddUserDirectoryButtonClick(object sender, EventArgs e)
    {
        TreeNode rootNode = (settingsTabControl.SelectedTab == tabSearchOrder) ? m_UserDefinedDirectoryNodeUM : m_UserDefinedDirectoryNodeKM;

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            TreeNode subNode = new(folderBrowserDialog.SelectedPath)
            {
                ImageIndex = (int)SearchOrderIconType.Directory,
                SelectedImageIndex = (int)SearchOrderIconType.Directory,
                Tag = CConsts.SearchOrderUserValue
            };
            rootNode.Nodes.Add(subNode);
            rootNode.Expand();
        }
    }

    private void DeleteUserDirectoryButtonClick(object sender, EventArgs e)
    {
        TreeView view = GetCurrentTVSearchOrder();
        if (view?.SelectedNode?.Tag is string text &&
            text.Equals(CConsts.SearchOrderUserValue, StringComparison.OrdinalIgnoreCase))
        {
            view?.Nodes.Remove(view.SelectedNode);
        }
    }

    private void ConnectServerButtonClick(object sender, EventArgs e)
    {
        if (_coreClient != null)
        {
            if (_coreClient.ClientConnection != null)
            {
                if (_coreClient.ClientConnection.Connected)
                {
                    _coreClient.DisconnectClient();
                }
            }

            _coreClient.ConnectClient();
            ShowServerStatusAndSetControls();
        }
    }

    private void ButtonElevate_Click(object sender, EventArgs e)
    {
        try
        {
            if (null != Process.Start(new ProcessStartInfo
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = $"\"{Application.ExecutablePath}\"",
                Verb = "runas",
                UseShellExecute = true
            }))
            {
                Application.Exit();
            }

        }
        catch { }
    }

    private void ButtonApiSetBrowse_Click(object sender, EventArgs e)
    {
        browseFileDialog.Filter = Properties.Resources.ResourceManager.GetString("DllFilesFilter");
        if (browseFileDialog.ShowDialog() == DialogResult.OK)
        {
            apisetTextBox.Text = browseFileDialog.FileName;
            _config.ApiSetSchemaFile = browseFileDialog.FileName;

            _coreClient?.SetApiSetSchemaNamespaceUse(_config.ApiSetSchemaFile);
            ShowApiSetNamespaceInformation();
        }
    }

    private void SymButtons_Click(object sender, EventArgs e)
    {
        if (sender == buttonSymbolsBrowse)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                symbolsStoreTextBox.Text = $"srv*{folderBrowserDialog.SelectedPath}{CConsts.SymbolsDownloadLink}";
            }
        }
        else if (sender == buttonDbghelpBrowse)
        {
            browseFileDialog.Filter = CConsts.DbgHelpBrowseFilter;
            if (browseFileDialog.ShowDialog() == DialogResult.OK)
            {
                dbghelpTextBox.Text = browseFileDialog.FileName;
            }
        }
    }

    private void ButtonSymbolPickColor_Click(object sender, EventArgs e)
    {
        if (colorDialog.ShowDialog() == DialogResult.OK)
        {
            _config.SymbolsHighlightColor = colorDialog.Color;
            panelSymColor.BackColor = _config.SymbolsHighlightColor;
        }
    }

    private void ButtonSymbolsDefaults_Click(object sender, EventArgs e)
    {
        dbghelpTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CConsts.DbgHelpDll);
        symbolsStoreTextBox.Text = $"srv*{Path.Combine(Path.GetTempPath(), CConsts.SymbolsDefaultStoreDirectory)}{CConsts.SymbolsDownloadLink}";
        panelSymColor.BackColor = Color.Yellow;
    }
}
