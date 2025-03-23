/*
*  File: apiset.c
*
*  Created on: Dec 06, 2024
*
*  Modified on: Mar 21, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#include "core.h"

__forceinline wchar_t locase_w(wchar_t c)
{
    if ((c >= 'A') && (c <= 'Z'))
        return c + 0x20;
    else
        return c;
}

BOOL
NTAPI
ApiSetpValidateNameToResolve(
    _In_ PCUNICODE_STRING ApiSetNameToResolve
)
{
    WCHAR* ApiSetNameBuffer;
    ULONGLONG ApiSetNameBufferPrefix;

    if (ApiSetNameToResolve->Length < API_SET_PREFIX_NAME_U_LENGTH) {
        return FALSE;
    }

    ApiSetNameBuffer = ApiSetNameToResolve->Buffer;
    ApiSetNameBufferPrefix = API_SET_TO_UPPER_PREFIX(((ULONG64*)ApiSetNameBuffer)[0]);
    if (ApiSetNameBufferPrefix != API_SET_PREFIX_API && ApiSetNameBufferPrefix != API_SET_PREFIX_EXT) {
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
    PAPI_SET_HASH_ENTRY_V6 HashEntry;
    PAPI_SET_NAMESPACE_ENTRY_V6 FoundEntry;

    if (!ApiSetNameToResolveLength) {
        return NULL;
    }

    entryHash = 0;

    for (i = 0; i < ApiSetNameToResolveLength; i++) {
        wch = locase_w(ApiSetNameToResolve[i]);
        entryHash = entryHash * ApiSetNamespace->HashMultiplier + wch;
    }

    FoundEntry = NULL;
    low = 0;
    middle = 0;
    high = (LONG)ApiSetNamespace->Count - 1;

    while (high >= low) {
        middle = (low + high) >> 1;

        HashEntry = API_SET_HASH_ENTRY_V6(ApiSetNamespace, middle);

        if (entryHash < HashEntry->Hash) {
            high = middle - 1;
        }
        else if (entryHash > HashEntry->Hash) {
            low = middle + 1;
        }
        else {
            FoundEntry = API_SET_NAMESPACE_ENTRY_V6(ApiSetNamespace, HashEntry);
            break;
        }
    }

    if (high < low || FoundEntry == NULL) {
        return NULL;
    }

    if (0 == gsup.RtlCompareUnicodeStrings(ApiSetNameToResolve,
        ApiSetNameToResolveLength,
        API_SET_NAMESPACE_ENTRY_NAME_V6(ApiSetNamespace, FoundEntry),
        FoundEntry->HashNameLength / sizeof(WCHAR),
        TRUE))
    {
        return FoundEntry;
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
    PAPI_SET_VALUE_ENTRY_V6 FoundEntry;
    PAPI_SET_VALUE_ENTRY_V6 ApiSetHostEntry;

    FoundEntry = API_SET_VALUE_ENTRY_V6(ApiSetNamespace, Entry, 0);

    high = (LONG)(Entry->Count - 1);
    if (!high) {
        return FoundEntry;
    }

    low = 1;
    while (low <= high) {
        middle = (low + high) >> 1;

        ApiSetHostEntry = API_SET_VALUE_ENTRY_V6(ApiSetNamespace, Entry, middle);

        result = gsup.RtlCompareUnicodeStrings(ApiSetNameToResolve,
            ApiSetNameToResolveLength,
            API_SET_VALUE_NAME_V6(ApiSetNamespace, ApiSetHostEntry),
            ApiSetHostEntry->NameLength / sizeof(WCHAR),
            TRUE);

        if (result < 0) {
            high = middle - 1;
        }
        else if (result > 0) {
            low = middle + 1;
        }
        else {
            FoundEntry = API_SET_VALUE_ENTRY_V6(ApiSetNamespace, Entry, middle);
            break;
        }
    }

    return FoundEntry;
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
    BOOLEAN Resolved = FALSE;
    USHORT ApiSetEffectiveLength;
    WCHAR* ApiSetNameBuffer, * pwch;
    ULONG ApiSetNameBufferLength;

    PAPI_SET_NAMESPACE_ENTRY_V6 ResolvedNamespaceEntry;
    PAPI_SET_VALUE_ENTRY_V6 HostLibraryEntry;

    do {

        if (!ApiSetpValidateNameToResolve(ApiSetNameToResolve))
            break;

        ApiSetNameBuffer = ApiSetNameToResolve->Buffer;
        ApiSetNameBufferLength = (ULONG)ApiSetNameToResolve->Length;
        pwch = (WCHAR*)((ULONG_PTR)ApiSetNameBuffer + ApiSetNameBufferLength);
        do {
            if (ApiSetNameBufferLength <= 1)
                break;
            ApiSetNameBufferLength -= sizeof(WCHAR);
            --pwch;
        } while (*pwch != L'-');

        ApiSetEffectiveLength = (USHORT)(ApiSetNameBufferLength / sizeof(WCHAR));
        if (!ApiSetEffectiveLength) {
            break;
        }

        ResolvedNamespaceEntry = ApiSetpSearchForApiSetV6((PAPI_SET_NAMESPACE_V6)ApiSetNamespace,
            ApiSetNameBuffer,
            ApiSetEffectiveLength);

        if (!ResolvedNamespaceEntry) {
            break;
        }

        if (ResolvedNamespaceEntry->Count > 1 && ParentName) {

            HostLibraryEntry = ApiSetpSearchForApiSetHostV6(ResolvedNamespaceEntry,
                ParentName->Buffer,
                ParentName->Length / sizeof(WCHAR),
                (PAPI_SET_NAMESPACE_V6)ApiSetNamespace);

        }
        else if (ResolvedNamespaceEntry->Count > 0) {

            HostLibraryEntry = API_SET_VALUE_ENTRY_V6(ApiSetNamespace,
                ResolvedNamespaceEntry,
                0);
        }
        else {
            break;
        }

        if (IS_API_SET_EMPTY_VALUE_ENTRY_V6(HostLibraryEntry)) {
            return STATUS_APISET_NOT_HOSTED;
        }

        Output->Length = (USHORT)HostLibraryEntry->ValueLength;
        Output->MaximumLength = Output->Length;
        Output->Buffer = API_SET_VALUE_ENTRY_VALUE_V6(ApiSetNamespace, HostLibraryEntry);

        Resolved = TRUE;

    } while (FALSE);

    return Resolved ? STATUS_SUCCESS : STATUS_APISET_NOT_PRESENT;
}

PAPI_SET_NAMESPACE_ENTRY_V4
NTAPI
ApiSetpSearchForApiSetV4(
    _In_ PVOID Namespace,
    _In_ PCUNICODE_STRING ApiSetNameToResolve
)
{
    LONG result, low, middle, high;
    PAPI_SET_NAMESPACE_ARRAY_V4 ApiSetNamespace;
    PAPI_SET_NAMESPACE_ENTRY_V4 ApiSetNamespaceEntry;
    UNICODE_STRING NamespaceEntry;

    ApiSetNamespace = (PAPI_SET_NAMESPACE_ARRAY_V4)Namespace;

    low = 0;
    high = (LONG)(ApiSetNamespace->Count - 1);
    while (high >= low) {

        middle = (high + low) >> 1;

        ApiSetNamespaceEntry = API_SET_NAMESPACE_ENTRY_V4(ApiSetNamespace, middle);

        NamespaceEntry.Length = NamespaceEntry.MaximumLength = (USHORT)ApiSetNamespaceEntry->NameLength;
        NamespaceEntry.Buffer = API_SET_NAMESPACE_ENTRY_NAME_V4(ApiSetNamespace, ApiSetNamespaceEntry);

        result = gsup.RtlCompareUnicodeString(ApiSetNameToResolve, &NamespaceEntry, TRUE);

        if (result < 0) {
            high = middle - 1;
        }
        else if (result > 0) {
            low = middle + 1;
        }
        else {
            return ApiSetNamespaceEntry;
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
    PAPI_SET_VALUE_ENTRY_V4 ApiSetHostEntry;
    UNICODE_STRING NamespaceEntry;

    low = 1;
    high = (LONG)(ApiSetValueArray->Count - 1);
    while (high >= low) {
        middle = (high + low) >> 1;

        ApiSetHostEntry = API_SET_VALUE_ENTRY_V4(ApiSetNamespace, ApiSetValueArray, middle);

        NamespaceEntry.Length = NamespaceEntry.MaximumLength = (USHORT)ApiSetHostEntry->NameLength;
        NamespaceEntry.Buffer = API_SET_VALUE_ENTRY_NAME_V4(ApiSetNamespace, ApiSetHostEntry);

        result = gsup.RtlCompareUnicodeString(ApiSetNameToResolve, &NamespaceEntry, TRUE);

        if (result < 0) {
            high = middle - 1;
        }
        else if (result > 0) {
            low = middle + 1;
        }
        else {
            return ApiSetHostEntry;
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
    BOOLEAN Resolved = FALSE;
    PAPI_SET_NAMESPACE_ENTRY_V4 ResolvedNamespaceEntry;
    PAPI_SET_VALUE_ARRAY_V4 ResolvedValueArray;
    PAPI_SET_VALUE_ENTRY_V4 HostLibraryEntry;
    UNICODE_STRING ApiSetNameNoExtString;
    USHORT PrefixLength = sizeof(ULONGLONG);

    do {

        if (!ApiSetpValidateNameToResolve(ApiSetNameToResolve))
            break;

        ApiSetNameNoExtString.Length = ApiSetNameToResolve->Length - PrefixLength;
        ApiSetNameNoExtString.MaximumLength = ApiSetNameNoExtString.Length;
        ApiSetNameNoExtString.Buffer = (WCHAR*)((ULONG_PTR)ApiSetNameToResolve->Buffer +
            PrefixLength);

        if (ApiSetNameNoExtString.Length >= PrefixLength &&
            ApiSetNameNoExtString.Buffer[(USHORT)(ApiSetNameNoExtString.Length - PrefixLength) / sizeof(WCHAR)] == L'.')
        {
            ApiSetNameNoExtString.Length -= PrefixLength;
        }

        ResolvedNamespaceEntry = ApiSetpSearchForApiSetV4(ApiSetNamespace,
            &ApiSetNameNoExtString);

        if (!ResolvedNamespaceEntry) {
            break;
        }

        ResolvedValueArray = API_SET_NAMESPACE_ENTRY_DATA_V4(ApiSetNamespace,
            ResolvedNamespaceEntry);

        if (ResolvedValueArray->Count > 1 && ParentName) {

            HostLibraryEntry = ApiSetpSearchForApiSetHostV4(ResolvedValueArray,
                ParentName,
                (PAPI_SET_NAMESPACE_ARRAY_V4)ApiSetNamespace);

        }
        else if (ResolvedValueArray->Count > 0) {

            HostLibraryEntry = API_SET_VALUE_ENTRY_V4(ApiSetNamespace, ResolvedValueArray, 0);

        }
        else {
            break;
        }

        if (HostLibraryEntry == NULL || IS_API_SET_EMPTY_VALUE_ENTRY_V4(HostLibraryEntry)) {
            return STATUS_APISET_NOT_HOSTED;
        }

        Output->Length = (USHORT)HostLibraryEntry->ValueLength;
        Output->MaximumLength = Output->Length;
        Output->Buffer = API_SET_VALUE_ENTRY_VALUE_V4(ApiSetNamespace, HostLibraryEntry);

        Resolved = TRUE;

    } while (FALSE);

    return Resolved ? STATUS_SUCCESS : STATUS_APISET_NOT_PRESENT;
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
    UNICODE_STRING ApiSetHostName;
    PAPI_SET_VALUE_ENTRY_V2 ApiSetValueEntry;

    low = 1;
    high = ApiSetValueArray->Count - 1;

    while (high >= low) {
        middle = (high + low) >> 1;

        ApiSetValueEntry = &ApiSetValueArray->Array[middle];
        ApiSetHostName.Length = (USHORT)ApiSetValueEntry->NameLength;
        ApiSetHostName.MaximumLength = ApiSetHostName.Length;
        ApiSetHostName.Buffer = (WCHAR*)((ULONG_PTR)ApiSetNamespace + ApiSetValueEntry->NameOffset);

        result = gsup.RtlCompareUnicodeString(ApiToResolve, &ApiSetHostName, TRUE);

        if (result < 0) {
            high = middle - 1;
        }
        else if (result > 0) {
            low = middle + 1;
        }
        else {
            return ApiSetValueEntry;
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
    PAPI_SET_NAMESPACE_ARRAY_V2 ApiSetNamespaceArray;
    PAPI_SET_NAMESPACE_ENTRY_V2 ApiSetNamespaceEntry;
    PAPI_SET_VALUE_ARRAY_V2 ApiSetValueArray;
    PAPI_SET_VALUE_ENTRY_V2 HostLibraryEntry;
    UNICODE_STRING ApiSetNamespaceString;
    UNICODE_STRING ApiSetNameNoExtString;
    USHORT PrefixLength = sizeof(ULONGLONG);

    do {

        if (!ApiSetpValidateNameToResolve(ApiSetNameToResolve))
            break;

        ApiSetNameNoExtString.Length = ApiSetNameToResolve->Length - PrefixLength;
        ApiSetNameNoExtString.MaximumLength = ApiSetNameNoExtString.Length;
        ApiSetNameNoExtString.Buffer = (WCHAR*)((ULONG_PTR)ApiSetNameToResolve->Buffer +
            PrefixLength);

        if (ApiSetNameNoExtString.Length >= PrefixLength &&
            ApiSetNameNoExtString.Buffer[(USHORT)(ApiSetNameNoExtString.Length - PrefixLength) / sizeof(WCHAR)] == L'.')
        {
            ApiSetNameNoExtString.Length -= PrefixLength;
        }

        ApiSetNamespaceArray = (PAPI_SET_NAMESPACE_ARRAY_V2)ApiSetNamespace;
        ApiSetNamespaceEntry = NULL;

        low = 0;
        high = (LONG)(ApiSetNamespaceArray->Count - 1);

        while (high >= low) {

            middle = (low + high) >> 1;
            ApiSetNamespaceEntry = API_SET_NAMESPACE_ENTRY_V2(ApiSetNamespace, middle);
            ApiSetNamespaceString.Length = (USHORT)ApiSetNamespaceEntry->NameLength;
            ApiSetNamespaceString.MaximumLength = ApiSetNamespaceString.Length;
            ApiSetNamespaceString.Buffer = (WCHAR*)((ULONG_PTR)ApiSetNamespace + ApiSetNamespaceEntry->NameOffset);

            result = gsup.RtlCompareUnicodeString(&ApiSetNameNoExtString, &ApiSetNamespaceString, TRUE);

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

        ApiSetValueArray = (PAPI_SET_VALUE_ARRAY_V2)((ULONG_PTR)ApiSetNamespace +
            ApiSetNamespaceEntry->DataOffset);

        if (ApiSetValueArray->Count > 1 && ParentName) {

            HostLibraryEntry = ApiSetpSearchForApiSetHostV2(ApiSetValueArray,
                ParentName,
                ApiSetNamespace);
        }
        else {
            HostLibraryEntry = NULL;
        }

        if (HostLibraryEntry == NULL) {
            HostLibraryEntry = ApiSetValueArray->Array;
        }

        Output->Length = (USHORT)HostLibraryEntry->ValueLength;
        Output->MaximumLength = Output->Length;
        Output->Buffer = (WCHAR*)((ULONG_PTR)ApiSetNamespace + HostLibraryEntry->ValueOffset);

        Resolved = TRUE;

    } while (FALSE);

    return Resolved ? STATUS_SUCCESS : STATUS_APISET_NOT_PRESENT;
}
