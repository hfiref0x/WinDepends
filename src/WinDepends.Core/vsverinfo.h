/*
Module name:
    vsverinfo.h

Description:
    Custom version info structure parser

Date:
    21 Jun 2020
*/

#pragma once

#ifndef _VSVERINFO_
#define _VSVERINFO_

#include <Windows.h>

typedef BOOL(__stdcall* PEnumStringInfoCallback)(PWCHAR key, PWCHAR value, PWCHAR langid, LPVOID cbparam);
typedef BOOL(__stdcall* PEnumVarInfoCallback)(PWCHAR key, DWORD value, LPVOID cbparam);

typedef struct _IMGVSHDR {
    WORD    wLength;
    WORD    wValueLength;
    WORD    wType;
} IMGVSHDR, * PIMGVSHDR;

typedef struct _IMGVSVERSIONINFO {
    IMGVSHDR    vshdr;
    WCHAR       wIdString[17];
} IMGVSVERSIONINFO, * PIMGVSVERSIONINFO;

typedef struct _IMGSTRINGINFO {
    IMGVSHDR    vshdr;
    WCHAR       wIdKey[15];
} IMGSTRINGINFO, * PIMGSTRINGINFO;

typedef struct _IMGVARINFO {
    IMGVSHDR    vshdr;
    WCHAR       wIdKey[13];
} IMGVARINFO, * PIMGVARINFO;

typedef struct _IMGSTRINGTABLE {
    IMGVSHDR    vshdr;
    WCHAR       wIdKey[9];
} IMGSTRINGTABLE, * PIMGSTRINGTABLE;

typedef struct _IMGVSTRING {
    IMGVSHDR    vshdr;
    WCHAR       szKey[1];
} IMGVSTRING, * PIMGVSTRING;

VS_FIXEDFILEINFO* PEImageEnumVersionFields(
    HMODULE                     module,
    PEnumStringInfoCallback     scallback,
    PEnumVarInfoCallback        vcallback,
    PVOID cbparam);

#endif /* _VSVERINFO_ */