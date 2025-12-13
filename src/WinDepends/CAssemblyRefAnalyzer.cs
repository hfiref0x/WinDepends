/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025
*
*  TITLE:       CASSEMBLYREFANALYZER.CS
*
*  VERSION:     1.00
*
*  DATE:        10 Dec 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace WinDepends;

/// <summary>
/// Types of .NET assemblies based on runtime environment
/// </summary>
public enum DotNetAssemblyKind
{
    Unknown,
    NetFramework,
    NetCoreOrNet
}

/// <summary>
/// CPU architecture types for assemblies
/// </summary>
public enum CpuType
{
    Unknown,
    X86,
    X64,
    AnyCpu,
    Itanium,
    Arm,
    Arm64,
    Other
}

/// <summary>
/// Represents a resolved assembly reference
/// </summary>
public class AssemblyReference
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string PublicKeyToken { get; set; }
    public string Culture { get; set; }
    public string ResolvedPath { get; set; }
    public bool IsResolved => !string.IsNullOrEmpty(ResolvedPath);
    public string ResolutionSource { get; set; }
    public bool IsNative { get; set; }
    public bool IsSystemAssembly { get; set; }
}

/// <summary>
/// Provides functionality for analyzing .NET assembly references and resolving dependencies.
/// This class helps with identifying and locating assembly references in the Global Assembly Cache (GAC)
/// and other common framework locations.
/// </summary>
public static class CAssemblyRefAnalyzer
{
    private static IntPtr fusionHandle = IntPtr.Zero;
    private static readonly object fusionLock = new object();
    private static bool fusionInitialized = false;
    private static CreateAssemblyEnumDelegate createAssemblyEnumDelegate;
    private static CreateAssemblyNameObjectDelegate createAssemblyNameObjectDelegate;

    private static readonly ConcurrentDictionary<string, (string path, string source, DateTime expiration)> _resolutionCache =
        new ConcurrentDictionary<string, (string, string, DateTime)>();
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(30);
    private static long _resolutionCounter = 0;

    private static readonly ConcurrentDictionary<string, (bool isValid, DateTime expiration)> _archValidationCache =
        new ConcurrentDictionary<string, (bool, DateTime)>();
    private static readonly TimeSpan _archCacheTtl = TimeSpan.FromMinutes(30);
    private static long _archCounter = 0;

