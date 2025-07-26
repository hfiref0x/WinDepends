/*
*  File: pe32plus.c
*
*  Created on: Jul 11, 2024
*
*  Modified on: Jun 23, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#include "core.h"
#include "pe32plus.h"
#include "vsverinfo.h"

DWORD PAGE_ALIGN(DWORD p)
{
    DWORD r = p % PAGE_SIZE;
    if (r == 0)
        return p;

    return p + PAGE_SIZE - r;
}

DWORD ALIGN_UP(DWORD p, DWORD a)
{
    DWORD r = p % a;
    if (r == 0)
        return p;

    return p + a - r;
}

DWORD ALIGN_DOWN(DWORD p, DWORD a)
{
    return p - (p % a);
}

/*
* relocimage
*
* Purpose:
*
* Send exception information to the client.
*
*/
BOOL relocimage(
    _In_ LPVOID MappedView,
    _In_ LPVOID RebaseFrom,
    _In_ PIMAGE_BASE_RELOCATION RelData,
    _In_ ULONG RelDataSize)
{
    LPWORD      entry;
    LONG64      delta = (ULONG_PTR)MappedView - (ULONG_PTR)RebaseFrom;
    PIMAGE_BASE_RELOCATION next_block, RelData0 = RelData;
    ULONG       p = 0, block_size;
    LONG64      rel, * ptr;

    if (RelDataSize < sizeof(IMAGE_BASE_RELOCATION))
        return FALSE;

    /* validate */
    while (p < RelDataSize)
    {
        block_size = RelData->SizeOfBlock;
        if ((block_size < sizeof(IMAGE_BASE_RELOCATION)) ||
            (p + block_size > RelDataSize) ||
            (block_size % sizeof(WORD) != 0))
        {
            return FALSE;
        }

        next_block = (PIMAGE_BASE_RELOCATION)((LPBYTE)RelData + block_size);
        for (entry = (LPWORD)(RelData + 1); entry < (LPWORD)next_block; ++entry)
        {
            switch (*entry >> 12)
            {
            case IMAGE_REL_BASED_HIGHLOW:
            case IMAGE_REL_BASED_DIR64:
            case IMAGE_REL_BASED_ABSOLUTE:
                break;
            default:
                /* unsupported reloc found */
                return FALSE;
                break;
            }
        }
        p += block_size;
        RelData = next_block;
    }

    if (p != RelDataSize)
        return FALSE;

    /* do relocate */
    p = 0;
    RelData = RelData0;
    while (p < RelDataSize)
    {
        block_size = RelData->SizeOfBlock;
        next_block = (PIMAGE_BASE_RELOCATION)((LPBYTE)RelData + block_size);
        for (entry = (LPWORD)(RelData + 1); entry < (LPWORD)next_block; ++entry)
        {
            switch (*entry >> 12)
            {
            case IMAGE_REL_BASED_HIGHLOW:
                ptr = (LONG64*)((LPBYTE)MappedView + RelData->VirtualAddress + (*entry & 0x0fff));
                rel = *(PULONG)ptr; // need zero extend value here (movzx)
                rel += delta;
                *(PLONG)ptr = (LONG)rel;
                break;
            case IMAGE_REL_BASED_DIR64:
                ptr = (LONG64*)((LPBYTE)MappedView + RelData->VirtualAddress + (*entry & 0x0fff));
                rel = *ptr;
                rel += delta;
                *ptr = rel;
                break;
            case IMAGE_REL_BASED_ABSOLUTE:
                // no relocation needed
                break;
            default:
                /* unsupported reloc found */
                return FALSE;
                break;
            }
        }
        p += block_size;
        RelData = next_block;
    }

    return TRUE;
}

BOOL get_datadirs(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
)
{
    LIST_ENTRY          msg_lh;
    PIMAGE_DOS_HEADER   dos_hdr;
    PIMAGE_FILE_HEADER  nt_file_hdr;
    BOOL                status = FALSE;
    WCHAR               text[WDEP_MSG_LENGTH_SMALL];
    DWORD               dir_limit, c;
    HRESULT             hr;
    SIZE_T              remaining, len;
    PWSTR               endPtr;

    define_3264_union(IMAGE_OPTIONAL_HEADER, opt_file_hdr);

    if (context == NULL) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_501);
        return FALSE;
    }

    __try {

        InitializeListHead(&msg_lh);

        if (!context->module)
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_404);
            return FALSE;
        }

        dos_hdr = (PIMAGE_DOS_HEADER)context->module;
        nt_file_hdr = (PIMAGE_FILE_HEADER)(context->module + sizeof(DWORD) + dos_hdr->e_lfanew);
        opt_file_hdr.opt_file_hdr32 = (IMAGE_OPTIONAL_HEADER32*)((PBYTE)nt_file_hdr + sizeof(IMAGE_FILE_HEADER));

        mlist_add(&msg_lh, WDEP_STATUS_OK JSON_ARRAY_BEGIN, JSON_ARRAY_BEGIN_LEN);

        switch (opt_file_hdr.opt_file_hdr32->Magic)
        {
        case IMAGE_NT_OPTIONAL_HDR32_MAGIC:

            if (opt_file_hdr.opt_file_hdr32->NumberOfRvaAndSizes > 256)
                dir_limit = 256;
            else
                dir_limit = opt_file_hdr.opt_file_hdr32->NumberOfRvaAndSizes;

            for (c = 0; c < dir_limit; ++c)
            {
                if (c > 0)
                    mlist_add(&msg_lh, JSON_COMMA, JSON_COMMA_LEN);

                hr = StringCchPrintfEx(text, WDEP_MSG_LENGTH_SMALL,
                    &endPtr,
                    (size_t*)&remaining,
                    0,
                    L"{\"vaddress\":%u,\"size\":%u}",
                    opt_file_hdr.opt_file_hdr32->DataDirectory[c].VirtualAddress,
                    opt_file_hdr.opt_file_hdr32->DataDirectory[c].Size
                );

                if (SUCCEEDED(hr)) {
                    len = endPtr - text;
                    mlist_add(&msg_lh, text, len);
                }
            }
            break;

        case IMAGE_NT_OPTIONAL_HDR64_MAGIC:

            if (opt_file_hdr.opt_file_hdr64->NumberOfRvaAndSizes > 256)
                dir_limit = 256;
            else
                dir_limit = opt_file_hdr.opt_file_hdr64->NumberOfRvaAndSizes;

            for (c = 0; c < dir_limit; ++c)
            {
                if (c > 0)
                    mlist_add(&msg_lh, JSON_COMMA, JSON_COMMA_LEN);

                hr = StringCchPrintfEx(text, WDEP_MSG_LENGTH_SMALL,
                    &endPtr,
                    (size_t*)&remaining,
                    0,
                    L"{\"vaddress\":%u,\"size\":%u}",
                    opt_file_hdr.opt_file_hdr64->DataDirectory[c].VirtualAddress,
                    opt_file_hdr.opt_file_hdr64->DataDirectory[c].Size
                );

                if (SUCCEEDED(hr)) {
                    len = endPtr - text;
                    mlist_add(&msg_lh, text, len);
                }
            }

            break;
        }

        mlist_add(&msg_lh, L"]\r\n", WSTRING_LEN(L"]\r\n"));
        mlist_traverse(&msg_lh, mlist_send, s, context);

        status = TRUE;
    }
    __except (ex_filter_dbg(context->filename, GetExceptionCode(), GetExceptionInformation()))
    {
        printf("exception in get_datadirs\r\n");
        mlist_traverse(&msg_lh, mlist_free, s, NULL);
        report_exception_to_client(s, ex_datadirs, GetExceptionCode());
    }

    return status;
}

