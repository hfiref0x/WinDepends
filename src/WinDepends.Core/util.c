/*
*  File: util.c
*
*  Created on: Aug 04, 2024
*
*  Modified on: Nov 27, 2024
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

int ex_filter_dbg(WCHAR *fileName, unsigned int code, struct _EXCEPTION_POINTERS* ep)
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

int ex_filter(unsigned int code, struct _EXCEPTION_POINTERS* ep)
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

void calculatePerformanceStats(DWORD64 bytesSent, LONG64 timeTaken)
{
    InterlockedAdd64((PLONG64)&gsup.CallStats.totalBytesSent, bytesSent);
    InterlockedIncrement64((PLONG64)&gsup.CallStats.totalSendCalls);
    InterlockedAdd64((PLONG64)&gsup.CallStats.totalTimeSpent, timeTaken);
}

int sendstring_plaintext_no_track(SOCKET s, const wchar_t* Buffer)
{
    return (send(s, (const char*)Buffer, (int)wcslen(Buffer) * sizeof(wchar_t), 0) >= 0);
}

int sendstring_plaintext(SOCKET s, const wchar_t* Buffer)
{
    int result;
    LARGE_INTEGER endCount;
    LONG64 timeTaken;

    int bufferLength = (int)wcslen(Buffer) * sizeof(wchar_t);

    if (gsup.EnableCallStats) {
        QueryPerformanceCounter(&gsup.CallStats.startCount);
    }

    result = send(s, (const char*)Buffer, bufferLength, 0);

    if (gsup.EnableCallStats && result != SOCKET_ERROR) {
        QueryPerformanceCounter(&endCount);
        timeTaken = (LONG64)((endCount.QuadPart - gsup.CallStats.startCount.QuadPart) * 1000000 / gsup.CallStats.frequency.QuadPart);
        calculatePerformanceStats(result, timeTaken);
    }

    return (result >= 0);
}

__forceinline wchar_t locase_w(wchar_t c)
{
    if ((c >= 'A') && (c <= 'Z'))
        return c + 0x20;
    else
        return c;
}

#define ULONG_MAX_VALUE 0xffffffffUL

__forceinline int _isdigit_w(wchar_t x) {
    return ((x >= L'0') && (x <= L'9'));
}

unsigned long strtoul_w(wchar_t* s)
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

        if (a > ULONG_MAX_VALUE)
            return ULONG_MAX_VALUE;

        s++;
    }
    return (unsigned long)a;
}

wchar_t* _filepath_w(const wchar_t* fname, wchar_t* fpath)
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

wchar_t* _filename_w(const wchar_t* f)
{
    wchar_t* p = (wchar_t*)f;

    if (!f)
        return NULL;

    while (*f != L'\0') {
        if (*f == L'\\')
            p = (wchar_t*)f + 1;
        f++;
    }
    return p;
}

USHORT chk_sum(ULONG partial_sum, PUSHORT source, ULONG length)
{
    while (length--)
    {
        partial_sum += *source++;
        partial_sum = (partial_sum >> 16) + (partial_sum & 0xffff);
    }
    return (USHORT)(((partial_sum >> 16) + partial_sum) & 0xffff);
}

/*
* sdbm_hash_string
*
* Purpose:
*
* Create sdbm hash for given string.
*
*/
ULONG sdbm_hash_string(PCWSTR String, ULONG Length)
{
    ULONG hash_value = 0, n_chars = Length;
    PCWSTR string_buffer = String;

    while (n_chars-- != 0)
        hash_value = (hash_value * 65599) + *string_buffer++;

    return hash_value;
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
                        dllEntry->Hash = sdbm_hash_string(dllEntry->Element, pDirInfo->Name.Length / sizeof(WCHAR));
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

PSUP_PATH_ELEMENT_ENTRY find_entry_by_file_name(
    _In_ LPCWSTR file_name,
    _In_ BOOL is_wow_list
)
{
    PSUP_PATH_ELEMENT_ENTRY dll_entry, dlls_head;
    DWORD file_name_hash;

    dlls_head = (is_wow_list) ? &gsup.KnownDlls32Head : &gsup.KnownDllsHead;

    file_name_hash = sdbm_hash_string(file_name, (ULONG)wcslen(file_name));

    dll_entry = dlls_head->Next;

    while (dll_entry != NULL) {
        if (dll_entry->Hash == file_name_hash) {
            return dll_entry;
        }
        dll_entry = dll_entry->Next;
    }

    return NULL;
}

VOID resolve_apiset_namespace()
{
    ULONG cch, dataSize = 0;
    UINT i;
    WCHAR szSystemDirectory[MAX_PATH + 1];
    WCHAR szFileName[MAX_PATH * 2];
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

    RtlSecureZeroMemory(szSystemDirectory, sizeof(szSystemDirectory));
    cch = GetSystemDirectory(szSystemDirectory, MAX_PATH);
    if (cch && cch < MAX_PATH) {

        StringCchPrintf(szFileName,
            RTL_NUMBER_OF(szFileName),
            TEXT("%s\\apisetschema.dll"),
            szSystemDirectory);

        hApiSetDll = LoadLibraryEx(szFileName, NULL, LOAD_LIBRARY_AS_DATAFILE);
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

            if (dataPtr != NULL && dataSize != 0) {
                gsup.ApiSetMap = dataPtr;
            }
        }
    }

#ifndef _WIN64
    Wow64RevertWow64FsRedirection(oldValue);
#endif
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
    QueryPerformanceFrequency(&gsup.CallStats.frequency);

    HMODULE hNtdll = GetModuleHandle(L"ntdll.dll");
    if (hNtdll == NULL) {
        return;
    }

    gsup.MinAppAddress = DEFAULT_APP_ADDRESS;
    gsup.NtOpenSymbolicLinkObject = (pfnNtOpenSymbolicLinkObject)GetProcAddress(hNtdll, "NtOpenSymbolicLinkObject");
    gsup.NtOpenDirectoryObject = (pfnNtOpenDirectoryObject)GetProcAddress(hNtdll, "NtOpenDirectoryObject");
    gsup.NtQueryDirectoryObject = (pfnNtQueryDirectoryObject)GetProcAddress(hNtdll, "NtQueryDirectoryObject");
    gsup.NtQuerySymbolicLinkObject = (pfnNtQuerySymbolicLinkObject)GetProcAddress(hNtdll, "NtQuerySymbolicLinkObject");
    gsup.RtlInitUnicodeString = (pfnRtlInitUnicodeString)GetProcAddress(hNtdll, "RtlInitUnicodeString");
    gsup.RtlCompareUnicodeStrings = (pfnRtlCompareUnicodeStrings)GetProcAddress(hNtdll, "RtlCompareUnicodeStrings");
    gsup.RtlImageNtHeader = (pfnRtlImageNtHeader)GetProcAddress(hNtdll, "RtlImageNtHeader");
    gsup.NtClose = (pfnNtClose)GetProcAddress(hNtdll, "NtClose");

    if (gsup.NtOpenSymbolicLinkObject == NULL ||
        gsup.NtOpenDirectoryObject == NULL ||
        gsup.NtQueryDirectoryObject == NULL ||
        gsup.NtQuerySymbolicLinkObject == NULL ||
        gsup.RtlInitUnicodeString == NULL ||
        gsup.RtlCompareUnicodeStrings == NULL ||
        gsup.RtlImageNtHeader == NULL ||
        gsup.NtClose == NULL)
    {
        return;
    }

    resolve_apiset_namespace();

    if (build_knowndlls_list(FALSE) &&
        build_knowndlls_list(TRUE))
    {
        gsup.Initialized = TRUE;
    }

    cmd_init();
}

