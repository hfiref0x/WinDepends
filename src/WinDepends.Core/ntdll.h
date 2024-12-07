/*
*  File: ntdll.h
*
*  Created on: Aug 17, 2024
*
*  Modified on: Dec 02, 2024
*
*      Project: WinDepends.Core
*
*      Author: Harmonie
*/

#pragma once

#ifndef _NTDLL_H_
#define _NTDLL_H_

#pragma warning(push)
#pragma warning(disable: 4201) // nameless union

#ifndef _NTDEF_
typedef _Return_type_success_(return >= 0) LONG NTSTATUS;
typedef NTSTATUS* PNTSTATUS;
#endif

#ifndef RtlOffsetToPointer
#define RtlOffsetToPointer(Base, Offset)  ((PCHAR)( ((PCHAR)(Base)) + ((ULONG_PTR)(Offset))  ))
#endif

#ifndef RtlPointerToOffset
#define RtlPointerToOffset(Base, Pointer)  ((ULONG)( ((PCHAR)(Pointer)) - ((PCHAR)(Base))  ))
#endif

//
// Generic test for success on any status value (non-negative numbers
// indicate success).
//
#ifndef NT_SUCCESS
#define NT_SUCCESS(Status) ((NTSTATUS)(Status) >= 0)
#endif

typedef struct _UNICODE_STRING {
    USHORT Length;
    USHORT MaximumLength;
    PWSTR  Buffer;
} UNICODE_STRING, * PUNICODE_STRING;
typedef const UNICODE_STRING* PCUNICODE_STRING;

typedef struct _OBJECT_ATTRIBUTES {
    ULONG Length;
    HANDLE RootDirectory;
    PUNICODE_STRING ObjectName;
    ULONG Attributes;
    PVOID SecurityDescriptor;
    PVOID SecurityQualityOfService;
} OBJECT_ATTRIBUTES;
typedef OBJECT_ATTRIBUTES* POBJECT_ATTRIBUTES;

#define InitializeObjectAttributes( p, n, a, r, s ) { \
    (p)->Length = sizeof( OBJECT_ATTRIBUTES );          \
    (p)->RootDirectory = r;                             \
    (p)->Attributes = a;                                \
    (p)->ObjectName = n;                                \
    (p)->SecurityDescriptor = s;                        \
    (p)->SecurityQualityOfService = NULL;               \
    }

#ifndef OBJ_CASE_INSENSITIVE
#define OBJ_CASE_INSENSITIVE                0x00000040L
#endif

//
// SymbolicLink Object Access Rights
//
#ifndef SYMBOLIC_LINK_QUERY
#define SYMBOLIC_LINK_QUERY 0x0001
#endif

//
// Directory Object Access Rights
//
#ifndef DIRECTORY_QUERY
#define DIRECTORY_QUERY                 (0x0001)
#endif

#ifndef DIRECTORY_TRAVERSE
#define DIRECTORY_TRAVERSE              (0x0002)
#endif

typedef enum _OBJECT_INFORMATION_CLASS {
    ObjectBasicInformation,
    ObjectNameInformation,
    ObjectTypeInformation,
    ObjectTypesInformation,
    ObjectHandleFlagInformation,
    ObjectSessionInformation,
    ObjectSessionObjectInformation,
    MaxObjectInfoClass
} OBJECT_INFORMATION_CLASS;

typedef struct _CLIENT_ID {
    HANDLE UniqueProcess;
    HANDLE UniqueThread;
} CLIENT_ID, * PCLIENT_ID;

