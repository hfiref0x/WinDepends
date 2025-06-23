/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CCOREDATACONTRACTS.CS
*
*  VERSION:     1.00
*
*  DATE:        22 Jun 2025
*  
*  Core Server reply structures (JSON serialized).
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.Serialization;

namespace WinDepends;

/// <summary>
/// Represents an exception reported by server.
/// </summary>
[DataContract]
public class CCoreException
{
    /// <summary>
    /// Exception code.
    /// </summary>
    [DataMember(Name = "code")]
    public UInt32 Code { get; set; }
    /// <summary>
    /// Location of the exception.
    /// </summary>
    [DataMember(Name = "location")]
    public UInt32 Location { get; set; }
}

/// <summary>
/// Represents a PE data directory entry.
/// </summary>
[DataContract]
public class CCoreDirectoryEntry
{
    /// <summary>
    /// Virtual address of the directory entry.
    /// </summary>
    [DataMember(Name = "vaddress")]
    public UInt32 VirtualAddress { get; set; }

    /// <summary>
    /// Size of the directory entry.
    /// </summary>
    [DataMember(Name = "size")]
    public UInt32 Size { get; set; }
}

/// <summary>
/// Represents Apiset namespace information (version and count).
/// </summary>
[DataContract]
public class CCoreApiSetNamespaceInfo
{
    /// <summary>
    /// Apiset namespace schema version.
    /// </summary>
    [DataMember(Name = "version")]
    public uint Version { get; set; }
    /// <summary>
    /// Count of entries in the namespace.
    /// </summary>
    [DataMember(Name = "count")]
    public uint Count { get; set; }
}

/// <summary>
/// Represents a resolved file name (typically a DLL or system path).
/// </summary>
[DataContract]
public class CCoreResolvedFileName
{
    /// <summary>
    /// The resolved file path.
    /// </summary>
    [DataMember(Name = "path")]
    public string Name { get; set; }
}

/// <summary>
/// Represents a single exported function from a PE file.
/// </summary>
[DataContract]
public class CCoreExportFunction
{
    /// <summary>
    /// Ordinal value of the export.
    /// </summary>
    [DataMember(Name = "ordinal")]
    public uint Ordinal { get; set; }

    /// <summary>
    /// Export hint.
    /// </summary>
    [DataMember(Name = "hint")]
    public uint Hint { get; set; }

    /// <summary>
    /// Name of the exported function.
    /// </summary>
    [DataMember(Name = "name")]
    public string Name { get; set; }

    /// <summary>
    /// Pointer (RVA) to the exported function.
    /// </summary>
    [DataMember(Name = "pointer")]
    public uint PointerAddress { get; set; }

    /// <summary>
    /// Forwarded export target (if any).
    /// </summary>
    [DataMember(Name = "forward")]
    public string Forward { get; set; }
}

/// <summary>
/// Represents an export library (export directory) of a PE file.
/// </summary>
[DataContract]
public class CCoreExportLibrary
{
    /// <summary>
    /// Timestamp of the export directory.
    /// </summary>
    [DataMember(Name = "timestamp")]
    public uint Timestamp { get; set; }

    /// <summary>
    /// Total number of exported entries.
    /// </summary>
    [DataMember(Name = "entries")]
    public uint Entries { get; set; }

    /// <summary>
    /// Number of named exports.
    /// </summary>
    [DataMember(Name = "named")]
    public uint Named { get; set; }

    /// <summary>
    /// Base ordinal for exports.
    /// </summary>
    [DataMember(Name = "base")]
    public uint Base { get; set; }

    /// <summary>
    /// List of exported functions.
    /// </summary>
    [DataMember(Name = "functions")]
    public List<CCoreExportFunction> Function { get; set; }
}

/// <summary>
/// Represents PE exports information.
/// </summary>
[DataContract]
public class CCoreExports
{
    /// <summary>
    /// Export library information.
    /// </summary>
    [DataMember(Name = "library")]
    public CCoreExportLibrary Library { get; set; }
}

/// <summary>
/// Represents a single imported function from a PE file.
/// </summary>
[DataContract]
public class CCoreImportFunction
{
    /// <summary>
    /// Ordinal value of the import.
    /// </summary>
    [DataMember(Name = "ordinal")]
    public uint Ordinal { get; set; }

    /// <summary>
    /// Import hint.
    /// </summary>
    [DataMember(Name = "hint")]
    public uint Hint { get; set; }

