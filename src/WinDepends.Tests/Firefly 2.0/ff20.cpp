/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       FF20.CPP
*
*  VERSION:     1.00
*
*  DATE:        11 Jul 2024
*
*  FIREFLY 20 entry point.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

#include "shared\global.h"
#include <objidl.h>

#pragma warning(disable: 4100) // unreferenced parameter

#include <DelayImp.h>
#pragma comment(lib, "Delayimp.lib")
#pragma comment (lib,"Gdiplus.lib")

#if defined(__cplusplus)
extern "C" {
#endif
__declspec(dllimport) DWORD WINAPI GdiplusStartup(
    OUT ULONG_PTR* token,
    const void* input,
    OUT void* output);
#if defined(__cplusplus)
}
#endif

class CBase
{
public:
    CBase() noexcept
    {
        OutputDebugString(L"[FF20] CBaseClass\r");
    }

    __declspec(dllexport) int BaseClassMethod(CBase a, CBase b) noexcept
    {
        return 0;
    }

    __declspec(dllexport) int BaseClassMethod2(int a, char b, void* p) noexcept
    {
        return 1;
    }
};

#if defined(__cplusplus)
extern "C" {
#endif
__declspec(dllexport) void GdiPlusStartup() noexcept
{
    GdiplusStartup(NULL, NULL, NULL);
}
#if defined(__cplusplus)
}
#endif

class CTestClass : CBase
{
    int x, y;

public:
    CTestClass() : CBase()
    {
        OutputDebugString(L"[FF20] CTestClass\r");
    }

    void SomeMethod() noexcept
    {
    }

    __declspec(dllexport) int TestClassMethod(CTestClass object, int a, int b, unsigned int c) noexcept
    {
        return 1;
    }

};

#if defined(__cplusplus)
extern "C" {
#endif
DWORD WINAPI Firefly20_DelayLoad()
{
    return ~GetTickCount();
}
#if defined(__cplusplus)
}
#endif

VOID WINAPI Firefly20_Export(
    VOID
)
{
}

BOOL WINAPI DllMain(
    _In_ HINSTANCE hinstDLL,
    _In_ DWORD fdwReason,
    _In_ LPVOID lpvReserved
)
{
    OutputDebugString(TEXT("[FF20] Loaded\r"));

    if (fdwReason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hinstDLL);
    }
    return TRUE;
}
