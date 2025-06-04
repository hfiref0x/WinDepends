/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CPATHRESOLVER.CS
*
*  VERSION:     1.00
*
*  DATE:        03 Jun 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Reflection.PortableExecutable;
using System.Text;

namespace WinDepends;

/// <summary>
/// Resolves file paths for modules according to Windows loading rules.
/// </summary>
public static class CPathResolver
{
    public static string WindowsDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    public static string System16Directory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), CConsts.HostSys16Dir);
    public static string System32Directory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.System);
    public static string SystemDriversDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CConsts.DriversDir);
    public static string SysWowDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

    public static bool Initialized { get; set; }
    public static string MainModuleFileName { get; private set; } = string.Empty;
    public static string CurrentDirectory { get; private set; } = string.Empty;
    public static List<string> UserDirectoriesUM { get; set; } = [];
    public static List<string> UserDirectoriesKM { get; set; } = [];

    public static string KnownDllsPath { get; set; } = string.Empty;
    public static string KnownDllsPath32 { get; set; } = string.Empty;
    public static string[] PathEnvironment { get; } =
        Environment.GetEnvironmentVariable("PATH").Split(";", StringSplitOptions.RemoveEmptyEntries) ?? [];
    public static List<string> KnownDlls { get; set; } = [];
    public static List<string> KnownDlls32 { get; set; } = [];

    static CSxsEntries ManifestSxsDependencies = [];
    public static CActCtxHelper ActCtxHelper { get; set; }
    static bool AutoElevate { get; set; }

    /// <summary>
    /// Queries information about the main module and initializes the resolver.
    /// </summary>
    /// <param name="module">The main module to analyze.</param>
    public static void QueryFileInformation(CModule module)
    {
        if (Initialized)
        {
            return;
        }

        MainModuleFileName = module.FileName;
        CurrentDirectory = Path.GetDirectoryName(MainModuleFileName) ?? string.Empty;

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

    /// <summary>
    /// Searches for the file in environment PATH directories.
    /// </summary>
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

    /// <summary>
    /// Searches for the file in the application manifest.
    /// </summary>
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

    /// <summary>
    /// Searches for the file in the specified application directory.
    /// </summary>
    static internal string PathFromApplicationDirectory(string fileName, string directoryName)
    {
        if (string.IsNullOrEmpty(directoryName))
        {
            return string.Empty;
        }

        string result = Path.Combine(directoryName, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    /// <summary>
    /// Searches for the file in the Windows directory.
    /// </summary>
    static internal string PathFromWindowsDirectory(string fileName)
    {
        string result = Path.Combine(WindowsDirectory, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    /// <summary>
    /// Searches for the file in the System directory.
    /// </summary>
    static internal string PathFromSystem16Directory(string fileName)
    {
        string result = Path.Combine(System16Directory, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    /// <summary>
    /// Searches for the file in the system drivers directory.
    /// </summary>
    static internal string PathFromSystemDriversDirectory(string fileName)
    {
        string result = Path.Combine(SystemDriversDirectory, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    /// <summary>
    /// Searches for the file in the user-specified directories.
    /// </summary>
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

    /// <summary>
    /// Searches for the file in the appropriate system directory.
    /// </summary>
    static internal string PathFromSystemDirectory(string fileName, bool Is64bitFile = true)
    {
        string sysDirectory = Is64bitFile ? System32Directory : SysWowDirectory;
        string result = Path.Combine(sysDirectory, fileName);
        return File.Exists(result) ? result : string.Empty;
    }

    /// <summary>
    /// Checks if the file is in the KnownDlls registry list.
    /// </summary>
    static internal bool FileIsInKnownDlls(string fileName, bool Is64bitFile = true)
    {
        List<string> dllsList = Is64bitFile ? KnownDlls : KnownDlls32;

        return dllsList.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the path to a file from the KnownDlls registry key.
    /// </summary>
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

    /// <summary>
    /// Determines the replacement directory based on CPU architecture.
    /// </summary>
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

    /// <summary>
    /// Applies file path architecture redirection for cross-architecture loading.
    /// </summary>
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

    /// <summary>
    /// Searches for a file in the application's .local directory.
    /// </summary>
    static internal string PathFromDotLocal(string applicationName, string fileName)
    {
        // DotLocal
        // Opportunistic search, find the first matching file.
        // Fixme: properly handle file name generation?
        string dotLocalDir = $"{applicationName}.local";
        if (!Directory.Exists(dotLocalDir))
            return string.Empty;

        try
        {
            string[] subdirectories = Directory.GetDirectories(dotLocalDir);
            foreach (string dir in subdirectories)
            {
                string filePath = Path.Combine(dir, fileName);
                if (File.Exists(filePath))
                    return filePath;
            }
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Searches for a file in the WinSXS directory using activation context.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <returns>The full path of the found file or an empty string if not found.</returns>
    static internal string PathFromWinSXS(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        bool needsDeactivation = false;
        IntPtr cookie = IntPtr.Zero;

        try
        {
            if (ActCtxHelper.ActivateContext())
            {
                needsDeactivation = true;
            }
            else
            {
                // If activation failed, try deactivating current context
                cookie = ActCtxHelper.DeactivateCurrentContext();
                if (cookie == IntPtr.Zero)
                    return string.Empty;
            }

            uint bufferSize = 2048;
            StringBuilder path = new((int)bufferSize);

            uint charsCopied = NativeMethods.SearchPath(null, fileName, null, bufferSize, path, out _);
            if (charsCopied > bufferSize)
            {
                // Buffer was too small
                path.Capacity = (int)charsCopied;
                charsCopied = NativeMethods.SearchPath(null, fileName, null, charsCopied, path, out _);
            }

            return charsCopied > 0 ? path.ToString(0, (int)charsCopied) : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
        finally
        {
            if (needsDeactivation)
            {
                ActCtxHelper.DeactivateContext();
            }
            else if (cookie != IntPtr.Zero)
            {
                ActCtxHelper.ReactivateContext(cookie);
            }
        }
    }

    /// <summary>
    /// Resolves a kernel module path based on search order.
    /// </summary>
    private static string ResolveKernelModulePath(
        string fileName,
        List<SearchOrderType> searchOrderKM,
        bool is64bitMachine,
        out SearchOrderType resolver)
    {
        resolver = SearchOrderType.None;
        string result;

        foreach (var searchOrder in searchOrderKM)
        {
            result = searchOrder switch
            {
                SearchOrderType.SystemDriversDirectory => PathFromSystemDriversDirectory(fileName),
                SearchOrderType.System32Directory => PathFromSystemDirectory(fileName, is64bitMachine),
                SearchOrderType.ApplicationDirectory => PathFromApplicationDirectory(fileName, CurrentDirectory),
                SearchOrderType.UserDefinedDirectory => PathFromUserDirectory(fileName, UserDirectoriesKM),
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(result))
            {
                resolver = searchOrder;
                return result;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves a path based on a specific search order type.
    /// </summary>
    private static string ResolveBySearchOrder(SearchOrderType searchOrder, string fileName, bool is64bitMachine, bool needRedirection)
    {
        string resolvedPath = string.Empty;

        if (searchOrder == SearchOrderType.WinSXS)
        {
            // Check .local first.
            resolvedPath = PathFromDotLocal(MainModuleFileName, fileName);
            if (!string.IsNullOrEmpty(resolvedPath))
            {
                return resolvedPath;
            }

            // Do not perform search if the target cpu architecture is different from system cpu architecture.
            // This is to avoid mass fp, as we cannot ensure proper search without taking "half" of Windows code inside and
            // should keep the resolution as simple as possible.
            if (!needRedirection)
            {
                resolvedPath = PathFromManifest(fileName, is64bitMachine);
                if (!string.IsNullOrEmpty(resolvedPath))
                {
                    return resolvedPath;
                }

                // Resolve path using activation context.
                return PathFromWinSXS(fileName);
            }
        }

        return searchOrder switch
        {
            SearchOrderType.ApplicationDirectory => PathFromApplicationDirectory(fileName, CurrentDirectory),
            SearchOrderType.WindowsDirectory => PathFromWindowsDirectory(fileName),
            SearchOrderType.EnvironmentPathDirectories => PathFromEnvironmentPathDirectories(fileName),
            SearchOrderType.System32Directory => PathFromSystemDirectory(fileName, is64bitMachine),
            SearchOrderType.SystemDirectory => PathFromSystem16Directory(fileName),
            SearchOrderType.KnownDlls => PathFromKnownDlls(fileName, is64bitMachine),
            SearchOrderType.UserDefinedDirectory => PathFromUserDirectory(fileName, UserDirectoriesUM),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Resolves a user mode module path based on search order.
    /// </summary>
    private static string ResolveUserModulePath(
        string fileName,
        List<SearchOrderType> searchOrderUM,
        bool is64bitMachine,
        bool needRedirection,
        ushort moduleMachine,
        out SearchOrderType resolver)
    {
        resolver = SearchOrderType.None;
        string result;

        foreach (var searchOrder in searchOrderUM)
        {
            result = ResolveBySearchOrder(searchOrder, fileName, is64bitMachine, needRedirection);
            if (!string.IsNullOrEmpty(result))
            {
                resolver = searchOrder;

                // Apply architecture redirection if needed
                if (needRedirection &&
                    resolver != SearchOrderType.KnownDlls &&
                    resolver != SearchOrderType.ApplicationDirectory)
                {
                    result = ApplyFilePathArchRedirection(result, moduleMachine);
                }

                return result;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves the full path for a module based on search order rules.
    /// </summary>
    static internal string ResolvePathForModule(string partiallyResolvedFileName,
                                                CModule module,
                                                List<SearchOrderType> searchOrderUM,
                                                List<SearchOrderType> searchOrderKM,
                                                out SearchOrderType resolver)
    {
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
        if (!needRedirection && module.IsKernelModule)
        {
            return ResolveKernelModulePath(partiallyResolvedFileName, searchOrderKM, is64bitMachine, out resolver);
        }

        // UM module resolving.
        return ResolveUserModulePath(partiallyResolvedFileName,
            searchOrderUM,
            is64bitMachine,
            needRedirection,
            moduleMachine,
            out resolver);
    }
}