    /// <summary>
    /// Name of the imported function.
    /// </summary>
    [DataMember(Name = "name")]
    public string Name { get; set; }

    /// <summary>
    /// Bound address value (if available).
    /// </summary>
    [DataMember(Name = "bound")]
    public UInt64 Bound { get; set; }
}

/// <summary>
/// Represents a single import library and its functions.
/// </summary>
[DataContract]
public class CCoreImportLibrary
{
    /// <summary>
    /// Name of the import library (DLL).
    /// </summary>
    [DataMember(Name = "name")]
    public string Name { get; set; }

    /// <summary>
    /// List of imported functions.
    /// </summary>
    [DataMember(Name = "functions")]
    public List<CCoreImportFunction> Function { get; set; }
}

/// <summary>
/// Represents all import libraries for a PE file.
/// </summary>
[DataContract]
public class CCoreImports
{
    /// <summary>
    /// Exception bit flag:
    /// 0 = No error,
    /// 1 = Exception occurred in standard import parsing,
    /// 2 = Exception occurred in delay-load import parsing,
    /// 3 = Both standard and delay-load import parsing raised exceptions.
    /// </summary>
    [DataMember(Name = "exception")]
    public UInt32 Exception { get; set; }

    /// <summary>
    /// Exception code from the system for the standard import parsing exception, or 0 if none.
    /// </summary>
    [DataMember(Name = "exception_code_std")]
    public UInt32 ExceptionCodeStd { get; set; }

    /// <summary>
    /// Exception code from the system for the delay-load import parsing exception, or 0 if none.
    /// </summary>
    [DataMember(Name = "exception_code_delay")]
    public UInt32 ExceptionCodeDelay { get; set; }

    /// <summary>
    /// List of import libraries.
    /// </summary>
    [DataMember(Name = "libraries")]
    public List<CCoreImportLibrary> Library { get; set; }

    /// <summary>
    /// List of import libraries (delay-load).
    /// </summary>
    [DataMember(Name = "libraries_delay")]
    public List<CCoreImportLibrary> LibraryDelay { get; set; }
}

/// <summary>
/// Represents the KnownDlls registry state (path and entries).
/// </summary>
[DataContract]
public class CCoreKnownDlls
{
    /// <summary>
    /// Path to the KnownDlls directory.
    /// </summary>
    [DataMember(Name = "path")]
    public string DllPath { get; set; }

    /// <summary>
    /// List of DLL entries in KnownDlls.
    /// </summary>
    [DataMember(Name = "entries")]
    public List<string> Entries { get; set; }
}

/// <summary>
/// Represents basic call statistics for a client session.
/// </summary>
[DataContract]
public class CCoreCallStats
{
    /// <summary>
    /// Total bytes sent to the client.
    /// </summary>
    [DataMember(Name = "totalBytesSent")]
    public UInt64 TotalBytesSent { get; set; }

    /// <summary>
    /// Total number of send calls.
    /// </summary>
    [DataMember(Name = "totalSendCalls")]
    public UInt64 TotalSendCalls { get; set; }

    /// <summary>
    /// Total time spent on calls (in milliseconds).
    /// </summary>
    [DataMember(Name = "totalTimeSpent")]
    public UInt64 TotalTimeSpent { get; set; }
}

/// <summary>
/// Represents file information for an opened PE file.
/// </summary>
[DataContract]
public class CCoreFileInformation
{
    /// <summary>
    /// File attributes.
    /// </summary>
    [DataMember(Name = "FileAttributes")]
    public uint FileAttributes { get; set; }

    /// <summary>
    /// Low part of the creation time.
    /// </summary>
    [DataMember(Name = "CreationTimeLow")]
    public uint CreationTimeLow { get; set; }

    /// <summary>
    /// High part of the creation time.
    /// </summary>
    [DataMember(Name = "CreationTimeHigh")]
    public uint CreationTimeHigh { get; set; }

    /// <summary>
    /// Low part of the last write time.
    /// </summary>
    [DataMember(Name = "LastWriteTimeLow")]
    public uint LastWriteTimeLow { get; set; }

    /// <summary>
    /// High part of the last write time.
    /// </summary>
    [DataMember(Name = "LastWriteTimeHigh")]
    public uint LastWriteTimeHigh { get; set; }

    /// <summary>
    /// High part of the file size.
    /// </summary>
    [DataMember(Name = "FileSizeHigh")]
    public uint FileSizeHigh { get; set; }

