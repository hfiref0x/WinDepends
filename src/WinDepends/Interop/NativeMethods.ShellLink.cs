/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       NATIVEMETHODS.SHELLLINK.CS
*
*  VERSION:     1.00
*  
*  DATE:        04 Jun 2025
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
    /// <summary>
    /// Flags for the GetPath method in IShellLinkW interface.
    /// </summary>
    [Flags()]
    internal enum SLGP_FLAGS : uint
    {
        /// <summary>Gets the standard short (8.3 format) file name.</summary>
        SLGP_SHORTPATH = 1,
        /// <summary>Gets the Universal Naming Convention (UNC) path name of the file.</summary>
        SLGP_UNCPRIORITY = 2,
        /// <summary>Gets the raw path name. A raw path is something that might not exist and may include environment variables.</summary>
        SLGP_RAWPATH = 4
    }

    /// <summary>
    /// Contains information about a file object.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WIN32_FIND_DATAW
    {
        /// <summary>File attributes of the file.</summary>
        public uint dwFileAttributes;
        /// <summary>Time the file was created.</summary>
        public long ftCreationTime;
        /// <summary>Time the file was last accessed.</summary>
        public long ftLastAccessTime;
        /// <summary>Time the file was last modified.</summary>
        public long ftLastWriteTime;
        /// <summary>High-order DWORD of the file size.</summary>
        public uint nFileSizeHigh;
        /// <summary>Low-order DWORD of the file size.</summary>
        public uint nFileSizeLow;
        /// <summary>Reserved for future use.</summary>
        public uint dwReserved0;
        /// <summary>Reserved for future use.</summary>
        public uint dwReserved1;
        /// <summary>Name of the file.</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        /// <summary>Alternative name for the file (8.3 format).</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    /// <summary>
    /// Flags for the Resolve method in IShellLinkW interface.
    /// </summary>
    [Flags()]
    internal enum SLR_FLAGS : uint
    {
        /// <summary>Do not display a dialog box if the link cannot be resolved.</summary>
        SLR_NO_UI = 0x1,
        /// <summary>Not used.</summary>
        SLR_ANY_MATCH = 0x2,
        /// <summary>Update the link state.</summary>
        SLR_UPDATE = 0x4,
        /// <summary>Do not update the link state.</summary>
        SLR_NOUPDATE = 0x8,
        /// <summary>Do not search for the link.</summary>
        SLR_NOSEARCH = 0x10,
        /// <summary>Do not store a link state.</summary>
        SLR_NOTRACK = 0x20,
        /// <summary>Do not use cached information when resolving the link.</summary>
        SLR_NOLINKINFO = 0x40,
        /// <summary>Activate the Microsoft Windows Installer if necessary.</summary>
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

    /// <summary>
    /// Provides the methods that enable an object to persist (be written to and loaded from a stream).
    /// </summary>
    [ComImport, Guid("0000010c-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersist
    {
        Int32 GetClassID(out Guid pClassID);
    }

    /// <summary>
    /// Provides methods that enable an object to be loaded from or saved to a file.
    /// </summary>
    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistFile : IPersist
    {
        new Int32 GetClassID(out Guid pClassID);

        int IsDirty();

        Int32 Load([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

        Int32 Save([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);

        Int32 SaveCompleted([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

        Int32 GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
    }

    /// <summary>
    /// CoClass that implements the IShellLinkW interface for creating shell links.
    /// </summary>
    [ComImport(), Guid("00021401-0000-0000-C000-000000000046")]
    public class ShellLink
    {
    }
}
