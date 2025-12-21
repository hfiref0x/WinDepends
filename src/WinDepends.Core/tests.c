/*
*  File: tests.c
*
*  Created on: Mar 07, 2025
*
*  Modified on: Mar 07, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team
*/

#include "core.h"

void test_api_setV6(PAPI_SET_NAMESPACE ApiSetNamespace)
{
    LPWSTR ToResolve6[] = {
       L"hui-ms-win-core-app-l1-2-3.dll",
        L"api-ms-win-nevedomaya-ebanaya-hyinua-l1-1-3.dll",
        L"api-ms-win-core-appinit-l1-1-0.dll",
        L"api-ms-win-core-com-private-l1-2-0",
        L"ext-ms-win-fs-clfs-l1-1-0.dll",
        L"ext-ms-win-core-app-package-registration-l1-1-1",
        L"ext-ms-win-shell-ntshrui-l1-1-0.dll",
        NULL,
        L"api-ms-win-core-psapi-l1-1-0.dll",
        L"api-ms-win-core-enclave-l1-1-1.dll",
        L"api-ms-onecoreuap-print-render-l1-1-0.dll",
        L"api-ms-win-deprecated-apis-advapi-l1-1-0.dll",
        L"api-ms-win-core-com-l2-1-1"
    };

    UNICODE_STRING Name, Resolved;
    WCHAR test[2000];

    SIZE_T length = 0;
    LPWSTR name = resolve_apiset_name(L"ext-ms-win-core-app-package-registration-l1-1-1", NULL, &length);
    if (name) {
        wprintf(L"DLL: %s\r\n", name);
    }
    gsup.RtlInitUnicodeString(&Name, L"ext-ms-win-core-app-package-registration-l1-1-1");
    if (NT_SUCCESS(ApiSetResolveToHostV6(ApiSetNamespace, &Name, NULL, &Resolved)))
    {
        StringCbCopyN(test, sizeof(test), Resolved.Buffer, Resolved.Length);
        wprintf(L"%s\r\n", test);
    }

    for (ULONG i = 0; i < RTL_NUMBER_OF(ToResolve6); i++) {

        gsup.RtlInitUnicodeString(&Name, ToResolve6[i]);

        if (NT_SUCCESS(ApiSetResolveToHostV6(ApiSetNamespace, &Name, NULL, &Resolved)))
        {
            StringCbCopyN(test, sizeof(test), Resolved.Buffer, Resolved.Length);
            wprintf(L"APISET V6: %s --> %s\r\n", ToResolve6[i], test);
        }
    }
}

void test_api_setV4(PAPI_SET_NAMESPACE ApiSetNamespace)
{
    LPWSTR ToResolve4[] = {
        L"API-MS-WIN-CORE-PROCESSTHREADS-L1-1-2.DLL",
        L"API-MS-WIN-CORE-KERNEL32-PRIVATE-L1-1-1.DLL",
        L"API-MS-WIN-CORE-PRIVATEPROFILE-L1-1-1.DLL",
        L"API-MS-WIN-CORE-SHUTDOWN-L1-1-1.DLL",
        L"API-MS-WIN-SERVICE-PRIVATE-L1-1-1.DLL",
        L"EXT-MS-WIN-MF-PAL-L1-1-0.DLL",
        L"EXT-MS-WIN-NTUSER-UICONTEXT-EXT-L1-1-0.DLL"
    };

    UNICODE_STRING Name, Resolved;
    WCHAR test[2000];

    for (ULONG i = 0; i < RTL_NUMBER_OF(ToResolve4); i++) {

        gsup.RtlInitUnicodeString(&Name, ToResolve4[i]);

        if (NT_SUCCESS(ApiSetResolveToHostV4(ApiSetNamespace, &Name, NULL, &Resolved)))
        {
            StringCbCopyN(test, sizeof(test), Resolved.Buffer, Resolved.Length);
            wprintf(L"APISET V4: %s --> %s\r\n", ToResolve4[i], test);
        }
    }
}

void test_api_setV2(PAPI_SET_NAMESPACE ApiSetNamespace)
{
    LPWSTR ToResolve2[] = {
        L"API-MS-Win-Core-Console-L1-1-0",
        L"API-MS-Win-Security-Base-L1-1-0",
        L"API-MS-Win-Core-Profile-L1-1-0.DLL",
        L"API-MS-Win-Core-Util-L1-1-0",
        L"API-MS-Win-Service-winsvc-L1-1-0",
        L"API-MS-Win-Core-ProcessEnvironment-L1-1-0",
        L"API-MS-Win-Core-Localization-L1-1-0.DLL",
        L"API-MS-Win-Security-LSALookup-L1-1-0",
        L"API-MS-Win-Service-Core-L1-1-0",
        L"API-MS-Win-Service-Management-L1-1-0",
        L"API-MS-Win-Service-Management-L2-1-0",
        L"API-MS-Win-Core-RtlSupport-L1-1-0",
        L"API-MS-Win-Core-Interlocked-L1-1-0.DLL"
    };

    UNICODE_STRING Name, Resolved;
    WCHAR test[2000];

    for (ULONG i = 0; i < RTL_NUMBER_OF(ToResolve2); i++) {

        gsup.RtlInitUnicodeString(&Name, ToResolve2[i]);

        if (NT_SUCCESS(ApiSetResolveToHostV2(ApiSetNamespace, &Name, NULL, &Resolved)))
        {
            StringCbCopyN(test, sizeof(test), Resolved.Buffer, Resolved.Length);
            wprintf(L"APISET V2: %s --> %s\r\n", ToResolve2[i], test);
        }
    }
}

void test_api_set()
{
    PAPI_SET_NAMESPACE ApiSetNamespace;
    HMODULE hModule = NULL;

    ApiSetNamespace = load_apiset_namespace(L"C:\\ApiSetSchema\\apisetschemaV6.dll", &hModule);
    if (ApiSetNamespace) {
        gsup.ApiSetMap = ApiSetNamespace;
    }
    else
    {
        return;
    }

    test_api_setV6(ApiSetNamespace);

    ApiSetNamespace = load_apiset_namespace(L"C:\\ApiSetSchema\\apisetschemaV4.dll", &hModule);
    if (ApiSetNamespace) {
        gsup.ApiSetMap = ApiSetNamespace;
    }
    else
    {
        return;
    }

    test_api_setV4(ApiSetNamespace);

    ApiSetNamespace = load_apiset_namespace(L"C:\\ApiSetSchema\\apisetschemaV2.dll", &hModule);
    if (ApiSetNamespace) {
        gsup.ApiSetMap = ApiSetNamespace;
    }
    else
    {
        return;
    }

    test_api_setV2(ApiSetNamespace);

}

