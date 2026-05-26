/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       MAINFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        24 May 2026
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
    SuccessSession,
    Cancelled
}

public partial class MainForm : Form
{
    /// <summary>
    /// Depends-core interface class.
    /// </summary>
    readonly CCoreClient _coreClient;

    /// <summary>
    /// Depends current context, must be initialized.
    /// </summary>
    CDepends _depends;

    /// <summary>
    /// Most Recently Used file list.
    /// </summary>
    CMRUList? _mruList;

    /// <summary>
    /// Program settings.
    /// </summary>
    CConfiguration _configuration;

    /// <summary>
    /// FileOpen dedicated class.
    /// </summary>
    readonly CFileOpenOrchestrationService _fileOpenOrchestrationService = new();

    /// <summary>
    /// Workaround for WinForms glitches.
    /// </summary>
    Control? _focusedControl;

    /// <summary>
    /// Root node of TVModules, must be initialized with AddModule*** routine.
    /// </summary>
    TreeNode? _rootNode;

    //
    // Flags used in Highlight instance operations
    //
    bool _instanceStopSearch;
    bool _instanceSelfFound;

    Form _functionsHintForm;
    Form _modulesHintForm;
    bool _disposingHintForms;

    string _functionLookupText = string.Empty;
    string _moduleLookupText = string.Empty;

    string? _searchFunctionName;
    UInt32 _searchOrdinal;

    readonly string[] _commandLineArgs;

    /// <summary>
    /// Flag to let the others know when WinDepends application is shutting down.
    /// </summary>
    bool _shutdownInProgress;

    /// <summary>
    /// Log search state.
    /// </summary>
    readonly LogSearchState _logSearchState = new();
    private FindDialogForm _findDialog;

    /// <summary>
    /// Gets the log search state for use by FindDialogForm.
    /// </summary>
    public LogSearchState LogSearchState => _logSearchState;

    readonly Dictionary<uint, string> _debugAbbreviations = new()
    {
        [(uint)DebugEntryType.Coff] = "DBG",
        [(uint)DebugEntryType.CodeView] = "CV",
        [(uint)DebugEntryType.Misc] = "PDB",
        [(uint)DebugEntryType.Fpo] = "FPO",
        [(uint)DebugEntryType.OmapFromSrc] = "OMAP",
        [(uint)DebugEntryType.OmapToSrc] = "OMAP",
        [(uint)DebugEntryType.Borland] = "Borland",
        [(uint)DebugEntryType.Clsid] = "CLSID",
        // [(uint)DebugEntryType.Reproducible] = "REPRO",
        [(uint)DebugEntryType.EmbeddedPortablePdb] = "EPPDB",
        [(uint)DebugEntryType.PdbChecksum] = "PDBSUM",
        // [(uint)DebugEntryType.ExtendedCharacteristics] = "CHAREX"
    };

    ListViewItem[] LVImportsCache = [];
    ListViewItem[] LVExportsCache = [];

    ListViewItem[] LVModulesCache = [];

    int LVImportsFirstItem;
    int LVExportsFirstItem;

    int LVModulesFirstItem;

    List<CFunction> _currentExportsList = [];
    List<CFunction> _currentImportsList = [];

    readonly Dictionary<int, FunctionHashObject> _parentImportsHashTable = [];

    readonly List<CModule> _loadedModulesList = [];

    SortOrder LVImportsSortOrder = SortOrder.Ascending;
    SortOrder LVExportsSortOrder = SortOrder.Ascending;
    SortOrder LVModulesSortOrder = SortOrder.Ascending;

    private bool _isCurrentlyOverLink = false;
    private int _currentLinkInstanceId = 0;

    private sealed class ModuleLinkInfo
    {
        public int Start { get; init; }
        public int Length { get; init; }
        public int End => Start + Length;
        public int InstanceId { get; init; }
    }

    private readonly ToolTip moduleToolTip = new ToolTip();
    private readonly List<ModuleLinkInfo> _moduleLinks = [];

