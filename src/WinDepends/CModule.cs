/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CMODULE.CS
*
*  VERSION:     1.00
*
*  DATE:        09 Aug 2025
*  
*  Implementation of base CModule and CModuleComparer classes.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization;

namespace WinDepends;

/// <summary>
/// Represents file system attributes of a file.
/// </summary>
[Flags]
public enum FileAttributes : uint
{
    /// <summary>
    /// The file is read-only.
    /// </summary>
    ReadOnly = 0x1,

    /// <summary>
    /// The file is hidden.
    /// </summary>
    Hidden = 0x2,

    /// <summary>
    /// The file is a system file.
    /// </summary>
    System = 0x4,

    /// <summary>
    /// The file is a directory.
    /// </summary>
    Directory = 0x10,

    /// <summary>
    /// The file has been archived.
    /// </summary>
    Archive = 0x20,

    /// <summary>
    /// The file is a device.
    /// </summary>
    Device = 0x40,

    /// <summary>
    /// The file is normal and has no other attributes set.
    /// </summary>
    Normal = 0x80,

    /// <summary>
    /// The file is temporary.
    /// </summary>
    Temporary = 0x100,

    /// <summary>
    /// The file is a sparse file.
    /// </summary>
    SparseFile = 0x200,

    /// <summary>
    /// The file is a reparse point.
    /// </summary>
    ReparsePoint = 0x400,

    /// <summary>
    /// The file is compressed.
    /// </summary>
    Compressed = 0x800,

    /// <summary>
    /// The file is offline. The data of the file is not immediately available.
    /// </summary>
    Offline = 0x1000,

    /// <summary>
    /// The file will not be indexed by the content indexing service.
    /// </summary>
    NotContextIndexed = 0x2000,

    /// <summary>
    /// The file or directory is encrypted.
    /// </summary>
    Encrypted = 0x4000
}

/// <summary>
/// FileAttributes extension to return short name of attributes.
/// </summary>
public static class FileAttributesExtension
{
    public static string ShortName(this FileAttributes fileAttributes)
    {
        Span<char> buffer = stackalloc char[8];
        int count = 0;

        if ((fileAttributes & FileAttributes.Hidden) == FileAttributes.Hidden) buffer[count++] = 'H';
        if ((fileAttributes & FileAttributes.System) == FileAttributes.System) buffer[count++] = 'S';
        if ((fileAttributes & FileAttributes.Archive) == FileAttributes.Archive) buffer[count++] = 'A';
        if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) buffer[count++] = 'R';
        if ((fileAttributes & FileAttributes.Compressed) == FileAttributes.Compressed) buffer[count++] = 'C';
        if ((fileAttributes & FileAttributes.Temporary) == FileAttributes.Temporary) buffer[count++] = 'T';
        if ((fileAttributes & FileAttributes.Offline) == FileAttributes.Offline) buffer[count++] = 'O';
        if ((fileAttributes & FileAttributes.Encrypted) == FileAttributes.Encrypted) buffer[count++] = 'E';

        return new string(buffer[..count]);
    }
}

/// <summary>
/// Represents types of debug directory entries found in PE files.
/// </summary>
[FlagsAttribute]
public enum DebugEntryType : uint
{
    /// <summary>
    /// Unknown debug information format.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// COFF debug information.
    /// </summary>
    Coff = 1,

    /// <summary>
    /// CodeView debug information format.
    /// </summary>
    CodeView = 2,

    /// <summary>
    /// Frame Pointer Omission debug information.
    /// </summary>
    Fpo = 3,

    /// <summary>
    /// Miscellaneous debug information.
    /// </summary>
    Misc = 4,

    /// <summary>
    /// Exception information.
    /// </summary>
    Exception = 5,

    /// <summary>
    /// Fixup information.
    /// </summary>
    Fixup = 6,

    /// <summary>
    /// Source to object map information.
    /// </summary>
    OmapToSrc = 7,

    /// <summary>
    /// Object to source map information.
    /// </summary>
    OmapFromSrc = 8,

    /// <summary>
    /// Borland-specific debug information.
    /// </summary>
    Borland = 9,

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    Reserved10 = 10,

    /// <summary>
    /// CLSID-specific information.
    /// </summary>
    Clsid = 11,

    /// <summary>
    /// Reproducible build information.
    /// </summary>
    Reproducible = 16,

    /// <summary>
    /// Embedded portable PDB debug information.
    /// </summary>
    EmbeddedPortablePdb = 17,

    /// <summary>
    /// PDB checksum information.
    /// </summary>
    PdbChecksum = 19,

    /// <summary>
    /// Extended characteristics information.
    /// </summary>
    ExtendedCharacteristics = 20
}

/// <summary>
/// Represents the visual type of a module for display in the tree view.
/// </summary>
/// <remarks>
/// This enum provides values that correspond to image indices in the application's image list,
/// representing different types and states of modules.
/// </remarks>
public enum ModuleIconType
{
    /// <summary>
    /// Missing module.
    /// </summary>
    MissingModule = 0,

    /// <summary>
    /// Invalid module.
    /// </summary>
    InvalidModule,

    /// <summary>
    /// Normal module with no errors. 
    /// </summary>
    NormalModule,