BOOL get_headers(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
)
{
    LIST_ENTRY          msg_lh;
    PIMAGE_DOS_HEADER   dos_hdr;
    PIMAGE_FILE_HEADER  nt_file_hdr;
    BOOL                status = FALSE;
    WCHAR               *manifest = NULL;
    ULONG               i;
    DWORD               dir_base = 0, dir_size = 0, dllchars_ex = 0, image_size = 0;
    DWORD               hdr_chars, hdr_subsystem;
    HRESULT             hr;
    SIZE_T              remaining, len, manifest_len;
    PWSTR               endPtr;

#define WDEP_TEXT_BUFFER_SIZE 16384
    static __declspec(thread) WCHAR header_buffer[WDEP_TEXT_BUFFER_SIZE];

    PIMAGE_DEBUG_DIRECTORY  pdbg = NULL;

    define_3264_union(IMAGE_OPTIONAL_HEADER, opt_file_hdr);

    if (context == NULL) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_501);
        return FALSE;
    }

    __try
    {
        InitializeListHead(&msg_lh);

        if (!context->module)
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_404);
            return FALSE;
        }

        dos_hdr = (PIMAGE_DOS_HEADER)context->module;
        nt_file_hdr = (PIMAGE_FILE_HEADER)(context->module + sizeof(DWORD) + dos_hdr->e_lfanew);
        opt_file_hdr.opt_file_hdr32 = (IMAGE_OPTIONAL_HEADER32*)((PBYTE)nt_file_hdr + sizeof(IMAGE_FILE_HEADER));
        
        mlist_add(&msg_lh, JSON_RESPONSE_BEGIN, JSON_RESPONSE_BEGIN_LEN);

        hdr_chars = nt_file_hdr->Characteristics;

        header_buffer[0] = 0;
        hr = StringCchPrintfEx(header_buffer, WDEP_TEXT_BUFFER_SIZE,
            &endPtr, (size_t*)&remaining, 0,
            L"\"ImageFileHeader\":{"
            L"\"Machine\":%u,"
            L"\"NumberOfSections\":%u,"
            L"\"TimeDateStamp\":%u,"
            L"\"PointerToSymbolTable\":%u,"
            L"\"NumberOfSymbols\":%u,"
            L"\"SizeOfOptionalHeader\":%u,"
            L"\"Characteristics\":%u},",
            nt_file_hdr->Machine,
            nt_file_hdr->NumberOfSections,
            nt_file_hdr->TimeDateStamp,
            nt_file_hdr->PointerToSymbolTable,
            nt_file_hdr->NumberOfSymbols,
            nt_file_hdr->SizeOfOptionalHeader,
            nt_file_hdr->Characteristics
        );

        if (SUCCEEDED(hr)) {
            len = endPtr - header_buffer;
            mlist_add(&msg_lh, header_buffer, len);
        }

        switch (opt_file_hdr.opt_file_hdr32->Magic)
        {
        case IMAGE_NT_OPTIONAL_HDR32_MAGIC:
            image_size = opt_file_hdr.opt_file_hdr32->SizeOfImage;
            hdr_subsystem = opt_file_hdr.opt_file_hdr32->Subsystem;

            get_pe_dirbase_size(opt_file_hdr.opt_file_hdr32, IMAGE_DIRECTORY_ENTRY_DEBUG, dir_base, dir_size);

            hr = StringCchPrintfEx(header_buffer, WDEP_TEXT_BUFFER_SIZE,
                &endPtr, (size_t*)&remaining, 0,
                L"\"ImageOptionalHeader\":{"
                L"\"Magic\":%u,"
                L"\"MajorLinkerVersion\":%u,"
                L"\"MinorLinkerVersion\":%u,"
                L"\"SizeOfCode\":%u,"
                L"\"SizeOfInitializedData\":%u,"
                L"\"SizeOfUninitializedData\":%u,"
                L"\"AddressOfEntryPoint\":%u,"
                L"\"BaseOfCode\":%u,"
                L"\"BaseOfData\":%u,"
                L"\"ImageBase\":%u,"
                L"\"SectionAlignment\":%u,"
                L"\"FileAlignment\":%u,"
                L"\"MajorOperatingSystemVersion\":%u,"
                L"\"MinorOperatingSystemVersion\":%u,"
                L"\"MajorImageVersion\":%u,"
                L"\"MinorImageVersion\":%u,"
                L"\"MajorSubsystemVersion\":%u,"
                L"\"MinorSubsystemVersion\":%u,"
                L"\"Win32VersionValue\":%u,"
                L"\"SizeOfImage\":%u,"
                L"\"SizeOfHeaders\":%u,"
                L"\"CheckSum\":%u,"
                L"\"Subsystem\":%u,"
                L"\"DllCharacteristics\":%u,"
                L"\"SizeOfStackReserve\":%u,"
                L"\"SizeOfStackCommit\":%u,"
                L"\"SizeOfHeapReserve\":%u,"
                L"\"SizeOfHeapCommit\":%u,"
                L"\"LoaderFlags\":%u,"
                L"\"NumberOfRvaAndSizes\":%u}",
                opt_file_hdr.opt_file_hdr32->Magic,
                opt_file_hdr.opt_file_hdr32->MajorLinkerVersion,
                opt_file_hdr.opt_file_hdr32->MinorLinkerVersion,
                opt_file_hdr.opt_file_hdr32->SizeOfCode,
                opt_file_hdr.opt_file_hdr32->SizeOfInitializedData,
                opt_file_hdr.opt_file_hdr32->SizeOfUninitializedData,
                opt_file_hdr.opt_file_hdr32->AddressOfEntryPoint,
                opt_file_hdr.opt_file_hdr32->BaseOfCode,
                opt_file_hdr.opt_file_hdr32->BaseOfData,
                opt_file_hdr.opt_file_hdr32->ImageBase,
                opt_file_hdr.opt_file_hdr32->SectionAlignment,
                opt_file_hdr.opt_file_hdr32->FileAlignment,
                opt_file_hdr.opt_file_hdr32->MajorOperatingSystemVersion,
                opt_file_hdr.opt_file_hdr32->MinorOperatingSystemVersion,
                opt_file_hdr.opt_file_hdr32->MajorImageVersion,
                opt_file_hdr.opt_file_hdr32->MinorImageVersion,
                opt_file_hdr.opt_file_hdr32->MajorSubsystemVersion,
                opt_file_hdr.opt_file_hdr32->MinorSubsystemVersion,
                opt_file_hdr.opt_file_hdr32->Win32VersionValue,
                opt_file_hdr.opt_file_hdr32->SizeOfImage,
                opt_file_hdr.opt_file_hdr32->SizeOfHeaders,
                opt_file_hdr.opt_file_hdr32->CheckSum,
                opt_file_hdr.opt_file_hdr32->Subsystem,
                opt_file_hdr.opt_file_hdr32->DllCharacteristics,
                opt_file_hdr.opt_file_hdr32->SizeOfStackReserve,
                opt_file_hdr.opt_file_hdr32->SizeOfStackCommit,
                opt_file_hdr.opt_file_hdr32->SizeOfHeapReserve,
                opt_file_hdr.opt_file_hdr32->SizeOfHeapCommit,
                opt_file_hdr.opt_file_hdr32->LoaderFlags,
                opt_file_hdr.opt_file_hdr32->NumberOfRvaAndSizes
            );

            if (SUCCEEDED(hr)) {
                len = endPtr - header_buffer;
                mlist_add(&msg_lh, header_buffer, len);
            }
            break;

        case IMAGE_NT_OPTIONAL_HDR64_MAGIC:
            image_size = opt_file_hdr.opt_file_hdr64->SizeOfImage;
            hdr_subsystem = opt_file_hdr.opt_file_hdr64->Subsystem;

            get_pe_dirbase_size(opt_file_hdr.opt_file_hdr64, IMAGE_DIRECTORY_ENTRY_DEBUG, dir_base, dir_size);

            hr = StringCchPrintfEx(header_buffer, WDEP_TEXT_BUFFER_SIZE,
                &endPtr, (size_t*)&remaining, 0,
                L"\"ImageOptionalHeader\":{"
                L"\"Magic\":%u,"
                L"\"MajorLinkerVersion\":%u,"
                L"\"MinorLinkerVersion\":%u,"
                L"\"SizeOfCode\":%u,"
                L"\"SizeOfInitializedData\":%u,"
                L"\"SizeOfUninitializedData\":%u,"
                L"\"AddressOfEntryPoint\":%u,"
                L"\"BaseOfCode\":%u,"
                L"\"ImageBase\":%llu,"
                L"\"SectionAlignment\":%u,"
                L"\"FileAlignment\":%u,"
                L"\"MajorOperatingSystemVersion\":%u,"
                L"\"MinorOperatingSystemVersion\":%u,"
                L"\"MajorImageVersion\":%u,"
                L"\"MinorImageVersion\":%u,"
                L"\"MajorSubsystemVersion\":%u,"
                L"\"MinorSubsystemVersion\":%u,"
                L"\"Win32VersionValue\":%u,"
                L"\"SizeOfImage\":%u,"
                L"\"SizeOfHeaders\":%u,"
                L"\"CheckSum\":%u,"
                L"\"Subsystem\":%u,"
                L"\"DllCharacteristics\":%u,"
                L"\"SizeOfStackReserve\":%llu,"
                L"\"SizeOfStackCommit\":%llu,"
                L"\"SizeOfHeapReserve\":%llu,"
                L"\"SizeOfHeapCommit\":%llu,"
                L"\"LoaderFlags\":%u,"
                L"\"NumberOfRvaAndSizes\":%u}",
                opt_file_hdr.opt_file_hdr64->Magic,
                opt_file_hdr.opt_file_hdr64->MajorLinkerVersion,
                opt_file_hdr.opt_file_hdr64->MinorLinkerVersion,
                opt_file_hdr.opt_file_hdr64->SizeOfCode,
                opt_file_hdr.opt_file_hdr64->SizeOfInitializedData,
                opt_file_hdr.opt_file_hdr64->SizeOfUninitializedData,
                opt_file_hdr.opt_file_hdr64->AddressOfEntryPoint,
                opt_file_hdr.opt_file_hdr64->BaseOfCode,
                opt_file_hdr.opt_file_hdr64->ImageBase,
                opt_file_hdr.opt_file_hdr64->SectionAlignment,
                opt_file_hdr.opt_file_hdr64->FileAlignment,
                opt_file_hdr.opt_file_hdr64->MajorOperatingSystemVersion,
                opt_file_hdr.opt_file_hdr64->MinorOperatingSystemVersion,
                opt_file_hdr.opt_file_hdr64->MajorImageVersion,
                opt_file_hdr.opt_file_hdr64->MinorImageVersion,
                opt_file_hdr.opt_file_hdr64->MajorSubsystemVersion,
                opt_file_hdr.opt_file_hdr64->MinorSubsystemVersion,
                opt_file_hdr.opt_file_hdr64->Win32VersionValue,
                opt_file_hdr.opt_file_hdr64->SizeOfImage,
                opt_file_hdr.opt_file_hdr64->SizeOfHeaders,
                opt_file_hdr.opt_file_hdr64->CheckSum,
                opt_file_hdr.opt_file_hdr64->Subsystem,
                opt_file_hdr.opt_file_hdr64->DllCharacteristics,
                opt_file_hdr.opt_file_hdr64->SizeOfStackReserve,
                opt_file_hdr.opt_file_hdr64->SizeOfStackCommit,
                opt_file_hdr.opt_file_hdr64->SizeOfHeapReserve,
                opt_file_hdr.opt_file_hdr64->SizeOfHeapCommit,
                opt_file_hdr.opt_file_hdr64->LoaderFlags,
                opt_file_hdr.opt_file_hdr64->NumberOfRvaAndSizes
            );

            if (SUCCEEDED(hr)) {
                len = endPtr - header_buffer;
                mlist_add(&msg_lh, header_buffer, len);
            }
            break;

        default:
            __leave;
            break;
        }

        pdbg = (PIMAGE_DEBUG_DIRECTORY)(context->module + dir_base);
        if (valid_image_range((ULONG_PTR)pdbg, sizeof(IMAGE_DEBUG_DIRECTORY), (ULONG_PTR)context->module, image_size))
        {
            mlist_add(&msg_lh, JSON_DEBUG_DIRECTORY_START, JSON_DEBUG_DIRECTORY_START_LEN);

            for (i = 0; dir_size >= sizeof(IMAGE_DEBUG_DIRECTORY); dir_size -= sizeof(IMAGE_DEBUG_DIRECTORY), ++pdbg, i++)
            {
                if ((pdbg->Type == IMAGE_DEBUG_TYPE_EX_DLLCHARACTERISTICS) &&
                    (pdbg->AddressOfRawData < image_size - sizeof(DWORD)) &&
                    dllchars_ex == 0)
                {
                    dllchars_ex = *(PDWORD)(context->module + pdbg->AddressOfRawData);
                }

                if (i > 0)
                    mlist_add(&msg_lh, JSON_COMMA, JSON_COMMA_LEN);

                hr = StringCchPrintfEx(header_buffer, WDEP_TEXT_BUFFER_SIZE,
                    &endPtr, (size_t*)&remaining, 0,
                    L"{\"Characteristics\":%u,"
                    L"\"TimeDateStamp\":%u,"
                    L"\"MajorVersion\":%u,"
                    L"\"MinorVersion\":%u,"
                    L"\"Type\":%u,"
                    L"\"SizeOfData\":%u,"
                    L"\"AddressOfRawData\":%u,"
                    L"\"PointerToRawData\":%u}",
                    pdbg->Characteristics,
                    pdbg->TimeDateStamp,
                    pdbg->MajorVersion,
                    pdbg->MinorVersion,
                    pdbg->Type,
                    pdbg->SizeOfData,
                    pdbg->AddressOfRawData,
                    pdbg->PointerToRawData
                );

                if (SUCCEEDED(hr)) {
                    len = endPtr - header_buffer;
                    mlist_add(&msg_lh, header_buffer, len);
                }
            }
        }

        mlist_add(&msg_lh, JSON_ARRAY_END, JSON_ARRAY_END_LEN);

        VS_FIXEDFILEINFO* vinfo;
        vinfo = PEImageEnumVersionFields((HMODULE)context->module, NULL, NULL, (LPVOID)&header_buffer);
        if (vinfo)
        {
            hr = StringCchPrintfEx(header_buffer, WDEP_TEXT_BUFFER_SIZE,
                &endPtr, (size_t*)&remaining, 0,
                L",\"Version\":{"
                L"\"dwFileVersionMS\":%u,"
                L"\"dwFileVersionLS\":%u,"
                L"\"dwProductVersionMS\":%u,"
                L"\"dwProductVersionLS\":%u}",
                vinfo->dwFileVersionMS,
                vinfo->dwFileVersionLS,
                vinfo->dwProductVersionMS,
                vinfo->dwProductVersionLS
            );

            if (SUCCEEDED(hr)) {
                len = endPtr - header_buffer;
                mlist_add(&msg_lh, header_buffer, len);
            }
        }

        hr = StringCchPrintfEx(header_buffer, WDEP_TEXT_BUFFER_SIZE,
            &endPtr, (size_t*)&remaining, 0,
            L",\"dllcharex\":%u",
            dllchars_ex);

        if (SUCCEEDED(hr)) {
            len = endPtr - header_buffer;
            mlist_add(&msg_lh, header_buffer, len);
        }

        // Query create process manifest, skip dlls and native images
        if ((hdr_chars & IMAGE_FILE_DLL) == 0 &&
            hdr_subsystem != IMAGE_SUBSYSTEM_NATIVE)
        {
            manifest = get_manifest((HMODULE)context->module);
            if (manifest)
            {
                if (SUCCEEDED(StringCchLength(manifest, STRSAFE_MAX_CCH, (size_t*)&manifest_len))) {
                    mlist_add(&msg_lh, L",\"manifest\":\"", WSTRING_LEN(L",\"manifest\":\""));
                    mlist_add(&msg_lh, manifest, manifest_len);
                    mlist_add(&msg_lh, L"\"", WSTRING_LEN(L"\""));
                }
                heap_free(NULL, manifest);
            }
        }

        mlist_add(&msg_lh, L"}\r\n", WSTRING_LEN(L"}\r\n"));
        mlist_traverse(&msg_lh, mlist_send, s, context);
    }
    __except (ex_filter_dbg(context->filename, GetExceptionCode(), GetExceptionInformation()))
    {
        printf("exception in get_headers\r\n");
        mlist_traverse(&msg_lh, mlist_free, s, NULL);
        report_exception_to_client(s, ex_headers, GetExceptionCode());
    }

    return status;
}

