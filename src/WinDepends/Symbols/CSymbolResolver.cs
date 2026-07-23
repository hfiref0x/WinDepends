/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CSYMBOLRESOLVER.CS
*
*  VERSION:     1.00
*  
*  DATE:        17 Jul 2026
*
*  MS Symbols resolver support class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDepends;

/// <summary>
/// Represents the result of initializing the symbol resolver.
/// </summary>
public enum SymbolResolverInitResult
{
    /// <summary>
    /// Failed to load the dbghelp.dll.
    /// </summary>
    DllLoadFailure = -1,

    /// <summary>
    /// Failed to initialize the symbol resolver.
    /// </summary>
    InitializationFailure = 0,

    /// <summary>
    /// Successfully initialized with symbol functionality.
    /// </summary>
    SuccessWithSymbols = 1,

    /// <summary>
    /// Successfully initialized for name undecoration only.
    /// </summary>
    SuccessForUndecorationOnly = 2,

    /// <summary>
    /// Successfully initialized with symbol functionality using better dbghelp.dll version.
    /// </summary>
    SuccessWithSymbolsAlternateDll = 3
}

public enum SymbolLoadState
{
    Idle,
    Queued,
    Loading,
    Loaded,
    Failed,
    Cancelled
}

public sealed class SymbolLoadStatusChangedEventArgs : EventArgs
{
    public string FileName { get; init; }
    public SymbolLoadState State { get; init; }
    public string Message { get; init; }
    public Exception Error { get; init; }
}

/// <summary>
/// Provides functionality for resolving symbols from Windows binary files using the DbgHelp API.
/// </summary>
public sealed class CSymbolResolver : IDisposable
{
    #region "P/Invoke stuff"
    public const uint SYMOPT_CASE_INSENSITIVE = 0x00000001;
    public const uint SYMOPT_UNDNAME = 0x00000002;
    public const uint SYMOPT_DEFERRED_LOADS = 0x00000004;
    public const uint SYMOPT_NO_CPP = 0x00000008;
    public const uint SYMOPT_LOAD_LINES = 0x00000010;
    public const uint SYMOPT_OMAP_FIND_NEAREST = 0x00000020;
    public const uint SYMOPT_LOAD_ANYTHING = 0x00000040;
    public const uint SYMOPT_IGNORE_CVREC = 0x00000080;
    public const uint SYMOPT_NO_UNQUALIFIED_LOADS = 0x00000100;
    public const uint SYMOPT_FAIL_CRITICAL_ERRORS = 0x00000200;
    public const uint SYMOPT_EXACT_SYMBOLS = 0x00000400;
    public const uint SYMOPT_ALLOW_ABSOLUTE_SYMBOLS = 0x00000800;
    public const uint SYMOPT_IGNORE_NT_SYMPATH = 0x00001000;
    public const uint SYMOPT_INCLUDE_32BIT_MODULES = 0x00002000;
    public const uint SYMOPT_PUBLICS_ONLY = 0x00004000;
    public const uint SYMOPT_NO_PUBLICS = 0x00008000;
    public const uint SYMOPT_AUTO_PUBLICS = 0x00010000;
    public const uint SYMOPT_NO_IMAGE_SEARCH = 0x00020000;
    public const uint SYMOPT_SECURE = 0x00040000;
    public const uint SYMOPT_NO_PROMPTS = 0x00080000;
    public const uint SYMOPT_OVERWRITE = 0x00100000;
    public const uint SYMOPT_IGNORE_IMAGEDIR = 0x00200000;
    public const uint SYMOPT_FLAT_DIRECTORY = 0x00400000;
    public const uint SYMOPT_FAVOR_COMPRESSED = 0x00800000;
    public const uint SYMOPT_ALLOW_ZERO_ADDRESS = 0x01000000;
    public const uint SYMOPT_DISABLE_SYMSRV_AUTODETECT = 0x02000000;
    public const uint SYMOPT_READONLY_CACHE = 0x04000000;
    public const uint SYMOPT_SYMPATH_LAST = 0x08000000;
    public const uint SYMOPT_DISABLE_FAST_SYMBOLS = 0x10000000;
    public const uint SYMOPT_DISABLE_SYMSRV_TIMEOUT = 0x20000000;
    public const uint SYMOPT_DISABLE_SRVSTAR_ON_STARTUP = 0x40000000;
    public const uint SYMOPT_DEBUG = 0x80000000;