    /// <summary>
    /// Duplicate module processed somewhere in the tree.
    /// </summary>
    DuplicateModule,

    /// <summary>
    /// Warning for module.
    /// </summary>
    WarningModule,

    /// <summary>
    /// Duplicate module processed with warnings somewhere in the tree.
    /// </summary>
    DuplicateModuleWarning,

    /// <summary>
    /// Normal 64-bit module with no errors.
    /// </summary>
    NormalModule64,

    /// <summary>
    /// Duplicate 64-bit module processed somewhere in the tree.
    /// </summary>
    DuplicateModule64,

    /// <summary>
    /// Warning for module 64-bit.
    /// </summary>
    WarningModule64,

    /// <summary>
    /// Duplicate 64-bit module processed with warnings somewhere in the tree.
    /// </summary>
    DuplicateModule64Warning,

    /// <summary>
    /// Forwarded module is missing.
    /// </summary>
    MissingForwardedModule,

    /// <summary>
    /// Forwarded module is invalid.
    /// </summary>
    InvalidForwardedModule,

    /// <summary>
    /// This is forwarded module.
    /// </summary>
    ForwardedModule,

    /// <summary>
    /// This is forwarded module processed somewhere in the tree.
    /// </summary>
    ForwardedModuleDuplicate,

    /// <summary>
    /// This is forwarded module with warnings.
    /// </summary>
    ForwardedModuleWarning,

    /// <summary>
    /// This is duplicate forwarded module with warnings.
    /// </summary>
    ForwardedModuleDuplicateWarning,

    /// <summary>
    /// This is 64-bit forwarded module.
    /// </summary>
    ForwardedModule64,

    /// <summary>
    /// This is 64-bit forwarded module processed somewhere in the tree.
    /// </summary>
    ForwardedModule64Duplicate,

    /// <summary>
    /// This is 64-bit forwarded module with warnings.
    /// </summary>
    ForwardedModule64Warning,

    /// <summary>
    /// This is 64-bit duplicate forwarded module with warnings.
    /// </summary>
    ForwardedModule64DuplicateWarning,

    /// <summary>
    /// Delay-load module is missing.
    /// </summary>
    MissingDelayLoadModule,

    /// <summary>
    /// Delay-load module is invalid.
    /// </summary>
    InvalidDelayLoadModule,

    /// <summary>
    /// This is a delay-load module.
    /// </summary>
    DelayLoadModule,

    /// <summary>
    /// Delay-load module processed somewhere in the tree.
    /// </summary>
    DelayLoadModuleDuplicate,

    /// <summary>
    /// Delay-load module with warnings.
    /// </summary>
    DelayLoadModuleWarning,

    /// <summary>
    /// Delay-load module processed somewhere in the tree with warnings.
    /// </summary>
    DelayLoadModuleDuplicateWarning,

    /// <summary>
    /// This is delay-load module 64-bit.
    /// </summary>
    DelayLoadModule64,

    /// <summary>
    /// Delay-load 64-bit module processed somewhere in the tree.
    /// </summary>
    DelayLoadModule64Duplicate,

    /// <summary>
    /// Delay-load 64-bit module with warnings.
    /// </summary>
    DelayLoadModule64Warning,

    /// <summary>
    /// Delay-load 64-bit module processed somewhere in the tree with warnings.
    /// </summary>
    DelayLoadModule64DuplicateWarning,

    /// <summary>
    /// Dynamic module that is missing.
    /// </summary>
    MissingDynamicModule,

    /// <summary>
    /// Dynamic module that is invalid.
    /// </summary>
    InvalidDynamicModule,

    /// <summary>
    /// Dynamic module.
    /// </summary>
    NormalDynamicModule,

    /// <summary>
    /// Duplicate module processed somewhere in the tree.
    /// </summary>
    DuplicateDynamicModule,

    /// <summary>
    /// Dynamic module with warnings.
    /// </summary>
    WarningDynamicModule,

    /// <summary>
    /// Duplicate dynamic module with warnings.
    /// </summary>
    DuplicateDynamicModuleWarning,

    /// <summary>
    /// Dynamic module 64-bit.
    /// </summary>
    NormalDynamicModule64,

    /// <summary>
    /// Duplicate 64-bit module processed somewhere in the tree.
    /// </summary>
    DuplicateDynamicModule64,

    /// <summary>
    /// Dynamic 64-bit module with warnings.
    /// </summary>
    WarningDynamicModule64,

    /// <summary>
    /// Duplicate dynamic 64-bit module with warnings.
    /// </summary>
    DuplicateDynamicModule64Warning,

    /// <summary>
    /// Dynamic module mapped as image or datafile.
    /// </summary>
    DynamicMappedModuleNoExec,

    /// <summary>
    /// Dynamic 64-bit module mapped as image or datafile.
    /// </summary>
    DynamicMappedModule64NoExec,

    /// <summary>
    /// Dynamic module mapped as image or datafile with warnings.
    /// </summary>
    DynamicMappedModuleNoExecWarning,
    /// <summary>
    /// Dynamic 64-bit module mapped as image or datafile with warnings.
    /// </summary>
    DynamicMappedModule64NoExecWarning,