BOOL get_exports(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
)
{
    LIST_ENTRY          msg_lh;
    PIMAGE_DOS_HEADER   dos_hdr;
    PIMAGE_FILE_HEADER  nt_file_hdr;
    DWORD               dir_base = 0, dir_size = 0, i, *ptrs, *names, p, hint, ctr = 0, ImageSize = 0, need_comma = 0;
    WORD                *name_ordinals;
    BOOL                status = FALSE, names_valid;
    char                *fname, *forwarder;
    HRESULT             hr;
    PWSTR               endPtr;
    SIZE_T              remaining, len;
    WCHAR               text_buffer[WDEP_MSG_LENGTH_BIG];

    PIMAGE_EXPORT_DIRECTORY ExportTable;

    define_3264_union(IMAGE_OPTIONAL_HEADER, opt_file_hdr);

    if (context == NULL) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_501);
        return FALSE;
    }

    __try
    {
        InitializeListHead(&msg_lh);

        //*(PBYTE)(NULL) = 0;

        if (!context->module)
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_404);
            return FALSE;
        }

        dos_hdr = (PIMAGE_DOS_HEADER)context->module;
        nt_file_hdr = (PIMAGE_FILE_HEADER)(context->module + sizeof(DWORD) + dos_hdr->e_lfanew);
        opt_file_hdr.opt_file_hdr32 = (IMAGE_OPTIONAL_HEADER32*)((PBYTE)nt_file_hdr + sizeof(IMAGE_FILE_HEADER));

        switch (opt_file_hdr.opt_file_hdr32->Magic)
        {
        case IMAGE_NT_OPTIONAL_HDR32_MAGIC:
            ImageSize = opt_file_hdr.opt_file_hdr32->SizeOfImage;
            get_pe_dirbase_size(opt_file_hdr.opt_file_hdr32, IMAGE_DIRECTORY_ENTRY_EXPORT, dir_base, dir_size);
            break;

        case IMAGE_NT_OPTIONAL_HDR64_MAGIC:
            ImageSize = opt_file_hdr.opt_file_hdr64->SizeOfImage;
            get_pe_dirbase_size(opt_file_hdr.opt_file_hdr64, IMAGE_DIRECTORY_ENTRY_EXPORT, dir_base, dir_size);
            break;

        default:
            __leave;
            break;
        }

        if ((dir_base > 0) && (dir_base < ImageSize))
        {
            mlist_add(&msg_lh, JSON_RESPONSE_BEGIN, JSON_RESPONSE_BEGIN_LEN);

            ExportTable = (PIMAGE_EXPORT_DIRECTORY)(context->module + dir_base);
            ptrs = (DWORD*)(context->module + ExportTable->AddressOfFunctions);
            names = (DWORD*)(context->module + ExportTable->AddressOfNames);
            name_ordinals = (WORD*)(context->module + ExportTable->AddressOfNameOrdinals);

            names_valid = valid_image_range((ULONG_PTR)name_ordinals, ExportTable->NumberOfNames * sizeof(DWORD), (ULONG_PTR)context->module, ImageSize);

            hr = StringCchPrintfEx(text_buffer, ARRAYSIZE(text_buffer),
                &endPtr, (size_t*)&remaining, 0,
                L"\"library\":{\"timestamp\":%u,\"entries\":%u,\"named\":%u,\"base\":%u,\"functions\":[",
                ExportTable->TimeDateStamp,
                ExportTable->NumberOfFunctions,
                ExportTable->NumberOfNames,
                ExportTable->Base);

            if (SUCCEEDED(hr)) {
                len = endPtr - text_buffer;
                mlist_add(&msg_lh, text_buffer, len);
            }

            for (i = 0; i < ExportTable->NumberOfFunctions; ++i)
            {
                if (!valid_image_range((ULONG_PTR)&ptrs[i], sizeof(DWORD), (ULONG_PTR)context->module, ImageSize))
                    break;

                if (!ptrs[i])
                    continue;

                ++ctr;
                fname = NULL;
                forwarder = "";
                hint = 0;

                if (names_valid)
                {
                    for (p = 0; p < ExportTable->NumberOfNames; ++p)
                    {
                        if (name_ordinals[p] == i)
                        {
                            hint = p;
                            fname = (char*)(context->module + names[p]);
                        }
                    }
                }

                if (fname == NULL)
                {
                    hint = MAXDWORD32;
                    fname = "";
                }

                if ((ptrs[i] >= dir_base) && (ptrs[i] < (dir_base + dir_size)))
                {
                    forwarder = (char*)context->module + ptrs[i];
                }

                if (need_comma > 0)
                    mlist_add(&msg_lh, JSON_COMMA, JSON_COMMA_LEN);

                hr = StringCchPrintfEx(text_buffer, ARRAYSIZE(text_buffer),
                    &endPtr, (size_t*)&remaining, 0,
                    L"{\"ordinal\":%u,"
                    L"\"hint\":%u,"
                    L"\"name\":\"%S\","
                    L"\"pointer\":%u,"
                    L"\"forward\":\"%S\"}",
                    ExportTable->Base + i, hint, fname, ptrs[i], forwarder);

                if (SUCCEEDED(hr)) {
                    len = endPtr - text_buffer;
                    mlist_add(&msg_lh, text_buffer, len);
                }

                need_comma = 1;
            }
            mlist_add(&msg_lh, L"]}}", WSTRING_LEN(L"]}"));
        }
        mlist_add(&msg_lh, L"\r\n", WSTRING_LEN(L"\r\n"));
        mlist_traverse(&msg_lh, mlist_send, s, context);
    }
    __except (ex_filter_dbg(context->filename, GetExceptionCode(), GetExceptionInformation()))
    {
        printf("exception in get_exports\r\n");
        mlist_traverse(&msg_lh, mlist_free, s, NULL);
        report_exception_to_client(s, ex_exports, GetExceptionCode());
    }

    return status;
}

