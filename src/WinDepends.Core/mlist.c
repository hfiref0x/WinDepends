/*
*  File: mlist.c
*
*  Created on: Nov 08, 2024
*
*  Modified on: Aug 16, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#include "core.h"

BOOL mlist_add(
    _In_ PLIST_ENTRY head,
    _In_ const wchar_t* text,
    _In_ size_t textLength
)
{
    BOOL bSuccess = FALSE;
    HANDLE processHeap = GetProcessHeap();
    message_node* newNode = NULL;
    size_t messageLength = textLength;
    HRESULT hr;

    do {

        newNode = (message_node*)heap_calloc(processHeap, sizeof(message_node));
        if (newNode == NULL) {
            break;
        }

        if (messageLength == 0) {
            hr = StringCchLength(text, STRSAFE_MAX_CCH, &messageLength);
            if (FAILED(hr)) {
                break;
            }
        }

        if (messageLength < MLIST_DEFAULT_BUFFER_SIZE) {
            newNode->message = newNode->staticBuffer;
            newNode->bufferSize = MLIST_DEFAULT_BUFFER_SIZE;
            newNode->isStaticBuffer = TRUE;
        }
        else {
            newNode->message = (wchar_t*)heap_calloc(processHeap, (messageLength + 1) * sizeof(wchar_t));
            if (newNode->message == NULL) {
                break;
            }
            newNode->bufferSize = messageLength + 1;
        }

        memcpy(newNode->message, text, messageLength * sizeof(wchar_t));
        newNode->messageLength = messageLength;
        InsertTailList(head, &newNode->ListEntry);

        bSuccess = TRUE;

    } while (FALSE);

    if (!bSuccess) {
        if (newNode) {
            if (!newNode->isStaticBuffer && newNode->message != NULL) {
                heap_free(processHeap, newNode->message);
            }
            heap_free(processHeap, newNode);
        }
    }

    return bSuccess;
}

BOOL mlist_traverse(
    _In_ PLIST_ENTRY head,
    _In_ mlist_action action,
    _In_ SOCKET s,
    _In_opt_ pmodule_ctx context
)
{
    BOOL bAnyError = FALSE;
    PLIST_ENTRY entry, nextEntry;
    message_node* node = NULL;
    PWCHAR pchBuffer = NULL; //cumulative buffer
    SIZE_T cchTotalSize = 128;
    SIZE_T position = 0;

    // Early exit for single-node send
    if (action == mlist_send &&
        !IsListEmpty(head) &&
        head->Flink->Flink == head)
    {
        entry = head->Flink;
        node = CONTAINING_RECORD(entry, message_node, ListEntry);
        if (node->message) {
            sendstring_plaintext(s, node->message, context);
        }

        if (!node->isStaticBuffer && node->message) {
            heap_free(NULL, node->message);
        }
        heap_free(NULL, node);
        InitializeListHead(head);
        return TRUE;
    }

    if (action == mlist_send) {
        // Pass 1: Calculate total size
        entry = head->Flink;
        while (entry != head) {
            node = CONTAINING_RECORD(entry, message_node, ListEntry);
            if (node->messageLength) {
                if (cchTotalSize > SIZE_MAX - node->messageLength) {
                    bAnyError = TRUE;
                    break;
                }
                cchTotalSize += node->messageLength;
            }
            entry = entry->Flink;
        }

        // Allocate buffer if no errors
        if (!bAnyError) {
            pchBuffer = (PWCHAR)heap_calloc(NULL, (cchTotalSize + 1) * sizeof(WCHAR));
            if (!pchBuffer) bAnyError = TRUE;
        }

        // Pass 2: Process nodes with safe traversal
        entry = head->Flink;
        while (entry != head) {
            nextEntry = entry->Flink;
            node = CONTAINING_RECORD(entry, message_node, ListEntry);

            // Copy data if buffer available and within bounds
            if (!bAnyError && pchBuffer && node->message) {
                SIZE_T msgLen = node->messageLength;
                if (position <= cchTotalSize &&
                    msgLen <= cchTotalSize - position)
                {
                    memcpy(pchBuffer + position, node->message, msgLen * sizeof(WCHAR));
                    position += msgLen;
                }
                else {
                    bAnyError = TRUE;
                }
            }

            if (!node->isStaticBuffer && node->message) {
                heap_free(NULL, node->message);
            }
            heap_free(NULL, node);

            entry = nextEntry;
        }

        // Send if successful
        if (!bAnyError && pchBuffer) {
            sendstring_plaintext(s, pchBuffer, context);
        }

        // Cleanup
        if (pchBuffer) heap_free(NULL, pchBuffer);
        InitializeListHead(head);
        return !bAnyError;
    }
    else if (action == mlist_free) {
        entry = head->Flink;
        while (entry != head) {
            nextEntry = entry->Flink;
            node = CONTAINING_RECORD(entry, message_node, ListEntry);

            if (!node->isStaticBuffer && node->message) {
                heap_free(NULL, node->message);
            }
            heap_free(NULL, node);

            entry = nextEntry;
        }
        InitializeListHead(head);
        return TRUE;
    }

    //unknown command, just leave
    return FALSE;
}

void mlist_append_to_main(
    _In_ PLIST_ENTRY src,
    _In_ PLIST_ENTRY dest
)
{
    PLIST_ENTRY entry, nextEntry;

    for (entry = src->Flink, nextEntry = entry->Flink;
        entry != src;
        entry = nextEntry, nextEntry = entry->Flink)
    {
        InsertTailList(dest, entry);
    }
    InitializeListHead(src);
}

#ifdef _DEBUG
VOID mlist_debug_dump(
    _In_ PLIST_ENTRY head
)
{
    PLIST_ENTRY entry;
    message_node* node;
    SIZE_T totalLen;
    SIZE_T pos;
    SIZE_T len;
    PWCHAR buffer;
    BOOL overflow;

    if (head == NULL) {
        DEBUG_PRINT("mlist_debug_dump: (null)\r\n");
        return;
    }

    if (IsListEmpty(head)) {
        DEBUG_PRINT("mlist_debug_dump: <empty>\r\n");
        return;
    }

    totalLen = 0;
    overflow = FALSE;

    for (entry = head->Flink; entry != head; entry = entry->Flink) {
        node = CONTAINING_RECORD(entry, message_node, ListEntry);
        if (node->message && node->messageLength) {
            if (SIZE_MAX - totalLen <= node->messageLength) {
                overflow = TRUE;
                break;
            }
            totalLen += node->messageLength;
        }
    }

    if (overflow) {
        DEBUG_PRINT("mlist_debug_dump: overflow\r\n");
        return;
    }

    buffer = (PWCHAR)heap_calloc(NULL, (totalLen + 1) * sizeof(WCHAR));
    if (!buffer) {
        DEBUG_PRINT("mlist_debug_dump: alloc failed\r\n");
        return;
    }

    pos = 0;
    for (entry = head->Flink; entry != head; entry = entry->Flink) {
        node = CONTAINING_RECORD(entry, message_node, ListEntry);
        if (node->message && node->messageLength) {
            len = node->messageLength;
            if (pos + len > totalLen) break;
            memcpy(buffer + pos, node->message, len * sizeof(WCHAR));
            pos += len;
        }
    }
    buffer[pos] = 0;

    DEBUG_PRINT("mlist_debug_dump: %ws\r\n", buffer);

    heap_free(NULL, buffer);
}
#endif