    /// <summary>
    /// .NET module with no errors.
    /// </summary>
    NormalDotNetModule,

    /// <summary>
    /// Duplicate .NET module processed somewhere in the tree.
    /// </summary>
    DuplicateDotNetModule,

    /// <summary>
    /// .NET module with warnings.
    /// </summary>
    WarningDotNetModule,

    /// <summary>
    /// Duplicate .NET module with warnings.
    /// </summary>
    DuplicateDotNetModuleWarning,

    /// <summary>
    /// 64-bit .NET module with no errors.
    /// </summary>
    NormalDotNetModule64,

    /// <summary>
    /// Duplicate 64-bit .NET module processed somewhere in the tree.
    /// </summary>
    DuplicateDotNetModule64,

    /// <summary>
    /// 64-bit .NET module with warnings.
    /// </summary>
    WarningDotNetModule64,

    /// <summary>
    /// Duplicate 64-bit .NET module with warnings.
    /// </summary>
    DuplicateDotNetModuleWarning64
}

/// <summary>
/// Represents the visual type of a module for display in list view (compact mode).
/// </summary>
/// <remarks>
/// This enum provides values that correspond to image indices in the application's compact image list,
/// representing different types and states of modules in a more consolidated form than <see cref="ModuleIconType"/>.
/// </remarks>
public enum ModuleIconCompactType
{
    /// <summary>
    /// Missing module.
    /// </summary>
    MissingModule = 0,

    /// <summary>
    /// Missing delay-load module.
    /// </summary>
    DelayLoadMissing,

    /// <summary>
    /// Missing dynamic module.
    /// </summary>
    DynamicMissing,

    /// <summary>
    /// Invalid module.
    /// </summary>
    Invalid,

    /// <summary>
    /// Invalid delay-load module.
    /// </summary>
    DelayLoadInvalid,

    /// <summary>
    /// Invalid dynamic module.
    /// </summary>
    DynamicInvalid,

    /// <summary>
    /// Warning for module.
    /// </summary>
    WarningModule,

    /// <summary>
    /// Warning for module 64-bit.
    /// </summary>
    WarningModule64,

    /// <summary>
    /// Warning for delay-load module.
    /// </summary>
    DelayLoadModuleWarning,

    /// <summary>
    /// Warning for delay-load module 64-bit.
    /// </summary>
    DelayLoadModule64Warning,

    /// <summary>
    /// Warning for dynamic load module.
    /// </summary>
    DynamicModuleWarning,

    /// <summary>
    /// Warning for dynamic load module 64-bit.
    /// </summary>
    DynamicModule64Warning,

    /// <summary>
    /// Warning for module loaded with 
    /// DONT_RESOLVE_DLL_REFERENCES and/or the LOAD_LIBRARY_AS_DATAFILE flag.
    /// </summary>
    WarningMappedNoExecImage,

    /// <summary>
    /// Warning for module 64-bit loaded with 
    /// DONT_RESOLVE_DLL_REFERENCES and/or the LOAD_LIBRARY_AS_DATAFILE flag.
    /// </summary>
    WarningMappedNoExecImage64,

    /// <summary>
    /// Normal module with no errors. 
    /// </summary>
    NormalModule,

    /// <summary>
    /// Normal 64-bit module with no errors.
    /// </summary>
    NormalModule64,

    /// <summary>
    /// Delay-load module.
    /// </summary>
    DelayLoadModule,

    /// <summary>
    /// Delay-load module 64-bit.
    /// </summary>
    DelayLoadModule64,

    /// <summary>
    /// Dynamic module.
    /// </summary>
    DynamicModule,

    /// <summary>
    /// Dynamic module 64-bit.
    /// </summary>
    DynamicModule64,

    /// <summary>
    /// The module mapped as image or datafile.
    /// </summary>
    MappedModuleNoExec,

    /// <summary>
    /// The 64-bit module mapped as image or datafile.
    /// </summary>
    MappedModule64NoExec,

    /// <summary>
    /// Normal .NET module with no errors.
    /// </summary>
    NormalDotNetModule,

    /// <summary>
    /// Normal 64-bit .NET module with no errors.
    /// </summary>
    NormalDotNetModule64,

    /// <summary>
    /// .NET module with warnings.
    /// </summary>
    WarningDotNetModule,

    /// <summary>
    /// 64-bit .NET module with warnings.
    /// </summary>
    WarningDotNetModule64
}

