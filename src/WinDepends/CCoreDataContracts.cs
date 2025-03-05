/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CCOREDATACONTRACTS.CS
*
*  VERSION:     1.00
*
*  DATE:        07 Dec 2024
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

/*
 *  CCoreDataDirectory.
 * 
*/

[DataContract]
public class CCoreDirectoryEntry
{

    [DataMember(Name = "vaddress")]
    public UInt32 VirtualAddress { get; set; }

    [DataMember(Name = "size")]
    public UInt32 Size { get; set; }
}

[DataContract]
public class CCoreDataDirectoryRoot
{

    [DataMember(Name = "directories")]
    public List<CCoreDirectoryEntry> Entry { get; set; }
}

/*
 *  CCoreApiSetNamespaceInfo.
 * 
*/

[DataContract]
public class CCoreApiSetNamespaceInfo
{
    [DataMember(Name = "version")]
    public uint Version { get; set; }
    [DataMember(Name = "count")]
    public uint Count { get; set; }
}

[DataContract]
public class CCoreApiSetNamespaceInfoRoot
{
    [DataMember(Name = "apisetns")]
    public CCoreApiSetNamespaceInfo Namespace { get; set; }
}

/*
 *  CCoreResolvedFileName.
 * 
*/

[DataContract]
public class CCoreResolvedFileName
{
    [DataMember(Name = "path")]
    public string Name { get; set; }
}

[DataContract]
public class CCoreResolvedFileNameRoot
{
    [DataMember(Name = "filename")]
    public CCoreResolvedFileName FileName { get; set; }
}

/*
 *  CCoreExports.
 * 
*/

[DataContract]
public class CCoreExportFunction
{

    [DataMember(Name = "ordinal")]
    public uint Ordinal { get; set; }

    [DataMember(Name = "hint")]
    public uint Hint { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "pointer")]
    public uint PointerAddress { get; set; }

    [DataMember(Name = "forward")]
    public string Forward { get; set; }
}

[DataContract]
public class CCoreExportLibrary
{
    [DataMember(Name = "timestamp")]
    public uint Timestamp { get; set; }
    [DataMember(Name = "entries")]
    public uint Entries { get; set; }
    [DataMember(Name = "named")]
    public uint Named { get; set; }
    [DataMember(Name = "base")]
    public uint Base { get; set; }
    [DataMember(Name = "functions")]
    public List<CCoreExportFunction> Function { get; set; }
}

[DataContract]
public class CCoreExports
{

    [DataMember(Name = "library")]
    public CCoreExportLibrary Library { get; set; }
}

[DataContract]
public class CCoreExportsRoot
{

    [DataMember(Name = "export")]
    public CCoreExports Export { get; set; }
}

/*
 *  CCoreImports.
 * 
*/
[DataContract]
public class CCoreImportFunction
{
    [DataMember(Name = "ordinal")]
    public uint Ordinal { get; set; }

    [DataMember(Name = "hint")]
    public uint Hint { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "bound")]
    public UInt64 Bound { get; set; }
}

[DataContract]
public class CCoreImportLibrary
{
    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "delay")]
    public int IsDelayLibrary { get; set; }

    [DataMember(Name = "functions")]
    public List<CCoreImportFunction> Function { get; set; }
}

[DataContract]
public class CCoreImports
{
    [DataMember(Name = "libraries")]
    public List<CCoreImportLibrary> Library { get; set; }
}

[DataContract]
public class CCoreImportsRoot
{
    [DataMember(Name = "import")]
    public CCoreImports Import { get; set; }
}

/*
 *  CCoreKnownDlls.
 * 
*/
[DataContract]
public class CCoreKnownDlls
{
    [DataMember(Name = "path")]
    public string DllPath { get; set; }
    [DataMember(Name = "entries")]
    public List<string> Entries { get; set; }
}

[DataContract]
public class CCoreKnownDllsRoot
{
    [DataMember(Name = "knowndlls")]
    public CCoreKnownDlls KnownDlls { get; set; }
}

/*
 *  CCoreServStats.
 * 
*/

[DataContract]
public class CCoreServStats
{

    [DataMember(Name = "threadsCount")]
    public UInt64 ThreadsCount { get; set; }

    [DataMember(Name = "socketsCreated")]
    public UInt64 SocketsCreated { get; set; }

    [DataMember(Name = "socketsClosed")]
    public UInt64 SocketsClosed { get; set; }
}

[DataContract]
public class CCoreServStatsRoot
{
    [DataMember(Name = "servstats")]
    public CCoreServStats ServStats { get; set; }
}

