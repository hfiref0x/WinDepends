/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       FF21.CPP
*
*  VERSION:     1.00
*
*  DATE:        15 Jul 2024
*
*  FIREFLY 21 entry point.
* 
*  MANIFEST ASSEMBLIES TESTS
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

#ifdef _M_IX86
#pragma comment(lib, "..\\bin\\ff23_32.lib")
#endif

#ifdef _M_X64
#pragma comment(lib, "..\\bin\\ff23_64.lib")
#endif

#ifdef _M_ARM
#pragma comment(lib, "..\\bin\\ff23_arm.lib")
#endif

#ifdef _M_ARM64
#pragma comment(lib, "..\\bin\\ff23_arm64.lib")
#endif

#ifdef _M_ARM64EC
#pragma comment(lib, "..\\bin\\ff23_arm64ec.lib")
#endif

#if defined(__cplusplus)
extern "C" {
#endif
    __declspec(dllimport) VOID WINAPI Firefly23_Export();
#if defined(__cplusplus)
}
#endif

int main()
{
    GdiplusStartupInput gdiplusStartupInput;
    ULONG_PTR           gdiplusToken;

    OutputDebugString(L"[FF21] Loaded\r");

    Firefly23_Export();

    GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, NULL);
    return 0;
}