    public MainForm()
    {
        InitializeComponent();

        _shutdownInProgress = false;
        reLog.Font = new Font(reLog.Font.FontFamily, CConsts.DefaultGuiFontSize, reLog.Font.Style, GraphicsUnit.Point);

        //
        // Add welcome message to the log.
        //
        AddLogMessage($"{CConsts.ProgramName} started, " +
            $"version {CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild} BETA",
            LogMessageType.ContentDefined, Color.Black, true);

        _configuration = CConfigManager.LoadConfiguration();
        EnsureColumnWidthCollections();
        CPathResolver.UserDirectoriesKM = _configuration.UserSearchOrderDirectoriesKM;
        CPathResolver.UserDirectoriesUM = _configuration.UserSearchOrderDirectoriesUM;

        var dbghelpInit = CSymbolResolver.AllocateSymbolResolver(_configuration.SymbolsDllPath,
                _configuration.SymbolsStorePath, _configuration.UseSymbols);

        if (dbghelpInit != SymbolResolverInitResult.InitializationFailure &&
            dbghelpInit != SymbolResolverInitResult.DllLoadFailure)
        {
            _configuration.SymbolsDllPath = CSymbolResolver.DllPath;
            _configuration.SymbolsStorePath = CSymbolResolver.StorePath;
        }

        //
        // Check for command line parameters.
        //
        _commandLineArgs = Environment.GetCommandLineArgs();

        //
        // Start server app.
        //       
        _coreClient = new(_configuration.CoreServerAppLocation, CConsts.CoreServerAddress, AddLogMessage);
        if (_coreClient.ConnectClient())
        {
            if (_coreClient.GetKnownDllsAll(CPathResolver.KnownDlls,
                CPathResolver.KnownDlls32,
                out string path, out string path32))
            {
                CPathResolver.KnownDllsPath = path;
                CPathResolver.KnownDllsPath32 = path32;
                CPathResolver.SyncKnownDllsCache();
            }

            _coreClient.SetApiSetSchemaNamespaceUse(_configuration.ApiSetSchemaFile);
        }

        // Display this message after server initialization message.
        LogSymbolsInitializationResult(dbghelpInit);

        LVExports.VirtualMode = true;
        LVExports.VirtualListSize = 0;

        LVImports.VirtualMode = true;
        LVImports.VirtualListSize = 0;

        LVModules.VirtualMode = true;
        LVModules.VirtualListSize = 0;

        _functionsHintForm = CreateHintForm(CConsts.HintFormLabelControl);
        _modulesHintForm = CreateHintForm(CConsts.HintFormLabelControl);

        moduleToolTip.AutoPopDelay = 2000;
        moduleToolTip.InitialDelay = 500;
        moduleToolTip.ReshowDelay = 200;
        moduleToolTip.ShowAlways = true;
    }

    private void LogSymbolsInitializationResult(SymbolResolverInitResult result)
    {
        switch (result)
        {
            case SymbolResolverInitResult.DllLoadFailure:
                AddLogMessage($"DBGHELP is not initialized, \"{CSymbolResolver.DllPath}\" is not loaded",
                    LogMessageType.ErrorOrWarning);
                break;
            case SymbolResolverInitResult.InitializationFailure:
                AddLogMessage($"DBGHELP initialization failed for \"{CSymbolResolver.DllPath}\", " +
                    $"store \"{CSymbolResolver.StorePath}\"", LogMessageType.ErrorOrWarning);
                break;
            case SymbolResolverInitResult.SuccessWithSymbols:
                AddLogMessage($"DBGHELP initialized using \"{CSymbolResolver.DllPath}\", " +
                    $"store \"{CSymbolResolver.StorePath}\"", LogMessageType.Information);
                break;
            case SymbolResolverInitResult.SuccessForUndecorationOnly:
                AddLogMessage($"DBGHELP initialized (undecoration only) using \"{CSymbolResolver.DllPath}\", " +
                    $"store \"{CSymbolResolver.StorePath}\"", LogMessageType.Information);
                break;
            case SymbolResolverInitResult.SuccessWithSymbolsAlternateDll:
                AddLogMessage($"DBGHELP initialized with best available \"{CSymbolResolver.DllPath}\", " +
                    $"store \"{CSymbolResolver.StorePath}\"", LogMessageType.Information);
                break;
        }
    }

    /// <summary>
    /// Creates a small borderless form used as a hint tooltip.
    /// </summary>
    /// <param name="LabelName">The name assigned to the label control hosted by the form.</param>
    /// <returns>A configured hint form instance.</returns>
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

