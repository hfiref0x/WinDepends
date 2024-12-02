/*
*  File: cmd.h
*
*  Created on: Aug 30, 2024
*
*  Modified on: Nov 30, 2024
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#pragma once

#ifndef _CMD_H_
#define _CMD_H_

typedef enum {
    ce_open = 0,
    ce_close,
    ce_imports,
    ce_exports,
    ce_headers,
    ce_datadirs,
    ce_shutdown,
    ce_exit,
    ce_knowndlls,
    ce_apisetresolve,
    ce_apisetmapsrc,
    ce_callstats,
    ce_servstats,
    ce_unknown = 0xffff
} cmd_entry_type;

cmd_entry_type get_command_entry(
    _In_ LPCWSTR cmd);

void cmd_query_knowndlls_list(
    _In_ SOCKET s,
    _In_opt_ LPCWSTR params
);

void cmd_unknown_command(
    _In_ SOCKET s
);

void cmd_resolve_apiset_name(
    _In_ SOCKET s,
    _In_ LPCWSTR api_set_name,
    _In_ pmodule_ctx context
);

void cmd_set_apisetmap_src(
    _In_ SOCKET s,
    _In_opt_ LPCWSTR params
);

void cmd_callstats(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
);

void cmd_servstats(
    _In_ SOCKET s,
    _In_ long long threadsCount,
    _In_ long long socketsCreated,
    _In_ long long socketsClosed
);

pmodule_ctx cmd_open(
    _In_ SOCKET s,
    _In_ LPCWSTR params
);

void cmd_close(
    _In_ pmodule_ctx module
);

#endif /* _CMD_H_ */