static void process_thunks64(
    PBYTE                   module,
    PIMAGE_THUNK_DATA64     thunk,
    PULONG64                bound_table,
    PLIST_ENTRY             msg_lh,
    BOOL                    rvabased,
    DWORD_PTR               image_base,
    DWORD_PTR               image_size,
    BOOL                    has_bound_imports
)
{
    DWORD       fhint = 0, ordinal = 0;
    ULONG64     fbound = 0;
    char        *strfname = NULL;
    HRESULT     hr;
    PWSTR       endPtr;
    SIZE_T      remaining, len;
    WCHAR       msg_text[WDEP_MSG_LENGTH_BIG];

    PIMAGE_IMPORT_BY_NAME fname = NULL;

    for (int i = 0; thunk->u1.AddressOfData; ++thunk, ++i)
    {
        if (has_bound_imports) {
            fbound = *bound_table;
            bound_table++;
        }
        else {
            fbound = 0;
        }

        if ((thunk->u1.Function & IMAGE_ORDINAL_FLAG64) != 0)
        {
            strfname = "";
            fhint = MAXDWORD32;
            ordinal = IMAGE_ORDINAL64(thunk->u1.Ordinal);
        }
        else
        {
            if (rvabased) {
                fname = (PIMAGE_IMPORT_BY_NAME)(module + thunk->u1.Function);
            }
            else {
                fname = (PIMAGE_IMPORT_BY_NAME)((DWORD_PTR)module + ((DWORD_PTR)thunk->u1.Function - image_base));
            }

            if (valid_image_structure((DWORD_PTR)module, image_size, (DWORD_PTR)fname, IMAGE_IMPORT_BY_NAME)) {
                strfname = (char*)&fname->Name;
                fhint = fname->Hint;
                ordinal = MAXDWORD32;
            }
            else {
                strfname = (char*)"Error resolving function name";
                fhint = MAXDWORD32;
                ordinal = MAXDWORD32;
            }
        }

        if (i > 0)
            mlist_add(msg_lh, JSON_COMMA, JSON_COMMA_LEN);

        hr = StringCchPrintfEx(msg_text, ARRAYSIZE(msg_text),
            &endPtr, (size_t*)&remaining, 0,
            L"{\"ordinal\":%u,\"hint\":%u,\"name\":\"%S\",\"bound\":%llu}",
            ordinal, fhint, strfname, fbound);

        if (SUCCEEDED(hr)) {
            len = endPtr - msg_text;
            mlist_add(msg_lh, msg_text, len);
        }
    }
}

