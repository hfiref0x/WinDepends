/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CONFIGURATIONFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        11 Oct 2025
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
    readonly CConfiguration m_CurrentConfiguration;
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
        m_CurrentConfiguration = currentConfiguration;
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
        chBoxUseESCKey.Checked = m_CurrentConfiguration.EscKeyEnabled;
        cbHistoryFullPath.Checked = m_CurrentConfiguration.HistoryShowFullPath;
        historyUpDown.Value = m_CurrentConfiguration.HistoryDepth;
        nodeMaxDepthUpDown.Value = m_CurrentConfiguration.ModuleNodeDepthMax;
        chBoxAnalysisEnableExperimentalFeatures.Checked = m_CurrentConfiguration.EnableExperimentalFeatures;
        chBoxExpandForwarders.Checked = m_CurrentConfiguration.ExpandForwarders;
        chBoxAutoExpands.Checked = m_CurrentConfiguration.AutoExpands;
        chBoxFullPaths.Checked = m_CurrentConfiguration.FullPaths;
        chBoxUndecorateSymbols.Checked = m_CurrentConfiguration.ViewUndecorated;
        chBoxResolveApiSets.Checked = m_CurrentConfiguration.ResolveAPIsets;
        chBoxHighlightApiSet.Checked = m_CurrentConfiguration.HighlightApiSet;
        chBoxApiSetNamespace.Checked = m_CurrentConfiguration.UseApiSetSchemaFile;
        chBoxUpperCase.Checked = m_CurrentConfiguration.UpperCaseModuleNames;
        chBoxCompressSessionFiles.Checked = m_CurrentConfiguration.CompressSessionFiles;
        chBoxClearLogOnFileOpen.Checked = m_CurrentConfiguration.ClearLogOnFileOpen;
        cbCustomImageBase.Enabled = m_CurrentConfiguration.UseCustomImageBase;
        if (m_CurrentConfiguration.UseCustomImageBase)
        {
            m_CurrentConfiguration.ProcessRelocsForImage = true;
        }
        chBoxProcessRelocs.Checked = m_CurrentConfiguration.ProcessRelocsForImage;
        chBoxUseStats.Checked = m_CurrentConfiguration.UseStats;
        chBoxAnalysisDefaultEnabled.Checked = m_CurrentConfiguration.AnalysisSettingsUseAsDefault;
        chBoxPropagateSettings.Checked = m_CurrentConfiguration.PropagateSettingsOnDependencies;
        chBoxUseSymbols.Checked = m_CurrentConfiguration.UseSymbols;

        groupBoxSymbols.Enabled = m_CurrentConfiguration.UseSymbols;

        commandTextBox.Text = m_CurrentConfiguration.ExternalViewerCommand;
        argumentsTextBox.Text = m_CurrentConfiguration.ExternalViewerArguments;

        searchOnlineTextBox.Text = m_CurrentConfiguration.ExternalFunctionHelpURL;

        serverAppLocationTextBox.Text = m_CurrentConfiguration.CoreServerAppLocation;

        symbolsStoreTextBox.Text = m_CurrentConfiguration.SymbolsStorePath;
        dbghelpTextBox.Text = m_CurrentConfiguration.SymbolsDllPath;

        panelSymColor.BackColor = m_CurrentConfiguration.SymbolsHighlightColor;

        buttonApiSetBrowse.Enabled = m_CurrentConfiguration.UseApiSetSchemaFile;
        if (m_CurrentConfiguration.UseApiSetSchemaFile)
        {
            apisetTextBox.Text = m_CurrentConfiguration.ApiSetSchemaFile;
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
            m_CurrentConfiguration.SearchOrderListUM,
            m_CurrentConfiguration.UserSearchOrderDirectoriesUM,
            ref m_UserDefinedDirectoryNodeUM);

        CreateSearchOrderView(TVSearchOrderDrivers,
            m_CurrentConfiguration.SearchOrderListKM,
            m_CurrentConfiguration.UserSearchOrderDirectoriesKM,
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
        if (!SelectComboBoxItemByUintValue(m_CurrentConfiguration.CustomImageBase))
        {
            var i = cbCustomImageBase.Items.Add($"0x{m_CurrentConfiguration.CustomImageBase:X}");
            cbCustomImageBase.SelectedIndex = i;
        }

        labelAllocGran.Text = $"0x{CUtils.AllocationGranularity:X}";

        CheckServerFileState(m_CurrentConfiguration.CoreServerAppLocation);
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
        if (e.KeyCode == Keys.Escape && m_CurrentConfiguration.EscKeyEnabled)
        {
            this.Close();
        }
    }

    private void HistoryFullPath_Click(object sender, EventArgs e)
    {
        m_CurrentConfiguration.HistoryShowFullPath = cbHistoryFullPath.Checked;
    }

    private void ChBox_Click(object sender, EventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag == null)
            return;

        bool isChecked = checkBox.Checked;

        switch (Convert.ToInt32(checkBox.Tag))
        {
            case CConsts.TagUseESC:
                m_CurrentConfiguration.EscKeyEnabled = isChecked;
                break;

            case CConsts.TagFullPaths:
                m_CurrentConfiguration.FullPaths = isChecked;
                break;

            case CConsts.TagAutoExpand:
                m_CurrentConfiguration.AutoExpands = isChecked;
                break;

            case CConsts.TagViewUndecorated:
                m_CurrentConfiguration.ViewUndecorated = isChecked;
                break;

            case CConsts.TagResolveAPIsets:
                m_CurrentConfiguration.ResolveAPIsets = isChecked;
                break;

            case CConsts.TagUpperCaseModuleNames:
                m_CurrentConfiguration.UpperCaseModuleNames = isChecked;
                break;

            case CConsts.TagClearLogOnFileOpen:
                m_CurrentConfiguration.ClearLogOnFileOpen = isChecked;
                break;

            case CConsts.TagCompressSessionFiles:
                m_CurrentConfiguration.CompressSessionFiles = isChecked;
                break;

            case CConsts.TagUseApiSetSchemaFile:
                m_CurrentConfiguration.UseApiSetSchemaFile = isChecked;
                buttonApiSetBrowse.Enabled = isChecked;
                break;

            case CConsts.TagHighlightApiSet:
                m_CurrentConfiguration.HighlightApiSet = isChecked;
                break;

            case CConsts.TagProcessRelocsForImage:
                m_CurrentConfiguration.ProcessRelocsForImage = isChecked;
                break;

            case CConsts.TagUseCustomImageBase:
                m_CurrentConfiguration.UseCustomImageBase = isChecked;
                if (m_CurrentConfiguration.UseCustomImageBase)
                {
                    m_CurrentConfiguration.ProcessRelocsForImage = true;
                    chBoxProcessRelocs.Checked = true;
                }
                cbCustomImageBase.Enabled = isChecked;
                break;

            case CConsts.TagUseStats:
                m_CurrentConfiguration.UseStats = isChecked;
                break;

            case CConsts.TagAnalysisDefaultEnabled:
                m_CurrentConfiguration.AnalysisSettingsUseAsDefault = isChecked;
                break;

            case CConsts.TagPropagateSettingsEnabled:
                m_CurrentConfiguration.PropagateSettingsOnDependencies = isChecked;
                break;

            case CConsts.TagUseSymbols:
                m_CurrentConfiguration.UseSymbols = isChecked;
                groupBoxSymbols.Enabled = isChecked;
                break;

            case CConsts.TagEnableExperimentalFeatures:
                m_CurrentConfiguration.EnableExperimentalFeatures = isChecked;
                break;
            case CConsts.TagExpandForwarders:
                m_CurrentConfiguration.ExpandForwarders = isChecked;
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

        m_CurrentConfiguration.ExternalViewerCommand = commandTextBox.Text;
        m_CurrentConfiguration.ExternalViewerArguments = argumentsTextBox.Text;
        m_CurrentConfiguration.ExternalFunctionHelpURL = searchOnlineTextBox.Text;
        m_CurrentConfiguration.CoreServerAppLocation = serverAppLocationTextBox.Text;

        if (m_CurrentConfiguration.UseApiSetSchemaFile && (!string.IsNullOrEmpty(apisetTextBox.Text)))
        {
            m_CurrentConfiguration.ApiSetSchemaFile = apisetTextBox.Text;
        }
        else
        {
            m_CurrentConfiguration.ApiSetSchemaFile = string.Empty;
        }

        SetSearchOrderList(TVSearchOrder,
            m_CurrentConfiguration.SearchOrderListUM,
            m_CurrentConfiguration.UserSearchOrderDirectoriesUM,
            m_UserDefinedDirectoryNodeUM);

        SetSearchOrderList(TVSearchOrderDrivers,
            m_CurrentConfiguration.SearchOrderListKM,
            m_CurrentConfiguration.UserSearchOrderDirectoriesKM,
            m_UserDefinedDirectoryNodeKM);

        if (m_CurrentConfiguration.UseCustomImageBase && cbCustomImageBase.SelectedItem != null)
        {
            uint selectedValue = CUtils.ParseMinAppAddressValue(cbCustomImageBase.SelectedItem.ToString());
            m_CurrentConfiguration.CustomImageBase = selectedValue;
        }

        if (m_CurrentConfiguration.UseSymbols)
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

            m_CurrentConfiguration.SymbolsDllPath = symDllPath;
            m_CurrentConfiguration.SymbolsStorePath = symStorePath;
            m_CurrentConfiguration.SymbolsHighlightColor = panelSymColor.BackColor;

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
        m_CurrentConfiguration.HistoryDepth = Convert.ToInt32(historyUpDown.Value);
    }

    private void NodeMaxDepth_ValueChanged(object sender, EventArgs e)
    {
        m_CurrentConfiguration.ModuleNodeDepthMax = Convert.ToInt32(nodeMaxDepthUpDown.Value);
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
            m_CurrentConfiguration.ApiSetSchemaFile = browseFileDialog.FileName;

            _coreClient?.SetApiSetSchemaNamespaceUse(m_CurrentConfiguration.ApiSetSchemaFile);
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
            m_CurrentConfiguration.SymbolsHighlightColor = colorDialog.Color;
            panelSymColor.BackColor = m_CurrentConfiguration.SymbolsHighlightColor;
        }
    }

    private void ButtonSymbolsDefaults_Click(object sender, EventArgs e)
    {
        dbghelpTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CConsts.DbgHelpDll);
        symbolsStoreTextBox.Text = $"srv*{Path.Combine(Path.GetTempPath(), CConsts.SymbolsDefaultStoreDirectory)}{CConsts.SymbolsDownloadLink}";
        panelSymColor.BackColor = Color.Yellow;
    }
}