    private static bool IsManagedAnyCpuLike(CModule module)
    {
        if (module == null)
            return false;

        if (module.ModuleData.ImageDotNet != 1)
            return false;

        try
        {
            using var fs = new FileStream(module.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(fs);

            var corHeader = peReader.PEHeaders?.CorHeader;
            if (corHeader == null)
                return false;

            var flags = corHeader.Flags;
            bool ilOnly = (flags & CorFlags.ILOnly) != 0;
            bool req32 = (flags & CorFlags.Requires32Bit) != 0;

            var machine = peReader.PEHeaders.CoffHeader.Machine;

            if (machine == Machine.I386 && ilOnly && !req32)
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool IsCpuMismatchForDisplay(CModule module, CModule rootModule)
    {
        bool isRootImageDotNet = rootModule.ModuleData.ImageDotNet == 1;

        if (isRootImageDotNet)
            return false;

        if (module.ModuleData.Machine == rootModule.ModuleData.Machine)
            return false;

        if (IsManagedAnyCpuLike(module))
            return false;

        return true;
    }

    /// <summary>
    /// Return name of one of the active lists that has focus.
    /// </summary>
    /// <returns>Name of the focused control.</returns>
    private string? FindFocusedListControlName()
    {
        Control[] controls = [TVModules, LVModules, LVImports, LVExports];
        return controls.FirstOrDefault(c => c.Focused)?.Name;
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

        LVModules.BeginUpdate();
        try
        {
            ResetModulesList();
        }
        finally { LVModules.EndUpdate(); }

        ClearModuleLinks();
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
                    if (_configuration.AutoExpands)
                    {
                        ExpandAllModulesWithUpdate();
                    }
                }
                break;

            case FileViewUpdateAction.ModulesTreeAndListChange:
                {
                    TVModules.BeginUpdate();
                    try
                    {
                        TreeViewUpdateNode(_rootNode);
                    }
                    finally { TVModules.EndUpdate(); }

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
            CConsts.FunctionIconsWidth, CConsts.FunctionIconsHeight, Color.Magenta);

        LVExports.SmallImageList = CUtils.CreateImageList(Properties.Resources.FunctionIcons,
            CConsts.FunctionIconsWidth, CConsts.FunctionIconsHeight, Color.Magenta);

        LVExports.Columns[_configuration.SortColumnExports].Text =
            $"{CConsts.AscendSortMark} {LVExports.Columns[_configuration.SortColumnExports].Text}";
    }

    /// <summary>
    /// Drag and Drop handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainForm_ElevatedDragDrop(object? sender, ElevatedDragDropEventArgs e)
    {
        if (e.Files.Count > 0 && !string.IsNullOrEmpty(e.Files[0]))
        {
            OpenInputFile(e.Files[0]);
        }
    }

    bool CreateOrUpdateToolbarImageStrip()
    {
        bool useClassic = _configuration.ToolBarTheme == ToolBarThemeType.Classic;

        Size desiredSize = useClassic
            ? new Size(CConsts.ToolBarIconsWidthClassic, CConsts.ToolBarIconsHeigthClassic)
            : new Size(CConsts.ToolBarIconsWidth, CConsts.ToolBarIconsHeigth);

        MainToolBar.ImageScalingSize = CUtils.CalculateToolBarImageSize(
            useClassic,
            CUtils.GetDpiScalingFactor(),
            desiredSize);

        CUtils.LoadToolbarImages(
            MainToolBar,
            desiredSize,
            useClassic,
            useClassic ? Color.Silver : Color.White);

        return MainToolBar.ImageList != null;
    }

    public void RestoreWindowSettings()
    {
        int? left = _configuration.WindowLeft;
        int? top = _configuration.WindowTop;
        int? height = _configuration.WindowHeight;
        int? width = _configuration.WindowWidth;
        int? state = _configuration.WindowState;

        bool hasValidDimensions = width.GetValueOrDefault() >= CConsts.MinValidWidth &&
                             height.GetValueOrDefault() >= CConsts.MinValidHeight;

        bool hasValidPosition = left.HasValue && top.HasValue &&
                                   CUtils.IsPointVisible(new Point(left.Value, top.Value));

        if (left.HasValue && top.HasValue && hasValidDimensions && hasValidPosition)
        {
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = CUtils.GetAdjustedBounds(new Rectangle(
                left.Value, top.Value, width.Value, height.Value));
        }
        else
        {
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        this.WindowState = (state.HasValue && state.Value != (int)FormWindowState.Minimized)
            ? (FormWindowState)state.Value
            : FormWindowState.Normal;

    }

    private void SetDefaultStatusBarText()
    {
        toolBarStatusLabel.Text = "Use the menu File->Open or drag and drop a file into the window to begin analysis";
    }

    /// <summary>
    /// MainForm load (Create) event.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MainForm_Load(object sender, EventArgs e)
    {
        ApplyConfiguredGuiFontSize();
        RestoreColumnWidthsFromConfiguration();
        LVModules.ColumnWidthChanged += LVModules_ColumnWidthChanged;
        LVImports.ColumnWidthChanged += LVImports_ColumnWidthChanged;
        LVExports.ColumnWidthChanged += LVExports_ColumnWidthChanged;

        //
        // Enable drag and drop for elevated instance.
        //
        ElevatedDragDropManager.EnableForWindow(this.Handle);
        ElevatedDragDropManager.EnableForWindow(LVImports.Handle);
        ElevatedDragDropManager.EnableForWindow(LVExports.Handle);
        ElevatedDragDropManager.EnableForWindow(reLog.Handle);
        ElevatedDragDropManager.EnableForWindow(LVModules.Handle);
        ElevatedDragDropManager.EnableForWindow(TVModules.Handle);
        ElevatedDragDropManager.Instance.ElevatedDragDrop += MainForm_ElevatedDragDrop;

        //
        // Create and load Most Recently Used files.
        //
        _mruList = new CMRUList(FileMenuItem,
            FileMenuItem.DropDownItems.IndexOf(MenuOpenNewInstance),
            _configuration.MRUList,
            _configuration.HistoryDepth,
            _configuration.HistoryShowFullPath,
            OpenInputFile,
            toolBarStatusLabel);

        //
        // Setup image lists for tree/list views.
        //
        CreateImageListsForViews();

        //
        // Restore window position, size and state.
        //
        RestoreWindowSettings();

        //
        // Toolbar images setup.
        //
        if (CreateOrUpdateToolbarImageStrip())
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
        AutoExpandToolButton.Checked = _configuration.AutoExpands;
        ViewFullPathsToolButton.Checked = _configuration.FullPaths;
        ViewUndecoratedToolButton.Checked = _configuration.ViewUndecorated;
        ResolveAPISetsToolButton.Checked = _configuration.ResolveAPIsets;

        //
        // Disable Save/Copy buttons by default.
        //
        SaveToolButton.Enabled = false;
        CopyToolButton.Enabled = false;

        //
        // Set program title.
        //
        var suffix = CUtils.IsAdministrator
            ? Environment.Is64BitProcess ? CConsts.Admin64Msg : CConsts.AdminMsg
            : Environment.Is64BitProcess ? CConsts.SixtyFourBitsMsg : "";

        this.Text = $"{CConsts.ProgramName}{suffix}";

        var fileOpened = false;

        //
        // Open file (if it was submitted through command line).
        //
        if (_commandLineArgs.Length > 1)
        {
            var fName = _commandLineArgs[1];
            if (!string.IsNullOrEmpty(fName) && File.Exists(fName))
            {
                fileOpened = OpenInputFile(fName);
            }
        }

        if (!fileOpened)
        {
            SetDefaultStatusBarText();
        }

    }

    /// <summary>
    /// MainForm close handler.
    /// Writes configuration back to disk.
    /// </summary>
    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        try
        {
            CleanupHintForms();
            ClearModuleLinks();
            moduleToolTip?.Dispose();

            if (_mruList != null && _configuration != null)
            {
                _configuration.MRUList.Clear();
                _configuration.MRUList.AddRange(_mruList.GetCurrentItems());
                CConfigManager.SaveConfiguration(_configuration);
            }
        }
        catch (Exception)
        {
            // Intentionally silent: shutdown/teardown path where UI logging targets may be disposed.
        }
        finally
        {
            _coreClient?.Dispose();
        }
    }

    private void MenuAbout_Click(object sender, EventArgs e)
    {
        using (AboutForm aboutForm = new(_configuration.EscKeyEnabled))
        {
            aboutForm.Font = this.Font;
            aboutForm.ShowDialog();
        }
    }

    private void ApplySymbolsConfiguration()
    {
        if (_configuration.UseSymbols)
        {
            var symStorePath = _configuration.SymbolsStorePath;
            var symDllPath = _configuration.SymbolsDllPath;

            //
            // Set defaults in case if nothing selected.
            //
            if (string.IsNullOrEmpty(symDllPath))
            {
                symDllPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CConsts.DbgHelpDll);
                _configuration.SymbolsDllPath = symDllPath;
            }
            if (string.IsNullOrEmpty(symStorePath))
            {
                symStorePath = $"srv*{Path.Combine(Path.GetTempPath(), CConsts.SymbolsDefaultStoreDirectory)}{CConsts.SymbolsDownloadLink}";
                _configuration.SymbolsStorePath = symStorePath;
            }

            CSymbolResolver.ReleaseSymbolResolver();
            var result = CSymbolResolver.AllocateSymbolResolver(symDllPath, symStorePath, _configuration.UseSymbols);
            if (result != SymbolResolverInitResult.InitializationFailure &&
                result != SymbolResolverInitResult.DllLoadFailure)
            {
                _configuration.SymbolsDllPath = CSymbolResolver.DllPath;
                _configuration.SymbolsStorePath = CSymbolResolver.StorePath;
            }
            LogSymbolsInitializationResult(result);
        }
        else
        {
            if (CSymbolResolver.ReleaseSymbolResolver())
            {
                AddLogMessage($"Debug symbols deallocated", LogMessageType.Information);
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
        CConfiguration optConfig = new(_configuration);
        if (_depends != null)
        {
            is64bitFile = _depends.RootModule.Is64bitArchitecture();
            currentFileName = Path.GetDirectoryName(_depends.RootModule.FileName);
        }
        else
        {
            if (!string.IsNullOrEmpty(Application.StartupPath))
            {
                currentFileName = Path.TrimEndingDirectorySeparator(Application.StartupPath);
            }
        }

        var bAutoExpandPrev = _configuration.AutoExpands;
        var bFullPathPrev = _configuration.FullPaths;
        var bUpperCaseModulesNamesPrev = _configuration.UpperCaseModuleNames;
        var bUndecoratedPrev = _configuration.ViewUndecorated;
        var bResolveAPISetsPrev = _configuration.ResolveAPIsets;
        var bHighlightAPISetsPrev = _configuration.HighlightApiSet;
        var bUseApiSetSchemaFilePrev = _configuration.UseApiSetSchemaFile;
        var ApiSetSchemaFilePrev = _configuration.ApiSetSchemaFile;
        var bUseSymbolsPrev = _configuration.UseSymbols;
        var guiFontSizePrev = _configuration.GuiFontSize;

        using (ConfigurationForm configForm = new(currentFileName,
                                                  is64bitFile,
                                                  optConfig,
                                                  _coreClient,
                                                  pageIndex))
        {
            configForm.Font = this.Font;
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                _configuration = optConfig;

                //
                // Re-display MRU list.
                //
                _mruList.UpdateSettings(_configuration.HistoryDepth, _configuration.HistoryShowFullPath);

                //
                // Check toolbar buttons depending on settings.
                //
                AutoExpandToolButton.Checked = _configuration.AutoExpands;
                ViewFullPathsToolButton.Checked = _configuration.FullPaths;
                ViewUndecoratedToolButton.Checked = _configuration.ViewUndecorated;
                ResolveAPISetsToolButton.Checked = _configuration.ResolveAPIsets;

                //
                // Update settings only if there are changed settings.
                //
                if (_configuration.AutoExpands != bAutoExpandPrev ||
                    _configuration.FullPaths != bFullPathPrev ||
                    _configuration.ResolveAPIsets != bResolveAPISetsPrev ||
                    _configuration.HighlightApiSet != bHighlightAPISetsPrev ||
                    _configuration.UpperCaseModuleNames != bUpperCaseModulesNamesPrev)
                {
                    UpdateFileView(FileViewUpdateAction.TreeViewAutoExpandsChange);
                    UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
                }

                if (_configuration.ViewUndecorated != bUndecoratedPrev)
                {
                    UpdateFileView(FileViewUpdateAction.FunctionsUndecorateChange);
                }

                if ((bUseApiSetSchemaFilePrev != _configuration.UseApiSetSchemaFile) ||
                    (ApiSetSchemaFilePrev != null && !ApiSetSchemaFilePrev.Equals(_configuration.ApiSetSchemaFile)))
                {
                    if (String.IsNullOrEmpty(_configuration.ApiSetSchemaFile))
                    {
                        AddLogMessage($"Apiset configuration has been changed, default system schema will be used", LogMessageType.Information);
                    }
                    else
                    {
                        AddLogMessage($"Apiset configuration has been changed, new apiset schema file {_configuration.ApiSetSchemaFile}", LogMessageType.Information);
                    }
                }

                if (_configuration.UseSymbols != bUseSymbolsPrev)
                {
                    ApplySymbolsConfiguration();
                }

                if (Math.Abs(_configuration.GuiFontSize - guiFontSizePrev) > 0.001f)
                {
                    ApplyConfiguredGuiFontSize();
                }

            }
        }

        Activate();
    }

    private void ConfigureMenuItem_Click(object sender, EventArgs e)
    {
        ShowConfigurationForm(CConsts.IdxTabMain);
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
            module = _loadedModulesList[selectedItemIndex];
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
                    if (string.IsNullOrWhiteSpace(_configuration.ExternalViewerCommand))
                    {
                        AddLogMessage("External viewer launch failed: external viewer executable is not configured.", LogMessageType.ErrorOrWarning);
                        return;
                    }

                    if (!CUtils.TryBuildExternalViewerArguments(_configuration.ExternalViewerArguments, module.FileName, out var safeArguments))
                    {
                        AddLogMessage("External viewer launch failed: invalid argument template.", LogMessageType.ErrorOrWarning);
                        return;
                    }


                    Process.Start(new ProcessStartInfo()
                    {
                        Arguments = safeArguments,
                        FileName = _configuration.ExternalViewerCommand,
                        WindowStyle = ProcessWindowStyle.Normal
                    });

                }
                catch (Exception ex)
                {
                    var message = $"External viewer launch failed for \"{module.FileName}\": {ex.Message}";
                    AddLogMessage(message, LogMessageType.ErrorOrWarning);
                    UpdateOperationStatus(message);
                }
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
                catch (Exception ex)
                {
                    var message = $"Open file location failed for \"{module.FileName}\": {ex.Message}";
                    AddLogMessage(message, LogMessageType.ErrorOrWarning);
                    UpdateOperationStatus(message);
                }
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
        if (e.KeyCode == Keys.Escape && _configuration.EscKeyEnabled)
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
        if (_focusedControl != null)
        {
            _focusedControl.Focus();
            _focusedControl = null;
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
        _focusedControl = CUtils.IsControlFocused(this.Controls);
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
                _configuration.FullPaths = toolStripButton.Checked;
                UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
                break;

            case CConsts.TagAutoExpand:
                _configuration.AutoExpands = toolStripButton.Checked;
                UpdateFileView(FileViewUpdateAction.TreeViewAutoExpandsChange);
                break;

            case CConsts.TagViewUndecorated:
                _configuration.ViewUndecorated = toolStripButton.Checked;
                UpdateFileView(FileViewUpdateAction.FunctionsUndecorateChange);
                break;

            case CConsts.TagResolveAPIsets:
                _configuration.ResolveAPIsets = toolStripButton.Checked;
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
                _configuration.FullPaths = menuItem.Checked;
                ViewFullPathsToolButton.Checked = menuItem.Checked;
                UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
                break;

            case CConsts.TagAutoExpand:
                _configuration.AutoExpands = menuItem.Checked;
                AutoExpandToolButton.Checked = menuItem.Checked;
                UpdateFileView(FileViewUpdateAction.TreeViewAutoExpandsChange);
                break;

            case CConsts.TagViewUndecorated:
                _configuration.ViewUndecorated = menuItem.Checked;
                ViewUndecoratedToolButton.Checked = menuItem.Checked;
                UpdateFileView(FileViewUpdateAction.FunctionsUndecorateChange);
                break;

            case CConsts.TagResolveAPIsets:
                _configuration.ResolveAPIsets = menuItem.Checked;
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
        moduleTreeAutoExpandMenuItem.Checked = _configuration.AutoExpands;
        moduleTreeFullPathsMenuItem.Checked = _configuration.FullPaths;
        ViewModuleSetMenuItems(true);
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
        _configuration.AutoExpands = !_configuration.AutoExpands;
        AutoExpandToolButton.Checked = _configuration.AutoExpands;
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
                _instanceStopSearch = false;
                bPrevInstanceEnabled = (null != TreeViewFindNodeInstancePrev(_rootNode, TVModules.SelectedNode, obj.FileName));

                _instanceSelfFound = false;
                _instanceStopSearch = false;
                bNextInstanceEnabled = (null != TreeViewFindNodeInstanceNext(_rootNode, TVModules.SelectedNode, obj.FileName));
            }
        }

        switch (controlName)
        {
            case CConsts.TVModulesName:
                bMatchingItemEnabled = (TVModules.Nodes.Count > 0);
                text = "Module In List";
                bOriginalInstanceEnabled = (null != (CUtils.TreeViewGetOriginalInstanceFromNode(TVModules.SelectedNode, _loadedModulesList)));
                break;

            case CConsts.LVModulesName:
                bMatchingItemEnabled = (LVModules.Items.Count > 0);
                text = "Module In Tree";
                bOriginalInstanceEnabled = (null != (CUtils.TreeViewGetOriginalInstanceFromNode(TVModules.SelectedNode, _loadedModulesList)));
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
            // Highlight Matching (Module/Function).
            //
            ContextViewHighlightMatchingItem.Enabled = bMatchingItemEnabled;
            ContextViewHighlightMatchingItem.Text = $"{baseText} {text}";

            //
            // Original Instance.
            //
            ContextOriginalInstanceItem.Enabled = bOriginalInstanceEnabled;

            //
            // Previous Instance.
            //
            ContextPreviousInstanceItem.Enabled = bPrevInstanceEnabled;

            //
            // Next Instance.
            //
            ContextNextInstanceItem.Enabled = bNextInstanceEnabled;

            //
            // Open Module Location.
            //
            ContextOpenModuleLocationItem.Enabled = bMatchingItemEnabled;

            //
            // View External (same as Highlight Matching).
            //
            ContextViewModuleInExternalViewerItem.Enabled = bMatchingItemEnabled;

            //
            // Properties.
            //
            ContextViewHighlightMatchingItem.Enabled = bMatchingItemEnabled;
        }
        else
        {
            //
            // View dropdown menu.
            //

            //
            // Highlight Matching (Module).
            //
            ViewHighlightMatchingItem.Enabled = bMatchingItemEnabled;
            ViewHighlightMatchingItem.Text = $"{baseText} {text}";

            //
            // Original Instance.
            //
            ViewOriginalInstanceItem.Enabled = bOriginalInstanceEnabled;

            //
            // Previous Instance.
            //
            ViewPreviousInstanceItem.Enabled = bPrevInstanceEnabled;

            //
            // Next Instance.
            //
            ViewNextInstanceItem.Enabled = bNextInstanceEnabled;

            //
            // Open Module Location.
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
            // Properties.
            //
            ViewPropertiesItem.Enabled = bMatchingItemEnabled;
        }

    }