static void process_thunks32(
    PBYTE                   module,
    PIMAGE_THUNK_DATA32     thunk,
    PULONG32                bound_table,
    PLIST_ENTRY             msg_lh,
    BOOL                    rvabased,
    DWORD_PTR               image_base,
    DWORD_PTR               image_size,
    BOOL                    has_bound_imports
)
{
    DWORD       fhint = 0, ordinal = 0;
    ULONG64     fbound = 0;
    char        *strfname = NULL;
    HRESULT     hr;
    PWSTR       endPtr;
    SIZE_T      remaining, len;
    WCHAR       msg_text[WDEP_MSG_LENGTH_BIG];

    PIMAGE_IMPORT_BY_NAME fname = NULL;

    for (int i = 0; thunk->u1.AddressOfData; ++thunk, ++i)
    {
        if (has_bound_imports) {
            fbound = *bound_table;
            bound_table++;
        }
        else {
            fbound = 0;
        }

        if ((thunk->u1.Function & IMAGE_ORDINAL_FLAG32) != 0)
        {
            strfname = "";
            fhint = MAXDWORD32;
            ordinal = IMAGE_ORDINAL32(thunk->u1.Ordinal);
        }
        else
        {
            if (rvabased) {
                fname = (PIMAGE_IMPORT_BY_NAME)(module + thunk->u1.Function);
            }
            else {
                fname = (PIMAGE_IMPORT_BY_NAME)((DWORD_PTR)module + ((DWORD_PTR)thunk->u1.Function - image_base));
            }
            if (valid_image_structure((DWORD_PTR)module, image_size, (DWORD_PTR)fname, IMAGE_IMPORT_BY_NAME)) {
                strfname = (char*)&fname->Name;
                fhint = fname->Hint;
                ordinal = MAXDWORD32;
            }
            else {
                strfname = (char*)"Error resolving function name";
                fhint = MAXDWORD32;
                ordinal = MAXDWORD32;
            }
        }

        if (i > 0)
            mlist_add(msg_lh, JSON_COMMA, JSON_COMMA_LEN);

        hr = StringCchPrintfEx(msg_text, ARRAYSIZE(msg_text),
            &endPtr, (size_t*)&remaining, 0,
            L"{\"ordinal\":%u,\"hint\":%u,\"name\":\"%S\",\"bound\":%llu}",
            ordinal, fhint, strfname, fbound);

        if (SUCCEEDED(hr)) {
            len = endPtr - msg_text;
            mlist_add(msg_lh, msg_text, len);
        }
    }
}

