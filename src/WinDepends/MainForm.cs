/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       MAINFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        28 Feb 2025
*  
*  Codename:    VasilEk
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;

namespace WinDepends;

public enum DisplayCacheType
{
    Imports,
    Exports,
    Modules
}
enum ProcessModuleAction
{
    ShowProperties,
    ExternalViewer,
    OpenFileLocation
}

enum FileOpenResult
{
    Success,
    Failure,
    Cancelled
}

public partial class MainForm : Form
{
    /// <summary>
    /// Depends-core interface class.
    /// </summary>
    readonly CCoreClient m_CoreClient;

    /// <summary>
    /// Depends current context, must be initialized.
    /// </summary>
    CDepends m_Depends;

    /// <summary>
    /// Most Recently Used file list.
    /// </summary>
    CMRUList? m_MRUList;

    /// <summary>
    /// Program settings.
    /// </summary>
    CConfiguration m_Configuration;

    /// <summary>
    /// Workaround for WinForms glitches.
    /// </summary>
    Control? focusedControl;

    /// <summary>
    /// Root node of TVModules, must be initialized with AddModule*** routine.
    /// </summary>
    TreeNode m_RootNode;

    //
    // Flags used in Highlight instance operations
    //
    bool m_InstanceStopSearch;
    bool m_InstanceSelfFound;

    readonly Form m_FunctionsHintForm;
    readonly Form m_ModulesHintForm;

    string m_FunctionLookupText = string.Empty;
    string m_ModuleLookupText = string.Empty;

    string m_SearchFunctionName;
    UInt32 m_SearchOrdinal;

    readonly string[] CommandLineArgs;

    /// <summary>
    /// Log search releated variables.
    /// </summary>
    public RichTextBoxFinds LogFindOptions { get; set; }
    public int LogSearchPosition { get; set; }
    public string LogFindText { get; set; }
    public int LogIndexOfSearchText { get; set; }

    readonly DebugEntryType[] m_KnownDebugTypes = [
                DebugEntryType.Unknown,
                DebugEntryType.Coff,
                DebugEntryType.CodeView,
                DebugEntryType.Misc,
                DebugEntryType.Fpo,
                DebugEntryType.OmapFromSrc,
                DebugEntryType.OmapToSrc,
                DebugEntryType.Borland];

    ListViewItem[] LVImportsCache = [];
    ListViewItem[] LVExportsCache = [];

    ListViewItem[] LVModulesCache = [];

    int LVImportsFirstItem;
    int LVExportsFirstItem;

    int LVModulesFirstItem;

    List<CFunction> m_CurrentExportsList = [];
    List<CFunction> m_CurrentImportsList = [];

    readonly Dictionary<int, FunctionHashObject> m_ParentImportsHashTable = [];

    readonly List<CModule> m_LoadedModulesList = [];

    SortOrder LVImportsSortOrder = SortOrder.Ascending;
    SortOrder LVExportsSortOrder = SortOrder.Ascending;
    SortOrder LVModulesSortOrder = SortOrder.Ascending;

    public MainForm()
    {
        InitializeComponent();

        //
        // Add welcome message to the log.
        //
        LogEvent($"{CConsts.ProgramName} started, " +
            $"version {CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild} BETA",
            LogEventType.StartMessage);

        m_Configuration = CConfigManager.LoadConfiguration();
        CPathResolver.UserDirectoriesKM = m_Configuration.UserSearchOrderDirectoriesKM;
        CPathResolver.UserDirectoriesUM = m_Configuration.UserSearchOrderDirectoriesUM;

        bool bSymbolsAllocated = false;

        if (m_Configuration.UseSymbols)
        {
            bSymbolsAllocated = CSymbolResolver.AllocateSymbolResolver(m_Configuration.SymbolsDllPath, m_Configuration.SymbolsStorePath);
        }
        //
        // Check for command line parameters.
        //
        CommandLineArgs = Environment.GetCommandLineArgs();

        //
        // Start server app.
        //       
        m_CoreClient = new(m_Configuration.CoreServerAppLocation, CConsts.CoreServerAddress, CConsts.CoreServerPort, LogEvent);
        if (m_CoreClient.ConnectClient())
        {
            if (m_CoreClient.GetKnownDllsAll(CPathResolver.KnownDlls,
                CPathResolver.KnownDlls32,
                out string path, out string path32))
            {
                CPathResolver.KnownDllsPath = path;
                CPathResolver.KnownDllsPath32 = path32;
            }

            m_CoreClient.SetApiSetSchemaNamespaceUse(m_Configuration.ApiSetSchemaFile);
        }

        //
        // Display this message after server initialization message.
        //
        if (bSymbolsAllocated)
        {
            LogEvent($"Debug symbols initialized using \"{m_Configuration.SymbolsDllPath}\", " +
                $"store \"{m_Configuration.SymbolsStorePath}\"", LogEventType.SymStateChange);
        }

        LVExports.VirtualMode = true;
        LVExports.VirtualListSize = 0;

        LVImports.VirtualMode = true;
        LVImports.VirtualListSize = 0;

        LVModules.VirtualMode = true;
        LVModules.VirtualListSize = 0;

        m_FunctionsHintForm = CreateHintForm(CConsts.HintFormLabelControl);
        m_ModulesHintForm = CreateHintForm(CConsts.HintFormLabelControl);
    }

    /// <summary>
    /// Creates a small borderless form used as tooltip.
    /// </summary>
    /// <param name="LabelName"></param>
    /// <returns></returns>
    static Form CreateHintForm(string LabelName)
    {
        var resultForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            TopMost = true,
            BackColor = Color.LightYellow
        };

        var resultFormLabel = new Label
        {
            AutoSize = true,
            Location = new Point(5, 5),
            Name = LabelName
        };

