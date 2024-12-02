/*
*  File: mlist.c
*
*  Created on: Nov 08, 2024
*
*  Modified on: Nov 08, 2024
*
*      Project: WinDepends.Core
*
*      Author:
*/

#include "core.h"

BOOL mlist_add(
    _In_ PLIST_ENTRY head,
    _In_ const wchar_t* text
)
{
    BOOL bSuccess = FALSE;
    HANDLE processHeap = GetProcessHeap();
    message_node* newNode = NULL;
    size_t messageLength;
    HRESULT hr;

    do {

        newNode = (message_node*)heap_calloc(processHeap, sizeof(message_node));
        if (newNode == NULL) {
            break;
        }

        hr = StringCchLength(text, STRSAFE_MAX_CCH, &messageLength);
        if (FAILED(hr)) {
            break;
        }

        newNode->message = (wchar_t*)heap_calloc(processHeap, (messageLength + 1) * sizeof(wchar_t));
        if (newNode->message == NULL) {
            break;
        }

        hr = StringCchCopy(newNode->message, messageLength + 1, text);
        if (FAILED(hr)) {
            break;
        }

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
    HRESULT hr;

    // Send list and dispose
    if (action == mlist_send) {

        for (entry = listHead->Flink, nextEntry = entry->Flink;
            entry != listHead;
            entry = nextEntry, nextEntry = entry->Flink)
        {
            node = CONTAINING_RECORD(entry, message_node, ListEntry);
            cchTotalSize += node->messageLength;
        }

        pchBuffer = (PWCHAR)heap_calloc(processHeap, cchTotalSize * sizeof(WCHAR));
        if (pchBuffer == NULL) {
            return FALSE;
        }

        for (entry = listHead->Flink, nextEntry = entry->Flink;
            entry != listHead;
            entry = nextEntry, nextEntry = entry->Flink)
        {
            node = CONTAINING_RECORD(entry, message_node, ListEntry);
            if (node->message != NULL) {

                hr = StringCchCat(pchBuffer, cchTotalSize, node->message);
                if (FAILED(hr)) {
                    bAnyError = TRUE;
                    break;
                }

                heap_free(processHeap, node->message);
            }

            heap_free(processHeap, node);
        }

        if (bAnyError)
            return FALSE;

        sendstring_plaintext(s, pchBuffer, context);

    }
    else if (action == mlist_free) { 
        
        // Just dispose, there is an error
        for (entry = listHead->Flink, nextEntry = entry->Flink;
            entry != listHead;
            entry = nextEntry, nextEntry = entry->Flink)
        {
            node = CONTAINING_RECORD(entry, message_node, ListEntry);
                       
            if (node->message != NULL) {
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
