/*
*  File: util.c
*
*  Created on: Aug 04, 2024
*
*  Modified on: May 18, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#include "core.h"

SUP_CONTEXT gsup;

typedef BOOL(WINAPI* pfnMiniDumpWriteDump)(
    _In_ HANDLE hProcess,
    _In_ DWORD ProcessId,
    _In_ HANDLE hFile,
    _In_ MINIDUMP_TYPE DumpType,
    _In_opt_ PMINIDUMP_EXCEPTION_INFORMATION ExceptionParam,
    _In_opt_ PMINIDUMP_USER_STREAM_INFORMATION UserStreamParam,
    _In_opt_ PMINIDUMP_CALLBACK_INFORMATION CallbackParam);

BOOL ex_write_dump(
    _In_ EXCEPTION_POINTERS* ExceptionPointers,
    _In_ LPCWSTR lpFileName
)
{
    BOOL bResult;
    HMODULE hDbgHelp;
    HANDLE hFile;
    WCHAR szBuffer[MAX_PATH * 2];
    UINT cch;

    MINIDUMP_EXCEPTION_INFORMATION mdei;

    pfnMiniDumpWriteDump pMiniDumpWriteDump;

    bResult = FALSE;
    hDbgHelp = GetModuleHandle(TEXT("dbghelp.dll"));
    if (hDbgHelp == NULL) {

        RtlSecureZeroMemory(szBuffer, sizeof(szBuffer));
        cch = GetSystemDirectory(szBuffer, MAX_PATH);
        if (cch == 0 || cch > MAX_PATH)
            return FALSE;

        StringCchCat(szBuffer, MAX_PATH, L"\\dbghelp.dll");
        hDbgHelp = LoadLibraryEx(szBuffer, 0, 0);
        if (hDbgHelp == NULL)
            return FALSE;
    }

    pMiniDumpWriteDump = (pfnMiniDumpWriteDump)GetProcAddress(hDbgHelp, "MiniDumpWriteDump");
    if (pMiniDumpWriteDump == NULL)
        return FALSE;

    StringCchPrintf(szBuffer, ARRAYSIZE(szBuffer), L"%ws.exception.dmp", lpFileName);
    hFile = CreateFile(szBuffer, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, 0, NULL);
    if (hFile != INVALID_HANDLE_VALUE) {
        mdei.ThreadId = GetCurrentThreadId();
        mdei.ExceptionPointers = ExceptionPointers;
        mdei.ClientPointers = FALSE;
        bResult = pMiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), hFile, MiniDumpNormal, &mdei, NULL, NULL);
        CloseHandle(hFile);
    }
    return bResult;
}

int ex_filter_dbg(
    _In_ WCHAR *fileName, 
    _In_ unsigned int code, 
    _In_ struct _EXCEPTION_POINTERS* ep
)
{
    if (code == EXCEPTION_ACCESS_VIOLATION)
    {
        ex_write_dump(ep, fileName);
        return EXCEPTION_EXECUTE_HANDLER;
    }
    else
    {
        return EXCEPTION_CONTINUE_SEARCH;
    };
}

int ex_filter(
    _In_ unsigned int code, 
    _In_ struct _EXCEPTION_POINTERS* ep
)
{
    UNREFERENCED_PARAMETER(ep);

    if (code == EXCEPTION_ACCESS_VIOLATION)
    {
        return EXCEPTION_EXECUTE_HANDLER;
    }
    else
    {
        return EXCEPTION_CONTINUE_SEARCH;
    };
}

LPVOID heap_malloc(_In_opt_ HANDLE heap, _In_ SIZE_T size)
{
    HANDLE hHeap = (heap == NULL) ? GetProcessHeap() : heap;

    return HeapAlloc(hHeap, 0, size);
}

LPVOID heap_calloc(_In_opt_ HANDLE heap, _In_ SIZE_T size)
{
    HANDLE hHeap = (heap == NULL) ? GetProcessHeap() : heap;

    return HeapAlloc(hHeap, HEAP_ZERO_MEMORY, size);
}

BOOL heap_free(_In_opt_ HANDLE heap, _In_ LPVOID memory)
{
    HANDLE hHeap = (heap == NULL) ? GetProcessHeap() : heap;

    return HeapFree(hHeap, 0, memory);
}

int sendstring_plaintext_no_track(
    _In_ SOCKET s, 
    _In_ const wchar_t* Buffer
)
{
    return (send(s, (const char*)Buffer, (int)wcslen(Buffer) * sizeof(wchar_t), 0) >= 0);
}

int sendstring_plaintext(
    _In_ SOCKET s, 
    _In_ const wchar_t* Buffer,
    _In_opt_ pmodule_ctx context
)
{
    int result;
    LARGE_INTEGER endCount;
    LONG64 timeTaken;
    BOOL enableStats;

    int bufferLength = (int)wcslen(Buffer) * sizeof(wchar_t);
    enableStats = ((context != NULL) && context->enable_call_stats);

    if (enableStats) {
        QueryPerformanceCounter(&context->start_count);
    }

    result = send(s, (const char*)Buffer, bufferLength, 0);

    if (enableStats && result != SOCKET_ERROR) {
        QueryPerformanceCounter(&endCount);
        timeTaken = (LONG64)((endCount.QuadPart - context->start_count.QuadPart) * 1000000 / gsup.PerformanceFrequency.QuadPart);

        context->total_bytes_sent += result;
        context->total_send_calls += 1;
        context->total_time_spent += timeTaken;
    }

    return (result >= 0);
}

__forceinline wchar_t locase_w(_In_ wchar_t c)
{
    if ((c >= 'A') && (c <= 'Z'))
        return c + 0x20;
    else
        return c;
}

__forceinline int _isdigit_w(_In_ wchar_t x) {
    return ((x >= L'0') && (x <= L'9'));
}

unsigned long strtoul_w(
    _In_ const wchar_t* s)
{
    unsigned long long	a = 0;
    wchar_t			c;

    if (s == 0)
        return 0;

    while (*s != 0) {
        c = *s;
        if (_isdigit_w(c))
            a = (a * 10) + (c - L'0');
        else
            break;

        if (a > ULONG_MAX)
            return ULONG_MAX;

        s++;
    }
    return (unsigned long)a;
}

wchar_t* _filepath_w(
    _In_ const wchar_t* fname, 
    _Out_ wchar_t* fpath)
{
    wchar_t* p = (wchar_t*)fname, * p0 = (wchar_t*)fname, * p1 = (wchar_t*)fpath;

    if ((fname == 0) || (fpath == NULL))
        return 0;

    while (*fname != L'\0') {
        if (*fname == L'\\')
            p = (wchar_t*)fname + 1;
        fname++;
    }

    while (p0 < p) {
        *p1 = *p0;
        p1++;
        p0++;
    }
    *p1 = 0;

    return fpath;
}

USHORT chk_sum(
    _In_ ULONG partial_sum, 
    _In_ PUSHORT source, 
    _In_ ULONG length)
{
    while (length--)
    {
        partial_sum += *source++;
        partial_sum = (partial_sum >> 16) + (partial_sum & 0xffff);
    }
    return (USHORT)(((partial_sum >> 16) + partial_sum) & 0xffff);
}

/*
* calc_mapped_file_chksum
*
* Purpose:
*
* Calculate PE file checksum.
*
*/
DWORD calc_mapped_file_chksum(
    _In_ PVOID base_address,
    _In_ ULONG file_length,
    _In_ PUSHORT opt_hdr_chksum
)
{
    USHORT partial_sum;

    partial_sum = chk_sum(0, (PUSHORT)base_address, (file_length + 1) >> 1);
    partial_sum -= (partial_sum < opt_hdr_chksum[0]);
    partial_sum -= opt_hdr_chksum[0];
    partial_sum -= (partial_sum < opt_hdr_chksum[1]);
    partial_sum -= opt_hdr_chksum[1];

    return (ULONG)partial_sum + file_length;
}

