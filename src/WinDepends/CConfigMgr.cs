/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CCONFIGMGR.CS
*
*  VERSION:     1.00
*
*  DATE:        05 Jun 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace WinDepends;

/// <summary>
/// CConfiguration.
/// Class contains settings for the entire program.
/// </summary>
[DataContract]
public class CConfiguration
{
    [DataMember]
    public bool UpperCaseModuleNames { get; set; }
    [DataMember]
    public bool ShowToolBar { get; set; }
    [DataMember]
    public bool ShowStatusBar { get; set; }
    [DataMember]
    public int SortColumnExports { get; set; }
    [DataMember]
    public int SortColumnImports { get; set; }
    [DataMember]
    public int SortColumnModules { get; set; }
    [DataMember]
    public int ModuleNodeDepthMax { get; set; }
    [DataMember]
    public bool ViewUndecorated { get; set; }
    [DataMember]
    public bool ResolveAPIsets { get; set; }
    [DataMember]
    public bool FullPaths { get; set; }
    [DataMember]
    public bool AutoExpands { get; set; }
    [DataMember]
    public bool EscKeyEnabled { get; set; }
    [DataMember]
    public bool CompressSessionFiles { get; set; }
    [DataMember]
    public bool HistoryShowFullPath { get; set; }
    [DataMember]
    public bool ClearLogOnFileOpen { get; set; }
    [DataMember]
    public bool UseApiSetSchemaFile { get; set; }
    [DataMember]
    public bool UseStats { get; set; }
    [DataMember]
    public bool UseSymbols { get; set; }
    [DataMember]
    public bool ProcessRelocsForImage { get; set; }
    [DataMember]
    public bool UseCustomImageBase { get; set; }
    [DataMember]
    public bool AnalysisSettingsUseAsDefault { get; set; }
    [DataMember]
    public bool PropagateSettingsOnDependencies { get; set; }
    [DataMember]
    public bool HighlightApiSet { get; set; }
    [DataMember]
    public int HistoryDepth { get; set; }
    [DataMember]
    public string ApiSetSchemaFile { get; set; }
    [DataMember]
    public string ExternalViewerCommand { get; set; }
    [DataMember]
    public string ExternalViewerArguments { get; set; }
    [DataMember]
    public string ExternalFunctionHelpURL { get; set; }
    [DataMember]
    public string CoreServerAppLocation { get; set; }
    [DataMember]
    public string SymbolsDllPath { get; set; }
    [DataMember]
    public string SymbolsStorePath { get; set; }
    [DataMember]
    public uint CustomImageBase { get; set; }
    [DataMember]
    public ToolBarThemeType ToolBarTheme { get; set; }
    [DataMember]
    public Color SymbolsHighlightColor { get; set; }
    [DataMember]
    public List<SearchOrderType> SearchOrderListUM { get; set; }
    [DataMember]
    public List<SearchOrderType> SearchOrderListKM { get; set; }
    [DataMember]
    public List<string> UserSearchOrderDirectoriesUM { get; set; }
    [DataMember]
    public List<string> UserSearchOrderDirectoriesKM { get; set; }
    [DataMember]
    public List<string> MRUList { get; set; }
    [DataMember]
    public int WindowLeft { get; set; }
    [DataMember]
    public int WindowTop { get; set; }
    [DataMember]
    public int WindowWidth { get; set; }
    [DataMember]
    public int WindowHeight { get; set; }
    [DataMember]
    public int WindowState { get; set; }

    public CConfiguration()
    {
    }

    public CConfiguration(CConfiguration other)
    {
        UpperCaseModuleNames = other.UpperCaseModuleNames;
        ShowToolBar = other.ShowToolBar;
        ShowStatusBar = other.ShowStatusBar;
        SortColumnExports = other.SortColumnExports;
        SortColumnImports = other.SortColumnImports;
        SortColumnModules = other.SortColumnModules;
        ModuleNodeDepthMax = other.ModuleNodeDepthMax;
        ViewUndecorated = other.ViewUndecorated;
        ResolveAPIsets = other.ResolveAPIsets;
        FullPaths = other.FullPaths;
        AutoExpands = other.AutoExpands;
        EscKeyEnabled = other.EscKeyEnabled;
        CompressSessionFiles = other.CompressSessionFiles;
        HistoryShowFullPath = other.HistoryShowFullPath;
        ClearLogOnFileOpen = other.ClearLogOnFileOpen;
        UseApiSetSchemaFile = other.UseApiSetSchemaFile;
        ProcessRelocsForImage = other.ProcessRelocsForImage;
        UseStats = other.UseStats;
        UseSymbols = other.UseSymbols;
        UseCustomImageBase = other.UseCustomImageBase;
        AnalysisSettingsUseAsDefault = other.AnalysisSettingsUseAsDefault;
        PropagateSettingsOnDependencies = other.PropagateSettingsOnDependencies;
        HighlightApiSet = other.HighlightApiSet;
        HistoryDepth = other.HistoryDepth;
        ApiSetSchemaFile = other.ApiSetSchemaFile;
        ExternalViewerCommand = other.ExternalViewerCommand;
        ExternalViewerArguments = other.ExternalViewerArguments;
        ExternalFunctionHelpURL = other.ExternalFunctionHelpURL;
        CoreServerAppLocation = other.CoreServerAppLocation;
        SymbolsDllPath = other.SymbolsDllPath;
        SymbolsStorePath = other.SymbolsStorePath;
        CustomImageBase = other.CustomImageBase;
        ToolBarTheme = other.ToolBarTheme;
        SymbolsHighlightColor = other.SymbolsHighlightColor;

        WindowLeft = other.WindowLeft;
        WindowTop = other.WindowTop;
        WindowWidth = other.WindowWidth;
        WindowHeight = other.WindowHeight;
        WindowState = other.WindowState;

        SearchOrderListUM = new List<SearchOrderType>(other.SearchOrderListUM ??
        [
            SearchOrderType.WinSXS,
            SearchOrderType.KnownDlls,
            SearchOrderType.ApplicationDirectory,
            SearchOrderType.System32Directory,
            SearchOrderType.SystemDirectory,
            SearchOrderType.WindowsDirectory,
            SearchOrderType.EnvironmentPathDirectories,
            SearchOrderType.UserDefinedDirectory
        ]);

        SearchOrderListKM = new List<SearchOrderType>(other.SearchOrderListKM ??
        [
            SearchOrderType.System32Directory,
            SearchOrderType.SystemDriversDirectory,
            SearchOrderType.ApplicationDirectory,
            SearchOrderType.UserDefinedDirectory
        ]);

        UserSearchOrderDirectoriesUM = new List<string>(other.UserSearchOrderDirectoriesUM ?? []);
        UserSearchOrderDirectoriesKM = new List<string>(other.UserSearchOrderDirectoriesKM ?? []);
        MRUList = new List<string>(other.MRUList ?? []);
    }

