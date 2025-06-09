/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CCONSTS.CS
*
*  VERSION:     1.00
*
*  DATE:        09 Jun 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
namespace WinDepends;

public static class CConsts
{
    public const string ProgramName = "Windows Dependencies";
    public const string ShortProgramName = "WinDepends";
    public const string CopyrightString = "© 2024 - 2025 WinDepends Project Authors";

    public const string NotAvailableMsg = "N/A";
    public const string NotBoundMsg = "Not bound";
    public const string NoneMsg = "None";

    public const string AdminMsg = " (Administrator)";
    public const string Admin64Msg = " (Administrator, 64-bit)";
    public const string SixtyFourBitsMsg = " (64-bit)";

    public const string DateTimeFormat24Hours = "dd/MM/yyyy HH:mm:ss";

    public const uint DefaultAppStartAddress = 0x1000000;

    public const uint VersionMajor = 1;
    public const uint VersionMinor = 0;
    public const uint VersionRevision = 0;
    public const uint VersionBuild = 2505;

    public const int HistoryDepthMax = 32;
    public const int HistoryDepthDefault = 10;

    public const int ModuleNodeDepthMin = 0;
    public const int ModuleNodeDepthDefault = 2;

    public const int MinPortNumber = 49152;
    public const int MaxPortNumber = ushort.MaxValue;

    public const int MinValidWidth = 480;
    public const int MinValidHeight = 640;

    public const int TagUseESC = 100;
    public const int TagAutoExpand = 120;
    public const int TagFullPaths = 121;
    public const int TagViewUndecorated = 122;
    public const int TagResolveAPIsets = 123;
    public const int TagUpperCaseModuleNames = 124;
    public const int TagClearLogOnFileOpen = 125;
    public const int TagViewExternalViewer = 200;
    public const int TagViewProperties = 201;
    public const int TagSystemInformation = 300;
    public const int TagConfiguration = 301;
    public const int TagCompressSessionFiles = 400;
    public const int TagUseApiSetSchemaFile = 500;
    public const int TagHighlightApiSet = 501;
    public const int TagProcessRelocsForImage = 600;
    public const int TagUseStats = 601;
    public const int TagAnalysisDefaultEnabled = 602;
    public const int TagPropagateSettingsEnabled = 603;
    public const int TagUseSymbols = 604;
    public const int TagUseCustomImageBase = 605;

    public const int TagTbUseClassic = 1000;
    public const int TagTbUseModern = 1001;

    public const int ModuleIconsAllHeight = 15;
    public const int ModuleIconsAllWidth = 26;
    public const int FunctionIconsHeigth = 14;
    public const int FunctionIconsWidth = 30;
    public const int ModuleIconsHeight = 15;
    public const int ModuleIconsWidth = 26;

    public const int ToolBarIconsHeigth = 48;
    public const int ToolBarIconsWidth = 48;

    public const int ToolBarIconsHeigthClassic = 15;
    public const int ToolBarIconsWidthClassic = 16;

    public const int SearchOrderIconsWidth = 18;
    public const int SearchOrderIconsHeigth = 17;

    public const int ToolBarImageMax = 11;

    public const float DefaultDPIValue = 96f;
    public const float DpiScaleFactor150 = 1.5f;

    public const string SearchOrderUserValue = "SearchOrderUserValue";

    /// <summary>
    /// Configuration tabs indexes.
    /// </summary>
    public const int IdxTabMain = 0;
    public const int IdxTabHistory = 1;
    public const int IdxTabAnalysis = 2;
    public const int IdxTabApiset = 3;
    public const int IdxTabExtHelpViewer = 4;
    public const int IdxTabExtModuleViewer = 5;
    public const int IdxTabHandledFileExtensions = 6;
    public const int IdxTabSearchOrder = 7;
    public const int IdxTabSearchOrderDrivers = 8;
    public const int IdxTabServer = 9;
    public const int IdxTabSymbols = 10;

    /// <summary>
    /// Count of LVImports, LVExports columns.
    /// </summary>
    public const int FunctionsColumnsCount = 4;
    /// <summary>
    /// Count of LVModules columns.
    /// </summary>
    public const int ModulesColumnsCount = 20;

    /// <summary>
    /// Setting value names.
    /// </summary>
    public const string ExternalFunctionHelpURL = "https://learn.microsoft.com/en-us/search/?terms=%1";

