/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       NATIVEMETHODS.CS
*
*  VERSION:     1.00
*  
*  DATE:        04 Jun 2025
*
*  Win32 API P/Invoke.
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

static partial class NativeMethods
{
    #region "P/Invoke stuff"

    internal static ushort LOWORD(this int value) => (ushort)(value & 0xffff);
    internal static ushort HIWORD(this int value) => (ushort)(value >> 16 & 0xffff);
    internal static ushort LOWORD(this uint value) => (ushort)(value & 0xffff);
    internal static ushort HIWORD(this uint value) => (ushort)(value >> 16 & 0xffff);
    internal static long LARGE_INTEGER(uint LowPart, int HighPart) => LowPart | (long)HighPart << 32;

    //
    // Summary:
    //     Represents the Common Object File Format (COFF) file extended characteristics.
    [Flags]
    public enum CharacteristicsEx : ushort
    {
        //
        // Summary:
        //     Indicates that the image is Control-flow Enforcement Technology (CET) Shadow Stack compatible.
        //
        SetCompat = 1,

        //
        // Summary:
        //     All branch targets in all image code sections are annotated with forward-edge control flow integrity
        //     guard instructions such as x86 CET-Indirect Branch Tracking (IBT) or ARM Branch Target Identification (BTI) instructions.
        //
        ForwardCfiCompat = 64
    }

    internal const uint STGM_READ = 0;
    internal const int MAX_PATH = 260;
    internal const int BCM_FIRST = 0x1600;
    internal const int BCM_SETSHIELD = (BCM_FIRST + 0x000C);

    internal const uint WM_DROPFILES = 0x0233;
    internal const uint WM_COPYDATA = 0x004A;
    internal const uint WM_COPYGLOBALDATA = 0x0049;

    [StructLayout(LayoutKind.Sequential)]
    public struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public ShellExecuteMaskFlags fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpVerb;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpParameters;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpDirectory;
        public ShowCommands nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    public enum ShowCommands : int
    {
        SW_HIDE = 0,
        SW_SHOWNORMAL = 1,
        SW_SHOWMINIMIZED = 2,
        SW_SHOWMAXIMIZED = 3,
        SW_SHOWNOACTIVATE = 4,
        SW_SHOW = 5,
        SW_MINIMIZE = 6,
        SW_SHOWMINNOACTIVE = 7,
        SW_SHOWNA = 8,
        SW_RESTORE = 9,
        SW_SHOWDEFAULT = 10,
        SW_FORCEMINIMIZE = 11,
    }

    [Flags]
    public enum ShellExecuteMaskFlags : uint
    {
        SEE_MASK_DEFAULT = 0x00000000,
        SEE_MASK_CLASSNAME = 0x00000001,
        SEE_MASK_CLASSKEY = 0x00000003,
        SEE_MASK_IDLIST = 0x00000004,
        SEE_MASK_INVOKEIDLIST = 0x0000000c,   // SEE_MASK_INVOKEIDLIST(0xC) implies SEE_MASK_IDLIST(0x04) 
        SEE_MASK_HOTKEY = 0x00000020,
        SEE_MASK_NOCLOSEPROCESS = 0x00000040,
        SEE_MASK_CONNECTNETDRV = 0x00000080,
        SEE_MASK_NOASYNC = 0x00000100,
        SEE_MASK_FLAG_DDEWAIT = SEE_MASK_NOASYNC,
        SEE_MASK_DOENVSUBST = 0x00000200,
        SEE_MASK_FLAG_NO_UI = 0x00000400,
        SEE_MASK_UNICODE = 0x00004000,
        SEE_MASK_NO_CONSOLE = 0x00008000,
        SEE_MASK_ASYNCOK = 0x00100000,
        SEE_MASK_HMONITOR = 0x00200000,
        SEE_MASK_NOZONECHECKS = 0x00800000,
        SEE_MASK_NOQUERYCLASSSTORE = 0x01000000,
        SEE_MASK_WAITFORINPUTIDLE = 0x02000000,
        SEE_MASK_FLAG_LOG_USAGE = 0x04000000,
    }