/*
* build_knowndlls_list
*
* Purpose:
*
* Read and remember KnownDlls list from object directory.
*
*/
BOOL build_knowndlls_list(
    _In_ BOOL IsWow64
)
{
    BOOL bResult = FALSE;

    NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
    ULONG returnLength = 0, ctx;

    HANDLE hDirectory = NULL;
    HANDLE hLink = NULL;

    POBJECT_DIRECTORY_INFORMATION pDirInfo;

    UNICODE_STRING usName, usKnownDllsPath;
    OBJECT_ATTRIBUTES objectAttributes;

    SIZE_T cbNameEntry;
    SIZE_T cbMaxName = 0, cbPath;

    PWCH stringBuffer = NULL;
    PWSTR lpKnownDllsDirName;
    PSUP_PATH_ELEMENT_ENTRY dllEntry, dllsHead;

    if (IsWow64) {
        dllsHead = &gsup.KnownDlls32Head;
        lpKnownDllsDirName = L"\\KnownDlls32";
    }
    else {
        dllsHead = &gsup.KnownDllsHead;
        lpKnownDllsDirName = L"\\KnownDlls";
    }

    gsup.RtlInitUnicodeString(&usName, lpKnownDllsDirName);
    InitializeObjectAttributes(&objectAttributes, &usName, OBJ_CASE_INSENSITIVE, NULL, NULL);

    do {

        ntStatus = gsup.NtOpenDirectoryObject(&hDirectory, DIRECTORY_QUERY | DIRECTORY_TRAVERSE, &objectAttributes);
        if (!NT_SUCCESS(ntStatus)) {
            break;
        }

        gsup.RtlInitUnicodeString(&usName, L"KnownDllPath");
        objectAttributes.RootDirectory = hDirectory;

        ntStatus = gsup.NtOpenSymbolicLinkObject(&hLink, SYMBOLIC_LINK_QUERY, &objectAttributes);
        if (!NT_SUCCESS(ntStatus)) {
            break;
        }

        usKnownDllsPath.Buffer = NULL;
        usKnownDllsPath.Length = usKnownDllsPath.MaximumLength = 0;

        ntStatus = gsup.NtQuerySymbolicLinkObject(hLink, &usKnownDllsPath, &returnLength);
        if (ntStatus != STATUS_BUFFER_TOO_SMALL && ntStatus != STATUS_BUFFER_OVERFLOW)
        {
            break;
        }

        stringBuffer = (PWCH)heap_calloc(NULL, sizeof(UNICODE_NULL) + returnLength);
        if (stringBuffer == NULL) {
            break;
        }

        usKnownDllsPath.Buffer = stringBuffer;
        usKnownDllsPath.Length = 0;
        usKnownDllsPath.MaximumLength = (USHORT)returnLength;

        ntStatus = gsup.NtQuerySymbolicLinkObject(hLink, &usKnownDllsPath, &returnLength);
        if (!NT_SUCCESS(ntStatus)) {
            break;
        }

        cbPath = usKnownDllsPath.Length;

        if (IsWow64) {
            gsup.KnownDlls32Path = stringBuffer;
            gsup.KnownDlls32PathCbMax = cbPath;
        }
        else {
            gsup.KnownDllsPath = stringBuffer;
            gsup.KnownDllsPathCbMax = cbPath;
        }

        ctx = 0;

        do {

            returnLength = 0;
            ntStatus = gsup.NtQueryDirectoryObject(hDirectory, NULL, 0, TRUE, FALSE, &ctx, &returnLength);
            if (ntStatus != STATUS_BUFFER_TOO_SMALL)
                break;

            pDirInfo = (POBJECT_DIRECTORY_INFORMATION)heap_calloc(NULL, returnLength);
            if (pDirInfo == NULL)
                break;

            ntStatus = gsup.NtQueryDirectoryObject(hDirectory, pDirInfo, returnLength, TRUE, FALSE, &ctx, &returnLength);
            if (!NT_SUCCESS(ntStatus)) {
                heap_free(NULL, pDirInfo);
                break;
            }

            dllEntry = (PSUP_PATH_ELEMENT_ENTRY)heap_calloc(NULL, sizeof(SUP_PATH_ELEMENT_ENTRY));
            if (dllEntry) {

                if (_wcsicmp(pDirInfo->TypeName.Buffer, L"Section") == 0) {

                    cbNameEntry = (SIZE_T)pDirInfo->Name.MaximumLength;

                    dllEntry->Element = (PWSTR)heap_calloc(NULL, cbNameEntry);

                    if (dllEntry->Element) {

                        RtlCopyMemory(dllEntry->Element, pDirInfo->Name.Buffer, pDirInfo->Name.Length);
                        dllEntry->Next = dllsHead->Next;

                        // Remember max filename size.
                        if (cbNameEntry > cbMaxName) {
                            cbMaxName = cbNameEntry;
                        }

                        dllsHead->Next = dllEntry;
                    }
                }
            }

            heap_free(NULL, pDirInfo);

        } while (TRUE);

        if (IsWow64) {
            gsup.KnownDlls32NameCbMax = cbMaxName;
        }
        else {
            gsup.KnownDllsNameCbMax = cbMaxName;
        }

        bResult = TRUE;

    } while (FALSE);

    if (hLink) {
        gsup.NtClose(hLink);
    }

    if (hDirectory) {
        gsup.NtClose(hDirectory);
    }

    if (!bResult && stringBuffer)
        heap_free(NULL, stringBuffer);

    return bResult;
}

