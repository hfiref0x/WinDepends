/*
*  File: main.c
*
*  Created on: Jul 8, 2024
*
*  Modified on: Aug 31, 2025
*
*      Project: WinDepends.Core.Fuzz
*
*      Author: WinDepends authors
*/

#define WIN32_LEAN_AND_MEAN

#include <Windows.h>
#include <shellapi.h>
#include <strsafe.h>

#pragma comment(lib, "Shell32.lib")

LPWSTR g_AppDir;
static BOOL g_EnableJsonValidation = TRUE;
static volatile LONG g_TotalFiles = 0;
static volatile LONG g_FailedFiles = 0;
static volatile LONG g_JsonTotal = 0;
static volatile LONG g_JsonValid = 0;
static volatile LONG g_JsonInvalid = 0;
static volatile LONG g_JsonTruncated = 0;

#define CORE_TEST L"WinDepends.Core.Tests.exe"
#ifdef _WIN64
#define CORE_APP  L"WinDepends.Core.x64.exe"
#else
#define CORE_APP  L"WinDepends.Core.x86.exe"
#endif

/* ===================== JSON VALIDATION ===================== */

static void JsonSkipWs(const char** p, const char* end)
{
    while (*p < end) {
        char c = **p;
        if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            (*p)++;
        else
            break;
    }
}

static int JsonParseString(const char** p, const char* end)
{
    const char* s;
    unsigned i;
    if (*p >= end || **p != '\"') return 0;
    (*p)++;
    s = *p;
    while (s < end) {
        char c = *s++;
        if ((unsigned char)c < 0x20) return 0;
        if (c == '\\') {
            if (s >= end) return 0;
            c = *s++;
            switch (c) {
            case '\"': case '\\': case '/':
            case 'b': case 'f': case 'n':
            case 'r': case 't':
                break;
            case 'u':
                for (i = 0; i < 4; i++) {
                    if (s >= end) return 0;
                    c = *s++;
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                        return 0;
                }
                break;
            default:
                return 0;
            }
        }
        else if (c == '\"') {
            *p = s;
            return 1;
        }
    }
    return 0;
}

static int JsonParseNumber(const char** p, const char* end)
{
    const char* s = *p;
    int digits = 0;
    if (s < end && (*s == '-')) s++;
    if (s < end && *s == '0') {
        s++;
    }
    else {
        if (s >= end || *s < '1' || *s > '9') return 0;
        while (s < end && *s >= '0' && *s <= '9') s++;
    }
    if (s < end && *s == '.') {
        s++;
        if (s >= end || *s < '0' || *s > '9') return 0;
        while (s < end && *s >= '0' && *s <= '9') s++;
    }
    if (s < end && (*s == 'e' || *s == 'E')) {
        s++;
        if (s < end && (*s == '+' || *s == '-')) s++;
        if (s >= end || *s < '0' || *s > '9') return 0;
        while (s < end && *s >= '0' && *s <= '9') s++;
    }
    digits = (int)(s - *p);
    if (digits <= 0) return 0;
    *p = s;
    return 1;
}

static int JsonParseLiteral(const char** p, const char* end, const char* lit, size_t len)
{
    if ((size_t)(end - *p) < len) return 0;
    if (memcmp(*p, lit, len) == 0) {
        *p += len;
        return 1;
    }
    return 0;
}

static int JsonParseValue(const char** p, const char* end, int depth);

static int JsonParseArray(const char** p, const char* end, int depth)
{
    if (**p != '[') return 0;
    (*p)++;
    JsonSkipWs(p, end);
    if (*p >= end) return 0;
    if (**p == ']') {
        (*p)++;
        return 1;
    }
    for (;;) {
        if (!JsonParseValue(p, end, depth + 1)) return 0;
        JsonSkipWs(p, end);
        if (*p >= end) return 0;
        if (**p == ']') {
            (*p)++;
            return 1;
        }
        if (**p != ',') return 0;
        (*p)++;
        JsonSkipWs(p, end);
        if (*p >= end) return 0;
    }
}