    public const ushort IMAGE_SUBSYSTEM_NATIVE = 0x0001;
    public const ushort IMAGE_FILE_EXECUTABLE_IMAGE = 0x2;
    public const ushort IMAGE_FILE_DLL = 0x2000;

    public const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
    public const ushort PROCESSOR_ARCHITECTURE_ARM = 5;
    public const ushort PROCESSOR_ARCHITECTURE_IA64 = 6;
    public const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;
    public const ushort PROCESSOR_ARCHITECTURE_ARM64 = 12;

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public IntPtr lpMinimumApplicationAddress;
        public IntPtr lpMaximumApplicationAddress;
        public IntPtr dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    public sealed class HResult
    {
        public const int S_OK = 0;
        public const int E_ACCESSDENIED = unchecked((int)0x80070005);
        public const int E_INVALIDARG = unchecked((int)0x80070057);
        public const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
        public const int STG_E_ACCESSDENIED = unchecked((int)0x80030005);
    }

    [Flags]
    public enum LoadLibraryFlags : uint
    {
        DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
        LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
        LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008,
        LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
        LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
        LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
        LOAD_LIBRARY_REQUIRE_SIGNED_TARGET = 0x00000080,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,
        LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,
        LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,
        LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,
        LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr LoadLibraryEx([MarshalAs(UnmanagedType.LPWStr)] string lpFileName, IntPtr hFile, LoadLibraryFlags dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("user32", SetLastError = true)]
    internal static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg,
           ChangeWindowMessageFilterExAction action, ref CHANGEFILTERSTRUCT pChangeFilterStruct);

    [StructLayout(LayoutKind.Sequential)]
    internal struct CHANGEFILTERSTRUCT
    {
        public uint cbSize;
        public uint dwFlags;
    }

