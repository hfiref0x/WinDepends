/*
*  File: tests-main.c
*
*  Created on: Jul 8, 2024
*
*  Modified on: Oct 01, 2024
*
*      Project: WinDepends.Core.Fuzz
*
*      Author:
*/

#define WIN32_LEAN_AND_MEAN

#include <Windows.h>
#include <shellapi.h>
#include <strsafe.h>

#pragma comment(lib, "Shell32.lib")

LPWSTR g_AppDir;
#define CORE_TEST L"WinDepends.Core.Tests.exe"
#ifdef _WIN64
#define CORE_APP  L"WinDepends.Core.x64.exe"
#else
#define CORE_APP  L"WinDepends.Core.x86.exe"
#endif


HANDLE StartCoreApp()
{
    WCHAR szCoreAppPath[1024];
    PROCESS_INFORMATION pi;
    STARTUPINFO si;

    StringCchPrintf(szCoreAppPath, ARRAYSIZE(szCoreAppPath), TEXT("%ws\\%ws"), g_AppDir, CORE_APP);

    ZeroMemory(&pi, sizeof(pi));
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);

    if (CreateProcess(NULL, szCoreAppPath, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
        printf("[FUZZ][OK] Server process executed successfully\n");
        CloseHandle(pi.hThread);
        return pi.hProcess;
    }
    else
    {
        printf("[FUZZ][ERROR] Executing server process failed\n");
    }

    return NULL;
}

DWORD WINAPI ThreadProc(HANDLE hChildOutRead)
{
    CHAR buffer[4096];
    DWORD bytesRead = 0;
    while (ReadFile(hChildOutRead, buffer, sizeof(buffer), &bytesRead, NULL) && bytesRead != 0) {
        printf("%.*s", bytesRead, buffer);
    }
    ExitThread(0);
}

void FuzzFromDirectory(LPWSTR directoryPath)
{
    WCHAR szFullPath[4096];
    WCHAR szSearchDir[MAX_PATH * 2];
    WIN32_FIND_DATA findFileData;
    HANDLE hFind, hProcess;
    STARTUPINFO si;
    PROCESS_INFORMATION pi;
    SECURITY_ATTRIBUTES saAttr;
    HANDLE hChildOutRead = NULL;
    HANDLE hChildOutWrite = NULL;

    printf("[FUZZ][OK] Starting fuzz loop\n");

    StringCchPrintf(szSearchDir, ARRAYSIZE(szSearchDir), L"%ws\\*", directoryPath);
    hFind = FindFirstFile(szSearchDir, &findFileData);
    if (hFind == INVALID_HANDLE_VALUE) {
        printf("[FUZZ][ERROR] Error FindFirstFile failed\n");
        return;
    }

    do {

        if (!(findFileData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) {

            hProcess = StartCoreApp();
            if (hProcess) {

                saAttr.nLength = sizeof(SECURITY_ATTRIBUTES);
                saAttr.bInheritHandle = TRUE;
                saAttr.lpSecurityDescriptor = NULL;

                if (CreatePipe(&hChildOutRead, &hChildOutWrite, &saAttr, 0)) {

                    SetHandleInformation(hChildOutRead, HANDLE_FLAG_INHERIT, 0);

                    StringCchPrintf(szFullPath, ARRAYSIZE(szFullPath), TEXT("%ws\\%ws %ws\\%ws"),
                        g_AppDir, CORE_TEST, directoryPath, findFileData.cFileName);

                    ZeroMemory(&si, sizeof(si));
                    ZeroMemory(&pi, sizeof(pi));

                    si.cb = sizeof(si);
                    si.hStdError = hChildOutWrite;
                    si.hStdOutput = hChildOutWrite;
                    si.dwFlags |= STARTF_USESTDHANDLES;

                    printf("\n=============================================================================\n");
                    wprintf(L"[FUZZ] File %s \n", findFileData.cFileName);
                    printf("=============================================================================\n");

                    // CreateProcess parameters
                    if (CreateProcess(NULL, szFullPath, NULL, NULL, TRUE, 0, NULL, NULL, &si, &pi)) {

                        SetConsoleTitle(szFullPath);

                        CloseHandle(hChildOutWrite);
                        hChildOutWrite = NULL;

                        DWORD tid;
                        HANDLE hThread = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)ThreadProc, (LPVOID)hChildOutRead, 0, &tid);
                        if (hThread) {
                            if (WaitForSingleObject(hThread, 2 * 1000) == WAIT_TIMEOUT) {
                                printf("\n[FUZZ][ERROR] Read thread timeout reached, terminating thread\n");
                                TerminateThread(hThread, 0);
                                CloseHandle(hThread);
                            }
                        }

                        if (WaitForSingleObject(pi.hProcess, 2 * 1000) == WAIT_TIMEOUT) {
                            printf("\n[FUZZ][ERROR] Timeout reached, terminating test application\n");
                            TerminateProcess(pi.hProcess, (DWORD)ERROR_TIMEOUT);
                        }

                        CloseHandle(pi.hProcess);
                        CloseHandle(pi.hThread);
                    }
                    TerminateProcess(hProcess, 0);
                    CloseHandle(hProcess);
                    CloseHandle(hChildOutRead);
                    if (hChildOutWrite) {
                        CloseHandle(hChildOutWrite);
                    }

                    printf("[FUZZ][OK] Server process terminated successfully\n");
                }
            }
        }

    } while (FindNextFile(hFind, &findFileData) != 0);

    printf("[FUZZ][OK] Completed!\n");

    FindClose(hFind);
}

void main()
{
    LPWSTR* szArglist;
    int     nArgs;

    szArglist = CommandLineToArgvW(GetCommandLineW(), &nArgs);
    if (szArglist) {
        if (nArgs > 2) {
            g_AppDir = szArglist[1];
            FuzzFromDirectory(szArglist[2]);
        }

        LocalFree((HLOCAL)szArglist);
    }
}
