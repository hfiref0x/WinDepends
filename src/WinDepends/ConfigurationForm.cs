/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CCONFIGURATIONFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        08 Oct 2024
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Diagnostics;

namespace WinDepends;

public partial class ConfigurationForm : Form
{
    private readonly string m_CurrentFileName = string.Empty;
    private readonly bool m_Is64bitFile;
    private bool m_SearchOrderExpand = true;
    private readonly CConfiguration m_CurrentConfiguration;

    public ConfigurationForm(string currentFileName, bool is64bitFile, CConfiguration currentConfiguration)
    {
        InitializeComponent();
        m_CurrentFileName = currentFileName;
        m_Is64bitFile = is64bitFile;
        m_CurrentConfiguration = currentConfiguration;
    }

    private void FillSearchOrderCategoryWithItems(TreeNode rootNode, SearchOrderType searchOrder)
    {
        int imageIndex = (int)SearchOderIconType.Module;
        string nodeName = "Cannot query content";
        TreeNode node;

        switch (searchOrder)
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
                    imageIndex = Directory.Exists(path) ? (int)SearchOderIconType.Directory : (int)SearchOderIconType.DirectoryBad;
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
                imageIndex = (int)SearchOderIconType.Directory;
                nodeName = CPathResolver.WindowsDirectory;
                break;

            case SearchOrderType.System32Directory:
                imageIndex = (int)SearchOderIconType.Directory;
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
                imageIndex = (int)SearchOderIconType.Directory;
                nodeName = CPathResolver.System16Directory;
                break;

            case SearchOrderType.ApplicationDirectory:
                imageIndex = (int)SearchOderIconType.Directory;
                nodeName = m_CurrentFileName;
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
        for (int i = 0; i < cbMinAppAddress.Items.Count; i++)
        {
            if (cbMinAppAddress.Items[i].ToString() == hexValue)
            {
                cbMinAppAddress.SelectedIndex = i;
                return true;
            }
        }

        return false;
    }

