/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       FF22.CPP
*
*  VERSION:     1.00
*
*  DATE:        15 Jul 2024
*
*  FIREFLY 22 entry point.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
#include "shared\global.h"
#include <StrSafe.h>

#ifdef _M_X64
#pragma comment(lib, "..\\bin\\ff20_64.lib")
#endif

#if defined(__cplusplus)
extern "C" {
#endif
    __declspec(dllimport) void GdiPlusStartup();
#if defined(__cplusplus)
}
#endif

PVOID QueryActCtxInformation(
    HANDLE ActivationContext,
    ACTIVATIONCONTEXTINFOCLASS InfoClass, 
    ULONG AssemblyIndex, 
    ULONG FileIndex)
{
    PVOID pvData = NULL;
    SIZE_T cbRequired;
    SIZE_T cbAvailable = 0;

    ACTIVATION_CONTEXT_QUERY_INDEX QueryIndex;

    QueryIndex.ulAssemblyIndex = AssemblyIndex;
    QueryIndex.ulFileIndexInAssembly = FileIndex;

    QueryActCtxW(0,
        ActivationContext,
        &QueryIndex,
        InfoClass,
        pvData,
        cbAvailable,
        &cbRequired);

    if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
    {
        pvData = (PVOID)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, cbRequired);
        if (pvData) {

            cbAvailable = cbRequired;

            if (QueryActCtxW(0,
                ActivationContext,
                &QueryIndex,
                InfoClass,
                pvData,
                cbAvailable,
                &cbRequired))
            {
                return pvData;
            }
            else
            {
                HeapFree(GetProcessHeap(), 0, pvData);
                pvData = NULL;
            }
        }
    }

    return pvData;
}

void QueryFilePathFromActivationContext(LPWSTR lpFileName, HANDLE hActivationContext)
{
    PACTIVATION_CONTEXT_DETAILED_INFORMATION pvCtxDetails;
    PACTIVATION_CONTEXT_ASSEMBLY_DETAILED_INFORMATION pvAssemblyDetails;

    pvCtxDetails = (PACTIVATION_CONTEXT_DETAILED_INFORMATION)QueryActCtxInformation(hActivationContext,
        ActivationContextDetailedInformation,
        1, 0);

    if (pvCtxDetails) {

        for (DWORD i = 1; i < pvCtxDetails->ulAssemblyCount; i++)
        {
            pvAssemblyDetails = (PACTIVATION_CONTEXT_ASSEMBLY_DETAILED_INFORMATION)QueryActCtxInformation(hActivationContext,
                AssemblyDetailedInformationInActivationContext,
                i, 0);
            if (pvAssemblyDetails && pvAssemblyDetails->ulAssemblyDirectoryNameLength) {

                SIZE_T sz;
                LPWSTR lpSxsPath;

                sz = (MAX_PATH + wcslen(pvAssemblyDetails->lpAssemblyDirectoryName)
                    + wcslen(lpFileName)) * sizeof(WCHAR);

                lpSxsPath = (LPWSTR)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sz);
                if (lpSxsPath) {

                    StringCchPrintf(lpSxsPath, sz / sizeof(WCHAR), TEXT("C:\\Windows\\WinSxs\\%s"),
                        pvAssemblyDetails->lpAssemblyDirectoryName);

                    DWORD dwSize = 0;
                    DWORD dwLength = SearchPath(lpSxsPath, lpFileName, NULL, dwSize, NULL, NULL);
                    if (dwLength > 0)
                    {
                        LPWSTR lpFinalPath = (LPWSTR)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, dwLength * sizeof(WCHAR));
                        if (lpFinalPath) {

                            if (SearchPath(lpSxsPath, lpFileName, NULL, dwLength, lpFinalPath, NULL))
                            {
                                OutputDebugString(lpFinalPath);
                                OutputDebugString(L"\r");
                            }

                            HeapFree(GetProcessHeap(), 0, lpFinalPath);
                        }
                       
                    }
                    HeapFree(GetProcessHeap(), 0, lpSxsPath);
                }

            }

            HeapFree(GetProcessHeap(), 0, pvAssemblyDetails);

        }

        HeapFree(GetProcessHeap(), 0, pvCtxDetails);
    }
}

int main()
{
    OutputDebugString(L"[FF22] Loaded\r");

    ACTCTX ctx;
    ZeroMemory(&ctx, sizeof(ACTCTX));

    ctx.cbSize = sizeof(ACTCTX);
    ctx.dwFlags = ACTCTX_FLAG_HMODULE_VALID | ACTCTX_FLAG_RESOURCE_NAME_VALID;
    ctx.hModule = GetModuleHandle(NULL);
    ctx.lpResourceName = CREATEPROCESS_MANIFEST_RESOURCE_ID;

    HANDLE hCtx = CreateActCtx(&ctx);
    if (hCtx != INVALID_HANDLE_VALUE) {

        LPWSTR szModules[] = { (LPWSTR)L"kernel32.dll",
            (LPWSTR)L"ntdll.dll", (LPWSTR)L"comctl32.dll", (LPWSTR)L"shell32.dll", (LPWSTR)L"lzx32.sys" };

        WCHAR buffer[MAX_PATH];
        ULONG_PTR cookie = 0;

        for (int i = 0; i < ARRAYSIZE(szModules); i++) {

            ULONG length = MAX_PATH;

            ZeroMemory(&buffer, sizeof(buffer));
            ActivateActCtx(&ctx, (ULONG_PTR*)&cookie);
            length = SearchPathW(NULL, szModules[i], NULL, length, buffer, NULL);
            if (length != 0) {
                OutputDebugString(buffer);
                OutputDebugString(L"\r");
            }
            DeactivateActCtx(0, cookie);

        }
        ReleaseActCtx(hCtx);
    }
    else {
        DWORD dwError = GetLastError();
        if (dwError == ERROR_SXS_CANT_GEN_ACTCTX) {
            DebugBreak();
        }
    }
    return 0;
}