static int JsonParseObject(const char** p, const char* end, int depth)
{
    if (**p != '{') return 0;
    (*p)++;
    JsonSkipWs(p, end);
    if (*p >= end) return 0;
    if (**p == '}') {
        (*p)++;
        return 1;
    }
    for (;;) {
        if (!JsonParseString(p, end)) return 0;
        JsonSkipWs(p, end);
        if (*p >= end || **p != ':') return 0;
        (*p)++;
        JsonSkipWs(p, end);
        if (!JsonParseValue(p, end, depth + 1)) return 0;
        JsonSkipWs(p, end);
        if (*p >= end) return 0;
        if (**p == '}') {
            (*p)++;
            return 1;
        }
        if (**p != ',') return 0;
        (*p)++;
        JsonSkipWs(p, end);
        if (*p >= end) return 0;
    }
}

static int JsonParseValue(const char** p, const char* end, int depth)
{
    if (depth > 128) return 0;
    JsonSkipWs(p, end);
    if (*p >= end) return 0;
    switch (**p) {
    case '{':
        return JsonParseObject(p, end, depth);
    case '[':
        return JsonParseArray(p, end, depth);
    case '\"':
        return JsonParseString(p, end);
    case 't':
        return JsonParseLiteral(p, end, "true", 4);
    case 'f':
        return JsonParseLiteral(p, end, "false", 5);
    case 'n':
        return JsonParseLiteral(p, end, "null", 4);
    default:
        if ((**p == '-') || (**p >= '0' && **p <= '9'))
            return JsonParseNumber(p, end);
        return 0;
    }
}

static int ValidateJsonStrict(const char* line, size_t len)
{
    const char* p = line;
    const char* end = line + len;
    JsonSkipWs(&p, end);
    if (p >= end) return 0;
    if (*p != '{' && *p != '[') return 0;
    if (!JsonParseValue(&p, end, 0)) return 0;
    JsonSkipWs(&p, end);
    return p == end;
}

static int IsLikelyJson(const char* line, size_t len)
{
    size_t i;
    while (len && (line[0] == ' ' || line[0] == '\t' || line[0] == '\r' || line[0] == '\n'))
        line++, len--;
    while (len && (line[len - 1] == ' ' || line[len - 1] == '\t' || line[len - 1] == '\r' || line[len - 1] == '\n'))
        len--;
    if (len < 2) return 0;
    if ((line[0] != '{' && line[0] != '[') || (line[len - 1] != '}' && line[len - 1] != ']')) return 0;
    for (i = 0; i < len; i++) {
        unsigned char c = (unsigned char)line[i];
        if (c < 0x20 && c != '\t' && c != '\n' && c != '\r')
            return 0;
    }
    return 1;
}

static void ValidateAndReportJsonWithFlag(const char* line, size_t len, BOOL truncated)
{
    int ok;
    if (!g_EnableJsonValidation) return;

    if (!truncated) {
        if (!IsLikelyJson(line, len)) return;
    }

    InterlockedIncrement(&g_JsonTotal);
    ok = ValidateJsonStrict(line, len);
    if (ok) {
        InterlockedIncrement(&g_JsonValid);
        if (truncated) {
            printf("[FUZZ][JSON][OK] [TRUNCATED]\n");
        }
        else {
            printf("[FUZZ][JSON][OK]\n");
        }
    }
    else {
        InterlockedIncrement(&g_JsonInvalid);
        if (truncated) {
            InterlockedIncrement(&g_JsonTruncated);
            printf("[FUZZ][JSON][INVALID] [TRUNCATED]\n");
        }
        else {
            printf("[FUZZ][JSON][INVALID]\n");
        }
    }
}

static void ValidateAndReportJson(const char* line, size_t len)
{
    ValidateAndReportJsonWithFlag(line, len, FALSE);
}

/* ===================== END JSON VALIDATION ===================== */

