/*
*  File: core.h
*
*  Created on: Jul 17, 2024
*
*  Modified on: Aug 03, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#pragma once

#ifndef _CORE_H_
#define _CORE_H_

#define WIN32_LEAN_AND_MEAN


#include <Windows.h>
#include <WinSock2.h>
#include <WS2tcpip.h>
#include <mstcpip.h>
#include <stdio.h>
#include <strsafe.h>
#include <intsafe.h>
#include <wincrypt.h>
#include <DbgHelp.h>

#pragma warning(push)
#pragma warning(disable: 4005) //Macro redefinition due to it already being defined somewhere else in the project which causes the above warning.
#pragma warning(disable: 6320) //Exception-filter expression is the constant EXCEPTION_EXECUTE_HANDLER. This might mask exceptions that were not intended to be handled.
#include <ntstatus.h>
#pragma warning(pop)

#include "ntdll.h"
#include "apisetx.h"

#define WINDEPENDS_SERVER_MAJOR_VERSION     1
#define WINDEPENDS_SERVER_MINOR_VERSION     0
#define WINDEPENDS_SERVER_REVISION          0
#define WINDEPENDS_SERVER_BUILD             2508

#define SERVER_ERROR_SUCCESS        0
#define SERVER_ERROR_WSASTARTUP     1
#define SERVER_ERROR_SOCKETINIT     2
#define SERVER_ERROR_INVALIDIP      3
#define SERVER_ERROR_BIND           4
#define SERVER_ERROR_LISTEN         5

#define DEFAULT_APP_ADDRESS_64  0x1000000
#define DEFAULT_APP_ADDRESS_32  0x400000

#define MAX_APP_ADDRESS         0x40000000
#define PAGE_GRANULARITY        0x10000

#define WDEP_MSG_LENGTH_SMALL   1024
#define WDEP_MSG_LENGTH_MEDIUM  2048
#define WDEP_MSG_LENGTH_BIG     4096

typedef struct {
    unsigned char* module;
    wchar_t* filename;
    wchar_t* directory;
    LARGE_INTEGER file_size;
    WORD moduleMagic;

    BOOL image_64bit;
    BOOL image_fixed;
    BOOL image_dotnet;
    BOOL process_relocs;
    BOOL enable_custom_image_base;
    BOOL enable_call_stats;

    int custom_image_base;
    DWORD allocation_granularity;

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

#ifdef _CONSOLE
#define DEBUG_PRINT(format, ...) printf(format, ##__VA_ARGS__)
#define DEBUG_PRINT_LASTERROR(prefix) printf("%s failed with error 0x%lX\n", prefix, GetLastError())
#define DEBUG_PRINT_FORMATTED(format, ...) printf(format, ##__VA_ARGS__)
#define DEBUG_PRINT_SIMPLE(message) printf("%s\r\n", message)
#else
#define DEBUG_PRINT(format, ...)
#define DEBUG_PRINT_LASTERROR(prefix)
#define DEBUG_PRINT_FORMATTED(format, ...)
#define DEBUG_PRINT_SIMPLE(message)
#endif

#endif /* _CORE_H_ */
