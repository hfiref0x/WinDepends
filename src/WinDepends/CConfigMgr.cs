/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CCONFIGMGR.CS
*
*  VERSION:     1.00
*
*  DATE:        28 Nov 2024
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.InteropServices;

namespace WinDepends;

/// <summary>
/// CConfiguration.
/// Class contains settings for the entire program.
/// </summary>
[Serializable()]
public class CConfiguration
{
    public bool UppperCaseModuleNames { get; set; }
    public bool ShowToolBar { get; set; }
    public bool ShowStatusBar { get; set; }
    public int SortColumnExports { get; set; }
    public int SortColumnImports { get; set; }
    public int SortColumnModules { get; set; }
    public int ModuleNodeDepthMax { get; set; }
    public bool ViewUndecorated { get; set; }
    public bool ResolveAPIsets { get; set; }
    public bool FullPaths { get; set; }
    public bool AutoExpands { get; set; }
    public bool EscKeyEnabled { get; set; }
    public bool CompressSessionFiles { get; set; }
    public bool HistoryShowFullPath { get; set; }
    public bool ClearLogOnFileOpen { get; set; }
    public bool UseApiSetSchema { get; set; }
    public bool UseRelocForImages { get; set; }

    public bool HighlightApiSet { get; set; }
    public int HistoryDepth { get; set; }
    public string ExternalViewerCommand { get; set; }
    public string ExternalViewerArguments { get; set; }
    public string ExternalFunctionHelpURL { get; set; }
    public string CoreServerAppLocation { get; set; }

    public uint MinAppAddress { get; set; }

    public List<SearchOrderType> SearchOrderListUM { get; set; }
    public List<SearchOrderType> SearchOrderListKM { get; set; }
    public List<string> UserSearchOrderDirectoriesUM { get; set; }
    public List<string> UserSearchOrderDirectoriesKM { get; set; }

    public List<string> MRUList { get; set; }

    public CConfiguration()
    {
    }

    public CConfiguration(bool bSetDefault)
    {
        if (bSetDefault)
        {
            MRUList = [];
            UppperCaseModuleNames = true;
            ShowToolBar = true;
            ShowStatusBar = true;
            SortColumnModules = ModulesColumns.Name.ToInt();
            ModuleNodeDepthMax = CConsts.ModuleNodeDepthDefault;
            CompressSessionFiles = true;
            HistoryDepth = CConsts.HistoryDepthDefault;
            ExternalViewerCommand = Application.ExecutablePath;
            ExternalViewerArguments = "\"%1\"";
            ExternalFunctionHelpURL = CConsts.ExternalFunctionHelpURL;
            MinAppAddress = CConsts.DefaultAppStartAddress;

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
            var result = (CConfiguration)CUtils.LoadPackedObjectFromFile(fileName, typeof(CConfiguration), null);
            if (result == null)
            {
                return new CConfiguration(true);
            }
            else
            {
                return result;
            }
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