BOOL get_imports(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
)
{
    LIST_ENTRY                  msg_lh;
    LIST_ENTRY                  std_lib_lh;
    LIST_ENTRY                  delay_lib_lh;

    PIMAGE_FILE_HEADER          nt_file_hdr;
    DWORD                       si_dir_base = 0, di_dir_base = 0, dirsize = 0, c, import_exception = 0;
    DWORD_PTR                   ImageBase = 0, ImageSize = 0, SizeOfHeaders = 0,
        IModuleName, INameTable, delta, boundIAT;
    BOOL                        status = FALSE, importPresent = FALSE, has_bound_imports = FALSE;
    PIMAGE_IMPORT_DESCRIPTOR    SImportTable;
    PIMAGE_DELAYLOAD_DESCRIPTOR DImportTable;
    HRESULT                     hr;
    PWSTR                       endPtr;
    SIZE_T                      remaining, len;

    ULONG                       except_code_std = 0, except_code_delay = 0;

    WCHAR                       msg_text[WDEP_MSG_LENGTH_BIG];

    define_3264_union(IMAGE_THUNK_DATA, thunk_data);
    define_3264_union(ULONG, bound_table);
    define_3264_union(IMAGE_OPTIONAL_HEADER, opt_file_header);

    if (context == NULL) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_501);
        return FALSE;
    }

    __try
    {
        InitializeListHead(&msg_lh);
        InitializeListHead(&std_lib_lh);
        InitializeListHead(&delay_lib_lh);

        //*(PBYTE)(NULL) = 0;

        if (!context->module)
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_404);
            return FALSE;
        }

        nt_file_hdr = (PIMAGE_FILE_HEADER)(context->module + sizeof(DWORD) + ((PIMAGE_DOS_HEADER)context->module)->e_lfanew);
        opt_file_header.uptr = (PBYTE)nt_file_hdr + sizeof(IMAGE_FILE_HEADER);
        switch (opt_file_header.opt_file_header32->Magic)
        {
        case IMAGE_NT_OPTIONAL_HDR32_MAGIC:
            ImageBase = opt_file_header.opt_file_header32->ImageBase;
            ImageSize = opt_file_header.opt_file_header32->SizeOfImage;
            SizeOfHeaders = opt_file_header.opt_file_header32->SizeOfHeaders;
            get_pe_dirbase_size(opt_file_header.opt_file_header32, IMAGE_DIRECTORY_ENTRY_IMPORT, si_dir_base, dirsize);
            get_pe_dirbase_size(opt_file_header.opt_file_header32, IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT, di_dir_base, dirsize);
            break;

        case IMAGE_NT_OPTIONAL_HDR64_MAGIC:
            ImageBase = (DWORD_PTR)opt_file_header.opt_file_header64->ImageBase;
            ImageSize = opt_file_header.opt_file_header64->SizeOfImage;
            SizeOfHeaders = opt_file_header.opt_file_header64->SizeOfHeaders;
            get_pe_dirbase_size(opt_file_header.opt_file_header64, IMAGE_DIRECTORY_ENTRY_IMPORT, si_dir_base, dirsize);
            get_pe_dirbase_size(opt_file_header.opt_file_header64, IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT, di_dir_base, dirsize);
            break;

        default:
            __leave;
            break;
        }

        // Build list for usual import.

        __try {

            if ((si_dir_base > 0) && (si_dir_base < ImageSize))
            {
                SImportTable = (PIMAGE_IMPORT_DESCRIPTOR)(context->module + si_dir_base);
                for (c = 0; SImportTable->Name && SImportTable->FirstThunk; ++SImportTable, ++c)
                {
                    importPresent = TRUE;
                    if (c > 0)
                        mlist_add(&std_lib_lh, JSON_COMMA, JSON_COMMA_LEN);

                    hr = StringCchPrintfEx(msg_text, ARRAYSIZE(msg_text),
                        &endPtr, (size_t*)&remaining, 0,
                        L"{\"name\":\"%S\",\"functions\":[",
                        (char*)context->module + SImportTable->Name);

                    if (SUCCEEDED(hr)) {
                        len = endPtr - msg_text;
                        mlist_add(&std_lib_lh, msg_text, len);
                    }

                    bound_table.uptr = NULL;
                    if ((SImportTable->OriginalFirstThunk < SizeOfHeaders) || (SImportTable->OriginalFirstThunk > ImageSize))
                    {
                        thunk_data.uptr = context->module + SImportTable->FirstThunk;
                    }
                    else
                    {
                        thunk_data.uptr = context->module + SImportTable->OriginalFirstThunk;
                    }
                    if (SImportTable->TimeDateStamp) {
                        bound_table.uptr = context->module + SImportTable->FirstThunk;
                    }

                    has_bound_imports = bound_table.uptr != NULL;

                    if (context->image_64bit)
                        process_thunks64(context->module, thunk_data.thunk_data64,
                            bound_table.bound_table64, &std_lib_lh, TRUE, ImageBase, ImageSize, has_bound_imports);
                    else
                        process_thunks32(context->module, thunk_data.thunk_data32,
                            bound_table.bound_table32, &std_lib_lh, TRUE, ImageBase, ImageSize, has_bound_imports);

                    mlist_add(&std_lib_lh, L"]}", WSTRING_LEN(L"]}"));
                }
            }
        }
        __except (ex_filter_dbg(context->filename, GetExceptionCode(), GetExceptionInformation()))
        {
            printf("exception in get_imports (standard)\r\n");
            mlist_traverse(&std_lib_lh, mlist_free, s, NULL);
            InitializeListHead(&std_lib_lh);
            import_exception |= 1;
            except_code_std = GetExceptionCode();
        }

        // Build list for delay-load import.
        __try
        {
            if ((di_dir_base > 0) && (di_dir_base < ImageSize))
            {
                DImportTable = (PIMAGE_DELAYLOAD_DESCRIPTOR)(context->module + di_dir_base);

                for (c = 0; DImportTable->DllNameRVA; ++DImportTable, ++c)
                {
                    if (c > 0)
                        mlist_add(&delay_lib_lh, JSON_COMMA, JSON_COMMA_LEN);

                    IModuleName = DImportTable->DllNameRVA;
                    INameTable = DImportTable->ImportNameTableRVA;
                    delta = (DWORD_PTR)context->module;

                    if (DImportTable->Attributes.RvaBased) {
                        IModuleName += delta;
                        INameTable += delta;
                    }
                    else {
                        IModuleName = DImportTable->DllNameRVA - (DWORD_PTR)ImageBase;
                        INameTable = DImportTable->ImportNameTableRVA - (DWORD_PTR)ImageBase;
                        IModuleName += delta;
                        INameTable += delta;
                    }

                    hr = StringCchPrintfEx(msg_text, ARRAYSIZE(msg_text),
                        &endPtr, (size_t*)&remaining, 0,
                        L"{\"name\":\"%S\",\"functions\":[",
                        (char*)IModuleName);

                    if (SUCCEEDED(hr)) {
                        len = endPtr - msg_text;
                        mlist_add(&delay_lib_lh, msg_text, len);
                    }

                    bound_table.uptr = NULL;
                    if (DImportTable->TimeDateStamp != 0) {
                        boundIAT = DImportTable->BoundImportAddressTableRVA;
                        if (DImportTable->Attributes.RvaBased)
                            bound_table.uptr = context->module + boundIAT;
                        else {
                            boundIAT = boundIAT - (DWORD_PTR)ImageBase;
                            bound_table.uptr = context->module + boundIAT;
                        }
                    }

                    thunk_data.uptr = (LPVOID)INameTable;
                    has_bound_imports = (bound_table.uptr != NULL);

                    if (context->image_64bit)
                        process_thunks64(context->module, thunk_data.thunk_data64,
                            bound_table.bound_table64, &delay_lib_lh, DImportTable->Attributes.RvaBased, ImageBase, ImageSize, has_bound_imports);
                    else
                        process_thunks32(context->module, thunk_data.thunk_data32,
                            bound_table.bound_table32, &delay_lib_lh, DImportTable->Attributes.RvaBased, ImageBase, ImageSize, has_bound_imports);

                    mlist_add(&delay_lib_lh, L"]}", WSTRING_LEN(L"]}"));
                }
            }
        }
        __except (ex_filter_dbg(context->filename, GetExceptionCode(), GetExceptionInformation()))
        {
            printf("exception in get_imports (delay)\r\n");
            mlist_traverse(&delay_lib_lh, mlist_free, s, NULL);
            InitializeListHead(&delay_lib_lh);
            import_exception |= 2;
            except_code_delay = GetExceptionCode();
        }

        //
        // Build combined output.
        //
        mlist_add(&msg_lh, WDEP_STATUS_OK L"{\"exception\":", WSTRING_LEN(WDEP_STATUS_OK L"{\"exception\":"));

        StringCchPrintf(msg_text, ARRAYSIZE(msg_text), L"%lu", import_exception);
        mlist_add(&msg_lh, msg_text, wcslen(msg_text));
        mlist_add(&msg_lh, L",\"exception_code_std\":", WSTRING_LEN(L",\"exception_code_std\":"));
        StringCchPrintf(msg_text, ARRAYSIZE(msg_text), L"%lu", except_code_std);
        mlist_add(&msg_lh, msg_text, wcslen(msg_text));
        mlist_add(&msg_lh, L",\"exception_code_delay\":", WSTRING_LEN(L",\"exception_code_delay\":"));
        StringCchPrintf(msg_text, ARRAYSIZE(msg_text), L"%lu", except_code_delay);
        mlist_add(&msg_lh, msg_text, wcslen(msg_text));

        mlist_add(&msg_lh, L",\"libraries\":[", WSTRING_LEN(L",\"libraries\":["));
        if (!IsListEmpty(&std_lib_lh))
            mlist_append_to_main(&std_lib_lh, &msg_lh);
        mlist_add(&msg_lh, L"],\"libraries_delay\":[", WSTRING_LEN(L"],\"libraries_delay\":["));
        if (!IsListEmpty(&delay_lib_lh))
            mlist_append_to_main(&delay_lib_lh, &msg_lh);
        mlist_add(&msg_lh, L"]}\r\n", WSTRING_LEN(L"]}\r\n"));

        mlist_traverse(&msg_lh, mlist_send, s, context);
    }
    __except (ex_filter_dbg(context->filename, GetExceptionCode(), GetExceptionInformation()))
    {
        printf("exception in get_imports\r\n");
        mlist_traverse(&msg_lh, mlist_free, s, NULL);
        report_exception_to_client(s, ex_imports, GetExceptionCode());
    }

    return status;
}