    /// <summary>
    /// Low part of the file size.
    /// </summary>
    [DataMember(Name = "FileSizeLow")]
    public uint FileSizeLow { get; set; }

    /// <summary>
    /// File checksum if available.
    /// </summary>
    [DataMember(Name = "RealChecksum")]
    public uint RealChecksum { get; set; }

    /// <summary>
    /// Indicates if the image is loaded at its preferred base address.
    /// </summary>
    [DataMember(Name = "ImageFixed")]
    public uint ImageFixed { get; set; }

    /// <summary>
    /// Indicates whether the file contains a .NET (managed) image (1 if .NET, 0 if native).
    /// </summary>
    [DataMember(Name = "ImageDotNet")]
    public uint ImageDotNet { get; set; }
}

/// <summary>
/// Represents the IMAGE_FILE_HEADER structure of a PE file.
/// </summary>
[DataContract]
public class CCoreFileHeader
{
    /// <summary>
    /// Machine type.
    /// </summary>
    [DataMember(Name = "Machine")]
    public UInt16 Machine { get; set; }

    /// <summary>
    /// Number of sections.
    /// </summary>
    [DataMember(Name = "NumberOfSections")]
    public UInt16 NumberOfSections { get; set; }

    /// <summary>
    /// Time/date stamp.
    /// </summary>
    [DataMember(Name = "TimeDateStamp")]
    public UInt32 TimeDateStamp { get; set; }

    /// <summary>
    /// Pointer to symbol table.
    /// </summary>
    [DataMember(Name = "PointerToSymbolTable")]
    public UInt32 PointerToSymbolTable { get; set; }

    /// <summary>
    /// Number of symbols.
    /// </summary>
    [DataMember(Name = "NumberOfSymbols")]
    public UInt32 NumberOfSymbols { get; set; }

    /// <summary>
    /// Size of optional header.
    /// </summary>
    [DataMember(Name = "SizeOfOptionalHeader")]
    public UInt16 SizeOfOptionalHeader { get; set; }

    /// <summary>
    /// File characteristics.
    /// </summary>
    [DataMember(Name = "Characteristics")]
    public UInt16 Characteristics { get; set; }
}

/// <summary>
/// Represents the IMAGE_OPTIONAL_HEADER structure of a PE file.
/// </summary>
[DataContract]
public class CCoreOptionalHeader
{
    [DataMember(Name = "Magic")]
    public UInt16 Magic { get; set; }

    [DataMember(Name = "MajorLinkerVersion")]
    public byte MajorLinkerVersion { get; set; }

    [DataMember(Name = "MinorLinkerVersion")]
    public byte MinorLinkerVersion { get; set; }

    [DataMember(Name = "SizeOfCode")]
    public UInt32 SizeOfCode { get; set; }

    [DataMember(Name = "SizeOfInitializedData")]
    public UInt32 SizeOfInitializedData { get; set; }

    [DataMember(Name = "SizeOfUninitializedData")]
    public UInt32 SizeOfUninitializedData { get; set; }

    [DataMember(Name = "AddressOfEntryPoint")]
    public UInt32 AddressOfEntryPoint { get; set; }

    [DataMember(Name = "BaseOfCode")]
    public UInt32 BaseOfCode { get; set; }

    [DataMember(Name = "ImageBase")]
    public UInt64 ImageBase { get; set; }

    [DataMember(Name = "SectionAlignment")]
    public UInt32 SectionAlignment { get; set; }

    [DataMember(Name = "FileAlignment")]
    public UInt32 FileAlignment { get; set; }

    [DataMember(Name = "MajorOperatingSystemVersion")]
    public UInt16 MajorOperatingSystemVersion { get; set; }

    [DataMember(Name = "MinorOperatingSystemVersion")]
    public UInt16 MinorOperatingSystemVersion { get; set; }

    [DataMember(Name = "MajorImageVersion")]
    public UInt16 MajorImageVersion { get; set; }

    [DataMember(Name = "MinorImageVersion")]
    public UInt16 MinorImageVersion { get; set; }

    [DataMember(Name = "MajorSubsystemVersion")]
    public UInt16 MajorSubsystemVersion { get; set; }

    [DataMember(Name = "MinorSubsystemVersion")]
    public UInt16 MinorSubsystemVersion { get; set; }

    [DataMember(Name = "Win32VersionValue")]
    public UInt32 Win32VersionValue { get; set; }

    [DataMember(Name = "SizeOfImage")]
    public UInt32 SizeOfImage { get; set; }