    internal enum ChangeWindowMessageFilterExAction : uint
    {
        Allow = 1
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern UInt32 SendMessage(IntPtr hWnd, UInt32 msg, UInt32 wParam, UInt32 lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern void DragAcceptFiles(IntPtr hWnd, bool accept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint DragQueryFile(IntPtr hDrop, uint iFile,
        [Out] StringBuilder lpszFile, uint cch);

    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern void DragQueryPoint(IntPtr hDrop, ref POINT ppt);

    [DllImport("shell32.dll")]
    internal static extern void DragFinish(IntPtr hDrop);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint SearchPath([MarshalAs(UnmanagedType.LPWStr)] string lpPath,
        [MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpExtension,
        UInt32 nBufferLength,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpBuffer,
        out IntPtr lpFilePart);

    internal enum MessageFilterInfo : uint
    {
        None,
        AlreadyAllowed,
        AlreadyDisAllowed,
        AllowedHigher
    }

    #endregion

    /// <summary>
    /// Adds an UAC shield icon to the Button control.
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    static internal uint AddShieldToButton(Button b)
    {
        b.FlatStyle = FlatStyle.System;
        return SendMessage(b.Handle, BCM_SETSHIELD, 0, 0xFFFFFFFF);
    }

    /// <summary>
    /// Runs explorer "properties" dialog.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    static internal bool ShowFileProperties(string fileName)
    {
        try
        {
            SHELLEXECUTEINFO info = new()
            {
                cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
                lpVerb = "properties",
                lpFile = fileName,
                nShow = ShowCommands.SW_SHOW,
                fMask = ShellExecuteMaskFlags.SEE_MASK_INVOKEIDLIST
            };
            return ShellExecuteEx(ref info);
        }
        catch { return false; }
    }

    /// <summary>
    /// Resolves the target path of a Windows shortcut (.lnk) file
    /// </summary>
    /// <param name="lnkFileName">Full path to the shortcut file</param>
    /// <returns>The resolved target path, or null if resolution fails</returns>
    static internal string ResolveShortcutTarget(string lnkFileName)
    {
        ShellLink link = null;
        try
        {
            link = new ShellLink();

            IPersistFile persistFile = (IPersistFile)link;
            if (persistFile.Load(lnkFileName, STGM_READ) != HResult.S_OK)
            {
                return null;
            }

            IShellLinkW shellLink = (IShellLinkW)link;

            StringBuilder pathBuffer = new StringBuilder(MAX_PATH);

            WIN32_FIND_DATAW findData = new WIN32_FIND_DATAW();

            if (shellLink.GetPath(pathBuffer, pathBuffer.Capacity, out findData, 0) == HResult.S_OK)
            {
                string targetPath = pathBuffer.ToString();

                int nullPos = targetPath.IndexOf('\0');
                if (nullPos >= 0)
                {
                    return targetPath.Substring(0, nullPos);
                }

                return targetPath;
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            if (link != null)
            {
                try
                {
                    Marshal.ReleaseComObject(link);
                }
                catch
                {
                }
                finally
                {
                    link = null;
                }
            }
        }

        return null;
    }

}

/// <summary>
/// Provides UAC-aware drag and drop support
/// </summary>
public sealed class ElevatedDragDropManager : IMessageFilter, IDisposable
{
    private static readonly Lazy<ElevatedDragDropManager> _instance =
        new(() => new ElevatedDragDropManager());

    public event EventHandler<ElevatedDragDropEventArgs>? ElevatedDragDrop;

    private bool _disposed;

    private ElevatedDragDropManager()
    {
        Application.AddMessageFilter(this);
    }

    public static ElevatedDragDropManager Instance => _instance.Value;

    /// <summary>
    /// Enables drag-drop support for a window handle
    /// </summary>
    public static void EnableForWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        var changeStruct = new NativeMethods.CHANGEFILTERSTRUCT
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.CHANGEFILTERSTRUCT>()
        };

        AllowMessage(hWnd, NativeMethods.WM_DROPFILES, ref changeStruct);
        AllowMessage(hWnd, NativeMethods.WM_COPYDATA, ref changeStruct);
        AllowMessage(hWnd, NativeMethods.WM_COPYGLOBALDATA, ref changeStruct);

        NativeMethods.DragAcceptFiles(hWnd, true);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_DROPFILES)
        {
            ProcessDropMessage(m);
            return true;
        }
        return false;
    }

    private void ProcessDropMessage(Message m)
    {
        IntPtr hDrop = m.WParam;
        try
        {
            uint fileCount = NativeMethods.DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            var files = new List<string>((int)fileCount);
            var buffer = new StringBuilder(512);

            for (uint i = 0; i < fileCount; i++)
            {
                uint charsNeeded = NativeMethods.DragQueryFile(hDrop, i, null, 0);
                if (charsNeeded == 0) continue;

                buffer.EnsureCapacity((int)charsNeeded + 1);
                NativeMethods.DragQueryFile(hDrop, i, buffer, charsNeeded + 1);
                files.Add(buffer.ToString());
                buffer.Clear();
            }

            var pt = new NativeMethods.POINT();
            NativeMethods.DragQueryPoint(hDrop, ref pt);

            ElevatedDragDrop?.Invoke(this, new ElevatedDragDropEventArgs
            {
                HWnd = m.HWnd,
                Files = files.AsReadOnly(),
                X = pt.X,
                Y = pt.Y
            });
        }
        finally
        {
            NativeMethods.DragFinish(hDrop);
        }
    }

    private static void AllowMessage(IntPtr hWnd, uint message,
        ref NativeMethods.CHANGEFILTERSTRUCT changeStruct)
    {
        NativeMethods.ChangeWindowMessageFilterEx(hWnd, message,
            NativeMethods.ChangeWindowMessageFilterExAction.Allow, ref changeStruct);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Application.RemoveMessageFilter(this);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~ElevatedDragDropManager() => Dispose();
}

public sealed class ElevatedDragDropEventArgs : EventArgs
{
    public IntPtr HWnd { get; init; }
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    public int X { get; init; }
    public int Y { get; init; }
}