    private static readonly HashSet<string> _systemAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib", "System", "System.Core", "System.Data", "System.Web", "System.Xml",
        "System.Configuration", "System.Drawing", "System.Windows.Forms", "System.Net.Http",
        "System.Private.CoreLib", "System.Runtime", "System.Collections", "System.Linq",
        "PresentationCore", "PresentationFramework", "WindowsBase", "System.Xaml"
    };

    /// <summary>
    /// Attempts to load fusion.dll from the appropriate .NET Framework directory and cache function pointers
    /// </summary>
    private static bool TryLoadFusion()
    {
        if (fusionInitialized)
            return fusionHandle != IntPtr.Zero;

        lock (fusionLock)
        {
            if (fusionInitialized)
                return fusionHandle != IntPtr.Zero;

            if (fusionHandle == IntPtr.Zero)
            {
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
                        {
                            var createAssemblyEnumAddr = NativeMethods.GetProcAddress(fusionHandle, "CreateAssemblyEnum");
                            var createAssemblyNameObjectAddr = NativeMethods.GetProcAddress(fusionHandle, "CreateAssemblyNameObject");

                            if (createAssemblyEnumAddr != IntPtr.Zero && createAssemblyNameObjectAddr != IntPtr.Zero)
                            {
                                createAssemblyEnumDelegate = (CreateAssemblyEnumDelegate)Marshal.GetDelegateForFunctionPointer(
                                    createAssemblyEnumAddr, typeof(CreateAssemblyEnumDelegate));

                                createAssemblyNameObjectDelegate = (CreateAssemblyNameObjectDelegate)Marshal.GetDelegateForFunctionPointer(
                                    createAssemblyNameObjectAddr, typeof(CreateAssemblyNameObjectDelegate));

                                break;
                            }
                            else
                            {
                                NativeMethods.FreeLibrary(fusionHandle);
                                fusionHandle = IntPtr.Zero;
                            }
                        }
                    }
                }
            }

            fusionInitialized = true;
            return fusionHandle != IntPtr.Zero;
        }
    }

    #region COM Interop for GAC Access

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

        return createAssemblyEnumDelegate(out ppEnum, pUnkReserved, pName, flags, pvReserved);
    }

    private static int CreateAssemblyNameObject(out IAssemblyName ppAssemblyNameObj, string szAssemblyName, int flags, IntPtr pvReserved)
    {
        ppAssemblyNameObj = null;
        if (!TryLoadFusion())
            return -1;

        return createAssemblyNameObjectDelegate(out ppAssemblyNameObj, szAssemblyName, flags, pvReserved);
    }

    private static void ReleaseComObject(object obj)
    {
        if (obj != null && Marshal.IsComObject(obj))
        {
            Marshal.ReleaseComObject(obj);
        }
    }

    #endregion

    /// <summary>
    /// Analyzes a .NET assembly file and returns a list of all its assembly references with resolved paths.
    /// Uses parallel processing for improved performance with many references.
    /// </summary>
    /// <param name="module">CModule instance representing the .NET assembly file</param>
    /// <returns>List of assembly references with full resolved paths</returns>
    public static List<AssemblyReference> AnalyzeAssemblyReferences(CModule module)
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        if (!File.Exists(module.FileName))
            throw new FileNotFoundException($"Assembly file not found: {module.FileName}");

        var references = new List<AssemblyReference>();

        if (!TryGetAssemblyMetadata(module, out var assemblyRefs, out var kind, out var runtimeVersion, out var cpuType))
            return references;

        Dictionary<string, (Version minVersion, Version maxVersion, Version newVersion, string newPublicKeyToken)> bindingRedirects = null;
        string configFile = module.FileName + ".config";
        if (!File.Exists(configFile))
        {
            string exeConfig = Path.ChangeExtension(module.FileName, ".config");
            if (File.Exists(exeConfig))
                configFile = exeConfig;
        }

        if (File.Exists(configFile))
        {
            bindingRedirects = ParseBindingRedirects(configFile);
        }

        Dictionary<string, string> gacResults = null;
        if (kind == DotNetAssemblyKind.NetFramework)
        {
            var gacQueries = assemblyRefs
                .Where(r => !string.IsNullOrEmpty(r.publicKeyToken) && r.publicKeyToken != "null")
                .Select(r => (r.name, r.publicKeyToken))
                .ToList();

            if (gacQueries.Count > 0)
            {
                gacResults = BatchFindInGac(gacQueries, cpuType);
            }
        }

        var concurrentRefs = new ConcurrentBag<AssemblyReference>();

        Parallel.ForEach(assemblyRefs, reference =>
        {
            var asmRef = new AssemblyReference
            {
                Name = reference.name,
                PublicKeyToken = reference.publicKeyToken,
                IsSystemAssembly = IsSystemAssembly(reference.name)
            };

            if (!string.IsNullOrEmpty(reference.displayName))
            {
                string[] parts = reference.displayName.Split(',');
                if (parts.Length >= 3)
                {
                    string[] versionParts = parts[1].Split('=');
                    string[] cultureParts = parts[2].Split('=');

                    if (versionParts.Length >= 2)
                        asmRef.Version = versionParts[1].Trim();
                    if (cultureParts.Length >= 2)
                        asmRef.Culture = cultureParts[1].Trim();
                }
            }

            string publicKeyToken = reference.publicKeyToken;
            if (bindingRedirects != null &&
                bindingRedirects.TryGetValue(reference.name, out var redirect) &&
                !string.IsNullOrEmpty(asmRef.Version) &&
                Version.TryParse(asmRef.Version, out var version))
            {
                if (version >= redirect.minVersion && version <= redirect.maxVersion)
                {
                    asmRef.Version = redirect.newVersion.ToString();
                    if (!string.IsNullOrEmpty(redirect.newPublicKeyToken))
                        publicKeyToken = redirect.newPublicKeyToken;
                }
            }

            if (gacResults != null && gacResults.TryGetValue(reference.name.ToLowerInvariant(), out string gacPath))
            {
                asmRef.ResolvedPath = gacPath;
                asmRef.ResolutionSource = "Global Assembly Cache (Batch)";
            }
            else
            {
                (asmRef.ResolvedPath, asmRef.ResolutionSource) = ResolveAssemblyPath(
                    reference.name,
                    publicKeyToken,
                    kind,
                    cpuType,
                    module.FileName);
            }

            concurrentRefs.Add(asmRef);
        });

        return concurrentRefs.ToList();
    }

    /// <summary>
    /// Performs a batch query to find multiple assemblies in the GAC
    /// </summary>
    private static Dictionary<string, string> BatchFindInGac(List<(string name, string publicKeyToken)> assemblies, CpuType cpuType)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IAssemblyEnum enumObj = null;

        if (!TryLoadFusion())
            return result;

        var assemblyLookup = assemblies.ToDictionary(
            a => a.name.ToLowerInvariant(),
            a => a.publicKeyToken,
            StringComparer.OrdinalIgnoreCase);

        int hr = CreateAssemblyEnum(out enumObj, IntPtr.Zero, null, 2 /* GAC */, IntPtr.Zero);
        if (hr != 0 || enumObj == null)
            return result;

        try
        {
            IAssemblyName foundName;
            while (enumObj.GetNextAssembly(IntPtr.Zero, out foundName, 0) == 0 && foundName != null)
            {
                try
                {
                    int disp = 1024;
                    var sb = new System.Text.StringBuilder(1024);
                    foundName.GetDisplayName(sb, ref disp, 0x0);
                    string display = sb.ToString();

                    string[] parts = display.Split(',');
                    if (parts.Length > 0)
                    {
                        string name = parts[0].Trim().ToLowerInvariant();

                        if (assemblyLookup.TryGetValue(name, out string publicKeyToken))
                        {
                            if (display.Contains("PublicKeyToken=" + publicKeyToken, StringComparison.OrdinalIgnoreCase))
                            {
                                string path = GetAssemblyPathFromGacDisplay(display, cpuType);
                                if (!string.IsNullOrEmpty(path) && File.Exists(path) && IsValidArchitectureCached(path, cpuType))
                                    result[name] = path;
                            }
                        }
                    }
                }
                finally
                {
                    ReleaseComObject(foundName);
                }
            }
        }
        finally
        {
            ReleaseComObject(enumObj);
        }

        return result;
    }

    /// <summary>
    /// Determines if an assembly is a system assembly
    /// </summary>
    private static bool IsSystemAssembly(string name)
    {
        return _systemAssemblies.Contains(name);
    }

    /// <summary>
    /// Parses binding redirects from a .config file
    /// </summary>
    private static Dictionary<string, (Version minVersion, Version maxVersion, Version newVersion, string newPublicKeyToken)> ParseBindingRedirects(string configFile)
    {
        var redirects = new Dictionary<string, (Version, Version, Version, string)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            XDocument doc = XDocument.Load(configFile);
            var assemblyBindings = doc.Descendants().Where(e => e.Name.LocalName == "assemblyBinding");
            foreach (var binding in assemblyBindings)
            {
                var dependentAssemblies = binding.Elements().Where(e => e.Name.LocalName == "dependentAssembly");
                foreach (var dependentAssembly in dependentAssemblies)
                {
                    var assemblyIdentity = dependentAssembly.Elements().FirstOrDefault(e => e.Name.LocalName == "assemblyIdentity");
                    var bindingRedirect = dependentAssembly.Elements().FirstOrDefault(e => e.Name.LocalName == "bindingRedirect");

                    if (assemblyIdentity != null && bindingRedirect != null)
                    {
                        string name = assemblyIdentity.Attribute("name")?.Value;
                        string publicKeyToken = assemblyIdentity.Attribute("publicKeyToken")?.Value;
                        string oldVersionStr = bindingRedirect.Attribute("oldVersion")?.Value;
                        string newVersionStr = bindingRedirect.Attribute("newVersion")?.Value;

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(newVersionStr))
                        {
                            string[] oldVersionParts = oldVersionStr?.Split('-');
                            if (oldVersionParts?.Length == 2 &&
                                Version.TryParse(oldVersionParts[0], out Version minVersion) &&
                                Version.TryParse(oldVersionParts[1], out Version maxVersion) &&
                                Version.TryParse(newVersionStr, out Version newVersion))
                            {
                                redirects[name] = (minVersion, maxVersion, newVersion, publicKeyToken);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is System.Xml.XmlException)
        {
            // Ignore and use empty redirects set.
        }
        catch
        {
        }
        return redirects;
    }

    /// <summary>
    /// Gets assembly metadata including references, kind, and CPU type
    /// </summary>
    private static bool TryGetAssemblyMetadata(
         CModule module,
         out List<(string name, string publicKeyToken, string displayName)> references,
         out DotNetAssemblyKind kind,
         out string runtimeVersion,
         out CpuType cpuType)
    {
        references = new List<(string, string, string)>();
        kind = DotNetAssemblyKind.Unknown;
        runtimeVersion = null;
        cpuType = CpuType.Unknown;

        try
        {
            using var fs = new FileStream(module.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(fs);

            if (!peReader.HasMetadata)
                return false;

            var mdReader = peReader.GetMetadataReader();
            runtimeVersion = mdReader.MetadataVersion;
            module.ModuleData.RuntimeVersion = runtimeVersion;

            var machine = peReader.PEHeaders.CoffHeader.Machine;
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
            var corHeader = peReader.PEHeaders.CorHeader;
            if (corHeader != null)
            {
                var flags = corHeader.Flags;
                module.ModuleData.CorFlags = (uint)flags;

                if (machine == System.Reflection.PortableExecutable.Machine.I386 && (flags & CorFlags.ILOnly) != 0)
                {
                    if ((flags & CorFlags.Requires32Bit) != 0)
                        cpuType = CpuType.X86;
                    else
                        cpuType = CpuType.AnyCpu;
                }
            }

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

            module.ModuleData.FrameworkKind = kind.ToString();

            foreach (var handle in mdReader.AssemblyReferences)
            {
                var asmRef = mdReader.GetAssemblyReference(handle);
                var name = mdReader.GetString(asmRef.Name);
                var pkt = BitConverter.ToString(mdReader.GetBlobBytes(asmRef.PublicKeyOrToken)).Replace("-", "").ToLowerInvariant();
                string cultureStr = asmRef.Culture.IsNil ? "neutral" : mdReader.GetString(asmRef.Culture);
                string displayName = GetAssemblyDisplayName(name, asmRef.Version, cultureStr, pkt);

                references.Add((name, pkt, displayName));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Constructs a display name for an assembly reference
    /// </summary>
    private static string GetAssemblyDisplayName(string name, Version version, string culture, string pkt)
    {
        return $"{name}, Version={version}, Culture={culture}, PublicKeyToken={pkt}";
    }

    /// <summary>
    /// Resolves the full path of an assembly based on .NET assembly resolution rules.
    /// Uses caching for performance with TTL (time-to-live).
    /// </summary>
    private static (string path, string source) ResolveAssemblyPath(
        string name,
        string publicKeyToken,
        DotNetAssemblyKind kind,
        CpuType cpuType,
        string referringAssemblyPath)
    {
        string cacheKey = $"{name}|{publicKeyToken}|{kind}|{cpuType}|{referringAssemblyPath}";

        if (_resolutionCache.TryGetValue(cacheKey, out var cachedResult))
        {
            if (cachedResult.expiration > DateTime.UtcNow)
                return (cachedResult.path, cachedResult.source);

            _resolutionCache.TryRemove(cacheKey, out _);
        }

        long counter = Interlocked.Increment(ref _resolutionCounter);
        if (counter % 100 == 0 && _resolutionCache.Count > 100)
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _resolutionCache
                .Where(kvp => kvp.Value.expiration < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
                _resolutionCache.TryRemove(key, out _);
        }

        (string path, string source) result = ResolveAssemblyPathCore(name, publicKeyToken, kind, cpuType, referringAssemblyPath);
        _resolutionCache[cacheKey] = (result.path, result.source, DateTime.UtcNow.Add(_cacheTtl));

        return result;
    }

    /// <summary>
    /// Core assembly resolution logic implementing .NET's probing rules
    /// </summary>
    private static (string path, string source) ResolveAssemblyPathCore(
        string name,
        string publicKeyToken,
        DotNetAssemblyKind kind,
        CpuType cpuType,
        string referringAssemblyPath)
    {
        // 1. Look in the same directory as the referring assembly
        string directory = Path.GetDirectoryName(referringAssemblyPath);

        if (!string.IsNullOrEmpty(directory))
        {
            // Check for exact named assembly (name.dll)
            string localPath = Path.Combine(directory, name + ".dll");
            if (File.Exists(localPath) && IsValidArchitectureCached(localPath, cpuType))
                return (localPath, "Local directory");

            // Check .exe extension too
            localPath = Path.Combine(directory, name + ".exe");
            if (File.Exists(localPath) && IsValidArchitectureCached(localPath, cpuType))
                return (localPath, "Local directory");

            // Check for architecture-specific subdirectories
            string archSubdir = GetArchitectureSubdirectory(cpuType);
            if (!string.IsNullOrEmpty(archSubdir))
            {
                string archDir = Path.Combine(directory, archSubdir);
                if (Directory.Exists(archDir))
                {
                    localPath = Path.Combine(archDir, name + ".dll");
                    if (File.Exists(localPath) && IsValidArchitectureCached(localPath, cpuType))
                        return (localPath, $"Architecture-specific directory ({archSubdir})");

                    localPath = Path.Combine(archDir, name + ".exe");
                    if (File.Exists(localPath) && IsValidArchitectureCached(localPath, cpuType))
                        return (localPath, $"Architecture-specific directory ({archSubdir})");
                }
            }

            // Check subdirectories based on assembly name
            string privateBinPath = Path.Combine(directory, name);
            if (Directory.Exists(privateBinPath))
            {
                localPath = Path.Combine(privateBinPath, name + ".dll");
                if (File.Exists(localPath) && IsValidArchitectureCached(localPath, cpuType))
                    return (localPath, "Private bin path");

                localPath = Path.Combine(privateBinPath, name + ".exe");
                if (File.Exists(localPath) && IsValidArchitectureCached(localPath, cpuType))
                    return (localPath, "Private bin path");
            }
        }

        // 2. For .NET Framework assemblies, check GAC
        if (kind == DotNetAssemblyKind.NetFramework)
        {
            string gacPath = FindInGac(name, publicKeyToken, cpuType);
            if (!string.IsNullOrEmpty(gacPath) && File.Exists(gacPath) && IsValidArchitectureCached(gacPath, cpuType))
                return (gacPath, "Global Assembly Cache");

            // 3. Check Framework directories
            string frameworkPath = FindInFrameworkDirectory(name, cpuType);
            if (!string.IsNullOrEmpty(frameworkPath) && IsValidArchitectureCached(frameworkPath, cpuType))
                return (frameworkPath, "Framework directory");
        }
        // 4. For .NET Core/.NET 5+, check runtime directories
        else if (kind == DotNetAssemblyKind.NetCoreOrNet)
        {
            string runtimePath = FindInRuntimeDirectory(name, cpuType);
            if (!string.IsNullOrEmpty(runtimePath) && IsValidArchitectureCached(runtimePath, cpuType))
                return (runtimePath, "Runtime directory");
        }

        // 5. Check for native DLLs in system directories
        if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            string baseName = Path.GetFileNameWithoutExtension(name);
            string nativePath = FindNativeLibrary(baseName, cpuType);
            if (!string.IsNullOrEmpty(nativePath))
                return (nativePath, "Native library");
        }

        return (null, "Not resolved");
    }

    /// <summary>
    /// Gets the appropriate architecture-specific subdirectory based on CPU type
    /// </summary>
    private static string GetArchitectureSubdirectory(CpuType cpuType)
    {
        switch (cpuType)
        {
            case CpuType.X86:
                return "x86";
            case CpuType.X64:
                return "x64";
            case CpuType.Arm:
                return "arm";
            case CpuType.Arm64:
                return "arm64";
            default:
                return null;
        }
    }

    private static bool IsValidArchitectureCached(string assemblyPath, CpuType requiredCpuType)
    {
        string cacheKey = $"{assemblyPath}|{requiredCpuType}";

        if (_archValidationCache.TryGetValue(cacheKey, out var cached))
        {
            if (cached.expiration > DateTime.UtcNow)
                return cached.isValid;

            _archValidationCache.TryRemove(cacheKey, out _);
        }

        long counter = Interlocked.Increment(ref _archCounter);
        if (counter % 200 == 0 && _archValidationCache.Count > 200)
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _archValidationCache
                .Where(kvp => kvp.Value.expiration < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
                _archValidationCache.TryRemove(key, out _);
        }

        bool isValid = IsValidArchitecture(assemblyPath, requiredCpuType);
        _archValidationCache[cacheKey] = (isValid, DateTime.UtcNow.Add(_archCacheTtl));
        return isValid;
    }

    /// <summary>
    /// Searches for a native library in system directories with respect to CPU architecture
    /// </summary>
    private static string FindNativeLibrary(string libraryName, CpuType cpuType)
    {
        string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string system32Dir = Path.Combine(windowsDir, "System32");

        string candidate = Path.Combine(windowsDir, libraryName + ".dll");
        if (File.Exists(candidate) && IsArchitectureCompatible(candidate, cpuType))
            return candidate;

        candidate = Path.Combine(system32Dir, libraryName + ".dll");
        if (File.Exists(candidate) && IsArchitectureCompatible(candidate, cpuType))
            return candidate;

        switch (cpuType)
        {
            case CpuType.X86:
                if (Environment.Is64BitOperatingSystem)
                {
                    string sysWow64 = Path.Combine(windowsDir, "SysWOW64");
                    if (Directory.Exists(sysWow64))
                    {
                        candidate = Path.Combine(sysWow64, libraryName + ".dll");
                        if (File.Exists(candidate) && IsArchitectureCompatible(candidate, cpuType))
                            return candidate;
                    }
                }
                break;

            case CpuType.Arm:
                if (Environment.Is64BitOperatingSystem)
                {
                    string sysArm32 = Path.Combine(windowsDir, "SysArm32");
                    if (Directory.Exists(sysArm32))
                    {
                        candidate = Path.Combine(sysArm32, libraryName + ".dll");
                        if (File.Exists(candidate) && IsArchitectureCompatible(candidate, cpuType))
                            return candidate;
                    }
                }
                break;
        }

        string systemRootDir = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        if (!string.IsNullOrEmpty(systemRootDir) && Directory.Exists(systemRootDir))
        {
            candidate = Path.Combine(systemRootDir, libraryName + ".dll");
            if (File.Exists(candidate) && IsArchitectureCompatible(candidate, cpuType))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Simple check if native DLL is compatible with requested architecture
    /// </summary>
    private static bool IsArchitectureCompatible(string filePath, CpuType requestedCpuType)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(fs);
            var machine = peReader.PEHeaders.CoffHeader.Machine;

            switch (requestedCpuType)
            {
                case CpuType.X86:
                    return machine == System.Reflection.PortableExecutable.Machine.I386;
                case CpuType.X64:
                    return machine == System.Reflection.PortableExecutable.Machine.Amd64;
                case CpuType.Arm:
                    return machine == System.Reflection.PortableExecutable.Machine.Arm;
                case CpuType.Arm64:
                    return machine == System.Reflection.PortableExecutable.Machine.Arm64;
                default:
                    return true;
            }
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Checks if an assembly's architecture is compatible with the required CPU type
    /// </summary>
    private static bool IsValidArchitecture(string assemblyPath, CpuType requiredCpuType)
    {
        try
        {
            using var fs = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
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

                        if (machine == System.Reflection.PortableExecutable.Machine.I386 && ilOnly && !req32)
                            return true;
                        if (machine == System.Reflection.PortableExecutable.Machine.Amd64)
                            return true;
                        return false;
                    }
                }
                return machine == System.Reflection.PortableExecutable.Machine.Amd64;
            }
            else if (requiredCpuType == CpuType.X86)
            {
                if (isManaged)
                {
                    var corHeader = peReader.PEHeaders.CorHeader;
                    if (corHeader != null)
                    {
                        if (machine == System.Reflection.PortableExecutable.Machine.I386)
                            return true;
                        return false;
                    }
                }
                return machine == System.Reflection.PortableExecutable.Machine.I386;
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
                    }
                }
                return machine == System.Reflection.PortableExecutable.Machine.Amd64 ||
                       machine == System.Reflection.PortableExecutable.Machine.I386;
            }
            else if (requiredCpuType == CpuType.Arm)
            {
                if (isManaged)
                {
                    var corHeader = peReader.PEHeaders.CorHeader;
                    if (corHeader != null)
                    {
                        var flags = corHeader.Flags;
                        bool ilOnly = (flags & CorFlags.ILOnly) != 0;

                        if (machine == System.Reflection.PortableExecutable.Machine.Arm)
                            return true;

                        if (machine == System.Reflection.PortableExecutable.Machine.I386 && ilOnly)
                            return true;
                    }
                }

                return machine == System.Reflection.PortableExecutable.Machine.Arm;
            }
            else if (requiredCpuType == CpuType.Arm64)
            {
                if (isManaged)
                {
                    var corHeader = peReader.PEHeaders.CorHeader;
                    if (corHeader != null)
                    {
                        var flags = corHeader.Flags;
                        bool ilOnly = (flags & CorFlags.ILOnly) != 0;
                        bool req32 = (flags & CorFlags.Requires32Bit) != 0;

                        if (machine == System.Reflection.PortableExecutable.Machine.Arm64)
                            return true;

                        if (machine == System.Reflection.PortableExecutable.Machine.Arm)
                            return true;

                        if (machine == System.Reflection.PortableExecutable.Machine.I386 && ilOnly && !req32)
                            return true;
                    }
                }

                return machine == System.Reflection.PortableExecutable.Machine.Arm64 ||
                       machine == System.Reflection.PortableExecutable.Machine.Arm;
            }
        }
        catch
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Searches for an assembly in the Global Assembly Cache (GAC)
    /// </summary>
    public static string FindInGac(string assemblyName, string publicKeyToken, CpuType cpuType)
    {
        IAssemblyName nameObj = null;
        IAssemblyEnum enumObj = null;

        if (string.IsNullOrEmpty(publicKeyToken) || publicKeyToken == "null")
            return null;

        int hr = CreateAssemblyNameObject(out nameObj, assemblyName, 0, IntPtr.Zero);
        if (hr != 0 || nameObj == null)
            return null;

        try
        {
            hr = CreateAssemblyEnum(out enumObj, IntPtr.Zero, nameObj, 2 /* GAC */, IntPtr.Zero);
            if (hr != 0 || enumObj == null)
                return null;

            IAssemblyName foundName;
            while (enumObj.GetNextAssembly(IntPtr.Zero, out foundName, 0) == 0 && foundName != null)
            {
                try
                {
                    int disp = 1024;
                    var sb = new System.Text.StringBuilder(1024);
                    foundName.GetDisplayName(sb, ref disp, 0x0);
                    string display = sb.ToString();

                    if (display.StartsWith(assemblyName + ",", StringComparison.OrdinalIgnoreCase) &&
                        display.Contains("PublicKeyToken=" + publicKeyToken, StringComparison.OrdinalIgnoreCase))
                    {
                        string path = GetAssemblyPathFromGacDisplay(display, cpuType);
                        if (!string.IsNullOrEmpty(path) && File.Exists(path) && IsValidArchitectureCached(path, cpuType))
                            return path;
                    }
                }
                finally
                {
                    ReleaseComObject(foundName);
                }
            }
        }
        finally
        {
            ReleaseComObject(enumObj);
            ReleaseComObject(nameObj);
        }

        return null;
    }

    /// <summary>
    /// Converts a GAC display name to a file path
    /// </summary>
    private static string GetAssemblyPathFromGacDisplay(string display, CpuType cpuType)
    {
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        string[] parts = display.Split(',');
        if (parts.Length < 4) return null;
        string name = parts[0].Trim();
        string version = parts[1].Split('=')[1].Trim();
        string culture = parts[2].Split('=')[1].Trim();
        string pkt = parts[3].Split('=')[1].Trim();

        if (string.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase))
            culture = "neutral";

        string archSuffix = "GAC_MSIL";
        switch (cpuType)
        {
            case CpuType.X86:
            case CpuType.Arm:
                archSuffix = "GAC_32";
                break;
            case CpuType.X64:
            case CpuType.Arm64:
                archSuffix = "GAC_64";
                break;
        }

        string subdir = $"{version}_{culture}_{pkt}";
        string newerGacPath = Path.Combine(windir, "Microsoft.NET", "assembly", archSuffix, name, subdir, name + ".dll");
        if (File.Exists(newerGacPath))
            return newerGacPath;

        string gacFolder = Path.Combine(windir, "assembly", "GAC");
        string olderGacPath = Path.Combine(gacFolder, name, version + "_" + culture + "_" + pkt, name + ".dll");
        if (File.Exists(olderGacPath))
            return olderGacPath;

        switch (cpuType)
        {
            case CpuType.X86:
            case CpuType.Arm:
                string gac32 = Path.Combine(windir, "assembly", "GAC_32", name, version + "_" + culture + "_" + pkt, name + ".dll");
                if (File.Exists(gac32))
                    return gac32;
                break;

            case CpuType.X64:
            case CpuType.Arm64:
                string gac64 = Path.Combine(windir, "assembly", "GAC_64", name, version + "_" + culture + "_" + pkt, name + ".dll");
                if (File.Exists(gac64))
                    return gac64;
                break;
        }

        string gacMsil = Path.Combine(windir, "assembly", "GAC_MSIL", name, version + "_" + culture + "_" + pkt, name + ".dll");
        return gacMsil;
    }

    private static IEnumerable<string> EnumerateDirectoriesByVersionDesc(string rootDir)
    {
        if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir))
            yield break;

        var dirs = Directory.GetDirectories(rootDir);
        var parsed = new List<(string dir, Version ver)>();
        var rest = new List<string>();

        foreach (var d in dirs)
        {
            string name = Path.GetFileName(d);

            if (!string.IsNullOrEmpty(name) && (name.StartsWith("v", StringComparison.OrdinalIgnoreCase)))
                name = name.Substring(1);

            if (Version.TryParse(name, out var v))
                parsed.Add((d, v));
            else
                rest.Add(d);
        }

        foreach (var d in parsed.OrderByDescending(p => p.ver).Select(p => p.dir))
            yield return d;

        foreach (var d in rest.OrderByDescending(d => d))
            yield return d;
    }

    /// <summary>
    /// Searches for an assembly in the .NET Framework directories
    /// </summary>
    private static string FindInFrameworkDirectory(string assemblyName, CpuType cpuType)
    {
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        string frameworkBase;
        switch (cpuType)
        {
            case CpuType.X64:
            case CpuType.Arm64:
                frameworkBase = "Framework64";
                break;
            default:
                frameworkBase = "Framework";
                break;
        }

        string frameworkDir = Path.Combine(windir, "Microsoft.NET", frameworkBase);

        if (Directory.Exists(frameworkDir))
        {
            var directories = EnumerateDirectoriesByVersionDesc(frameworkDir);
            foreach (var versionDir in directories)
            {
                string candidate = Path.Combine(versionDir, assemblyName + ".dll");
                if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                    return candidate;

                string wpfDir = Path.Combine(versionDir, "WPF");
                if (Directory.Exists(wpfDir))
                {
                    candidate = Path.Combine(wpfDir, assemblyName + ".dll");
                    if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                        return candidate;
                }
            }
        }

        if (cpuType == CpuType.Arm || cpuType == CpuType.Arm64)
        {
            string armFrameworkDir = Path.Combine(windir, "Microsoft.NET", cpuType == CpuType.Arm ? "Framework_ARM" : "Framework_ARM64");
            if (Directory.Exists(armFrameworkDir))
            {
                var armDirectories = EnumerateDirectoriesByVersionDesc(armFrameworkDir);
                foreach (var versionDir in armDirectories)
                {
                    string candidate = Path.Combine(versionDir, assemblyName + ".dll");
                    if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                        return candidate;
                }
            }
        }

        return null;
    }

    private static string TryResolveDotnetRootFromPathEnv(string pathEnv)
    {
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        string candidateRoot = null;
        string[] paths = pathEnv.Split(Path.PathSeparator);
        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path))
                continue;

            string dotnetExe = Path.Combine(path, "dotnet.exe");
            if (File.Exists(dotnetExe))
            {
                candidateRoot = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(candidateRoot))
            return null;

        string shared = Path.Combine(candidateRoot, "shared", "Microsoft.NETCore.App");
        if (Directory.Exists(shared))
            return candidateRoot;

        return null;
    }

    /// <summary>
    /// Searches for an assembly in .NET Core/.NET runtime directories
    /// </summary>
    private static string FindInRuntimeDirectory(string assemblyName, CpuType cpuType)
    {
        string runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        string candidate = Path.Combine(runtimeDir, assemblyName + ".dll");
        if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
            return candidate;

        string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (string.IsNullOrEmpty(dotnetRoot))
        {
            switch (cpuType)
            {
                case CpuType.X64:
                case CpuType.Arm64:
                    dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT(x64)");
                    break;
                case CpuType.X86:
                    dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)");
                    break;
                case CpuType.Arm:
                    dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT(arm)");
                    break;
            }
        }

        if (string.IsNullOrEmpty(dotnetRoot))
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            dotnetRoot = TryResolveDotnetRootFromPathEnv(pathEnv);
        }

        if (!string.IsNullOrEmpty(dotnetRoot))
        {
            string rid = GetRuntimeIdentifier(cpuType);

            string sharedDir = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
            if (Directory.Exists(sharedDir))
            {
                var directories = EnumerateDirectoriesByVersionDesc(sharedDir);
                foreach (var versionDir in directories)
                {
                    if (!string.IsNullOrEmpty(rid))
                    {
                        string archDir = Path.Combine(versionDir, rid);
                        if (Directory.Exists(archDir))
                        {
                            candidate = Path.Combine(archDir, assemblyName + ".dll");
                            if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                                return candidate;
                        }
                    }

                    candidate = Path.Combine(versionDir, assemblyName + ".dll");
                    if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                        return candidate;
                }
            }

            string desktopDir = Path.Combine(dotnetRoot, "shared", "Microsoft.WindowsDesktop.App");
            if (Directory.Exists(desktopDir))
            {
                var directories = EnumerateDirectoriesByVersionDesc(desktopDir);
                foreach (var versionDir in directories)
                {
                    if (!string.IsNullOrEmpty(rid))
                    {
                        string archDir = Path.Combine(versionDir, rid);
                        if (Directory.Exists(archDir))
                        {
                            candidate = Path.Combine(archDir, assemblyName + ".dll");
                            if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                                return candidate;
                        }
                    }

                    candidate = Path.Combine(versionDir, assemblyName + ".dll");
                    if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                        return candidate;
                }
            }

            string aspnetDir = Path.Combine(dotnetRoot, "shared", "Microsoft.AspNetCore.App");
            if (Directory.Exists(aspnetDir))
            {
                var directories = EnumerateDirectoriesByVersionDesc(aspnetDir);
                foreach (var versionDir in directories)
                {
                    if (!string.IsNullOrEmpty(rid))
                    {
                        string archDir = Path.Combine(versionDir, rid);
                        if (Directory.Exists(archDir))
                        {
                            candidate = Path.Combine(archDir, assemblyName + ".dll");
                            if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                                return candidate;
                        }
                    }

                    candidate = Path.Combine(versionDir, assemblyName + ".dll");
                    if (File.Exists(candidate) && IsValidArchitectureCached(candidate, cpuType))
                        return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the .NET Core Runtime Identifier (RID) for the given CPU type
    /// </summary>
    private static string GetRuntimeIdentifier(CpuType cpuType)
    {
        switch (cpuType)
        {
            case CpuType.X86:
                return "win-x86";
            case CpuType.X64:
                return "win-x64";
            case CpuType.Arm:
                return "win-arm";
            case CpuType.Arm64:
                return "win-arm64";
            default:
                return null;
        }
    }

    /// <summary>
    /// Main entry point for analyzing a PE file's assembly dependencies
    /// </summary>
    /// <param name="module">CModule instance representing the .NET PE file</param>
    /// <returns>List of assembly references with resolved paths</returns>
    public static List<AssemblyReference> GetAssemblyDependencies(CModule module)
    {
        return AnalyzeAssemblyReferences(module);
    }

    /// <summary>
    /// Clears the internal reference resolution cache
    /// </summary>
    public static void ClearCache()
    {
        _resolutionCache.Clear();
        _archValidationCache.Clear();
    }

    /// <summary>
    /// Releases resources used by the analyzer
    /// </summary>
    public static void Cleanup()
    {
        lock (fusionLock)
        {
            if (fusionHandle != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(fusionHandle);
                fusionHandle = IntPtr.Zero;
            }
            fusionInitialized = false;
            createAssemblyEnumDelegate = null;
            createAssemblyNameObjectDelegate = null;
        }
    }
}