/*
* load_apiset_namespace
*
* Purpose:
*
* Query apiset table from schema dll.
*
*/
PVOID load_apiset_namespace(
    _In_ LPCWSTR apiset_schema_dll
)
{
    ULONG dataSize = 0;
    UINT i;
    HMODULE hApiSetDll;

    PIMAGE_NT_HEADERS ntHeaders;
    IMAGE_SECTION_HEADER* sectionTableEntry;
    PBYTE baseAddress;
    PBYTE dataPtr = NULL;

#ifndef _WIN64
    PVOID oldValue;
#endif

#ifndef _WIN64
    Wow64DisableWow64FsRedirection(&oldValue);
#endif
    hApiSetDll = LoadLibraryEx(apiset_schema_dll, NULL, LOAD_LIBRARY_AS_DATAFILE);
    if (hApiSetDll) {

        baseAddress = (PBYTE)(((ULONG_PTR)hApiSetDll) & ~3);

        ntHeaders = gsup.RtlImageNtHeader(baseAddress);

        sectionTableEntry = IMAGE_FIRST_SECTION(ntHeaders);

        i = ntHeaders->FileHeader.NumberOfSections;
        while (i > 0) {
            if (_strnicmp((CHAR*)&sectionTableEntry->Name, API_SET_SECTION_NAME,
                sizeof(API_SET_SECTION_NAME)) == 0)
            {
                dataSize = sectionTableEntry->SizeOfRawData;

                dataPtr = (PBYTE)RtlOffsetToPointer(
                    baseAddress,
                    sectionTableEntry->PointerToRawData);

                break;
            }
            i -= 1;
            sectionTableEntry += 1;
        }

    }

#ifndef _WIN64
    Wow64RevertWow64FsRedirection(oldValue);
#endif

    return dataPtr;
}

