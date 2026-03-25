/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       FF23.CPP
*
*  VERSION:     1.00
*
*  DATE:        11 Jul 2024
*
*  FIREFLY 23 entry point.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

#include "shared\global.h"
#include <objidl.h>
#include <gdiplus.h>
#pragma comment (lib,"Gdiplus.lib")
using namespace Gdiplus;

#pragma warning(disable: 4100) // unreferenced parameter


#include <DelayImp.h>
#pragma comment(lib, "Delayimp.lib")

#ifdef _M_IX86
#pragma comment(lib, "..\\bin\\ff20_32.lib")
#endif

#ifdef _M_X64
#pragma comment(lib, "..\\bin\\ff20_64.lib")
#endif

#ifdef _M_ARM
#pragma comment(lib, "..\\bin\\ff20_arm.lib")
#endif

#ifdef _M_ARM64
#pragma comment(lib, "..\\bin\\ff20_arm64.lib")
#endif

#ifdef _M_ARM64EC
#pragma comment(lib, "..\\bin\\ff20_arm64ec.lib")
#endif

#ifndef _M_ARM64EC
#pragma comment(linker, " /EXPORT:HeapAlloc=ntdll.RtlAllocateHeap")
#pragma comment(linker, " /EXPORT:timeBeginPeriod=\\\\?\\globalroot\\systemroot\\system32\\winmm.timeBeginPeriod")
#pragma comment(linker, " /EXPORT:timeEndPeriod=\\\\?\\globalroot\\systemroot\\system32\\winmm.timeEndPeriod")
#pragma comment(linker, " /EXPORT:InvalidForward0=\\\\?\\globalroot\\systemroot\\system32\\kernel32.InvalidFunction")
#pragma comment(linker, " /EXPORT:InvalidForward1=ernel32.CreateFile")
#pragma comment(linker, " /EXPORT:InvalidForward2=kernel32.dll.InvalidFunction")
#pragma comment(linker, " /EXPORT:InvalidForward3=kernel32.InvalidFunction")
#pragma comment(linker, " /EXPORT:InvalidForward4=\\127.0.0.1\\c:\\windows\\system.Function")
#endif

class CTestClass
{
    int x, y;

public:
    CTestClass()
    {
        OutputDebugString(L"[FF23] CTestClass\r");
    }

    __declspec(dllexport) int ExportedMethod(CTestClass object, int value, int* result, bool test)
    {
        OutputDebugString(L"[FF23] ExportedMethod\r");

        GdiplusStartupInput gdiplusStartupInput;
        ULONG_PTR           gdiplusToken;

        GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, NULL);
        return 0;
    }

};

#if defined(__cplusplus)
extern "C" {
#endif
    __declspec(dllimport) DWORD WINAPI Firefly20_DelayLoad();
#if defined(__cplusplus)
}
#endif

#if defined(__cplusplus)
extern "C" {
#endif
VOID WINAPI Firefly23_Export(
    VOID
)
{
    OutputDebugString(L"[FF23] Firefly20 called\r");
    Firefly20_DelayLoad();
}
#if defined(__cplusplus)
}
#endif

BOOL WINAPI DllMain(
    _In_ HINSTANCE hinstDLL,
    _In_ DWORD fdwReason,
    _In_ LPVOID lpvReserved
)
{
    UNREFERENCED_PARAMETER(lpvReserved);

    OutputDebugString(TEXT("[FF23] Loaded\r"));

    if (fdwReason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hinstDLL);
    }
    return TRUE;
}
