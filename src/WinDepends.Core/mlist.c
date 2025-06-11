/*
*  File: mlist.c
*
*  Created on: Nov 08, 2024
*
*  Modified on: Jun 10, 2025
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
            if (newNode->message != NULL) {
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
    PLIST_ENTRY listHead = head, entry, nextEntry;
    message_node* node = NULL;
    HANDLE processHeap = GetProcessHeap();

    PWCHAR pchBuffer = NULL; // cumulative buffer
    SIZE_T cchTotalSize = 128; // default safe space for cumulative buffer
    SIZE_T position, msgLen;

    // Early exit for a small sends
    if (head->Flink != head && head->Flink->Flink == head && action == mlist_send) {
        node = CONTAINING_RECORD(head->Flink, message_node, ListEntry);
        if (node->message) {
            sendstring_plaintext(s, node->message, context);
            if (!node->isStaticBuffer) {
                heap_free(processHeap, node->message);
            }
        }
        heap_free(processHeap, node);
        return TRUE;
    }

    // Send list and dispose
    if (action == mlist_send) {

        position = 0;

        for (entry = listHead->Flink, nextEntry = entry->Flink;
            entry != listHead;
            entry = nextEntry, nextEntry = entry->Flink)
        {
            node = CONTAINING_RECORD(entry, message_node, ListEntry);
            cchTotalSize += node->messageLength;
        }

        pchBuffer = (PWCHAR)heap_calloc(processHeap, (1 + cchTotalSize) * sizeof(WCHAR));
        if (pchBuffer == NULL) {
            return FALSE;
        }

        for (entry = listHead->Flink, nextEntry = entry->Flink;
            entry != listHead;
            entry = nextEntry, nextEntry = entry->Flink)
        {
            node = CONTAINING_RECORD(entry, message_node, ListEntry);
            if (node->message != NULL) {
                
                msgLen = node->messageLength;
                if (position + msgLen > cchTotalSize) {
                    bAnyError = TRUE;
                    break;
                }

                memcpy(pchBuffer + position, node->message, msgLen * sizeof(WCHAR));
                position += msgLen;
                
                if (!node->staticBuffer)
                    heap_free(processHeap, node->message);
            }

            heap_free(processHeap, node);
        }

        if (bAnyError) {
            if (pchBuffer != NULL) {
                heap_free(processHeap, pchBuffer);
                pchBuffer = NULL;
            }
            return FALSE;
        }

        sendstring_plaintext(s, pchBuffer, context);

    }
    else if (action == mlist_free) { 
        
        // Just dispose, there is an error
        for (entry = listHead->Flink, nextEntry = entry->Flink;
            entry != listHead;
            entry = nextEntry, nextEntry = entry->Flink)
        {
            node = CONTAINING_RECORD(entry, message_node, ListEntry);
                       
            if (!node->isStaticBuffer && node->message != NULL) {
                heap_free(processHeap, node->message);
            }
            
            heap_free(processHeap, node);
        }
    }

    if (pchBuffer != NULL) {
        heap_free(processHeap, pchBuffer);
    }

    return TRUE;
}
