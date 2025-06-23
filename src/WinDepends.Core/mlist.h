/*
*  File: mlist.h
*
*  Created on: Nov 08, 2024
*
*  Modified on: Jun 10, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#pragma once

#ifndef _MLIST_H_
#define _MLIST_H_

#define MLIST_DEFAULT_BUFFER_SIZE 256

typedef struct {
    LIST_ENTRY ListEntry;
    PWCHAR message;
    SIZE_T messageLength;
    SIZE_T bufferSize;
    BOOL isStaticBuffer;
    WCHAR staticBuffer[MLIST_DEFAULT_BUFFER_SIZE];  // Pre-allocated buffer for small messages
} message_node;

BOOL mlist_add(
    _In_ PLIST_ENTRY head,
    _In_ const wchar_t* text,
    _In_ size_t textLength
);

typedef enum {
    // Dispose memory allocated for list.
    mlist_free,
    // Send list to client and dispose memory allocated for list.
    mlist_send
} mlist_action;

BOOL mlist_traverse(
    _In_ PLIST_ENTRY head,
    _In_ mlist_action action,
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
);

void mlist_append_to_main(
    _In_ PLIST_ENTRY src,
    _In_ PLIST_ENTRY dest
);

#endif _MLIST_H_
