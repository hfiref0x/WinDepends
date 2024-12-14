/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CACTCTXHELPER.CS
*
*  VERSION:     1.00
*
*  DATE:        13 Dec 2024
*  
*  Activation context path resolution helper.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Runtime.InteropServices;

namespace WinDepends;

public class CActCtxHelper : IDisposable
{
    readonly IntPtr INVALID_HANDLE_VALUE = new(-1);
    public IntPtr ActivationContext { get; set; } = new(-1);
    IntPtr contextCookie;
    #region "P-Invoke"
    [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateActCtx(ref ACTCTX actctx);

    [DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ActivateActCtx(IntPtr hActCtx, out IntPtr lpCookie);

    [DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeactivateActCtx(int dwFlags, IntPtr lpCookie);

    [DllImport("kernel32.dll", PreserveSig = true)]
    static extern void ReleaseActCtx(IntPtr hActCtx);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ACTCTX
    {
        public int cbSize;
        public uint dwFlags;
        public string lpSource;
        public UInt16 wProcessorArchitecture;
        public UInt16 wLangId;
        public string lpAssemblyDirectory;
        public IntPtr lpResourceName;
        public string lpApplicationName;
        public IntPtr hModule;
    }

    public const ushort CREATEPROCESS_MANIFEST_RESOURCE_ID = 1;
    public const ushort ISOLATIONAWARE_MANIFEST_RESOURCE_ID = 2;
    public const ushort ISOLATIONAWARE_NOSTATICIMPORT_MANIFEST_RESOURCE_ID = 3;

    public const int ACTCTX_FLAG_PROCESSOR_ARCHITECTURE_VALID = 0x001;
    public const int ACTCTX_FLAG_ASSEMBLY_DIRECTORY_VALID = 0x004;
    public const int ACTCTX_FLAG_RESOURCE_NAME_VALID = 0x008;
    public const int ACTCTX_FLAG_APPLICATION_NAME_VALID = 0x020;
    #endregion

    public CActCtxHelper(string fileName)
    {
        nint resourceId = CREATEPROCESS_MANIFEST_RESOURCE_ID;
        string extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension) &&
            extension.Equals(CConsts.DllFileExt, StringComparison.OrdinalIgnoreCase))
        {
            resourceId = ISOLATIONAWARE_MANIFEST_RESOURCE_ID;
        }

        var requestedActivationContext = new ACTCTX
        {
            cbSize = Marshal.SizeOf<ACTCTX>(),

            dwFlags = ACTCTX_FLAG_ASSEMBLY_DIRECTORY_VALID |
                ACTCTX_FLAG_RESOURCE_NAME_VALID |
                ACTCTX_FLAG_APPLICATION_NAME_VALID,

            lpSource = fileName,
            lpApplicationName = fileName,
            lpAssemblyDirectory = Path.GetDirectoryName(fileName),
            lpResourceName = resourceId
        };
        ActivationContext = CreateActCtx(ref requestedActivationContext);
    }

    public bool ActivateContext()
    {
        if (ActivationContext != INVALID_HANDLE_VALUE)
        {
            return ActivateActCtx(ActivationContext, out contextCookie);
        }
        return false;
    }

    public bool ActivateContextOverride()
    {
        return ActivateActCtx(0, out contextCookie);
    }

    public bool DeactivateContext()
    {
        bool result = false;
        if (contextCookie != IntPtr.Zero)
        {
            result = DeactivateActCtx(0, contextCookie);
            if (result)
            {
                contextCookie = IntPtr.Zero;
            }
        }
        return result;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (contextCookie != IntPtr.Zero)
            {
                DeactivateActCtx(0, contextCookie);
                contextCookie = IntPtr.Zero;
            }

            if (ActivationContext != INVALID_HANDLE_VALUE)
            {
                ReleaseActCtx(ActivationContext);
                ActivationContext = INVALID_HANDLE_VALUE;
            }
        }
    }
}