/*
* ApiSetpSearchForApiSetHost
*
* Purpose:
*
* Resolve alias name if present.
*
*/
PAPI_SET_VALUE_ENTRY_V6 ApiSetpSearchForApiSetHost(
    _In_ PAPI_SET_NAMESPACE_ENTRY_V6 Entry,
    _In_ PWCHAR ApiSetToResolve,
    _In_ USHORT ApiSetToResolveLength,
    _In_ PVOID Namespace)
{
    API_SET_VALUE_ENTRY_V6* ValueEntry;
    API_SET_VALUE_ENTRY_V6* AliasValueEntry, * Result = NULL;
    ULONG AliasCount, i, AliasIndex;
    PWCHAR AliasName;
    LONG CompareResult;

    ValueEntry = API_SET_TO_VALUE_ENTRY(Namespace, Entry, 0);
    AliasCount = Entry->Count;

    if (AliasCount >= 1) {

        i = 1;

        do {
            AliasIndex = (AliasCount + i) >> 1;
            AliasValueEntry = API_SET_TO_VALUE_ENTRY(Namespace, Entry, AliasIndex);
            AliasName = API_SET_TO_VALUE_NAME(Namespace, AliasValueEntry);

            CompareResult = gsup.RtlCompareUnicodeStrings(ApiSetToResolve,
                ApiSetToResolveLength,
                AliasName,
                AliasValueEntry->NameLength >> 1,
                TRUE);

            if (CompareResult < 0) {
                AliasCount = AliasIndex - 1;
            }
            else {
                if (CompareResult == 0) {

                    Result = API_SET_TO_VALUE_ENTRY(Namespace,
                        Entry,
                        ((AliasCount + i) >> 1));

                    break;
                }
                i = (AliasCount + 1);
            }

        } while (i <= AliasCount);

    }
    else {
        Result = ValueEntry;
    }

    return Result;
}

