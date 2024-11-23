/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       NATIVEMETHODS.SHELLLINK.CS
*
*  VERSION:     1.00
*  
*  DATE:        24 Sep 2024
*
*  Win32 API P/Invoke for IShellLink COM interface.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.InteropServices;
using System.Text;

namespace WinDepends;

static partial class NativeMethods
{
    [Flags()]
    internal enum SLGP_FLAGS : uint
    {
        SLGP_SHORTPATH = 1,
        SLGP_UNCPRIORITY = 2,
        SLGP_RAWPATH = 4
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public long ftCreationTime;
        public long ftLastAccessTime;
        public long ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [Flags()]
    internal enum SLR_FLAGS : uint
    {
        SLR_NO_UI = 0x1,
        SLR_ANY_MATCH = 0x2,
        SLR_UPDATE = 0x4,
        SLR_NOUPDATE = 0x8,
        SLR_NOSEARCH = 0x10,
        SLR_NOTRACK = 0x20,
        SLR_NOLINKINFO = 0x40,
        SLR_INVOKE_MSI = 0x80
    }

    [ComImport(), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        Int32 GetPath([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, SLGP_FLAGS fFlags);
        Int32 GetIDList(out IntPtr ppidl);
        Int32 SetIDList(IntPtr pidl);
        Int32 GetDescription([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        Int32 SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        Int32 GetWorkingDirectory([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        Int32 SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        Int32 GetArguments([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        Int32 SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        Int32 GetHotkey(out short pwHotkey);
        Int32 SetHotkey(short wHotkey);
        Int32 GetShowCmd(out int piShowCmd);
        Int32 SetShowCmd(int iShowCmd);
        Int32 GetIconLocation([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        Int32 SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        Int32 SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        Int32 Resolve(IntPtr hwnd, SLR_FLAGS fFlags);
        Int32 SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010c-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersist
    {
        Int32 GetClassID(out Guid pClassID);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistFile : IPersist
    {
        new void GetClassID(out Guid pClassID);

        int IsDirty();

        Int32 Load([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

        Int32 Save([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);

        Int32 SaveCompleted([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

        Int32 GetCurFile([In, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
    }

    [ComImport(), Guid("00021401-0000-0000-C000-000000000046")]
    public class ShellLink
    {
    }
}
