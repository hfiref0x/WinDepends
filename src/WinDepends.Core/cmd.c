/*
*  File: cmd.c
*
*  Created on: Aug 30, 2024
*
*  Modified on: Nov 26, 2024
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

cmd_entry cmds[] = {
    {L"open", 4, ce_open },
    {L"close", 5, ce_close },
    {L"imports", 7, ce_imports },
    {L"exports", 7, ce_exports },
    {L"headers", 7, ce_headers },
    {L"datadirs", 8, ce_datadirs },
    {L"shutdown", 8, ce_shutdown },
    {L"exit", 4, ce_exit },
    {L"knowndlls", 9, ce_knowndlls },
    {L"apisetresolve", 13, ce_apisetresolve },
    {L"apisetmapsrc", 12, ce_apisetmapsrc },
    {L"usereloc", 8, ce_usereloc },
    {L"callstats", 9, ce_callstats },
    {L"servstats", 9, ce_servstats }
};

void cmd_init()
{
    //Future use
    ;
}

cmd_entry_type get_command_entry(
    _In_ LPCWSTR cmd
)
{
    for (size_t i = 0; i < ARRAYSIZE(cmds); i++) {
        if (wcsncmp(cmds[i].cmd, cmd, cmds[i].length) == 0)
            return cmds[i].type;
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
    sendstring_plaintext(s, WDEP_STATUS_405);
}

/*
* cmd_callstats
*
* Purpose:
*
* Global server call stats.
*
*/
void cmd_callstats(
    _In_ SOCKET s,
    _In_opt_ LPCWSTR params
)
{
    WCHAR buffer[512];

    if (params != NULL) {

        if (wcsncmp(params, L"enable", 6) == 0)
        {
            gsup.EnableCallStats = TRUE;
        }
        else if (wcsncmp(params, L"disable", 7) == 0)
        {
            gsup.EnableCallStats = FALSE;
        }
        else if (wcsncmp(params, L"reset", 5) == 0)
        {
            InterlockedExchange64((PLONG64)&gsup.CallStats.totalBytesSent, 0);
            InterlockedExchange64((PLONG64)&gsup.CallStats.totalSendCalls, 0);
            InterlockedExchange64((PLONG64)&gsup.CallStats.totalTimeSpent, 0);
        }
    }

    if (gsup.EnableCallStats) {

        StringCchPrintf(buffer, ARRAYSIZE(buffer),
            L"%s{\"stats\":{"
            L"\"totalBytesSent\":%llu,"
            L"\"totalSendCalls\":%llu,"
            L"\"totalTimeSpent\":%llu}}\r\n",
            WDEP_STATUS_OK,
            gsup.CallStats.totalBytesSent,
            gsup.CallStats.totalSendCalls,
            gsup.CallStats.totalTimeSpent);

        sendstring_plaintext_no_track(s, buffer);
    }
    else {
        sendstring_plaintext_no_track(s, WDEP_STATUS_OK);
    }
}

/*
* cmd_servstats
*
* Purpose:
*
* Global server stats.
*
*/
void cmd_servstats(
    _In_ SOCKET s,
    _In_ long long threadsCount,
    _In_ long long socketsCreated,
    _In_ long long socketsClosed
)
{
    WCHAR buffer[512];

    StringCchPrintf(buffer, ARRAYSIZE(buffer),
        L"%s{\"servstats\":{"
        L"\"threadsCount\":%I64d,"
        L"\"socketsCreated\":%I64d,"
        L"\"socketsClosed\":%I64d}}\r\n",
        WDEP_STATUS_OK,
        threadsCount,
        socketsCreated,
        socketsClosed);

    sendstring_plaintext_no_track(s, buffer);
}