/*
* utils_init
*
* Purpose:
*
* Initialize support context structure.
*
*/
void utils_init()
{
    RtlSecureZeroMemory(&gsup, sizeof(SUP_CONTEXT));
    QueryPerformanceFrequency(&gsup.PerformanceFrequency);

    gsup.ApiSetMap = NtCurrentPeb()->ApiSetMap;

    HMODULE hNtdll = GetModuleHandle(L"ntdll.dll");
    if (hNtdll == NULL) {
        return;
    }

    gsup.NtOpenSymbolicLinkObject = (pfnNtOpenSymbolicLinkObject)GetProcAddress(hNtdll, "NtOpenSymbolicLinkObject");
    gsup.NtOpenDirectoryObject = (pfnNtOpenDirectoryObject)GetProcAddress(hNtdll, "NtOpenDirectoryObject");
    gsup.NtQueryDirectoryObject = (pfnNtQueryDirectoryObject)GetProcAddress(hNtdll, "NtQueryDirectoryObject");
    gsup.NtQuerySymbolicLinkObject = (pfnNtQuerySymbolicLinkObject)GetProcAddress(hNtdll, "NtQuerySymbolicLinkObject");
    gsup.RtlInitUnicodeString = (pfnRtlInitUnicodeString)GetProcAddress(hNtdll, "RtlInitUnicodeString");
    gsup.RtlCompareUnicodeStrings = (pfnRtlCompareUnicodeStrings)GetProcAddress(hNtdll, "RtlCompareUnicodeStrings");
    gsup.RtlCompareUnicodeString = (pfnRtlCompareUnicodeString)GetProcAddress(hNtdll, "RtlCompareUnicodeString");
    gsup.RtlImageNtHeader = (pfnRtlImageNtHeader)GetProcAddress(hNtdll, "RtlImageNtHeader");
    gsup.NtClose = (pfnNtClose)GetProcAddress(hNtdll, "NtClose");

    if (gsup.NtOpenSymbolicLinkObject == NULL ||
        gsup.NtOpenDirectoryObject == NULL ||
        gsup.NtQueryDirectoryObject == NULL ||
        gsup.NtQuerySymbolicLinkObject == NULL ||
        gsup.RtlInitUnicodeString == NULL ||
        gsup.RtlCompareUnicodeStrings == NULL ||
        gsup.RtlCompareUnicodeString  == NULL ||
        gsup.RtlImageNtHeader == NULL ||
        gsup.NtClose == NULL)
    {
        return;
    }

    BOOL bWow64 = FALSE;
    gsup.dwAllocationGranularity = PAGE_GRANULARITY;

    if (IsWow64Process(GetCurrentProcess(), &bWow64)) {

        SYSTEM_INFO si;

        if (bWow64) {
            GetNativeSystemInfo(&si);
        }
        else {
            GetSystemInfo(&si);
        }

        gsup.dwAllocationGranularity = si.dwAllocationGranularity;

    }

    if (build_knowndlls_list(FALSE) &&
        build_knowndlls_list(TRUE))
    {
        gsup.Initialized = TRUE;
    }

}

