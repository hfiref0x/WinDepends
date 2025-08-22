/*
*  File: cmd.c
*
*  Created on: Aug 30, 2024
*
*  Modified on: Aug 14, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#include "core.h"

typedef struct {
    wchar_t* cmd;
    size_t length;
    cmd_entry_type type;
} cmd_entry, * pcmd_entry;

// Command line array sorted for binary search.
static const cmd_entry cmds[] = {
    {L"apisetmapsrc", 12, ce_apisetmapsrc },
    {L"apisetnsinfo", 12, ce_apisetnsinfo },
    {L"apisetresolve", 13, ce_apisetresolve },
    {L"callstats", 9, ce_callstats },
    {L"close", 5, ce_close },
    {L"datadirs", 8, ce_datadirs },
    {L"exit", 4, ce_exit },
    {L"exports", 7, ce_exports },
    {L"headers", 7, ce_headers },
    {L"imports", 7, ce_imports },
    {L"knowndlls", 9, ce_knowndlls },
    {L"open", 4, ce_open },
    {L"shutdown", 8, ce_shutdown }
};

/*
* get_command_entry
*
* Purpose:
*
* Returns the corresponding cmd_entry_type if found.
*
*/
cmd_entry_type get_command_entry(
    _In_ LPCWSTR cmd
)
{
    int left = 0, right = ARRAYSIZE(cmds) - 1;
    while (left <= right) {
        int mid = left + (right - left) / 2;
        int cmp = wcsncmp(cmds[mid].cmd, cmd, cmds[mid].length);
        if (cmp == 0) {
            return cmds[mid].type;
        }
        if (cmp < 0) {
            left = mid + 1;
        }
        else {
            right = mid - 1;
        }
    }
    return ce_unknown;
}

/*
* cmd_unknown_command
*
* Purpose:
*
* Unknown command handler.
*
*/
void cmd_unknown_command(
    _In_ SOCKET s
)
{
    sendstring_plaintext_no_track(s, WDEP_STATUS_405);
}

/*
* cmd_callstats
*
* Purpose:
*
* Server call stats.
*
*/
void cmd_callstats(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
)
{
    WCHAR buffer[512];
    DWORD64 totalBytesSent = 0, totalSendCalls = 0, totalTimeSpent = 0;

    if (context == NULL) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_501);
        return;
    }

    __try {

        if (context->enable_call_stats) {

            totalBytesSent = context->total_bytes_sent;
            totalSendCalls = context->total_send_calls;
            totalTimeSpent = context->total_time_spent;

        }

    }
    __finally
    {
        StringCchPrintf(buffer, ARRAYSIZE(buffer),
            L"%s{\"totalBytesSent\":%llu,"
            L"\"totalSendCalls\":%llu,"
            L"\"totalTimeSpent\":%llu}\r\n",
            WDEP_STATUS_OK,
            totalBytesSent,
            totalSendCalls,
            totalTimeSpent);

        sendstring_plaintext_no_track(s, buffer);
    }
}

/*
* cmd_query_knowndlls_list
*
* Purpose:
*
* Return KnownDlls list.
*
*/
void cmd_query_knowndlls_list(
    _In_ SOCKET s,
    _In_opt_ LPCWSTR params
)
{
    BOOL is_wow64, send_ok, response_ok;
    LIST_ENTRY msg_lh;
    PSUP_PATH_ELEMENT_ENTRY dlls_head, dll_entry;
    PWSTR dlls_path;
    PWCH buffer;
    SIZE_T sz, i;
    HRESULT hr;
    PWSTR endPtr;
    SIZE_T remaining, len;
    WCHAR escapedName[1024];
    PWSTR escapedPath;
    SIZE_T pathLen, escPathLen, escPathAlloc;

    if (params == NULL || !gsup.Initialized) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        return;
    }

    is_wow64 = (wcsncmp(params, L"32", 2) == 0);

    if (is_wow64) {
        dlls_head = &gsup.KnownDlls32Head;
        dlls_path = gsup.KnownDlls32Path;
        sz = MAX_PATH + gsup.KnownDlls32NameCbMax + gsup.KnownDlls32PathCbMax;
    }
    else {
        dlls_head = &gsup.KnownDllsHead;
        dlls_path = gsup.KnownDllsPath;
        sz = MAX_PATH + gsup.KnownDllsNameCbMax + gsup.KnownDllsPathCbMax;
    }

    if (sz == 0 || dlls_path == NULL) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        return;
    }

    pathLen = wcslen(dlls_path);
    escPathAlloc = (pathLen * 6) + 1;
    escapedPath = (PWSTR)heap_calloc(NULL, escPathAlloc * sizeof(WCHAR));
    if (escapedPath == NULL) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        return;
    }

    if (!json_escape_string(dlls_path, escapedPath, escPathAlloc, &escPathLen)) {
        heap_free(NULL, escapedPath);
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        return;
    }

    buffer = (PWCH)heap_calloc(NULL, sz);
    if (buffer == NULL) {
        heap_free(NULL, escapedPath);
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        return;
    }

    InitializeListHead(&msg_lh);
    response_ok = FALSE;

    hr = StringCchPrintfEx(buffer,
        sz / sizeof(WCHAR),
        &endPtr,
        (size_t*)&remaining,
        0,
        L"%ws{\"path\":\"%ws\", \"entries\":[",
        WDEP_STATUS_OK,
        escapedPath);

    if (SUCCEEDED(hr)) {
        response_ok = mlist_add(&msg_lh, buffer, endPtr - buffer);
    }

    if (response_ok) {
        i = 0;
        dll_entry = dlls_head->Next;

        while (dll_entry && response_ok) {

            if (i > 0) {
                if (!mlist_add(&msg_lh, JSON_COMMA, JSON_COMMA_LEN)) {
                    response_ok = FALSE;
                    break;
                }
            }

            if (!json_escape_string(dll_entry->Element, escapedName, ARRAYSIZE(escapedName), &len)) {
                escapedName[0] = 0;
                len = 0;
            }

            hr = StringCchPrintfEx(
                buffer,
                sz / sizeof(WCHAR),
                &endPtr,
                (size_t*)&remaining,
                0,
                L"\"%ws\"",
                escapedName
            );

            if (SUCCEEDED(hr)) {
                if (!mlist_add(&msg_lh, buffer, endPtr - buffer)) {
                    response_ok = FALSE;
                    break;
                }
            }
            else {
                response_ok = FALSE;
                break;
            }

            dll_entry = dll_entry->Next;
            ++i;
        }

        if (response_ok) {
            if (!mlist_add(&msg_lh, L"]}\r\n", WSTRING_LEN(L"]}\r\n"))) {
                response_ok = FALSE;
            }
        }
    }

    if (!response_ok) {
        mlist_traverse(&msg_lh, mlist_free, s, NULL);
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
    }
    else {
        send_ok = mlist_traverse(&msg_lh, mlist_send, s, NULL);
        if (!send_ok) {
            sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        }
    }

    heap_free(NULL, buffer);
    heap_free(NULL, escapedPath);
}

