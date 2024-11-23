/*
*  File: tests-main.c
*
*  Created on: Jul 8, 2024
*
*  Modified on: Oct 01, 2024
*
*      Project: WinDepends.Core.Tests
*
*      Author:
*/

#define WIN32_LEAN_AND_MEAN

#include <Windows.h>
#include <WinSock2.h>
#include <WS2tcpip.h>
#include <stdio.h>
#include <strsafe.h>
#include <shellapi.h>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "Shell32.lib")

#define APP_PORT        8209
#define APP_ADDR        "127.0.0.1"
#define APP_MAXUSERS    32
#define APP_KEEPALIVE   1

#define WDEP_STATUS_OK  L"WDEP/1.0 200 OK\r\n"
#define WDEP_STATUS_208 L"WDEP/1.0 208 Already resolved\r\n"
#define WDEP_STATUS_400 L"WDEP/1.0 400 Invalid parameters received\r\n"
#define WDEP_STATUS_403 L"WDEP/1.0 403 Can not read file headers\r\n"
#define WDEP_STATUS_404 L"WDEP/1.0 404 File not found or can not be accessed.\r\n"
#define WDEP_STATUS_415 L"WDEP/1.0 415 Invalid file headers or signatures\r\n"
#define WDEP_STATUS_500 L"WDEP/1.0 500 Can not allocate resources\r\n"

SOCKET     g_appsocket = INVALID_SOCKET;

int sendstring_plaintext(SOCKET s, const wchar_t* Buffer)
{
    return (send(s, (const char*)Buffer, (int)wcslen(Buffer) * sizeof(wchar_t), 0) >= 0);
}

#define CC_CHAIN_DATA 32762

typedef struct _BUFFER_CHAIN
{
    struct _BUFFER_CHAIN* Next;
    unsigned long           DataSize;
    wchar_t                 Data[CC_CHAIN_DATA];
} BUFFER_CHAIN, * PBUFFER_CHAIN;

PBUFFER_CHAIN recvdata(SOCKET s)
{
    PBUFFER_CHAIN   buf, buf0;
    int             e, p;
    wchar_t         prev = 0;

    buf = VirtualAlloc(NULL, sizeof(BUFFER_CHAIN), MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
    buf0 = buf;

    while (buf)
    {
        buf->Next = NULL;
        buf->DataSize = 0;

        for (p = 0; p < CC_CHAIN_DATA; ++p)
        {
            e = recv(s, (char*)&buf->Data[p], sizeof(wchar_t), 0);
            if (e != sizeof(wchar_t))
                return buf0;

            ++buf->DataSize;
            if ((buf->Data[p] == L'\n') && (prev == L'\r'))
                return buf0;

            prev = buf->Data[p];
        }

        buf->Next = VirtualAlloc(NULL, sizeof(BUFFER_CHAIN), MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
        buf = buf->Next;
    }

    return buf0;
}

void freebuffers(PBUFFER_CHAIN buffer)
{
    PBUFFER_CHAIN   next;
    while (buffer)
    {
        next = buffer->Next;
        VirtualFree(buffer, 0, MEM_RELEASE);
        buffer = next;
    }
}

void print_received_data()
{
    PBUFFER_CHAIN   idata, buf;
    idata = recvdata(g_appsocket);
    buf = idata;
    for (buf = idata; buf != NULL; buf = buf->Next)
    {
        wprintf(L"%s", buf->Data);
    }
    freebuffers(idata);
}

BOOL is_successful_request()
{
    BOOL result = FALSE;
    PBUFFER_CHAIN idata, buf;

    idata = recvdata(g_appsocket);
    buf = idata;

    for (buf = idata; buf != NULL; buf = buf->Next)
    {
        wprintf(L"%s", buf->Data);
    }

    if (idata)
    {
        result = (0 == _wcsicmp(idata->Data, WDEP_STATUS_OK));
    }

    freebuffers(idata);

    return result;
}

void main()
{
    LPWSTR      *szArglist;
    int         nArgs;

    WORD        wVersionRequested;
    WSADATA     wsadat = { 0 };
    int         wsaerr, e;

    WCHAR szBuffer[MAX_PATH * 2];

    struct sockaddr_in app_saddr = { 0 };

    printf("Starting WinDepends.Core.Tests . . .\r\n");

    szArglist = CommandLineToArgvW(GetCommandLineW(), &nArgs);

    wVersionRequested = MAKEWORD(2, 2);
    wsaerr = WSAStartup(wVersionRequested, &wsadat);
    if (wsaerr != 0)
    {
        printf("Failed to initialize Winsock.\r\n");
        ExitProcess(1);
    }

    g_appsocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (g_appsocket == INVALID_SOCKET)
    {
        printf("Socket create error.\r\n");
    }

    while (g_appsocket != INVALID_SOCKET)
    {
        app_saddr.sin_family = AF_INET;
        app_saddr.sin_port = htons(APP_PORT);
        e = inet_pton(AF_INET, APP_ADDR, &app_saddr.sin_addr);
        if (e != 1) {
            printf("Invalid IP address.\r\n");
            break;
        }

        e = connect(g_appsocket, (PSOCKADDR)&app_saddr, sizeof(app_saddr));
        if (e == SOCKET_ERROR) {
            printf("Unable to connect socket.\r\n");
            break;
        }

        print_received_data();

        // Execute command line test.
        if (nArgs > 1) {

            StringCchPrintf(szBuffer, ARRAYSIZE(szBuffer), L"open %ws\r\n", szArglist[1]);
            sendstring_plaintext(g_appsocket, szBuffer);

            if (is_successful_request())
            {
                print_received_data();

                sendstring_plaintext(g_appsocket, L"headers\r\n");
                if (is_successful_request())
                {
                    print_received_data();
                }

                sendstring_plaintext(g_appsocket, L"imports\r\n");
                if (is_successful_request())
                {
                    print_received_data();
                }

                sendstring_plaintext(g_appsocket, L"exports\r\n");
                if (is_successful_request())
                {
                    print_received_data();
                }

            }
        }
        else {
            //sendstring_plaintext(g_appsocket, L"open C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\r\n");
            sendstring_plaintext(g_appsocket, L"open C:\\windows\\system32\\ntdll.dll\r\n");
            if (is_successful_request())
            {
                print_received_data();
            }
        /*    sendstring_plaintext(g_appsocket, L"knowndlls 64\r\n");
            if (is_successful_request())
            {
                print_received_data();
            }*/


            sendstring_plaintext(g_appsocket, L"headers\r\n");
            if (is_successful_request())
            {
                print_received_data();
            }

            sendstring_plaintext(g_appsocket, L"imports\r\n");
            if (is_successful_request())
            {
                print_received_data();
            }

            sendstring_plaintext(g_appsocket, L"exports\r\n");
            if (is_successful_request())
            {
                print_received_data();
            }
        }

        Sleep(0);

        break;
    }

    if (g_appsocket != INVALID_SOCKET)
        closesocket(g_appsocket);

    WSACleanup();
    ExitProcess(0);
}