/*
* resolve_apiset_name
*
* Purpose:
*
* Lookup apiset target dll by contract name.
*
*/
_Success_(return != NULL)
LPWSTR resolve_apiset_name(
    _In_ LPCWSTR apiset_name,
    _In_opt_ LPCWSTR parent_name,
    _Out_ SIZE_T * name_length
)
{
    NTSTATUS Status = STATUS_NOT_FOUND;
    PAPI_SET_NAMESPACE ApiSetNamespace;
    UNICODE_STRING Name, ParentName, ResolvedName;
    LPWSTR ResolvedNameBuffer;
    SIZE_T NameBufferSize;

    __try {

        *name_length = 0;

        if (gsup.Initialized == FALSE) {
            return NULL;
        }

        gsup.RtlInitUnicodeString(&Name, apiset_name);
        if (parent_name) {
            gsup.RtlInitUnicodeString(&ParentName, parent_name);
        }

        ApiSetNamespace = (PAPI_SET_NAMESPACE)gsup.ApiSetMap;
        RtlInitEmptyUnicodeString(&ResolvedName, NULL, 0);

        switch (ApiSetNamespace->Version) {

        case API_SET_SCHEMA_VERSION_V2:
            Status = ApiSetResolveToHostV2(ApiSetNamespace, &Name, NULL, &ResolvedName);
            break;

        case API_SET_SCHEMA_VERSION_V4:
            Status = ApiSetResolveToHostV4(ApiSetNamespace, &Name, NULL, &ResolvedName);
            break;

        case API_SET_SCHEMA_VERSION_V6:
            Status = ApiSetResolveToHostV6(ApiSetNamespace, &Name, NULL, &ResolvedName);
            break;

        default:
            break;
        }

        if (NT_SUCCESS(Status) && ResolvedName.Length) {

            NameBufferSize = ResolvedName.Length + sizeof(UNICODE_NULL);
            ResolvedNameBuffer = (LPWSTR)heap_calloc(NULL, NameBufferSize);
            if (ResolvedNameBuffer) {

                if (S_OK == StringCbCopyN(ResolvedNameBuffer, 
                    NameBufferSize,
                    ResolvedName.Buffer, 
                    ResolvedName.Length)) 
                {
                    *name_length = ResolvedName.Length;
                    return ResolvedNameBuffer;
                }
                else {
                    heap_free(NULL, ResolvedNameBuffer);
                    return NULL;
                }

            }
        }

    }
    __except (ex_filter(GetExceptionCode(), GetExceptionInformation())) {
        printf("exception in resolve_apiset_name\r\n");
    }

    return NULL;
}

/*
* get_manifest
*
* Purpose:
*
* Read SxS create process manifest from the file resource and return it as base64 encoded array.
*
*/
LPVOID get_manifest(
    _In_ HMODULE module
)
{
    HRSRC   h_manifest;
    DWORD   sz_manifest, cch_encoded = 0;
    HGLOBAL p_manifest;
    LPVOID  encoded;
    SIZE_T  buffer_size = 0;

    if (module == NULL) {
        return NULL;
    }

    do {
        h_manifest = FindResource(module, CREATEPROCESS_MANIFEST_RESOURCE_ID, RT_MANIFEST);
        if (h_manifest == NULL) {
            printf("get_manifest: FindResource failed: 0x%08X\r\n", GetLastError());
            break;
        }

        sz_manifest = SizeofResource(module, h_manifest);
        if (sz_manifest == 0) {
            printf("get_manifest: SizeofResource failed: 0x%08X\r\n", GetLastError());
            break;
        }

        p_manifest = LoadResource(module, h_manifest);
        if (p_manifest == NULL) {
            printf("get_manifest: LoadResource failed: 0x%08X\r\n", GetLastError());
            break;
        }

        if (!CryptBinaryToString(p_manifest, sz_manifest,
            CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF,
            NULL,
            &cch_encoded))
        {
            printf("get_manifest: CryptBinaryToString (1) failed: 0x%08X\r\n", GetLastError());
            break;
        }

        if (FAILED(SizeTMult(cch_encoded, sizeof(WCHAR), &buffer_size))) {
            printf("get_manifest: arithmetic overflow\r\n");
            break;
        }

        encoded = heap_calloc(NULL, buffer_size);
        if (encoded) {
            if (CryptBinaryToString(p_manifest, sz_manifest,
                CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF,
                encoded, &cch_encoded))
            {
                return encoded;
            }
            else {
                printf("get_manifest: CryptBinaryToString (2) failed: 0x%08X\r\n", GetLastError());
                heap_free(NULL, encoded);
            }
        }

    } while (FALSE);

    return NULL;
}