HANDLE StartCoreApp()
{
    HRESULT hr;
    PROCESS_INFORMATION pi;
    STARTUPINFO si;
    WCHAR szCoreAppPath[MAX_PATH * 2];
    WCHAR szCmdLine[MAX_PATH * 2];

    ZeroMemory(&pi, sizeof(pi));
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);

    hr = StringCchPrintf(szCoreAppPath, ARRAYSIZE(szCoreAppPath), TEXT("%ws\\%ws"), g_AppDir, CORE_APP);
    if (FAILED(hr))
        return NULL;

    hr = StringCchPrintf(szCmdLine, ARRAYSIZE(szCmdLine), L"\"%ws\"", szCoreAppPath);
    if (FAILED(hr))
        return NULL;

    if (CreateProcess(szCoreAppPath, szCmdLine, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
        printf("[FUZZ][OK] Server process executed successfully\n");
        CloseHandle(pi.hThread);
        return pi.hProcess;
    }
    else
    {
        printf("[FUZZ][ERROR] Executing server process failed (err=%lu)\n", GetLastError());
    }

    return NULL;
}

DWORD WINAPI ThreadProc(LPVOID Param)
{
    HANDLE hChildOutRead = (HANDLE)Param;
    CHAR chunk[4096];
    CHAR* pszLineBuf = NULL;
    DWORD bytesRead;
    SIZE_T lineLen = 0, i;
    SIZE_T cchLineBuf = 1 * (1024 * 1024);
    SIZE_T cchLineBufMax = 64 * (1024 * 1024);
    BOOL truncatedLine = FALSE;

    pszLineBuf = (CHAR*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, cchLineBuf);
    if (pszLineBuf) {

        for (;;) {
            if (!ReadFile(hChildOutRead, chunk, sizeof(chunk), &bytesRead, NULL) || bytesRead == 0)
                break;

            for (i = 0; i < bytesRead; i++) {
                char c = chunk[i];

                if (c == '\n') {
                    pszLineBuf[lineLen] = 0;
                    printf("%s\n", pszLineBuf);
                    ValidateAndReportJsonWithFlag(pszLineBuf, lineLen, truncatedLine);
                    lineLen = 0;
                    truncatedLine = FALSE;
                }
                else if (c != '\r') {
                    if (lineLen + 1 >= cchLineBuf) {
                        SIZE_T newCap;
                        CHAR* pNew;

                        if (cchLineBuf < cchLineBufMax) {
                            newCap = cchLineBuf * 2;
                            if (newCap > cchLineBufMax) newCap = cchLineBufMax;
                            pNew = (CHAR*)HeapReAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, pszLineBuf, newCap);
                            if (pNew) {
                                pszLineBuf = pNew;
                                cchLineBuf = newCap;
                            }
                            else {
                                truncatedLine = TRUE;
                            }
                        }
                        else {
                            truncatedLine = TRUE;
                        }

                        if (truncatedLine) {
                            pszLineBuf[lineLen] = 0;
                            printf("%s\n", pszLineBuf);
                            ValidateAndReportJsonWithFlag(pszLineBuf, lineLen, TRUE);
                            lineLen = 0;
                            truncatedLine = TRUE;
                        }
                    }

                    if (lineLen + 1 < cchLineBuf) {
                        pszLineBuf[lineLen++] = c;
                    }
                }
            }
            fflush(stdout);
        }

        if (lineLen) {
            pszLineBuf[lineLen] = 0;
            printf("%s\n", pszLineBuf);
            ValidateAndReportJsonWithFlag(pszLineBuf, lineLen, truncatedLine);
        }

        HeapFree(GetProcessHeap(), 0, pszLineBuf);
    }
    return 0;
}

static void PrintSummary(void)
{
    LONG totalFiles = g_TotalFiles;
    LONG failedFiles = g_FailedFiles;
    LONG jsonTotal = g_JsonTotal;
    LONG jsonValid = g_JsonValid;
    LONG jsonInvalid = g_JsonInvalid;
    LONG jsonTruncated = g_JsonTruncated;

    printf("[FUZZ][SUMMARY] Files=%ld FailedFiles=%ld JSON_Total=%ld JSON_Valid=%ld JSON_Invalid=%ld JSON_Truncated=%ld\n",
        totalFiles, failedFiles, jsonTotal, jsonValid, jsonInvalid, jsonTruncated);
}