LPBYTE pe32open(
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
)
{
    HANDLE              hf = INVALID_HANDLE_VALUE;
    IMAGE_DOS_HEADER    dos_hdr = { 0 };
    IMAGE_FILE_HEADER   nt_file_hdr = { 0 };
    DWORD               iobytes = 0, dwSignature = 0, szOptAndSections,
        vsize, psize, tsize, status = 0, dwRealChecksum = 0, dwLastError = 0, dir_base = 0, dir_size = 0;
    OVERLAPPED          ovl;
    PBYTE               module = NULL;
    INT64               c, image_base;

    BOOL                image_fixed = TRUE, image_dotnet = FALSE;

    PIMAGE_SECTION_HEADER       sections = NULL;
    BY_HANDLE_FILE_INFORMATION  fileinfo = { 0 };
    WCHAR                       text[WDEP_MSG_LENGTH_BIG];

    define_3264_union(IMAGE_OPTIONAL_HEADER, opt_file_hdr);

    if (context == NULL || context->filename == NULL) {
        sendstring_plaintext_no_track(s, WDEP_STATUS_501);
        return FALSE;
    }

    opt_file_hdr.uptr = NULL;

    __try
    {
        context->image_fixed = TRUE;
        context->image_64bit = FALSE;

        // Open input file
        hf = CreateFile(context->filename, GENERIC_READ | SYNCHRONIZE, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
        if (hf == INVALID_HANDLE_VALUE)
        {
            DEBUG_PRINT_LASTERROR("pe32open: CreateFile");
            sendstring_plaintext_no_track(s, WDEP_STATUS_404);
            __leave;
        }

        // Get file information
        if (!GetFileInformationByHandle(hf, &fileinfo))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_404);
            __leave;
        }

        context->file_size.LowPart = fileinfo.nFileSizeLow;
        context->file_size.HighPart = fileinfo.nFileSizeHigh;

        // Read DOS header
        if (!ReadFile(hf, &dos_hdr, sizeof(dos_hdr), &iobytes, NULL))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_403);
            __leave;
        }

        // Validate DOS header
        if ((iobytes != sizeof(dos_hdr)) || (dos_hdr.e_magic != IMAGE_DOS_SIGNATURE)
            || (dos_hdr.e_lfanew <= 0) || ((DWORD)dos_hdr.e_lfanew >= fileinfo.nFileSizeLow))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_415);
            __leave;
        }

        RtlZeroMemory(&ovl, sizeof(ovl));
        ovl.Offset = dos_hdr.e_lfanew;
        if (!ReadFile(hf, &dwSignature, sizeof(dwSignature), &iobytes, &ovl))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_403);
            __leave;
        }

        // Validate PE signature
        if ((iobytes != sizeof(dwSignature)) || (dwSignature != IMAGE_NT_SIGNATURE))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_415);
            __leave;
        }

        // Read COFF header
        RtlZeroMemory(&ovl, sizeof(ovl));
        ovl.Offset = dos_hdr.e_lfanew + sizeof(dwSignature);
        if (!ReadFile(hf, &nt_file_hdr, sizeof(nt_file_hdr), &iobytes, &ovl))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_403);
            __leave;
        }
        if (iobytes != sizeof(nt_file_hdr))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_415);
            __leave;
        }

        RtlZeroMemory(&ovl, sizeof(ovl));
        ovl.Offset = dos_hdr.e_lfanew + sizeof(dwSignature) + IMAGE_SIZEOF_FILE_HEADER;

#pragma region CHECKSUM
        // Calculate checksum via memory mapping
        HANDLE hm = CreateFileMapping(hf, NULL, PAGE_READONLY, 0, 0, NULL);
        if (hm != NULL)
        {
            PVOID mapping = MapViewOfFile(hm, FILE_MAP_READ, 0, 0, 0);
            if (mapping != NULL)
            {
                opt_file_hdr.opt_file_hdr64 = (PIMAGE_OPTIONAL_HEADER64)((PBYTE)mapping + ovl.Offset);
                dwRealChecksum = calc_mapped_file_chksum(mapping, fileinfo.nFileSizeLow, (PUSHORT)&opt_file_hdr.opt_file_hdr64->CheckSum);
                UnmapViewOfFile(mapping);
            }
            CloseHandle(hm);
        }