/*
* get_params_token
*
* Purpose:
*
* Query tokens from parameters string.
*
*/
_Success_(return) BOOL get_params_token(
    _In_ LPCWSTR params,
    _In_ ULONG token_index,
    _Out_ LPWSTR buffer,
    _In_ ULONG buffer_length, //in chars
    _Out_ PULONG token_len
)
{
    ULONG c, plen = 0;
    WCHAR divider;

    *token_len = 0;

    if (params == NULL) {
        if ((buffer != NULL) && (buffer_length > 0)) {
            *buffer = 0;
        }
        return FALSE;
    }

    for (c = 0; c <= token_index; c++) {
        plen = 0;

        while (*params == ' ') {
            params++;
        }

        switch (*params) {
        case 0:
            goto zero_term_exit;

        case '"':
            params++;
            divider = '"';
            break;

        default:
            divider = ' ';
        }

        while ((*params != '"') && (*params != divider) && (*params != 0)) {
            plen++;
            if (c == token_index)
                if ((plen < buffer_length) && (buffer != NULL)) {
                    *buffer = *params;
                    buffer++;
                }
            params++;
        }

        if (*params != 0)
            params++;
    }

zero_term_exit:

    if ((buffer != NULL) && (buffer_length > 0))
        *buffer = 0;

    *token_len = plen;

    return (plen < buffer_length) ? TRUE : FALSE;
}

/*
* get_params_option
*
* Purpose:
*
* Query parameters options by name and type.
*
*/
_Success_(return) BOOL get_params_option(
    _In_ LPCWSTR params,
    _In_ LPCWSTR option_name,
    _In_ BOOL is_parametric,
    _Out_opt_ LPWSTR value,
    _In_ ULONG value_length, //in chars
    _Out_opt_ PULONG param_length
)
{
    BOOL result;
    WCHAR param_buffer[MAX_PATH + 1];
    ULONG rlen;
    INT	i = 0;

    if (param_length)
        *param_length = 0;

    if (is_parametric) {
        if (value == NULL || value_length == 0)
        {
            return FALSE;
        }
    }

    if (value)
        *value = L'\0';

    RtlSecureZeroMemory(param_buffer, sizeof(param_buffer));

    while (get_params_token(
        params,
        i,
        param_buffer,
        MAX_PATH,
        &rlen))
    {
        if (rlen == 0)
            break;

        if (wcscmp(param_buffer, option_name) == 0) {
            if (is_parametric) {
                result = get_params_token(params, i + 1, value, value_length, &rlen);
                if (param_length)
                    *param_length = rlen;
                return result;
            }

            return TRUE;
        }
        ++i;
    }

    return FALSE;
}

/*
* report_exception_to_client
*
* Purpose:
*
* Send exception information to the client.
*
*/
VOID report_exception_to_client(
    _In_ SOCKET s,
    _In_ exception_location location,
    _In_ DWORD exception_code
)
{
    WCHAR text[512];
    LPWSTR exlocation;

    switch (location) {
    case ex_headers:
        exlocation = L"file headers";
        break;
    case ex_datadirs:
        exlocation = L"data directories";
        break;
    case ex_imports:
        exlocation = L"imports";
        break;
    case ex_exports:
        exlocation = L"exports";
        break;
    default:
        exlocation = L"data";
        break;
    }

    StringCchPrintf(text, ARRAYSIZE(text),
        L"%sAn unhandled exception (0x%lX) occurred while processing %s of \r\n",
        WDEP_STATUS_600,
        exception_code,
        exlocation);

    sendstring_plaintext_no_track(s, text);
}
