/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CPATHRESOLVER.CS
*
*  VERSION:     1.00
*
*  DATE:        07 Dec 2024
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDepends;

public static class CPathResolver
{
    public static bool Initialized { get; set; }
    public static string MainModuleFileName { get; private set; }
    public static string CurrentDirectory { get; private set; }
    public static string WindowsDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    public static string System16Directory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), CConsts.HostSys16Dir);
    public static string System32Directory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.System);
    public static string SystemDriversDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CConsts.DriversDir);
    public static string SysWowDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

    public static List<string> UserDirectoriesUM { get; set; } = [];
    public static List<string> UserDirectoriesKM { get; set; } = [];

    public static string KnownDllsPath { get; set; }
    public static string KnownDllsPath32 { get; set; }
    public static string[] PathEnvironment { get; } = Environment.GetEnvironmentVariable("PATH").Split(";", StringSplitOptions.RemoveEmptyEntries);
    public static List<string> KnownDlls { get; set; } = [];
    public static List<string> KnownDlls32 { get; set; } = [];

    static CSxsEntries ManifestSxsDependencies = [];
    public static CActCtxHelper ActCtxHelper { get; set; }
    static bool AutoElevate { get; set; }

    public static void QueryFileInformation(CModule module)
    {
        if (Initialized)
        {
            return;
        }

        MainModuleFileName = module.FileName;
        CurrentDirectory = Path.GetDirectoryName(MainModuleFileName);

        if (ManifestSxsDependencies.Count > 0)
        {
            ManifestSxsDependencies.Clear();
        }

        // Skip native and dlls.
        if ((module.ModuleData.Characteristics & NativeMethods.IMAGE_FILE_DLL) == 0 &&
            (module.ModuleData.Subsystem != NativeMethods.IMAGE_SUBSYSTEM_NATIVE))
        {
            ManifestSxsDependencies = CSxsManifest.GetManifestInformation(module, CurrentDirectory, out bool bAutoElevate);
            AutoElevate = bAutoElevate;
        }
        Initialized = true;
    }

    static internal string PathFromEnvironmentPathDirectories(string fileName)
    {
        foreach (string path in PathEnvironment)
        {
            var result = Path.Combine(path, fileName);
            if (File.Exists(result))
            {
                return result;
            }
        }

        return string.Empty;
    }

    static internal string PathFromManifest(string fileName, bool Is64bitFile = true)
    {
        if (!FileIsInKnownDlls(fileName, Is64bitFile))
        {
            foreach (var sxsEntry in ManifestSxsDependencies)
            {
                if (Path.GetFileName(sxsEntry.FilePath).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return sxsEntry.FilePath;
                }
            }

        }

        return string.Empty;
    }

    static internal string PathFromApplicationDirectory(string fileName, string directoryName)
    {
        if (string.IsNullOrEmpty(directoryName))
        {
            return string.Empty;
        }

        string result = Path.Combine(directoryName, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    static internal string PathFromWindowsDirectory(string fileName)
    {
        string result = Path.Combine(WindowsDirectory, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    static internal string PathFromSystem16Directory(string fileName)
    {
        string result = Path.Combine(System16Directory, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    static internal string PathFromSystemDriversDirectory(string fileName)
    {
        string result = Path.Combine(SystemDriversDirectory, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    static internal string PathFromUserDirectory(string fileName, List<string> userDirectories)
    {
        string result;
        foreach (var entry in userDirectories)
        {
            result = Path.Combine(entry, fileName);
            if (File.Exists(result))
            {
                return result;
            }
        }

        return string.Empty;
    }

    static internal string PathFromSystemDirectory(string fileName, bool Is64bitFile = true)
    {
        string sysDirectory = Is64bitFile ? System32Directory : SysWowDirectory;
        string result = Path.Combine(sysDirectory, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    static internal bool FileIsInKnownDlls(string fileName, bool Is64bitFile = true)
    {
        List<string> dllsList = Is64bitFile ? KnownDlls : KnownDlls32;

        return dllsList.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }

    static internal string PathFromKnownDlls(string fileName, bool Is64bitFile = true)
    {
        List<string> dllsList = (Is64bitFile) ? KnownDlls : KnownDlls32;
        string dllsDir = (Is64bitFile) ? KnownDllsPath : KnownDllsPath32;

        foreach (string dll in dllsList)
        {
            if (string.Equals(dll, fileName, StringComparison.OrdinalIgnoreCase))
            {
                string result = Path.Combine(dllsDir, fileName);
                return File.Exists(result) ? result : string.Empty;
            }
        }

        return string.Empty;
    }

    static string GetReplacementDirectory(ushort cpuArchitecture)
    {
        return cpuArchitecture switch
        {
            (ushort)Machine.I386 => "SysWOW64",
            (ushort)Machine.ArmThumb2 => "SysArm32",
            (ushort)Machine.Amd64 => "SysX8664",
            (ushort)Machine.Arm64 => "SysArm64",
            _ => CConsts.HostSysDir,
        };
    }

    static internal string ApplyFilePathArchRedirection(string filePath, ushort cpuArchitecture)
    {
        string result = filePath;

        string[] pathRedirectExempt =
        [
            "system32\\catroot",
            "system32\\catroot2",
            "system32\\driverstore",
            "system32\\drivers\\etc",
            "system32\\logfiles",
            "system32\\spool"
        ];
        string directoryPart;

        // Search the exempt directories.
        foreach (var element in pathRedirectExempt)
        {
            directoryPart = Path.Combine(WindowsDirectory, element);
            if (filePath.Contains(directoryPart, StringComparison.OrdinalIgnoreCase))
            {
                return filePath; // Leave as is if it contains exempt directory.
            }
        }

        // Only checks for "windows\system32" bulk in a path.
        directoryPart = Path.Combine(WindowsDirectory, CConsts.HostSysDir);
        if (filePath.Contains(directoryPart, StringComparison.OrdinalIgnoreCase))
        {
            result = filePath.Replace(CConsts.HostSysDir, GetReplacementDirectory(cpuArchitecture), StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    static internal string PathFromDotLocal(string applicationName, string fileName)
    {
        // DotLocal
        // Opportunistic search, find the first matching file.
        // Fixme: properly handle file name generation?
        string dotLocalDir = $"{applicationName}.local";
        if (Directory.Exists(dotLocalDir))
        {
            var subDirs = Directory.GetDirectories(dotLocalDir);
            foreach (var dir in subDirs)
            {
                string dotLocalFileName = Path.Combine(dir, fileName);
                if (File.Exists(dotLocalFileName))
                {
                    return dotLocalFileName;
                }
            }
        }

        return string.Empty;
    }

    static internal string PathFromWinSXS(string fileName)
    {
        string result = string.Empty;
        if (ActCtxHelper.ActivateContext())
        {
            StringBuilder sbOut = new(2048);
            uint res = NativeMethods.SearchPath(null, fileName, null, 2048, sbOut, out _);
            if (res == 0)
            {
                if (Marshal.GetLastWin32Error() == 0x7A) // ERROR_INSUFFICIENT_BUFFER
                {
                    sbOut.Clear();
                    sbOut.Capacity = (int)res;
                    res = NativeMethods.SearchPath(null, fileName, null, (uint)sbOut.Capacity, sbOut, out _);
                }
            }

            ActCtxHelper.DeactivateContext();

            if (res != 0)
            {
                result = sbOut.ToString();
            }
        }

        return result;
    }

    static internal string ResolvePathForModule(string partiallyResolvedFileName,
                                                CModule module,
                                                List<SearchOrderType> searchOrderUM,
                                                List<SearchOrderType> searchOrderKM,
                                                out SearchOrderType resolver)
    {
        string result;
        SearchOrderType resolvedBy = SearchOrderType.None;
        bool is64bitMachine = module.Is64bitArchitecture();
        ushort moduleMachine = module.ModuleData.Machine;

        var needRedirection = CUtils.SystemProcessorArchitecture switch
        {
            NativeMethods.PROCESSOR_ARCHITECTURE_INTEL => (moduleMachine != (ushort)Machine.I386),
            NativeMethods.PROCESSOR_ARCHITECTURE_AMD64 => (moduleMachine != (ushort)Machine.Amd64),
            NativeMethods.PROCESSOR_ARCHITECTURE_IA64 => (moduleMachine != (ushort)Machine.IA64),
            // FIXME
            NativeMethods.PROCESSOR_ARCHITECTURE_ARM64 => (moduleMachine != (ushort)Machine.Arm64),
            _ => false,
        };

        // KM module resolving.
        if (needRedirection == false && module.IsKernelModule)
        {
            foreach (var entry in searchOrderKM)
            {
                result = string.Empty;
                switch (entry)
                {
                    case SearchOrderType.SystemDriversDirectory:
                        result = PathFromSystemDriversDirectory(partiallyResolvedFileName);
                        resolvedBy = SearchOrderType.SystemDriversDirectory;
                        break;

                    case SearchOrderType.System32Directory:
                        result = PathFromSystemDirectory(partiallyResolvedFileName, is64bitMachine);
                        resolvedBy = SearchOrderType.System32Directory;
                        break;

                    case SearchOrderType.ApplicationDirectory:
                        result = PathFromApplicationDirectory(partiallyResolvedFileName, CurrentDirectory);
                        resolvedBy = SearchOrderType.ApplicationDirectory;
                        break;
                    case SearchOrderType.UserDefinedDirectory:
                        result = PathFromUserDirectory(partiallyResolvedFileName, UserDirectoriesKM);
                        resolvedBy = SearchOrderType.UserDefinedDirectory;
                        break;
                }

                if (!string.IsNullOrEmpty(result))
                {
                    resolver = resolvedBy;
                    return result;
                }
            }

            resolver = resolvedBy;
            return string.Empty;
        }

        // UM module resolving.
        foreach (var entry in searchOrderUM)
        {
            result = string.Empty;

            switch (entry)
            {
                case SearchOrderType.WinSXS:

                    result = PathFromDotLocal(MainModuleFileName, partiallyResolvedFileName);
                    if (!string.IsNullOrEmpty(result))
                    {
                        resolvedBy = SearchOrderType.WinSXS;
                        break;
                    }

                    // Do not perform search if the target cpu architecture is different from system cpu architecture.
                    // This is to avoid mass fp, as we cannot ensure proper search without taking "half" of Windows code inside and
                    // should keep the resolution as simple as possible.
                    if (!needRedirection)
                    {
                        result = PathFromManifest(partiallyResolvedFileName, is64bitMachine);
                        if (string.IsNullOrEmpty(result))
                        {

                            //
                            // Resolve path using activation context.
                            //
                            result = PathFromWinSXS(partiallyResolvedFileName);
                        }
                        resolvedBy = SearchOrderType.WinSXS;
                    }
                    break;

                case SearchOrderType.ApplicationDirectory:
                    result = PathFromApplicationDirectory(partiallyResolvedFileName, CurrentDirectory);
                    resolvedBy = SearchOrderType.ApplicationDirectory;
                    break;

                case SearchOrderType.WindowsDirectory:
                    result = PathFromWindowsDirectory(partiallyResolvedFileName);
                    resolvedBy = SearchOrderType.WindowsDirectory;
                    break;

                case SearchOrderType.EnvironmentPathDirectories:
                    result = PathFromEnvironmentPathDirectories(partiallyResolvedFileName);
                    resolvedBy = SearchOrderType.EnvironmentPathDirectories;
                    break;

                case SearchOrderType.System32Directory:
                    result = PathFromSystemDirectory(partiallyResolvedFileName, is64bitMachine);
                    resolvedBy = SearchOrderType.System32Directory;
                    break;

                case SearchOrderType.SystemDirectory:
                    result = PathFromSystem16Directory(partiallyResolvedFileName);
                    resolvedBy = SearchOrderType.SystemDirectory;
                    break;

                case SearchOrderType.KnownDlls:
                    result = PathFromKnownDlls(partiallyResolvedFileName, is64bitMachine);
                    resolvedBy = SearchOrderType.KnownDlls;
                    break;

                case SearchOrderType.UserDefinedDirectory:
                    result = PathFromUserDirectory(partiallyResolvedFileName, UserDirectoriesUM);
                    resolvedBy = SearchOrderType.UserDefinedDirectory;
                    break;

            }

            if (!string.IsNullOrEmpty(result))
            {
                if (needRedirection &&
                    (resolvedBy != SearchOrderType.KnownDlls &&
                    resolvedBy != SearchOrderType.ApplicationDirectory))
                {
                    result = ApplyFilePathArchRedirection(result, moduleMachine);
                }

                resolver = resolvedBy;
                return result;
            }

        }

        resolver = resolvedBy;
        return string.Empty;
    }
}