/// <summary>
/// Contains data about a module's PE file structure and metadata.
/// </summary>
/// <remarks>
/// This class stores detailed information about a module's PE file structure,
/// including headers, characteristics, checksums, version information, and exports.
/// </remarks>
[DataContract]
public class CModuleData
{
    [DataMember]
    public DateTime FileTimeStamp { get; set; }
    [DataMember]
    public UInt32 LinkTimeStamp { get; set; }
    [DataMember]
    public UInt64 FileSize { get; set; }
    [DataMember]
    public FileAttributes Attributes { get; set; } = FileAttributes.Normal;
    [DataMember]
    public uint LinkChecksum { get; set; }
    [DataMember]
    public uint RealChecksum { get; set; }
    [DataMember]
    public ushort Machine { get; set; } = (ushort)System.Reflection.PortableExecutable.Machine.Amd64;
    [DataMember]
    public ushort Characteristics { get; set; } = (ushort)System.Reflection.PortableExecutable.Characteristics.ExecutableImage;
    [DataMember]
    public ushort DllCharacteristics { get; set; }
    [DataMember]
    public uint ExtendedCharacteristics { get; set; }
    [DataMember]
    public ushort Subsystem { get; set; } = (ushort)System.Reflection.PortableExecutable.Subsystem.WindowsCui;
    [DataMember]
    public UInt64 PreferredBase { get; set; }
    [DataMember]
    public uint VirtualSize { get; set; }
    [DataMember]
    public string FileVersion { get; set; }
    [DataMember]
    public string ProductVersion { get; set; }
    [DataMember]
    public string ImageVersion { get; set; }
    [DataMember]
    public string LinkerVersion { get; set; }
    [DataMember]
    public string OSVersion { get; set; }
    [DataMember]
    public string SubsystemVersion { get; set; }
    [DataMember]
    public uint ImageFixed { get; set; } = 0;
    [DataMember]

    //
    // .NET specific properties
    //

    public uint ImageDotNet { get; set; } = 0;
    [DataMember]
    public uint CorFlags { get; set; } = 0;
    [DataMember]
    public string RuntimeVersion { get; set; }
    [DataMember]
    public string FrameworkKind { get; set; }
    [DataMember]
    public bool IsSystemAssembly { get; set; }
    [DataMember]
    public string ResolutionSource { get; set; }
    [DataMember]
    public string ReferenceVersion { get; set; }
    [DataMember]
    public string ReferencePublicKeyToken { get; set; }
    [DataMember]
    public string ReferenceCulture { get; set; }

