/*
*  File: pe32plus.h
*
*  Created on: Jul 11, 2024
*
*  Modified on: Nov 30, 2024
*
*      Project: WinDepends.Core
*
*      Author:
*/

#pragma once

#ifndef _PE32PLUS_H_
#define _PE32PLUS_H_

#include "core.h"

#define PAGE_SIZE 4096

//
// Windows Dependencies Exchange Protocol/1.0
//
#define WDEP_STATUS_OK  L"WDEP/1.0 200 OK\r\n"
#define WDEP_STATUS_208 L"WDEP/1.0 208 Already resolved\r\n"
#define WDEP_STATUS_400 L"WDEP/1.0 400 Invalid parameters received\r\n"
#define WDEP_STATUS_403 L"WDEP/1.0 403 Can not read file headers\r\n"
#define WDEP_STATUS_404 L"WDEP/1.0 404 File not found or can not be accessed\r\n"
#define WDEP_STATUS_405 L"WDEP/1.0 405 Command unknown or not allowed\r\n"
#define WDEP_STATUS_415 L"WDEP/1.0 415 Invalid file headers or signatures\r\n"
#define WDEP_STATUS_500 L"WDEP/1.0 500 Can not allocate resources\r\n"
#define WDEP_STATUS_501 L"WDEP/1.0 501 Context not allocated\r\n"
#define WDEP_STATUS_600 L"WDEP/1.0 600 Exception in get_datadirs routine\r\n"
#define WDEP_STATUS_601 L"WDEP/1.0 601 Exception in get_headers routine\r\n"
#define WDEP_STATUS_602 L"WDEP/1.0 602 Exception in get_imports routine\r\n"
#define WDEP_STATUS_603 L"WDEP/1.0 603 Exception in get_exports routine\r\n"

#define get_pe_dirbase_size(hdr, index, base, size) if (hdr->NumberOfRvaAndSizes > index) {base = hdr->DataDirectory[index].VirtualAddress; size = hdr->DataDirectory[index].Size;}
#define define_3264_union(type, name) union name##__ {LPVOID uptr; type##32 *name##32; type##64 *name##64;} name;

#define process_thunks(thunk, flag, bound, offset) \
    for (int i = 0; thunk->u1.AddressOfData; ++thunk, ++bound, ++i) { \
        DWORD fhint, ordinal; ULONG64 fbound = 0; char *strfname;\
        if ((ULONG_PTR)bound > 0x10000) fbound = *bound; \
        if ((thunk->u1.Function & flag) != 0)  \
        { strfname = ""; fhint = MAXDWORD32; ordinal = IMAGE_ORDINAL64(thunk->u1.Ordinal); } \
        else \
        { \
           PIMAGE_IMPORT_BY_NAME fname; \
           if (thunk->u1.Function < offset) \
             fname = (PIMAGE_IMPORT_BY_NAME)(context->module + thunk->u1.Function); \
           else \
             fname = (PIMAGE_IMPORT_BY_NAME)(context->module + thunk->u1.Function - offset); \
           fhint = fname->Hint; strfname = (char*)&fname->Name; ordinal = MAXDWORD32; \
        } \
        if (i > 0) mlist_add(&msg_lh, L","); \
        StringCchPrintf(text, ARRAYSIZE(text), L"{\"ordinal\":%u,\"hint\":%u,\"name\":\"%S\",\"bound\":%llu}", ordinal, fhint, strfname, fbound); \
        mlist_add(&msg_lh, text); }

#define valid_image_range(range_start, range_size, image_base, image_size) \
    (((range_size) <= (image_size)) && ((range_start) >= (image_base)) && ((range_start) <= (image_base + image_size)) && ((range_start + range_size) <= (image_base + image_size)))

#define valid_image_structure(image_base, image_size, pointer, struct_type) \
    valid_image_range((pointer), sizeof(struct_type), (image_base), (image_size))


BOOL relocimage64(
    _In_ LPVOID MappedView,
    _In_ LPVOID RebaseFrom,
    _In_ PIMAGE_BASE_RELOCATION RelData,
    _In_ ULONG RelDataSize
);

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
