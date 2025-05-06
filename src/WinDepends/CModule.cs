/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CMODULE.CS
*
*  VERSION:     1.00
*
*  DATE:        05 May 2025
*  
*  Implementation of base CModule class.
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

[Flags]
public enum ModuleInfoFlags : uint
{
    Normal = 0x100,
    Duplicate = 0x200,
    ExportError = 0x400,
    DuplicateExportError = 0x800,
    FileNotFound = 0x1000,
    Invalid = 0x2000,
    WarningOtherErrors = 0x4000
}

[Flags]
public enum FileAttributes : uint
{
    ReadOnly = 0x1,
    Hidden = 0x2,
    System = 0x4,

    Directory = 0x10,
    Archive = 0x20,
    Device = 0x40,
    Normal = 0x80,

    Temporary = 0x100,
    SparseFile = 0x200,
    ReparsePoint = 0x400,
    Compressed = 0x800,

    Offline = 0x1000,
    NotContextIndexed = 0x2000,
    Encrypted = 0x4000
}

/// <summary>
/// FileAttributes extension to return short name of attributes.
/// </summary>
public static class FileAttributesExtension
{
    public static string ShortName(this FileAttributes fileAttributes) =>
        $"{(fileAttributes.HasFlag(FileAttributes.Hidden) ? "H" : "")}" +
        $"{(fileAttributes.HasFlag(FileAttributes.System) ? "S" : "")}" +
        $"{(fileAttributes.HasFlag(FileAttributes.Archive) ? "A" : "")}" +
        $"{(fileAttributes.HasFlag(FileAttributes.ReadOnly) ? "R" : "")}" +
        $"{(fileAttributes.HasFlag(FileAttributes.Compressed) ? "C" : "")}" +
        $"{(fileAttributes.HasFlag(FileAttributes.Temporary) ? "T" : "")}" +
        $"{(fileAttributes.HasFlag(FileAttributes.Offline) ? "O" : "")}" +
        $"{(fileAttributes.HasFlag(FileAttributes.Encrypted) ? "E" : "")}";
}

[FlagsAttribute]
public enum DebugEntryType : uint
{
    Unknown = 0,
    Coff = 1,
    CodeView = 2,
    Fpo = 3,
    Misc = 4,
    Exception = 5,
    Fixup = 6,
    OmapToSrc = 7,
    OmapFromSrc = 8,
    Borland = 9,
    Reserved10 = 10,
    Clsid = 11,
    Reproducible = 16,
    EmbeddedPortablePdb = 17,
    PdbChecksum = 19,
    ExtendedCharacteristics = 20
}

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
    DynamicMappedModule64NoExecWarning
}

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
    MappedModule64NoExec
}

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
    //[DataMember]
    // public UInt64 ActualBase { get; set; } //unused, profiling artifact
    [DataMember]
    public uint VirtualSize { get; set; }
    //[DataMember]
    // public uint LoadOrder { get; set; } //unused, profiling artifact
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
    //
    // Module debug directories.
    //
    [DataMember]
    public List<uint> DebugDirTypes { get; set; } = [];

    //
    // Module exports.
    //
    [DataMember]
    public List<CFunction> Exports { get; set; } = [];

    public CModuleData()
    {
    }

    public CModuleData(CModuleData other)
    {
        FileTimeStamp = other.FileTimeStamp;
        LinkTimeStamp = other.LinkTimeStamp;
        FileSize = other.FileSize;
        Attributes = other.Attributes;
        LinkChecksum = other.LinkChecksum;
        RealChecksum = other.RealChecksum;
        ImageFixed = other.ImageFixed;
        Machine = other.Machine;
        Characteristics = other.Characteristics;
        DllCharacteristics = other.DllCharacteristics;
        ExtendedCharacteristics = other.ExtendedCharacteristics;
        Subsystem = other.Subsystem;
        PreferredBase = other.PreferredBase;
        // ActualBase = other.ActualBase; //unused, profiling artifact
        VirtualSize = other.VirtualSize;
        // LoadOrder = other.LoadOrder; //unused, profiling artifact
        FileVersion = other.FileVersion;
        ProductVersion = other.ProductVersion;
        ImageVersion = other.ImageVersion;
        LinkerVersion = other.LinkerVersion;
        OSVersion = other.OSVersion;
        SubsystemVersion = other.SubsystemVersion;
        DebugDirTypes = new List<uint>(other.DebugDirTypes);
    }
}

[DataContract]
public class CModule
{
    [DataMember]
    public Guid ModuleGuid { get; set; }

