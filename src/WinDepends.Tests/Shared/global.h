/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       GLOBAL.H
*
*  VERSION:     1.00
*
*  DATE:        04 Jul 2024
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
#pragma once

#if !defined UNICODE
#error ANSI build is not supported
#endif

//disable nonmeaningful warnings.
#pragma warning(push)
#pragma warning(disable: 4005) // macro redefinition
#pragma warning(disable: 4055) // %s : from data pointer %s to function pointer %s
#pragma warning(disable: 4201) // nonstandard extension used : nameless struct/union

#include <Windows.h>

#pragma warning(pop)