    [DataMember(Name = "SizeOfHeaders")]
    public UInt32 SizeOfHeaders { get; set; }

    [DataMember(Name = "CheckSum")]
    public UInt32 CheckSum { get; set; }

    [DataMember(Name = "Subsystem")]
    public UInt16 Subsystem { get; set; }

    [DataMember(Name = "DllCharacteristics")]
    public UInt16 DllCharacteristics { get; set; }

    [DataMember(Name = "SizeOfStackReserve")]
    public UInt64 SizeOfStackReserve { get; set; }

    [DataMember(Name = "SizeOfStackCommit")]
    public UInt64 SizeOfStackCommit { get; set; }

    [DataMember(Name = "SizeOfHeapReserve")]
    public UInt64 SizeOfHeapReserve { get; set; }

    [DataMember(Name = "SizeOfHeapCommit")]
    public UInt64 SizeOfHeapCommit { get; set; }

    [DataMember(Name = "LoaderFlags")]
    public UInt32 LoaderFlags { get; set; }

    [DataMember(Name = "NumberOfRvaAndSizes")]
    public UInt32 NumberOfRvaAndSizes { get; set; }
}

/// <summary>
/// Represents a debug directory entry in a PE file.
/// </summary>
[DataContract]
public class CCoreDebugDirectory
{
    /// <summary>
    /// Characteristics of the debug directory.
    /// </summary>
    [DataMember(Name = "Characteristics")]
    public uint Characteristics { get; set; }

    /// <summary>
    /// Time/date stamp.
    /// </summary>
    [DataMember(Name = "TimeDateStamp")]
    public uint TimeDateStamp { get; set; }

    /// <summary>
    /// Major version number.
    /// </summary>
    [DataMember(Name = "MajorVersion")]
    public uint MajorVersion { get; set; }

    /// <summary>
    /// Minor version number.
    /// </summary>
    [DataMember(Name = "MinorVersion")]
    public uint MinorVersion { get; set; }

    /// <summary>
    /// Debug directory type.
    /// </summary>
    [DataMember(Name = "Type")]
    public uint Type { get; set; }

    /// <summary>
    /// Size of data.
    /// </summary>
    [DataMember(Name = "SizeOfData")]
    public uint SizeOfData { get; set; }

    /// <summary>
    /// Address of raw data.
    /// </summary>
    [DataMember(Name = "AddressOfRawData")]
    public uint AddressOfRawData { get; set; }

    /// <summary>
    /// Pointer to raw data.
    /// </summary>
    [DataMember(Name = "PointerToRawData")]
    public uint PointerToRawData { get; set; }
}

/// <summary>
/// Represents file version information.
/// </summary>
[DataContract]
public class CCoreFileVersion
{
    [DataMember(Name = "dwFileVersionMS")]
    public uint FileVersionMS { get; set; }

    [DataMember(Name = "dwFileVersionLS")]
    public uint FileVersionLS { get; set; }

    [DataMember(Name = "dwProductVersionMS")]
    public uint ProductVersionMS { get; set; }

    [DataMember(Name = "dwProductVersionLS")]
    public uint ProductVersionLS { get; set; }
}

/// <summary>
/// Represents PE32+ headers, debug directories, file version, and extended characteristics.
/// </summary>
[DataContract]
public class CCoreImageHeaders
{
    /// <summary>
    /// File header (IMAGE_FILE_HEADER).
    /// </summary>
    [DataMember(Name = "ImageFileHeader")]
    public CCoreFileHeader FileHeader { get; set; }

    /// <summary>
    /// Optional header (IMAGE_OPTIONAL_HEADER).
    /// </summary>
    [DataMember(Name = "ImageOptionalHeader")]
    public CCoreOptionalHeader OptionalHeader { get; set; }

    /// <summary>
    /// List of debug directory entries.
    /// </summary>
    [DataMember(Name = "DebugDirectory")]
    public List<CCoreDebugDirectory> DebugDirectory { get; set; }

    /// <summary>
    /// File version information.
    /// </summary>
    [DataMember(Name = "Version")]
    public CCoreFileVersion FileVersion { get; set; }

    /// <summary>
    /// Extended DLL characteristics.
    /// </summary>
    [DataMember(Name = "dllcharex")]
    public uint ExtendedDllCharacteristics { get; set; }

    /// <summary>
    /// Embedded manifest in base64 encoding, if present.
    /// </summary>
    [DataMember(Name = "manifest")]
    public string Base64Manifest { get; set; }
}