    /// <summary>
    /// Name of working lists.
    /// </summary>
    public const string TVModulesName = "TVModules";
    public const string LVModulesName = "LVModules";
    public const string LVImportsName = "LVImports";
    public const string LVExportsName = "LVExports";

    /// <summary>
    /// Up, down sort marks.
    /// </summary>
    public const string AscendSortMark = "\u25B2";
    public const string DescendSortMark = "\u25BC";

    //
    // Session file extension
    //
    public const string WinDependsSessionFileExt = ".wds";

    //
    // Shortcut file extension
    //
    public const string ShortcutFileExt = ".lnk";

    // Dll file extension
    public const string DllFileExt = ".dll";

    //
    // Common open dialog filter extensions.
    //
    public const string HandledFileExtensionsMsg = "Handled File Extensions|";
    public const string WinDependsFilter = "|WinDepends session view (*.wds)|*.wds|All files (*.*)|*.*";
    public const string ConfigBrowseFilter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
    public const string DbgHelpBrowseFilter = "Dynamic Link Libraries (*.dll)|*.dll|All files (*.*)|*.*";

    public const string ShellIntegrationCommand = "file\\shell\\View in WinDepends";

    //
    // System stuff.
    //
    public const string ExplorerApp = "explorer.exe";
    public const string HostSysDir = "system32";
    public const string HostSys16Dir = "system";
    public const string DriversDir = "drivers";

    public const string NtoskrnlExe = "ntoskrnl.exe";
    public const string NtdllDll = "ntdll.dll";
    public const string Kernel32Dll = "kernel32.dll";
    public const string KdComDll = "kdcom.dll";
    public const string BootVidDll = "bootvid.dll";
    public const string HalDll = "hal.dll";

    public const string DbgHelpDll = "dbghelp.dll";
    public const string SymbolsDownloadLink = "*https://msdl.microsoft.com/download/symbols";
    public const string SymbolsDefaultStoreDirectory = "WinDepends\\Symbols";

    //
    // Urls.
    //
    public const string WinDependsHome = "https://github.com/hfiref0x/WinDepends";
    public const string WinDependsDocs = "https://github.com/hfiref0x/WinDepends.Docs";
    public const string DependsHome = "https://www.dependencywalker.com/";

    //
    // WinDepends server app.
    //
    public const string WinDependsCoreApp = "WinDepends.Core";

    public const string CoreServerAddress = "127.0.0.1";
    public const int CoreServerChainSizeMax = 32762;

    public const string HintFormLabelControl = "HintLabel";
    public const string CategoryUserDefinedDirectory = "The user defined directory";

    public const int SERVER_ERROR_SUCCESS = 0;
    public const int SERVER_ERROR_WSASTARTUP = 1;
    public const int SERVER_ERROR_SOCKETINIT = 2;
    public const int SERVER_ERROR_INVALIDIP = 3;
    public const int SERVER_ERROR_BIND = 4;
    public const int SERVER_ERROR_LISTEN = 5;

    /// <summary>
    /// Msg: OK
    /// </summary>
    public const string WDEP_STATUS_200 = "WDEP/1.0 200 OK\r\n";
    /// <summary>
    /// Msg: Unknown data format
    /// </summary>
    public const string WDEP_STATUS_208 = "WDEP/1.0 208 Unknown data format\r\n";
    /// <summary>
    /// Msg: Invalid parameters received
    /// </summary>
    public const string WDEP_STATUS_400 = "WDEP/1.0 400 Invalid parameters received\r\n";
    /// <summary>
    /// Msg: Can not read file headers
    /// </summary>
    public const string WDEP_STATUS_403 = "WDEP/1.0 403 Can not read file headers\r\n";
    /// <summary>
    /// Msg: File not found or can not be accessed
    /// </summary>
    public const string WDEP_STATUS_404 = "WDEP/1.0 404 File not found or can not be accessed\r\n";
    /// <summary>
    /// Msg: Invaild file headers or signatures
    /// </summary>
    public const string WDEP_STATUS_415 = "WDEP/1.0 415 Invalid file headers or signatures\r\n";
    /// <summary>
    /// Msg: Can not allocate resources
    /// </summary>
    public const string WDEP_STATUS_500 = "WDEP/1.0 500 Can not allocate resources\r\n";
    /// <summary>
    /// Context not allocated
    /// </summary>
    public const string WDEP_STATUS_501 = "WDEP/1.0 501 Context not allocated\r\n";
    /// <summary>
    /// Image buffer not allocated
    /// </summary>
    public const string WDEP_STATUS_502 = "WDEP/1.0 502 Image buffer not allocated\r\n";
    /// <summary>
    /// Unhandled exception reported to the client
    /// </summary>
    public const string WDEP_STATUS_600 = "WDEP/1.0 600 Exception\r\n";
}