    private void ConfigurationForm_Load(object sender, EventArgs e)
    {
        TVSettings.ExpandAll();

        //
        // Setup state of controls.
        //
        foreach (Control ctrl in tabShellIntegrationPage.Controls)
        {
            ctrl.Enabled = CUtils.IsAdministrator;
        }

        shellIntegrationWarningLabel.Enabled = !CUtils.IsAdministrator;
        shellIntegrationWarningLabel.Visible = !CUtils.IsAdministrator;
        checkBox1.Checked = m_CurrentConfiguration.EscKeyEnabled;
        cbHistoryFullPath.Checked = m_CurrentConfiguration.HistoryShowFullPath;
        historyUpDown.Value = m_CurrentConfiguration.HistoryDepth;
        nodeMaxDepthUpDown.Value = m_CurrentConfiguration.ModuleNodeDepthMax;
        chBoxAutoExpands.Checked = m_CurrentConfiguration.AutoExpands;
        chBoxFullPaths.Checked = m_CurrentConfiguration.FullPaths;
        chBoxUndecorateSymbols.Checked = m_CurrentConfiguration.ViewUndecorated;
        chBoxResolveApiSets.Checked = m_CurrentConfiguration.ResolveAPIsets;
        chBoxHighlightApiSet.Checked = m_CurrentConfiguration.HighlightApiSet;
        chBoxApiSetNamespace.Checked = m_CurrentConfiguration.UseApiSetSchema;
        chBoxUpperCase.Checked = m_CurrentConfiguration.UppperCaseModuleNames;
        chBoxCompressSessionFiles.Checked = m_CurrentConfiguration.CompressSessionFiles;
        chBoxClearLogOnFileOpen.Checked = m_CurrentConfiguration.ClearLogOnFileOpen;
        chBoxUseReloc.Checked = m_CurrentConfiguration.UseRelocForImages;
        cbMinAppAddress.Enabled = m_CurrentConfiguration.UseRelocForImages;

        commandTextBox.Text = m_CurrentConfiguration.ExternalViewerCommand;
        argumentsTextBox.Text = m_CurrentConfiguration.ExternalViewerArguments;

        searchOnlineTextBox.Text = m_CurrentConfiguration.ExternalFunctionHelpURL;

        serverAppLocationTextBox.Text = m_CurrentConfiguration.CoreServerAppLocation;

        //
        // Elevate button setup.
        //
        buttonElevate.Visible = !CUtils.IsAdministrator;
        buttonElevate.Enabled = !CUtils.IsAdministrator;
        NativeMethods.AddShieldToButton(buttonElevate);

        //
        // Search order levels.
        //

        TVSearchOrder.ImageList = CUtils.CreateImageList(Properties.Resources.SearchOrderIcons,
            CConsts.SearchOrderIconsWidth, CConsts.SearchOrderIconsHeigth, Color.Magenta);

        foreach (var sol in m_CurrentConfiguration.SearchOrderList)
        {
            TreeNode tvNode = new(sol.ToDescription())
            {
                Tag = sol
            };
            TVSearchOrder.Nodes.Add(tvNode);
            FillSearchOrderCategoryWithItems(tvNode, sol);
        }

        TVSearchOrder.ExpandAll();

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
        cbMinAppAddress.Items.Clear();
        cbMinAppAddress.Items.Add($"0x{CUtils.MinAppAddress:X}");
        cbMinAppAddress.Items.Add($"0x{CConsts.DefaultAppStartAddress:X}");
        if (!SelectComboBoxItemByUintValue(m_CurrentConfiguration.MinAppAddress))
        {
            var i = cbMinAppAddress.Items.Add($"0x{m_CurrentConfiguration.MinAppAddress:X}");
            cbMinAppAddress.SelectedIndex = i;
        }

        labelAllocGran.Text = $"0x{CUtils.AllocationGranularity:X}";
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
        CheckBox checkBox = sender as CheckBox;
        switch (Convert.ToInt32(checkBox.Tag))
        {
            case CConsts.TagUseESC:
                m_CurrentConfiguration.EscKeyEnabled = checkBox.Checked;
                break;

            case CConsts.TagFullPaths:
                m_CurrentConfiguration.FullPaths = checkBox.Checked;
                break;

            case CConsts.TagAutoExpand:
                m_CurrentConfiguration.AutoExpands = checkBox.Checked;
                break;

            case CConsts.TagViewUndecorated:
                m_CurrentConfiguration.ViewUndecorated = checkBox.Checked;
                break;

            case CConsts.TagResolveAPIsets:
                m_CurrentConfiguration.ResolveAPIsets = checkBox.Checked;
                break;

            case CConsts.TagUpperCaseModuleNames:
                m_CurrentConfiguration.UppperCaseModuleNames = checkBox.Checked;
                break;

            case CConsts.TagClearLogOnFileOpen:
                m_CurrentConfiguration.ClearLogOnFileOpen = checkBox.Checked;
                break;

            case CConsts.TagCompressSessionFiles:
                m_CurrentConfiguration.CompressSessionFiles = checkBox.Checked;
                break;

            case CConsts.TagUseApiSetSchema:
                m_CurrentConfiguration.UseApiSetSchema = checkBox.Checked;
                break;

            case CConsts.TagHighlightApiSet:
                m_CurrentConfiguration.HighlightApiSet = checkBox.Checked;
                break;

            case CConsts.TagUseRelocForImages:
                m_CurrentConfiguration.UseRelocForImages = checkBox.Checked;
                cbMinAppAddress.Enabled = checkBox.Checked;
                break;

        }
    }

