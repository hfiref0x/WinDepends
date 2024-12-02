/*
*  File: mlist.h
*
*  Created on: Nov 08, 2024
*
*  Modified on: Nov 30, 2024
*
*      Project: WinDepends.Core
*
*      Author:
*/

#pragma once

#ifndef _MLIST_H_
#define _MLIST_H_

typedef struct {
    LIST_ENTRY ListEntry;
    PWCHAR message;
    SIZE_T messageLength;
} message_node;

BOOL mlist_add(
    _In_ PLIST_ENTRY head,
    _In_ const wchar_t* text
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

#endif _MLIST_H_