#pragma endregion

        // Allocate memory for optional header and sections
        szOptAndSections = nt_file_hdr.SizeOfOptionalHeader + nt_file_hdr.NumberOfSections * IMAGE_SIZEOF_SECTION_HEADER;
        if (szOptAndSections < PAGE_SIZE)
            szOptAndSections = PAGE_SIZE;

        opt_file_hdr.opt_file_hdr64 = VirtualAllocEx(GetCurrentProcess(), NULL, szOptAndSections,
            MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

        if (!opt_file_hdr.opt_file_hdr64)
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_500);
            __leave;
        }

        // Read optional header and section headers
        if (!ReadFile(hf, opt_file_hdr.opt_file_hdr64, szOptAndSections, &iobytes, &ovl))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_403);
            __leave;
        }

        sections = (PIMAGE_SECTION_HEADER)((PBYTE)opt_file_hdr.opt_file_hdr64 + nt_file_hdr.SizeOfOptionalHeader);

        // Validate PE magic number
        context->moduleMagic = opt_file_hdr.opt_file_hdr64->Magic;
        if (context->moduleMagic != IMAGE_NT_OPTIONAL_HDR32_MAGIC &&
            context->moduleMagic != IMAGE_NT_OPTIONAL_HDR64_MAGIC)
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_415);
            __leave;
        }

        context->image_64bit = (context->moduleMagic == IMAGE_NT_OPTIONAL_HDR64_MAGIC);

        // Validate section and file alignment
        DWORD sec_align, file_align;
        if (context->image_64bit) {
            sec_align = opt_file_hdr.opt_file_hdr64->SectionAlignment;
            file_align = opt_file_hdr.opt_file_hdr64->FileAlignment;
        }
        else {
            sec_align = opt_file_hdr.opt_file_hdr32->SectionAlignment;
            file_align = opt_file_hdr.opt_file_hdr32->FileAlignment;
        }

        if ((sec_align | file_align) == 0) {
            sendstring_plaintext_no_track(s, WDEP_STATUS_415);
            __leave;
        }

        /* checking for sections continuity */
        if (nt_file_hdr.NumberOfSections == 0)
        {
            vsize = PAGE_ALIGN(max((ULONG)dos_hdr.e_lfanew,
                opt_file_hdr.opt_file_hdr64->SizeOfImage));
        }
        else
        {
            vsize = sections[0].VirtualAddress;
        }

        for (c = 0; c < nt_file_hdr.NumberOfSections; ++c)
        {
            if (((sections[c].VirtualAddress % opt_file_hdr.opt_file_hdr64->SectionAlignment) != 0) ||
                (sections[c].VirtualAddress != vsize))
            {
                sendstring_plaintext_no_track(s, WDEP_STATUS_415);
                __leave;
            }

            tsize = sections[c].Misc.VirtualSize;
            psize = sections[c].SizeOfRawData;
            if ((tsize | psize) == 0)
            {
                sendstring_plaintext_no_track(s, WDEP_STATUS_415);
                __leave;
            }

            if (tsize == 0) tsize = psize;
            vsize += ALIGN_UP(tsize, opt_file_hdr.opt_file_hdr64->SectionAlignment);
        }

        vsize = PAGE_ALIGN(vsize);

        if (vsize != PAGE_ALIGN(opt_file_hdr.opt_file_hdr64->SizeOfImage))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_415);
            __leave;
        }

        /* End of image validation. Begin image loading */

        image_base = (context->image_64bit) ? DEFAULT_APP_ADDRESS_64 : DEFAULT_APP_ADDRESS_32;

        if (context->image_64bit) {
            get_pe_dirbase_size(opt_file_hdr.opt_file_hdr64, IMAGE_DIRECTORY_ENTRY_BASERELOC, dir_base, dir_size);
            if (dir_base && dir_size >= sizeof(IMAGE_BASE_RELOCATION)) {
                image_fixed = FALSE;
            }

            /*else {
                image_base = opt_file_hdr.opt_file_hdr64->ImageBase;
            }*/

            get_pe_dirbase_size(opt_file_hdr.opt_file_hdr64, IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR, dir_base, dir_size);
            if (dir_base && dir_size >= sizeof(IMAGE_COR20_HEADER)) {
                image_dotnet = TRUE;
            }
        }
        else {
            get_pe_dirbase_size(opt_file_hdr.opt_file_hdr32, IMAGE_DIRECTORY_ENTRY_BASERELOC, dir_base, dir_size);
            if (dir_base && dir_size >= sizeof(IMAGE_BASE_RELOCATION)) {
                image_fixed = FALSE;
            }

            /*else {
                image_base = opt_file_hdr.opt_file_hdr32->ImageBase;
            }*/

            get_pe_dirbase_size(opt_file_hdr.opt_file_hdr32, IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR, dir_base, dir_size);
            if (dir_base && dir_size >= sizeof(IMAGE_COR20_HEADER)) {
                image_dotnet = TRUE;
            }
        }

        DEBUG_PRINT_FORMATTED("pe32open:\r\n\tprocess_relocs: %li\r\n\tenable_custom_image_base %li\r\n\tcustom_image_base: 0x%lX\r\n\timage_fixed: %li\r\nimage_dotnet: %li\r\n",
            context->process_relocs,
            context->enable_custom_image_base,
            context->custom_image_base,
            image_fixed,
            image_dotnet);

        if (context->enable_custom_image_base) {

            module = VirtualAllocEx(GetCurrentProcess(), (LPVOID)(ULONG_PTR)context->custom_image_base, vsize,
                MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

            dwLastError = GetLastError();

        }
        else {

            // allocate image buffer below 4GB for x86-32 compatibility
            for (c = image_base; c < MAX_APP_ADDRESS; c += context->allocation_granularity)
            {
                DEBUG_PRINT("pe32open: module allocated at 0x%p\r\n", module);

                module = VirtualAllocEx(GetCurrentProcess(), (LPVOID)(ULONG_PTR)c, vsize,
                    MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

                dwLastError = GetLastError();

                if (module)
                    break;
            }
        }

        if (!module) {
            DEBUG_PRINT("pe32open: module is not allocated, GetLastError 0x%lX\r\n", dwLastError);
            sendstring_plaintext_no_track(s, WDEP_STATUS_502);
            __leave;
        }

        DEBUG_PRINT("pe32open: module allocated at 0x%p\r\n", module);

        // Read PE headers into memory
        RtlZeroMemory(&ovl, sizeof(ovl));
        if (nt_file_hdr.NumberOfSections == 0)
        {
            psize = PAGE_ALIGN(
                max((ULONG)dos_hdr.e_lfanew,
                    opt_file_hdr.opt_file_hdr64->SizeOfImage));
        }
        else
        {
            psize = ALIGN_UP(
                max((ULONG)dos_hdr.e_lfanew, opt_file_hdr.opt_file_hdr64->SizeOfHeaders),
                opt_file_hdr.opt_file_hdr64->FileAlignment);
        }

        if (!ReadFile(hf, module, psize, &iobytes, &ovl))
        {
            sendstring_plaintext_no_track(s, WDEP_STATUS_403);
            __leave;
        }

        // Read sections into memory
        for (c = 0; c < nt_file_hdr.NumberOfSections; ++c)
        {
            if (sections[c].PointerToRawData == 0)
                continue;

            RtlZeroMemory(&ovl, sizeof(ovl));
            ovl.Offset = ALIGN_DOWN(sections[c].PointerToRawData, opt_file_hdr.opt_file_hdr64->FileAlignment);

            tsize = sections[c].Misc.VirtualSize;
            psize = sections[c].SizeOfRawData;
            if (tsize == 0) tsize = psize;
            tsize = min(tsize, psize);
            tsize = ALIGN_UP(tsize, opt_file_hdr.opt_file_hdr64->FileAlignment);

            if (!ReadFile(hf, module + sections[c].VirtualAddress, tsize, &iobytes, &ovl))
            {
                sendstring_plaintext_no_track(s, WDEP_STATUS_403);
                __leave;
            }
        }

        // Process relocations if needed
        if ((!image_fixed) && context->process_relocs) {

            BOOL relocs_processed = FALSE;

            if (context->image_64bit) {
                get_pe_dirbase_size(opt_file_hdr.opt_file_hdr64, IMAGE_DIRECTORY_ENTRY_BASERELOC, dir_base, dir_size);
                if (dir_base) {
                    relocs_processed = relocimage(module, (LPVOID)(DWORD_PTR)opt_file_hdr.opt_file_hdr64->ImageBase,
                        (PIMAGE_BASE_RELOCATION)(module + dir_base), dir_size);
                }
            }
            else {
                get_pe_dirbase_size(opt_file_hdr.opt_file_hdr32, IMAGE_DIRECTORY_ENTRY_BASERELOC, dir_base, dir_size);
                if (dir_base) {
                    relocs_processed = relocimage(module, (LPVOID)(ULONG_PTR)opt_file_hdr.opt_file_hdr32->ImageBase,
                        (PIMAGE_BASE_RELOCATION)(module + dir_base), dir_size);
                }
            }

            DEBUG_PRINT("pe32open: module relocation result %li\r\n", relocs_processed);
        }

        context->image_dotnet = image_dotnet;
        context->image_fixed = image_fixed;

        status = 1;
    }
    __finally
    {
        if (_abnormal_termination()) {
            printf("exception in pe32open\r\n");
        }

        if (!status && module) {
            VirtualFreeEx(GetCurrentProcess(), module, 0, MEM_RELEASE);
            module = NULL;
        }

        if (opt_file_hdr.opt_file_hdr64) {
            VirtualFreeEx(GetCurrentProcess(), opt_file_hdr.opt_file_hdr64, 0, MEM_RELEASE);
        }

        if (hf != INVALID_HANDLE_VALUE)
            CloseHandle(hf);
    }

    if (module) {
        StringCchPrintf(text, ARRAYSIZE(text),
            WDEP_STATUS_OK
            L"{\"FileAttributes\":%u,"
            L"\"CreationTimeLow\":%u,"
            L"\"CreationTimeHigh\":%u,"
            L"\"LastWriteTimeLow\":%u,"
            L"\"LastWriteTimeHigh\":%u,"
            L"\"FileSizeHigh\":%u,"
            L"\"FileSizeLow\":%u,"
            L"\"RealChecksum\":%u,"
            L"\"ImageFixed\":%u,"
            L"\"ImageDotNet\":%u}\r\n",
            fileinfo.dwFileAttributes,
            fileinfo.ftCreationTime.dwLowDateTime,
            fileinfo.ftCreationTime.dwHighDateTime,
            fileinfo.ftLastWriteTime.dwLowDateTime,
            fileinfo.ftLastWriteTime.dwHighDateTime,
            fileinfo.nFileSizeHigh,
            fileinfo.nFileSizeLow,
            dwRealChecksum,
            (DWORD)image_fixed,
            (DWORD)image_dotnet
        );
        sendstring_plaintext(s, text, context);
    }

    return module;
}

BOOL pe32close(PBYTE module)
{
    if (module)
        return VirtualFreeEx(GetCurrentProcess(), module, 0, MEM_RELEASE);

    return FALSE;
}
