/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CACTCTXHELPER.CS
*
*  VERSION:     1.00
*
*  DATE:        02 Jun 2025
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
    readonly static IntPtr INVALID_HANDLE_VALUE = new(-1);
    public IntPtr ActivationContext { get; set; } = INVALID_HANDLE_VALUE;
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

    private const uint ACTCTX_FLAG_SET =
        0x004 | // ACTCTX_FLAG_ASSEMBLY_DIRECTORY_VALID
        0x008 | // ACTCTX_FLAG_RESOURCE_NAME_VALID 
        0x020;  // ACTCTX_FLAG_APPLICATION_NAME_VALID
    #endregion

    /// <summary>
    /// Creates a new activation context helper for the specified file.
    /// </summary>
    /// <param name="fileName">Path to the file containing the manifest.</param>
    /// <exception cref="ArgumentNullException">Thrown if fileName is null or empty.</exception>
    public CActCtxHelper(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

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
            dwFlags = ACTCTX_FLAG_SET,
            lpSource = fileName,
            lpApplicationName = fileName,
            lpAssemblyDirectory = Path.GetDirectoryName(fileName),
            lpResourceName = resourceId
        };
        ActivationContext = CreateActCtx(ref requestedActivationContext);
    }

    /// <summary>
    /// Activates the context and returns success or failure.
    /// </summary>
    /// <returns>True if activation succeeded; otherwise, false.</returns>
    public bool ActivateContext()
    {
        return ActivationContext != INVALID_HANDLE_VALUE &&
                       ActivateActCtx(ActivationContext, out contextCookie);
    }
    /// <summary>
    /// Deactivates the current context.
    /// </summary>
    /// <returns>True if deactivation succeeded; otherwise, false.</returns>
    public bool DeactivateContext()
    {
        if (contextCookie == IntPtr.Zero)
            return false;

        if (DeactivateActCtx(0, contextCookie))
        {
            contextCookie = IntPtr.Zero;
            return true;
        }

        return false;
    }
    /// <summary>
    /// Deactivates the current context and returns the cookie for later reactivation.
    /// </summary>
    /// <returns>The deactivation cookie, or IntPtr.Zero if deactivation failed.</returns>
    public IntPtr DeactivateCurrentContext()
    {
        return ActivateActCtx(0, out IntPtr cookie) ? cookie : IntPtr.Zero;
    }
    /// <summary>
    /// Reactivates a previously deactivated context using the cookie from DeactivateCurrentContext.
    /// </summary>
    /// <param name="cookie">The cookie obtained from DeactivateCurrentContext.</param>
    public void ReactivateContext(IntPtr cookie)
    {
        if (cookie != IntPtr.Zero)
            DeactivateActCtx(0, cookie);
    }
    /// <summary>
    /// Disposes the activation context, releasing all associated resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    /// <summary>
    /// Disposes the activation context, releasing all associated resources.
    /// </summary>
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