/// <summary>
/// LVImports/LVExports image indexes.
/// </summary>
public enum FunctionsColumns : int
{
    Image = 0,
    Ordinal,
    Hint,
    Name,
    EntryPoint
}

/// <summary>
/// LVModules column indexes.
/// </summary>
public enum ModulesColumns : int
{
    Image = 0,
    Name,
    FileTimeStamp,
    LinkTimeStamp,
    FileSize,
    Attributes,
    LinkChecksum,
    RealChecksum,
    CPU,
    Subsystem,
    Symbols,
    PrefferedBase,
    //  ActualBase, //unused, profiling artifact
    VirtualSize,
    //  LoadOrder,  //unused, profiling artifact
    FileVer,
    ProductVer,
    ImageVer,
    LinkerVer,
    OSVer,
    SubsystemVer
}

public static class ModulesColumnsExtension
{
    /// <summary>
    /// Return ModulesColumns enum value as int.
    /// </summary>
    /// <param name="column"></param>
    /// <returns></returns>
    public static int ToInt(this ModulesColumns column)
    {
        return (int)column;
    }
}

public enum SearchOderIconType
{
    Magnifier = 0,
    Module,
    ModuleBad,
    Directory,
    DirectoryBad
}

public enum ToolBarIconType
{
    OpenFile = 0,
    SaveFile,
    Copy,
    AutoExpand,
    FullPaths,
    ViewUndecorated,
    ViewModulesInExternalViewer,
    Properties,
    SystemInformation,
    Configuration,
    ResolveAPISets
}

public enum LogMessageType
{
    Normal = 0,
    System,
    ErrorOrWarning,
    Information,
    ContentDefined
}

public enum FileViewUpdateAction
{
    TreeViewAutoExpandsChange,
    FunctionsUndecorateChange,
    ModulesTreeAndListChange
}

/// <summary>
/// Built-in file extensions declaration.
/// </summary>
static class InternalFileHandledExtensions
{
    public static List<PropertyElement> ExtensionList { get; } =
    [
        new PropertyElement("exe", "Application"),
        new PropertyElement("com", "Application"),
        new PropertyElement("dll", "Dynamic Link Library"),
        new PropertyElement("sys", "System File"),
        new PropertyElement("drv", "Driver File"),
        new PropertyElement("efi", "UEFI Runtime Module"),
        new PropertyElement("cpl", "Control Panel File"),
        new PropertyElement("bpl", "Borland Package Library"),
        new PropertyElement("tlb", "Type Library"),
        new PropertyElement("scr", "Screensaver Executable"),
        new PropertyElement("ocx", "ActiveX Control"),
        new PropertyElement("ax", "DirectShow Filter"),
        new PropertyElement("acm", "Audio Compression Manager Codec")
    ];

}

public enum SearchOrderType : uint
{
    WinSXS = 0,
    KnownDlls,
    ApplicationDirectory,
    System32Directory,
    SystemDirectory,
    WindowsDirectory,
    EnvironmentPathDirectories,
    SystemDriversDirectory,
    UserDefinedDirectory,
    None = 0xfff
}

public enum ToolBarThemeType : uint
{
    Classic = 0,
    Modern = 1
}

/// <summary>
/// SearchOrderTypes extension to return description of levels.
/// </summary>
public static class SearchOrderTypesExtension
{
    public static string ToDescription(this SearchOrderType searchOrder)
    {
        return searchOrder switch
        {
            SearchOrderType.WinSXS => "Side-by-side components",
            SearchOrderType.KnownDlls => "The system's KnownDlls list",
            SearchOrderType.WindowsDirectory => "The system's root OS directory",
            SearchOrderType.ApplicationDirectory => "The application directory",
            SearchOrderType.System32Directory => "The system directory",
            SearchOrderType.SystemDirectory => "The 16-bit system directory",
            SearchOrderType.EnvironmentPathDirectories => "The system's \"PATH\" environment variable directories",
            SearchOrderType.UserDefinedDirectory => CConsts.CategoryUserDefinedDirectory,
            _ => searchOrder.ToString()
        };
    }
}