/*
* ApiSetpSearchForApiSet
*
* Purpose:
*
* Find apiset entry by hash from it name.
*
*/
PAPI_SET_NAMESPACE_ENTRY_V6 ApiSetpSearchForApiSet(
    _In_ PVOID Namespace,
    _In_ PWCHAR ResolveName,
    _In_ USHORT ResolveNameEffectiveLength)
{
    ULONG LookupHash = 0, i, c, HashIndex, EntryCount, EntryHash;
    WCHAR ch;

    PWCHAR NamespaceEntryName;
    API_SET_HASH_ENTRY_V6* LookupHashEntry;
    PAPI_SET_NAMESPACE_ENTRY_V6 NamespaceEntry = NULL;
    PAPI_SET_NAMESPACE_ARRAY_V6 ApiSetNamespace = (PAPI_SET_NAMESPACE_ARRAY_V6)Namespace;

    if (ApiSetNamespace->Count == 0 || ResolveNameEffectiveLength == 0)
        return NULL;

    // Calculate lookup hash.
    for (i = 0; i < ResolveNameEffectiveLength; i++) {
        ch = locase_w(ResolveName[i]);
        LookupHash = LookupHash * ApiSetNamespace->HashMultiplier + ch;
    }

    // Search for hash.
    c = 0;
    EntryCount = ApiSetNamespace->Count - 1;
    do {

        HashIndex = (EntryCount + c) >> 1;

        LookupHashEntry = API_SET_TO_HASH_ENTRY(ApiSetNamespace, HashIndex);
        EntryHash = LookupHashEntry->Hash;

        if (LookupHash < EntryHash) {
            EntryCount = HashIndex - 1;
            if (c > EntryCount)
                return NULL;
            continue;
        }

        if (EntryHash == LookupHash) {

            // Hash found, query namespace entry and break.
            NamespaceEntry = API_SET_TO_NAMESPACE_ENTRY(ApiSetNamespace, LookupHashEntry);
            break;
        }

        c = HashIndex + 1;

        if (c > EntryCount)
            return NULL;

    } while (1);

    if (NamespaceEntry == NULL)
        return NULL;

    // Verify entry name.
    NamespaceEntryName = API_SET_TO_NAMESPACE_ENTRY_NAME(ApiSetNamespace, NamespaceEntry);

    if (0 == gsup.RtlCompareUnicodeStrings(ResolveName,
        ResolveNameEffectiveLength,
        NamespaceEntryName,
        (NamespaceEntry->HashNameLength >> 1),
        TRUE))
    {
        return NamespaceEntry;
    }

    return NULL;
}

BOOL name_is_apiset(
    _In_ LPCWSTR set_name
)
{
    ULONG64 schema_prefix;
    size_t name_size = wcslen(set_name) * sizeof(WCHAR);

    if (name_size <= API_SET_PREFIX_NAME_U_SIZE) {
        return FALSE;
    }

    // API- or EXT- only
    schema_prefix = API_SET_TO_UPPER_PREFIX(((ULONG64*)set_name)[0]);
    if ((schema_prefix != API_SET_PREFIX_API) && (schema_prefix != API_SET_PREFIX_EXT)) {
        return FALSE;
    }

    return TRUE;
}

