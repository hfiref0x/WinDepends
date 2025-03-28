/*
Module name:
    vsverinfo.c

Description:
    Custom version info structure parser

Date:
    21 Jun 2020
*/

#include "vsverinfo.h"

ULONG_PTR __inline ALIGN_UP_32(ULONG_PTR p)
{
    return (p + 3) & (~(ULONG_PTR)3);
}

BOOL PEImageEnumVarFileInfo(PIMGVSTRING hdr, PEnumVarInfoCallback vcallback, PVOID cbparam)
{
    ULONG_PTR   vlimit = (ULONG_PTR)hdr + hdr->vshdr.wLength;
    PDWORD      value;
    DWORD       uzero = 0;

    for (
        // first child structure
        hdr = (PIMGVSTRING)ALIGN_UP_32((ULONG_PTR)hdr + sizeof(IMGVARINFO));
        (ULONG_PTR)hdr < vlimit;
        hdr = (PIMGVSTRING)ALIGN_UP_32((ULONG_PTR)hdr + hdr->vshdr.wLength))
    {
        if (hdr->vshdr.wValueLength == 0)
            value = &uzero;
        else
            value = (PDWORD)ALIGN_UP_32((ULONG_PTR)&hdr->szKey + (1 + wcslen(hdr->szKey)) * sizeof(WCHAR));

        if (!vcallback(hdr->szKey, *value, cbparam))
            return FALSE;
    }

    return TRUE;
}

BOOL PEImageEnumStrings(PIMGVSTRING hdr, PEnumStringInfoCallback callback, PWCHAR langid, PVOID cbparam)
{
    ULONG_PTR   vlimit = (ULONG_PTR)hdr + hdr->vshdr.wLength;
    PWCHAR      value;

    for (
        // first child structure
        hdr = (PIMGVSTRING)ALIGN_UP_32((ULONG_PTR)hdr + sizeof(IMGSTRINGTABLE));
        (ULONG_PTR)hdr < vlimit;
        hdr = (PIMGVSTRING)ALIGN_UP_32((ULONG_PTR)hdr + hdr->vshdr.wLength))
    {
        if (hdr->vshdr.wValueLength == 0)
            value = L"";
        else
            value = (PWCHAR)ALIGN_UP_32((ULONG_PTR)&hdr->szKey + (1 + wcslen(hdr->szKey)) * sizeof(WCHAR));

        if (!callback(hdr->szKey, value, langid, cbparam))
            return FALSE;
    }

    return TRUE;
}

BOOL PEImageEnumStringFileInfo(PIMGSTRINGTABLE hdr, PEnumStringInfoCallback callback, PVOID cbparam)
{
    ULONG_PTR   vlimit = (ULONG_PTR)hdr + hdr->vshdr.wLength;

    for (
        // first child structure
        hdr = (PIMGSTRINGTABLE)ALIGN_UP_32((ULONG_PTR)hdr + sizeof(IMGSTRINGINFO));
        (ULONG_PTR)hdr < vlimit;
        hdr = (PIMGSTRINGTABLE)ALIGN_UP_32((ULONG_PTR)hdr + hdr->vshdr.wLength))
    {
        if (!PEImageEnumStrings((PIMGVSTRING)hdr, callback, hdr->wIdKey, cbparam))
            return FALSE;
    }

    return TRUE;
}

VS_FIXEDFILEINFO* PEImageEnumVersionFields(
    HMODULE module,
    PEnumStringInfoCallback scallback,
    PEnumVarInfoCallback vcallback,
    PVOID cbparam)
{
    HRSRC       hvres = FindResourceEx(module, RT_VERSION, MAKEINTRESOURCE(1), MAKELANGID(LANG_NEUTRAL, SUBLANG_NEUTRAL));
    HGLOBAL     rptr;
    ULONG_PTR   vlimit;

    VS_FIXEDFILEINFO* vinfo = NULL;
    PIMGVSVERSIONINFO   hdr;

    while (hvres)
    {
        rptr = LoadResource(module, hvres);
        if (!rptr)
            return NULL;

        // root structure
        hdr = (PIMGVSVERSIONINFO)rptr;
        vlimit = (ULONG_PTR)hdr + hdr->vshdr.wLength;

        if (hdr->vshdr.wValueLength)
            vinfo = (VS_FIXEDFILEINFO*)((ULONG_PTR)hdr + sizeof(IMGVSVERSIONINFO));

        if (!(scallback || vcallback))
            break;

        for (
            // first child structure
            hdr = (PIMGVSVERSIONINFO)ALIGN_UP_32((ULONG_PTR)hdr + hdr->vshdr.wValueLength + sizeof(IMGVSVERSIONINFO));
            (ULONG_PTR)hdr < vlimit;
            hdr = (PIMGVSVERSIONINFO)ALIGN_UP_32((ULONG_PTR)hdr + hdr->vshdr.wLength))
        {

            if ((wcscmp(hdr->wIdString, L"StringFileInfo") == 0) && scallback)
                if (!PEImageEnumStringFileInfo((PIMGSTRINGTABLE)hdr, scallback, cbparam))
                    break;

            if ((wcscmp(hdr->wIdString, L"VarFileInfo") == 0) && vcallback)
                if (!PEImageEnumVarFileInfo((PIMGVSTRING)hdr, vcallback, cbparam))
                    break;
        }

        break;
    }

    return vinfo;
}