    private void ButtonEleavate_Click(object sender, EventArgs e)
    {
        try
        {
            if (null != Process.Start(new ProcessStartInfo
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Application.ExecutablePath,
                Verb = "runas",
                UseShellExecute = true
            }))
            {
                Application.Exit();
            }

        }
        catch { };

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
                if (item.Checked)
                {
                    CUtils.SetAssoc(item.Tag.ToString());
                }
            }
        }

        m_CurrentConfiguration.ExternalViewerCommand = commandTextBox.Text;
        m_CurrentConfiguration.ExternalViewerArguments = argumentsTextBox.Text;
        m_CurrentConfiguration.ExternalFunctionHelpURL = searchOnlineTextBox.Text;
        m_CurrentConfiguration.CoreServerAppLocation = serverAppLocationTextBox.Text;

        m_CurrentConfiguration.SearchOrderList.Clear();
        foreach (TreeNode node in TVSearchOrder.Nodes)
        {
            m_CurrentConfiguration.SearchOrderList.Add((SearchOrderType)node.Tag);
        }

        if (m_CurrentConfiguration.UseRelocForImages && cbMinAppAddress.SelectedItem != null)
        {
            uint selectedValue = ParseMinAppAddressValue(cbMinAppAddress.SelectedItem.ToString());
            m_CurrentConfiguration.MinAppAddress = selectedValue;
        }
    }

    private static UInt32 ParseMinAppAddressValue(string value)
    {
        try
        {
            string selectedHex = value.Substring(2); //remove prefix
            uint selectedValue = uint.Parse(selectedHex, System.Globalization.NumberStyles.HexNumber);
            selectedValue &= ~(CUtils.AllocationGranularity - 1);
            return selectedValue;
        }
        catch
        {
            return CConsts.DefaultAppStartAddress;
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
                break;
            }
        }
    }

    private void TVSearchOderMoveUp(object sender, EventArgs e)
    {
        var node = TVSearchOrder.SelectedNode;
        TreeView view = node.TreeView;

        if (node.Parent != null) node = node.Parent;

        if (node.TreeView.Nodes.Contains(node))
        {
            int index = view.Nodes.IndexOf(node);
            if (index > 0)
            {
                view.Nodes.RemoveAt(index);
                view.Nodes.Insert(index - 1, node);
                view.SelectedNode = node;
            }
        }

        TVSearchOrder.Focus();
    }

    private void TVSearchOderMoveDown(object sender, EventArgs e)
    {
        var node = TVSearchOrder.SelectedNode;
        TreeView view = node.TreeView;

        if (node.Parent != null) node = node.Parent;

        if (view != null && view.Nodes.Contains(node))
        {
            int index = view.Nodes.IndexOf(node);
            if (index < view.Nodes.Count - 1)
            {
                view.Nodes.RemoveAt(index);
                view.Nodes.Insert(index + 1, node);
                view.SelectedNode = node;
            }
        }

        TVSearchOrder.Focus();
    }

    private void TVSearchOrderAfterSelect(object sender, TreeViewEventArgs e)
    {
        var bEnabled = TVSearchOrder.SelectedNode != null;
        MoveUpButton.Enabled = bEnabled;
        MoveDownButton.Enabled = bEnabled;
    }

    private void ExpandSearchOrderButton_Click(object sender, EventArgs e)
    {
        m_SearchOrderExpand = !m_SearchOrderExpand;
        if (m_SearchOrderExpand)
        {
            ExpandSearchOrderButton.Text = "Collapse View";
            TVSearchOrder.ExpandAll();
        }
        else
        {
            ExpandSearchOrderButton.Text = "Expand View";
            TVSearchOrder.CollapseAll();
        }

    }

    private void BrowseForServerAppClick(object sender, EventArgs e)
    {
        browseFileDialog.Filter = CConsts.ConfigBrowseFilter;
        if (browseFileDialog.ShowDialog() == DialogResult.OK)
        {
            serverAppLocationTextBox.Text = browseFileDialog.FileName;
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
            if (!string.IsNullOrEmpty(cbMinAppAddress.Text))
            {
                var selectedValue = ParseMinAppAddressValue(cbMinAppAddress.Text);
                var stringValue = $"0x{selectedValue:X}";

                for (int i = 0; i < cbMinAppAddress.Items.Count; i++)
                {
                    if (stringValue == cbMinAppAddress.Items[i].ToString())
                    {
                        return;
                    }
                }
                cbMinAppAddress.SelectedIndex = cbMinAppAddress.Items.Add(stringValue);
            }
        }
    }

}
