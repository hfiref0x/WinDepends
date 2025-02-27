/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CSYMBOLRESOLVER.CS
*
*  VERSION:     1.00
*  
*  DATE:        27 Feb 2025
*
*  MS Symbols resolver support class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDepends;

public static class CSymbolResolver
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
        /// <summary>Undecorate 32-bit decorated names.</summary>
        Decode32Bit = 0x0800,

        /// <summary>Enable full undecoration.</summary>
        Complete = 0x0000,

        /// <summary>Undecorate only the name for primary declaration. Returns [scope::]name. Does expand template parameters.</summary>
        NameOnly = 0x1000,

        /// <summary>Disable expansion of access specifiers for members.</summary>
        NoAccessSpecifiers = 0x0080,

        /// <summary>Disable expansion of the declaration language specifier.</summary>
        NoAllocateLanguage = 0x0010,

        /// <summary>Disable expansion of the declaration model.</summary>
        NoAllocationModel = 0x0008,

        /// <summary>Do not undecorate function arguments.</summary>
        NoArguments = 0x2000,

        /// <summary>Disable expansion of CodeView modifiers on the this type for primary declaration.</summary>
        NoCVThisType = 0x0040,

        /// <summary>Disable expansion of return types for primary declarations.</summary>
        NoFunctionReturns = 0x0004,

        /// <summary>Remove leading underscores from Microsoft keywords.</summary>
        NoLeadingUndersCores = 0x0001,

        /// <summary>Disable expansion of the static or virtual attribute of members.</summary>
        NoMemberType = 0x0200,

        /// <summary>Disable expansion of Microsoft keywords.</summary>
        NoMsKeyWords = 0x0002,

        /// <summary>Disable expansion of Microsoft keywords on the this type for primary declaration.</summary>
        NoMsThisType = 0x0020,

        /// <summary>Disable expansion of the Microsoft model for user-defined type returns.</summary>
        NoReturnUDTModel = 0x0400,

        /// <summary>Do not undecorate special names, such as vtable, vcall, vector, metatype, and so on.</summary>
        NoSpecialSyms = 0x4000,

        /// <summary>Disable all modifiers on the this type.</summary>
        NoThisType = 0x0060,

        /// <summary>Disable expansion of throw-signatures for functions and pointers to functions.</summary>
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

    // P/Invoke delegates
    static SymLoadModuleExDelegate SymLoadModuleEx;
    static SymUnloadModule64Delegate SymUnloadModule64;
    static SymInitializeDelegate SymInitialize;
    static SymGetOptionsDelegate SymGetOptions;
    static SymSetOptionsDelegate SymSetOptions;
    static SymCleanupDelegate SymCleanup;
    static SymFromAddrDelegate SymFromAddr;
    static UnDecorateSymbolNameDelegate UnDecorateSymbolName;
    static IntPtr DbgHelpModule { get; set; } = IntPtr.Zero;
    static bool SymbolsInitialized { get; set; }
    public static string DllPath { get; set; }
    public static string StorePath { get; set; }

    static readonly SafeProcessHandle CurrentProcess = new(new IntPtr(-1), false);

    private struct SymModuleItem
    {
        public IntPtr BaseAddress;
    }

    static readonly ConcurrentDictionary<string, SymModuleItem> symModulesCache = new();

    public static void CacheSymModule(string symModuleName, IntPtr baseAddress)
    {
        var item = new SymModuleItem { BaseAddress = baseAddress };
        symModulesCache.AddOrUpdate(symModuleName, item, (key, oldValue) => item);
    }

    public static IntPtr RetrieveCachedSymModule(string symModuleName)
    {
        if (symModulesCache.TryGetValue(symModuleName, out var item))
        {
            return item.BaseAddress;
        }
        else
        {
            return IntPtr.Zero;
        }
    }

    public static void UnloadCachedSymModules()
    {
        foreach (var kvp in symModulesCache)
        {
            SymUnloadModule64(CurrentProcess, kvp.Value.BaseAddress);
        }
    }

    private static void ClearDelegates()
    {
        SymLoadModuleEx = null;
        SymUnloadModule64 = null;
        SymInitialize = null;
        SymCleanup = null;
        SymFromAddr = null;
        UnDecorateSymbolName = null;
        SymSetOptions = null;
        SymGetOptions = null;
    }
    private static bool InitializeDelegates()
    {
        bool bResult;
        try
        {
            UnDecorateSymbolName = Marshal.GetDelegateForFunctionPointer<UnDecorateSymbolNameDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "UnDecorateSymbolNameW"));
            SymLoadModuleEx = Marshal.GetDelegateForFunctionPointer<SymLoadModuleExDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymLoadModuleExW"));
            SymUnloadModule64 = Marshal.GetDelegateForFunctionPointer<SymUnloadModule64Delegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymUnloadModule64"));
            SymGetOptions = Marshal.GetDelegateForFunctionPointer<SymGetOptionsDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymGetOptions"));
            SymSetOptions = Marshal.GetDelegateForFunctionPointer<SymSetOptionsDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymSetOptions"));
            SymInitialize = Marshal.GetDelegateForFunctionPointer<SymInitializeDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymInitializeW"));
            SymFromAddr = Marshal.GetDelegateForFunctionPointer<SymFromAddrDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymFromAddrW"));
            SymCleanup = Marshal.GetDelegateForFunctionPointer<SymCleanupDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymCleanup"));

            if (UnDecorateSymbolName == null
                || SymLoadModuleEx == null
                || SymUnloadModule64 == null
                || SymGetOptions == null
                || SymSetOptions == null
                || SymInitialize == null
                || SymCleanup == null
                || SymFromAddr == null)
            {
                ClearDelegates();
                bResult = false;
            }
            else
            {
                bResult = true;
            }

        }
        catch
        {
            ClearDelegates();
            bResult = false;
        }

        return bResult;
    }

    public static bool AllocateSymbolResolver(string dllPath, string storePath)
    {
        DllPath = dllPath;
        StorePath = storePath;

        DbgHelpModule = NativeMethods.LoadLibraryEx(dllPath, IntPtr.Zero, 0);
        if (DbgHelpModule != IntPtr.Zero)
        {
            if (InitializeDelegates())
            {
                var symOptions = SymGetOptions();

                //
                // No SYMOPT_UNDNAME as we have a special GUI option for it.
                //
                SymSetOptions((symOptions | SYMOPT_DEFERRED_LOADS | SYMOPT_FAIL_CRITICAL_ERRORS | SYMOPT_PUBLICS_ONLY) & ~SYMOPT_UNDNAME);

                SymbolsInitialized = SymInitialize(CurrentProcess, StorePath, false);
            }
        }

        return SymbolsInitialized;
    }

    public static bool ReleaseSymbolResolver()
    {
        bool bResult = false;

        if (DbgHelpModule != IntPtr.Zero)
        {
            if (SymbolsInitialized)
            {
                bResult = SymCleanup(CurrentProcess);
                UnloadCachedSymModules();
            }

            symModulesCache.Clear();

            NativeMethods.FreeLibrary(DbgHelpModule);
            DbgHelpModule = IntPtr.Zero;

            ClearDelegates();

            SymbolsInitialized = false;
        }

        return bResult;
    }

    /// <summary>
    /// Call dbghelp!UnDecorateSymbolNameW to undecorate name.
    /// </summary>
    /// <param name="functionName"></param>
    /// <returns></returns>
    internal static string UndecorateFunctionName(string functionName)
    {
        if (UnDecorateSymbolName == null)
        {
            return functionName;
        }

        var sb = new StringBuilder(128);

        if (UnDecorateSymbolName(functionName, sb, sb.Capacity, UNDNAME.Complete) > 0)
        {
            return sb.ToString();
        }

        return string.Empty;
    }

    internal static IntPtr LoadModule(string fileName, UInt64 baseAddress)
    {
        if (!SymbolsInitialized)
        {
            return IntPtr.Zero;
        }
        else
        {
            return SymLoadModuleEx(CurrentProcess,
                                    IntPtr.Zero,
                                    fileName,
                                    null,
                                    baseAddress,
                                    0,
                                    IntPtr.Zero,
                                    0);
        }
    }

    public static bool QuerySymbolForAddress(UInt64 address, out string symbolName)
    {
        symbolName = null;

        if (!SymbolsInitialized)
        {
            return false;
        }

        var symbolInfo = new SYMBOL_INFO
        {
            SizeOfStruct = SIZE_OF_SYMBOL_INFO,
            MaxNameLen = MAX_SYM_NAME
        };

        if (SymFromAddr(CurrentProcess, address, out ulong displacement, ref symbolInfo))
        {
            symbolName = symbolInfo.Name;
            return true;
        }

        return false;
    }

    public static string GetSymbolNameForAddress(UInt64 address, string moduleFileName)
    {
        var moduleBase = RetrieveCachedSymModule(moduleFileName);
        if (moduleBase != IntPtr.Zero)
        {
            if (QuerySymbolForAddress(address, out string symName))
            {
                return symName;
            }
        }

        return string.Empty;
    }

}