    //
    //  Module instance id, representing module, generated as GetHashCode()
    //
    [DataMember]
    public int InstanceId { get; set; }
    //
    // Original instance of module, if we are duplicate.
    //
    [DataMember]
    public int OriginalInstanceId { get; set; }
    [DataMember]
    public int ModuleImageIndex { get; set; }
    [DataMember]
    public bool IsProcessed { get; set; }
    [DataMember]
    public int Depth { get; set; }
    [DataMember]
    public bool IsForward { get; set; }
    [DataMember]
    public bool IsDelayLoad { get; set; }
    [DataMember]
    public bool FileNotFound { get; set; }
    [DataMember]
    public bool Invalid { get; set; }
    [DataMember]
    public bool IsReproducibleBuild { get; set; }
    [DataMember]
    public bool IsApiSetContract { get; set; }
    [DataMember]
    public bool IsKernelModule { get; set; }
    [DataMember]
    public bool ExportContainErrors { get; set; }
    [DataMember]
    public bool OtherErrorsPresent { get; set; }
    //
    // Original module file name.
    //
    [DataMember]
    public SearchOrderType FileNameResolvedBy { get; set; }
    [DataMember]
    public string FileName { get; set; } = string.Empty;
    [DataMember]
    public string RawFileName { get; set; } = string.Empty;
    //
    // PE headers information.
    //
    [DataMember]
    public CModuleData ModuleData { get; set; }
    //
    // Base64 encoded manifest
    //
    [DataMember]
    public string ManifestData { get; set; } = string.Empty;
    //
    // Parent module imports.
    //
    [DataMember]
    public List<CFunction> ParentImports { get; set; } = [];
    //
    // List of modules that depends on us.
    //
    [DataMember]
    public List<CModule> Dependents { get; set; } = [];

    public CModule()
    {
        ModuleGuid = Guid.NewGuid();
    }