    [Flags]
    public enum UNDNAME : uint
    {
        Decode32Bit = 0x0800,
        Complete = 0x0000,
        NameOnly = 0x1000,
        NoAccessSpecifiers = 0x0080,
        NoAllocateLanguage = 0x0010,
        NoAllocationModel = 0x0008,
        NoArguments = 0x2000,
        NoCVThisType = 0x0040,
        NoFunctionReturns = 0x0004,
        NoLeadingUndersCores = 0x0001,
        NoMemberType = 0x0200,
        NoMsKeyWords = 0x0002,
        NoMsThisType = 0x0020,
        NoReturnUDTModel = 0x0400,
        NoSpecialSyms = 0x4000,
        NoThisType = 0x0060,
        NoThrowSignatures = 0x0100,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SYMBOL_INFO
    {
        public UInt32 SizeOfStruct;
        public UInt32 TypeIndex;
        public UInt64 Reserved;
        public UInt64 Reserved2;
        public UInt32 Index;
        public UInt32 Size;
        public UInt64 ModBase;
        public UInt32 Flags;
        public UInt64 Value;
        public UInt64 Address;
        public UInt32 Register;
        public UInt32 Scope;
        public UInt32 Tag;
        public UInt32 NameLen;
        public UInt32 MaxNameLen;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2000)]
        public string Name;
    }

    const UInt32 MAX_SYM_NAME = 2000;
    static readonly UInt32 SIZE_OF_SYMBOL_INFO = (uint)Marshal.SizeOf<SYMBOL_INFO>() - (MAX_SYM_NAME * 2);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate IntPtr SymLoadModuleExDelegate(SafeProcessHandle hProcess,
        IntPtr hFile,
        [MarshalAs(UnmanagedType.LPWStr)] string ImageName,
        [MarshalAs(UnmanagedType.LPWStr)] string ModuleName,
        UInt64 BaseOfDll,
        int SizeOfDll,
        IntPtr Data,
        int Flags);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate bool SymUnloadModule64Delegate(SafeProcessHandle hProcess,
        IntPtr BaseOfDll);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate bool SymInitializeDelegate(SafeProcessHandle hProcess,
        [MarshalAs(UnmanagedType.LPWStr)] string UserSearchPath,
        bool fInvadeProcess);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate bool SymCleanupDelegate(SafeProcessHandle hProcess);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate bool SymFromAddrDelegate(
        SafeProcessHandle hProcess,
        UInt64 Address,
        out UInt64 Displacement,
        ref SYMBOL_INFO Symbol);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate uint SymGetOptionsDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate uint SymSetOptionsDelegate(uint options);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    delegate uint UnDecorateSymbolNameDelegate(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder outputString,
        int maxStringLength,
        UNDNAME flags);
    #endregion

    SymLoadModuleExDelegate SymLoadModuleEx;
    SymUnloadModule64Delegate SymUnloadModule64;
    SymInitializeDelegate SymInitialize;
    SymGetOptionsDelegate SymGetOptions;
    SymSetOptionsDelegate SymSetOptions;
    SymCleanupDelegate SymCleanup;
    SymFromAddrDelegate SymFromAddr;
    UnDecorateSymbolNameDelegate UnDecorateSymbolName;

    readonly object _stateLock = new();
    readonly object _dbgHelpLock = new();
    readonly AutoResetEvent _preloadSignal = new(false);

    Thread _preloadWorkerThread;
    bool _workerStarted;
    volatile bool _disposeRequested;

    string _pendingModuleFileName = string.Empty;
    UInt64 _pendingModuleBaseAddress;
    int _pendingRequestId;

    string _activeModuleFileName = string.Empty;

    string _nativeLoadedModuleFileName = string.Empty;
    IntPtr _nativeLoadedModuleBase = IntPtr.Zero;

    IntPtr DbgHelpModule { get; set; } = IntPtr.Zero;

    public bool SymbolsInitialized { get; private set; }
    public bool UndecorationReady { get; private set; }

    /// <summary>
    /// When set, recognized STL default-argument template forms in undecorated
    /// names are collapsed to their conventional spellings (e.g. std::string).
    /// </summary>
    public bool SimplifyTemplateDefaults { get; set; }
    public string DllPath { get; private set; }
    public string StorePath { get; private set; }

    public event EventHandler<SymbolLoadStatusChangedEventArgs> SymbolLoadStatusChanged;

    static readonly SafeProcessHandle CurrentProcess = new(new IntPtr(-1), false);

    private void RaiseSymbolLoadStatusChanged(string fileName, SymbolLoadState state, string message, Exception error = null)
    {
        SymbolLoadStatusChanged?.Invoke(this, new SymbolLoadStatusChangedEventArgs
        {
            FileName = fileName,
            State = state,
            Message = message,
            Error = error
        });
    }

    private void EnsurePreloadWorkerStarted()
    {
        if (_workerStarted)
            return;

        _preloadWorkerThread = new Thread(PreloadWorkerProc)
        {
            IsBackground = true,
            Name = "CSymbolResolver.PreloadWorker"
        };

        _workerStarted = true;
        _preloadWorkerThread.Start();
    }

    private void ClearSymbolsDelegates()
    {
        SymLoadModuleEx = null;
        SymUnloadModule64 = null;
        SymInitialize = null;
        SymCleanup = null;
        SymFromAddr = null;
        SymSetOptions = null;
        SymGetOptions = null;
    }

    private void ClearUndecorateDelegate()
    {
        UnDecorateSymbolName = null;
    }

    private bool InitializeUndecorateDelegate()
    {
        try
        {
            UnDecorateSymbolName = Marshal.GetDelegateForFunctionPointer<UnDecorateSymbolNameDelegate>(
                NativeMethods.GetProcAddress(DbgHelpModule, "UnDecorateSymbolNameW"));
        }
        catch
        {
            UnDecorateSymbolName = null;
        }

        return UnDecorateSymbolName != null;
    }

    private bool InitializeSymbolsDelegates()
    {
        bool bResult;

        try
        {
            SymLoadModuleEx = Marshal.GetDelegateForFunctionPointer<SymLoadModuleExDelegate>(
                NativeMethods.GetProcAddress(DbgHelpModule, "SymLoadModuleExW"));
            SymUnloadModule64 = Marshal.GetDelegateForFunctionPointer<SymUnloadModule64Delegate>(
                NativeMethods.GetProcAddress(DbgHelpModule, "SymUnloadModule64"));
            SymGetOptions = Marshal.GetDelegateForFunctionPointer<SymGetOptionsDelegate>(
                NativeMethods.GetProcAddress(DbgHelpModule, "SymGetOptions"));
            SymSetOptions = Marshal.GetDelegateForFunctionPointer<SymSetOptionsDelegate>(
                NativeMethods.GetProcAddress(DbgHelpModule, "SymSetOptions"));
            SymInitialize = Marshal.GetDelegateForFunctionPointer<SymInitializeDelegate>(
                NativeMethods.GetProcAddress(DbgHelpModule, "SymInitializeW"));
            SymFromAddr = Marshal.GetDelegateForFunctionPointer<SymFromAddrDelegate>(
                NativeMethods.GetProcAddress(DbgHelpModule, "SymFromAddrW"));
            SymCleanup = Marshal.GetDelegateForFunctionPointer<SymCleanupDelegate>(
                NativeMethods.GetProcAddress(DbgHelpModule, "SymCleanup"));

            if (SymLoadModuleEx == null
                || SymUnloadModule64 == null
                || SymGetOptions == null
                || SymSetOptions == null
                || SymInitialize == null
                || SymCleanup == null
                || SymFromAddr == null)
            {
                ClearSymbolsDelegates();
                bResult = false;
            }
            else
            {
                bResult = true;
            }
        }
        catch
        {
            ClearSymbolsDelegates();
            bResult = false;
        }

        return bResult;
    }

    /// <summary>
    /// Initializes the symbol resolver with the specified parameters.
    /// </summary>
    /// <param name="dllPath">The path to DbgHelp.dll.</param>
    /// <param name="storePath">The path to store symbol files.</param>
    /// <param name="useSymbols">Indicates whether to use symbols or only name undecoration.</param>
    /// <returns>
    /// A <see cref="SymbolResolverInitResult"/> value indicating the result of the initialization:
    /// <list type="bullet">
    ///   <item><description><see cref="SymbolResolverInitResult.DllLoadFailure"/> if DbgHelp.dll could not be loaded</description></item>
    ///   <item><description><see cref="SymbolResolverInitResult.InitializationFailure"/> if initialization failed</description></item>
    ///   <item><description><see cref="SymbolResolverInitResult.SuccessWithSymbolsAlternateDll"/> if successfully initialized with a better dbghelp.dll</description></item>
    ///   <item><description><see cref="SymbolResolverInitResult.SuccessWithSymbols"/> if successfully initialized with symbols</description></item>
    ///   <item><description><see cref="SymbolResolverInitResult.SuccessForUndecorationOnly"/> if successfully initialized for name undecoration only</description></item>
    /// </list>
    /// </returns>
    public SymbolResolverInitResult AllocateSymbolResolver(string dllPath, string storePath, bool useSymbols)
    {
        DllPath = string.Empty;
        StorePath = storePath;

        string? dllToLoad = dllPath;
        string? preferredDbgHelp = FindDbgHelpDll();

        if (string.IsNullOrWhiteSpace(dllToLoad) || !File.Exists(dllToLoad))
        {
            dllToLoad = preferredDbgHelp ?? CConsts.DbgHelpDll;
        }
        else if (IsSystemDbgHelp(dllToLoad) &&
                !string.IsNullOrEmpty(preferredDbgHelp) &&
                !PathsEqual(preferredDbgHelp, dllToLoad))
        {
            dllToLoad = preferredDbgHelp;
        }

        lock (_dbgHelpLock)
        {
            DbgHelpModule = NativeMethods.LoadLibraryEx(dllToLoad, IntPtr.Zero, 0);
            if (DbgHelpModule == IntPtr.Zero &&
                !string.Equals(dllToLoad, CConsts.DbgHelpDll, StringComparison.OrdinalIgnoreCase))
            {
                dllToLoad = CConsts.DbgHelpDll;
                DbgHelpModule = NativeMethods.LoadLibraryEx(dllToLoad, IntPtr.Zero, 0);
            }

            if (DbgHelpModule == IntPtr.Zero)
                return SymbolResolverInitResult.DllLoadFailure;

            DllPath = dllToLoad;

            UndecorationReady = InitializeUndecorateDelegate();

            if (useSymbols && InitializeSymbolsDelegates())
            {
                SymSetOptions(
                    (SymGetOptions() |
                     SYMOPT_DEFERRED_LOADS |
                     SYMOPT_FAIL_CRITICAL_ERRORS |
                     SYMOPT_PUBLICS_ONLY
                    // | SYMOPT_NO_PROMPTS
                    ) & ~SYMOPT_UNDNAME);

                SymbolsInitialized = SymInitialize(CurrentProcess, StorePath, false);
            }
        }

        if (useSymbols)
        {
            if (!SymbolsInitialized)
                return SymbolResolverInitResult.InitializationFailure;

            EnsurePreloadWorkerStarted();

            return IsSystemDbgHelp(DllPath)
                ? SymbolResolverInitResult.SuccessWithSymbols
                : SymbolResolverInitResult.SuccessWithSymbolsAlternateDll;
        }

        return UndecorationReady
            ? SymbolResolverInitResult.SuccessForUndecorationOnly
            : SymbolResolverInitResult.InitializationFailure;
    }

    public void RequestModulePreload(string fileName, UInt64 baseAddress)
    {
        string loadedFileName;
        IntPtr loadedBase;

        if (!SymbolsInitialized || string.IsNullOrEmpty(fileName))
            return;

        if (!File.Exists(fileName))
        {
            RaiseSymbolLoadStatusChanged(fileName, SymbolLoadState.Failed,
                $"Symbol load failed for \"{fileName}\": file not found.");
            return;
        }

        lock (_dbgHelpLock)
        {
            loadedFileName = _nativeLoadedModuleFileName;
            loadedBase = _nativeLoadedModuleBase;
        }

        lock (_stateLock)
        {
            if (loadedBase != IntPtr.Zero &&
                string.Equals(loadedFileName, fileName, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(_pendingModuleFileName))
            {
                _activeModuleFileName = fileName;
                return;
            }

            if (string.Equals(_pendingModuleFileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _pendingModuleFileName = fileName;
            _pendingModuleBaseAddress = baseAddress;
            _pendingRequestId++;
        }

        RaiseSymbolLoadStatusChanged(fileName, SymbolLoadState.Queued,
            $"Loading symbols for \"{fileName}\", please wait...");

        _preloadSignal.Set();
    }

    private void PreloadWorkerProc()
    {
        while (true)
        {
            string fileName;
            UInt64 baseAddress;
            int requestId;
            bool alreadyLoaded;
            bool loadSucceeded;
            IntPtr loadedBase;
            string nativeLoadedName;
            int lastError;

            _preloadSignal.WaitOne();

            if (_disposeRequested)
                break;

            lock (_stateLock)
            {
                fileName = _pendingModuleFileName;
                baseAddress = _pendingModuleBaseAddress;
                requestId = _pendingRequestId;

                _pendingModuleFileName = string.Empty;
                _pendingModuleBaseAddress = 0;
            }

            if (string.IsNullOrEmpty(fileName))
                continue;

            try
            {
                RaiseSymbolLoadStatusChanged(fileName, SymbolLoadState.Loading,
                    $"Loading symbols for \"{fileName}\", please wait...");

                lock (_dbgHelpLock)
                {
                    alreadyLoaded = string.Equals(_nativeLoadedModuleFileName, fileName, StringComparison.OrdinalIgnoreCase)
                        && _nativeLoadedModuleBase != IntPtr.Zero;
                }

                loadSucceeded = alreadyLoaded;
                loadedBase = IntPtr.Zero;
                nativeLoadedName = string.Empty;
                lastError = 0;

                if (!alreadyLoaded)
                {
                    lock (_dbgHelpLock)
                    {
                        if (_disposeRequested)
                            break;

                        loadedBase = LoadModuleNative(fileName, baseAddress, out lastError);
                        if (loadedBase != IntPtr.Zero)
                        {
                            _nativeLoadedModuleFileName = fileName;
                            _nativeLoadedModuleBase = loadedBase;
                            nativeLoadedName = fileName;
                            loadSucceeded = true;
                        }
                        else
                        {
                            _nativeLoadedModuleFileName = string.Empty;
                            _nativeLoadedModuleBase = IntPtr.Zero;
                        }
                    }
                }
                else
                {
                    lock (_dbgHelpLock)
                    {
                        nativeLoadedName = _nativeLoadedModuleFileName;
                        loadedBase = _nativeLoadedModuleBase;
                    }
                }

                if (!loadSucceeded)
                {
                    RaiseSymbolLoadStatusChanged(fileName, SymbolLoadState.Failed,
                        lastError != 0
                            ? $"Symbol load failed for \"{fileName}\", error 0x{lastError:X8}."
                            : $"Symbol load failed for \"{fileName}\".");
                    continue;
                }

                lock (_stateLock)
                {
                    if (string.Equals(nativeLoadedName, fileName, StringComparison.OrdinalIgnoreCase) &&
                        loadedBase != IntPtr.Zero)
                    {
                        _activeModuleFileName = fileName;
                    }
                }

                lock (_stateLock)
                {
                    bool isCancelled;

                    lock (_stateLock)
                    {
                        isCancelled = requestId != _pendingRequestId;
                    }

                    if (isCancelled)
                    {
                        RaiseSymbolLoadStatusChanged(fileName, SymbolLoadState.Cancelled, string.Empty);
                        continue;
                    }
                }

                RaiseSymbolLoadStatusChanged(fileName, SymbolLoadState.Loaded, $"Symbols loading for \"{fileName}\" has been finished");
            }
            catch (Exception ex)
            {
                RaiseSymbolLoadStatusChanged(fileName, SymbolLoadState.Failed,
                    $"Symbol load failed for \"{fileName}\": {ex.Message}", ex);
            }
        }

        if (!_disposeRequested)
        {
            RaiseSymbolLoadStatusChanged(string.Empty, SymbolLoadState.Idle, string.Empty);
        }
    }

    private IntPtr LoadModuleNative(string fileName, UInt64 baseAddress, out int lastError)
    {
        IntPtr symModule;

        lastError = 0;

        if (!SymbolsInitialized || SymLoadModuleEx == null)
            return IntPtr.Zero;

        ClearLoadedModuleNative();

        symModule = SymLoadModuleEx(CurrentProcess,
            IntPtr.Zero,
            fileName,
            null,
            baseAddress,
            0,
            IntPtr.Zero,
            0);

        if (symModule == IntPtr.Zero)
        {
            lastError = Marshal.GetLastWin32Error();
        }

        return symModule;
    }

    private bool ClearLoadedModuleNative()
    {
        bool result = false;

        if (_nativeLoadedModuleBase != IntPtr.Zero)
        {
            if (SymUnloadModule64 != null)
            {
                result = SymUnloadModule64(CurrentProcess, _nativeLoadedModuleBase);
            }
        }

        _nativeLoadedModuleBase = IntPtr.Zero;
        _nativeLoadedModuleFileName = string.Empty;

        return result;
    }

    public IntPtr RetrieveCachedSymModule(string moduleName)
    {
        lock (_dbgHelpLock)
        {
            if (string.IsNullOrEmpty(moduleName))
                return IntPtr.Zero;

            if (string.Equals(_nativeLoadedModuleFileName, moduleName, StringComparison.OrdinalIgnoreCase))
                return _nativeLoadedModuleBase;

            return IntPtr.Zero;
        }
    }

    public bool ClearCachedSymModule()
    {
        bool result;

        lock (_dbgHelpLock)
        {
            result = ClearLoadedModuleNative();
        }

        lock (_stateLock)
        {
            _activeModuleFileName = string.Empty;
        }

        return result;
    }

    /// <summary>
    /// Releases all resources used by the symbol resolver.
    /// </summary>
    /// <returns>True if cleanup was successful, false otherwise.</returns>
    public bool ReleaseSymbolResolver()
    {
        bool bResult = false;

        lock (_dbgHelpLock)
        {
            if (DbgHelpModule != IntPtr.Zero)
            {
                ClearLoadedModuleNative();

                if (SymbolsInitialized && SymCleanup != null)
                {
                    bResult = SymCleanup(CurrentProcess);
                }

                NativeMethods.FreeLibrary(DbgHelpModule);
                DbgHelpModule = IntPtr.Zero;

                ClearUndecorateDelegate();
                UndecorationReady = false;

                ClearSymbolsDelegates();
                SymbolsInitialized = false;
            }
        }

        lock (_stateLock)
        {
            _pendingModuleFileName = string.Empty;
            _pendingModuleBaseAddress = 0;
            _pendingRequestId = 0;
            _activeModuleFileName = string.Empty;
        }

        return bResult;
    }

    public void Dispose()
    {
        _disposeRequested = true;
        _preloadSignal.Set();

        if (_preloadWorkerThread != null && _preloadWorkerThread.IsAlive)
        {
            _preloadWorkerThread.Join();
        }

        _preloadSignal.Dispose();
        ReleaseSymbolResolver();
    }

    /// <summary>
    /// Undecorates a C++ decorated function name.
    /// </summary>
    /// <param name="functionName">The decorated function name.</param>
    /// <returns>The undecorated function name, or the original name if it wasn't decorated.</returns>
    internal string UndecorateFunctionName(string functionName)
    {
        lock (_dbgHelpLock)
        {
            if (!UndecorationReady || UnDecorateSymbolName == null)
            {
                return functionName;
            }

            StringBuilder sb = new(1024);

            // Note: DependencyWalker uses UNDNAME.NoAllocateLanguage | UNDNAME.NoMsKeyWords | UNDNAME.NoFunctionReturns | UNDNAME.NoAccessSpecifiers
            if (UnDecorateSymbolName(functionName, sb, sb.Capacity, UNDNAME.NoMsKeyWords) > 0)
            {
                string undecorated = sb.ToString();
                return SimplifyTemplateDefaults
                    ? CStlNameSimplifier.Simplify(undecorated)
                    : undecorated;
            }
        }

        return functionName;
    }

    /// <summary>
    /// Queries for a symbol at the specified address.
    /// </summary>
    /// <param name="address">The address to query.</param>
    /// <param name="symbolName">When this method returns, contains the symbol name if found, or null if not found.</param>
    /// <returns>True if a symbol was found at the specified address, false otherwise.</returns>
    public bool QuerySymbolForAddress(UInt64 address, out string symbolName)
    {
        symbolName = null;

        lock (_dbgHelpLock)
        {
            if (!SymbolsInitialized || SymFromAddr == null)
            {
                return false;
            }

            SYMBOL_INFO symbolInfo = new()
            {
                SizeOfStruct = SIZE_OF_SYMBOL_INFO,
                MaxNameLen = MAX_SYM_NAME
            };

            if (SymFromAddr(CurrentProcess, address, out ulong displacement, ref symbolInfo))
            {
                if (displacement != 0)
                    return false;

                symbolName = symbolInfo.Name;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the best available dbghelp.dll for current process architecture.
    /// </summary>
    /// <returns>
    /// Full path to the preferred dbghelp.dll if found; otherwise null.
    /// Preference order: Windows Kits Debuggers (by highest file version) for current arch,
    /// then common Program Files Windows Kits locations, then the system copy.
    /// </returns>
    internal static string? FindDbgHelpDll()
    {
        string architecture = CUtils.GetProcessArchitectureName();
        HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);

        foreach (string root in EnumerateWindowsKitsRoots())
        {
            AddIfExists(candidates,
                Path.Combine(root, CConsts.DebuggersString, architecture, CConsts.DbgHelpDll));
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        AddIfExists(candidates,
            Path.Combine(programFilesX86, CConsts.WindowsKitsString, "10",
                CConsts.DebuggersString, architecture, CConsts.DbgHelpDll));

        AddIfExists(candidates,
            Path.Combine(programFiles, CConsts.WindowsKitsString, "10",
                CConsts.DebuggersString, architecture, CConsts.DbgHelpDll));

        AddIfExists(candidates,
            Path.Combine(programFilesX86, CConsts.WindowsKitsString, "8.1",
                CConsts.DebuggersString, architecture, CConsts.DbgHelpDll));

        AddIfExists(candidates,
            Path.Combine(programFiles, CConsts.WindowsKitsString, "8.1",
                CConsts.DebuggersString, architecture, CConsts.DbgHelpDll));

        AddIfExists(candidates,
            Path.Combine(Environment.SystemDirectory, CConsts.DbgHelpDll));

        if (candidates.Count == 0)
            return null;

        string? bestPath = null;
        Version? bestVersion = null;

        foreach (string candidate in candidates)
        {
            try
            {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(candidate);

                Version version = new(
                    Math.Max(versionInfo.FileMajorPart, 0),
                    Math.Max(versionInfo.FileMinorPart, 0),
                    Math.Max(versionInfo.FileBuildPart, 0),
                    Math.Max(versionInfo.FilePrivatePart, 0));

                if (bestVersion == null || version > bestVersion)
                {
                    bestVersion = version;
                    bestPath = candidate;
                }
            }
            catch
            {
                // If version information cannot be obtained, keep the first valid candidate.
                bestPath ??= candidate;
            }
        }

        return bestPath;

        static void AddIfExists(ISet<string> set, string path)
        {
            try
            {
                if (File.Exists(path))
                    set.Add(path);
            }
            catch
            {
                // Intentionally silent.
            }
        }
    }

    /// <summary>
    /// Enumerates installed Windows Kits root directories from HKLM for both 64-bit and 32-bit registry views.
    /// </summary>
    /// <returns>
    /// A sequence of root paths (e.g., "C:\Program Files (x86)\Windows Kits\10\") suitable for composing Debuggers\<arch>\dbghelp.dll.
    /// </returns>
    private static IEnumerable<string> EnumerateWindowsKitsRoots()
    {
        List<string> results = [];
        string subKey = @"SOFTWARE\Microsoft\Windows Kits\Installed Roots";
        string[] valueNames = ["KitsRoot10", "KitsRoot81", "KitsRoot"];

        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key == null)
                    continue;

                foreach (string valueName in valueNames)
                {
                    if (key.GetValue(valueName) is string path &&
                         !string.IsNullOrWhiteSpace(path))
                    {
                        results.Add(path);
                    }
                }
            }
            catch
            {
                // Intentionally silent.
            }
        }

        return results;
    }

    /// <summary>
    /// Determines whether the specified path points to the system-provided dbghelp.dll (System32 or SysWOW64).
    /// </summary>
    /// <param name="path">Path to test.</param>
    /// <returns>True if the path resolves to the OS-shipped dbghelp.dll; otherwise false.</returns>
    private static bool IsSystemDbgHelp(string? path)
    {
        try
        {
            string sys32 = Path.Combine(Environment.SystemDirectory, CConsts.DbgHelpDll);
            if (PathsEqual(path, sys32))
                return true;

            try
            {
                string sysX86 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                if (!string.IsNullOrEmpty(sysX86))
                {
                    string sysWow64 = Path.Combine(sysX86, CConsts.DbgHelpDll);
                    if (PathsEqual(path, sysWow64))
                        return true;
                }
            }
            catch
            {
                // Intentionally silent.
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(string? firstPath, string? secondPath)
    {
        if (string.IsNullOrWhiteSpace(firstPath) ||
            string.IsNullOrWhiteSpace(secondPath))
        {
            return false;
        }

        try
        {
            firstPath = Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar);
            secondPath = Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            // Fall back to comparing the original paths.
        }

        return string.Equals(
            firstPath,
            secondPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
