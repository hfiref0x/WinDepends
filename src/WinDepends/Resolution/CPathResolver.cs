/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CPATHRESOLVER.CS
*
*  VERSION:     1.00
*
*  DATE:        24 Dec 2025
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
    private const uint InitialSearchPathBufferSize = 2048;
    private const uint MaxSearchPathBufferSize = (uint)int.MaxValue;

    public static string WindowsDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    public static string WinSxSDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), CConsts.WinSxSDir);
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
            (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(";", StringSplitOptions.RemoveEmptyEntries);
    private static readonly List<string> _knownDllsList = [];
    private static readonly List<string> _knownDlls32List = [];
    private static HashSet<string> _knownDllsSet = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _knownDlls32Set = new(StringComparer.OrdinalIgnoreCase);

    // Cache for SearchPath results obtained under activation context that are NOT coming
    // from the WinSxS folder. These cached candidates are only used as a last resort
    // (when all other resolution methods failed).
    private static readonly Dictionary<string, string> _winsxsSearchPathCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _winsxsCacheLock = new();

    /// <summary>
    /// Gets or sets the list of known 64-bit DLLs. Setting this property updates the internal lookup cache.
    /// </summary>
    public static List<string> KnownDlls
    {
        get => _knownDllsList;
        set
        {
            HashSet<string> newSet;

            _knownDllsList.Clear();

            if (value != null)
            {
                _knownDllsList.AddRange(value);
                newSet = new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                newSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            _knownDllsSet = newSet;
        }
    }

    /// <summary>
    /// Gets or sets the list of known 32-bit DLLs. Setting this property updates the internal lookup cache.
    /// </summary>
    public static List<string> KnownDlls32
    {
        get => _knownDlls32List;
        set
        {
            HashSet<string> newSet;

            _knownDlls32List.Clear();

            if (value != null)
            {
                _knownDlls32List.AddRange(value);
                newSet = new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                newSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            _knownDlls32Set = newSet;
        }
    }

    static CSxsEntries ManifestSxsDependencies = [];
    public static CActCtxHelper ActCtxHelper { get; set; }
    static bool AutoElevate { get; set; }

    /// <summary>
    /// Synchronizes the internal HashSet with the public List for KnownDlls.
    /// Call this after external code populates the KnownDlls list.
    /// </summary>
    public static void SyncKnownDllsCache()
    {
        HashSet<string> newSet, newSet32;

        newSet = new HashSet<string>(_knownDllsList, StringComparer.OrdinalIgnoreCase);
        newSet32 = new HashSet<string>(_knownDlls32List, StringComparer.OrdinalIgnoreCase);

        _knownDllsSet = newSet;
        _knownDlls32Set = newSet32;
    }

    public static void ClearWinSxSSearchPathCache()
    {
        lock (_winsxsCacheLock)
        {
            _winsxsSearchPathCache.Clear();
        }
    }

    /// <summary>
    /// Queries information about the main module and initializes the resolver.
    /// </summary>
    /// <param name="module">The main module to analyze.</param>
    public static void QueryFileInformation(CModule module)
    {
        if (module == null)
        {
            Initialized = false;
            return;
        }

        if (Initialized)
        {
            return;
        }

        // Clear cached non-WinSxS search path candidates for previous scan.
        ClearWinSxSSearchPathCache();

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
    /// Combines a directory path with a file name and validates the resulting path exists.
    /// </summary>
    /// <param name="directory">The directory path.</param>
    /// <param name="fileName">The file name to combine.</param>
    /// <returns>The full path if the file exists, otherwise an empty string.</returns>
    private static string CombineAndValidatePath(string directory, string fileName)
    {
        string result;

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            return string.Empty;

        try
        {
            result = Path.Combine(directory, fileName);
            return File.Exists(result) ? result : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Searches for the file in environment PATH directories.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <returns>The full path if found, otherwise an empty string.</returns>
    static internal string PathFromEnvironmentPathDirectories(string fileName)
    {
        foreach (string path in PathEnvironment)
        {
            string result = CombineAndValidatePath(path, fileName);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Searches for the file in the application manifest.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <param name="Is64bitFile">True for 64-bit files, false for 32-bit files.</param>
    /// <returns>The full path if found in manifest, otherwise an empty string.</returns>
    static internal string PathFromManifest(string fileName, bool Is64bitFile = true)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        if (!FileIsInKnownDlls(fileName, Is64bitFile))
        {
            foreach (var sxsEntry in ManifestSxsDependencies)
            {
                if (sxsEntry?.FilePath == null)
                    continue;

                string sxsFileName = Path.GetFileName(sxsEntry.FilePath);

                if (string.Equals(sxsFileName, fileName, StringComparison.OrdinalIgnoreCase))
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
    /// <param name="fileName">The file name to search for.</param>
    /// <param name="directoryName">The directory to search in.</param>
    /// <returns>The full path if found, otherwise an empty string.</returns>
    static internal string PathFromApplicationDirectory(string fileName, string directoryName)
    {
        return CombineAndValidatePath(directoryName, fileName);
    }

    /// <summary>
    /// Searches for the file in the Windows directory.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <returns>The full path if found, otherwise an empty string.</returns>
    static internal string PathFromWindowsDirectory(string fileName)
    {
        return CombineAndValidatePath(WindowsDirectory, fileName);
    }

    /// <summary>
    /// Searches for the file in the System directory.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <returns>The full path if found, otherwise an empty string.</returns>
    static internal string PathFromSystem16Directory(string fileName)
    {
        return CombineAndValidatePath(System16Directory, fileName);
    }

    /// <summary>
    /// Searches for the file in the system drivers directory.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <returns>The full path if found, otherwise an empty string.</returns>
    static internal string PathFromSystemDriversDirectory(string fileName)
    {
        return CombineAndValidatePath(SystemDriversDirectory, fileName);
    }

    /// <summary>
    /// Searches for the file in the user-specified directories.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <param name="userDirectories">List of user-defined search directories.</param>
    /// <returns>The full path if found, otherwise an empty string.</returns>
    static internal string PathFromUserDirectory(string fileName, List<string> userDirectories)
    {
        string result;

        if (userDirectories == null || userDirectories.Count == 0)
            return string.Empty;

        foreach (var entry in userDirectories)
        {
            result = CombineAndValidatePath(entry, fileName);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Searches for the file in the appropriate system directory.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <param name="Is64bitFile">True for System32, false for SysWOW64.</param>
    /// <returns>The full path if found, otherwise an empty string.</returns>
    static internal string PathFromSystemDirectory(string fileName, bool Is64bitFile = true)
    {
        string sysDirectory = Is64bitFile ? System32Directory : SysWowDirectory;
        return CombineAndValidatePath(sysDirectory, fileName);
    }

    /// <summary>
    /// Checks if the file is in the KnownDlls list.
    /// </summary>
    /// <param name="fileName">The file name to check.</param>
    /// <param name="Is64bitFile">True for 64-bit known DLLs, false for 32-bit.</param>
    /// <returns>True if the file is in the KnownDlls list, otherwise false.</returns>
    static internal bool FileIsInKnownDlls(string fileName, bool Is64bitFile = true)
    {
        HashSet<string> dllsSet;

        if (string.IsNullOrEmpty(fileName))
            return false;

        dllsSet = Is64bitFile ? _knownDllsSet : _knownDlls32Set;
        return dllsSet.Contains(fileName);
    }

    /// <summary>
    /// Gets the path to a file from the KnownDlls key.
    /// </summary>
    /// <param name="fileName">The file name to resolve.</param>
    /// <param name="Is64bitFile">True for 64-bit known DLLs, false for 32-bit.</param>
    /// <returns>The full path if the file is a known DLL and exists, otherwise an empty string.</returns>
    static internal string PathFromKnownDlls(string fileName, bool Is64bitFile = true)
    {
        string dllsDir;

        if (!FileIsInKnownDlls(fileName, Is64bitFile))
            return string.Empty;

        dllsDir = Is64bitFile ? KnownDllsPath : KnownDllsPath32;
        return CombineAndValidatePath(dllsDir, fileName);
    }

    /// <summary>
    /// Determines the replacement directory based on CPU architecture.
    /// </summary>
    /// <param name="cpuArchitecture">The CPU architecture identifier.</param>
    /// <returns>The replacement directory name for the specified architecture.</returns>
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
    /// <param name="filePath">The file path to redirect.</param>
    /// <param name="cpuArchitecture">The target CPU architecture.</param>
    /// <returns>The redirected path if applicable, otherwise the original path.</returns>
    static internal string ApplyFilePathArchRedirection(string filePath, ushort cpuArchitecture)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

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
    /// <param name="applicationName">The application name to build the .local directory path.</param>
    /// <param name="fileName">The file name to search for.</param>
    /// <returns>The full path if found, otherwise an empty string.</returns>
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
    /// Behavior: if SearchPath result is from WinSxS folder, return it.
    /// Otherwise cache the candidate and return empty; cached candidates are used only when
    /// all other resolution methods failed.
    /// </summary>
    /// <param name="fileName">The file name to search for.</param>
    /// <returns>The full path of the found file from WinSxS or empty if non-WinSxS candidate (cached).</returns>
    static internal string PathFromWinSXS(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        if (ActCtxHelper == null)
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

            uint bufferSize = InitialSearchPathBufferSize;
            StringBuilder path = new((int)bufferSize);

            uint charsCopied = NativeMethods.SearchPath(null, fileName, null, bufferSize, path, out _);
            if (charsCopied > bufferSize)
            {
                if (charsCopied > MaxSearchPathBufferSize)
                    return string.Empty;

                // Buffer was too small
                path.Capacity = (int)charsCopied;
                charsCopied = NativeMethods.SearchPath(null, fileName, null, charsCopied, path, out _);
            }

            if (charsCopied == 0)
                return string.Empty;

            string found = path.ToString(0, (int)charsCopied);

            // Check candidate to be from WinSxS.
            if (!string.IsNullOrEmpty(found) &&
                found.IndexOf(WinSxSDirectory, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Candidate from WinSxS, allow it as result.
                return found;
            }

            // Candidate not from WinSxS: cache it for later fallback and do not return it now.
            lock (_winsxsCacheLock)
            {
                _winsxsSearchPathCache[fileName] = found;
            }

            return string.Empty;

        }
        catch (OutOfMemoryException)
        {
            return string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
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
    /// <param name="fileName">The file name to resolve.</param>
    /// <param name="searchOrderKM">Kernel mode search order list.</param>
    /// <param name="is64bitMachine">True if the target is 64-bit architecture.</param>
    /// <param name="resolver">Output parameter indicating which resolver found the file.</param>
    /// <returns>The resolved path if found, otherwise an empty string.</returns>
    private static string ResolveKernelModulePath(
        string fileName,
        List<SearchOrderType> searchOrderKM,
        bool is64bitMachine,
        out SearchOrderType resolver)
    {
        string result;
        resolver = SearchOrderType.None;

        if (searchOrderKM == null)
            return string.Empty;

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
    /// <param name="searchOrder">The search order type to use.</param>
    /// <param name="fileName">The file name to resolve.</param>
    /// <param name="is64bitMachine">True if the target is 64-bit architecture.</param>
    /// <param name="needRedirection">True if architecture redirection is needed.</param>
    /// <returns>The resolved path if found, otherwise an empty string.</returns>
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

            return string.Empty;
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
    /// <param name="fileName">The file name to resolve.</param>
    /// <param name="searchOrderUM">User mode search order list.</param>
    /// <param name="is64bitMachine">True if the target is 64-bit architecture.</param>
    /// <param name="needRedirection">True if architecture redirection is needed.</param>
    /// <param name="moduleMachine">The module's machine type.</param>
    /// <param name="resolver">Output parameter indicating which resolver found the file.</param>
    /// <returns>The resolved path if found, otherwise an empty string.</returns>
    private static string ResolveUserModulePath(
        string fileName,
        List<SearchOrderType> searchOrderUM,
        bool is64bitMachine,
        bool needRedirection,
        ushort moduleMachine,
        out SearchOrderType resolver)
    {
        string result;
        resolver = SearchOrderType.None;

        // Direct absolute/UNC path handling.
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            string direct = fileName.Trim().Trim('"');
            if (direct.Contains('/'))
                direct = direct.Replace('/', '\\');

            // If user supplied an absolute path, prefer it but only if it exists.
            if (Path.IsPathRooted(direct))
            {
                try
                {
                    string full = Path.GetFullPath(direct);

                    // Only short-circuit if the file actually exists.
                    if (File.Exists(full))
                    {
                        // Prefer to mark as ApplicationDirectory only when the file is in current application directory.
                        string dir = Path.GetDirectoryName(full) ?? string.Empty;
                        if (!string.IsNullOrEmpty(CurrentDirectory) &&
                            dir.Equals(CurrentDirectory, StringComparison.OrdinalIgnoreCase))
                        {
                            resolver = SearchOrderType.ApplicationDirectory;
                        }
                        else
                        {
                            resolver = SearchOrderType.None;
                        }
                        return full;
                    }

                    // If the absolute path doesn't exist, continue with configured search order
                    // (this allows PATH and other locations to be tried).
                }
                catch
                {
                    // Ignore normalization failures and continue with search order.
                }
            }
        }

        if (searchOrderUM == null)
            return string.Empty;

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

        // If all resolution methods failed, try cached non-WinSxS SearchPath result for this file.
        lock (_winsxsCacheLock)
        {
            if (_winsxsSearchPathCache.TryGetValue(fileName, out string cached) && !string.IsNullOrEmpty(cached))
            {
                resolver = SearchOrderType.WinSXS;

                // Remove on first use to avoid cross-application reuse.
                _winsxsSearchPathCache.Remove(fileName);
                return cached;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves the full path for a module based on search order rules.
    /// </summary>
    /// <param name="partiallyResolvedFileName">The partially resolved file name.</param>
    /// <param name="parentModule">The parent module requesting the resolution.</param>
    /// <param name="searchOrderUM">User mode search order list.</param>
    /// <param name="searchOrderKM">Kernel mode search order list.</param>
    /// <param name="resolver">Output parameter indicating which resolver found the file.</param>
    /// <returns>The fully resolved path if found, otherwise an empty string.</returns>
    static internal string ResolvePathForModule(string partiallyResolvedFileName,
                                                CModule parentModule,
                                                List<SearchOrderType> searchOrderUM,
                                                List<SearchOrderType> searchOrderKM,
                                                out SearchOrderType resolver)
    {
        bool is64bitMachine, needRedirection;
        ushort moduleMachine;

        resolver = SearchOrderType.None;

        if (parentModule == null || string.IsNullOrEmpty(partiallyResolvedFileName))
            return string.Empty;

        is64bitMachine = parentModule.Is64bitArchitecture();
        moduleMachine = parentModule.ModuleData.Machine;

        needRedirection = CUtils.SystemProcessorArchitecture switch
        {
            NativeMethods.PROCESSOR_ARCHITECTURE_INTEL => moduleMachine != (ushort)Machine.I386,
            NativeMethods.PROCESSOR_ARCHITECTURE_AMD64 => moduleMachine != (ushort)Machine.Amd64,
            NativeMethods.PROCESSOR_ARCHITECTURE_IA64 => moduleMachine != (ushort)Machine.IA64,
            NativeMethods.PROCESSOR_ARCHITECTURE_ARM64 => moduleMachine != (ushort)Machine.Arm64,
            _ => false,
        };

        // KM module resolving.
        if (!needRedirection && parentModule.IsKernelModule)
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
