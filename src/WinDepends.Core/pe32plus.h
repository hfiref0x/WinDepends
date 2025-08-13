/*
*  File: pe32plus.h
*
*  Created on: Jul 11, 2024
*
*  Modified on: Aug 11, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#pragma once

#ifndef _PE32PLUS_H_
#define _PE32PLUS_H_

#include "core.h"

#define PAGE_SIZE 4096

//
// Import/Export limits
//
#define WDEP_MAX_EXPORT_FUNCTIONS 65536
#define WDEP_MAX_IMPORT_THUNKS 65536
#define WDEP_MAX_IMPORT_LIBRARIES 4096

#define WDEP_IMPORT_SANITY_SCAN_MAX_LIBS 64
#define WDEP_IMPORT_SANITY_PROBE_THUNKS 8

//
// Windows Dependencies Exchange Protocol/1.0
//
#define WDEP_STATUS_OK  L"WDEP/1.0 200 OK\r\n"
#define WDEP_STATUS_208 L"WDEP/1.0 208 Unknown data format\r\n"
#define WDEP_STATUS_400 L"WDEP/1.0 400 Invalid parameters received\r\n"
#define WDEP_STATUS_403 L"WDEP/1.0 403 Can not read file headers\r\n"
#define WDEP_STATUS_404 L"WDEP/1.0 404 File not found or can not be accessed\r\n"
#define WDEP_STATUS_405 L"WDEP/1.0 405 Command unknown or not allowed\r\n"
#define WDEP_STATUS_415 L"WDEP/1.0 415 Invalid file headers or signatures\r\n"
#define WDEP_STATUS_500 L"WDEP/1.0 500 Can not allocate resources\r\n"
#define WDEP_STATUS_501 L"WDEP/1.0 501 Context not allocated\r\n"
#define WDEP_STATUS_502 L"WDEP/1.0 502 Image buffer not allocated\r\n"
#define WDEP_STATUS_600 L"WDEP/1.0 600 Exception\r\n"

#define WSTRING_LEN(str) (sizeof(str) / sizeof(WCHAR) - 1)

#define JSON_OBJECT_BEGIN        L"{"
#define JSON_COMMA               L","
#define JSON_ARRAY_BEGIN         L"["
#define JSON_ARRAY_END           L"]"
#define JSON_COMMA_LEN           WSTRING_LEN(JSON_COMMA)
#define JSON_OBJECT_BEGIN_LEN    WSTRING_LEN(JSON_OBJECT_BEGIN)
#define JSON_ARRAY_BEGIN_LEN     WSTRING_LEN(JSON_ARRAY_BEGIN)
#define JSON_ARRAY_END_LEN       WSTRING_LEN(JSON_ARRAY_END)

#define JSON_RESPONSE_BEGIN      WDEP_STATUS_OK L"{"
#define JSON_RESPONSE_BEGIN_LEN  WSTRING_LEN(JSON_RESPONSE_BEGIN)
#define JSON_RESPONSE_END        L"}\r\n"
#define JSON_RESPONSE_END_LEN    WSTRING_LEN(JSON_RESPONSE_END)

#define JSON_DEBUG_DIRECTORY_START L",\"DebugDirectory\":["
#define JSON_DEBUG_DIRECTORY_START_LEN WSTRING_LEN(JSON_DEBUG_DIRECTORY_START)

#define get_pe_dirbase_size(hdr, index, base, size) if (hdr->NumberOfRvaAndSizes > index) {base = hdr->DataDirectory[index].VirtualAddress; size = hdr->DataDirectory[index].Size;}
#define define_3264_union(type, name) union name##__ {LPVOID uptr; type##32 *name##32; type##64 *name##64;} name;

#define valid_image_range(range_start, range_size, image_base, image_size) \
    (((range_size) <= (image_size)) && \
     ((range_start) >= (image_base)) && \
     (((range_start) - (image_base)) <= ((image_size) - (range_size))))

#define valid_image_structure(image_base, image_size, pointer, struct_type) \
    valid_image_range((pointer), sizeof(struct_type), (image_base), (image_size))


LPBYTE pe32open(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
);

BOOL get_headers(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
);

BOOL get_datadirs(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
);

BOOL get_imports(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
);

BOOL get_exports(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
);

BOOL pe32close(
    PBYTE module
);

#endif /* _PE32PLUS_H_ */