    /// <summary>
    /// Gets or sets the list of debug directory entry types in the module.
    /// </summary>
    /// <value>A list of debug directory types found in the module.</value>
    [DataMember]
    public List<uint> DebugDirTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of exported functions from this module.
    /// </summary>
    /// <value>A list of <see cref="CFunction"/> objects representing the module's exports.</value>
    [DataMember]
    public List<CFunction> Exports { get; set; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CModuleData"/> class.
    /// </summary>
    public CModuleData()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CModuleData"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The <see cref="CModuleData"/> object to copy from.</param>
    public CModuleData(CModuleData other)
    {
        FileTimeStamp = other.FileTimeStamp;
        LinkTimeStamp = other.LinkTimeStamp;
        FileSize = other.FileSize;
        Attributes = other.Attributes;
        LinkChecksum = other.LinkChecksum;
        RealChecksum = other.RealChecksum;
        ImageFixed = other.ImageFixed;
        ImageDotNet = other.ImageDotNet;
        CorFlags = other.CorFlags;
        Machine = other.Machine;
        Characteristics = other.Characteristics;
        DllCharacteristics = other.DllCharacteristics;
        ExtendedCharacteristics = other.ExtendedCharacteristics;
        Subsystem = other.Subsystem;
        PreferredBase = other.PreferredBase;
        VirtualSize = other.VirtualSize;
        FileVersion = other.FileVersion;
        ProductVersion = other.ProductVersion;
        ImageVersion = other.ImageVersion;
        LinkerVersion = other.LinkerVersion;
        OSVersion = other.OSVersion;
        SubsystemVersion = other.SubsystemVersion;

        // .NET specific properties
        RuntimeVersion = other.RuntimeVersion;
        FrameworkKind = other.FrameworkKind;
        IsSystemAssembly = other.IsSystemAssembly;
        ResolutionSource = other.ResolutionSource;
        ReferenceVersion = other.ReferenceVersion;
        ReferencePublicKeyToken = other.ReferencePublicKeyToken;
        ReferenceCulture = other.ReferenceCulture;

        DebugDirTypes = other.DebugDirTypes != null ?
            new List<uint>(other.DebugDirTypes) :
            new List<uint>();
    }
}

/// <summary>
/// Represents a module (PE format binary) in the dependency tree.
/// </summary>
/// <remarks>
/// This class encapsulates a module's metadata, status, and relationships with other modules.
/// It provides methods to analyze module properties, determine icon types for display,
/// and manage exports and imports.
/// </remarks>
[DataContract]
public class CModule
{
    /// <summary>
    /// Gets or sets the unique instance identifier for this module in the dependency tree.
    /// </summary>
    /// <value>
    /// A unique identifier for this specific instance of the module.
    /// </value>
    /// <remarks>
    /// The instance ID allows distinguishing between multiple instances of the same
    /// module that may appear at different places in the dependency tree.
    /// </remarks>
    [DataMember]
    public int InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the original instance ID if this module is a duplicate.
    /// </summary>
    /// <value>
    /// The instance ID of the original module, or 0 if this is the original instance.
    /// </value>
    /// <remarks>
    /// When a module appears multiple times in the dependency tree, only one instance
    /// is fully processed, and duplicates reference that instance through this property.
    /// </remarks>
    [DataMember]
    public int OriginalInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the image index for displaying this module in lists and trees.
    /// </summary>
    /// <value>
    /// The index into the application's image list for module icons.
    /// </value>
    [DataMember]
    public int ModuleImageIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this module has been processed.
    /// </summary>
    /// <value>
    /// <c>true</c> if this module has been fully analyzed; otherwise, <c>false</c>.
    /// </value>
    [DataMember]
    public bool IsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the depth of this module in the dependency tree.
    /// </summary>
    /// <value>
    /// The level of this module in the tree, with 0 being the root module.
    /// </value>
    [DataMember]
    public int Depth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this module is a forwarded module.
    /// </summary>
    /// <value>
    /// <c>true</c> if this module is referenced via a forwarded export; otherwise, <c>false</c>.
    /// </value>
    [DataMember]
    public bool IsForward { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this module is delay-loaded.
    /// </summary>
    /// <value>
    /// <c>true</c> if this module is loaded when needed rather than at process startup; otherwise, <c>false</c>.
    /// </value>
    [DataMember]
    public bool IsDelayLoad { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module file could not be found.
    /// </summary>
    /// <value>
    /// <c>true</c> if the module file was not found on disk; otherwise, <c>false</c>.
    /// </value>
    [DataMember]
    public bool FileNotFound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module is invalid.
    /// </summary>
    /// <value>
    /// <c>true</c> if the module is corrupted or not a valid PE file; otherwise, <c>false</c>.
    /// </value>
    [DataMember]
    public bool IsInvalid { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this module is from a reproducible build.
    /// </summary>
    /// <value>
    /// <c>true</c> if the module was built with reproducible build settings; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Reproducible builds produce identical binary output given the same source input,
    /// making verification and security auditing easier.
    /// </remarks>
    [DataMember]
    public bool IsReproducibleBuild { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this module is an API set contract.
    /// </summary>
    /// <value>
    /// <c>true</c> if this module is an API set contract (e.g., api-ms-win-*); otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// API set contracts are a Windows feature that allows redirecting API implementations
    /// to different host modules.
    /// </remarks>
    [DataMember]
    public bool IsApiSetContract { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this module is a Windows kernel module.
    /// </summary>
    /// <value>
    /// <c>true</c> if this module is part of the Windows kernel; otherwise, <c>false</c>.
    /// </value>
    [DataMember]
    public bool IsKernelModule { get; set; }

    [DataMember]
    public bool IsDotNetModule { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether the module has export errors.
    /// </summary>
    /// <value>
    /// <c>true</c> if errors were found in the export table; otherwise, <c>false</c>.
    /// </value>
    [DataMember]
    public bool ExportContainErrors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module has other errors.
    /// </summary>
    /// <value>
    /// <c>true</c> if the module has errors not specifically categorized; otherwise, <c>false</c>.
    /// </value>
    [DataMember]
    public bool OtherErrorsPresent { get; set; }

    /// <summary>
    /// Gets or sets how the module file name was resolved.
    /// </summary>
    /// <value>
    /// The search order type used to resolve this module's path.
    /// </value>
    [DataMember]
    public SearchOrderType FileNameResolvedBy { get; set; }

    /// <summary>
    /// Gets or sets the full path and filename of the module.
    /// </summary>
    /// <value>
    /// The resolved path to the module file.
    /// </value>
    [DataMember]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw (unresolved) filename of the module.
    /// </summary>
    /// <value>
    /// The original name used to reference this module before path resolution.
    /// </value>
    [DataMember]
    public string RawFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module data containing detailed PE information.
    /// </summary>
    /// <value>
    /// The <see cref="CModuleData"/> object containing the module's PE file information.
    /// </value>
    [DataMember]
    public CModuleData ModuleData { get; set; }

    /// <summary>
    /// Gets or sets the module's manifest data as a Base64-encoded string.
    /// </summary>
    /// <value>
    /// The Base64-encoded XML manifest from the module's resources, or empty if no manifest exists.
    /// </value>
    [DataMember]
    public string ManifestData { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of parent imports referencing functions from this module.
    /// </summary>
    /// <value>
    /// A list of <see cref="CFunction"/> objects representing imports from parent modules.
    /// </value>
    [DataMember]
    public List<CFunction> ParentImports { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of modules that depend on this module.
    /// </summary>
    /// <value>
    /// A list of <see cref="CModule"/> objects that directly import from this module.
    /// </value>
    [DataMember]
    public List<CModule> Dependents { get; set; } = [];

    [DataMember]
    public List<CForwarderEntry> ForwarderEntries { get; set; } = new List<CForwarderEntry>();

    [DataMember]
    public bool ForwardersExpanded { get; set; }

    /// <summary>
    /// Gets or sets the cached filename (without path) for display purposes.
    /// </summary>
    /// <remarks>
    /// This field is used to cache the result of Path.GetFileName() for performance
    /// when sorting and displaying modules.
    /// </remarks>
    internal string _cachedFileName;

    /// <summary>
    /// Resets cached data in the module.
    /// </summary>
    /// <remarks>
    /// Call this method when the module's filename or other key data changes to ensure
    /// that cached values are regenerated when next accessed.
    /// </remarks>
    public void ResetCache()
    {
        _cachedFileName = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CModule"/> class.
    /// </summary>
    public CModule()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CModule"/> class with a specified filename.
    /// </summary>
    /// <param name="moduleFileName">The name or path of the module file.</param>
    public CModule(string moduleFileName)
    {
        RawFileName = moduleFileName;
        FileName = moduleFileName;
        ModuleData = new()
        {
            FileTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CModule"/> class with detailed properties.
    /// </summary>
    /// <param name="moduleFileName">The resolved path of the module file.</param>
    /// <param name="rawModuleFileName">The original unresolved name of the module file.</param>
    /// <param name="fileNameResolvedBy">The method used to resolve the module path.</param>
    /// <param name="isApiSetContract">Whether the module is an API set contract.</param>
    public CModule(string moduleFileName, string rawModuleFileName, SearchOrderType fileNameResolvedBy, bool isApiSetContract)
    {
        RawFileName = rawModuleFileName;
        FileName = moduleFileName;
        FileNameResolvedBy = fileNameResolvedBy;
        IsApiSetContract = isApiSetContract;
        ModuleData = new()
        {
            FileTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
    }

    /// <summary>
    /// Determines if the module targets a 64-bit architecture.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the module targets a 64-bit architecture; otherwise, <c>false</c>.
    /// </returns>
    public bool Is64bitArchitecture() =>
        ModuleData.Machine == (uint)Machine.Amd64 ||
        ModuleData.Machine == (uint)Machine.IA64 ||
        ModuleData.Machine == (uint)Machine.Arm64 ||
        ModuleData.Machine == (uint)Machine.LoongArch64;

    /// <summary>
    /// Gets the appropriate icon index for displaying this module in the tree view.
    /// </summary>
    /// <returns>
    /// An integer representing the index in the image list for the module's icon.
    /// </returns>
    /// <remarks>
    /// The icon is selected based on the module's architecture, status flags,
    /// and whether it's a delay-load or forwarded module.
    /// </remarks>
    public int GetIconIndexForModule()
    {
        bool is64bit = Is64bitArchitecture();
        bool bDuplicate = OriginalInstanceId != 0;
        bool bDuplicateAndExportError = bDuplicate && ExportContainErrors;

        if (IsDotNetModule)
        {
            if (IsInvalid)
            {
                return (int)ModuleIconType.InvalidModule;
            }

            if (FileNotFound)
            {
                return (int)ModuleIconType.MissingModule;
            }

            if (bDuplicateAndExportError)
            {
                return is64bit ? (int)ModuleIconType.DuplicateDotNetModuleWarning64 : (int)ModuleIconType.DuplicateDotNetModuleWarning;
            }
            if (bDuplicate)
            {
                return is64bit ? (int)ModuleIconType.DuplicateDotNetModule64 : (int)ModuleIconType.DuplicateDotNetModule;
            }

            return is64bit ? (ExportContainErrors ? (int)ModuleIconType.WarningDotNetModule64 : (int)ModuleIconType.NormalDotNetModule64) :
                (ExportContainErrors ? (int)ModuleIconType.WarningDotNetModule : (int)ModuleIconType.NormalDotNetModule);
        }

        if (IsDelayLoad)
        {
            if (IsInvalid)
            {
                return (int)ModuleIconType.InvalidDelayLoadModule;
            }

            if (FileNotFound)
            {
                return (int)ModuleIconType.MissingDelayLoadModule;
            }

            if (bDuplicateAndExportError)
            {
                return is64bit ? (int)ModuleIconType.DelayLoadModule64DuplicateWarning : (int)ModuleIconType.DelayLoadModuleDuplicateWarning;
            }

            if (bDuplicate)
            {
                return is64bit ? (int)ModuleIconType.DelayLoadModule64Duplicate : (int)ModuleIconType.DelayLoadModuleDuplicate;
            }

            if (ExportContainErrors || OtherErrorsPresent)
            {
                return is64bit ? (int)ModuleIconType.DelayLoadModule64Warning : (int)ModuleIconType.DelayLoadModuleWarning;
            }

            return is64bit ? (int)ModuleIconType.DelayLoadModule64 : (int)ModuleIconType.DelayLoadModule;
        }

        if (IsForward)
        {
            if (IsInvalid)
            {
                return (int)ModuleIconType.InvalidForwardedModule;
            }

            if (FileNotFound)
            {
                return (int)ModuleIconType.MissingForwardedModule;
            }

            if (bDuplicateAndExportError)
            {
                return is64bit ? (int)ModuleIconType.DuplicateModule64Warning : (int)ModuleIconType.DuplicateModuleWarning;
            }

            if (bDuplicate)
            {
                return is64bit ? (int)ModuleIconType.ForwardedModule64Duplicate : (int)ModuleIconType.ForwardedModuleDuplicate;
            }

            if (ExportContainErrors || OtherErrorsPresent)
            {
                return is64bit ? (int)ModuleIconType.ForwardedModule64Warning : (int)ModuleIconType.ForwardedModuleWarning;
            }

            return is64bit ? (int)ModuleIconType.ForwardedModule64 : (int)ModuleIconType.ForwardedModule;
        }

        if (IsInvalid)
        {
            return (int)ModuleIconType.InvalidModule;
        }

        if (FileNotFound)
        {
            return (int)ModuleIconType.MissingModule;
        }

        if (bDuplicate)
        {
            return is64bit ? (int)ModuleIconType.DuplicateModule64 : (int)ModuleIconType.DuplicateModule;
        }

        if (OtherErrorsPresent)
        {
            return is64bit ? (int)ModuleIconType.WarningModule64 : (int)ModuleIconType.WarningModule;
        }

        return is64bit ? (ExportContainErrors ? (int)ModuleIconType.WarningModule64 : (int)ModuleIconType.NormalModule64) :
            (ExportContainErrors ? (int)ModuleIconType.WarningModule : (int)ModuleIconType.NormalModule);
    }

    /// <summary>
    /// Gets the appropriate icon index for displaying this module in list view (compact mode).
    /// </summary>
    /// <returns>
    /// An integer representing the index in the compact image list for the module's icon.
    /// </returns>
    /// <remarks>
    /// The compact icon is a simplified representation compared to the tree view icon
    /// but still conveys the module's architecture and key status attributes.
    /// </remarks>
    public int GetIconIndexForModuleCompact()
    {
        bool is64bit = Is64bitArchitecture();

        if (IsDotNetModule)
        {
            if (IsInvalid)
            {
                return (int)ModuleIconCompactType.Invalid;
            }

            if (FileNotFound)
            {
                return (int)ModuleIconCompactType.MissingModule;
            }

            if (OtherErrorsPresent)
            {
                return is64bit ? (int)ModuleIconCompactType.WarningDotNetModule64 : (int)ModuleIconCompactType.WarningDotNetModule;
            }

            return is64bit ? (ExportContainErrors ? (int)ModuleIconCompactType.WarningDotNetModule64 : (int)ModuleIconCompactType.NormalDotNetModule64) :
                (ExportContainErrors ? (int)ModuleIconCompactType.WarningDotNetModule : (int)ModuleIconCompactType.NormalDotNetModule);
        }

        if (IsDelayLoad)
        {
            if (IsInvalid)
            {
                return (int)ModuleIconCompactType.DelayLoadInvalid;
            }

            if (FileNotFound)
            {
                return (int)ModuleIconCompactType.DelayLoadMissing;
            }

            if (ExportContainErrors)
            {
                return (int)ModuleIconCompactType.DelayLoadModuleWarning;
            }

            if (OtherErrorsPresent)
            {
                return is64bit ? (int)ModuleIconCompactType.DelayLoadModule64Warning : (int)ModuleIconCompactType.DelayLoadModuleWarning;
            }

            return is64bit ? (int)ModuleIconCompactType.DelayLoadModule64 : (int)ModuleIconCompactType.DelayLoadModule;
        }

        if (IsInvalid)
        {
            return (int)ModuleIconCompactType.Invalid;
        }

        if (FileNotFound)
        {
            return (int)ModuleIconCompactType.MissingModule;
        }

        if (OtherErrorsPresent)
        {
            return is64bit ? (int)ModuleIconCompactType.WarningModule64 : (int)ModuleIconCompactType.WarningModule;
        }

        return is64bit ? (ExportContainErrors ? (int)ModuleIconCompactType.WarningModule64 : (int)ModuleIconCompactType.NormalModule64) :
            (ExportContainErrors ? (int)ModuleIconCompactType.WarningModule : (int)ModuleIconCompactType.NormalModule);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code derived from the module's filename (case-insensitive).
    /// </returns>
    public override int GetHashCode()
    {
        return FileName?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current module.
    /// </summary>
    /// <param name="obj">The object to compare with the current module.</param>
    /// <returns>
    /// <c>true</c> if the specified object is a module with the same filename (case-insensitive);
    /// otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals(object obj)
    {
        if (obj is not CModule other)
            return false;

        return string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the module name, with special handling for API set contracts.
    /// </summary>
    /// <param name="needResolve">
    /// Whether to return the resolved name (FileName) or raw name (RawFileName) for API set contracts.
    /// </param>
    /// <returns>
    /// The appropriate module name based on the parameter and whether the module is an API set contract.
    /// </returns>
    public string GetModuleNameRespectApiSet(bool needResolve)
    {
        if (IsApiSetContract)
            return needResolve ? FileName ?? string.Empty : RawFileName ?? string.Empty;
        return FileName ?? string.Empty;
    }

    /// <summary>
    /// Gets the module's manifest data as a byte array.
    /// </summary>
    /// <returns>
    /// A byte array containing the manifest data, or <c>null</c> if no manifest exists or decoding fails.
    /// </returns>
    public byte[] GetManifestDataAsArray()
    {
        try
        {
            if (!string.IsNullOrEmpty(ManifestData))
            {
                return Convert.FromBase64String(ManifestData);
            }
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentNullException)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Sets the module's manifest data from a Base64-encoded string.
    /// </summary>
    /// <param name="data">The Base64-encoded manifest data.</param>
    public void SetManifestData(string data)
    {
        ManifestData = data;
    }

    /// <summary>
    /// Resolves a function by ordinal in the module's export table.
    /// </summary>
    /// <param name="ordinal">The ordinal value to search for.</param>
    /// <returns>
    /// The <see cref="CFunction"/> object with the specified ordinal, or <c>null</c> if not found.
    /// </returns>
    /// <remarks>
    /// This method only returns functions that have a name (not exported by ordinal only).
    /// </remarks>
    public CFunction ResolveFunctionForOrdinal(uint ordinal)
    {
        foreach (var function in ModuleData.Exports)
        {
            if (function.Ordinal == ordinal && !function.SnapByOrdinal())
            {
                return function;
            }
        }

        return null;
    }

}

/// <summary>
/// Compares <see cref="CModule"/> objects for sorting in list views based on different field types.
/// </summary>
/// <remarks>
/// This class implements comparison of module objects based on the selected column index.
/// It properly handles column-specific comparison logic and supports both ascending and descending sort orders.
/// 
/// For filename comparisons, it uses string comparison with optional full path display.
/// For numeric fields, it uses direct value comparison without string conversion for better performance.
/// </remarks>
public class CModuleComparer : IComparer<CModule>
{
    private readonly int _fieldIndex;
    private readonly SortOrder _sortOrder;
    private readonly bool _fullPaths;
    private readonly StringComparer _ignoreCase;

    /// <summary>
    /// Initializes a new instance of the <see cref="CModuleComparer"/> class.
    /// </summary>
    /// <param name="sortOrder">The sort direction to apply to comparisons.</param>
    /// <param name="fieldIndex">The index of the field to compare.</param>
    /// <param name="fullPaths">Whether to display and compare full paths for filenames.</param>
    /// <remarks>
    /// This constructor configures the comparer to sort modules based on the specified field
    /// and in the specified direction.
    /// </remarks>
    public CModuleComparer(SortOrder sortOrder, int fieldIndex, bool fullPaths)
    {
        _fieldIndex = fieldIndex;
        _sortOrder = sortOrder;
        _fullPaths = fullPaths;
        _ignoreCase = StringComparer.OrdinalIgnoreCase;
    }

    public int Compare(CModule x, CModule y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return _sortOrder == SortOrder.Ascending ? -1 : 1;
        if (y == null) return _sortOrder == SortOrder.Ascending ? 1 : -1;

        int comparisonResult;

        switch (_fieldIndex)
        {
            case (int)ModuleColumns.Name when !_fullPaths:
                {
                    x._cachedFileName ??= Path.GetFileName(x.FileName ?? string.Empty);
                    y._cachedFileName ??= Path.GetFileName(y.FileName ?? string.Empty);
                    comparisonResult = _ignoreCase.Compare(x._cachedFileName, y._cachedFileName);
                    break;
                }

            case (int)ModuleColumns.Name:
                comparisonResult = _ignoreCase.Compare(x.FileName, y.FileName);
                break;

            case (int)ModuleColumns.Image:
                comparisonResult = x.ModuleImageIndex.CompareTo(y.ModuleImageIndex);
                break;

            case (int)ModuleColumns.LinkChecksum:
                comparisonResult = x.ModuleData.LinkChecksum.CompareTo(y.ModuleData.LinkChecksum);
                break;

            case (int)ModuleColumns.RealChecksum:
                comparisonResult = x.ModuleData.RealChecksum.CompareTo(y.ModuleData.RealChecksum);
                break;

            case (int)ModuleColumns.VirtualSize:
                comparisonResult = x.ModuleData.VirtualSize.CompareTo(y.ModuleData.VirtualSize);
                break;

            case (int)ModuleColumns.PrefferedBase:
                comparisonResult = x.ModuleData.PreferredBase.CompareTo(y.ModuleData.PreferredBase);
                break;

            case (int)ModuleColumns.LinkTimeStamp:
                comparisonResult = x.ModuleData.LinkTimeStamp.CompareTo(y.ModuleData.LinkTimeStamp);
                break;

            case (int)ModuleColumns.FileTimeStamp:
                comparisonResult = x.ModuleData.FileTimeStamp.CompareTo(y.ModuleData.FileTimeStamp);
                break;

            case (int)ModuleColumns.FileSize:
                comparisonResult = x.ModuleData.FileSize.CompareTo(y.ModuleData.FileSize);
                break;

            case (int)ModuleColumns.CPU:
                comparisonResult = x.ModuleData.Machine.CompareTo(y.ModuleData.Machine);
                break;

            default:
                comparisonResult = _ignoreCase.Compare(x.FileName, y.FileName);
                break;
        }

        return _sortOrder == SortOrder.Descending ? -comparisonResult : comparisonResult;
    }
}
public class CForwarderEntry
{
    public string TargetModuleName;      // Raw module token from forward string (before resolution)
    public string TargetFunctionName;    // Empty if by ordinal
    public uint TargetOrdinal;           // UInt32.MaxValue if by name

    public override int GetHashCode()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (TargetModuleName?.ToLowerInvariant().GetHashCode() ?? 0);
            h = h * 31 + (TargetFunctionName?.GetHashCode() ?? 0);
            h = h * 31 + (int)TargetOrdinal;
            return h;
        }
    }
    public override bool Equals(object o)
    {
        if (o is not CForwarderEntry e) return false;
        return string.Equals(TargetModuleName, e.TargetModuleName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(TargetFunctionName, e.TargetFunctionName, StringComparison.Ordinal) &&
               TargetOrdinal == e.TargetOrdinal;
    }
}