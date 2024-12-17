/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CSYMBOLRESOLVER.CS
*
*  VERSION:     1.00
*  
*  DATE:        17 Dec 2024
*
*  MS Symbols resolver support class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinDepends;

public static class CSymbolResolver
{
    #region "P/Invoke stuff"
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    public delegate IntPtr SymLoadModuleExDelegate(IntPtr hProcess, IntPtr hFile, string ImageName, string ModuleName, long BaseOfDll, int SizeOfDll, IntPtr Data, int Flags);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    public delegate bool SymInitializeDelegate(IntPtr hProcess, string UserSearchPath, bool fInvadeProcess);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    public delegate bool SymCleanupDelegate(IntPtr hProcess);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    public delegate bool SymFromAddrDelegate(IntPtr hProcess, long Address, out long Displacement, ref SYMBOL_INFO Symbol);

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    public delegate uint UnDecorateSymbolNameDelegate(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder outputString,
        int maxStringLength,
        UNDNAME flags);

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

    [StructLayout(LayoutKind.Sequential)]
    public struct SYMBOL_INFO
    {
        public uint SizeOfStruct;
        public uint TypeIndex;
        public ulong Reserved;
        public ulong ModBase;
        public uint Flags;
        public ulong Value;
        public ulong Address;
        public uint Register;
        public uint Scope;
        public uint Tag;
        public uint NameLen;
        public uint MaxNameLen;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string Name;
    }
    #endregion

    // P/Invoke delegates
    static SymLoadModuleExDelegate SymLoadModuleEx;
    static SymInitializeDelegate SymInitialize;
    static SymCleanupDelegate SymCleanup;
    static SymFromAddrDelegate SymFromAddr;
    static UnDecorateSymbolNameDelegate UnDecorateSymbolName;

    static IntPtr DbgHelpModule { get; set; } = IntPtr.Zero;
    static bool SymbolsInitialized { get; set; }

    public static string DllsPath { get; set; }
    public static string StorePath { get; set; }

    private static void ClearDelegates()
    {
        SymLoadModuleEx = null;
        SymInitialize = null;
        SymCleanup = null;
        SymFromAddr = null;
        UnDecorateSymbolName = null;
    }
    private static bool InitializeDelegates()
    {
        UnDecorateSymbolName = Marshal.GetDelegateForFunctionPointer<UnDecorateSymbolNameDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "UnDecorateSymbolNameW"));
        SymLoadModuleEx = Marshal.GetDelegateForFunctionPointer<SymLoadModuleExDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymLoadModuleExW"));
        SymInitialize = Marshal.GetDelegateForFunctionPointer<SymInitializeDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymInitialize"));
        SymCleanup = Marshal.GetDelegateForFunctionPointer<SymCleanupDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymCleanup"));
        SymFromAddr = Marshal.GetDelegateForFunctionPointer<SymFromAddrDelegate>(NativeMethods.GetProcAddress(DbgHelpModule, "SymFromAddr"));

        if (UnDecorateSymbolName == null || SymLoadModuleEx == null || SymInitialize == null || SymCleanup == null || SymFromAddr == null)
        {
            ClearDelegates();
            return false;
        }

        return true;
    }

    public static void AllocateSymbolResolver(string dllsPath, string storePath)
    {
        DllsPath = string.IsNullOrEmpty(dllsPath) ? CConsts.DbgHelpDll : Path.Combine(dllsPath, CConsts.DbgHelpDll);
        StorePath = storePath;

        DbgHelpModule = NativeMethods.LoadLibraryEx(DllsPath, IntPtr.Zero, NativeMethods.LoadLibraryFlags.LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (DbgHelpModule != IntPtr.Zero)
        {
            if (InitializeDelegates())
            {
                SymbolsInitialized = SymInitialize(Process.GetCurrentProcess().Handle, StorePath, false);
            }
        }

    }

    public static void ReleaseSymbolResolver()
    {
        if (DbgHelpModule != IntPtr.Zero)
        {
            if (SymbolsInitialized)
            {
                SymCleanup(Process.GetCurrentProcess().Handle);
            }
            NativeMethods.FreeLibrary(DbgHelpModule);
            DbgHelpModule = IntPtr.Zero;
            ClearDelegates();
        }
    }

    /// <summary>
    /// Call dbghelp!UnDecorateSymbolNameW to undecorate name.
    /// </summary>
    /// <param name="functionName"></param>
    /// <returns></returns>
    static internal string UndecorateFunctionName(string functionName)
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
    static internal bool LoadPDBFile(string pdbFileName)
    {
        if (SymbolsInitialized == false)
        {
            return false;
        }
        else
        {
            return IntPtr.Zero != SymLoadModuleEx(Process.GetCurrentProcess().Handle,
                                                  IntPtr.Zero,
                                                  pdbFileName,
                                                  null,
                                                  0,
                                                  0,
                                                  IntPtr.Zero,
                                                  0);
        }
    }

    public static bool QuerySymbolForAddress(long address, out string symbolName)
    {
        if (SymbolsInitialized == false)
        {
            symbolName = null;
            return false;
        }

        SYMBOL_INFO symbol = new();
        symbol.SizeOfStruct = (uint)Marshal.SizeOf(symbol);

        if (SymFromAddr(Process.GetCurrentProcess().Handle, address, out long displacement, ref symbol))
        {
            symbolName = symbol.Name;
            return true;
        }
        symbolName = null;
        return false;
    }

}
