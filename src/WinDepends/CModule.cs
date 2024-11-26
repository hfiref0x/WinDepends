/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CMODULE.CS
*
*  VERSION:     1.00
*
*  DATE:        25 Nov 2024
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
        $"{(fileAttributes.HasFlag(FileAttributes.Compressed) ? "C" : "")}";
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

[Serializable()]
public class CModuleData
{
    public DateTime FileTimeStamp { get; set; }
    public UInt32 LinkTimeStamp { get; set; }
    public UInt64 FileSize { get; set; }
    public FileAttributes Attributes { get; set; } = FileAttributes.Normal;
    public uint LinkChecksum { get; set; }
    public uint RealChecksum { get; set; }
    public ushort Machine { get; set; } = (ushort)System.Reflection.PortableExecutable.Machine.Amd64;
    public ushort Characteristics { get; set; } = (ushort)System.Reflection.PortableExecutable.Characteristics.ExecutableImage;
    public ushort DllCharacteristics { get; set; }
    public uint ExtendedCharacteristics { get; set; }
    public ushort Subsystem { get; set; } = (ushort)System.Reflection.PortableExecutable.Subsystem.WindowsCui;
    public UInt64 PreferredBase { get; set; }
    public UInt64 ActualBase { get; set; }
    public uint VirtualSize { get; set; }
    public uint LoadOrder { get; set; }
    public string FileVersion { get; set; }
    public string ProductVersion { get; set; }
    public string ImageVersion { get; set; }
    public string LinkerVersion { get; set; }
    public string OSVersion { get; set; }
    public string SubsystemVersion { get; set; }
    public List<uint> DebugDirTypes { get; set; } = [];
    public bool DebugInfoPresent() => DebugDirTypes.Count > 0;

    //
    // Module exports.
    //
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
        Machine = other.Machine;
        Characteristics = other.Characteristics;
        DllCharacteristics = other.DllCharacteristics;
        ExtendedCharacteristics = other.ExtendedCharacteristics;
        Subsystem = other.Subsystem;
        PreferredBase = other.PreferredBase;
        ActualBase = other.ActualBase;
        VirtualSize = other.VirtualSize;
        LoadOrder = other.LoadOrder;
        FileVersion = other.FileVersion;
        ProductVersion = other.ProductVersion;
        ImageVersion = other.ImageVersion;
        LinkerVersion = other.LinkerVersion;
        OSVersion = other.OSVersion;
        SubsystemVersion = other.SubsystemVersion;
        DebugDirTypes = new List<uint>(other.DebugDirTypes);
    }
}

[Serializable()]
public class CModule
{
    //
    // Unique instance id, representing module, generated as GetHashCode()
    //
    public int InstanceId { get; set; }

    //
    // Original instance of module, if we are duplicate.
    //
    public int OriginalInstanceId { get; set; }

    public int ModuleImageIndex { get; set; }

    public bool IsProcessed { get; set; }

    public int Depth { get; set; }

    public bool IsForward { get; set; }
    public bool IsDelayLoad { get; set; }
    public bool FileNotFound { get; set; }
    public bool Invalid { get; set; }
    public bool IsReproducibleBuild { get; set; }
    public bool IsApiSetContract { get; set; }
    public bool IsKernelModule { get; set; }
    public bool ExportContainErrors { get; set; }
    public bool OtherErrorsPresent { get; set; }

    //
    // Original module file name.
    //
    public SearchOrderType FileNameResolvedBy { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string RawFileName { get; set; } = string.Empty;

    //
    // PE headers information.
    //
    public CModuleData ModuleData { get; set; }

    //
    // Base64 encoded manifest
    //
    public string ManifestData { get; set; } = string.Empty;

    //
    // Parent module imports.
    //
    public List<CFunction> ParentImports { get; set; } = [];

    //
    // List of modules that depends on us.
    //
    public List<CModule> Dependents { get; set; } = [];

    public CModule()
    {
    }

    public CModule(string moduleFileName)
    {
        RawFileName = moduleFileName;
        FileName = moduleFileName;
        ModuleData = new()
        {
            FileTimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
    }

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

            if (bWarningOtherErrors)
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

            if (bWarningOtherErrors)
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
        if (!string.IsNullOrEmpty(ManifestData))
        {
            return Convert.FromBase64String(ManifestData);
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
