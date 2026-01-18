/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       MAINFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        08 Jan 2026
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

    private readonly ToolTip moduleToolTip = new ToolTip();
    private readonly Dictionary<(int Start, int Length), int> moduleLinks = new Dictionary<(int Start, int Length), int>();

    public MainForm()
    {
        InitializeComponent();

        _shutdownInProgress = false;

        //
        // Add welcome message to the log.
        //
        AddLogMessage($"{CConsts.ProgramName} started, " +
            $"version {CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild} BETA",
            LogMessageType.ContentDefined, Color.Black, true);

        _configuration = CConfigManager.LoadConfiguration();
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

    private static string FormatByteSize(UInt64 bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024 * 1024)} MB",
            >= 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes} byte"
        };
    }

    private void LogModuleStats(CCoreCallStats stats, string moduleFileName)
    {
        if (stats == null)
            return;

        var statsData = $"[STATS {Path.GetFileName(moduleFileName)}] Received: {FormatByteSize(stats.TotalBytesSent)}, " +
                        $"\"send\" calls: {stats.TotalSendCalls}, \"send\" time spent (\u00B5s): {stats.TotalTimeSpent}";
        AddLogMessage(statsData, LogMessageType.ContentDefined, Color.Purple, true, false);
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

    private void HandleModuleOpenStatus(CModule module, ModuleOpenStatus openStatus, CFileOpenSettings settings, bool currentModuleIsRoot)
    {
        switch (openStatus)
        {
            case ModuleOpenStatus.Okay:

                module.IsProcessed = _coreClient.GetModuleHeadersInformation(module);

                //
                // If this is root module, setup resolver.
                //
                if (currentModuleIsRoot)
                {
                    CPathResolver.QueryFileInformation(module);
                }

                _coreClient.GetModuleImportExportInformation(module,
                    _configuration.SearchOrderListUM,
                    _configuration.SearchOrderListKM,
                    _parentImportsHashTable,
                    settings.EnableExperimentalFeatures,
                    settings.ExpandForwarders);

                //
                // Collect forwarders if exists.
                // Has local settings priority over global.
                //
                if (settings.ExpandForwarders)
                {
                    _coreClient.ExpandAllForwarderModules(module, _configuration.SearchOrderListUM,
                        _configuration.SearchOrderListKM,
                        _parentImportsHashTable);

                    // Validate forwarded exports after expansion
                    _coreClient.ValidateForwardedExports(module);
                }

                CCoreCallStats stats = null;
                if (settings.UseStats)
                {
                    stats = _coreClient.GetCoreCallStats();
                }

                _coreClient.CloseModule();

                //
                // Display statistics.
                //
                if (settings.UseStats && stats != null)
                {
                    LogModuleStats(stats, module.FileName);
                }

                if (module.ExportContainErrors)
                {
                    AddLogMessage($"Module \"{module.FileName}\" contain export errors.",
                        LogMessageType.ErrorOrWarning, null, true, true, module);
                }

                // Add warning for modules with forwarding issues
                if (module.OtherErrorsPresent && module.ForwarderEntries.Count > 0)
                {
                    AddLogMessage($"Module \"{Path.GetFileName(module.FileName)}\" has unresolved forwarded exports.",
                        LogMessageType.ErrorOrWarning, null, true, true, module);
                }

                bool isCpuMismatch = IsCpuMismatchForDisplay(module, _depends.RootModule);

                if (isCpuMismatch)
                {
                    module.OtherErrorsPresent = true;
                    AddLogMessage($"Module \"{module.FileName}\" with different CPU type was found.",
                        LogMessageType.ErrorOrWarning, null, true, true, module);
                }

                // Skip this message for kernel modules, dotnet files, and when relocation processing is disabled
                if (module.ModuleData.ImageFixed != 0 &&
                    !module.IsKernelModule &&
                    module.ModuleData.ImageDotNet != 1 &&
                    settings.ProcessRelocsForImage)  // Only warn if relocation processing was requested
                {
                    module.OtherErrorsPresent = true;
                    AddLogMessage($"Module \"{Path.GetFileName(module.FileName)}\" has no relocations.",
                        LogMessageType.ErrorOrWarning, null, true, true, module);
                }

                if (!module.IsProcessed)
                {
                    AddLogMessage($"Module \"{module.FileName}\" was not fully processed.",
                        LogMessageType.ErrorOrWarning,
                        null, true, true, module);
                }
                break;

            case ModuleOpenStatus.ErrorUnspecified:
                AddLogMessage($"Module \"{module.FileName}\" analysis failed.", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorSendCommand:
                AddLogMessage($"Send command has failed for module \"{module.FileName}\".", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorReceivedDataInvalid:
                AddLogMessage($"Received invalid data for module \"{module.FileName}\".", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorFileNotMapped:
                AddLogMessage($"Server failed to map input module \"{module.FileName}\".", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorCannotReadFileHeaders:
                AddLogMessage($"Server failed to read headers of module \"{module.FileName}\".", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorInvalidHeadersOrSignatures:
                if (module.IsDelayLoad)
                {
                    AddLogMessage($"Delay-load module \"{module.FileName}\" has invalid headers or signatures.", LogMessageType.ErrorOrWarning,
                        null, true, true, module);
                }
                else
                {
                    AddLogMessage($"Module \"{module.FileName}\" has invalid headers or signatures.", LogMessageType.ErrorOrWarning,
                        null, true, true, module);
                }
                break;

            case ModuleOpenStatus.ErrorFileNotFound:

                // In case if this is ApiSets failure.
                // API-* are mandatory to load, while EXT-* are not.
                bool bExtApiSet = module.IsApiSetContract && module.RawFileName.StartsWith("EXT-", StringComparison.OrdinalIgnoreCase);

                string messageText;
                LogMessageType messageType = bExtApiSet ? LogMessageType.Information : LogMessageType.ErrorOrWarning;

                if (module.IsDelayLoad)
                {
                    if (bExtApiSet)
                    {
                        messageText = $"Delay-load extension apiset module \"{module.FileName}\" was not found.";
                    }
                    else
                    {
                        messageText = $"Delay-load dependency module \"{module.FileName}\" was not found.";
                    }
                }
                else
                {
                    if (bExtApiSet)
                    {
                        messageText = $"Extension apiset  module \"{module.FileName}\" was not found.";
                    }
                    else
                    {
                        messageText = $"Required implicit or forwarded dependency \"{module.FileName}\" was not found.";
                    }
                }

                AddLogMessage(messageText, messageType, null, true, true, module);
                break;
        }
    }

    /// <summary>
    /// Validates if the module can be added based on tree depth settings
    /// </summary>
    /// <param name="parentNode">The parent node to check depth against</param>
    /// <param name="maxDepth">The maximum allowed depth</param>
    /// <returns>True if within depth limit, false otherwise</returns>
    private bool ValidateTreeDepth(TreeNode parentNode, int maxDepth)
    {
        if (parentNode == null)
            return true; // Root node is always valid

        if (parentNode.Tag is CModule parentModule)
        {
            return parentModule.Depth <= maxDepth;
        }

        return true;
    }

    /// <summary>
    /// Builds a display name for a module from the given raw name
    /// </summary>
    /// <param name="rawName">The original path or module name</param>
    /// <param name="fullPaths">If true, returns rawName directly. If false processes the path to extract the last segment.</param>
    /// <returns></returns>
    private static string BuildModuleDisplayName(string rawName, bool fullPaths)
    {
        if (string.IsNullOrEmpty(rawName) || fullPaths)
            return rawName;

        string normalized = rawName.Replace('/', '\\');
        if (string.IsNullOrEmpty(normalized))
            return rawName;

        // Preserve roots before any trimming/normalization that could drop the last separator.
        // Drive root: C:\
        // UNC share root: \\server\share\
        if (IsDriveRootPath(normalized) || IsUncShareRootPath(normalized))
            return normalized;

        normalized = normalized.TrimEnd('\\');
        if (string.IsNullOrEmpty(normalized))
            return rawName;

        // Handle extended UNC: \\?\UNC\server\share\... 
        if (normalized.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(8);
        }
        // Handle \\?\ prefix
        else if (normalized.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(4);
        }

        int lastSep = normalized.LastIndexOf('\\');
        return lastSep >= 0 ? normalized.Substring(lastSep + 1) : normalized;
    }

    private static bool IsDriveRootPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // C:\
        if (path.Length == 3 &&
            char.IsLetter(path[0]) &&
            path[1] == ':' &&
            path[2] == '\\')
        {
            return true;
        }

        // \\?\C:\
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) &&
            path.Length == 7 &&
            char.IsLetter(path[4]) &&
            path[5] == ':' &&
            path[6] == '\\')
        {
            return true;
        }

        return false;
    }

    private static bool IsUncShareRootPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalized = path;

        // \\?\UNC\server\share\ => \\server\share\
        if (normalized.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            normalized = @"\\" + normalized.Substring(8);

        // \\?\UNC\ already handled above; handle generic \\?\ prefix
        if (normalized.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(4);

        if (!normalized.StartsWith(@"\\", StringComparison.Ordinal))
            return false;

        // Strip trailing slashes but keep path with two segments: \\server\share
        normalized = normalized.TrimEnd('\\');

        int firstSep = normalized.IndexOf('\\', 2);
        if (firstSep < 0)
            return false;

        int secondSep = normalized.IndexOf('\\', firstSep + 1);
        return secondSep < 0;
    }

    /// <summary>
    /// AddModuleEntry core implementation. Shared between normal and session files.
    /// </summary>
    /// <param name="module">The module to add</param>
    /// <param name="parentNode">Parent node, or null for root level</param>
    /// <param name="maxDepth">Maximum allowed node depth</param>
    /// <param name="moduleProcessor">Optional processing to perform on new modules</param>
    /// <returns>Created TreeNode or null if validation fails</returns>
    private TreeNode AddModuleEntryCore(
        CModule module,
        TreeNode parentNode,
        int maxDepth,
        Action<CModule> moduleProcessor = null)
    {
        // 1. Validate tree depth
        if (!ValidateTreeDepth(parentNode, maxDepth))
            return null;

        // 2. Check if module already exists
        bool isNewModule = true;
        CModule origInstance = CUtils.GetModuleByHash(module.FileName, _loadedModulesList);

        if (origInstance != null)
        {
            isNewModule = false;
            module.OriginalInstanceId = origInstance.InstanceId;
            module.FileNotFound = origInstance.FileNotFound;
            module.ExportContainErrors = origInstance.ExportContainErrors;
            module.IsInvalid = origInstance.IsInvalid;
            module.OtherErrorsPresent = origInstance.OtherErrorsPresent;
            module.IsDotNetModule = origInstance.IsDotNetModule;
            module.ModuleData = new(origInstance.ModuleData);

            // Propagate errors from duplicate to parent if this is not root
            if (parentNode?.Tag is CModule parent)
            {
                // Only propagate genuine errors, not from apiset contracts or stopped nodes
                bool shouldPropagate = origInstance.ExportContainErrors ||
                                       origInstance.OtherErrorsPresent ||
                                       origInstance.FileNotFound;

                // Don't propagate from apiset contracts
                if (origInstance.IsApiSetContract)
                    shouldPropagate = false;

                // Don't propagate from stopped/duplicate nodes that have forwarders
                // (these are expected to have "unprocessed" forwarders)
                if (shouldPropagate)
                {
                    bool isStoppedNode = (origInstance.Dependents == null || origInstance.Dependents.Count == 0) &&
                                         (origInstance.ForwarderEntries != null && origInstance.ForwarderEntries.Count > 0);
                    if (isStoppedNode)
                        shouldPropagate = false;
                }

                if (shouldPropagate)
                {
                    parent.OtherErrorsPresent = true;
                    parent.ModuleImageIndex = parent.GetIconIndexForModule();
                    parentNode.ImageIndex = parent.ModuleImageIndex;
                    parentNode.SelectedImageIndex = parent.ModuleImageIndex;
                }
            }
        }

        // 3. Run custom processing if this is a new module
        if (isNewModule && moduleProcessor != null)
        {
            moduleProcessor(module);
        }

        // 4. Format display name
        string moduleDisplayName = BuildModuleDisplayName(module.GetModuleNameRespectApiSet(_configuration.ResolveAPIsets), _configuration.FullPaths);

        if (_configuration.UpperCaseModuleNames)
        {
            moduleDisplayName = moduleDisplayName.ToUpperInvariant();
        }

        // Mark forward user as red if there is no forward module.
        if (module.IsForward && module.FileNotFound && parentNode?.Tag is CModule parentMod)
        {
            parentMod.OtherErrorsPresent = true;
            parentMod.ModuleImageIndex = parentMod.GetIconIndexForModule();
            parentNode.ImageIndex = parentMod.ModuleImageIndex;
            parentNode.SelectedImageIndex = parentMod.ModuleImageIndex;
        }

        // 5. Create the tree node
        module.ModuleImageIndex = module.GetIconIndexForModule();
        TreeNode tvNode = new(moduleDisplayName)
        {
            Tag = module,
            ImageIndex = module.ModuleImageIndex,
            SelectedImageIndex = module.ModuleImageIndex,
            ForeColor = (module.IsApiSetContract && _configuration.HighlightApiSet) ? Color.Blue : Color.Black
        };

        // 6. Add to tree in correct location
        if (parentNode != null)
        {
            if (parentNode.Tag is CModule parentModule)
            {
                module.Depth = parentModule.Depth + 1;
            }
            parentNode?.Nodes?.Add(tvNode);
        }
        else
        {
            TVModules.Nodes?.Add(tvNode);
        }

        // 7. Update module collections if new
        if (isNewModule)
        {
            _loadedModulesList.Add(module);
        }

        return tvNode;
    }

    /// <summary>
    /// Insert module entry to TVModules treeview.
    /// </summary>
    /// <returns>Tree node.</returns>
    private TreeNode AddModuleEntry(CModule module, CFileOpenSettings fileOpenSettings, TreeNode parentNode = null)
    {
        bool isRootModule = (parentNode == null);

        // Define action processor (callback)
        Action<CModule> processModule = (mod) =>
        {
            var effectiveSettings = new CFileOpenSettings(fileOpenSettings);

            // If this is a dependency and propagation is disabled, reset to defaults
            if (!isRootModule && !fileOpenSettings.PropagateSettingsOnDependencies)
            {
                effectiveSettings.ProcessRelocsForImage = false;
                effectiveSettings.UseStats = false;
                effectiveSettings.UseCustomImageBase = false;
                effectiveSettings.CustomImageBase = 0;
            }

            // Open and process module
            mod.InstanceId = mod.GetHashCode();
            ModuleOpenStatus openStatus = _coreClient.OpenModule(
                ref mod,
                effectiveSettings);

            HandleModuleOpenStatus(mod, openStatus, effectiveSettings, isRootModule);

            // Set module icon index
            mod.ModuleImageIndex = mod.GetIconIndexForModule();
        };

        // Use shared implementation with our specific processor
        return AddModuleEntryCore(
            module,
            parentNode,
            _configuration.ModuleNodeDepthMax,
            processModule);
    }

    /// <summary>
    /// Adds module entry from loaded session object (saved session view).
    /// </summary>
    /// <param name="module">Module entry to be added.</param>
    /// <param name="parentNode">Parent node if present.</param>
    /// <returns>Tree node entry.</returns>
    private TreeNode AddSessionModuleEntry(CModule module, TreeNode parentNode = null)
    {
        // Just use the shared implementation without any specific processor set
        return AddModuleEntryCore(
            module,
            parentNode,
            _depends.SessionNodeMaxDepth);
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

    private void ResetDisplayCache(DisplayCacheType cacheType)
    {
        switch (cacheType)
        {
            case DisplayCacheType.Imports:
                Array.Clear(LVImportsCache, 0, LVImportsCache.Length);
                Array.Resize(ref LVImportsCache, 0);
                LVImportsFirstItem = 0;
                break;
            case DisplayCacheType.Exports:
                Array.Clear(LVExportsCache, 0, LVExportsCache.Length);
                Array.Resize(ref LVExportsCache, 0);
                LVExportsFirstItem = 0;
                break;
            case DisplayCacheType.Modules:
                Array.Clear(LVModulesCache, 0, LVModulesCache.Length);
                Array.Resize(ref LVModulesCache, 0);
                LVModulesFirstItem = 0;
                break;
        }
    }

    private void ResetFunctionLists()
    {
        ResetDisplayCache(DisplayCacheType.Imports);
        ResetDisplayCache(DisplayCacheType.Exports);
        LVImports.VirtualListSize = LVExports.VirtualListSize = 0;
        LVImports.Invalidate();
        LVExports.Invalidate();
    }

    public void ResetModulesList()
    {
        ResetDisplayCache(DisplayCacheType.Modules);
        LVModules.VirtualListSize = 0;
        _loadedModulesList.Clear();
        LVModules.Invalidate();
    }
    private void ClearModuleLinks()
    {
        moduleLinks.Clear();
    }

    private void RichEditLog_ClearLog()
    {
        if (!reLog.IsDisposed)
        {
            reLog.Clear();
            ClearModuleLinks(); // Clear the links when log is cleared
        }
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

        //
        // Force garbage collection.
        //
        //GC.Collect();
    }

    /// <summary>
    /// Updates tree nodes respecting module display settings using non-recursive iteration.
    /// </summary>
    /// <param name="startNode"></param>
    private void TreeViewUpdateNode(TreeNode startNode)
    {
        if (startNode == null)
            return;

        Stack<TreeNode> nodeStack = new(Math.Min(TVModules.GetNodeCount(true), CConsts.MaxTreeNodeStackSize));
        nodeStack.Push(startNode);

        bool fullPaths = _configuration.FullPaths;
        bool upperCase = _configuration.UpperCaseModuleNames;
        bool highlightApiSet = _configuration.HighlightApiSet;
        bool resolveApiSets = _configuration.ResolveAPIsets;

        while (nodeStack.Count > 0)
        {
            TreeNode node = nodeStack.Pop();
            if (node == null) continue;

            if (node.Tag is CModule module)
            {
                string displayName = module.GetModuleNameRespectApiSet(resolveApiSets);

                displayName = BuildModuleDisplayName(displayName, fullPaths);

                if (upperCase && !string.IsNullOrEmpty(displayName))
                    displayName = displayName.ToUpperInvariant();

                node.Text = displayName;
                node.ForeColor = (module.IsApiSetContract && highlightApiSet) ? Color.Blue : Color.Black;
            }

            if (node.Nodes.Count > 0)
                nodeStack.Push(node.Nodes[0]);

            if (node.NextNode != null)
                nodeStack.Push(node.NextNode);
        }
    }

    public void UpdateItemsView(ListView listView, DisplayCacheType cacheType)
    {
        listView.BeginUpdate();
        try
        {
            ResetDisplayCache(cacheType);
            listView.Invalidate();
        }
        finally { listView.EndUpdate(); }
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

    void ExpandAllModulesWithUpdate()
    {
        TVModules.BeginUpdate();
        try
        {
            TVModules.ExpandAll();
        }
        finally { TVModules.EndUpdate(); }
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
            toolBarStatusLabel.Text = "Use the menu File->Open or drag and drop a file into the window to begin analysis";
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
        }
        finally
        {
            _coreClient?.Dispose();
        }
    }

    /// <summary>
    /// Adds message to the log.
    /// </summary>
    /// <param name="message">Text message to add to the log</param>
    /// <param name="messageType">Type of the message</param>
    /// <param name="color">Optional custom color</param>
    /// <param name="useBold">Whether to use bold font</param>
    /// <param name="moduleMessage">Whether this is a module-related message</param>
    /// <param name="relatedModule">The module related to this message to create a clickable link</param>
    public void AddLogMessage(string message, LogMessageType messageType,
        Color? color = null, bool useBold = false, bool moduleMessage = false, CModule relatedModule = null)
    {
        Color outputColor = Color.Black;
        bool boldText = false;

        if (_shutdownInProgress)
            return;

        switch (messageType)
        {
            case LogMessageType.ErrorOrWarning:
                boldText = true;
                outputColor = Color.Red;
                break;

            case LogMessageType.Information:
                boldText = true;
                break;

            case LogMessageType.System:
                boldText = true;
                outputColor = Color.Blue;
                break;

            case LogMessageType.ContentDefined:
                boldText = useBold;
                outputColor = color ?? Color.Black;
                break;

            case LogMessageType.Normal:
            default:
                break;
        }

        if (moduleMessage)
        {
            _depends.ModuleAnalysisLog.Add(new LogEntry(message, outputColor));
        }

        if (!reLog.IsDisposed)
        {
            reLog.SuspendLayout();

            int startPosition = reLog.TextLength;
            reLog.AppendText(message + Environment.NewLine);

            // First apply the overall style to the entire message
            reLog.Select(startPosition, reLog.TextLength - startPosition);
            reLog.SelectionColor = outputColor;

            if (boldText)
            {
                reLog.SelectionFont = new Font(reLog.Font, FontStyle.Bold);
            }

            if (relatedModule != null)
            {
                string justFileName = Path.GetFileName(relatedModule.FileName);

                string[] patternsToCheck = {
                $"\"{justFileName}\"",     // "filename.dll"
                $"{justFileName}"          // filename.dll
            };

                foreach (var pattern in patternsToCheck)
                {
                    int moduleNameIndex = message.IndexOf(pattern);
                    if (moduleNameIndex >= 0)
                    {
                        // Calculate the exact position in the RichTextBox
                        int linkStart = startPosition + moduleNameIndex;
                        int linkLength = pattern.Length;

                        // Apply ONLY underline to the module name, preserving the color
                        reLog.Select(linkStart, linkLength);
                        Font currentFont = reLog.SelectionFont;
                        reLog.SelectionFont = new Font(
                            currentFont.FontFamily,
                            currentFont.Size,
                            currentFont.Style | FontStyle.Underline);

                        // Store the link information
                        moduleLinks[(linkStart, linkLength)] = relatedModule.InstanceId;
                        break;
                    }
                }
            }

            reLog.SelectionLength = 0;
            reLog.ScrollToCaret();
            reLog.ResumeLayout();
        }
    }

    private void PostOpenFileUpdateControls(string fileName)
    {
        var suffix = CUtils.IsAdministrator
            ? Environment.Is64BitProcess ? CConsts.Admin64Msg : CConsts.AdminMsg
            : Environment.Is64BitProcess ? CConsts.SixtyFourBitsMsg : "";

        _mruList.AddFile(fileName);
        this.Text = $"{CConsts.ProgramName}{suffix} [{Path.GetFileName(fileName)}]";
    }

    private void MenuCloseItem_Click(object sender, EventArgs e)
    {
        if (_depends != null)
        {
            CloseInputFile();
        }
    }

    /// <summary>
    /// Disposes resources allocated for currently opened file and resets output controls.
    /// </summary>
    private void CloseInputFile()
    {
        if (_depends != null)
        {
            var programTitle = CUtils.IsAdministrator
           ? Environment.Is64BitProcess ? CConsts.Admin64Msg : CConsts.AdminMsg
           : Environment.Is64BitProcess ? CConsts.SixtyFourBitsMsg : "";

            this.Text = $"{CConsts.ProgramName}{programTitle}";

            _depends = null;
            _rootNode = null;
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
            if (_configuration.ClearLogOnFileOpen)
            {
                RichEditLog_ClearLog();
            }
            bResult = OpenSessionFile(fileName);
        }
        else
        {
            //
            // Display file open settings dialog depending on global settings.
            //
            CFileOpenSettings fileOpenSettings = new(_configuration);

            if (!fileOpenSettings.UseAsDefault)
            {
                using (FileOpenForm fileOpenForm = new(_configuration.EscKeyEnabled, fileOpenSettings, fileName))
                {
                    //
                    // Update global settings if use as default is checked.
                    //
                    if (fileOpenForm.ShowDialog() == DialogResult.OK)
                    {
                        if (fileOpenSettings.UseAsDefault)
                        {
                            _configuration.UseStats = fileOpenSettings.UseStats;
                            _configuration.ProcessRelocsForImage = fileOpenSettings.ProcessRelocsForImage;
                            _configuration.UseCustomImageBase = fileOpenSettings.UseCustomImageBase;
                            _configuration.CustomImageBase = fileOpenSettings.CustomImageBase;
                            _configuration.PropagateSettingsOnDependencies = fileOpenSettings.PropagateSettingsOnDependencies;
                            _configuration.ExpandForwarders = fileOpenSettings.ExpandForwarders;
                            _configuration.EnableExperimentalFeatures = fileOpenSettings.EnableExperimentalFeatures;
                            _configuration.AnalysisSettingsUseAsDefault = true;
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

                Activate();
            }

            CloseInputFile();

            AddLogMessage($"Openning \"{fileName}\" for analysis.", LogMessageType.Information);

            if (_configuration.ClearLogOnFileOpen)
            {
                RichEditLog_ClearLog();
            }

            _parentImportsHashTable.Clear();

            // Create new root object and output it submodules.
            _depends = new(fileName)
            {
                // Remember node max depth value.
                SessionNodeMaxDepth = _configuration.ModuleNodeDepthMax
            };

            if (_depends.RootModule != null)
            {
                CPathResolver.Initialized = false;
                using (CActCtxHelper sxsHelper = new(fileName))
                {
                    CPathResolver.ActCtxHelper = sxsHelper;

                    TVModules.BeginUpdate();
                    try
                    {
                        PopulateObjectToLists(_depends.RootModule, false, fileOpenSettings);
                        _rootNode?.Expand();
                    }
                    finally { TVModules.EndUpdate(); }

                    LVModules.BeginUpdate();
                    try
                    {
                        LVModules.VirtualListSize = _loadedModulesList.Count;
                        LVModulesSort(LVModules, _configuration.SortColumnModules,
                           LVModulesSortOrder, _loadedModulesList, DisplayCacheType.Modules);

                    }
                    finally { LVModules.EndUpdate(); }

                    bResult = true;
                }//CActCtxHelper
            }

        }

        if (bResult)
        {
            PostOpenFileUpdateControls(fileName);

            if (_configuration.AutoExpands)
            {
                ExpandAllModulesWithUpdate();
            }
        }

        SaveToolButton.Enabled = bResult;
        CopyToolButton.Enabled = bResult;

        if (TVModules.Nodes.Count > 0)
        {
            TVModules.SelectedNode = TVModules.Nodes[0];
        }

        if (bResult && bSessionFile)
            return FileOpenResult.SuccessSession;

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

            string logEvent;
            LogMessageType messageType;

            switch (fileOpenResult)
            {
                case FileOpenResult.SuccessSession:
                    logEvent = $"Session file \"{resolvedFileName}\" has been opened.";
                    messageType = LogMessageType.System;
                    break;
                case FileOpenResult.Success:
                    logEvent = $"Analysis of \"{resolvedFileName}\" has been completed.";
                    messageType = LogMessageType.Information;
                    break;
                case FileOpenResult.Failure:
                    logEvent = $"There is an error while processing \"{resolvedFileName}\" file.";
                    messageType = LogMessageType.ErrorOrWarning;
                    break;
                case FileOpenResult.Cancelled:
                default:
                    logEvent = "Operation has been cancelled.";
                    messageType = LogMessageType.Normal;
                    break;
            }

            AddLogMessage(logEvent, messageType);
            UpdateOperationStatus(logEvent);
        }
        catch
        {
            var message = $"There is an error while processing \"{fileName}\" file.";
            AddLogMessage(message, LogMessageType.ErrorOrWarning);
            UpdateOperationStatus(message);
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

        _depends = LoadSessionObjectFromFile(fileName, _configuration.CompressSessionFiles);
        if (_depends == null)
        {
            // Deserialization failed, leaving.
            return false;
        }

        AddLogMessage($"Openning session file \"{fileName}\"", LogMessageType.System);

        if (_depends.RootModule != null)
        {
            // Insert tree modules from session file.
            TVModules.BeginUpdate();
            try
            {
                PopulateObjectToLists(_depends.RootModule, true, null);
                // Expand root module.
                _rootNode?.Expand();
            }
            finally { TVModules.EndUpdate(); }

            // Insert list modules from session file.
            LVModules.BeginUpdate();
            try
            {
                LVModules.VirtualListSize = _loadedModulesList.Count;
                LVModulesSort(LVModules, _configuration.SortColumnModules,
                    LVModulesSortOrder, _loadedModulesList, DisplayCacheType.Modules);
            }
            finally { LVModules.EndUpdate(); }

            // Restore important module related warnings/errors in the log.
            foreach (var entry in _depends.ModuleAnalysisLog)
            {
                AddLogMessage(entry.LoggedMessage, LogMessageType.ContentDefined,
                    entry.EntryColor, true, false);
            }

        }
        else
        {
            // Corrupted file, leaving.
            _depends = null;
            _rootNode = null;
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
            _rootNode = AddSessionModuleEntry(module, null);

            // Add root session module dependencies.
            foreach (var importModule in module.Dependents)
            {
                UpdateOperationStatus($"Populating {importModule.FileName}");
                baseNodes.Add(AddSessionModuleEntry(importModule, _rootNode));
            }
        }
        else
        {
            // Add root module.
            _rootNode = AddModuleEntry(module, fileOpenSettings, null);

            // Add root module dependencies.
            foreach (var importModule in module.Dependents)
            {
                UpdateOperationStatus($"Populating {importModule.FileName}");
                baseNodes.Add(AddModuleEntry(importModule, fileOpenSettings, _rootNode));
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
        using (AboutForm aboutForm = new(_configuration.EscKeyEnabled))
        {
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

        using (ConfigurationForm configForm = new(currentFileName,
                                                  is64bitFile,
                                                  optConfig,
                                                  _coreClient,
                                                  pageIndex))
        {
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

                if (bUseApiSetSchemaFilePrev != _configuration.UseApiSetSchemaFile ||
                    !ApiSetSchemaFilePrev.Equals(_configuration.ApiSetSchemaFile))
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

            }
        }

        Activate();
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
                if (selectedItemIndex < _currentImportsList.Count)
                {
                    function = _currentImportsList[selectedItemIndex];
                }
            }
            else if (LVExports.Focused && LVExports.SelectedIndices.Count > 0)
            {
                selectedItemIndex = LVExports.SelectedIndices[0];
                if (selectedItemIndex < _currentExportsList.Count)
                {
                    function = _currentExportsList[selectedItemIndex];
                }
            }

            if (function != null)
            {
                fName = function.RawName;
                Process.Start(new ProcessStartInfo()
                {
                    FileName = new StringBuilder(_configuration.ExternalFunctionHelpURL).Replace("%1", fName).ToString(),
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
                    Process.Start(new ProcessStartInfo()
                    {
                        Arguments = new StringBuilder(_configuration.ExternalViewerArguments).Replace("%1", module.FileName).ToString(),
                        FileName = _configuration.ExternalViewerCommand,
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
        undecorateFunctionToolStripMenuItem.Checked = _configuration.ViewUndecorated;
        resolveAPIsetsToolStripMenuItem.Checked = _configuration.ResolveAPIsets;
        SetPopupMenuItemText(true);
    }

    private void UndecorateFunctionToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _configuration.ViewUndecorated = !_configuration.ViewUndecorated;
        ViewUndecoratedToolButton.Checked = _configuration.ViewUndecorated;
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

    private void ExternalHelpMenuItem_Click(object sender, EventArgs e)
    {
        ProcessFunctionEntry();
    }

    private void MenuClearLogItem_Click(object sender, EventArgs e)
    {
        reLog.Clear();
        ClearModuleLinks();
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

    /// <summary>
    /// Text search in the rich edit log
    /// </summary>
    /// <param name="searchText">Text to find</param>
    /// <param name="startIndex">Position to start search from</param>
    /// <param name="options">Search options</param>
    /// <returns>Index of found text or -1 if not found</returns>
    private int FindMyText(string searchText, int startIndex, RichTextBoxFinds options)
    {
        if (string.IsNullOrEmpty(searchText) || reLog.TextLength == 0)
            return -1;

        if (!options.HasFlag(RichTextBoxFinds.Reverse) && startIndex >= reLog.TextLength)
            startIndex = 0;

        if (options.HasFlag(RichTextBoxFinds.Reverse) && startIndex <= 0)
            startIndex = reLog.TextLength;

        if (reLog.TextLength > 500000) // >500KB
        {
            return FindMyTextChunked(searchText, startIndex, options);
        }

        int result = reLog.Find(searchText, startIndex, options);
        if (result == -1)
        {
            if (!options.HasFlag(RichTextBoxFinds.Reverse))
            {
                result = reLog.Find(searchText, 0, options);
            }
            else
            {
                result = reLog.Find(searchText, reLog.TextLength, options);
            }
        }

        return result;
    }

    /// <summary>
    /// Performs search in chunks for very large logs to maintain UI responsiveness
    /// </summary>
    private int FindMyTextChunked(string searchText, int startIndex, RichTextBoxFinds options)
    {
        reLog.SuspendLayout();

        try
        {
            const int ChunkSize = 200000; // 200KB chunks
            bool isReverse = options.HasFlag(RichTextBoxFinds.Reverse);

            int chunkStart, chunkEnd;

            if (!isReverse)
            {
                chunkStart = startIndex;
                chunkEnd = Math.Min(chunkStart + ChunkSize, reLog.TextLength);
            }
            else
            {
                chunkStart = Math.Max(0, startIndex - ChunkSize);
                chunkEnd = startIndex;
            }

            int result = reLog.Find(searchText, chunkStart, chunkEnd - chunkStart, options);

            if (result != -1)
                return result;

            if (!isReverse)
            {
                for (int pos = chunkEnd; pos < reLog.TextLength; pos += ChunkSize)
                {
                    int end = Math.Min(pos + ChunkSize, reLog.TextLength);
                    result = reLog.Find(searchText, pos, end - pos, options);

                    if (result != -1)
                        return result;
                }

                for (int pos = 0; pos < startIndex; pos += ChunkSize)
                {
                    int end = Math.Min(pos + ChunkSize, startIndex);
                    result = reLog.Find(searchText, pos, end - pos, options);

                    if (result != -1)
                        return result;
                }
            }
            else
            {
                for (int pos = chunkStart; pos > 0; pos = Math.Max(0, pos - ChunkSize))
                {
                    int start = pos;
                    int length = Math.Min(ChunkSize, pos);
                    result = reLog.Find(searchText, start - length, length, options);

                    if (result != -1)
                        return result;

                    if (pos < ChunkSize) break;
                }

                for (int pos = reLog.TextLength; pos > startIndex; pos -= ChunkSize)
                {
                    int start = pos;
                    int length = Math.Min(ChunkSize, pos - startIndex);
                    result = reLog.Find(searchText, start - length, length, options);

                    if (result != -1)
                        return result;
                }
            }

            return -1;
        }
        finally
        {
            reLog.ResumeLayout();
        }
    }

    public void LogFindString()
    {
        int startPos;
        int index;

        if (string.IsNullOrEmpty(_logSearchState.FindText))
            return;

        this.Activate();
        reLog.Focus();

        startPos = _logSearchState.SearchPosition;

        if (_logSearchState.IndexOfSearchText >= 0 &&
            _logSearchState.IndexOfSearchText == reLog.SelectionStart &&
            reLog.SelectionLength == _logSearchState.FindText.Length)
        {
            if (!_logSearchState.FindOptions.HasFlag(RichTextBoxFinds.Reverse))
                startPos = _logSearchState.IndexOfSearchText + _logSearchState.FindText.Length;
            else
                startPos = _logSearchState.IndexOfSearchText;
        }

        index = FindMyText(_logSearchState.FindText, startPos, _logSearchState.FindOptions);

        if (index != -1)
        {
            reLog.Select(index, _logSearchState.FindText.Length);
            reLog.ScrollToCaret();

            _logSearchState.SearchPosition = !_logSearchState.FindOptions.HasFlag(RichTextBoxFinds.Reverse)
                ? index + _logSearchState.FindText.Length
                : index - 1;

            _logSearchState.IndexOfSearchText = index;
        }
        else
        {
            reLog.SelectionLength = 0;
            _logSearchState.IndexOfSearchText = -1;
        }
    }

    private void ShowFindDialog()
    {
        // If dialog doesn't exist or was disposed, create a new one
        if (_findDialog == null || _findDialog.IsDisposed)
        {
            _findDialog = new FindDialogForm(this, _configuration);
            _findDialog.Owner = this;
            _findDialog.Show();
        }
        else
        {
            _findDialog.Show();
            _findDialog.Activate();
        }
    }

    private void FindMenuItem_Click(object sender, EventArgs e)
    {
        ShowFindDialog();
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
        _configuration.ResolveAPIsets = !_configuration.ResolveAPIsets;
        ResolveAPISetsToolButton.Checked = _configuration.ResolveAPIsets;
        UpdateFileView(FileViewUpdateAction.ModulesTreeAndListChange);
    }

    private ListViewItem FindMatchingFunctionInList(ListView SourceList, ListView DestinationList, List<CFunction> FunctionsList)
    {
        if (SourceList.SelectedIndices.Count > 0)
        {
            int selectedIndex = SourceList.SelectedIndices[0];
            if (selectedIndex >= 0 && selectedIndex < FunctionsList.Count)
            {
                _searchFunctionName = FunctionsList[selectedIndex].RawName;
                _searchOrdinal = FunctionsList[selectedIndex].Ordinal;

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
            funcSrc = _currentImportsList;
        }
        else if (LVExports.Focused)
        {
            funcSrc = _currentExportsList;
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
            try
            {
                lvDst.SelectedIndices.Clear();
                lvResult.Selected = true;
                lvResult.EnsureVisible();
                lvDst.Focus();
            }
            finally { lvDst.EndUpdate(); }
        }
    }

    private void HighlightModuleInTreeOrList(object sender, EventArgs e)
    {
        if (TVModules.Focused && TVModules.SelectedNode?.Tag is CModule selectedModule)
        {
            LVModules.BeginUpdate();
            try
            {
                ListViewItem lvResult = LVModules.FindItemWithText(selectedModule.FileName);
                if (lvResult != null)
                {
                    LVModules.SelectedIndices.Clear();
                    lvResult.Selected = true;
                    lvResult.EnsureVisible();
                    LVModules.Focus();
                }
            }
            finally { LVModules.EndUpdate(); }
        }
        else if (LVModules.Focused && LVModules.SelectedIndices.Count > 0)
        {
            int selectedItemIndex = LVModules.SelectedIndices[0];

            if (selectedItemIndex < _loadedModulesList.Count)
            {
                CModule selectedListViewModule = _loadedModulesList[selectedItemIndex];

                TVModules.BeginUpdate();
                try
                {
                    TreeNode resultNode = CUtils.TreeViewFindModuleNodeByObject(selectedListViewModule, _rootNode);

                    if (resultNode != null)
                    {
                        TVModules.SelectedNode = resultNode;
                        TVModules.SelectedNode.Expand();
                        TVModules.SelectedNode.EnsureVisible();
                        TVModules.Select();
                    }
                }
                finally { TVModules.EndUpdate(); }
            }
        }
    }

    private void CollapseAllMenuItem_Click(object sender, EventArgs e)
    {
        TVModules.BeginUpdate();
        try
        {
            TVModules.CollapseAll();
        }
        finally { TVModules.EndUpdate(); }
    }

    private void ExpandAllMenuItem_Click(object sender, EventArgs e)
    {
        TVModules.BeginUpdate();
        try
        {
            TVModules.ExpandAll();
            TVModules.SelectedNode?.EnsureVisible();
        }
        finally { TVModules.EndUpdate(); }
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

        using (var sysInfoDlg = new SysInfoDialogForm(si, isLocal))
        {
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
        _currentImportsList = module.ParentImports;

        if (module.OriginalInstanceId != 0)
        {
            // Duplicate module, exports from the original instance.
            CModule origInstance = CUtils.InstanceIdToModule(module.OriginalInstanceId, _loadedModulesList);

            // Set list from original instance if it present, otherwise create new empty list. 
            _currentExportsList = origInstance?.ModuleData.Exports ?? [];
        }
        else
        {
            //
            // Original module
            //
            _currentExportsList = module.ModuleData.Exports;
        }

        //
        // Update function icons.
        //
        ResolveFunctionKindForList(_currentImportsList, module, _loadedModulesList);
        ResolveFunctionKindForList(_currentExportsList, module, _loadedModulesList);

        UpdateListViewInternal(LVExports, _currentExportsList, _configuration.SortColumnExports, LVExportsSortOrder, DisplayCacheType.Exports);
        UpdateListViewInternal(LVImports, _currentImportsList, _configuration.SortColumnImports, LVImportsSortOrder, DisplayCacheType.Imports);

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
                function.ResolveFunctionKind(module, modulesList, _parentImportsHashTable);
            }
        }
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
        try
        {
            CModule origInstance = CUtils.TreeViewGetOriginalInstanceFromNode(TVModules.SelectedNode, _loadedModulesList);
            if (origInstance != null)
            {
                var tvNode = CUtils.TreeViewFindModuleNodeByObject(origInstance, _rootNode);
                if (tvNode != null)
                {
                    TVModules.SelectedNode = tvNode;
                    TVModules.SelectedNode.Expand();
                    TVModules.SelectedNode.EnsureVisible();
                    TVModules.Select();
                }
            }
        }
        finally { TVModules.EndUpdate(); }
    }

    TreeNode TreeViewFindNodeInstancePrev(TreeNode currentNode, TreeNode selectedNode, string moduleName)
    {
        TreeNode lastNode = null;

        while (currentNode != null && !_instanceStopSearch)
        {
            if (currentNode.Tag is CModule obj && obj.FileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentNode == selectedNode)
                {
                    _instanceStopSearch = true;
                    break;
                }
                else
                {
                    lastNode = currentNode;
                }
            }

            if (currentNode.Nodes.Count != 0 && !_instanceStopSearch)
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

        while (currentNode != null && !_instanceStopSearch)
        {
            if (currentNode.Tag is CModule obj && obj.FileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentNode == selectedNode)
                {
                    _instanceSelfFound = true;
                }
                else if (_instanceSelfFound)
                {
                    _instanceStopSearch = true;
                    lastNode = currentNode;
                }
            }

            if (currentNode.Nodes.Count != 0 && !_instanceStopSearch)
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

        _instanceStopSearch = false;

        if (bNextInstance)
        {
            _instanceSelfFound = false;
        }

        TVModules.BeginUpdate();
        try
        {
            TreeNode tvNode;

            if (bNextInstance)
            {
                tvNode = TreeViewFindNodeInstanceNext(_rootNode, TVModules.SelectedNode, obj.FileName);
            }
            else
            {
                tvNode = TreeViewFindNodeInstancePrev(_rootNode, TVModules.SelectedNode, obj.FileName);
            }

            if (tvNode != null)
            {
                TVModules.SelectedNode = tvNode;
                TVModules.SelectedNode.Expand();
                TVModules.SelectedNode.EnsureVisible();
                TVModules.Select();
            }
        }
        finally { TVModules.EndUpdate(); }
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
        bool bSessionAllocated = _depends != null;
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
            string exceptionMessage;

            if (ex.InnerException != null)
            {
                exceptionMessage = ex.InnerException.Message;
            }
            else
            {
                exceptionMessage = ex.Message;
            }

            AddLogMessage($"Session file \"{fileName}\" could not be opened because \"" +
                $"{exceptionMessage}\"",
                LogMessageType.ErrorOrWarning);
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

        if (_depends == null)
        {
            return;
        }

        // We want new filename for our session view.
        if (saveAs)
        {
            _depends.IsSavedSessionView = false;
            _depends.SessionFileName = string.Empty;
        }

        // This is new session.
        if (!_depends.IsSavedSessionView && string.IsNullOrEmpty(_depends.SessionFileName))
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
                if (_depends.SystemInformation.Count == 0)
                {
                    CUtils.CollectSystemInformation(_depends.SystemInformation);
                }

                _depends.IsSavedSessionView = true;
                _depends.SessionFileName = fileName;
            }
        }
        else
        {
            // This is loaded session, just save as is.
            fileName = _depends.SessionFileName;
        }

        bool bSaved = false;

        try
        {
            if (useCompression)
            {
                bSaved = CUtils.SavePackedObjectToFile(fileName, _depends, typeof(CDepends), UpdateOperationStatus);
            }
            else
            {
                bSaved = CUtils.SaveObjectToFilePlainText(fileName, _depends, typeof(CDepends));
            }
        }
        catch (Exception ex)
        {
            if (!jsonOutput)
            {
                _depends.IsSavedSessionView = false;
                _depends.SessionFileName = string.Empty;
            }

            string exceptionMessage;

            if (ex.InnerException != null)
            {
                exceptionMessage = ex.InnerException.Message;
            }
            else
            {
                exceptionMessage = ex.Message;
            }

            AddLogMessage($"Error: Session file \"{fileName}\" could not be saved because \"" +
                $"{exceptionMessage}\"", LogMessageType.ErrorOrWarning);
        }

        if (bSaved)
        {
            AddLogMessage($"Information: File \"{fileName}\" has been saved.", LogMessageType.System);
        }

        UpdateOperationStatus(string.Empty);
    }

    private void MainMenuSave_Click(object sender, EventArgs e)
    {
        SaveSessionObjectToFile(false, _configuration.CompressSessionFiles);
    }

    private void MainMenuSaveAs_Click(object sender, EventArgs e)
    {
        SaveSessionObjectToFile(true, _configuration.CompressSessionFiles);
    }

    private void TVModules_AfterSelect(object sender, TreeViewEventArgs e)
    {
        CModule module = (CModule)e.Node.Tag;
        if (module != null)
        {
            if (_configuration.UseSymbols) PreloadSymbolForSelectedModule(module);
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

        string moduleDisplayName = BuildModuleDisplayName(module.GetModuleNameRespectApiSet(_configuration.ResolveAPIsets), _configuration.FullPaths);

        if (_configuration.UpperCaseModuleNames)
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
            lvItem.SubItems.Add(module.IsReproducibleBuild
                ? $"Repro hash: 0x{moduleData.LinkTimeStamp:X8}"
                : CUtils.TimeSince1970ToString(moduleData.LinkTimeStamp));

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
            string value = Enum.IsDefined(typeof(Machine), moduleData.Machine)
                ? ((Machine)moduleData.Machine).FriendlyName()
                : $"0x{moduleData.Machine:X4}";

            bool isCpuMismatch = IsCpuMismatchForDisplay(module, _depends.RootModule);
            if (isCpuMismatch)
            {
                lvItem.UseItemStyleForSubItems = false;
                lvItem.SubItems.Add(value, Color.Red, Color.White, lvItem.Font);
            }
            else
            {
                lvItem.SubItems.Add(value);
            }

            // Subsystem
            lvItem.SubItems.Add(Enum.IsDefined(typeof(Subsystem), moduleData.Subsystem)
                ? ((Subsystem)moduleData.Subsystem).FriendlyName()
                : $"0x{moduleData.Subsystem:X4}");

            // Debug Symbols
            if (moduleData.DebugDirTypes.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var entry in moduleData.DebugDirTypes.Distinct())
                {
                    if (_debugAbbreviations.TryGetValue(entry, out var abbr))
                    {
                        if (sb.Length > 0) sb.Append(',');
                        sb.Append(abbr);
                    }
                }
                value = sb.Length > 0 ? sb.ToString() : CConsts.NoneMsg;
            }
            else
            {
                value = CConsts.NoneMsg;
            }
            lvItem.SubItems.Add(value);

            // Preferred base
            // Calculate the hexadecimal format string based on architecture
            string hexFormat = $"X{UIntPtr.Size * (module.Is64bitArchitecture() ? 2 : 1)}";
            lvItem.SubItems.Add($"0x{moduleData.PreferredBase.ToString(hexFormat)}");

            // Virtual size
            lvItem.SubItems.Add($"0x{moduleData.VirtualSize:X8}");

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

    static string TryUndecorateFunction(bool viewUndecorated, CFunction function)
    {
        if (!CSymbolResolver.UndecorationReady)
        {
            return function.RawName;
        }

        return viewUndecorated && function.IsNameDecorated() ? function.UndecorateFunctionName() : function.RawName;
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
        lvItem.SubItems.Add(function.Ordinal == uint.MaxValue
            ? CConsts.NotAvailableMsg
            : $"{function.Ordinal} (0x{function.Ordinal:X4})");

        // Hint
        lvItem.SubItems.Add(function.Hint == uint.MaxValue
            ? CConsts.NotAvailableMsg
            : $"{function.Hint} (0x{function.Hint:X4})");

        // FunctionName
        string functionName = string.Empty;
        if (function.SnapByOrdinal())
        {
            var resolvedFunction = module.ResolveFunctionForOrdinal(function.Ordinal);
            if (resolvedFunction != null)
            {
                functionName = TryUndecorateFunction(_configuration.ViewUndecorated, resolvedFunction);
            }
        }
        else
        {
            functionName = TryUndecorateFunction(_configuration.ViewUndecorated, function);
        }

        //
        // Nothing found, attempt to resolve using symbols.
        //
        if (string.IsNullOrEmpty(functionName) && _configuration.UseSymbols)
        {
            var moduleBase = CSymbolResolver.RetrieveCachedSymModule(module.FileName);
            if (moduleBase != IntPtr.Zero)
            {
                UInt64 functionAddress = 0;
                //
                // If function is parent import then lookup it by ordinal in module exports
                // to query function actual address for symbols resolving.
                //
                if (!function.IsExportFunction && function.Address == 0)
                {
                    var exportFunction = module.ModuleData.Exports.Find(item => item.Ordinal == function.Ordinal);
                    if (exportFunction != null)
                    {
                        functionAddress = exportFunction.Address;
                    }
                }
                else
                {
                    functionAddress = function.Address;
                }

                if (functionAddress != 0)
                {
                    var symAddress = Convert.ToUInt64(moduleBase) + functionAddress;
                    if (CSymbolResolver.QuerySymbolForAddress(symAddress, out string symName))
                    {
                        function.IsNameFromSymbols = true;
                        function.RawName = symName;
                        functionName = TryUndecorateFunction(_configuration.ViewUndecorated, function);
                    }
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
            lvItem.SubItems.Add(functionName, Color.Black, _configuration.SymbolsHighlightColor, lvItem.Font);
        }
        else
        {
            lvItem.SubItems.Add(functionName);
        }

        // EntryPoint
        lvItem.SubItems.Add(function.IsForward()
            ? function.ForwardName
            : function.Address == 0 ? CConsts.NotBoundMsg : $"0x{function.Address:X8}");

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
        if (LVExportsCache != null &&
            e.ItemIndex >= LVExportsFirstItem &&
            e.ItemIndex < LVExportsFirstItem + LVExportsCache.Length)
        {
            e.Item = LVExportsCache[e.ItemIndex - LVExportsFirstItem];
        }
        else
        {
            var selectedModule = TVModules.SelectedNode?.Tag as CModule;
            e.Item = LVCreateFunctionEntry(_currentExportsList[e.ItemIndex], selectedModule);
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
        if (LVImportsCache != null &&
            e.ItemIndex >= LVImportsFirstItem &&
            e.ItemIndex < LVImportsFirstItem + LVImportsCache.Length)
        {
            e.Item = LVImportsCache[e.ItemIndex - LVImportsFirstItem];
        }
        else
        {
            var selectedModule = TVModules.SelectedNode?.Tag as CModule;
            e.Item = LVCreateFunctionEntry(_currentImportsList[e.ItemIndex], selectedModule);
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
        if (LVModulesCache != null &&
            e.ItemIndex >= LVModulesFirstItem &&
            e.ItemIndex < LVModulesFirstItem + LVModulesCache.Length)
        {
            e.Item = LVModulesCache[e.ItemIndex - LVModulesFirstItem];
        }
        else
        {
            e.Item = LVCreateModuleEntry(_loadedModulesList[e.ItemIndex]);
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

        for (int i = 0, j = LVExportsFirstItem; i < length && j < _currentExportsList.Count; i++, j++)
        {
            LVExportsCache[i] = LVCreateFunctionEntry(_currentExportsList[j], selectedModule);
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

        for (int i = 0, j = LVImportsFirstItem; i < length && j < _currentImportsList.Count; i++, j++)
        {
            LVImportsCache[i] = LVCreateFunctionEntry(_currentImportsList[j], selectedModule);
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

        for (int i = 0, j = LVModulesFirstItem; i < length && j < _loadedModulesList.Count; i++, j++)
        {
            LVModulesCache[i] = LVCreateModuleEntry(_loadedModulesList[j]);
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
        if (string.IsNullOrEmpty(_searchFunctionName))
        {
            if (_searchOrdinal != UInt32.MaxValue)
            {
                foreach (var entry in itemList)
                {
                    if (entry.Ordinal == _searchOrdinal)
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
                if (entry.RawName.Equals(_searchFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    e.Index = itemList.IndexOf(entry);
                    return;
                }
            }

            // If item is not found, search by ordinal if possible.
            if (_searchOrdinal != UInt32.MaxValue)
            {
                foreach (var entry in itemList)
                {
                    if (entry.Ordinal == _searchOrdinal)
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
        LVFunctionsSearchForVirtualItem(_currentImportsList, e);
    }

    /// <summary>
    /// LVExports virtual listview search handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVExportsSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
    {
        LVFunctionsSearchForVirtualItem(_currentExportsList, e);
    }

    /// <summary>
    /// LVModules virtual listview search handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVModulesSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
    {
        foreach (var module in _loadedModulesList)
        {
            if (module.FileName.Equals(e.Text, StringComparison.OrdinalIgnoreCase))
            {
                e.Index = _loadedModulesList.IndexOf(module);
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
        _configuration.SortColumnExports = columnIndex;
        LVExportsSortOrder = (LVExportsSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVFunctionsSort(LVExports, columnIndex, LVExportsSortOrder, _currentExportsList, DisplayCacheType.Exports);
    }

    private void LVImportsColumnClick(object sender, ColumnClickEventArgs e)
    {
        int columnIndex = e.Column;
        _configuration.SortColumnImports = columnIndex;
        LVImportsSortOrder = (LVImportsSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVFunctionsSort(LVImports, columnIndex, LVImportsSortOrder, _currentImportsList, DisplayCacheType.Imports);
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
        IComparer<CModule> modulesComparer = new CModuleComparer(sortOrder, columnIndex, _configuration.FullPaths);
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
        _configuration.SortColumnModules = columnIndex;
        LVModulesSortOrder = (LVModulesSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVModulesSort(LVModules, columnIndex, LVModulesSortOrder, _loadedModulesList, DisplayCacheType.Modules);
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
        if (_shutdownInProgress)
            return;

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
        if (_depends != null)
        {
            var fName = _depends.RootModule.RawFileName;
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

        _functionLookupText += char.ToLower(e.KeyChar);

        if (_functionsHintForm.Controls[CConsts.HintFormLabelControl] is Label hintLabel)
        {
            hintLabel.Text = "Search: " + _functionLookupText;
            hintLabel.Size = hintLabel.PreferredSize;

            Point location = lvDst.PointToScreen(new Point(lvDst.Bounds.Left, lvDst.Bounds.Bottom));

            _functionsHintForm.Size = new Size(hintLabel.Width + 10, hintLabel.Height + 10);
            _functionsHintForm.Location = location;
            _functionsHintForm.Show();
        }

        List<CFunction> currentList = lvDst == LVImports ? _currentImportsList : _currentExportsList;

        _searchOrdinal = UInt32.MaxValue;
        _searchFunctionName = string.Empty;

        var matchingItem = currentList.FirstOrDefault(item => item.RawName.StartsWith(_functionLookupText, StringComparison.OrdinalIgnoreCase));
        if (matchingItem != null)
        {
            _searchFunctionName = matchingItem.RawName;
            _searchOrdinal = matchingItem.Ordinal;

            lvDst.BeginUpdate();
            try
            {
                lvDst.SelectedIndices.Clear();
                ListViewItem lvResult = lvDst.FindItemWithText(null);

                if (lvResult != null)
                {
                    lvResult.Selected = true;
                    lvResult.EnsureVisible();
                    lvDst.Focus();
                }
            }
            finally { lvDst.EndUpdate(); }
        }

        await Task.Delay(2000);
        _functionsHintForm.Hide();
        _functionLookupText = "";
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

        _moduleLookupText += char.ToLower(e.KeyChar);

        if (_modulesHintForm.Controls[CConsts.HintFormLabelControl] is Label hintLabel)
        {
            hintLabel.Text = "Search: " + _moduleLookupText;
            hintLabel.Size = hintLabel.PreferredSize;

            Point location = new(LVModules.Bounds.Left, LVModules.Bounds.Bottom);
            _modulesHintForm.Size = new Size(hintLabel.Width + 10, hintLabel.Height + 10);
            _modulesHintForm.Location = LVModules.PointToScreen(location);
            _modulesHintForm.Show();
        }

        CModule matchingModule = _loadedModulesList.FirstOrDefault(module =>
        {
            string moduleName = Path.GetFileName(module.GetModuleNameRespectApiSet(_configuration.ResolveAPIsets));
            return moduleName.StartsWith(_moduleLookupText, StringComparison.OrdinalIgnoreCase);
        });

        if (matchingModule != null)
        {
            LVModules.BeginUpdate();
            try
            {
                LVModules.SelectedIndices.Clear();
                ListViewItem lvResult = LVModules.FindItemWithText(matchingModule.FileName);
                if (lvResult != null)
                {
                    lvResult.Selected = true;
                    lvResult.EnsureVisible();
                    LVModules.Focus();
                }
            }
            finally { LVModules.EndUpdate(); }
        }

        await Task.Delay(2000);
        _modulesHintForm.Hide();
        _moduleLookupText = "";
    }

    private void LVFunctions_Leave(object sender, EventArgs e)
    {
        _functionsHintForm.Hide();
        _functionLookupText = "";
    }

    private void LVModules_Leave(object sender, EventArgs e)
    {
        _modulesHintForm.Hide();
        _moduleLookupText = "";
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

        FormWindowState state = (this.WindowState == FormWindowState.Minimized)
            ? FormWindowState.Normal
            : this.WindowState;

        _configuration.WindowState = (int)state;
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

    protected override void OnHandleDestroyed(EventArgs e)
    {
        try
        {
            // Unsubscribe from events
            if (ElevatedDragDropManager.Instance != null)
            {
                ElevatedDragDropManager.Instance.ElevatedDragDrop -= MainForm_ElevatedDragDrop;
            }

            // Clean up hint forms
            CleanupHintForms();
        }
        finally
        {
            base.OnHandleDestroyed(e);
        }
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
        catch (Exception)
        {
        }
        finally
        {
            _disposingHintForms = false;
        }
    }

    /// <summary>
    /// Finds a module node in the tree view by instance ID and selects it
    /// </summary>
    private void FindAndSelectModuleNode(int instanceId)
    {
        TreeNode foundNode = FindModuleNodeByInstanceId(TVModules.Nodes, instanceId);
        if (foundNode != null)
        {
            TreeNode parent = foundNode.Parent;
            while (parent != null)
            {
                parent.Expand();
                parent = parent.Parent;
            }

            TVModules.SelectedNode = foundNode;
            foundNode.EnsureVisible();

            TVModules.Focus();
        }
    }

    /// <summary>
    /// Recursively finds a tree node by module instance ID
    /// </summary>
    /// <param name="nodes">Collection of tree nodes to search</param>
    /// <param name="instanceId">Module instance ID to find</param>
    /// <returns>The found tree node or null</returns>
    private static TreeNode FindModuleNodeByInstanceId(TreeNodeCollection nodes, int instanceId)
    {
        Queue<TreeNode> nodesToSearch = new Queue<TreeNode>();

        foreach (TreeNode rootNode in nodes)
        {
            nodesToSearch.Enqueue(rootNode);
        }

        while (nodesToSearch.Count > 0)
        {
            TreeNode currentNode = nodesToSearch.Dequeue();

            if (currentNode?.Tag is CModule module && module.InstanceId == instanceId)
            {
                return currentNode;
            }

            foreach (TreeNode childNode in currentNode.Nodes)
            {
                nodesToSearch.Enqueue(childNode);
            }
        }

        return null;
    }

    /// <summary>
    /// Handles clicks on module links in the log
    /// </summary>
    private void RichEditLog_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int charIndex = reLog.GetCharIndexFromPosition(new Point(e.X, e.Y));

            foreach (var link in moduleLinks)
            {
                var (start, length) = link.Key;
                int instanceId = link.Value;

                if (charIndex >= start && charIndex < start + length)
                {
                    FindAndSelectModuleNode(instanceId);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Handles mouse movement over the rich text log to provide cursor feedback for module links
    /// </summary>
    private void RichEditLog_MouseMove(object sender, MouseEventArgs e)
    {
        // Get character index at current mouse position
        int charIndex = reLog.GetCharIndexFromPosition(new Point(e.X, e.Y));

        // Check if we're over a link
        bool overLink = false;
        int instanceId = 0;

        foreach (var link in moduleLinks)
        {
            var (start, length) = link.Key;
            if (charIndex >= start && charIndex < start + length)
            {
                overLink = true;
                instanceId = link.Value;
                break;
            }
        }

        // Only change cursor if state changed
        if (overLink != _isCurrentlyOverLink ||
            (overLink && instanceId != _currentLinkInstanceId))
        {
            reLog.Cursor = overLink ? Cursors.Hand : Cursors.Default;
            _isCurrentlyOverLink = overLink;
            _currentLinkInstanceId = instanceId;

            CModule module = _loadedModulesList.FirstOrDefault(m => m.InstanceId == instanceId);
            if (module != null)
            {
                string tooltipText = $"Click to navigate to module: {Path.GetFileName(module.FileName)}";
                if (moduleToolTip.GetToolTip(reLog) != tooltipText)
                    moduleToolTip.SetToolTip(reLog, tooltipText);
            }
        }
    }
}