    private void ViewToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
    {
        //
        // Set check state for menu items in MainMenu->View tool strip.
        //
        mainMenuAutoExpandItem.Checked = _configuration.AutoExpands;
        mainMenuFullPathsItem.Checked = _configuration.FullPaths;
        mainMenuUndecorateCFunctionsItem.Checked = _configuration.ViewUndecorated;
        mainMenuResolveApiSetsItem.Checked = _configuration.ResolveAPIsets;
        mainMenuShowToolBarItem.Checked = _configuration.ShowToolBar;
        manuMenuShowStatusBarItem.Checked = _configuration.ShowStatusBar;

        //
        // Enable/disable view instance items depending on active control.
        //
        ViewModuleSetMenuItems(false);

        var sessionOpened = _depends != null;

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
    /// Updates popup menu text and enabled state based on the currently focused view.
    /// </summary>
    /// <param name="functionView">If true, applies function-view menu text; otherwise applies module-view menu text.</param>
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
                funcSrc = _currentImportsList;
            }
            else
            {
                selectedCount = LVExports.SelectedIndices.Count;

                lvSrc = LVExports;
                lvDst = LVImports;
                funcSrc = _currentExportsList;
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
        toolStripMenuItem11.Checked = _configuration.FullPaths;
        SetPopupMenuItemText(false);
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
        _configuration.FullPaths = !_configuration.FullPaths;
        ViewFullPathsToolButton.Checked = _configuration.FullPaths;
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

        ListView[] controls = { LVExports, LVImports, LVModules };

        ListView target = null;

        foreach (var lv in controls)
        {
            if (lv.Focused)
            {
                target = lv;
                break;
            }
        }

        if (target == null) return;

        target.BeginUpdate();

        try
        {
            for (int i = 0; i < target.VirtualListSize; i++)
            {
                target.SelectedIndices.Add(i);
            }
        }
        finally { target.EndUpdate(); }
    }