/*
* cmd_apisetnamespace_info
*
* Purpose:
*
* Retrieve apiset namespace information.
*
*/
void cmd_apisetnamespace_info(
    _In_ SOCKET s
)
{
    BOOL bUnknown = FALSE;
    ULONG version = 0, count = 0;
    PAPI_SET_NAMESPACE ApiSetNamespace;
    WCHAR buffer[200];

    union {
        PAPI_SET_NAMESPACE_V6 v6;
        PAPI_SET_NAMESPACE_ARRAY_V4 v4;
        PAPI_SET_NAMESPACE_ARRAY_V2 v2;
        PVOID Data;
    } ApiSet;

    if (gsup.Initialized == FALSE) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        return;
    }

    __try {

        ApiSetNamespace = (PAPI_SET_NAMESPACE)gsup.ApiSetMap;
        ApiSet.Data = gsup.ApiSetMap;
        version = ApiSetNamespace->Version;

        switch (ApiSetNamespace->Version) {

        case API_SET_SCHEMA_VERSION_V2:
            count = ApiSet.v2->Count;
            break;

        case API_SET_SCHEMA_VERSION_V4:
            count = ApiSet.v4->Count;
            break;

        case API_SET_SCHEMA_VERSION_V6:
            count = ApiSet.v6->Count;
            break;

        default:
            bUnknown = TRUE;
            __leave;
        }
    }
    __finally {
        buffer[0] = 0;
        if (bUnknown) {
            sendstring_plaintext_no_track(s, WDEP_STATUS_208);
        }
        else {
            StringCchPrintf(buffer, ARRAYSIZE(buffer),
                L"%ws{\"version\":%u, \"count\":%lu}\r\n",
                WDEP_STATUS_OK, 
                version, count);
            sendstring_plaintext_no_track(s, buffer);
        }
    }
}

/*
* cmd_set_apisetmap_src
*
* Purpose:
*
* Change apiset namespace source.
*
*/
void cmd_set_apisetmap_src(
    _In_ SOCKET s,
    _In_opt_ LPCWSTR params
)
{
    ULONG param_length;
    SIZE_T sz;
    PWCH file_name = NULL;
    PVOID api_set_namespace;

    if (!gsup.Initialized) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        return;
    }

    if (params == NULL) {
        gsup.UseApiSetMapFile = FALSE;
        gsup.ApiSetMap = NtCurrentPeb()->ApiSetMap;
        sendstring_plaintext_no_track(s, WDEP_STATUS_OK);
    }
    else {
        sz = (wcslen(params) + 1) * sizeof(WCHAR);
        file_name = (PWCH)heap_calloc(NULL, sz);
        if (file_name != NULL) {

            param_length = 0;
            if (get_params_option(
                params,
                L"file",
                TRUE,
                file_name,
                (ULONG)sz,
                &param_length))
            {
                api_set_namespace = load_apiset_namespace(file_name);
                if (api_set_namespace) {
                    gsup.UseApiSetMapFile = TRUE;
                    gsup.ApiSetMap = api_set_namespace;
                }
            }

            heap_free(NULL, file_name);
            sendstring_plaintext_no_track(s, WDEP_STATUS_OK);
        }
        else {
            sendstring_plaintext_no_track(s, WDEP_STATUS_500);
        }
    }
}

