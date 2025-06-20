/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025
*
*  TITLE:       CASSEMBLYREFANALYZER.CS
*
*  VERSION:     1.00
*
*  DATE:        20 Jun 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace WinDepends;

/// <summary>
/// Provides functionality for analyzing .NET assembly references and resolving dependencies.
/// This class helps with identifying and locating assembly references in the Global Assembly Cache (GAC)
/// and other common framework locations.
/// </summary>
public static class CAssemblyRefAnalyzer
{
    // Fusion.dll dynamic loading
    private static IntPtr fusionHandle = IntPtr.Zero;

    private static bool TryLoadFusion()
    {
        if (fusionHandle != IntPtr.Zero)
            return true;

        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] candidates;

        if (Environment.Is64BitProcess)
        {
            candidates = new[]
            {
                Path.Combine(windir, "Microsoft.NET", "Framework64", "v4.0.30319", "fusion.dll"),
                Path.Combine(windir, "Microsoft.NET", "Framework64", "v2.0.50727", "fusion.dll"),
            };
        }
        else
        {
            candidates = new[]
            {
                Path.Combine(windir, "Microsoft.NET", "Framework", "v4.0.30319", "fusion.dll"),
                Path.Combine(windir, "Microsoft.NET", "Framework", "v2.0.50727", "fusion.dll"),
            };
        }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                fusionHandle = NativeMethods.LoadLibraryEx(path, 0, 0);
                if (fusionHandle != IntPtr.Zero)
                    return true;
            }
        }
        return false;
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("21B8916C-F28E-11D2-A473-00C04F8EF448")]
    private interface IAssemblyEnum
    {
        [PreserveSig]
        int GetNextAssembly(IntPtr pvReserved, out IAssemblyName ppName, int flags);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone(out IAssemblyEnum ppEnum);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("CD193BC0-B4BC-11d2-9833-00C04FC31D2E")]
    private interface IAssemblyName
    {
        [PreserveSig]
        int SetProperty(int PropertyId, IntPtr pvProperty, int cbProperty);

        [PreserveSig]
        int GetProperty(int PropertyId, IntPtr pvProperty, ref int pcbProperty);

        [PreserveSig]
        int Finalize();

        [PreserveSig]
        int GetDisplayName([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder szDisplayName, ref int pccDisplayName, int displayFlags);

        [PreserveSig]
        int Reserved(ref Guid guid, object obj1, object obj2, string str, long llFlags, int pvReserved, int cbReserved, out int ppReserved);

        [PreserveSig]
        int GetName(ref int lpcwBuffer, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwzName);

        [PreserveSig]
        int GetVersion(out int versionHi, out int versionLow);

        [PreserveSig]
        int IsEqual(IAssemblyName pName, int cmpFlags);

        [PreserveSig]
        int Clone(out IAssemblyName pName);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateAssemblyEnumDelegate(out IAssemblyEnum ppEnum, IntPtr pUnkReserved, IAssemblyName pName, int flags, IntPtr pvReserved);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateAssemblyNameObjectDelegate(out IAssemblyName ppAssemblyNameObj, [MarshalAs(UnmanagedType.LPWStr)] string szAssemblyName, int flags, IntPtr pvReserved);

    private static int CreateAssemblyEnum(out IAssemblyEnum ppEnum, IntPtr pUnkReserved, IAssemblyName pName, int flags, IntPtr pvReserved)
    {
        ppEnum = null;
        if (!TryLoadFusion())
            return -1;
        var addr = NativeMethods.GetProcAddress(fusionHandle, "CreateAssemblyEnum");
        if (addr == IntPtr.Zero)
            return -1;
        var del = (CreateAssemblyEnumDelegate)Marshal.GetDelegateForFunctionPointer(addr, typeof(CreateAssemblyEnumDelegate));
        return del(out ppEnum, pUnkReserved, pName, flags, pvReserved);
    }

    private static int CreateAssemblyNameObject(out IAssemblyName ppAssemblyNameObj, string szAssemblyName, int flags, IntPtr pvReserved)
    {
        ppAssemblyNameObj = null;
        if (!TryLoadFusion())
            return -1;
        var addr = NativeMethods.GetProcAddress(fusionHandle, "CreateAssemblyNameObject");
        if (addr == IntPtr.Zero)
            return -1;
        var del = (CreateAssemblyNameObjectDelegate)Marshal.GetDelegateForFunctionPointer(addr, typeof(CreateAssemblyNameObjectDelegate));
        return del(out ppAssemblyNameObj, szAssemblyName, flags, pvReserved);
    }

    /// <summary>
    /// Checks whether a candidate assembly file matches the required CPU type.
    /// For managed assemblies, allows AnyCPU or the required architecture.
    /// For native, requires exact architecture match.
    /// </summary>
    private static bool IsValidArchitecture(string candidatePath, CpuType requiredCpuType)
    {
        try
        {
            using var fs = new FileStream(candidatePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(fs);
            var machine = peReader.PEHeaders.CoffHeader.Machine;
            var isManaged = peReader.HasMetadata;

            if (requiredCpuType == CpuType.X64)
            {
                if (isManaged)
                {
                    var corHeader = peReader.PEHeaders.CorHeader;
                    if (corHeader != null)
                    {
                        var flags = corHeader.Flags;
                        bool ilOnly = (flags & CorFlags.ILOnly) != 0;
                        bool req32 = (flags & CorFlags.Requires32Bit) != 0;

                        // AnyCPU (ILOnly, !Requires32Bit), or x64 is valid
                        if (machine == System.Reflection.PortableExecutable.Machine.I386 && ilOnly && !req32)
                            return true; // AnyCPU
                        if (machine == System.Reflection.PortableExecutable.Machine.Amd64)
                            return true; // x64
                        return false; // x86-only managed
                    }
                }
                else
                {
                    return machine == System.Reflection.PortableExecutable.Machine.Amd64;
                }
            }
            else if (requiredCpuType == CpuType.X86)
            {
                if (isManaged)
                {
                    var corHeader = peReader.PEHeaders.CorHeader;
                    if (corHeader != null)
                    {
                        var flags = corHeader.Flags;
                        bool ilOnly = (flags & CorFlags.ILOnly) != 0;
                        bool req32 = (flags & CorFlags.Requires32Bit) != 0;
                        bool pref32 = (flags & CorFlags.Prefers32Bit) != 0;

                        // x86, or AnyCPU w/ Requires32Bit, or AnyCPU w/ Prefers32Bit, or AnyCPU (ILOnly)
                        if (machine == System.Reflection.PortableExecutable.Machine.I386)
                        {
                            if (req32 || pref32 || ilOnly)
                                return true; // 32-bit compatible
                        }
                        return false; // x64-only managed
                    }
                }
                else
                {
                    // Native: Must be x86
                    return machine == System.Reflection.PortableExecutable.Machine.I386;
                }
            }
            else if (requiredCpuType == CpuType.AnyCpu)
            {
                if (isManaged)
                {
                    var corHeader = peReader.PEHeaders.CorHeader;
                    if (corHeader != null)
                    {
                        var flags = corHeader.Flags;
                        bool ilOnly = (flags & CorFlags.ILOnly) != 0;
                        if (machine == System.Reflection.PortableExecutable.Machine.I386 && ilOnly)
                            return true;
                        if (machine == System.Reflection.PortableExecutable.Machine.Amd64)
                            return true;
                        return false;
                    }
                }
                else
                {
                    return machine == System.Reflection.PortableExecutable.Machine.Amd64 ||
                           machine == System.Reflection.PortableExecutable.Machine.I386;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    /// <summary>
    /// Searches for an assembly in the Global Assembly Cache (GAC).
    /// </summary>
    public static string FindInGac(string assemblyName, string publicKeyToken, CpuType cpuType)
    {
        if (!TryLoadFusion())
            return null;

        IAssemblyName nameObj;
        int hr = CreateAssemblyNameObject(out nameObj, assemblyName, 0, IntPtr.Zero);
        if (hr != 0 || nameObj == null)
            return null;

        hr = CreateAssemblyEnum(out var enumObj, IntPtr.Zero, nameObj, 2 /* GAC */, IntPtr.Zero);
        if (hr != 0 || enumObj == null)
            return null;

        while (enumObj.GetNextAssembly(IntPtr.Zero, out var foundName, 0) == 0 && foundName != null)
        {
            int disp = 1024;
            var sb = new System.Text.StringBuilder(1024);
            foundName.GetDisplayName(sb, ref disp, 0x0);
            string display = sb.ToString();
            if (display.StartsWith(assemblyName + ",", StringComparison.OrdinalIgnoreCase)
                && display.Contains("PublicKeyToken=" + publicKeyToken, StringComparison.OrdinalIgnoreCase))
            {
                string path = GetAssemblyPathFromGacDisplay(display, cpuType);
                if (File.Exists(path) && IsValidArchitecture(path, cpuType))
                    return path;
            }
        }
        return null;
    }

    private static string GetAssemblyPathFromGacDisplay(string display, CpuType cpuType)
    {
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string baseGac = Path.Combine(windir, "Microsoft.NET", "assembly", "GAC_MSIL");

        string[] parts = display.Split(',');
        if (parts.Length < 4) return null;
        string name = parts[0].Trim();
        string version = parts[1].Split('=')[1].Trim();
        string culture = parts[2].Split('=')[1].Trim();
        string pkt = parts[3].Split('=')[1].Trim();

        string archSuffix = null;
        if (cpuType == CpuType.X86)
        {
            archSuffix = "GAC_32";
        }
        else if (cpuType == CpuType.X64)
        {
            archSuffix = "GAC_64";
        }

        string subdir = $"{version}_{culture}_{pkt}";

        if (archSuffix != null)
        {
            string archGac = Path.Combine(windir, "assembly", archSuffix, name, subdir, name + ".dll");
            if (File.Exists(archGac))
                return archGac;
        }

        string msilGac = Path.Combine(baseGac, name, subdir, name + ".dll");
        return msilGac;
    }

    public static bool GetDotNetAssemblyReferences(
        CModule module,
        out List<(string ReferenceName, string PublicKeyToken)> references,
        out DotNetAssemblyKind kind,
        out string runtimeVersion,
        out CpuType cpuType,
        out string archString,
        out string corFlagsString)
    {
        references = new List<(string, string)>();
        kind = DotNetAssemblyKind.Unknown;
        runtimeVersion = null;
        cpuType = CpuType.Unknown;
        archString = "";
        corFlagsString = "";

        using var fs = new FileStream(module.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var peReader = new PEReader(fs);

        var peHeader = peReader.PEHeaders.PEHeader;
        if (peHeader == null)
            return false;

        var machine = peReader.PEHeaders.CoffHeader.Machine;
        archString = machine.ToString();

        switch (machine)
        {
            case System.Reflection.PortableExecutable.Machine.I386:
                cpuType = CpuType.X86;
                break;
            case System.Reflection.PortableExecutable.Machine.Amd64:
                cpuType = CpuType.X64;
                break;
            case System.Reflection.PortableExecutable.Machine.IA64:
                cpuType = CpuType.Itanium;
                break;
            case System.Reflection.PortableExecutable.Machine.Arm:
                cpuType = CpuType.Arm;
                break;
            case System.Reflection.PortableExecutable.Machine.Arm64:
                cpuType = CpuType.Arm64;
                break;
            default:
                cpuType = CpuType.Other;
                break;
        }

        if (!peReader.HasMetadata)
            return false;

        var mdReader = peReader.GetMetadataReader();
        runtimeVersion = mdReader.MetadataVersion;
        module.ModuleData.CorFlags = (uint)peReader.PEHeaders.CorHeader.Flags;

        bool hasMscorlib = false, hasSystemPrivateCoreLib = false;
        foreach (var handle in mdReader.AssemblyReferences)
        {
            var asmRef = mdReader.GetAssemblyReference(handle);
            var name = mdReader.GetString(asmRef.Name);
            if (string.Equals(name, "mscorlib", StringComparison.OrdinalIgnoreCase))
                hasMscorlib = true;
            if (string.Equals(name, "System.Private.CoreLib", StringComparison.OrdinalIgnoreCase))
                hasSystemPrivateCoreLib = true;
        }

        if (hasMscorlib && !hasSystemPrivateCoreLib)
            kind = DotNetAssemblyKind.NetFramework;
        else if (hasSystemPrivateCoreLib)
            kind = DotNetAssemblyKind.NetCoreOrNet;

        if (kind == DotNetAssemblyKind.Unknown)
        {
            if (runtimeVersion.StartsWith("v4.0.30319"))
                kind = DotNetAssemblyKind.NetFramework;
            else if (runtimeVersion.StartsWith("v4.0.0"))
                kind = DotNetAssemblyKind.NetCoreOrNet;
        }

        var corHeader = peReader.PEHeaders.CorHeader;
        if (corHeader != null)
        {
            var flags = corHeader.Flags;
            corFlagsString = flags.ToString();

            bool ilOnly = (flags & CorFlags.ILOnly) != 0;
            bool req32 = (flags & CorFlags.Requires32Bit) != 0;
            bool pref32 = (flags & CorFlags.Prefers32Bit) != 0;

            if (machine == System.Reflection.PortableExecutable.Machine.I386 && ilOnly)
            {
                if (req32)
                    cpuType = CpuType.X86;
                else if (pref32)
                    cpuType = CpuType.AnyCpu; // AnyCPU prefers 32-bit
                else
                    cpuType = CpuType.AnyCpu; // AnyCPU
            }
        }

        foreach (var handle in mdReader.AssemblyReferences)
        {
            var asmRef = mdReader.GetAssemblyReference(handle);
            var name = mdReader.GetString(asmRef.Name);
            var pkt = BitConverter.ToString(mdReader.GetBlobBytes(asmRef.PublicKeyOrToken)).Replace("-", "").ToLowerInvariant();
            references.Add((name, pkt));
        }

        return true;
    }

    /// <summary>
    /// Attempts to locate the full path of an assembly on the system,
    /// ensuring that the candidate matches the required CPU type.
    /// </summary>
    public static string FindAssemblyFullPath(string assemblyName, string publicKeyToken, DotNetAssemblyKind kind, CpuType cpuType, string inputAssemblyPath)
    {
        // 1. Check input dir
        string dir = Path.GetDirectoryName(inputAssemblyPath);
        string candidate = Path.Combine(dir, assemblyName + ".dll");
        if (File.Exists(candidate) && IsValidArchitecture(candidate, cpuType)) return candidate;

        if (kind == DotNetAssemblyKind.NetFramework)
        {
            // 2. Try real GAC enumeration, prefer arch-specific GAC
            string gacPath = FindInGac(assemblyName, publicKeyToken, cpuType);
            if (gacPath != null && File.Exists(gacPath) && IsValidArchitecture(gacPath, cpuType))
                return gacPath;

            // 3. Check Windows\Microsoft.NET\Framework or Framework64 and WPF subdir
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string frameworkBase = (cpuType == CpuType.X64) ? "Framework64" : "Framework";
            string frameworkDir = Path.Combine(windir, "Microsoft.NET", frameworkBase);

            if (Directory.Exists(frameworkDir))
            {
                foreach (var versionDir in Directory.GetDirectories(frameworkDir))
                {
                    // Check main dir
                    candidate = Path.Combine(versionDir, assemblyName + ".dll");
                    if (File.Exists(candidate) && IsValidArchitecture(candidate, cpuType)) return candidate;

                    // Check WPF subdir
                    string wpfDir = Path.Combine(versionDir, "WPF");
                    candidate = Path.Combine(wpfDir, assemblyName + ".dll");
                    if (File.Exists(candidate) && IsValidArchitecture(candidate, cpuType)) return candidate;
                }
            }
        }
        else if (kind == DotNetAssemblyKind.NetCoreOrNet)
        {
            // 4. Check runtime/shared folders
            string runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            candidate = Path.Combine(runtimeDir, assemblyName + ".dll");
            if (File.Exists(candidate) && IsValidArchitecture(candidate, cpuType)) return candidate;

            string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrEmpty(dotnetRoot))
            {
                string shared = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
                if (Directory.Exists(shared))
                {
                    foreach (var versionDir in Directory.GetDirectories(shared))
                    {
                        candidate = Path.Combine(versionDir, assemblyName + ".dll");
                        if (File.Exists(candidate) && IsValidArchitecture(candidate, cpuType)) return candidate;
                    }
                }
            }
        }

        return null;
    }

    public static class CorFlagsHelper
    {
        /// <summary>
        /// Returns true if the assembly is AnyCPU (ILOnly and not Requires32Bit).
        /// </summary>
        public static bool IsAnyCpu(CorFlags corFlags)
        {
            return (corFlags & CorFlags.ILOnly) != 0 && (corFlags & CorFlags.Requires32Bit) == 0;
        }

        /// <summary>
        /// Returns true if the assembly is x86-only (Requires32Bit is set).
        /// </summary>
        public static bool IsX86Only(CorFlags corFlags)
        {
            return (corFlags & CorFlags.Requires32Bit) != 0;
        }

        /// <summary>
        /// Returns true if the assembly prefers 32-bit (Prefers32Bit is set, only on ILOnly assemblies).
        /// </summary>
        public static bool Prefers32Bit(CorFlags corFlags)
        {
            return (corFlags & CorFlags.Prefers32Bit) != 0;
        }
    }
}
