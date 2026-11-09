/*
*  File: apiset.c
*
*  Created on: Dec 06, 2024
*
*  Modified on: Nov 05, 2026
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#include "core.h"

BOOL
NTAPI
ApiSetpValidateNameToResolve(
    _In_ PCUNICODE_STRING ApiSetNameToResolve
)
{
    WCHAR* nameBuffer;
    ULONGLONG nameBufferPrefix;

    if (ApiSetNameToResolve->Length < API_SET_PREFIX_NAME_U_LENGTH) {
        return FALSE;
    }

    nameBuffer = ApiSetNameToResolve->Buffer;
    nameBufferPrefix = API_SET_TO_UPPER_PREFIX(((ULONG64*)nameBuffer)[0]);
    if (nameBufferPrefix != API_SET_PREFIX_API && nameBufferPrefix != API_SET_PREFIX_EXT) {
        return FALSE;
    }

    return TRUE;
}

PAPI_SET_NAMESPACE_ENTRY_V6
NTAPI
ApiSetpSearchForApiSetV6(
    _In_ PAPI_SET_NAMESPACE_V6 ApiSetNamespace,
    _In_ PWCHAR ApiSetNameToResolve,
    _In_ USHORT ApiSetNameToResolveLength
)
{
    WCHAR wch;
    USHORT i;
    ULONG entryHash;
    LONG low, middle, high;
    PAPI_SET_HASH_ENTRY_V6 hashEntry;
    PAPI_SET_NAMESPACE_ENTRY_V6 foundEntry;

    if (!ApiSetNameToResolveLength) {
        return NULL;
    }

    entryHash = 0;

    for (i = 0; i < ApiSetNameToResolveLength; i++) {
        wch = locase_w(ApiSetNameToResolve[i]);
        entryHash = entryHash * ApiSetNamespace->HashMultiplier + wch;
    }

    foundEntry = NULL;
    low = 0;
    middle = 0;
    high = (LONG)ApiSetNamespace->Count - 1;

    while (high >= low) {
        middle = (low + high) >> 1;

        hashEntry = API_SET_HASH_ENTRY_V6(ApiSetNamespace, middle);

        if (entryHash < hashEntry->Hash) {
            high = middle - 1;
        }
        else if (entryHash > hashEntry->Hash) {
            low = middle + 1;
        }
        else {
            foundEntry = API_SET_NAMESPACE_ENTRY_V6(ApiSetNamespace, hashEntry);
            break;
        }
    }

    if (high < low || foundEntry == NULL) {
        return NULL;
    }

    if (0 == gsup.RtlCompareUnicodeStrings(ApiSetNameToResolve,
        ApiSetNameToResolveLength,
        API_SET_NAMESPACE_ENTRY_NAME_V6(ApiSetNamespace, foundEntry),
        foundEntry->HashNameLength / sizeof(WCHAR),
        TRUE))
    {
        return foundEntry;
    }

    return NULL;
}

PAPI_SET_VALUE_ENTRY_V6
NTAPI
ApiSetpSearchForApiSetHostV6(
    _In_ PAPI_SET_NAMESPACE_ENTRY_V6 Entry,
    _In_ WCHAR* ApiSetNameToResolve,
    _In_ USHORT ApiSetNameToResolveLength,
    _In_ PAPI_SET_NAMESPACE_V6 ApiSetNamespace
)
{
    LONG result, low, middle, high;
    PAPI_SET_VALUE_ENTRY_V6 foundEntry;
    PAPI_SET_VALUE_ENTRY_V6 hostEntry;

    foundEntry = API_SET_VALUE_ENTRY_V6(ApiSetNamespace, Entry, 0);

    high = (LONG)(Entry->Count - 1);
    if (!high) {
        return foundEntry;
    }

    low = 1;
    while (low <= high) {
        middle = (low + high) >> 1;

        hostEntry = API_SET_VALUE_ENTRY_V6(ApiSetNamespace, Entry, middle);

        result = gsup.RtlCompareUnicodeStrings(ApiSetNameToResolve,
            ApiSetNameToResolveLength,
            API_SET_VALUE_NAME_V6(ApiSetNamespace, hostEntry),
            hostEntry->NameLength / sizeof(WCHAR),
            TRUE);

        if (result < 0) {
            high = middle - 1;
        }
        else if (result > 0) {
            low = middle + 1;
        }
        else {
            foundEntry = API_SET_VALUE_ENTRY_V6(ApiSetNamespace, Entry, middle);
            break;
        }
    }

    return foundEntry;
}

NTSTATUS
NTAPI
ApiSetResolveToHostV6(
    _In_ PAPI_SET_NAMESPACE ApiSetNamespace,
    _In_ PCUNICODE_STRING ApiSetNameToResolve,
    _In_opt_ PCUNICODE_STRING ParentName,
    _Out_ PUNICODE_STRING Output
)
{
    BOOLEAN resolved = FALSE;
    USHORT effectiveLength;
    WCHAR* nameBuffer, * pwch;
    ULONG nameBufferLength;

    PAPI_SET_NAMESPACE_ENTRY_V6 resolvedNamespaceEntry;
    PAPI_SET_VALUE_ENTRY_V6 hostEntry;

    do {

        if (!ApiSetpValidateNameToResolve(ApiSetNameToResolve))
            break;

        nameBuffer = ApiSetNameToResolve->Buffer;
        nameBufferLength = (ULONG)ApiSetNameToResolve->Length;
        pwch = (WCHAR*)((ULONG_PTR)nameBuffer + nameBufferLength);
        do {
            if (nameBufferLength <= 1)
                break;
            nameBufferLength -= sizeof(WCHAR);
            --pwch;
        } while (*pwch != L'-');

        effectiveLength = (USHORT)(nameBufferLength / sizeof(WCHAR));
        if (!effectiveLength) {
            break;
        }

        resolvedNamespaceEntry = ApiSetpSearchForApiSetV6((PAPI_SET_NAMESPACE_V6)ApiSetNamespace,
            nameBuffer,
            effectiveLength);

        if (!resolvedNamespaceEntry) {
            break;
        }

        if (resolvedNamespaceEntry->Count > 1 && ParentName) {

            hostEntry = ApiSetpSearchForApiSetHostV6(resolvedNamespaceEntry,
                ParentName->Buffer,
                ParentName->Length / sizeof(WCHAR),
                (PAPI_SET_NAMESPACE_V6)ApiSetNamespace);

        }
        else if (resolvedNamespaceEntry->Count > 0) {

            hostEntry = API_SET_VALUE_ENTRY_V6(ApiSetNamespace,
                resolvedNamespaceEntry,
                0);
        }
        else {
            break;
        }

        if (IS_API_SET_EMPTY_VALUE_ENTRY_V6(hostEntry)) {
            return STATUS_APISET_NOT_HOSTED;
        }

        Output->Length = (USHORT)hostEntry->ValueLength;
        Output->MaximumLength = Output->Length;
        Output->Buffer = API_SET_VALUE_ENTRY_VALUE_V6(ApiSetNamespace, hostEntry);

        resolved = TRUE;

    } while (FALSE);

    return resolved ? STATUS_SUCCESS : STATUS_APISET_NOT_PRESENT;
}

PAPI_SET_NAMESPACE_ENTRY_V4
NTAPI
ApiSetpSearchForApiSetV4(
    _In_ PVOID Namespace,
    _In_ PCUNICODE_STRING ApiSetNameToResolve
)
{
    LONG result, low, middle, high;
    PAPI_SET_NAMESPACE_ARRAY_V4 namespaceArray;
    PAPI_SET_NAMESPACE_ENTRY_V4 namespaceEntry;
    UNICODE_STRING entryName;

    namespaceArray = (PAPI_SET_NAMESPACE_ARRAY_V4)Namespace;

    low = 0;
    high = (LONG)(namespaceArray->Count - 1);
    while (high >= low) {

        middle = (high + low) >> 1;

        namespaceEntry = API_SET_NAMESPACE_ENTRY_V4(namespaceArray, middle);

        entryName.Length = entryName.MaximumLength = (USHORT)namespaceEntry->NameLength;
        entryName.Buffer = API_SET_NAMESPACE_ENTRY_NAME_V4(namespaceArray, namespaceEntry);

        result = gsup.RtlCompareUnicodeString(ApiSetNameToResolve, &entryName, TRUE);

        if (result < 0) {
            high = middle - 1;
        }
        else if (result > 0) {
            low = middle + 1;
        }
        else {
            return namespaceEntry;
        }
    }

    return NULL;
}

PAPI_SET_VALUE_ENTRY_V4
NTAPI
ApiSetpSearchForApiSetHostV4(
    _In_ PAPI_SET_VALUE_ARRAY_V4 ApiSetValueArray,
    _In_ PCUNICODE_STRING ApiSetNameToResolve,
    _In_ PAPI_SET_NAMESPACE_ARRAY_V4 ApiSetNamespace
)
{
    LONG result, low, middle, high;
    PAPI_SET_VALUE_ENTRY_V4 hostEntry;
    UNICODE_STRING entryName;

    low = 1;
    high = (LONG)(ApiSetValueArray->Count - 1);
    while (high >= low) {
        middle = (high + low) >> 1;

        hostEntry = API_SET_VALUE_ENTRY_V4(ApiSetNamespace, ApiSetValueArray, middle);

        entryName.Length = entryName.MaximumLength = (USHORT)hostEntry->NameLength;
        entryName.Buffer = API_SET_VALUE_ENTRY_NAME_V4(ApiSetNamespace, hostEntry);

        result = gsup.RtlCompareUnicodeString(ApiSetNameToResolve, &entryName, TRUE);

        if (result < 0) {
            high = middle - 1;
        }
        else if (result > 0) {
            low = middle + 1;
        }
        else {
            return hostEntry;
        }
    }

    return NULL;
}

NTSTATUS
NTAPI
ApiSetResolveToHostV4(
    _In_ PAPI_SET_NAMESPACE ApiSetNamespace,
    _In_ PCUNICODE_STRING ApiSetNameToResolve,
    _In_opt_ PCUNICODE_STRING ParentName,
    _Out_ PUNICODE_STRING Output
)
{
    PAPI_SET_NAMESPACE_ARRAY_V4 namespaceArray;
    PAPI_SET_NAMESPACE_ENTRY_V4 namespaceEntry;
    PAPI_SET_VALUE_ARRAY_V4 valueArray;
    PAPI_SET_VALUE_ENTRY_V4 hostEntry;
    UNICODE_STRING nameWithoutExtension;
    USHORT prefixLength = sizeof(ULONGLONG);

    if (!ApiSetpValidateNameToResolve(ApiSetNameToResolve))
        return STATUS_APISET_NOT_PRESENT;

    nameWithoutExtension.Length = ApiSetNameToResolve->Length - prefixLength;
    nameWithoutExtension.MaximumLength = nameWithoutExtension.Length;
    nameWithoutExtension.Buffer = (WCHAR*)((ULONG_PTR)ApiSetNameToResolve->Buffer + prefixLength);

    if (nameWithoutExtension.Length >= prefixLength &&
        nameWithoutExtension.Buffer[(USHORT)(nameWithoutExtension.Length - prefixLength) / sizeof(WCHAR)] == L'.')
    {
        nameWithoutExtension.Length -= prefixLength;
    }

    namespaceArray = (PAPI_SET_NAMESPACE_ARRAY_V4)ApiSetNamespace;
    namespaceEntry = ApiSetpSearchForApiSetV4(namespaceArray, &nameWithoutExtension);
    if (!namespaceEntry)
        return STATUS_APISET_NOT_PRESENT;

    valueArray = API_SET_NAMESPACE_ENTRY_DATA_V4(namespaceArray, namespaceEntry);
    if (valueArray->Count == 0)
        return STATUS_APISET_NOT_PRESENT;

    hostEntry = NULL;

    if (ParentName && valueArray->Count > 1)
        hostEntry = ApiSetpSearchForApiSetHostV4(valueArray, ParentName, namespaceArray);

    if (!hostEntry)
        hostEntry = API_SET_VALUE_ENTRY_V4(namespaceArray, valueArray, 0);

    if (IS_API_SET_EMPTY_VALUE_ENTRY_V4(hostEntry))
        return STATUS_APISET_NOT_HOSTED;

    Output->Length = (USHORT)hostEntry->ValueLength;
    Output->MaximumLength = Output->Length;
    Output->Buffer = API_SET_VALUE_ENTRY_VALUE_V4(namespaceArray, hostEntry);

    return STATUS_SUCCESS;
}

PAPI_SET_VALUE_ENTRY_V2
NTAPI
ApiSetpSearchForApiSetHostV2(
    _In_ PAPI_SET_VALUE_ARRAY_V2 ApiSetValueArray,
    _In_ PCUNICODE_STRING ApiToResolve,
    _In_ PAPI_SET_NAMESPACE ApiSetNamespace
)
{
    LONG low, middle, high, result;
    UNICODE_STRING hostName;
    PAPI_SET_VALUE_ENTRY_V2 valueEntry;

    low = 1;
    high = ApiSetValueArray->Count - 1;

    while (high >= low) {
        middle = (high + low) >> 1;

        valueEntry = &ApiSetValueArray->Array[middle];
        hostName.Length = (USHORT)valueEntry->NameLength;
        hostName.MaximumLength = hostName.Length;
        hostName.Buffer = (WCHAR*)((ULONG_PTR)ApiSetNamespace + valueEntry->NameOffset);

        result = gsup.RtlCompareUnicodeString(ApiToResolve, &hostName, TRUE);

        if (result < 0) {
            high = middle - 1;
        }
        else if (result > 0) {
            low = middle + 1;
        }
        else {
            return valueEntry;
        }
    }

    return NULL;
}

NTSTATUS
NTAPI
ApiSetResolveToHostV2(
    _In_ PAPI_SET_NAMESPACE ApiSetNamespace,
    _In_ PCUNICODE_STRING ApiSetNameToResolve,
    _In_opt_ PCUNICODE_STRING ParentName,
    _Out_ PUNICODE_STRING Output
)
{
    BOOLEAN Resolved = FALSE;
    LONG result, low, middle, high;
    PAPI_SET_NAMESPACE_ARRAY_V2 namespaceArray;
    PAPI_SET_NAMESPACE_ENTRY_V2 namespaceEntry;
    PAPI_SET_VALUE_ARRAY_V2 valueArray;
    PAPI_SET_VALUE_ENTRY_V2 hostEntry;
    UNICODE_STRING namespaceName;
    UNICODE_STRING nameWithoutExtension;
    USHORT prefixLength = sizeof(ULONGLONG);

    do {

        if (!ApiSetpValidateNameToResolve(ApiSetNameToResolve))
            break;

        nameWithoutExtension.Length = ApiSetNameToResolve->Length - prefixLength;
        nameWithoutExtension.MaximumLength = nameWithoutExtension.Length;
        nameWithoutExtension.Buffer = (WCHAR*)((ULONG_PTR)ApiSetNameToResolve->Buffer + prefixLength);

        if (nameWithoutExtension.Length >= prefixLength &&
            nameWithoutExtension.Buffer[(USHORT)(nameWithoutExtension.Length - prefixLength) / sizeof(WCHAR)] == L'.')
        {
            nameWithoutExtension.Length -= prefixLength;
        }

        namespaceArray = (PAPI_SET_NAMESPACE_ARRAY_V2)ApiSetNamespace;
        namespaceEntry = NULL;

        low = 0;
        high = (LONG)(namespaceArray->Count - 1);

        while (high >= low) {

            middle = (low + high) >> 1;
            namespaceEntry = API_SET_NAMESPACE_ENTRY_V2(ApiSetNamespace, middle);
            namespaceName.Length = (USHORT)namespaceEntry->NameLength;
            namespaceName.MaximumLength = namespaceName.Length;
            namespaceName.Buffer = (WCHAR*)((ULONG_PTR)ApiSetNamespace + namespaceEntry->NameOffset);

            result = gsup.RtlCompareUnicodeString(&nameWithoutExtension, &namespaceName, TRUE);

            if (result < 0) {
                high = middle - 1;
            }
            else if (result > 0) {
                low = middle + 1;
            }
            else {
                break;
            }
        }

        if (high < low) {
            break;
        }

        valueArray = (PAPI_SET_VALUE_ARRAY_V2)((ULONG_PTR)ApiSetNamespace + namespaceEntry->DataOffset);

        if (valueArray->Count > 1 && ParentName) {

            hostEntry = ApiSetpSearchForApiSetHostV2(valueArray,
                ParentName,
                ApiSetNamespace);
        }
        else {
            hostEntry = NULL;
        }

        if (hostEntry == NULL) {
            hostEntry = valueArray->Array;
        }

        Output->Length = (USHORT)hostEntry->ValueLength;
        Output->MaximumLength = Output->Length;
        Output->Buffer = (WCHAR*)((ULONG_PTR)ApiSetNamespace + hostEntry->ValueOffset);

        Resolved = TRUE;

    } while (FALSE);

    return Resolved ? STATUS_SUCCESS : STATUS_APISET_NOT_PRESENT;
}
