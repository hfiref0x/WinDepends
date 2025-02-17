/*
*  File: core.h
*
*  Created on: Jul 17, 2024
*
*  Modified on: Nov 30, 2024
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#pragma once

#ifndef _CORE_H_
#define _CORE_H_

#define WIN32_LEAN_AND_MEAN
#ifndef _DEBUG
#define _NO_VERBOSE
#endif

#include <Windows.h>
#include <WinSock2.h>
#include <WS2tcpip.h>
#include <stdio.h>
#include <strsafe.h>
#include <wincrypt.h>
#include <DbgHelp.h>

#pragma warning(push)
#pragma warning(disable: 4005)
#include <ntstatus.h>
#pragma warning(pop)

#include "ntdll.h"
#include "apisetx.h"

#define WINDEPENDS_SERVER_MAJOR_VERSION 1
#define WINDEPENDS_SERVER_MINOR_VERSION 0

#define RELOC_DEFAULT_APP_ADDRESS 0x1000000
#define RELOC_MAX_APP_ADDRESS 0x40000000
#define RELOC_PAGE_GRANULARITY 0x10000

typedef struct {
    unsigned char* module;
    wchar_t* filename;
    wchar_t* directory;
    LARGE_INTEGER file_size;
    WORD moduleMagic;

    BOOL use_reloc;
    BOOL enable_call_stats;

    int min_app_address;

    LARGE_INTEGER start_count;
    DWORD64 total_bytes_sent;
    DWORD64 total_send_calls;
    DWORD64 total_time_spent;

} module_ctx, * pmodule_ctx;

#include "pe32plus.h"
#include "util.h"
#include "cmd.h"
#include "mlist.h"

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "Crypt32.lib")

#ifdef _NO_VERBOSE
#define printf
#define wprintf
#endif

#endif /* _CORE_H_ */