void FuzzFromDirectory(LPWSTR directoryPath)
{
    HRESULT hr;
    DWORD waitResult = 0, tid, cchFullPath = MAX_PATH * 2, cchCmdLine = MAX_PATH * 4;
    DWORD exitCode = 0;
    HANDLE hChildOutRead, hChildOutWrite, hThread = NULL;
    HANDLE hFind = INVALID_HANDLE_VALUE, hServerProcess = NULL;
    LPWSTR ext;
    WCHAR* pszFullPath = NULL;
    WCHAR* pszCmdLine = NULL;
    WIN32_FIND_DATA findFileData;
    STARTUPINFO si;
    PROCESS_INFORMATION pi;
    SECURITY_ATTRIBUTES saAttr;
    WCHAR szSearchDir[MAX_PATH * 2];
    WCHAR szTestExePath[MAX_PATH * 2];

    __try {
        printf("[FUZZ][OK] Starting fuzz loop\n");

        pszFullPath = (WCHAR*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, cchFullPath * sizeof(WCHAR));
        pszCmdLine = (WCHAR*)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, cchCmdLine * sizeof(WCHAR));
        if (pszCmdLine == NULL || pszFullPath == NULL) {
            printf("[FUZZ][ERROR] Memory allocation failed\n");
            __leave;
        }

        hr = StringCchPrintf(szSearchDir, ARRAYSIZE(szSearchDir), L"%ws\\*", directoryPath);
        if (FAILED(hr)) {
            printf("[FUZZ][ERROR] Path too long\n");
            __leave;
        }

        hFind = FindFirstFile(szSearchDir, &findFileData);
        if (hFind == INVALID_HANDLE_VALUE) {
            printf("[FUZZ][ERROR] Error FindFirstFile failed (err=%lu)\n", GetLastError());
            __leave;
        }

        hr = StringCchPrintf(szTestExePath, ARRAYSIZE(szTestExePath), L"%ws\\%ws", g_AppDir, CORE_TEST);
        if (FAILED(hr)) {
            FindClose(hFind);
            __leave;
        }

        do {

            if (findFileData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
                continue;

            // Skip minidumps in test directory.
            ext = wcsrchr(findFileData.cFileName, L'.');
            if (ext && (_wcsicmp(ext, L".dmp") == 0))
                continue;

            hServerProcess = StartCoreApp();
            if (!hServerProcess)
                continue;

            ZeroMemory(&saAttr, sizeof(saAttr));
            saAttr.nLength = sizeof(SECURITY_ATTRIBUTES);
            saAttr.bInheritHandle = TRUE;

            hChildOutRead = NULL;
            hChildOutWrite = NULL;

            if (!CreatePipe(&hChildOutRead, &hChildOutWrite, &saAttr, 0)) {
                printf("[FUZZ][ERROR] CreatePipe failed (err=%lu)\n", GetLastError());
                TerminateProcess(hServerProcess, 0);
                CloseHandle(hServerProcess);
                continue;
            }

            SetHandleInformation(hChildOutRead, HANDLE_FLAG_INHERIT, 0);

            hr = StringCchPrintf(pszFullPath, cchFullPath, L"%ws\\%ws", directoryPath, findFileData.cFileName);
            if (FAILED(hr)) {
                printf("[FUZZ][ERROR] Full path too long, skipping\n");
                CloseHandle(hChildOutRead);
                CloseHandle(hChildOutWrite);
                TerminateProcess(hServerProcess, 0);
                CloseHandle(hServerProcess);
                continue;
            }

            hr = StringCchPrintf(pszCmdLine, cchCmdLine, L"\"%ws\" \"%ws\"", szTestExePath, pszFullPath);
            if (FAILED(hr)) {
                printf("[FUZZ][ERROR] Command line too long, skipping\n");
                CloseHandle(hChildOutRead);
                CloseHandle(hChildOutWrite);
                TerminateProcess(hServerProcess, 0);
                CloseHandle(hServerProcess);
                continue;
            }

            ZeroMemory(&si, sizeof(si));
            ZeroMemory(&pi, sizeof(pi));
            si.cb = sizeof(si);
            si.hStdError = hChildOutWrite;
            si.hStdOutput = hChildOutWrite;
            si.dwFlags = STARTF_USESTDHANDLES;

            printf("\n=============================================================================\n");
            wprintf(L"[FUZZ] File %s \n", findFileData.cFileName);
            printf("=============================================================================\n");

            if (CreateProcess(szTestExePath, pszCmdLine, NULL, NULL, TRUE, 0, NULL, NULL, &si, &pi)) {

                SetConsoleTitle(pszFullPath);

                CloseHandle(hChildOutWrite);
                hChildOutWrite = NULL;

                hThread = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)ThreadProc, (LPVOID)hChildOutRead, 0, &tid);
                if (hThread == NULL) {
                    printf("[FUZZ][ERROR] Create thread failed (err=%lu)\n", GetLastError());
                }

                waitResult = WaitForSingleObject(pi.hProcess, 5000);
                if (waitResult == WAIT_TIMEOUT) {
                    InterlockedIncrement(&g_FailedFiles);
                    printf("\n[FUZZ][ERROR] Timeout reached, terminating test application\n");
                    TerminateProcess(pi.hProcess, (DWORD)ERROR_TIMEOUT);
                    WaitForSingleObject(pi.hProcess, 500);
                }
                else {
                    if (GetExitCodeProcess(pi.hProcess, &exitCode)) {
                        if (exitCode == 0) {
                            InterlockedIncrement(&g_TotalFiles);
                        }
                        else {
                            InterlockedIncrement(&g_FailedFiles);
                            if (exitCode >= 0xC0000000 && exitCode < 0xD0000000) {
                                printf("[FUZZ][ERROR] Test app crashed, code=0x%08lX\n", exitCode);
                            }
                            else {
                                printf("[FUZZ][ERROR] Test app exited with code=0x%08lX\n", exitCode);
                            }
                        }
                    }
                    else {
                        InterlockedIncrement(&g_FailedFiles);
                        printf("[FUZZ][ERROR] GetExitCodeProcess failed (err=%lu)\n", GetLastError());
                    }
                }
                if (hThread) {
                    WaitForSingleObject(hThread, 1500);
                    CloseHandle(hThread);
                }

                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
            }
            else {
                InterlockedIncrement(&g_FailedFiles);
                printf("[FUZZ][ERROR] Executing test process failed (err=%lu)\n", GetLastError());
                if (hChildOutWrite) {
                    CloseHandle(hChildOutWrite);
                }
            }

            CloseHandle(hChildOutRead);
            TerminateProcess(hServerProcess, 0);
            CloseHandle(hServerProcess);

            printf("[FUZZ][OK] Server process terminated successfully\n");

        } while (FindNextFile(hFind, &findFileData) != 0);

        printf("[FUZZ][OK] Completed!\n");
        PrintSummary();
    }
    __finally {
        if (hFind != INVALID_HANDLE_VALUE) FindClose(hFind);
        if (pszCmdLine) HeapFree(GetProcessHeap(), 0, pszCmdLine);
        if (pszFullPath) HeapFree(GetProcessHeap(), 0, pszFullPath);
    }
}

int wmain(int argc, wchar_t* argv[])
{
    UINT em;

    em = SetErrorMode(0);
    SetErrorMode(em | SEM_NOGPFAULTERRORBOX | SEM_FAILCRITICALERRORS);

    if (argc < 3) {
        printf("Usage: WinDepends.Core.Fuzz.exe <AppDirectory> <InputDirectory>\n");
        return 1;
    }

    g_AppDir = argv[1];
    FuzzFromDirectory(argv[2]);
    return 0;
}