typedef struct _PEB {
    BOOLEAN InheritedAddressSpace;
    BOOLEAN ReadImageFileExecOptions;
    BOOLEAN BeingDebugged;
    union
    {
        BOOLEAN BitField;
        struct
        {
            BOOLEAN ImageUsesLargePages : 1;
            BOOLEAN IsProtectedProcess : 1;
            BOOLEAN IsImageDynamicallyRelocated : 1;
            BOOLEAN SkipPatchingUser32Forwarders : 1;
            BOOLEAN IsPackagedProcess : 1;
            BOOLEAN IsAppContainer : 1;
            BOOLEAN IsProtectedProcessLight : 1;
            BOOLEAN IsLongPathAwareProcess : 1;
        };
    };

    HANDLE Mutant;
    PVOID ImageBaseAddress;
    PVOID Ldr;
    PVOID ProcessParameters;
    PVOID SubSystemData;
    PVOID ProcessHeap;
    PRTL_CRITICAL_SECTION FastPebLock;
    PSLIST_HEADER AtlThunkSListPtr;
    PVOID IFEOKey;

    union
    {
        ULONG CrossProcessFlags;
        struct
        {
            ULONG ProcessInJob : 1;
            ULONG ProcessInitializing : 1;
            ULONG ProcessUsingVEH : 1;
            ULONG ProcessUsingVCH : 1;
            ULONG ProcessUsingFTH : 1;
            ULONG ProcessPreviouslyThrottled : 1;
            ULONG ProcessCurrentlyThrottled : 1;
            ULONG ProcessImagesHotPatched : 1;
            ULONG ReservedBits0 : 24;
        };
    };
    union
    {
        PVOID KernelCallbackTable;
        PVOID UserSharedInfoPtr;
    };
    ULONG SystemReserved;
    ULONG AtlThunkSListPtr32;
    PVOID ApiSetMap;
    //incomplete
} PEB, * PPEB;

typedef struct _TEB {
    NT_TIB NtTib;
    PVOID EnvironmentPointer;
    CLIENT_ID ClientId;
    PVOID ActiveRpcHandle;
    PVOID ThreadLocalStoragePointer;
    PPEB ProcessEnvironmentBlock;
    //incomplete
} TEB, * PTEB;

__inline struct _PEB* NtCurrentPeb() { return NtCurrentTeb()->ProcessEnvironmentBlock; }

#ifndef RtlInitEmptyUnicodeString
#define RtlInitEmptyUnicodeString(_ucStr,_buf,_bufSize) \
    ((_ucStr)->Buffer = (_buf), \
     (_ucStr)->Length = 0, \
     (_ucStr)->MaximumLength = (USHORT)(_bufSize))
#endif

typedef struct _OBJECT_DIRECTORY_INFORMATION {
    UNICODE_STRING Name;
    UNICODE_STRING TypeName;
} OBJECT_DIRECTORY_INFORMATION, * POBJECT_DIRECTORY_INFORMATION;

typedef VOID(NTAPI* pfnRtlInitUnicodeString)(
    _Out_ PUNICODE_STRING DestinationString,
    _In_opt_ PCWSTR SourceString);

typedef NTSTATUS(NTAPI* pfnNtOpenSymbolicLinkObject)(
    _Out_ PHANDLE LinkHandle,
    _In_ ACCESS_MASK DesiredAccess,
    _In_ POBJECT_ATTRIBUTES ObjectAttributes);

typedef NTSTATUS(NTAPI* pfnNtQuerySymbolicLinkObject)(
    _In_ HANDLE LinkHandle,
    _Inout_ PUNICODE_STRING LinkTarget,
    _Out_opt_ PULONG  ReturnedLength);

typedef NTSTATUS(NTAPI* pfnNtOpenDirectoryObject)(
    _Out_ PHANDLE DirectoryHandle,
    _In_ ACCESS_MASK DesiredAccess,
    _In_ POBJECT_ATTRIBUTES ObjectAttributes);

typedef NTSTATUS(NTAPI* pfnNtQueryDirectoryObject)(
    _In_ HANDLE DirectoryHandle,
    _Out_writes_bytes_opt_(Length) PVOID Buffer,
    _In_ ULONG Length,
    _In_ BOOLEAN ReturnSingleEntry,
    _In_ BOOLEAN RestartScan,
    _Inout_ PULONG Context,
    _Out_opt_ PULONG ReturnLength);

typedef NTSTATUS(NTAPI* pfnNtClose)(
    _In_ _Post_ptr_invalid_ HANDLE Handle);

typedef LONG(NTAPI* pfnRtlCompareUnicodeStrings)(
    _In_reads_(String1Length) PWCHAR String1,
    _In_ SIZE_T String1Length,
    _In_reads_(String2Length) PWCHAR String2,
    _In_ SIZE_T String2Length,
    _In_ BOOLEAN CaseInSensitive);

typedef LONG(NTAPI* pfnRtlCompareUnicodeString)(
    _In_ PCUNICODE_STRING String1,
    _In_ PCUNICODE_STRING String2,
    _In_ BOOLEAN CaseInSensitive);

typedef PIMAGE_NT_HEADERS(NTAPI* pfnRtlImageNtHeader)(
    _In_ PVOID Base);

#pragma warning(pop)

#endif /* _NTDLL_H_ */