    private void MenuExitItem_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void CopyFunctionNamesToClipboard(ListView listView, List<CFunction> functions)
    {
        var textBuilder = new StringBuilder();
        bool viewUndecorated = _configuration.ViewUndecorated;

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
            textBuilder.AppendLine(module.GetModuleNameRespectApiSet(_configuration.ResolveAPIsets));
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
                CopyFunctionNamesToClipboard(LVImports, _currentImportsList);
                break;

            case CConsts.LVExportsName:
                CopyFunctionNamesToClipboard(LVExports, _currentExportsList);
                break;

            case CConsts.LVModulesName:
                CopyModuleNamesToClipboard(LVModules, _loadedModulesList);
                break;

            case CConsts.TVModulesName:
                var selectedNode = TVModules.SelectedNode;
                if (selectedNode != null && selectedNode.Tag is CModule module)
                {
                    var moduleName = module.GetModuleNameRespectApiSet(_configuration.ResolveAPIsets);

                    if (!string.IsNullOrEmpty(moduleName))
                    {
                        Clipboard.Clear();
                        Clipboard.SetText(moduleName);
                    }
                }
                break;
        }
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
        _configuration.ResolveAPIsets = !_configuration.ResolveAPIsets;
        ResolveAPISetsToolButton.Checked = _configuration.ResolveAPIsets;
        UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
    }

    private void ShowSysInfoDialog(object sender, EventArgs e)
    {
        List<PropertyElement> si = [];
        var isLocal = true;

        if (_depends != null)
        {
            isLocal = !_depends.IsSavedSessionView;

            if (isLocal)
            {
                CUtils.CollectSystemInformation(si);
                _depends.SystemInformation = si;
            }
            else
            {
                si = _depends.SystemInformation;
            }
        }
        else
        {
            CUtils.CollectSystemInformation(si);
        }

        using (var sysInfoDlg = new SysInfoDialogForm(si, isLocal, this.Font.Size))
        {
            sysInfoDlg.Font = this.Font;
            sysInfoDlg.ShowDialog();
        }
    }

    private void StatusBarToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _configuration.ShowStatusBar = (sender as ToolStripMenuItem).Checked;
        StatusBar.Visible = _configuration.ShowStatusBar;
    }

    private void ToolbarToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _configuration.ShowToolBar = (sender as ToolStripMenuItem).Checked;
        MainToolBar.Visible = _configuration.ShowToolBar;
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
        var symBase = CSymbolResolver.RetrieveCachedSymModule(module.FileName);
        if (symBase == IntPtr.Zero)
        {
            UpdateOperationStatus($"Please wait, loading symbols for \"{module.FileName}\"...");
            CSymbolResolver.LoadModule(module.FileName, 0);
            UpdateOperationStatus(string.Empty);
        }
    }

    private void MainMenu_FileDropDown(object sender, EventArgs e)
    {
        bool bSessionAllocated = _depends != null;
        MenuSaveAsItem.Visible = bSessionAllocated;
        MenuSaveItem.Visible = bSessionAllocated;
        MenuCloseItem.Visible = bSessionAllocated;
    }

    /// <summary>
    /// Updates the status bar text for the current operation.
    /// </summary>
    /// <param name="status">Status text to display.</param>
    private void UpdateOperationStatus(string status)
    {
        if (_shutdownInProgress)
            return;

        if (toolBarStatusLabel.Owner == null || toolBarStatusLabel.Owner.IsDisposed)
            return;

        if (toolBarStatusLabel.Owner.InvokeRequired)
        {
            toolBarStatusLabel.Owner.BeginInvoke((MethodInvoker)delegate
            {
                if (_shutdownInProgress)
                    return;

                if (toolBarStatusLabel.Owner == null || toolBarStatusLabel.Owner.IsDisposed)
                    return;

                toolBarStatusLabel.Text = status;
                toolBarStatusLabel.Owner.Refresh();
            });
        }
        else
        {
            toolBarStatusLabel.Text = status;
            toolBarStatusLabel.Owner.Refresh();
        }
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
        _configuration.UseSymbols = symStateChangeMenuItem.Checked;
        ApplySymbolsConfiguration();
    }

    private void StatusBarPopupMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        symStateChangeMenuItem.Checked = _configuration.UseSymbols;
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        _shutdownInProgress = true;

        if (_findDialog != null && !_findDialog.IsDisposed)
        {
            _findDialog.Close();
            _findDialog.Dispose();
        }

        // Remember window position, size and state.
        Rectangle bounds = this.WindowState == FormWindowState.Normal
            ? this.Bounds
            : this.RestoreBounds;

        if (bounds.Width >= CConsts.MinValidWidth && bounds.Height >= CConsts.MinValidHeight)
        {
            _configuration.WindowLeft = bounds.Left;
            _configuration.WindowTop = bounds.Top;
            _configuration.WindowWidth = bounds.Width;
            _configuration.WindowHeight = bounds.Height;
        }

        RememberAllColumnWidths();

        FormWindowState state = (this.WindowState == FormWindowState.Minimized)
            ? FormWindowState.Normal
            : this.WindowState;

        _configuration.WindowState = (int)state;
    }

    private static List<int> CaptureColumnWidths(ListView listView)
    {
        return [.. listView.Columns.Cast<ColumnHeader>().Select(ch => ch.Width)];
    }

    private static void ApplyColumnWidths(ListView listView, List<int>? widths)
    {
        if (widths == null)
            return;

        int max = Math.Min(listView.Columns.Count, widths.Count);
        for (int i = 0; i < max; i++)
        {
            if (widths[i] > 0)
            {
                listView.Columns[i].Width = widths[i];
            }
        }
    }

    private void EnsureColumnWidthCollections()
    {
        _configuration.ModulesColumnWidths ??= [];
        _configuration.ImportsColumnWidths ??= [];
        _configuration.ExportsColumnWidths ??= [];

        if (_configuration.ModulesColumnWidths.Count != LVModules.Columns.Count)
            _configuration.ModulesColumnWidths = CaptureColumnWidths(LVModules);
        if (_configuration.ImportsColumnWidths.Count != LVImports.Columns.Count)
            _configuration.ImportsColumnWidths = CaptureColumnWidths(LVImports);
        if (_configuration.ExportsColumnWidths.Count != LVExports.Columns.Count)
            _configuration.ExportsColumnWidths = CaptureColumnWidths(LVExports);
    }

    private void RestoreColumnWidthsFromConfiguration()
    {
        EnsureColumnWidthCollections();
        ApplyColumnWidths(LVModules, _configuration.ModulesColumnWidths);
        ApplyColumnWidths(LVImports, _configuration.ImportsColumnWidths);
        ApplyColumnWidths(LVExports, _configuration.ExportsColumnWidths);
    }

    private void RememberAllColumnWidths()
    {
        _configuration.ModulesColumnWidths = CaptureColumnWidths(LVModules);
        _configuration.ImportsColumnWidths = CaptureColumnWidths(LVImports);
        _configuration.ExportsColumnWidths = CaptureColumnWidths(LVExports);
    }

    private static bool IsAllowedGuiFontSize(float value)
    {
        return CConsts.AvailableGuiFontSizes.Any(size => Math.Abs(size - value) < 0.001f);
    }

    /// <summary>
    /// Set new font to GUI components (RichEdit special handling, main menu and status bar).
    /// </summary>
    private void ApplyConfiguredGuiFontSize()
    {
        if (!IsAllowedGuiFontSize(_configuration.GuiFontSize))
        {
            _configuration.GuiFontSize = CConsts.DefaultGuiFontSize;
        }

        var current = this.Font;
        var newFont = new Font(current.FontFamily, _configuration.GuiFontSize, current.Style, GraphicsUnit.Point);
        this.Font = newFont;

        // Set zoom factor for richedit.
        reLog.ZoomFactor = _configuration.GuiFontSize / CConsts.DefaultGuiFontSize;

        // Set new font size to main menu.
        mainMenu.Font = newFont;

        // Change popup menu fonts.
        moduleTreePopupMenu.Font = newFont;
        functionPopupMenu.Font = newFont;
        richEditPopupMenu.Font = newFont;
        moduleViewPopupMenu.Font = newFont;

        // Update status bar and pepaint default message.
        StatusBar.Font = newFont;
        SetDefaultStatusBarText();
    }

    private void LVModules_ColumnWidthChanged(object? sender, ColumnWidthChangedEventArgs e)
    {
        _configuration.ModulesColumnWidths = CaptureColumnWidths(LVModules);
    }

    private void LVImports_ColumnWidthChanged(object? sender, ColumnWidthChangedEventArgs e)
    {
        _configuration.ImportsColumnWidths = CaptureColumnWidths(LVImports);
    }

    private void LVExports_ColumnWidthChanged(object? sender, ColumnWidthChangedEventArgs e)
    {
        _configuration.ExportsColumnWidths = CaptureColumnWidths(LVExports);
    }

    private void MainForm_DpiChanged(object sender, DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        PerformLayout();
    }

    private void ToolBarTheme_Click(object sender, EventArgs e)
    {
        ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
        int tag = Convert.ToInt32(menuItem.Tag);

        if (tag == CConsts.TagTbUseClassic)
        {
            _configuration.ToolBarTheme = ToolBarThemeType.Classic;
            mainMenuModernToolbar.Checked = false;
        }
        else if (tag == CConsts.TagTbUseModern)
        {
            _configuration.ToolBarTheme = ToolBarThemeType.Modern;
            mainMenuClassicToolbar.Checked = false;
        }

        CreateOrUpdateToolbarImageStrip();
    }

    private void MainMenuToolBarTheme_OnDropDownOpening(object sender, EventArgs e)
    {
        mainMenuClassicToolbar.Checked = _configuration.ToolBarTheme == ToolBarThemeType.Classic;
        mainMenuModernToolbar.Checked = _configuration.ToolBarTheme == ToolBarThemeType.Modern;
    }

    private void CleanupHintForms()
    {
        if (_disposingHintForms)
            return;

        _disposingHintForms = true;

        try
        {
            // Close and dispose hint forms
            if (_functionsHintForm != null)
            {
                if (!_functionsHintForm.IsDisposed)
                {
                    _functionsHintForm.Close();
                    _functionsHintForm.Dispose();
                }
                _functionsHintForm = null;
            }

            if (_modulesHintForm != null)
            {
                if (!_modulesHintForm.IsDisposed)
                {
                    _modulesHintForm.Close();
                    _modulesHintForm.Dispose();
                }
                _modulesHintForm = null;
            }
        }
        finally
        {
            _disposingHintForms = false;
        }
    }

    /// <summary>
    /// Finds module link information for the specified character index.
    /// </summary>
    /// <param name="charIndex">Character index in the log.</param>
    /// <returns>The matching module link information, or null if no link contains the index.</returns>
    private ModuleLinkInfo FindModuleLinkAtPosition(int charIndex)
    {
        int left;
        int right;
        int middle;
        ModuleLinkInfo link;

        left = 0;
        right = _moduleLinks.Count - 1;

        while (left <= right)
        {
            middle = left + ((right - left) / 2);
            link = _moduleLinks[middle];

            if (charIndex < link.Start)
            {
                right = middle - 1;
            }
            else if (charIndex >= link.End)
            {
                left = middle + 1;
            }
            else
            {
                return link;
            }
        }

        return null;
    }

}