    public CModule(string moduleFileName)
    {
        ModuleGuid = Guid.NewGuid();
        RawFileName = moduleFileName;
        FileName = moduleFileName;
        ModuleData = new()
        {
            FileTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
    }

    public CModule(string moduleFileName, string rawModuleFileName, SearchOrderType fileNameResolvedBy, bool isApiSetContract)
    {
        ModuleGuid = Guid.NewGuid();
        RawFileName = rawModuleFileName;
        FileName = moduleFileName;
        FileNameResolvedBy = fileNameResolvedBy;
        IsApiSetContract = isApiSetContract;
        ModuleData = new()
        {
            FileTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
    }

    public ModuleInfoFlags GetModuleFlags()
    {
        ModuleInfoFlags flags = new();

        if (Invalid)
        {
            flags |= ModuleInfoFlags.Invalid;
        }

        if (FileNotFound)
        {
            flags |= ModuleInfoFlags.FileNotFound;
        }

        if (OtherErrorsPresent)
        {
            flags |= ModuleInfoFlags.WarningOtherErrors;
        }

        if (OriginalInstanceId != 0)
        {
            if (ExportContainErrors)
            {
                flags |= ModuleInfoFlags.DuplicateExportError;
            }
            else
            {
                flags |= ModuleInfoFlags.Duplicate;
            }
        }
        else
        {
            flags |= ModuleInfoFlags.Normal;

            if (ExportContainErrors)
            {
                flags |= ModuleInfoFlags.ExportError;
            }
        }

        return flags;
    }

    public bool Is64bitArchitecture()
    {
        var machine = ModuleData.Machine;
        bool is64BitMachine = machine == (uint)Machine.Amd64 ||
                              machine == (uint)Machine.IA64 ||
                              machine == (uint)Machine.Arm64 ||
                              machine == (uint)Machine.LoongArch64;
        return is64BitMachine;
    }

    /// <summary>
    /// Get icon index for tree view module display.
    /// </summary>
    /// <returns></returns>
    public int GetIconIndexForModule()
    {
        bool is64bit = Is64bitArchitecture();
        ModuleInfoFlags mflags = GetModuleFlags();
        bool bDuplicateAndExportError = mflags.HasFlag(ModuleInfoFlags.Duplicate | ModuleInfoFlags.ExportError);
        bool bDuplicate = mflags.HasFlag(ModuleInfoFlags.Duplicate);
        bool bFileNotFound = mflags.HasFlag(ModuleInfoFlags.FileNotFound);
        bool bExportError = mflags.HasFlag(ModuleInfoFlags.ExportError);
        bool bInvalid = mflags.HasFlag(ModuleInfoFlags.Invalid);
        bool bWarningOtherErrors = mflags.HasFlag(ModuleInfoFlags.WarningOtherErrors);

        if (IsDelayLoad)
        {
            if (bInvalid)
            {
                return (int)ModuleIconType.InvalidDelayLoadModule;
            }

            if (bFileNotFound)
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

            if (bExportError || bWarningOtherErrors)
            {
                return is64bit ? (int)ModuleIconType.DelayLoadModule64Warning : (int)ModuleIconType.DelayLoadModuleWarning;
            }

            return is64bit ? (int)ModuleIconType.DelayLoadModule64 : (int)ModuleIconType.DelayLoadModule;
        }

        if (IsForward)
        {
            if (bInvalid)
            {
                return (int)ModuleIconType.InvalidForwardedModule;
            }

            if (bFileNotFound)
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

            if (bExportError || bWarningOtherErrors)
            {
                return is64bit ? (int)ModuleIconType.ForwardedModule64Warning : (int)ModuleIconType.ForwardedModuleWarning;
            }

            return is64bit ? (int)ModuleIconType.ForwardedModule64 : (int)ModuleIconType.ForwardedModule;
        }

        if (bInvalid)
        {
            return (int)ModuleIconType.InvalidModule;
        }

        if (bFileNotFound)
        {
            return (int)ModuleIconType.MissingModule;
        }

        if (bDuplicate)
        {
            return is64bit ? (int)ModuleIconType.DuplicateModule64 : (int)ModuleIconType.DuplicateModule;
        }

        if (bWarningOtherErrors)
        {
            return is64bit ? (int)ModuleIconType.WarningModule64 : (int)ModuleIconType.WarningModule;
        }

        return is64bit ? (bExportError ? (int)ModuleIconType.WarningModule64 : (int)ModuleIconType.NormalModule64) :
            (bExportError ? (int)ModuleIconType.WarningModule : (int)ModuleIconType.NormalModule);
    }

    /// <summary>
    /// Get image index for compact (list view) module display.
    /// </summary>
    /// <returns></returns>
    public int GetIconIndexForModuleCompact()
    {
        bool is64bit = Is64bitArchitecture();
        ModuleInfoFlags mflags = GetModuleFlags();
        bool bFileNotFound = mflags.HasFlag(ModuleInfoFlags.FileNotFound);
        bool bExportError = mflags.HasFlag(ModuleInfoFlags.ExportError);
        bool bInvalid = mflags.HasFlag(ModuleInfoFlags.Invalid);
        bool bWarningOtherErrors = mflags.HasFlag(ModuleInfoFlags.WarningOtherErrors);

        if (IsDelayLoad)
        {
            if (bInvalid)
            {
                return (int)ModuleIconCompactType.DelayLoadInvalid;
            }

            if (bFileNotFound)
            {
                return (int)ModuleIconCompactType.DelayLoadMissing;
            }

            if (bExportError)
            {
                return (int)ModuleIconCompactType.DelayLoadModuleWarning;
            }

            if (bWarningOtherErrors)
            {
                return is64bit ? (int)ModuleIconCompactType.DelayLoadModule64Warning : (int)ModuleIconCompactType.DelayLoadModuleWarning;
            }

            return is64bit ? (int)ModuleIconCompactType.DelayLoadModule64 : (int)ModuleIconCompactType.DelayLoadModule;
        }

        if (bInvalid)
        {
            return (int)ModuleIconCompactType.Invalid;
        }

        if (bFileNotFound)
        {
            return (int)ModuleIconCompactType.MissingModule;
        }

        if (bWarningOtherErrors)
        {
            return is64bit ? (int)ModuleIconCompactType.WarningModule64 : (int)ModuleIconCompactType.WarningModule;
        }

        return is64bit ? (bExportError ? (int)ModuleIconCompactType.WarningModule64 : (int)ModuleIconCompactType.NormalModule64) :
            (bExportError ? (int)ModuleIconCompactType.WarningModule : (int)ModuleIconCompactType.NormalModule);
    }

    public override int GetHashCode()
    {
        return FileName.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    public string GetModuleNameRespectApiSet(bool needResolve)
    {
        return IsApiSetContract ? (needResolve ? FileName : RawFileName) : FileName;
    }

    public byte[] GetManifestDataAsArray()
    {
        try
        {
            if (!string.IsNullOrEmpty(ManifestData))
            {
                return Convert.FromBase64String(ManifestData);
            }
        }
        catch (FormatException)
        {
            return null;
        }

        return null;
    }
    public void SetManifestData(string data)
    {
        ManifestData = data;
    }

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