/*
* cmd_resolve_apiset_name
*
* Purpose:
*
* Resolve apiset library name.
*
*/
void cmd_resolve_apiset_name(
    _In_ SOCKET s,
    _In_ LPCWSTR api_set_name,
    _In_ pmodule_ctx context
)
{
    LPWSTR resolved_name = NULL;
    PWCH buffer;
    SIZE_T name_length = 0, sz;

    resolved_name = resolve_apiset_name(api_set_name, NULL, &name_length);
    if (resolved_name && name_length) {

        sz = (MAX_PATH * sizeof(WCHAR)) + name_length + sizeof(UNICODE_NULL);
        buffer = (PWCH)heap_calloc(NULL, sz);
        if (buffer) {
            if (SUCCEEDED(StringCchPrintf(
                buffer, sz / sizeof(WCHAR), 
                L"%ws{\"path\":\"%ws\"}\r\n", 
                WDEP_STATUS_OK, resolved_name)))
            {
                sendstring_plaintext(s, buffer, context);
            }
            heap_free(NULL, buffer);
        }
        heap_free(NULL, resolved_name);
    }
    else {
        sendstring_plaintext_no_track(s, WDEP_STATUS_500);
    }
}

/*
* cmd_close
*
* Purpose:
*
* Closes currently opened module and frees associated context.
* 
*/
void cmd_close(
    _In_ pmodule_ctx context
)
{
    if (context) {

        pe32close(context->module);

        if (context->filename) {
            heap_free(NULL, context->filename);
        }

        if (context->directory) {
            heap_free(NULL, context->directory);
        }

        heap_free(NULL, context);
    }
}

/*
* cmd_open
*
* Purpose:
*
* Open module and allocate associated context.
*
*/
pmodule_ctx cmd_open(
    _In_ SOCKET s,
    _In_ LPCWSTR params
)
{
    BOOL bResult = FALSE;
    ULONG param_length;
    SIZE_T sz;
    PWCH file_name = NULL;
    pmodule_ctx context;
    WCHAR option_buffer[100];

    do {

        context = (pmodule_ctx)heap_calloc(NULL, sizeof(module_ctx));
        if (context == NULL) {
            break;
        }

        sz = (wcslen(params) + 1) * sizeof(WCHAR);
        file_name = (PWCH)heap_calloc(NULL, sz);
        if (file_name == NULL) {
            break;
        }

        context->allocation_granularity = gsup.dwAllocationGranularity;

        param_length = 0;
        RtlSecureZeroMemory(&option_buffer, sizeof(option_buffer));

        //
        // Read process_relocs command.
        //
        if (get_params_option(
            params,
            L"process_relocs",
            FALSE,
            NULL,
            0,
            &param_length))
        {
            context->process_relocs = TRUE;
        }

        //
        // Read enable_custom_image_base command and set value.
        //
        if (get_params_option(
            params,
            L"custom_image_base",
            TRUE,
            option_buffer,
            ARRAYSIZE(option_buffer),
            &param_length))
        {
            context->process_relocs = TRUE;
            context->enable_custom_image_base = TRUE;
            context->custom_image_base = strtoul_w(option_buffer);
        }

        //
        // Read usestats command.
        //
        param_length = 0;
        context->enable_call_stats = get_params_option(
            params,
            L"use_stats",
            FALSE,
            NULL,
            0,
            &param_length);

        //
        // pe32open take place here.
        //
        param_length = 0;
        if (get_params_option(
            params,
            L"file",
            TRUE,
            file_name,
            (ULONG)sz,
            &param_length))
        {
            sz = (1 + wcslen(file_name)) * sizeof(WCHAR);
            context->filename = (PWCH)heap_calloc(NULL, sz);
            if (context->filename) {
                wcscpy_s(context->filename, sz / sizeof(WCHAR), file_name);
            }
            else {
                break;
            }

            context->directory = (PWCH)heap_calloc(NULL, sz);
            if (context->directory) {
                _filepath_w(file_name, context->directory);
            }
            else {
                // Clean up filename if directory alloc fails
                heap_free(NULL, context->filename);
                context->filename = NULL;
                break;
            }

            context->module = pe32open(s, context);
            bResult = context->module != NULL;
            if (bResult) {
                // Remember common fields.
                context->dos_hdr = (PIMAGE_DOS_HEADER)context->module;
                context->nt_file_hdr = (PIMAGE_FILE_HEADER)(context->module + sizeof(DWORD) + context->dos_hdr->e_lfanew);
            }
        }

    } while (FALSE);

    if (!bResult) {
        if (context) {
            if (context->filename) {
                heap_free(NULL, context->filename);
            }
            if (context->directory) {
                heap_free(NULL, context->directory);
            }
            heap_free(NULL, context);
            context = NULL;
        }
    }

    if (file_name != NULL) {
        heap_free(NULL, file_name);
    }

    return context;
}