/*
* resolve_apiset_name_worker
*
* Purpose:
*
* Resolve apiset library name.
*
*/
NTSTATUS resolve_apiset_name_worker(
    _In_ PVOID Namespace,
    _In_ PCUNICODE_STRING ApiSetToResolve,
    _In_opt_ PCUNICODE_STRING ApiSetParentName,
    _Inout_ PUNICODE_STRING ResolvedHostLibraryName
)
{
    NTSTATUS status = STATUS_APISET_NOT_PRESENT;
    PWCHAR buffer_ptr;
    USHORT length;
    API_SET_NAMESPACE_ENTRY_V6* resolved_entry;
    API_SET_VALUE_ENTRY_V6* host_library_entry = NULL;

    // Calculate length without everything after last hyphen including dll suffix.
    buffer_ptr = (PWCHAR)RtlOffsetToPointer(ApiSetToResolve->Buffer, ApiSetToResolve->Length);
    length = ApiSetToResolve->Length;

    do {
        if (length <= 1)
            break;
        length -= sizeof(WCHAR);
        --buffer_ptr;
    } while (*buffer_ptr != L'-');

    length = (USHORT)length >> 1;

    // Resolve apiset entry.
    resolved_entry = ApiSetpSearchForApiSet(Namespace, ApiSetToResolve->Buffer, length);

    if (resolved_entry == NULL)
        return STATUS_INVALID_PARAMETER;

    // If parent name specified and resolved entry has more than 1 value entry check it out.
    if (ApiSetParentName && resolved_entry->Count > 1) {
        host_library_entry = ApiSetpSearchForApiSetHost(resolved_entry, ApiSetParentName->Buffer, ApiSetParentName->Length >> 1, Namespace);
    }
    else {
        // If resolved apiset entry has value check it out.
        if (resolved_entry->Count > 0) {
            host_library_entry = API_SET_TO_VALUE_ENTRY(Namespace, resolved_entry, 0);
        }
    }

    // Set output parameter if host library resolved.
    if (host_library_entry) {
        if (!API_SET_EMPTY_NAMESPACE_VALUE(host_library_entry)) {
            // Host library name is not null terminated, handle that.
            buffer_ptr = (PWSTR)heap_calloc(NULL, host_library_entry->ValueLength + sizeof(UNICODE_NULL));

            if (buffer_ptr) {
                RtlCopyMemory(buffer_ptr, (PWSTR)RtlOffsetToPointer(Namespace, host_library_entry->ValueOffset), (SIZE_T)host_library_entry->ValueLength);

                ResolvedHostLibraryName->Length = (USHORT)host_library_entry->ValueLength;
                ResolvedHostLibraryName->MaximumLength = (USHORT)host_library_entry->ValueLength;
                ResolvedHostLibraryName->Buffer = buffer_ptr;
                status = STATUS_SUCCESS;
            }
        }
    }

    return status;
}

PVOID get_apiset_namespace(
    VOID
)
{
    if (gsup.UseApiSetMapFile && gsup.ApiSetMap)
    {
        //printf("using namespace from file\r\n");
        return gsup.ApiSetMap;
    }
    else {
        //printf("using namespace from PEB\r\n");
        return NtCurrentPeb()->ApiSetMap;
    }
}

LPWSTR resolve_apiset_name(
    _In_ LPCWSTR name_to_resolve,
    _In_opt_ LPCWSTR parent_library_name,
    _Out_ SIZE_T* name_length
)
{
    PAPI_SET_NAMESPACE_ARRAY_V6 apisetNamespace;
    UNICODE_STRING resolve_library, parent_library, resolved_host_library, * pus;
    NTSTATUS status;

    *name_length = 0;

    if (gsup.Initialized == FALSE) {
        return NULL;
    }

    __try {
        apisetNamespace = (PAPI_SET_NAMESPACE_ARRAY_V6)get_apiset_namespace();
        if (apisetNamespace->Version != API_SET_SCHEMA_VERSION_V6) {
            return NULL;
        }

        gsup.RtlInitUnicodeString(&resolve_library, name_to_resolve);
        if (parent_library_name) {
            gsup.RtlInitUnicodeString(&parent_library, parent_library_name);
            pus = &parent_library;
        }
        else {
            pus = NULL;
        }

        resolved_host_library.Buffer = NULL;
        resolved_host_library.Length = resolved_host_library.MaximumLength = 0;

        status = resolve_apiset_name_worker(apisetNamespace, &resolve_library, pus, &resolved_host_library);
        if (NT_SUCCESS(status)) {
            *name_length = resolved_host_library.Length;
            return resolved_host_library.Buffer;
        }
    }
    __except (ex_filter(GetExceptionCode(), GetExceptionInformation())) {
        printf("exception in resolve_apiset_name\r\n");
    }

    return NULL;
}

void base64encode(char* s, char* b64) 
{
    const char alpha64[65] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    int b0, b1, c = 0;

    while (*s) {
        b0 = *s++;
        b64[c++] = alpha64[b0 >> 2];

        if (*s) {
            b1 = *s++;
            b64[c++] = alpha64[((b0 & 3) << 4) | (b1 >> 4)];

            if (*s) {
                b0 = *s++;
                b64[c++] = alpha64[((b1 & 0x0f) << 2) | (b0 >> 6)];
                b64[c++] = alpha64[b0 & 0x3f];
            }
            else {
                b64[c++] = alpha64[(b1 & 0x0f) << 2];
                b64[c++] = '=';
            }
        }
        else {
            b64[c++] = alpha64[(b0 & 3) << 4];
            b64[c++] = '=';
            b64[c++] = '=';
        }
    }

    b64[c] = '\0';
}