/*
* cmd_usereloc
*
* Purpose:
*
* Enable/disable relocation.
* 
* usereloc 1234567
* usereloc <NULL>
*
*/
void cmd_usereloc(
    _In_ SOCKET s,
    _In_opt_ LPCWSTR params
)
{
    if (params == NULL) {
        printf("use relocation turned off\r\n");
        gsup.UseRelocation = FALSE;
        gsup.MinAppAddress = DEFAULT_APP_ADDRESS;
    }
    else {
        gsup.MinAppAddress = strtoul_w((PWCHAR)params);
        gsup.UseRelocation = TRUE;
        printf("use relocation turned on, min app address 0x%lX\r\n", gsup.MinAppAddress);
    }

    sendstring_plaintext(s, WDEP_STATUS_OK);
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
    if (params == NULL || !gsup.Initialized) {
        sendstring_plaintext(s, WDEP_STATUS_500);
        return;
    }

    gsup.UseApiSetMapFile = (wcsncmp(params, L"file", 4) == 0);
    sendstring_plaintext(s, WDEP_STATUS_OK);
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
    BOOL is_wow64;
    LIST_ENTRY msg_lh;
    PSUP_PATH_ELEMENT_ENTRY dlls_head, dll_entry;
    PWSTR dlls_path;
    PWCH buffer;
    SIZE_T sz, i;

    if (params == NULL || !gsup.Initialized) {
        sendstring_plaintext(s, WDEP_STATUS_500);
        return;
    }

    InitializeListHead(&msg_lh);

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

    if (sz == 0) {
        sendstring_plaintext(s, WDEP_STATUS_500);
        return;
    }

    buffer = (PWCH)heap_calloc(NULL, sz);
    if (buffer) {

        StringCchPrintf(buffer,
            sz / sizeof(WCHAR),
            L"%ws{\"knowndlls\":{\"path\":\"%ws\", \"entries\":[",
            WDEP_STATUS_OK,
            dlls_path);

        mlist_add(&msg_lh, buffer);

        i = 0;
        dll_entry = dlls_head->Next;
        while (dll_entry != NULL) {

            if (i > 0)
                mlist_add(&msg_lh, L",");

            StringCchPrintf(buffer, sz / sizeof(WCHAR),
                L"\"%ws\"",
                dll_entry->Element);
            
            mlist_add(&msg_lh, buffer);

            dll_entry = dll_entry->Next;
            i += 1;
        }

        mlist_add(&msg_lh, L"]}}\r\n");
        mlist_traverse(&msg_lh, mlist_send, s);

        heap_free(NULL, buffer);
    }
    else {
        sendstring_plaintext(s, WDEP_STATUS_500);
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
    _In_ LPCWSTR api_set_name
)
{
    LPWSTR resolved_name = NULL;
    PWCH buffer;
    SIZE_T name_length = 0, sz;

    if (name_is_apiset(api_set_name)) {
        resolved_name = resolve_apiset_name(api_set_name, NULL, &name_length);
        if (resolved_name && name_length) {

            sz = (MAX_PATH * sizeof(WCHAR)) + name_length + sizeof(UNICODE_NULL);
            buffer = (PWCH)heap_calloc(NULL, sz);
            if (buffer) {
                StringCchPrintf(buffer, sz / sizeof(WCHAR), L"%ws{\"filename\":{\"path\":\"%ws\"}}\r\n", WDEP_STATUS_OK, resolved_name);
                sendstring_plaintext(s, buffer);
                heap_free(NULL, buffer);
            }
            heap_free(NULL, resolved_name);
        }
        else {
            sendstring_plaintext(s, WDEP_STATUS_500);
        }
    }
    else {
        sendstring_plaintext(s, WDEP_STATUS_208);
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
    _In_ LPCWSTR filename
)
{
    SIZE_T sz;
    pmodule_ctx context;

    context = (pmodule_ctx)heap_calloc(NULL, sizeof(module_ctx));
    if (context) {

        sz = (1 + wcslen(filename)) * sizeof(WCHAR);
        context->filename = (PWCH)heap_calloc(NULL, sz);
        if (context->filename) {
            wcscpy_s(context->filename, sz / sizeof(WCHAR), filename);
        }

        context->directory = (PWCH)heap_calloc(NULL, sz);
        if (context->directory) {
            _filepath_w(filename, context->directory);
        }

        context->module = pe32open(s, context, gsup.UseRelocation, gsup.MinAppAddress);
       
    }

    return context;
}