/*
 *  CCoreCallStats.
 * 
*/

[DataContract]
public class CCoreCallStats
{

    [DataMember(Name = "totalBytesSent")]
    public UInt64 TotalBytesSent { get; set; }

    [DataMember(Name = "totalSendCalls")]
    public UInt64 TotalSendCalls { get; set; }

    [DataMember(Name = "totalTimeSpent")]
    public UInt64 TotalTimeSpent { get; set; }
}

[DataContract]
public class CCoreCallStatsRoot
{
    [DataMember(Name = "stats")]
    public CCoreCallStats CallStats { get; set; }
}


/*
 *  CCoreFileInformation.
 * 
*/

[DataContract]
public class CCoreFileInformation
{
    [DataMember(Name = "FileAttributes")]
    public uint FileAttributes { get; set; }

    [DataMember(Name = "CreationTimeLow")]
    public uint CreationTimeLow { get; set; }

    [DataMember(Name = "CreationTimeHigh")]
    public uint CreationTimeHigh { get; set; }

    [DataMember(Name = "LastWriteTimeLow")]
    public uint LastWriteTimeLow { get; set; }

    [DataMember(Name = "LastWriteTimeHigh")]
    public uint LastWriteTimeHigh { get; set; }

    [DataMember(Name = "FileSizeHigh")]
    public uint FileSizeHigh { get; set; }

    [DataMember(Name = "FileSizeLow")]
    public uint FileSizeLow { get; set; }

    [DataMember(Name = "RealChecksum")]
    public uint RealChecksum { get; set; }

    [DataMember(Name = "ImageFixed")]
    public uint ImageFixed { get; set; }
}

[DataContract]
public class CCoreFileInformationRoot
{
    [DataMember(Name = "fileinfo")]
    public CCoreFileInformation FileInformation { get; set; }
}

/*
 *  CCoreStructs.
 * 
*/

[DataContract]
public class CCoreFileHeader
{

    [DataMember(Name = "Machine")]
    public UInt16 Machine { get; set; }

    [DataMember(Name = "NumberOfSections")]
    public UInt16 NumberOfSections { get; set; }

    [DataMember(Name = "TimeDateStamp")]
    public UInt32 TimeDateStamp { get; set; }

    [DataMember(Name = "PointerToSymbolTable")]
    public UInt32 PointerToSymbolTable { get; set; }

    [DataMember(Name = "NumberOfSymbols")]
    public UInt32 NumberOfSymbols { get; set; }

    [DataMember(Name = "SizeOfOptionalHeader")]
    public UInt16 SizeOfOptionalHeader { get; set; }

    [DataMember(Name = "Characteristics")]
    public UInt16 Characteristics { get; set; }
}

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

[DataContract]
public class CCoreDebugDirectory
{

    [DataMember(Name = "Characteristics")]
    public uint Characteristics { get; set; }

    [DataMember(Name = "TimeDateStamp")]
    public uint TimeDateStamp { get; set; }

    [DataMember(Name = "MajorVersion")]
    public uint MajorVersion { get; set; }

    [DataMember(Name = "MinorVersion")]
    public uint MinorVersion { get; set; }

    [DataMember(Name = "Type")]
    public uint Type { get; set; }

    [DataMember(Name = "SizeOfData")]
    public uint SizeOfData { get; set; }

    [DataMember(Name = "AddressOfRawData")]
    public uint AddressOfRawData { get; set; }

    [DataMember(Name = "PointerToRawData")]
    public uint PointerToRawData { get; set; }
}

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
/// PE32+ headers
/// List of debug directories
/// FileVersion
/// Extended characteristics
/// </summary>
[DataContract]
public class CCoreStructs
{
    [DataMember(Name = "ImageFileHeader")]
    public CCoreFileHeader FileHeader { get; set; }
    [DataMember(Name = "ImageOptionalHeader")]
    public CCoreOptionalHeader OptionalHeader { get; set; }
    [DataMember(Name = "DebugDirectory")]
    public List<CCoreDebugDirectory> DebugDirectory { get; set; }
    [DataMember(Name = "Version")]
    public CCoreFileVersion FileVersion { get; set; }
    [DataMember(Name = "dllcharex")]
    public uint ExtendedDllCharacteristics { get; set; }
    [DataMember(Name = "manifest")]
    public string Base64Manifest { get; set; }
}

[DataContract]
public class CCoreStructsRoot
{
    [DataMember(Name = "headers")]
    public CCoreStructs HeadersInfo { get; set; }
}