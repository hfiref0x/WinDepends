/*
*  File: util.h
*
*  Created on: Aug 04, 2024
*
*  Modified on: Aug 03, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#pragma once

#ifndef _UTIL_H_
#define _UTIL_H_

typedef struct _SUP_PATH_ELEMENT_ENTRY {
    struct _SUP_PATH_ELEMENT_ENTRY* Next;
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

    BOOL EnableCallStats;
    LARGE_INTEGER PerformanceFrequency;

    DWORD dwAllocationGranularity;

    pfnNtOpenSymbolicLinkObject NtOpenSymbolicLinkObject;
    pfnNtOpenDirectoryObject NtOpenDirectoryObject;
    pfnNtQueryDirectoryObject NtQueryDirectoryObject;
    pfnNtQuerySymbolicLinkObject NtQuerySymbolicLinkObject;
    pfnRtlInitUnicodeString RtlInitUnicodeString;
    pfnRtlCompareUnicodeStrings RtlCompareUnicodeStrings;
    pfnRtlCompareUnicodeString RtlCompareUnicodeString;
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
FORCEINLINE
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

void utils_init();

int sendstring_plaintext(
    _In_ SOCKET s,
    _In_ const wchar_t* Buffer,
    _In_opt_ pmodule_ctx context
);

int sendstring_plaintext_no_track(
    _In_ SOCKET s, 
    _In_ const wchar_t* Buffer
);

PVOID load_apiset_namespace(
    _In_ LPCWSTR apiset_schema_dll
);

_Success_(return != NULL) LPWSTR resolve_apiset_name(
    _In_ LPCWSTR apiset_name,
    _In_opt_ LPCWSTR parent_name,
    _Out_ SIZE_T * name_length
);

unsigned long strtoul_w(_In_ const wchar_t* s);
wchar_t* _filepath_w(
    _In_ const wchar_t* fname, 
    _Out_ wchar_t* fpath);

DWORD calc_mapped_file_chksum(
    _In_ PVOID base_address,
    _In_ ULONG file_length,
    _In_ PUSHORT opt_hdr_chksum
);

LPVOID get_manifest(
    _In_ HMODULE module
);

_Success_(return) BOOL get_params_token(
    _In_ LPCWSTR params,
    _In_ ULONG token_index,
    _Out_ LPWSTR buffer,
    _In_ ULONG buffer_length, //in chars
    _Out_ PULONG token_len
);

_Success_(return) BOOL get_params_option(
    _In_ LPCWSTR params,
    _In_ LPCWSTR option_name,
    _In_ BOOL is_parametric,
    _Out_opt_ LPWSTR value,
    _In_ ULONG value_length, //in chars
    _Out_opt_ PULONG param_length
);

LPVOID heap_malloc(_In_opt_ HANDLE heap, _In_ SIZE_T size);
LPVOID heap_calloc(_In_opt_ HANDLE heap, _In_ SIZE_T size);
BOOL heap_free(_In_opt_ HANDLE heap, _In_ LPVOID memory);

int ex_filter(_In_ unsigned int code, _In_ struct _EXCEPTION_POINTERS* ep);
int ex_filter_dbg(_In_ WCHAR* fileName, _In_ unsigned int code, _In_ struct _EXCEPTION_POINTERS* ep);

typedef enum _exception_location {
    ex_headers = 0,
    ex_datadirs,
    ex_imports,
    ex_exports
} exception_location;

VOID report_exception_to_client(
    _In_ SOCKET s,
    _In_ exception_location location,
    _In_ DWORD exception_code);

_Success_(return) 
BOOL json_escape_string(
    _In_ LPCWSTR src,
    _Out_writes_to_(dest_cch, *out_len) LPWSTR dest,
    _In_ SIZE_T dest_cch,
    _Out_ SIZE_T * out_len
);

#endif /* _UTIL_H_ */