    public CConfiguration(bool bSetDefault)
    {
        if (bSetDefault)
        {
            MRUList = [];
            UpperCaseModuleNames = true;
            ShowToolBar = true;
            ShowStatusBar = true;
            SortColumnModules = ModulesColumns.Name.ToInt();
            ModuleNodeDepthMax = CConsts.ModuleNodeDepthDefault;
            CompressSessionFiles = true;
            HistoryDepth = CConsts.HistoryDepthDefault;
            ExternalViewerCommand = Application.ExecutablePath;
            ExternalViewerArguments = "\"%1\"";
            ExternalFunctionHelpURL = CConsts.ExternalFunctionHelpURL;
            CustomImageBase = CConsts.DefaultAppStartAddress;
            UseApiSetSchemaFile = false;
            ProcessRelocsForImage = true;
            UseCustomImageBase = false;
            ToolBarTheme = ToolBarThemeType.Classic;

            WindowState = (int)FormWindowState.Normal;

            SymbolsStorePath = $"srv*{Path.Combine(Path.GetTempPath(), CConsts.SymbolsDefaultStoreDirectory)}{CConsts.SymbolsDownloadLink}";
            SymbolsDllPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CConsts.DbgHelpDll);
            SymbolsHighlightColor = Color.Yellow;

            string cpuArch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
            CoreServerAppLocation = $"{Path.GetDirectoryName(Application.ExecutablePath)}\\{CConsts.WinDependsCoreApp}.{cpuArch}.exe";

            SearchOrderListUM =
            [
                SearchOrderType.WinSXS,
                SearchOrderType.KnownDlls,
                SearchOrderType.ApplicationDirectory,
                SearchOrderType.System32Directory,
                SearchOrderType.SystemDirectory,
                SearchOrderType.WindowsDirectory,
                SearchOrderType.EnvironmentPathDirectories,
                SearchOrderType.UserDefinedDirectory
            ];

            UserSearchOrderDirectoriesUM = [];

            SearchOrderListKM =
            [
                SearchOrderType.System32Directory,
                SearchOrderType.SystemDriversDirectory,
                SearchOrderType.ApplicationDirectory,
                SearchOrderType.UserDefinedDirectory
            ];

            UserSearchOrderDirectoriesKM = [];
        }
    }

}

static class CConfigManager
{
    public static CConfiguration LoadConfiguration()
    {
        string cpuArch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        string fileName = $"{Path.GetDirectoryName(Application.ExecutablePath)}\\{CConsts.ShortProgramName}.{cpuArch}.settings.bin";

        if (!File.Exists(fileName))
        {
            return new CConfiguration(true);
        }

        try
        {
            CConfiguration confObj = (CConfiguration)CUtils.LoadPackedObjectFromFile(fileName, typeof(CConfiguration), null);

            //
            // Test configuration if it loaded, these lists cannot be null, if they are - configuration is broken, reset it.
            //
            if (confObj == null || confObj.MRUList == null || confObj.SearchOrderListUM == null || confObj.SearchOrderListKM == null)
            {
                return new CConfiguration(true);
            }

            if (confObj.ModuleNodeDepthMax < CConsts.ModuleNodeDepthMin)
            {
                confObj.ModuleNodeDepthMax = CConsts.ModuleNodeDepthDefault;
            }

            return confObj;
        }
        catch
        {
            return new CConfiguration(true);
        }

    }

    public static void SaveConfiguration(CConfiguration configuration)
    {
        string cpuArch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        string fileName = $"{Path.GetDirectoryName(Application.ExecutablePath)}\\{CConsts.ShortProgramName}.{cpuArch}.settings.bin";

        try
        {
            CUtils.SavePackedObjectToFile(fileName, configuration, typeof(CConfiguration), null);
        }
        catch { }
    }
}