        resultForm.Controls.Add(resultFormLabel);
        return resultForm;
    }

    /// <summary>
    /// Insert module entry to TVModules treeview.
    /// </summary>
    /// <returns>Tree node.</returns>
    private TreeNode AddModuleEntry(CModule module, CFileOpenSettings fileOpenSettings, TreeNode parentNode = null)
    {
        CModule parentModule;
        bool currentModuleIsRoot = (parentNode == null);

        //
        // Respect tree depth settings.
        //
        if (!currentModuleIsRoot)
        {
            parentModule = (CModule)parentNode.Tag;
            if (parentModule.Depth > m_Configuration.ModuleNodeDepthMax)
            {
                return null;
            }
        }

        //
        // Most important part of the entire function.
        // Create module instance id (hash from filename) and look if it is already in list.
        // If it - then link this item with new one as original instance.
        // This allows us to exclude duplicate information gathering thus
        // decreasing resource usage and increasing overall enumeration performance.
        //
        module.InstanceId = module.GetHashCode();

        CModule origInstance = CUtils.GetModuleByHash(module.FileName, m_LoadedModulesList);
        if (origInstance != null)
        {
            module.OriginalInstanceId = origInstance.InstanceId;
            module.FileNotFound = origInstance.FileNotFound;
            module.ExportContainErrors = origInstance.ExportContainErrors;
            module.Invalid = origInstance.Invalid;
            module.OtherErrorsPresent = origInstance.OtherErrorsPresent;
            module.ModuleData = new(origInstance.ModuleData);
        }

        string moduleDisplayName = module.GetModuleNameRespectApiSet(m_Configuration.ResolveAPIsets);

        if (!m_Configuration.FullPaths)
        {
            moduleDisplayName = Path.GetFileName(moduleDisplayName);
        }

        if (m_Configuration.UppperCaseModuleNames)
        {
            moduleDisplayName = moduleDisplayName.ToUpperInvariant();
        }

        //
        // Add item to TreeView.
        //
        TreeNode tvNode = new(moduleDisplayName);

        //
        // Collect information.
        //
        if (origInstance == null)
        {
            bool useReloc = false;
            bool useStats = false;
            uint minAppAddress = 0;

            //
            // If this is not a root node.
            //
            if (!currentModuleIsRoot)
            {
                if (fileOpenSettings.PropagateSettingsOnDependencies)
                {
                    useReloc = fileOpenSettings.UseRelocForImages;
                    minAppAddress = fileOpenSettings.MinAppAddress;
                    useStats = fileOpenSettings.UseStats;
                }
            }
            else
            {
                useReloc = fileOpenSettings.UseRelocForImages;
                minAppAddress = fileOpenSettings.MinAppAddress;
                useStats = fileOpenSettings.UseStats;
            }

            ModuleOpenStatus openStatus = m_CoreClient.OpenModule(ref module, useStats, useReloc, minAppAddress);

            switch (openStatus)
            {
                case ModuleOpenStatus.Okay:

                    module.IsProcessed = m_CoreClient.GetModuleHeadersInformation(module);
                    if (!module.IsProcessed)
                    {
                        LogEvent(module.FileName, LogEventType.ModuleProcessingError);
                    }

                    //
                    // If this is root module, setup resolver.
                    //
                    if (currentModuleIsRoot)
                    {
                        CPathResolver.QueryFileInformation(module);
                    }

                    m_CoreClient.GetModuleImportExportInformation(module,
                        m_Configuration.SearchOrderListUM,
                        m_Configuration.SearchOrderListKM,
                        m_ParentImportsHashTable);

                    CCoreCallStats stats = null;
                    if (useStats)
                    {
                        stats = m_CoreClient.GetCoreCallStats();
                    }

                    m_CoreClient.CloseModule();

                    //
                    // Display statistics.
                    //
                    if (m_Configuration.UseStats && stats != null)
                    {
                        string sizeText;
                        if (stats.TotalBytesSent >= 1024 * 1024)
                        {
                            sizeText = $"{stats.TotalBytesSent / (1024 * 1024)} MB";
                        }
                        else if (stats.TotalBytesSent >= 1024)
                        {
                            sizeText = $"{stats.TotalBytesSent / 1024} KB";
                        }
                        else
                        {
                            sizeText = $"{stats.TotalBytesSent} byte";
                        }

                        var statsData = $"[STATS {Path.GetFileName(module.FileName)}] Received: {sizeText}, \"send\" calls: {stats.TotalSendCalls}, \"send\" time spent (�s): {stats.TotalTimeSpent}";
                        LogEvent(module.FileName, LogEventType.FileStats, statsData);
                    }

                    if (module.ExportContainErrors)
                    {
                        LogEvent(module.FileName, LogEventType.ModuleExportsError);
                    }

                    if (module.ModuleData.Machine != m_Depends.RootModule.ModuleData.Machine)
                    {
                        module.OtherErrorsPresent = true;
                        LogEvent(module.FileName, LogEventType.ModuleMachineMismatch);
                    }
                    break;

                case ModuleOpenStatus.ErrorUnspecified:
                    LogEvent(module.FileName, LogEventType.ModuleOpenHardError);
                    break;
                case ModuleOpenStatus.ErrorSendCommand:
                    LogEvent(module.FileName, LogEventType.ModuleOpenHardError, "Send command failure.");
                    break;
                case ModuleOpenStatus.ErrorReceivedDataInvalid:
                    LogEvent(module.FileName, LogEventType.ModuleOpenHardError, "Received data is invalid.");
                    break;
                case ModuleOpenStatus.ErrorCannotReadFileHeaders:
                    LogEvent(module.FileName, LogEventType.ModuleOpenHardError, "Cannot read file headers.");
                    break;
                case ModuleOpenStatus.ErrorInvalidHeadersOrSignatures:
                    if (module.IsDelayLoad)
                    {
                        LogEvent(module.FileName, LogEventType.ModuleOpenHardError, "Delay-load module has invalid headers or signatures.");
                    }
                    else
                    {
                        LogEvent(module.FileName, LogEventType.ModuleOpenHardError, "Module has invalid headers or signatures.");
                    }
                    break;

                case ModuleOpenStatus.ErrorFileNotFound:

                    // In case if this is ApiSets failure.
                    // API-* are mandatory to load, while EXT-* are not.
                    bool bExtApiSet = module.IsApiSetContract && module.RawFileName.StartsWith("EXT-", StringComparison.OrdinalIgnoreCase);

                    if (module.IsDelayLoad)
                    {
                        LogEvent(module.FileName, bExtApiSet ? LogEventType.DelayLoadModuleNotFoundExtApiSet : LogEventType.DelayLoadModuleNotFound);
                    }
                    else
                    {
                        LogEvent(module.FileName, bExtApiSet ? LogEventType.ModuleNotFoundExtApiSet : LogEventType.ModuleNotFound);
                    }
                    break;
            }

        }

        //
        // Select module icon type.
        //
        module.ModuleImageIndex = module.GetIconIndexForModule();

        tvNode.Tag = module;
        tvNode.ImageIndex = module.ModuleImageIndex;
        tvNode.SelectedImageIndex = module.ModuleImageIndex;

        if (!currentModuleIsRoot)
        {
            parentModule = (CModule)parentNode.Tag;
            module.Depth = parentModule.Depth + 1;
            parentNode.Nodes.Add(tvNode);
        }
        else
        {
            TVModules.Nodes.Add(tvNode);
        }

        //
        // Higlight apisets.
        //
        tvNode.ForeColor = (module.IsApiSetContract && m_Configuration.HighlightApiSet) ? Color.Blue : Color.Black;

        //
        // Check if module is already added.
        //
        if (origInstance == null)
        {
            m_LoadedModulesList.Add(module);
            LVModules.VirtualListSize = m_LoadedModulesList.Count;
        }

        return tvNode;
    }

    /// <summary>
    /// Adds module entry from loaded session object (saved session view).
    /// </summary>
    /// <param name="module">Module entry to be added.</param>
    /// <param name="parentNode">Parent node if present.</param>
    /// <returns>Tree node entry.</returns>
    private TreeNode AddSessionModuleEntry(CModule module, TreeNode parentNode = null)
    {
        //
        // Respect tree depth settings.
        //
        if (parentNode != null)
        {
            CModule parentModule = (CModule)parentNode.Tag;
            if (parentModule.Depth > m_Depends.SessionNodeMaxDepth)
            {
                return null;
            }
        }

        int moduleImageIndex = module.ModuleImageIndex;
        string moduleDisplayName = module.GetModuleNameRespectApiSet(m_Configuration.ResolveAPIsets);

        CModule origInstance = CUtils.GetModuleByHash(module.FileName, m_LoadedModulesList);

        if (!m_Configuration.FullPaths)
        {
            moduleDisplayName = Path.GetFileName(moduleDisplayName);
        }

        if (m_Configuration.UppperCaseModuleNames)
        {
            moduleDisplayName = moduleDisplayName.ToUpperInvariant();
        }

        //
        // Add item to TreeView.
        //
        TreeNode tvNode = new(moduleDisplayName)
        {
            Tag = module,
            ImageIndex = moduleImageIndex,
            SelectedImageIndex = moduleImageIndex,
            ForeColor = (module.IsApiSetContract && m_Configuration.HighlightApiSet) ? Color.Blue : Color.Black
        };

        if (parentNode != null)
        {
            parentNode.Nodes.Add(tvNode);
        }
        else
        {
            TVModules.Nodes.Add(tvNode);
        }

        //
        // If module is original then add it to the list.
        //
        if (origInstance == null)
        {
            m_LoadedModulesList.Add(module);
            LVModules.VirtualListSize = m_LoadedModulesList.Count;
        }

        return tvNode;
    }

    /// <summary>
    /// Return name of one of the active lists that has focus.
    /// </summary>
    /// <returns>Name of the focused control.</returns>
    private string? FindFocusedListControlName()
    {
        if (TVModules.Focused) return TVModules.Name;
        if (LVModules.Focused) return LVModules.Name;
        if (LVImports.Focused) return LVImports.Name;
        if (LVExports.Focused) return LVExports.Name;
        return null;
    }

    private void ResetDisplayCache(DisplayCacheType cacheType)
    {
        switch (cacheType)
        {
            case DisplayCacheType.Imports:
                LVImportsCache = null;
                LVImportsFirstItem = 0;
                break;
            case DisplayCacheType.Exports:
                LVExportsCache = null;
                LVExportsFirstItem = 0;
                break;
            case DisplayCacheType.Modules:
                LVModulesCache = null;
                LVModulesFirstItem = 0;
                break;
        }
    }

    private void ResetFunctionLists()
    {
        LVImports.Invalidate();
        LVExports.Invalidate();
        ResetDisplayCache(DisplayCacheType.Imports);
        ResetDisplayCache(DisplayCacheType.Exports);
        LVImports.VirtualListSize = LVExports.VirtualListSize = 0;
    }

    public void ResetModulesList()
    {
        LVModules.Invalidate();
        ResetDisplayCache(DisplayCacheType.Modules);
        LVModules.VirtualListSize = 0;
        m_LoadedModulesList.Clear();
    }

    /// <summary>
    /// Reset main form controls.
    /// </summary>
    public void ResetFileView()
    {
        //
        // There is no session opened, disable buttons.
        //
        CopyToolButton.Enabled = false;
        SaveToolButton.Enabled = false;

        //
        // Clear tree/list views.
        //
        TVModules.Nodes.Clear();

        ResetFunctionLists();
        ResetModulesList();

        //
        // Force garbage collection.
        //
        GC.Collect();
    }

    /// <summary>
    /// Recursively update tree nodes respecting module display settings.
    /// </summary>
    /// <param name="startNode"></param>
    private void TreeViewUpdateNode(TreeNode startNode)
    {
        while (startNode != null)
        {
            CModule module = (CModule)startNode.Tag;
            string moduleDisplayName = module.GetModuleNameRespectApiSet(m_Configuration.ResolveAPIsets);
            string fName = (m_Configuration.FullPaths) ? moduleDisplayName : Path.GetFileName(moduleDisplayName);

            if (m_Configuration.UppperCaseModuleNames)
            {
                fName = fName.ToUpperInvariant();
            }

            startNode.Text = fName;
            startNode.ForeColor = (module.IsApiSetContract && m_Configuration.HighlightApiSet) ? Color.Blue : Color.Black;

            if (startNode.Nodes.Count != 0)
            {
                TreeViewUpdateNode(startNode.Nodes[0]);
            }

            startNode = startNode.NextNode;
        }
    }

    public void UpdateItemsView(ListView listView, DisplayCacheType cacheType)
    {
        listView.BeginUpdate();
        listView.Invalidate();
        ResetDisplayCache(cacheType);
        listView.EndUpdate();
    }

    /// <summary>
    /// Update file view display according to a new settings.
    /// </summary>
    /// <param name="action"></param>
    public void UpdateFileView(FileViewUpdateAction action)
    {
        switch (action)
        {
            case FileViewUpdateAction.TreeViewAutoExpandsChange:
                {
                    if (m_Configuration.AutoExpands)
                    {
                        TVModules.ExpandAll();
                    }
                }
                break;

            case FileViewUpdateAction.ModulesTreeAndListChange:
                {
                    TVModules.BeginUpdate();
                    TreeViewUpdateNode(m_RootNode);
                    TVModules.EndUpdate();

                    UpdateItemsView(LVModules, DisplayCacheType.Modules);
                }
                break;

            case FileViewUpdateAction.FunctionsUndecorateChange:
                {
                    UpdateItemsView(LVImports, DisplayCacheType.Imports);
                    UpdateItemsView(LVExports, DisplayCacheType.Exports);
                }
                break;
        }

    }

    private void CreateImageListsForViews()
    {
        TVModules.ImageList = CUtils.CreateImageList(Properties.Resources.ModuleIconsAll,
        CConsts.ModuleIconsAllWidth, CConsts.ModuleIconsAllHeight, Color.Magenta);

        LVModules.SmallImageList = CUtils.CreateImageList(Properties.Resources.ModuleIcons,
            CConsts.ModuleIconsWidth, CConsts.ModuleIconsHeight, Color.Magenta);

        LVImports.SmallImageList = CUtils.CreateImageList(Properties.Resources.FunctionIcons,
            CConsts.FunctionIconsWidth, CConsts.FunctionIconsHeigth, Color.Magenta);

        LVExports.SmallImageList = CUtils.CreateImageList(Properties.Resources.FunctionIcons,
            CConsts.FunctionIconsWidth, CConsts.FunctionIconsHeigth, Color.Magenta);

        LVExports.Columns[m_Configuration.SortColumnExports].Text =
            $"{CConsts.AscendSortMark} {LVExports.Columns[m_Configuration.SortColumnExports].Text}";
    }

    /// <summary>
    /// Drag and Drop handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainForm_ElevatedDragDrop(System.Object sender, ElevatedDragDropEventArgs e)
    {
        if (e.Files.Count > 0 && !string.IsNullOrEmpty(e.Files[0]))
        {
            OpenInputFile(e.Files[0]);
        }
    }

    /// <summary>
    /// MainForm load (Create) event.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainForm_Load(object sender, EventArgs e)
    {
        //
        // Enable drag and drop for elevated instance.
        //
        ElevatedDragDropManager.EnableDragDrop(this.Handle);
        ElevatedDragDropManager.EnableDragDrop(LVImports.Handle);
        ElevatedDragDropManager.EnableDragDrop(LVExports.Handle);
        ElevatedDragDropManager.EnableDragDrop(reLog.Handle);
        ElevatedDragDropManager.EnableDragDrop(LVModules.Handle);
        ElevatedDragDropManager.EnableDragDrop(TVModules.Handle);
        ElevatedDragDropManager.GetInstance().ElevatedDragDrop += MainForm_ElevatedDragDrop;

        //
        // Create and load Most Recently Used files.
        //
        m_MRUList = new CMRUList(FileMenuItem,
            FileMenuItem.DropDownItems.IndexOf(MenuOpenNewInstance),
            m_Configuration.MRUList,
            m_Configuration.HistoryDepth,
            m_Configuration.HistoryShowFullPath,
            OpenInputFile,
            toolBarStatusLabel);

        //
        // Setup image lists for tree/list views.
        //
        CreateImageListsForViews();

        //
        // Toolbar images setup.
        //
        MainToolBar.ImageList = CUtils.CreateImageList(Properties.Resources.ToolBarIcons,
            CConsts.ToolBarIconsHeigth, CConsts.ToolBarIconsWidth, Color.Silver);

        if (MainToolBar.ImageList != null)
        {
            OpenToolButton.ImageIndex = (int)ToolBarIconType.OpenFile;
            SaveToolButton.ImageIndex = (int)ToolBarIconType.SaveFile;
            CopyToolButton.ImageIndex = (int)ToolBarIconType.Copy;
            AutoExpandToolButton.ImageIndex = (int)ToolBarIconType.AutoExpand;
            ViewFullPathsToolButton.ImageIndex = (int)ToolBarIconType.FullPaths;
            ViewUndecoratedToolButton.ImageIndex = (int)ToolBarIconType.ViewUndecorated;
            ViewModulesToolButton.ImageIndex = (int)ToolBarIconType.ViewModulesInExternalViewer;
            ConfigureToolButton.ImageIndex = (int)ToolBarIconType.Configuration;
            ResolveAPISetsToolButton.ImageIndex = (int)ToolBarIconType.ResolveAPISets;
            PropertiesToolButton.ImageIndex = (int)ToolBarIconType.Properties;
            SystemInfoToolButton.ImageIndex = (int)ToolBarIconType.SystemInformation;
        }

        //
        // Check toolbar buttons depending on settings.
        //
        AutoExpandToolButton.Checked = m_Configuration.AutoExpands;
        ViewFullPathsToolButton.Checked = m_Configuration.FullPaths;
        ViewUndecoratedToolButton.Checked = m_Configuration.ViewUndecorated;
        ResolveAPISetsToolButton.Checked = m_Configuration.ResolveAPIsets;

        //
        // Disable Save/Copy buttons by default.
        //
        SaveToolButton.Enabled = false;
        CopyToolButton.Enabled = false;

        //
        // Set program title.
        //
        this.Text = CConsts.ProgramName;
        if (CUtils.IsAdministrator)
        {
            if (Environment.Is64BitProcess)
            {
                this.Text += " (Administrator, 64-bit)";
            }
            else
            {
                this.Text += " (Administrator)";
            }
        }
        else
        {
            if (Environment.Is64BitProcess)
            {
                this.Text += " (64-bit)";
            }
        }

        var fileOpened = false;

        //
        // Open file (if it was submitted through command line).
        //
        if (CommandLineArgs.Length > 1)
        {
            var fName = CommandLineArgs[1];
            if (!string.IsNullOrEmpty(fName) && File.Exists(fName))
            {
                fileOpened = OpenInputFile(fName);
            }
        }

        if (!fileOpened)
        {
            toolBarStatusLabel.Text = "Use the menu File->Open or drag and drop a file into the window to begin analysis";
        }

    }

    /// <summary>
    /// MainForm close handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        m_Configuration.MRUList.Clear();
        m_Configuration.MRUList.AddRange(m_MRUList.FileList);
        CConfigManager.SaveConfiguration(m_Configuration);
        m_CoreClient?.Dispose();
    }

    /// <summary>
    /// Output log entry restored from session object (saved file).
    /// </summary>
    /// <param name="loggedMessage"></param>
    /// <param name="color"></param>
    public void LogEventFromSession(string loggedMessage, Color color)
    {
        reLog.AppendText($"{loggedMessage}", color, true, true);
    }

    /// <summary>
    /// Output event to the log.
    /// </summary>
    /// <param name="fileName">Optional. Filename to be added to the log.</param>
    /// <param name="eventType">Type of event.</param>
    /// <param name="extraInformation">Additional information to be added to the log.</param>
    public void LogEvent(string? fileName, LogEventType eventType, string extraInformation = null)
    {
        Color outputColor = Color.Blue;
        bool boldText = true;
        bool newLine = true;
        string logDateTime = $"[{DateTime.Now.ToString("HH:mm:ss.fff", System.Globalization.DateTimeFormatInfo.InvariantInfo)}] ";

        string loggedMessage;

        switch (eventType)
        {
            //
            // Intro text.
            //
            case LogEventType.StartMessage:
                loggedMessage = fileName;
                boldText = true;
                outputColor = Color.Black;
                break;

            //
            // Symbols initialization message.
            //
            case LogEventType.SymStateChange:
                loggedMessage = fileName;
                boldText = true;
                outputColor = Color.Blue;
                break;
            case LogEventType.SymInitFailed:
                loggedMessage = fileName;
                boldText = true;
                outputColor = Color.Red;
                break;

            //
            // Generic message.
            //
            case LogEventType.FileOpen:
                loggedMessage = $"File: Openning \"{fileName}\"";
                outputColor = Color.Blue;
                break;
            case LogEventType.FileOpenSession:
                loggedMessage = $"Session: Openning \"{fileName}\"";
                outputColor = Color.Blue;
                break;

            //
            // Session file messages.
            //
            case LogEventType.FileOpenSessionOK:
                loggedMessage = $"Session: \"{fileName}\" has been opened.";
                outputColor = Color.Blue;
                break;
            case LogEventType.FileOpenSessionError:
                loggedMessage = $"Error: Session file \"{fileName}\" could not be opened because \"{extraInformation}\"";
                outputColor = Color.Red;
                break;
            case LogEventType.FileSessionSave:
                loggedMessage = $"Information: File \"{fileName}\" has been saved.";
                break;
            case LogEventType.FileSessionSaveError:
                loggedMessage = $"Error: Session file \"{fileName}\" cound not be saved because \"{extraInformation}\"";
                outputColor = Color.Red;
                break;

            //
            // Stats command output.
            //
            case LogEventType.FileStats:
                loggedMessage = extraInformation;
                outputColor = Color.Purple;
                break;

            //
            // Message restored from the session object (saved file).
            //
            case LogEventType.ModuleLogFromSession:
                loggedMessage = extraInformation;
                outputColor = Color.Red;
                break;

            //
            // Server messages.
            //
            case LogEventType.CoreServerStartOK:
                loggedMessage = $"Server: {extraInformation}";
                outputColor = Color.Blue;
                break;
            case LogEventType.CoreServerStartError:
                loggedMessage = $"Error: Server initialization failed: \"{extraInformation}\"";
                outputColor = Color.Red;
                break;
            case LogEventType.CoreServerReceiveError:
                loggedMessage = $"Error: Failed to receive data from the server \"{extraInformation}\"";
                outputColor = Color.Red;
                break;
            case LogEventType.CoreServerSendError:
                loggedMessage = $"Error: Failed to send data to the server \"{extraInformation}\"";
                outputColor = Color.Red;
                break;
            case LogEventType.CoreServerDeserializeError:
                loggedMessage = $"Error: Failed to deserialize data received from the server \"{extraInformation}\"";
                outputColor = Color.Red;
                break;

            //
            // Module messages.
            //
            case LogEventType.ModuleProcessingError:
                loggedMessage = $"Error: Module \"{fileName}\" was not processed.";
                outputColor = Color.Red;
                m_Depends.ModuleAnalysisLog.Add(new LogEntry(loggedMessage, outputColor));
                break;
            case LogEventType.DelayLoadModuleNotFound:
                loggedMessage = $"Warning: Delay-load dependency module \"{fileName}\" was not found.";
                outputColor = Color.Red;
                m_Depends.ModuleAnalysisLog.Add(new LogEntry(loggedMessage, outputColor));
                break;
            case LogEventType.DelayLoadModuleNotFoundExtApiSet:
                loggedMessage = $"Note: Delay-load extension apiset module \"{fileName}\" was not found.";
                outputColor = Color.Black;
                m_Depends.ModuleAnalysisLog.Add(new LogEntry(loggedMessage, outputColor));
                break;
            case LogEventType.ModuleOpenHardError:
                loggedMessage = $"Error: \"{fileName}\" analysis failed. {extraInformation}";
                outputColor = Color.Red;
                m_Depends.ModuleAnalysisLog.Add(new LogEntry(loggedMessage, outputColor));
                break;
            case LogEventType.ModuleNotFoundExtApiSet:
                loggedMessage = $"Note: Extension apiset  module \"{fileName}\" was not found.";
                outputColor = Color.Black;
                m_Depends.ModuleAnalysisLog.Add(new LogEntry(loggedMessage, outputColor));
                break;
            case LogEventType.ModuleNotFound:
                loggedMessage = $"Error: Required implicit or forwarded dependency \"{fileName}\" was not found.";
                outputColor = Color.Red;
                m_Depends.ModuleAnalysisLog.Add(new LogEntry(loggedMessage, outputColor));
                break;
            case LogEventType.ModuleMachineMismatch:
                loggedMessage = $"Error: Module \"{fileName}\" with different CPU type was found.";
                outputColor = Color.Red;
                m_Depends.ModuleAnalysisLog.Add(new LogEntry(loggedMessage, outputColor));
                break;
            case LogEventType.ModuleExportsError:
                loggedMessage = $"Warning: Module \"{fileName}\" contain export errors.";
                outputColor = Color.Red;
                m_Depends.ModuleAnalysisLog.Add(new LogEntry(loggedMessage, outputColor));
                break;


            default:
                loggedMessage = fileName;
                outputColor = Color.Black;
                boldText = false;
                break;
        }

        reLog.AppendText($"{logDateTime}{loggedMessage}", outputColor, boldText, newLine);
    }

    private void UpdateControlsAndLogOpenEvent(bool sessionFile, string fileName)
    {
        string programTitle = CConsts.ProgramName;
        if (CUtils.IsAdministrator)
        {
            if (Environment.Is64BitProcess)
            {
                programTitle += " (Administrator, 64-bit)";
            }
            else
            {
                programTitle += " (Administrator)";
            }
        }
        else
        {
            if (Environment.Is64BitProcess)
            {
                programTitle += " (64-bit)";
            }
        }

        m_MRUList.AddFile(fileName);
        programTitle += $" [{Path.GetFileName(fileName)}]";

        if (sessionFile)
        {
            LogEvent(fileName, LogEventType.FileOpenSessionOK);
        }

        this.Text = programTitle;
    }

    private void MenuCloseItem_Click(object sender, EventArgs e)
    {
        if (m_Depends != null)
        {
            CloseInputFile();
        }
    }

    /// <summary>
    /// Disposes resources allocated for currently opened file and resets output controls.
    /// </summary>
    private void CloseInputFile()
    {
        if (m_Depends != null)
        {
            string programTitle = CConsts.ProgramName;

            if (CUtils.IsAdministrator)
            {
                programTitle += " (Administrator)";
            }

            this.Text = programTitle;

            m_Depends = null;
            m_RootNode = null;
        }

        ResetFileView();
    }

    /// <summary>
    /// Opens input file from file system.
    /// </summary>
    /// <param name="fileName"></param>
    private FileOpenResult OpenInputFileInternal(string? fileName)
    {
        bool bResult = false;

        string fileExt = Path.GetExtension(fileName);

        bool bSessionFile = fileExt.Equals(CConsts.WinDependsSessionFileExt, StringComparison.OrdinalIgnoreCase);

        if (bSessionFile)
        {
            if (m_Configuration.ClearLogOnFileOpen)
            {
                reLog.Clear();
            }
            bResult = OpenSessionFile(fileName);
        }
        else
        {
            //
            // Display file open settings dialog depending on global settings.
            //
            CFileOpenSettings fileOpenSettings = new(m_Configuration);

            if (!fileOpenSettings.AnalysisSettingsUseAsDefault)
            {
                using (FileOpenForm fileOpenForm = new(m_Configuration.EscKeyEnabled, fileOpenSettings, fileName))
                {
                    //
                    // Update global settings if use as default is checked.
                    //
                    if (fileOpenForm.ShowDialog() == DialogResult.OK)
                    {
                        if (fileOpenSettings.AnalysisSettingsUseAsDefault)
                        {
                            m_Configuration.UseStats = fileOpenSettings.UseStats;
                            m_Configuration.UseRelocForImages = fileOpenSettings.UseRelocForImages;
                            m_Configuration.MinAppAddress = fileOpenSettings.MinAppAddress;
                            m_Configuration.PropagateSettingsOnDependencies = fileOpenSettings.PropagateSettingsOnDependencies;
                            m_Configuration.AnalysisSettingsUseAsDefault = true;
                        }
                    }
                    else
                    {
                        //
                        // User cancelled file opening, return.
                        //
                        return FileOpenResult.Cancelled;
                    }
                }
            }

            CloseInputFile();

            LogEvent(fileName, LogEventType.FileOpen);

            if (m_Configuration.ClearLogOnFileOpen)
            {
                reLog.Clear();
            }

            m_ParentImportsHashTable.Clear();

            // Create new root object and output it submodules.
            m_Depends = new(fileName)
            {
                // Remember node max depth value.
                SessionNodeMaxDepth = m_Configuration.ModuleNodeDepthMax
            };

            if (m_Depends.RootModule != null)
            {
                CPathResolver.Initialized = false;
                using (CActCtxHelper sxsHelper = new(fileName))
                {
                    CPathResolver.ActCtxHelper = sxsHelper;

                    PopulateObjectToLists(m_Depends.RootModule, false, fileOpenSettings);

                    LVModules.BeginUpdate();

                    LVModulesSort(LVModules, m_Configuration.SortColumnModules,
                       LVModulesSortOrder, m_LoadedModulesList, DisplayCacheType.Modules);

                    LVModules.EndUpdate();

                    bResult = true;
                }//CActCtxHelper
            }

        }

        if (bResult)
        {
            UpdateControlsAndLogOpenEvent(bSessionFile, fileName);

            if (m_Configuration.AutoExpands)
            {
                TVModules.ExpandAll();
            }
        }

        SaveToolButton.Enabled = bResult;
        CopyToolButton.Enabled = bResult;

        if (TVModules.Nodes.Count > 0)
        {
            TVModules.SelectedNode = TVModules.Nodes[0];
        }

        return bResult ? FileOpenResult.Success : FileOpenResult.Failure;
    }

    /// <summary>
    /// Opens input file from file system.
    /// </summary>
    /// <param name="fileName"></param>
    private bool OpenInputFile(string? fileName)
    {
        bool bResult = false;

        try
        {
            mainMenu.Enabled = false;
            MainToolBar.Enabled = false;
            TVModules.Enabled = false;
            LVModules.Enabled = false;

            //
            // Check shortcut.
            //
            var resolvedFileName = fileName;
            var fileExtension = Path.GetExtension(resolvedFileName);
            if (!string.IsNullOrEmpty(fileExtension) && fileExtension.Equals(CConsts.ShortcutFileExt, StringComparison.OrdinalIgnoreCase))
            {
                resolvedFileName = NativeMethods.ResolveShortcutTarget(fileName);
            }

            var fileOpenResult = OpenInputFileInternal(resolvedFileName);
            bResult = fileOpenResult == FileOpenResult.Success;
            string logEvent = fileOpenResult switch
            {
                FileOpenResult.Success => $"Populating \"{resolvedFileName}\" has been completed",
                FileOpenResult.Failure => $"There is an error while populating \"{resolvedFileName}\"",
                _ => "Operation has been cancelled"
            };

            UpdateOperationStatus(logEvent);
        }
        catch
        {
            UpdateOperationStatus($"There is an exception error while populating \"{fileName}\"");
        }
        finally
        {
            mainMenu.Enabled = true;
            MainToolBar.Enabled = true;
            TVModules.Enabled = true;
            LVModules.Enabled = true;
            TVModules.Focus();
        }

        return bResult;
    }

    /// <summary>
    /// Opens WinDepends serialized session file.
    /// </summary>
    /// <param name="fileName"></param>
    private bool OpenSessionFile(string? fileName)
    {
        CloseInputFile();

        m_Depends = LoadSessionObjectFromFile(fileName, m_Configuration.CompressSessionFiles);
        if (m_Depends == null)
        {
            // Deserialization failed, leaving.
            return false;
        }

        LogEvent(fileName, LogEventType.FileOpenSession);

        if (m_Depends.RootModule != null)
        {
            PopulateObjectToLists(m_Depends.RootModule, true, null);

            // Restore important module related warnings/errors in the log.
            foreach (var entry in m_Depends.ModuleAnalysisLog)
            {
                LogEventFromSession(entry.LoggedMessage, entry.EntryColor);
            }
        }
        else
        {
            // Corrupted file, leaving.
            m_Depends = null;
            m_RootNode = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds hierarchical tree diagram of module dependencies.
    /// First, it adds the given module as the root and populates its dependencies.
    /// Next, it takes each dependent module and populates its dependencies recursively respecting the maximum depth level from settings.
    /// </summary>
    /// <param name="module">Module to populate dependencies.</param>
    /// <param name="loadFromObject">If set then source is session object (restored from session file).</param>
    /// <param name="fileOpenSettings">Specific file open settings from the program configuration.</param>
    private void PopulateObjectToLists(CModule module, bool loadFromObject, CFileOpenSettings fileOpenSettings)
    {
        List<TreeNode> baseNodes = [];

        UpdateOperationStatus($"Populating {module.FileName}");

        if (loadFromObject)
        {
            // Add root session module.
            m_RootNode = AddSessionModuleEntry(module, null);

            // Add root session module dependencies.
            foreach (var importModule in module.Dependents)
            {
                UpdateOperationStatus($"Populating {importModule.FileName}");
                baseNodes.Add(AddSessionModuleEntry(importModule, m_RootNode));
            }
        }
        else
        {
            // Add root module.
            m_RootNode = AddModuleEntry(module, fileOpenSettings, null);

            // Add root module dependencies.
            foreach (var importModule in module.Dependents)
            {
                UpdateOperationStatus($"Populating {importModule.FileName}");
                baseNodes.Add(AddModuleEntry(importModule, fileOpenSettings, m_RootNode));
            }
        }

        // Add sub dependencies.
        foreach (var node in baseNodes)
        {
            CModule nodeModule = node.Tag as CModule;

            foreach (var dependent in nodeModule.Dependents)
            {
                UpdateOperationStatus($"Populating {dependent.FileName}");
                PopulateDependentObjectsToLists(dependent, node, loadFromObject, fileOpenSettings);
            }
        }

    }

    /// <summary>
    /// Populates main module dependencies recursively respecting the maximum depth level from settings.
    /// </summary>
    /// <param name="module">Module to populate dependencies.</param>
    /// <param name="parentNode">A previous treeview node.</param>
    /// <param name="loadFromObject">If set then source is session object (restored from session file).</param>
    /// <param name="fileOpenSettings">Specific file open settings from the program configuration.</param>
    private void PopulateDependentObjectsToLists(CModule module, TreeNode parentNode, bool loadFromObject, CFileOpenSettings fileOpenSettings)
    {
        TreeNode tvNode;

        if (loadFromObject)
        {
            tvNode = AddSessionModuleEntry(module, parentNode);
        }
        else
        {
            tvNode = AddModuleEntry(module, fileOpenSettings, parentNode);
        }

        foreach (CModule dependentModule in module.Dependents)
        {
            UpdateOperationStatus($"Populating {dependentModule.FileName}");
            PopulateDependentObjectsToLists(dependentModule, tvNode, loadFromObject, fileOpenSettings);
        }
    }

    private void OpenFileHandler(object sender, EventArgs e)
    {
        OpenDialog1.Filter = CConsts.HandledFileExtensionsMsg +
            string.Join(";", InternalFileHandledExtensions.ExtensionList.Select(ext => "*." + ext.Name)) + CConsts.WinDependsFilter;

        if (OpenDialog1.ShowDialog() == DialogResult.OK)
        {
            OpenInputFile(OpenDialog1.FileName);
        }
    }

    private void MenuAbout_Click(object sender, EventArgs e)
    {
        using (AboutForm aboutForm = new(m_Configuration.EscKeyEnabled))
        {
            aboutForm.ShowDialog();
        }
    }

    private void ApplySymbolsConfiguration()
    {
        if (m_Configuration.UseSymbols)
        {
            var symStorePath = m_Configuration.SymbolsStorePath;
            var symDllPath = m_Configuration.SymbolsDllPath;

            //
            // Set defaults in case if nothing selected.
            //
            if (string.IsNullOrEmpty(symDllPath))
            {
                symDllPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CConsts.DbgHelpDll);
                m_Configuration.SymbolsDllPath = symDllPath;
            }
            if (string.IsNullOrEmpty(symStorePath))
            {
                symStorePath = $"srv*{Path.Combine(Path.GetTempPath(), CConsts.SymbolsDefaultStoreDirectory)}{CConsts.SymbolsDownloadLink}";
                m_Configuration.SymbolsStorePath = symStorePath;
            }

            CSymbolResolver.ReleaseSymbolResolver();
            if (CSymbolResolver.AllocateSymbolResolver(symDllPath, symStorePath))
            {
                LogEvent($"Debug symbols initialized using \"{symDllPath}\", " +
                    $"store \"{symStorePath}\"", LogEventType.SymStateChange);
            }
            else
            {
                LogEvent($"Debug symbols initialization failed for \"{symDllPath}\", " +
                    $"store \"{symStorePath}\"", LogEventType.SymInitFailed);
            }
        }
        else
        {
            if (CSymbolResolver.ReleaseSymbolResolver())
            {
                LogEvent($"Debug symbols deallocated", LogEventType.SymStateChange);
            }
        }
    }

    /// <summary>
    /// Displays configuration form as modal dialog with selected settings page index.
    /// When user finishes work with configuration, depending on user choice apply 
    /// the changed settings or do nothing.
    /// </summary>
    /// <param name="pageIndex">An index of page to be selected.</param>
    private void ShowConfigurationForm(int pageIndex)
    {
        bool is64bitFile = true;
        string currentFileName = string.Empty;
        CConfiguration optConfig = new(m_Configuration);
        if (m_Depends != null)
        {
            is64bitFile = m_Depends.RootModule.Is64bitArchitecture();
            currentFileName = Path.GetDirectoryName(m_Depends.RootModule.FileName);
        }
        else
        {
            if (!string.IsNullOrEmpty(Application.StartupPath))
            {
                currentFileName = Path.TrimEndingDirectorySeparator(Application.StartupPath);
            }
        }

        var bAutoExpandPrev = m_Configuration.AutoExpands;
        var bFullPathPrev = m_Configuration.FullPaths;
        var bUndecoratedPrev = m_Configuration.ViewUndecorated;
        var bResolveAPISetsPrev = m_Configuration.ResolveAPIsets;
        var bHighlightAPISetsPrev = m_Configuration.HighlightApiSet;
        var bUseApiSetSchemaFilePrev = m_Configuration.UseApiSetSchemaFile;
        var bUseSymbolsPrev = m_Configuration.UseSymbols;

        using (ConfigurationForm configForm = new(currentFileName,
                                                  is64bitFile,
                                                  optConfig,
                                                  m_CoreClient,
                                                  LogEvent,
                                                  pageIndex))
        {
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                m_Configuration = optConfig;

                //
                // Re-display MRU list.
                //
                m_MRUList.UpdateFileView(m_Configuration.HistoryDepth, m_Configuration.HistoryShowFullPath);
                m_MRUList.ShowFiles();

                //
                // Check toolbar buttons depending on settings.
                //
                AutoExpandToolButton.Checked = m_Configuration.AutoExpands;
                ViewFullPathsToolButton.Checked = m_Configuration.FullPaths;
                ViewUndecoratedToolButton.Checked = m_Configuration.ViewUndecorated;
                ResolveAPISetsToolButton.Checked = m_Configuration.ResolveAPIsets;

                //
                // Update settings only if there are changed settings.
                //
                if (m_Configuration.AutoExpands != bAutoExpandPrev ||
                    m_Configuration.FullPaths != bFullPathPrev ||
                    m_Configuration.ResolveAPIsets != bResolveAPISetsPrev ||
                    m_Configuration.HighlightApiSet != bHighlightAPISetsPrev)
                {
                    UpdateFileView(FileViewUpdateAction.TreeViewAutoExpandsChange);
                    UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
                }

                if (m_Configuration.ViewUndecorated != bUndecoratedPrev)
                {
                    UpdateFileView(FileViewUpdateAction.FunctionsUndecorateChange);
                }

                if (m_Configuration.UseApiSetSchemaFile != bUseApiSetSchemaFilePrev)
                {
                    m_CoreClient?.SetApiSetSchemaNamespaceUse(m_Configuration.ApiSetSchemaFile);
                }

                if (m_Configuration.UseSymbols != bUseSymbolsPrev)
                {
                    ApplySymbolsConfiguration();
                }

            }
        }
    }

    private void ConfigureMenuItem_Click(object sender, EventArgs e)
    {
        ShowConfigurationForm(CConsts.IdxTabMain);
    }

    /// <summary>
    /// Imports/Export list menu handler.
    /// </summary>
    void ProcessFunctionEntry()
    {
        int selectedItemIndex;
        CFunction function = null;
        string fName;

        try
        {
            if (LVImports.Focused && LVImports.SelectedIndices.Count > 0)
            {
                selectedItemIndex = LVImports.SelectedIndices[0];
                if (selectedItemIndex < m_CurrentImportsList.Count)
                {
                    function = m_CurrentImportsList[selectedItemIndex];
                }
            }
            else if (LVExports.Focused && LVExports.SelectedIndices.Count > 0)
            {
                selectedItemIndex = LVExports.SelectedIndices[0];
                if (selectedItemIndex < m_CurrentExportsList.Count)
                {
                    function = m_CurrentExportsList[selectedItemIndex];
                }
            }

            if (function != null)
            {
                fName = function.RawName;
                Process.Start(new ProcessStartInfo()
                {
                    FileName = new StringBuilder(m_Configuration.ExternalFunctionHelpURL).Replace("%1", fName).ToString(),
                    Verb = "open",
                    UseShellExecute = true
                });
            }

        }
        catch { }
    }

    /// <summary>
    /// Module tree, module list menu handler.
    /// </summary>
    /// <param name="action">One of the ProcessModuleAction values.</param>
    private void ProcessModuleEntry(ProcessModuleAction action)
    {
        CModule module;

        if (LVModules.Focused && LVModules.SelectedIndices.Count > 0)
        {
            var selectedItemIndex = LVModules.SelectedIndices[0];
            module = m_LoadedModulesList[selectedItemIndex];
        }
        else
        {
            module = TVModules.SelectedNode?.Tag as CModule;
        }

        if (module == null || string.IsNullOrEmpty(module.FileName)) return;

        switch (action)
        {
            case ProcessModuleAction.ShowProperties:
                NativeMethods.ShowFileProperties(module.FileName);
                break;

            case ProcessModuleAction.ExternalViewer:
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        Arguments = new StringBuilder(m_Configuration.ExternalViewerArguments).Replace("%1", module.FileName).ToString(),
                        FileName = m_Configuration.ExternalViewerCommand,
                        WindowStyle = ProcessWindowStyle.Normal
                    });

                }
                catch { }
                break;

            case ProcessModuleAction.OpenFileLocation:
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), CConsts.ExplorerApp),
                        Arguments = $" /select, \"{module.FileName}\"",
                        Verb = "open",
                        UseShellExecute = true
                    });

                }
                catch { }
                break;
        }
    }

    /// <summary>
    /// MainForm KeyDown handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && m_Configuration.EscKeyEnabled)
        {
            this.Close();
        }

        if (e.KeyCode == Keys.Enter && (ModifierKeys == Keys.None || ModifierKeys == Keys.Alt))
        {
            var controlName = FindFocusedListControlName();
            if (controlName != null)
            {
                switch (controlName)
                {
                    case CConsts.TVModulesName:
                    case CConsts.LVModulesName:
                        ProcessModuleEntry((ModifierKeys == Keys.Alt) ?
                            ProcessModuleAction.ShowProperties : ProcessModuleAction.ExternalViewer);
                        break;
                    case CConsts.LVImportsName:
                    case CConsts.LVExportsName:
                        if (ModifierKeys == Keys.None)
                        {
                            ProcessFunctionEntry();
                        }
                        break;
                }
            }

        }
    }

    /// <summary>
    /// Windows Forms focus glitch workaround.
    /// Split container MouseUp event handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SplitContainer_MouseUp(object sender, MouseEventArgs e)
    {
        //
        // Windows Forms focus glitch workaround.
        //
        if (focusedControl != null)
        {
            focusedControl.Focus();
            focusedControl = null;
        }
    }

    /// <summary>
    /// Windows Forms focus glitch workaround.
    /// Split container MouseDown event handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SplitContainer_MouseDown(object sender, MouseEventArgs e)
    {
        focusedControl = CUtils.IsControlFocused(this.Controls);
    }

    /// <summary>
    /// ToolStrip Buttons Click handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ToolButtonClick(object sender, EventArgs e)
    {
        ToolStripButton toolStripButton = sender as ToolStripButton;

        switch (Convert.ToInt32(toolStripButton.Tag))
        {
            case CConsts.TagFullPaths:
                m_Configuration.FullPaths = toolStripButton.Checked;
                UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
                break;

            case CConsts.TagAutoExpand:
                m_Configuration.AutoExpands = toolStripButton.Checked;
                UpdateFileView(FileViewUpdateAction.TreeViewAutoExpandsChange);
                break;

            case CConsts.TagViewUndecorated:
                m_Configuration.ViewUndecorated = toolStripButton.Checked;
                UpdateFileView(FileViewUpdateAction.FunctionsUndecorateChange);
                break;

            case CConsts.TagResolveAPIsets:
                m_Configuration.ResolveAPIsets = toolStripButton.Checked;
                UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
                break;

            case CConsts.TagViewExternalViewer:
                ProcessModuleEntry(ProcessModuleAction.ExternalViewer);
                break;

            case CConsts.TagViewProperties:
                ProcessModuleEntry(ProcessModuleAction.ShowProperties);
                break;

            case CConsts.TagSystemInformation:
                ShowSysInfoDialog(sender, e);
                break;

            case CConsts.TagConfiguration:
                ConfigureMenuItem_Click(sender, e);
                break;

        }
    }

    /// <summary>
    /// MainMenu -> View -> items click handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SettingsMenuItem_Click(object sender, EventArgs e)
    {
        ToolStripMenuItem menuItem = sender as ToolStripMenuItem;

        switch (Convert.ToInt32(menuItem.Tag))
        {
            case CConsts.TagFullPaths:
                m_Configuration.FullPaths = menuItem.Checked;
                ViewFullPathsToolButton.Checked = menuItem.Checked;
                UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
                break;

            case CConsts.TagAutoExpand:
                m_Configuration.AutoExpands = menuItem.Checked;
                AutoExpandToolButton.Checked = menuItem.Checked;
                UpdateFileView(FileViewUpdateAction.TreeViewAutoExpandsChange);
                break;

            case CConsts.TagViewUndecorated:
                m_Configuration.ViewUndecorated = menuItem.Checked;
                ViewUndecoratedToolButton.Checked = menuItem.Checked;
                UpdateFileView(FileViewUpdateAction.FunctionsUndecorateChange);
                break;

            case CConsts.TagResolveAPIsets:
                m_Configuration.ResolveAPIsets = menuItem.Checked;
                ResolveAPISetsToolButton.Checked = menuItem.Checked;
                UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
                break;
        }

    }

    private void TVModules_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            TVModules.SelectedNode = TVModules.GetNodeAt(e.X, e.Y);
            if (TVModules.SelectedNode != null)
            {
                moduleTreePopupMenu.Show(Cursor.Position);
            }
        }
    }

    private void ModuleTreeContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        moduleTreeAutoExpandMenuItem.Checked = m_Configuration.AutoExpands;
        moduleTreeFullPathsMenuItem.Checked = m_Configuration.FullPaths;
        ViewModuleSetMenuItems(true);
    }

    private void RichEditLog_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            richEditPopupMenu.Show(Cursor.Position);
        }
    }
    private void RichEditSelectAllMenuItem_Click(object sender, EventArgs e)
    {
        reLog.SelectAll();
    }

    private void RichEditCopyTextMenuItem_Click(object sender, EventArgs e)
    {
        reLog.Copy();
    }

    private void RichEditPopupMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        richEditCopyTextMenuItem.Enabled = (!string.IsNullOrEmpty(reLog.SelectedText));
    }

    private void ModuleTreeAutoExpandMenuItem_Click(object sender, EventArgs e)
    {
        m_Configuration.AutoExpands = !m_Configuration.AutoExpands;
        AutoExpandToolButton.Checked = m_Configuration.AutoExpands;
    }

    private void ViewModuleSetMenuItems(bool bContextMenu)
    {
        var controlName = FindFocusedListControlName();
        if (controlName == null)
            return;

        string baseText = "Highlight Matching", text = "";
        bool bMatchingItemEnabled = true;
        bool bLoookupFunctionEnabled = false;
        bool bOriginalInstanceEnabled = false;
        bool bPrevInstanceEnabled = false;
        bool bNextInstanceEnabled = false;

        if (TVModules.SelectedNode != null)
        {
            CModule obj = (CModule)TVModules.SelectedNode.Tag;
            if (obj != null)
            {
                m_InstanceStopSearch = false;
                bPrevInstanceEnabled = (null != TreeViewFindNodeInstancePrev(m_RootNode, TVModules.SelectedNode, obj.FileName));

                m_InstanceSelfFound = false;
                m_InstanceStopSearch = false;
                bNextInstanceEnabled = (null != TreeViewFindNodeInstanceNext(m_RootNode, TVModules.SelectedNode, obj.FileName));
            }
        }

        switch (controlName)
        {
            case CConsts.TVModulesName:
                bMatchingItemEnabled = (TVModules.Nodes.Count > 0);
                text = "Module In List";
                bOriginalInstanceEnabled = (null != (CUtils.TreeViewGetOriginalInstanceFromNode(TVModules.SelectedNode, m_LoadedModulesList)));
                break;

            case CConsts.LVModulesName:
                bMatchingItemEnabled = (LVModules.Items.Count > 0);
                text = "Module In Tree";
                bOriginalInstanceEnabled = (null != (CUtils.TreeViewGetOriginalInstanceFromNode(TVModules.SelectedNode, m_LoadedModulesList)));
                break;

            case CConsts.LVImportsName:
                bMatchingItemEnabled = (LVExports.Items.Count > 0);
                bLoookupFunctionEnabled = (LVImports.Items.Count > 0);
                text = "Export Function";
                break;

            case CConsts.LVExportsName:
                bMatchingItemEnabled = (LVImports.Items.Count > 0);
                bLoookupFunctionEnabled = (LVExports.Items.Count > 0);
                text = "Import Function";
                break;
        }

        if (bContextMenu)
        {
            //
            // Context menu items.
            //

            //
            // Highlight Matching (Module/Function)
            //
            ContextViewHighlightMatchingItem.Enabled = bMatchingItemEnabled;
            ContextViewHighlightMatchingItem.Text = $"{baseText} {text}";

            //
            // Original Instance
            //
            ContextOriginalInstanceItem.Enabled = bOriginalInstanceEnabled;

            //
            // Previous Instance
            //
            ContextPreviousInstanceItem.Enabled = bPrevInstanceEnabled;

            //
            // Next Instance
            //
            ContextNextInstanceItem.Enabled = bNextInstanceEnabled;

            //
            // Open Module Location
            //
            ContextOpenModuleLocationItem.Enabled = bMatchingItemEnabled;

            //
            // View External (same as Highlight Matching)
            //
            ContextViewModuleInExternalViewerItem.Enabled = bMatchingItemEnabled;

            //
            // Propeties
            //
            ContextViewHighlightMatchingItem.Enabled = bMatchingItemEnabled;
        }
        else
        {
            //
            // View dropdown menu.
            //

            //
            // Highlight Matching (Module)
            //
            ViewHighlightMatchingItem.Enabled = bMatchingItemEnabled;
            ViewHighlightMatchingItem.Text = $"{baseText} {text}";

            //
            // Original Instance
            //
            ViewOriginalInstanceItem.Enabled = bOriginalInstanceEnabled;

            //
            // Previous Instance
            //
            ViewPreviousInstanceItem.Enabled = bPrevInstanceEnabled;

            //
            // Next Instance
            //
            ViewNextInstanceItem.Enabled = bNextInstanceEnabled;

            //
            // Open Module Location
            //
            ViewOpenModuleLocationItem.Enabled = bMatchingItemEnabled;

            //
            // View External (same as Highlight Matching)
            //
            ViewModuleExternalViewerItem.Enabled = bMatchingItemEnabled;

            //
            // Lookup Function.
            //
            ViewLookupFunctionInExternalHelpItem.Enabled = bLoookupFunctionEnabled;


            //
            // Propeties
            //
            ViewPropertiesItem.Enabled = bMatchingItemEnabled;
        }

    }

    private void ViewToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
    {
        //
        // Set check state for menu items in MainMenu->View tool strip.
        //
        mainMenuAutoExpandItem.Checked = m_Configuration.AutoExpands;
        mainMenuFullPathsItem.Checked = m_Configuration.FullPaths;
        mainMenuUndecorateCFunctionsItem.Checked = m_Configuration.ViewUndecorated;
        mainMenuResolveApiSetsItem.Checked = m_Configuration.ResolveAPIsets;
        mainMenuShowToolBarItem.Checked = m_Configuration.ShowToolBar;
        manuMenuShowStatusBarItem.Checked = m_Configuration.ShowStatusBar;

        //
        // Enable/disable view instance items depending on active control.
        //
        ViewModuleSetMenuItems(false);

        var sessionOpened = m_Depends != null;

        //
        // Enable/Disable refresh button.
        //
        ViewRefreshItem.Enabled = sessionOpened;

        // Enable/Disable tree view specific commands.
        mainMenuExpandAllItem.Enabled = sessionOpened;
        mainMenuCollapseAllItem.Enabled = sessionOpened;
    }

    private void LVModules_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ListViewItem item = LVModules.GetItemAt(e.X, e.Y);
            if (item != null)
            {
                moduleViewPopupMenu.Show(Cursor.Position);
            }
        }
    }

    /// <summary>
    /// Called when user spawns popup menu on LVImports/LVExports/LVModules virtual listviews.
    /// </summary>
    /// <param name="functionView">If true then selection is LVImports/LVExports virtual listview, LVModules otherwise.</param>
    void SetPopupMenuItemText(bool functionView = true)
    {
        ToolStripMenuItem menuItem;
        int selectedCount;
        string menuText;
        ListView lvSrc, lvDst;
        List<CFunction> funcSrc;

        if (functionView)
        {
            menuText = "&Copy Function Name";
            menuItem = CopyFunctionNameMenuItem;

            if (LVImports.Focused)
            {
                selectedCount = LVImports.SelectedIndices.Count;

                lvSrc = LVImports;
                lvDst = LVExports;
                funcSrc = m_CurrentImportsList;
            }
            else
            {
                selectedCount = LVExports.SelectedIndices.Count;

                lvSrc = LVExports;
                lvDst = LVImports;
                funcSrc = m_CurrentExportsList;
            }

            ListViewItem lvResult = FindMatchingFunctionInList(lvSrc, lvDst, funcSrc);
            MatchingFunctionPopupMenuItem.Enabled = lvResult != null;
        }
        else
        {
            menuText = "&Copy File Name";
            menuItem = moduleViewCopyFileNameMenuItem;
            selectedCount = LVModules.SelectedIndices.Count;
        }

        if (selectedCount > 1)
        {
            menuText += "s";
        }

        menuItem.Text = menuText;
        MenuCopyItem.Enabled = (selectedCount > 0);
        MenuCopyItem.Text = menuText;
    }

    private void ModuleViewPopupMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        toolStripMenuItem11.Checked = m_Configuration.FullPaths;
        SetPopupMenuItemText(false);
    }

    /// <summary>
    /// LVImport/LVExport list view MouseDown handler.
    /// Depending on currently active list modify popup menu and show it to the user.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVFunctions_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ListView listView = sender as ListView;
            ListViewItem item = listView.GetItemAt(e.X, e.Y);
            if (item != null)
            {
                string viewType;
                if (listView == LVImports)
                {
                    viewType = "Export";
                    LVImports.SelectedIndices.Clear();
                    LVImports.SelectedIndices.Add(item.Index);
                }
                else
                {
                    LVExports.SelectedIndices.Clear();
                    LVExports.SelectedIndices.Add(item.Index);
                    viewType = "Import";
                }

                MatchingFunctionPopupMenuItem.Text = "Highlight Matching " + viewType + " Function";
                functionPopupMenu.Show(Cursor.Position);
            }
        }
    }

    private void FunctionPopupMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        undecorateFunctionToolStripMenuItem.Checked = m_Configuration.ViewUndecorated;
        resolveAPIsetsToolStripMenuItem.Checked = m_Configuration.ResolveAPIsets;
        SetPopupMenuItemText(true);
    }

    private void UndecorateFunctionToolStripMenuItem_Click(object sender, EventArgs e)
    {
        m_Configuration.ViewUndecorated = !m_Configuration.ViewUndecorated;
        ViewUndecoratedToolButton.Checked = m_Configuration.ViewUndecorated;
        UpdateFileView(FileViewUpdateAction.FunctionsUndecorateChange);
    }

    private void ViewModuleInExternalViewer_Click(object sender, EventArgs e)
    {
        ProcessModuleEntry(ProcessModuleAction.ExternalViewer);
    }

    private void PropertiesMenuItem_Click(object sender, EventArgs e)
    {
        ProcessModuleEntry(ProcessModuleAction.ShowProperties);
    }

    private void ModuleFullPathsMenuItem_Click(object sender, EventArgs e)
    {
        m_Configuration.FullPaths = !m_Configuration.FullPaths;
        ViewFullPathsToolButton.Checked = m_Configuration.FullPaths;
        UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
    }

    /// <summary>
    /// Menu/Toolbar items mouse enter handler for displaying tooltips in the status bar area.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainMenu_MouseEnter(object sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem)
        {
            string text = menuItem.Text.Replace("&", string.Empty);
            text = Properties.Resources.ResourceManager.GetString(text);
            if (!string.IsNullOrEmpty(text))
            {
                toolBarStatusLabel.Text = text;
            }
        }
        else if (sender is ToolStripButton toolStripButton)
        {
            string text = Properties.Resources.ResourceManager.GetString(toolStripButton.Text);
            if (!string.IsNullOrEmpty(text))
            {
                toolBarStatusLabel.Text = text;
            }
        }
    }

    /// <summary>
    /// Menu mouse leave handler for resetting tooltip display in the status bar area.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainMenu_MouseLeave(object sender, EventArgs e)
    {
        toolBarStatusLabel.Text = string.Empty;
    }

    private void SelectAll_Click(object sender, EventArgs e)
    {
        if (reLog.Focused)
        {
            reLog.SelectAll();
            return;
        }

        ListView listView;

        if (LVExports.Focused)
        {
            listView = LVExports;
        }
        else if (LVImports.Focused)
        {
            listView = LVImports;
        }
        else if (LVModules.Focused)
        {
            listView = LVModules;
        }
        else
        {
            return;
        }

        listView.BeginUpdate();

        for (int i = 0; i < listView.VirtualListSize; i++)
        {
            listView.SelectedIndices.Add(i);
        }

        listView.EndUpdate();
    }

    private void ExternalHelpMenuItem_Click(object sender, EventArgs e)
    {
        ProcessFunctionEntry();
    }

    private void MenuClearLogItem_Click(object sender, EventArgs e)
    {
        reLog.Clear();
    }

    private void MenuExitItem_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void CopyFunctionNamesToClipboard(ListView listView, List<CFunction> functions)
    {
        var textBuilder = new StringBuilder();
        bool viewUndecorated = m_Configuration.ViewUndecorated;

        foreach (int itemIndex in listView.SelectedIndices)
        {
            CFunction currentFunction = functions[itemIndex];

            string functionName = viewUndecorated && !string.IsNullOrEmpty(currentFunction.UndecoratedName)
                ? currentFunction.UndecoratedName
                : currentFunction.RawName;

            textBuilder.AppendLine(functionName);
        }

        CUtils.SetClipboardData(textBuilder.ToString());
    }

    private void CopyModuleNamesToClipboard(ListView listView, List<CModule> moduleList)
    {
        var textBuilder = new StringBuilder();

        foreach (int itemIndex in listView.SelectedIndices)
        {
            CModule module = moduleList[itemIndex];
            textBuilder.AppendLine(module.GetModuleNameRespectApiSet(m_Configuration.ResolveAPIsets));
        }

        CUtils.SetClipboardData(textBuilder.ToString());
    }

    /// <summary>
    /// Copy menu element handler shared between all popup menus.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MenuCopyItem_Click(object sender, EventArgs e)
    {
        //
        // RichEditLog Copy.
        //
        if (reLog.Focused)
        {
            reLog.Copy();
            return;
        }

        var controlName = FindFocusedListControlName();
        if (controlName == null)
        {
            return;
        }

        switch (controlName)
        {
            case CConsts.LVImportsName:
                CopyFunctionNamesToClipboard(LVImports, m_CurrentImportsList);
                break;

            case CConsts.LVExportsName:
                CopyFunctionNamesToClipboard(LVExports, m_CurrentExportsList);
                break;

            case CConsts.LVModulesName:
                CopyModuleNamesToClipboard(LVModules, m_LoadedModulesList);
                break;

            case CConsts.TVModulesName:
                var selectedNode = TVModules.SelectedNode;
                if (selectedNode != null && selectedNode.Tag is CModule module)
                {
                    var moduleName = module.GetModuleNameRespectApiSet(m_Configuration.ResolveAPIsets);

                    if (!string.IsNullOrEmpty(moduleName))
                    {
                        Clipboard.Clear();
                        Clipboard.SetText(moduleName);
                    }
                }
                break;
        }
    }

    static int reLogOldSelectStart, reLogOldSelectEnd;
    static Color reLogOldColor, reLogOldBkColor;

    public int FindMyText(string txtToSearch, int searchStart, int searchEnd)
    {
        if (reLogOldSelectStart > 0 && reLogOldSelectEnd > 0)
        {
            reLog.SelectionBackColor = reLogOldBkColor;
            reLog.SelectionColor = reLogOldColor;
            reLog.Select(reLogOldSelectStart, reLogOldSelectEnd);
        }

        int retVal = -1;

        if (searchStart >= 0 && LogIndexOfSearchText >= 0)
        {
            if (searchEnd > searchStart || searchEnd == -1)
            {
                LogIndexOfSearchText = reLog.Find(txtToSearch, searchStart, searchEnd, RichTextBoxFinds.None);
                if (LogIndexOfSearchText != -1)
                {
                    retVal = LogIndexOfSearchText;
                }
            }
        }
        return retVal;
    }

    public void LogFindString()
    {
        if (string.IsNullOrEmpty(LogFindText))
        {
            return;
        }

        int startindex;
        LogIndexOfSearchText = 0;
        startindex = FindMyText(LogFindText.Trim(), LogSearchPosition, reLog.Text.Length);

        if (startindex >= 0)
        {
            int endindex = LogFindText.Length;

            reLogOldColor = reLog.SelectionColor;
            reLogOldBkColor = reLog.SelectionBackColor;
            reLogOldSelectStart = startindex;
            reLogOldSelectEnd = endindex;

            reLog.SelectionBackColor = Color.Black;
            reLog.SelectionColor = Color.LightGreen;
            reLog.Select(startindex, endindex);
            reLog.ScrollToCaret();
            LogSearchPosition = startindex + endindex;
        }
        else
        {
            LogSearchPosition = 0;
        }

    }

    private void RichEditLog_Click(object sender, EventArgs e)
    {
        int selStart = reLog.SelectionStart;
        int selLength = reLog.SelectionLength;

        if (reLogOldSelectStart > 0 && reLogOldSelectEnd > 0)
        {
            reLog.Select(reLogOldSelectStart, reLogOldSelectEnd);
            reLog.SelectionBackColor = reLogOldBkColor;
            reLog.SelectionColor = reLogOldColor;
            reLog.SelectionStart = selStart;
            reLog.SelectionLength = selLength;
        }

    }

    private void FindMenuItem_Click(object sender, EventArgs e)
    {
        LogFindText = string.Empty;
        LogSearchPosition = 0;
        using (FindDialogForm FindDialog = new(this, m_Configuration.EscKeyEnabled))
        {
            FindDialog.ShowDialog();
        }
    }

    private void FindNextMenuItemClick(object sender, EventArgs e)
    {
        LogFindString();
    }

    private void EditMenuItem_DropDownOpening(object sender, EventArgs e)
    {
        if (reLog.Focused)
        {
            MenuCopyItem.Text = "&Copy Text";
            MenuCopyItem.Enabled = (reLog.SelectionLength > 0);

            MenuSelectAllItem.Enabled = true;
            MenuFindtem.Enabled = true;
            MenuFindNextItem.Enabled = true;
            return;
        }

        if (TVModules.Focused)
        {
            SetPopupMenuItemText(false);
            MenuCopyItem.Enabled = (TVModules.SelectedNode != null);
            MenuSelectAllItem.Enabled = false;
            MenuFindtem.Enabled = false;
            MenuFindNextItem.Enabled = false;
            return;
        }

        if (LVModules.Focused)
        {
            SetPopupMenuItemText(false);
            MenuSelectAllItem.Enabled = true;
            MenuFindtem.Enabled = false;
            MenuFindNextItem.Enabled = false;
            return;
        }

        if (LVImports.Focused || LVExports.Focused)
        {
            SetPopupMenuItemText(true);
            MenuSelectAllItem.Enabled = true;
            MenuFindtem.Enabled = false;
            MenuFindNextItem.Enabled = false;
            return;
        }

    }

    private void ResolveAPIsetsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        m_Configuration.ResolveAPIsets = !m_Configuration.ResolveAPIsets;
        ResolveAPISetsToolButton.Checked = m_Configuration.ResolveAPIsets;
        UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
    }

    private ListViewItem FindMatchingFunctionInList(ListView SourceList, ListView DestinationList, List<CFunction> FunctionsList)
    {
        if (SourceList.SelectedIndices.Count > 0)
        {
            int selectedIndex = SourceList.SelectedIndices[0];
            if (selectedIndex >= 0 && selectedIndex < FunctionsList.Count)
            {
                m_SearchFunctionName = FunctionsList[selectedIndex].RawName;
                m_SearchOrdinal = FunctionsList[selectedIndex].Ordinal;

                return DestinationList.FindItemWithText(null);
            }
        }

        return null;
    }

    private void HighlightFunction_Click(object sender, EventArgs e)
    {
        ListView lvSrc, lvDst;
        List<CFunction> funcSrc;

        if (LVImports.Focused)
        {
            lvSrc = LVImports;
            lvDst = LVExports;
            funcSrc = m_CurrentImportsList;
        }
        else if (LVExports.Focused)
        {
            funcSrc = m_CurrentExportsList;
            lvSrc = LVExports;
            lvDst = LVImports;
        }
        else
        {
            return;
        }

        ListViewItem lvResult = FindMatchingFunctionInList(lvSrc, lvDst, funcSrc);
        if (lvResult != null)
        {
            lvDst.BeginUpdate();
            lvDst.SelectedIndices.Clear();
            lvResult.Selected = true;
            lvResult.EnsureVisible();
            lvDst.Focus();
        }

        lvDst.EndUpdate();
    }

    private void HighlightModuleInTreeOrList(object sender, EventArgs e)
    {
        if (TVModules.Focused && TVModules.SelectedNode?.Tag is CModule selectedModule)
        {
            LVModules.BeginUpdate();
            ListViewItem lvResult = LVModules.FindItemWithText(selectedModule.FileName);

            if (lvResult != null)
            {
                LVModules.SelectedIndices.Clear();
                lvResult.Selected = true;
                lvResult.EnsureVisible();
                LVModules.Focus();
            }

            LVModules.EndUpdate();
        }
        else if (LVModules.Focused && LVModules.SelectedIndices.Count > 0)
        {
            int selectedItemIndex = LVModules.SelectedIndices[0];

            if (selectedItemIndex < m_LoadedModulesList.Count)
            {
                CModule selectedListViewModule = m_LoadedModulesList[selectedItemIndex];
                TVModules.BeginUpdate();

                TreeNode resultNode = CUtils.TreeViewFindModuleNodeByObject(selectedListViewModule, m_RootNode);

                if (resultNode != null)
                {
                    TVModules.SelectedNode = resultNode;
                    TVModules.SelectedNode.Expand();
                    TVModules.SelectedNode.EnsureVisible();
                    TVModules.Select();
                }

                TVModules.EndUpdate();
            }
        }
    }

    private void CollapseAllMenuItem_Click(object sender, EventArgs e)
    {
        TVModules.BeginUpdate();
        TVModules.CollapseAll();
        TVModules.EndUpdate();
    }

    private void ExpandAllMenuItem_Click(object sender, EventArgs e)
    {
        TVModules.BeginUpdate();
        TVModules.ExpandAll();
        TVModules.SelectedNode?.EnsureVisible();
        TVModules.EndUpdate();
    }

    private void ShowSysInfoDialog(object sender, EventArgs e)
    {
        List<PropertyElement> si = [];
        var isLocal = true;

        if (m_Depends != null)
        {
            isLocal = !m_Depends.IsSavedSessionView;

            if (isLocal)
            {
                CUtils.CollectSystemInformation(si);
                m_Depends.SystemInformation = si;
            }
            else
            {
                si = m_Depends.SystemInformation;
            }
        }
        else
        {
            CUtils.CollectSystemInformation(si);
        }

        using (var sysInfoDlg = new SysInfoDialogForm(si, isLocal))
        {
            sysInfoDlg.ShowDialog();
        }
    }

    private void StatusBarToolStripMenuItem_Click(object sender, EventArgs e)
    {
        m_Configuration.ShowStatusBar = (sender as ToolStripMenuItem).Checked;
        StatusBar.Visible = m_Configuration.ShowStatusBar;
    }

    private void ToolbarToolStripMenuItem_Click(object sender, EventArgs e)
    {
        m_Configuration.ShowToolBar = (sender as ToolStripMenuItem).Checked;
        MainToolBar.Visible = m_Configuration.ShowToolBar;
    }

    private void HighlightMatching_Click(object sender, EventArgs e)
    {
        var name = FindFocusedListControlName();
        if (name != null)
        {
            switch (name)
            {
                case CConsts.TVModulesName:
                case CConsts.LVModulesName:
                    HighlightModuleInTreeOrList(sender, e);
                    break;

                case CConsts.LVImportsName:
                case CConsts.LVExportsName:
                    HighlightFunction_Click(sender, e);
                    break;
            }

        }
    }

    private void PreloadSymbolForSelectedModule(CModule module)
    {
        var moduleBase = CSymbolResolver.RetrieveCachedSymModule(module.FileName);
        if (moduleBase == IntPtr.Zero)
        {
            UpdateOperationStatus($"Please wait, loading symbols for \"{module.FileName}\"...");
            moduleBase = CSymbolResolver.LoadModule(module.FileName, 0);
            UpdateOperationStatus(string.Empty);

            if (moduleBase != IntPtr.Zero)
            {
                CSymbolResolver.CacheSymModule(module.FileName, moduleBase);
            }
        }
    }

    /// <summary>
    /// Invoked each time user selects a module. Builds parent import and export lists.
    /// </summary>
    /// <param name="module">Currently selected module.</param>
    private void BuildFunctionListForSelectedModule(CModule module)
    {
        ResetFunctionLists();

        //
        // Parent imports.
        //       
        m_CurrentImportsList = module.ParentImports;

        if (module.OriginalInstanceId != 0)
        {
            // Duplicate module, exports from the original instance.
            CModule origInstance = CUtils.InstanceIdToModule(module.OriginalInstanceId, m_LoadedModulesList);

            // Set list from original instance if it present, otherwise create new empty list. 
            m_CurrentExportsList = origInstance?.ModuleData.Exports ?? [];
        }
        else
        {
            //
            // Original module
            //
            m_CurrentExportsList = module.ModuleData.Exports;
        }

        //
        // Update function icons.
        //
        ResolveFunctionKindForList(m_CurrentImportsList, module, m_LoadedModulesList);
        ResolveFunctionKindForList(m_CurrentExportsList, module, m_LoadedModulesList);

        UpdateListViewInternal(LVExports, m_CurrentExportsList, m_Configuration.SortColumnExports, LVExportsSortOrder, DisplayCacheType.Exports);
        UpdateListViewInternal(LVImports, m_CurrentImportsList, m_Configuration.SortColumnImports, LVImportsSortOrder, DisplayCacheType.Imports);

        void UpdateListViewInternal(ListView listView, List<CFunction> functionList, int sortColumn, SortOrder sortOrder, DisplayCacheType displayCacheType)
        {
            listView.VirtualListSize = functionList.Count;
            if (listView.VirtualListSize > 0)
            {
                LVFunctionsSort(listView, sortColumn, sortOrder, functionList, displayCacheType);
            }
        }

        void ResolveFunctionKindForList(List<CFunction> currentList, CModule module, List<CModule> modulesList)
        {
            foreach (CFunction function in currentList)
            {
                function.ResolveFunctionKind(module, modulesList, m_ParentImportsHashTable);
            }
        }
    }

    private class CModuleComparer(SortOrder sortOrder, int fieldIndex, bool fullPaths) : IComparer<CModule>
    {
        private int FieldIndex { get; } = fieldIndex;
        private SortOrder SortOrder { get; } = sortOrder;

        public int Compare(CModule x, CModule y)
        {
            int comparisonResult = FieldIndex switch
            {
                _ when FieldIndex == (int)ModulesColumns.Name && !fullPaths =>
                    string.Compare(Path.GetFileName(x.FileName), Path.GetFileName(y.FileName), StringComparison.Ordinal),
                (int)ModulesColumns.Image => x.ModuleImageIndex.CompareTo(y.ModuleImageIndex),
                // (int)ModulesColumns.LoadOrder => x.ModuleData.LoadOrder.CompareTo(y.ModuleData.LoadOrder),//unused, profiling artifact
                (int)ModulesColumns.LinkChecksum => x.ModuleData.LinkChecksum.CompareTo(y.ModuleData.LinkChecksum),
                (int)ModulesColumns.RealChecksum => x.ModuleData.RealChecksum.CompareTo(y.ModuleData.RealChecksum),
                (int)ModulesColumns.VirtualSize => x.ModuleData.VirtualSize.CompareTo(y.ModuleData.VirtualSize),
                (int)ModulesColumns.PrefferedBase => x.ModuleData.PreferredBase.CompareTo(y.ModuleData.PreferredBase),
                (int)ModulesColumns.LinkTimeStamp => x.ModuleData.LinkTimeStamp.CompareTo(y.ModuleData.LinkTimeStamp),
                (int)ModulesColumns.FileTimeStamp => x.ModuleData.FileTimeStamp.CompareTo(y.ModuleData.FileTimeStamp),
                (int)ModulesColumns.FileSize => x.ModuleData.FileSize.CompareTo(y.ModuleData.FileSize),
                (int)ModulesColumns.Name => x.FileName.CompareTo(y.FileName),
                _ => string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal)
            };

            return SortOrder == SortOrder.Descending ? -comparisonResult : comparisonResult;
        }
    }

    private class CFunctionComparer(SortOrder sortOrder, int fieldIndex) : IComparer<CFunction>
    {
        private int FieldIndex { get; } = fieldIndex;
        private SortOrder SortOrder { get; } = sortOrder;

        public int Compare(CFunction x, CFunction y) =>
            (FieldIndex) switch
            {
                (int)FunctionsColumns.EntryPoint => x.IsForward() && y.IsForward() ? string.Compare(x.ForwardName, y.ForwardName) : x.Address.CompareTo(y.Address),
                (int)FunctionsColumns.Ordinal => x.Ordinal.CompareTo(y.Ordinal),
                (int)FunctionsColumns.Hint => x.Hint.CompareTo(y.Hint),
                (int)FunctionsColumns.Name => !string.IsNullOrEmpty(x.UndecoratedName) && !string.IsNullOrEmpty(y.UndecoratedName) ? x.UndecoratedName.CompareTo(y.UndecoratedName) : x.RawName.CompareTo(y.RawName),
                (int)FunctionsColumns.Image or _ => x.Kind.CompareTo(y.Kind)
            } * (SortOrder == SortOrder.Descending ? -1 : 1);
    }

    private static void UpdateListViewColumnSortMark(ListView listView, int columnIndex, SortOrder sortOrder)
    {
        static string GetProcessedText(string columnText)
        {
            return new StringBuilder(columnText)
                .Replace(CConsts.AscendSortMark, string.Empty)
                .Replace(CConsts.DescendSortMark, string.Empty)
                .ToString().Trim();
        }

        string commonText = GetProcessedText(listView.Columns[columnIndex].Text);

        foreach (ColumnHeader column in listView.Columns)
        {
            if (column.Index == columnIndex)
            {
                string newMark = (sortOrder == SortOrder.Ascending) ? CConsts.AscendSortMark : CConsts.DescendSortMark;
                column.Text = $"{newMark} {commonText}";
            }
            else
            {
                column.Text = GetProcessedText(column.Text);
            }
        }
    }

    private void HighlightOriginalInstance_Click(object sender, EventArgs e)
    {
        TVModules.BeginUpdate();

        CModule origInstance = CUtils.TreeViewGetOriginalInstanceFromNode(TVModules.SelectedNode, m_LoadedModulesList);
        if (origInstance != null)
        {
            var tvNode = CUtils.TreeViewFindModuleNodeByObject(origInstance, m_RootNode);
            if (tvNode != null)
            {
                TVModules.SelectedNode = tvNode;
                TVModules.SelectedNode.Expand();
                TVModules.SelectedNode.EnsureVisible();
                TVModules.Select();
            }
        }

        TVModules.EndUpdate();
    }

    TreeNode TreeViewFindNodeInstancePrev(TreeNode currentNode, TreeNode selectedNode, string moduleName)
    {
        TreeNode lastNode = null;

        while (currentNode != null && !m_InstanceStopSearch)
        {
            if (currentNode.Tag is CModule obj && obj.FileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentNode == selectedNode)
                {
                    m_InstanceStopSearch = true;
                    break;
                }
                else
                {
                    lastNode = currentNode;
                }
            }

            if (currentNode.Nodes.Count != 0 && !m_InstanceStopSearch)
            {
                var tvNode = TreeViewFindNodeInstancePrev(currentNode.Nodes[0], selectedNode, moduleName);
                if (tvNode != null)
                {
                    lastNode = tvNode;
                }
            }

            currentNode = currentNode.NextNode;
        }

        return lastNode;
    }

    TreeNode TreeViewFindNodeInstanceNext(TreeNode currentNode, TreeNode selectedNode, string moduleName)
    {
        TreeNode lastNode = null;

        while (currentNode != null && !m_InstanceStopSearch)
        {
            if (currentNode.Tag is CModule obj && obj.FileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentNode == selectedNode)
                {
                    m_InstanceSelfFound = true;
                }
                else if (m_InstanceSelfFound)
                {
                    m_InstanceStopSearch = true;
                    lastNode = currentNode;
                }
            }

            if (currentNode.Nodes.Count != 0 && !m_InstanceStopSearch)
            {
                var tvNode = TreeViewFindNodeInstanceNext(currentNode.Nodes[0], selectedNode, moduleName);
                if (tvNode != null)
                {
                    lastNode = tvNode;
                }
            }

            currentNode = currentNode.NextNode;
        }

        return lastNode;
    }

    private void HighlightInstanceHandler(bool bNextInstance = false)
    {
        if (TVModules.SelectedNode == null || TVModules.SelectedNode.Tag is not CModule obj)
        {
            return;
        }

        m_InstanceStopSearch = false;

        if (bNextInstance)
        {
            m_InstanceSelfFound = false;
        }

        TVModules.BeginUpdate();

        TreeNode tvNode;

        if (bNextInstance)
        {
            tvNode = TreeViewFindNodeInstanceNext(m_RootNode, TVModules.SelectedNode, obj.FileName);
        }
        else
        {
            tvNode = TreeViewFindNodeInstancePrev(m_RootNode, TVModules.SelectedNode, obj.FileName);
        }

        if (tvNode != null)
        {
            TVModules.SelectedNode = tvNode;
            TVModules.SelectedNode.Expand();
            TVModules.SelectedNode.EnsureVisible();
            TVModules.Select();
        }

        TVModules.EndUpdate();
    }

    private void HighlightPreviousInstance_Click(object sender, EventArgs e)
    {
        HighlightInstanceHandler(false);
    }

    private void HighlightNextInstance_Click(object sender, EventArgs e)
    {
        HighlightInstanceHandler(true);
    }

    private void MainMenu_FileDropDown(object sender, EventArgs e)
    {
        bool bSessionAllocated = m_Depends != null;
        MenuSaveAsItem.Visible = bSessionAllocated;
        MenuSaveItem.Visible = bSessionAllocated;
        MenuCloseItem.Visible = bSessionAllocated;
    }

    private CDepends LoadSessionObjectFromFile(string fileName, bool bIsCompressed = true)
    {
        try
        {
            if (bIsCompressed)
            {
                return (CDepends)CUtils.LoadPackedObjectFromFile(fileName, typeof(CDepends), UpdateOperationStatus);
            }
            else
            {
                return (CDepends)CUtils.LoadObjectFromFilePlainText(fileName, typeof(CDepends));
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException != null)
            {
                LogEvent(fileName, LogEventType.FileOpenSessionError, ex.InnerException.Message);
            }
            else
            {
                LogEvent(fileName, LogEventType.FileOpenSessionError, ex.Message);
            }
        }

        UpdateOperationStatus(string.Empty);
        return null;
    }

    /// <summary>
    /// Save/SaveAs handler.
    /// </summary>
    /// <param name="saveAs">Works as Save or SaveAs depending on this flag.</param>
    /// <param name="useCompression">If true then enabled compression for saved file.</param>
    private void SaveSessionObjectToFile(bool saveAs, bool useCompression = true)
    {
        string fileName;
        bool jsonOutput = false;

        if (m_Depends == null)
        {
            return;
        }

        // We want new filename for our session view.
        if (saveAs)
        {
            m_Depends.IsSavedSessionView = false;
            m_Depends.SessionFileName = string.Empty;
        }

        // This is new session.
        if (!m_Depends.IsSavedSessionView && string.IsNullOrEmpty(m_Depends.SessionFileName))
        {
            if (SaveFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            fileName = SaveFileDialog1.FileName;

            if (SaveFileDialog1.FilterIndex == 2) //JSON output file.
            {
                useCompression = false;
                jsonOutput = true;
            }
            else // WDS optionally packed file.
            {
                // Save system information into file also.
                if (m_Depends.SystemInformation.Count == 0)
                {
                    CUtils.CollectSystemInformation(m_Depends.SystemInformation);
                }

                m_Depends.IsSavedSessionView = true;
                m_Depends.SessionFileName = fileName;
            }
        }
        else
        {
            // This is loaded session, just save as is.
            fileName = m_Depends.SessionFileName;
        }

        bool bSaved = false;

        try
        {
            if (useCompression)
            {
                bSaved = CUtils.SavePackedObjectToFile(fileName, m_Depends, typeof(CDepends), UpdateOperationStatus);
            }
            else
            {
                bSaved = CUtils.SaveObjectToFilePlainText(fileName, m_Depends, typeof(CDepends));
            }
        }
        catch (Exception ex)
        {
            if (!jsonOutput)
            {
                m_Depends.IsSavedSessionView = false;
                m_Depends.SessionFileName = string.Empty;
            }
            if (ex.InnerException != null)
            {
                LogEvent(fileName, LogEventType.FileSessionSaveError, ex.InnerException.Message);
            }
            else
            {
                LogEvent(fileName, LogEventType.FileSessionSaveError, ex.Message);
            }
        }

        if (bSaved)
        {
            LogEvent(fileName, LogEventType.FileSessionSave);
        }

        UpdateOperationStatus(string.Empty);
    }

    private void MainMenuSave_Click(object sender, EventArgs e)
    {
        SaveSessionObjectToFile(false, m_Configuration.CompressSessionFiles);
    }

    private void MainMenuSaveAs_Click(object sender, EventArgs e)
    {
        SaveSessionObjectToFile(true, m_Configuration.CompressSessionFiles);
    }

    private void TVModules_AfterSelect(object sender, TreeViewEventArgs e)
    {
        CModule module = (CModule)e.Node.Tag;
        if (module != null)
        {
            if (m_Configuration.UseSymbols) PreloadSymbolForSelectedModule(module);
            BuildFunctionListForSelectedModule(module);
        }
    }

    /// <summary>
    /// Creates module entry for LVModules virtual listview (lower bottom view).
    /// </summary>
    /// <param name="module">The module to be displayed.</param>
    /// <returns>Listview item.</returns>
    private ListViewItem LVCreateModuleEntry(CModule module)
    {
        //
        // ModuleImage | Module | File TimeStamp | Link TimeStamp | FileSize | Attr. | LinkChecksum | Real Checksum | CPU
        // Subsystem | Preffered Base | VirtualSize | FileVer | ProductVer | ImageVer | LinkerVer | OSVer | SubsystemVer | 
        //

        //
        // Add item to ListView.
        //
        ListViewItem lvItem = new()
        {
            Tag = module,
            ImageIndex = module.GetIconIndexForModuleCompact()
        };

        string moduleDisplayName = module.GetModuleNameRespectApiSet(m_Configuration.ResolveAPIsets);

        if (!m_Configuration.FullPaths)
        {
            moduleDisplayName = Path.GetFileName(moduleDisplayName);
        }

        if (m_Configuration.UppperCaseModuleNames)
        {
            moduleDisplayName = moduleDisplayName.ToUpperInvariant();
        }

        // Module
        lvItem.SubItems.Add(moduleDisplayName);

        if (!module.IsProcessed)
        {
            // Empty row
            // i = 2, 0 - ModuleImage, 1 - ModuleName.
            for (int i = 2; i < LVModules.Columns.Count; i++)
            {
                lvItem.SubItems.Add("");
            }
        }
        else
        {
            var moduleData = module.ModuleData;

            // File time stamp
            lvItem.SubItems.Add(moduleData.FileTimeStamp.ToString(CConsts.DateTimeFormat24Hours));

            // Linker stamp
            string value = (module.IsReproducibleBuild) ?
                $"Repro hash: 0x{moduleData.LinkTimeStamp:X8}" : CUtils.TimeSince1970ToString(moduleData.LinkTimeStamp);
            lvItem.SubItems.Add(value);

            // File size
            lvItem.SubItems.Add($"{moduleData.FileSize:#,###0}");

            // Attributes
            lvItem.SubItems.Add(moduleData.Attributes.ShortName());

            // Link/Real Checksum
            lvItem.SubItems.Add($"0x{moduleData.LinkChecksum:X8}");

            if (moduleData.LinkChecksum != 0 && (moduleData.LinkChecksum != moduleData.RealChecksum))
            {
                lvItem.UseItemStyleForSubItems = false;
                lvItem.SubItems.Add($"0x{moduleData.RealChecksum:X8}", Color.Red, Color.White, lvItem.Font);
            }
            else
            {
                lvItem.SubItems.Add($"0x{moduleData.RealChecksum:X8}");
            }

            // CPU
            value = Enum.IsDefined(typeof(Machine), moduleData.Machine) ?
                ((Machine)moduleData.Machine).FriendlyName() : $"0x{moduleData.Machine:X4}";
            if (m_Depends.RootModule.ModuleData.Machine != moduleData.Machine)
            {
                lvItem.UseItemStyleForSubItems = false;
                lvItem.SubItems.Add(value, Color.Red, Color.White, lvItem.Font);
            }
            else
            {
                lvItem.SubItems.Add(value);
            }

            // Subsystem
            value = Enum.IsDefined(typeof(Subsystem), moduleData.Subsystem) ?
                ((Subsystem)moduleData.Subsystem).FriendlyName() : $"0x{moduleData.Subsystem:X4}";
            lvItem.SubItems.Add(value);

            // Debug Symbols
            if (moduleData.DebugDirTypes.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var entry in moduleData.DebugDirTypes)
                {
                    foreach (var dbgType in m_KnownDebugTypes)
                    {
                        if ((uint)dbgType == entry)
                        {
                            if (sb.Length > 0)
                            {
                                sb.Append(',');
                            }

                            var ddt = (DebugEntryType)entry;
                            string ddtDesc = ddt switch
                            {
                                DebugEntryType.Coff => "DBG",
                                DebugEntryType.CodeView => "CV",
                                DebugEntryType.Misc => "PDB",
                                DebugEntryType.Fpo => "FPO",
                                DebugEntryType.OmapFromSrc or DebugEntryType.OmapToSrc => "OMAP",
                                DebugEntryType.Borland => "Borland",
                                DebugEntryType.Clsid => "CLSID",
                                DebugEntryType.Reproducible => "REPRO",
                                DebugEntryType.EmbeddedPortablePdb => "EPPDB",
                                DebugEntryType.PdbChecksum => "PDBSUM",
                                DebugEntryType.ExtendedCharacteristics => "CHAREX",
                                _ => ddt.ToString()
                            };

                            sb.Append(ddtDesc);
                            break;
                        }
                    }
                }

                if (sb.Length > 0)
                {
                    value = sb.ToString();
                }
                else
                {
                    value = CConsts.NoneMsg;
                }
            }
            else
            {
                value = CConsts.NoneMsg;
            }
            lvItem.SubItems.Add(value);

            // Calculate the hexadecimal format string based on architecture
            var digitMultiply = UIntPtr.Size * (module.Is64bitArchitecture() ? 2 : 1);
            var hexFormat = $"X{digitMultiply}";

            // Preferred base
            lvItem.SubItems.Add($"0x{moduleData.PreferredBase.ToString(hexFormat)}");

            // Actual base (currently unused, profing artifact)
            /* value = (module.ModuleData.ActualBase == UIntPtr.Zero) ? "Unknown" : $"0x{moduleData.ActualBase.ToString(hexFormat)}";
             lvItem.SubItems.Add(value);*/

            lvItem.SubItems.Add($"0x{moduleData.VirtualSize:X8}");

            // lvItem.SubItems.Add(moduleData.LoadOrder.ToString()); //currently unused, profing artifact

            // Versions
            lvItem.SubItems.Add(moduleData.FileVersion);
            lvItem.SubItems.Add(moduleData.ProductVersion);
            lvItem.SubItems.Add(moduleData.ImageVersion);
            lvItem.SubItems.Add(moduleData.LinkerVersion);
            lvItem.SubItems.Add(moduleData.OSVersion);
            lvItem.SubItems.Add(moduleData.SubsystemVersion);
        }
        return lvItem;
    }

    /// <summary>
    /// Create function entry for LVImports/LVExports virtual listviews.
    /// </summary>
    /// <param name="function">Function information to be displayed.</param>
    /// <param name="module">Module linked with the function.</param>
    /// <returns>Listview item.</returns>
    private ListViewItem LVCreateFunctionEntry(CFunction function, CModule module)
    {
        ListViewItem lvItem = new();
        if (module == null)
        {
            return lvItem;
        }

        // Ordinal
        string ordinalValue = function.Ordinal == UInt32.MaxValue ? CConsts.NotAvailableMsg : $"{function.Ordinal} (0x{function.Ordinal:X4})";
        lvItem.SubItems.Add(ordinalValue);

        // Hint
        string hintValue = function.Hint == UInt32.MaxValue ? CConsts.NotAvailableMsg : $"{function.Hint} (0x{function.Hint:X4})";
        lvItem.SubItems.Add(hintValue);

        // FunctionName
        string functionName = string.Empty;
        if (function.SnapByOrdinal())
        {
            var resolvedFunction = module.ResolveFunctionForOrdinal(function.Ordinal);
            if (resolvedFunction != null)
            {
                functionName = m_Configuration.ViewUndecorated && resolvedFunction.IsNameDecorated() ? resolvedFunction.UndecorateFunctionName() : resolvedFunction.RawName;
            }
        }
        else
        {
            functionName = m_Configuration.ViewUndecorated && function.IsNameDecorated() ? function.UndecorateFunctionName() : function.RawName;
        }

        //
        // Nothing found, attempt to resolve using symbols.
        //
        if (string.IsNullOrEmpty(functionName) && m_Configuration.UseSymbols)
        {
            var moduleBase = CSymbolResolver.RetrieveCachedSymModule(module.FileName);
            if (moduleBase != IntPtr.Zero)
            {
                UInt64 address = Convert.ToUInt64(moduleBase) + function.Address;
                if (CSymbolResolver.QuerySymbolForAddress(address, out string symName))
                {
                    function.IsNameFromSymbols = true;
                    function.RawName = symName;
                    functionName = m_Configuration.ViewUndecorated && function.IsNameDecorated() ? function.UndecorateFunctionName() : function.RawName;
                }
            }
        }

        if (string.IsNullOrEmpty(functionName))
        {
            functionName = CConsts.NotAvailableMsg;
        }

        if (function.IsNameFromSymbols)
        {
            lvItem.UseItemStyleForSubItems = false;
            lvItem.SubItems.Add(functionName, Color.Black, m_Configuration.SymbolsHighlightColor, lvItem.Font);
        }
        else
        {
            lvItem.SubItems.Add(functionName);
        }

        // EntryPoint
        string entryPoint;
        if (function.IsForward())
        {
            entryPoint = function.ForwardName;
        }
        else
        {
            entryPoint = function.Address == 0 ? CConsts.NotBoundMsg : $"0x{function.Address:X8}";
        }
        lvItem.SubItems.Add(entryPoint);

        lvItem.ImageIndex = (int)function.Kind;
        return lvItem;
    }

    /// <summary>
    /// Retrieve list item from cache or build it from export list.
    /// Automatically called when ListView wants to populate items.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVExportsRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
        if (LVExportsCache != null && e.ItemIndex >= LVExportsFirstItem &&
            e.ItemIndex < LVExportsFirstItem + LVExportsCache.Length)
        {
            e.Item = LVExportsCache[e.ItemIndex - LVExportsFirstItem];
        }
        else
        {
            var selectedModule = TVModules.SelectedNode?.Tag as CModule;
            e.Item = LVCreateFunctionEntry(m_CurrentExportsList[e.ItemIndex], selectedModule);
        }
    }

    /// <summary>
    /// Retrieve list item from cache or build it from import list.
    /// Automatically called when ListView wants to populate items.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVImportsRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
        if (LVImportsCache != null && e.ItemIndex >= LVImportsFirstItem &&
            e.ItemIndex < LVImportsFirstItem + LVImportsCache.Length)
        {
            e.Item = LVImportsCache[e.ItemIndex - LVImportsFirstItem];
        }
        else
        {
            var selectedModule = TVModules.SelectedNode?.Tag as CModule;
            e.Item = LVCreateFunctionEntry(m_CurrentImportsList[e.ItemIndex], selectedModule);
        }
    }

    /// <summary>
    /// Retrieve list item from cache or build it from modules list.
    /// Automatically called when ListView wants to populate items.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVModulesRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
        if (LVModulesCache != null && e.ItemIndex >= LVModulesFirstItem &&
            e.ItemIndex < LVModulesFirstItem + LVModulesCache.Length)
        {
            e.Item = LVModulesCache[e.ItemIndex - LVModulesFirstItem];
        }
        else
        {
            e.Item = LVCreateModuleEntry(m_LoadedModulesList[e.ItemIndex]);
        }
    }

    /// <summary>
    /// Cache listview export entry.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVExportsCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
    {
        if (LVExportsCache != null && e.StartIndex >= LVExportsFirstItem && e.EndIndex <= LVExportsFirstItem + LVExportsCache.Length)
        {
            return;
        }

        LVExportsFirstItem = e.StartIndex;
        int length = e.EndIndex - e.StartIndex + 1;
        LVExportsCache = new ListViewItem[length];
        var selectedModule = TVModules.SelectedNode?.Tag as CModule;

        for (int i = 0, j = LVExportsFirstItem; i < length && j < m_CurrentExportsList.Count; i++, j++)
        {
            LVExportsCache[i] = LVCreateFunctionEntry(m_CurrentExportsList[j], selectedModule);
        }
    }

    /// <summary>
    /// Cache listview import entry.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVImportsCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
    {
        if (LVImportsCache != null && e.StartIndex >= LVImportsFirstItem && e.EndIndex <= LVImportsFirstItem + LVImportsCache.Length)
        {
            return;
        }

        LVImportsFirstItem = e.StartIndex;
        int length = e.EndIndex - e.StartIndex + 1;
        LVImportsCache = new ListViewItem[length];
        var selectedModule = TVModules.SelectedNode?.Tag as CModule;

        for (int i = 0, j = LVImportsFirstItem; i < length && j < m_CurrentImportsList.Count; i++, j++)
        {
            LVImportsCache[i] = LVCreateFunctionEntry(m_CurrentImportsList[j], selectedModule);
        }
    }

    /// <summary>
    /// Cache listview modules entry.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVModulesCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
    {
        if (LVModulesCache != null && e.StartIndex >= LVModulesFirstItem && e.EndIndex <= LVModulesFirstItem + LVModulesCache.Length)
        {
            return;
        }

        LVModulesFirstItem = e.StartIndex;
        int length = e.EndIndex - e.StartIndex + 1;
        LVModulesCache = new ListViewItem[length];

        for (int i = 0, j = LVModulesFirstItem; i < length && j < m_LoadedModulesList.Count; i++, j++)
        {
            LVModulesCache[i] = LVCreateModuleEntry(m_LoadedModulesList[j]);
        }
    }

    /// <summary>
    /// LVImports/LVExports virtual listview search handler.
    /// </summary>
    /// <param name="itemList"></param>
    /// <param name="e"></param>
    private void LVFunctionsSearchForVirtualItem(List<CFunction> itemList, SearchForVirtualItemEventArgs e)
    {
        // Search by ordinal.
        if (string.IsNullOrEmpty(m_SearchFunctionName))
        {
            if (m_SearchOrdinal != UInt32.MaxValue)
            {
                foreach (var entry in itemList)
                {
                    if (entry.Ordinal == m_SearchOrdinal)
                    {
                        e.Index = itemList.IndexOf(entry);
                        return;
                    }
                }
            }
        }
        else
        {
            // Search by name.
            foreach (var entry in itemList)
            {
                if (entry.RawName.Equals(m_SearchFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    e.Index = itemList.IndexOf(entry);
                    return;
                }
            }

            // If item is not found, search by ordinal if possible.
            if (m_SearchOrdinal != UInt32.MaxValue)
            {
                foreach (var entry in itemList)
                {
                    if (entry.Ordinal == m_SearchOrdinal)
                    {
                        e.Index = itemList.IndexOf(entry);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// LVImports virtual listview search handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVImportsSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
    {
        LVFunctionsSearchForVirtualItem(m_CurrentImportsList, e);
    }

    /// <summary>
    /// LVExports virtual listview search handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVExportsSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
    {
        LVFunctionsSearchForVirtualItem(m_CurrentExportsList, e);
    }

    /// <summary>
    /// LVModules virtual listview search handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVModulesSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
    {
        foreach (var module in m_LoadedModulesList)
        {
            if (module.FileName.Equals(e.Text, StringComparison.OrdinalIgnoreCase))
            {
                e.Index = m_LoadedModulesList.IndexOf(module);
                return;
            }
        }
    }

    /// <summary>
    /// LVImports/LVExports virtual listview sort handler.
    /// </summary>
    /// <param name="listView"></param>
    /// <param name="columnIndex"></param>
    /// <param name="sortOrder"></param>
    /// <param name="data"></param>
    /// <param name="cacheType"></param>
    private void LVFunctionsSort(ListView listView, int columnIndex, SortOrder sortOrder, List<CFunction> data, DisplayCacheType cacheType)
    {
        IComparer<CFunction> funcComparer = new CFunctionComparer(sortOrder, columnIndex);
        data.Sort(funcComparer);
        //
        // Reset listview items cache.
        //
        UpdateItemsView(listView, cacheType);
        UpdateListViewColumnSortMark(listView, columnIndex, sortOrder);
    }

    private void LVExportsColumnClick(object sender, ColumnClickEventArgs e)
    {
        int columnIndex = e.Column;
        m_Configuration.SortColumnExports = columnIndex;
        LVExportsSortOrder = (LVExportsSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVFunctionsSort(LVExports, columnIndex, LVExportsSortOrder, m_CurrentExportsList, DisplayCacheType.Exports);
    }

    private void LVImportsColumnClick(object sender, ColumnClickEventArgs e)
    {
        int columnIndex = e.Column;
        m_Configuration.SortColumnImports = columnIndex;
        LVImportsSortOrder = (LVImportsSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVFunctionsSort(LVImports, columnIndex, LVImportsSortOrder, m_CurrentImportsList, DisplayCacheType.Imports);
    }

    /// <summary>
    /// LVModules virtual listview sort handler.
    /// </summary>
    /// <param name="listView"></param>
    /// <param name="columnIndex"></param>
    /// <param name="sortOrder"></param>
    /// <param name="moduleList"></param>
    /// <param name="cacheType"></param>
    private void LVModulesSort(ListView listView, int columnIndex, SortOrder sortOrder, List<CModule> moduleList, DisplayCacheType cacheType)
    {
        IComparer<CModule> modulesComparer = new CModuleComparer(sortOrder, columnIndex, m_Configuration.FullPaths);
        moduleList.Sort(modulesComparer);
        //
        // Reset listview items cache.
        //
        UpdateItemsView(listView, cacheType);
        UpdateListViewColumnSortMark(listView, columnIndex, sortOrder);
    }

    private void LVModulesColumnClick(object sender, ColumnClickEventArgs e)
    {
        int columnIndex = e.Column;
        m_Configuration.SortColumnModules = columnIndex;
        LVModulesSortOrder = (LVModulesSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVModulesSort(LVModules, columnIndex, LVModulesSortOrder, m_LoadedModulesList, DisplayCacheType.Modules);
    }

    private void ViewOpenModuleLocationItem_Click(object sender, EventArgs e)
    {
        ProcessModuleEntry(ProcessModuleAction.OpenFileLocation);
    }

    /// <summary>
    /// Callback used to update information in the status bar.
    /// </summary>
    /// <param name="status"></param>
    private void UpdateOperationStatus(string status)
    {
        if (toolBarStatusLabel.Owner.InvokeRequired)
        {
            toolBarStatusLabel.Owner.BeginInvoke((MethodInvoker)delegate
            {
                toolBarStatusLabel.Text = status;
            });
        }
        else
        {
            toolBarStatusLabel.Text = status;
        }

        Application.DoEvents();
    }

    private void ViewRefreshItem_Click(object sender, EventArgs e)
    {
        if (m_Depends != null)
        {
            var fName = m_Depends.RootModule.RawFileName;
            OpenInputFile(fName);
        }
    }

    private async void LVFunctions_KeyPress(object sender, KeyPressEventArgs e)
    {
        ListView lvDst = LVImports.Focused ? LVImports : LVExports;

        if (!lvDst.Focused || lvDst.VirtualListSize <= 0)
        {
            e.Handled = true;
            return;
        }

        if (!char.IsLetterOrDigit(e.KeyChar))
        {
            e.Handled = true;
            return;
        }

        m_FunctionLookupText += char.ToLower(e.KeyChar);

        if (m_FunctionsHintForm.Controls[CConsts.HintFormLabelControl] is Label hintLabel)
        {
            hintLabel.Text = "Search: " + m_FunctionLookupText;
            hintLabel.Size = hintLabel.PreferredSize;

            Point location = lvDst.PointToScreen(new Point(lvDst.Bounds.Left, lvDst.Bounds.Bottom));

            m_FunctionsHintForm.Size = new Size(hintLabel.Width + 10, hintLabel.Height + 10);
            m_FunctionsHintForm.Location = location;
            m_FunctionsHintForm.Show();
        }

        List<CFunction> currentList = lvDst == LVImports ? m_CurrentImportsList : m_CurrentExportsList;

        m_SearchOrdinal = UInt32.MaxValue;
        m_SearchFunctionName = string.Empty;

        var matchingItem = currentList.FirstOrDefault(item => item.RawName.StartsWith(m_FunctionLookupText, StringComparison.OrdinalIgnoreCase));
        if (matchingItem != null)
        {
            m_SearchFunctionName = matchingItem.RawName;
            m_SearchOrdinal = matchingItem.Ordinal;

            lvDst.BeginUpdate();
            lvDst.SelectedIndices.Clear();
            ListViewItem lvResult = lvDst.FindItemWithText(null);

            if (lvResult != null)
            {
                lvResult.Selected = true;
                lvResult.EnsureVisible();
                lvDst.Focus();
            }

            lvDst.EndUpdate();
        }

        await Task.Delay(2000);
        m_FunctionsHintForm.Hide();
        m_FunctionLookupText = "";
    }

    private async void LVModules_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (!LVModules.Focused || LVModules.VirtualListSize <= 0)
        {
            e.Handled = true;
            return;
        }

        if (!char.IsLetterOrDigit(e.KeyChar))
        {
            e.Handled = true;
            return;
        }

        m_ModuleLookupText += char.ToLower(e.KeyChar);

        if (m_ModulesHintForm.Controls[CConsts.HintFormLabelControl] is Label hintLabel)
        {
            hintLabel.Text = "Search: " + m_ModuleLookupText;
            hintLabel.Size = hintLabel.PreferredSize;

            Point location = new(LVModules.Bounds.Left, LVModules.Bounds.Bottom);
            m_ModulesHintForm.Size = new Size(hintLabel.Width + 10, hintLabel.Height + 10);
            m_ModulesHintForm.Location = LVModules.PointToScreen(location);
            m_ModulesHintForm.Show();
        }

        CModule matchingModule = m_LoadedModulesList.FirstOrDefault(module =>
        {
            string moduleName = Path.GetFileName(module.GetModuleNameRespectApiSet(m_Configuration.ResolveAPIsets));
            return moduleName.StartsWith(m_ModuleLookupText, StringComparison.OrdinalIgnoreCase);
        });

        if (matchingModule != null)
        {
            LVModules.BeginUpdate();
            LVModules.SelectedIndices.Clear();
            ListViewItem lvResult = LVModules.FindItemWithText(matchingModule.FileName);

            if (lvResult != null)
            {
                lvResult.Selected = true;
                lvResult.EnsureVisible();
                LVModules.Focus();
            }

            LVModules.EndUpdate();

        }

        await Task.Delay(2000);
        m_ModulesHintForm.Hide();
        m_ModuleLookupText = "";
    }

    private void LVFunctions_Leave(object sender, EventArgs e)
    {
        m_FunctionsHintForm.Hide();
        m_FunctionLookupText = "";
    }

    private void LVModules_Leave(object sender, EventArgs e)
    {
        m_ModulesHintForm.Hide();
        m_ModuleLookupText = "";
    }

    private void RichEditEnableInterfaceButtons(bool enable)
    {
        ViewPropertiesItem.Enabled = enable;
        PropertiesToolButton.Enabled = enable;
        toolStripMenuItem13.Enabled = enable;
        ContextPropertiesItem.Enabled = enable;
    }

    private void RichEditLog_Enter(object sender, EventArgs e)
    {
        RichEditEnableInterfaceButtons(false);
    }

    private void RichEditLog_Leave(object sender, EventArgs e)
    {
        RichEditEnableInterfaceButtons(true);
    }

    private void RichEditLog_SelectionChanged(object sender, EventArgs e)
    {
        CopyToolButton.Enabled = (reLog.SelectionLength > 0);
    }

    private void LVModules_Click(object sender, EventArgs e)
    {
        CopyToolButton.Enabled = LVModules.SelectedIndices.Count > 0;
    }

    private void LVFunctions_Click(object sender, EventArgs e)
    {
        if (LVExports.Focused)
        {
            CopyToolButton.Enabled = LVExports.SelectedIndices.Count > 0;
        }
        else
        {
            CopyToolButton.Enabled = LVImports.SelectedIndices.Count > 0;
        }
    }

    private void TVModules_Click(object sender, EventArgs e)
    {
        CopyToolButton.Enabled = TVModules.SelectedNode != null;
    }

    private void MainMenu_ConfigureItemClick(object sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem)
        {
            ShowConfigurationForm(Convert.ToInt32(menuItem.Tag));
        }
    }

    private void MenuNewInstance_Click(object sender, EventArgs e)
    {
        CUtils.RunExternalCommand(Application.ExecutablePath, false);
    }

    private void MenuDocumentation_Click(object sender, EventArgs e)
    {
        CUtils.RunExternalCommand(CConsts.WinDependsDocs, true);
    }

    private void ToolBarSymStatus_DoubleClick(object sender, EventArgs e)
    {
        ShowConfigurationForm(CConsts.IdxTabSymbols);
    }

    private void ToolBarSymStatus_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            statusBarPopupMenu.Show(StatusBar,
                new Point(StatusBar.Width, StatusBar.Height - toolBarSymStatusLabel.Height), ToolStripDropDownDirection.AboveLeft);
        }
    }

    private void SymStateMenuItem_Click(object sender, EventArgs e)
    {
        m_Configuration.UseSymbols = symStateChangeMenuItem.Checked;
        ApplySymbolsConfiguration();
    }

    private void StatusBarPopupMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        symStateChangeMenuItem.Checked = m_Configuration.UseSymbols;
    }
}
