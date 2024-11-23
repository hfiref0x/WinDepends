/*
*  File: util.h
*
*  Created on: Aug 04, 2024
*
*  Modified on: Sep 25, 2024
*
*      Project: WinDepends.Core
*
*      Author:
*/

#pragma once

#ifndef _UTIL_H_
#define _UTIL_H_

typedef struct _GLOBAL_STATS {
    LARGE_INTEGER frequency;
    LARGE_INTEGER startCount;
    DWORD64 totalBytesSent;
    DWORD64 totalSendCalls;
    DWORD64 totalTimeSpent;
} GLOBAL_STATS, * PGLOBAL_STATS;

typedef struct _SUP_PATH_ELEMENT_ENTRY {
    struct _SUP_PATH_ELEMENT_ENTRY* Next;
    DWORD Hash;
    PWSTR Element;
} SUP_PATH_ELEMENT_ENTRY, * PSUP_PATH_ELEMENT_ENTRY;

typedef struct _SUP_CONTEXT {
    BOOL Initialized;

    SIZE_T KnownDllsNameCbMax;
    SIZE_T KnownDlls32NameCbMax;

    SUP_PATH_ELEMENT_ENTRY KnownDllsHead;
    SUP_PATH_ELEMENT_ENTRY KnownDlls32Head;

    PWSTR KnownDllsPath;
    PWSTR KnownDlls32Path;

    SIZE_T KnownDllsPathCbMax;
    SIZE_T KnownDlls32PathCbMax;

    BOOL UseApiSetMapFile;
    PVOID ApiSetMap;

    BOOL UseRelocation;
    ULONG MinAppAddress;

    pfnNtOpenSymbolicLinkObject NtOpenSymbolicLinkObject;
    pfnNtOpenDirectoryObject NtOpenDirectoryObject;
    pfnNtQueryDirectoryObject NtQueryDirectoryObject;
    pfnNtQuerySymbolicLinkObject NtQuerySymbolicLinkObject;
    pfnRtlInitUnicodeString RtlInitUnicodeString;
    pfnRtlCompareUnicodeStrings RtlCompareUnicodeStrings;
    pfnRtlImageNtHeader RtlImageNtHeader;
    pfnNtClose NtClose;

} SUP_CONTEXT, * PSUP_CONTEXT;

FORCEINLINE
VOID
InitializeListHead(
    _Out_ PLIST_ENTRY ListHead
)
{
    ListHead->Flink = ListHead->Blink = ListHead;
    return;
}

_Must_inspect_result_
BOOLEAN
CFORCEINLINE
IsListEmpty(
    _In_ const LIST_ENTRY* ListHead
)
{
    return (BOOLEAN)(ListHead->Flink == ListHead);
}

FORCEINLINE
VOID
InsertTailList(
    _Inout_ PLIST_ENTRY ListHead,
    _Inout_ __drv_aliasesMem PLIST_ENTRY Entry
)
{
    PLIST_ENTRY Blink;

    Blink = ListHead->Blink;
    Entry->Flink = ListHead;
    Entry->Blink = Blink;
    Blink->Flink = Entry;
    ListHead->Blink = Entry;
    return;
}

extern SUP_CONTEXT gsup;
extern GLOBAL_STATS gstats;

void utils_init();

int sendstring_plaintext(SOCKET s, const wchar_t* Buffer);
int sendstring_plaintext_nodbg(SOCKET s, const wchar_t* Buffer);

BOOL name_is_apiset(
    _In_ LPCWSTR set_name
);

LPWSTR resolve_apiset_name(
    _In_ LPCWSTR name_to_resolve,
    _In_opt_ LPCWSTR parent_library_name,
    _Out_ SIZE_T* name_length
);

unsigned long strtoul_w(wchar_t* s);
wchar_t* _filepath_w(const wchar_t* fname, wchar_t* fpath);
wchar_t* _filename_w(const wchar_t* f);

DWORD calc_mapped_file_chksum(
    _In_ PVOID base_address,
    _In_ ULONG file_length,
    _In_ PUSHORT opt_hdr_chksum
);

void base64encode(char* s, char* b64);

LPVOID heap_malloc(_In_opt_ HANDLE heap, _In_ SIZE_T size);
LPVOID heap_calloc(_In_opt_ HANDLE heap, _In_ SIZE_T size);
BOOL heap_free(_In_opt_ HANDLE heap, _In_ LPVOID memory);

int ex_filter(unsigned int code, struct _EXCEPTION_POINTERS* ep);
int ex_filter_dbg(WCHAR* fileName, unsigned int code, struct _EXCEPTION_POINTERS* ep);

#endif _UTIL_H_
