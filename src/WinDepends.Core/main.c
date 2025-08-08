/*
*  File: main.c
*
*  Created on: Jul 8, 2024
*
*  Modified on: Aug 03, 2025
*
*      Project: WinDepends.Core
*
*      Author: WinDepends dev team 
*/

#include "core.h"
#include "pe32plus.h"

#define APP_PORT_DEFAULT    8209
#define APP_ADDR            "127.0.0.1"
#define APP_MAXUSERS        1
#define APP_KEEPALIVE       1

static long g_threads = 0;
static long long g_client_sockets_created = 0;
static long long g_client_sockets_closed = 0;
static int g_shutdown = 0;
SOCKET     g_appsocket = INVALID_SOCKET;

#define cmd_debug_log   L"cmd %s, param: %s\r\n"

int recvcmd(SOCKET s, char* buffer, int buffer_size)
{
    int	l, p = 0, wp;
    wchar_t* ubuf, prev;

    memset(buffer, 0, buffer_size);

    while (buffer_size > 0)
    {
        l = recv(s, buffer + p, buffer_size, 0);
        if (l <= 0)
            return 0;

        buffer_size -= l;
        p += l;

        if ((p >= 4) && ((p & 1) == 0))
        {
            wp = 0;
            prev = L'\0';
            ubuf = (wchar_t*)buffer;
            while ((int)(wp * sizeof(wchar_t)) < p)
            {
                if ((*ubuf == L'\n') && (prev == L'\r'))
                {
                    ubuf[-1] = L'\0';
                    return 1;
                }
                prev = *ubuf;
                ++ubuf;
                ++wp;
            }
        }
    }

    return 0;
}

VOID server_shutdown()
{
    g_shutdown = 1;
    closesocket(g_appsocket);
    g_appsocket = INVALID_SOCKET;
}

DWORD WINAPI client_thread(
    _In_ SOCKET s
)
{
    int         rcv_buffer_size = (sizeof(wchar_t) * 65536) + 4096;
    size_t      i;
    wchar_t*    rcvbuf = NULL, * cmd, * params;
    HANDLE      hheap = NULL;
    WCHAR       hello_msg[200];

    // Variable used to hold module context data.
    module_ctx  *pmctx = NULL;

    InterlockedIncrement(&g_threads);
    
    StringCchPrintf(hello_msg, 
        ARRAYSIZE(hello_msg), 
        L"WinDepends.Core %u.%u.%u.%u built at %S\r\n",
        WINDEPENDS_SERVER_MAJOR_VERSION,
        WINDEPENDS_SERVER_MINOR_VERSION,
        WINDEPENDS_SERVER_REVISION,
        WINDEPENDS_SERVER_BUILD,
        __TIMESTAMP__);

    sendstring_plaintext_no_track(s, hello_msg);

    while (s != INVALID_SOCKET) {
        hheap = HeapCreate(0, (SIZE_T)(256 * 1024), 0);
        if (!hheap)
            break;

        rcvbuf = HeapAlloc(hheap, 0, rcv_buffer_size);
        while (rcvbuf)
        {
            if (!recvcmd(s, (char*)rcvbuf, rcv_buffer_size))
                break;

            i = 0;
            cmd = rcvbuf;
            while ((*cmd != L'\0') && (isalpha(*cmd) == 0))
                ++cmd;

            params = cmd;
            while ((*params != L'\0') && (*params != L' '))
                ++params;

            while (*params == L' ')
                ++params;

            if (*params == L'\0')
                params = NULL;

            wprintf(cmd_debug_log, cmd, (params == NULL) ? L"no params" : params);

            switch (get_command_entry(cmd)) {

                //
                // Open module file for analysis and allocate designated context.
                //
            case ce_open:                
                if (params != NULL) {
                    if (pmctx != NULL) 
                        cmd_close(pmctx);
                    pmctx = cmd_open(s, params);
                }
                break;

                //
                // Close module file and deallocate module context.
                //
            case ce_close:
                if (pmctx) {
                    cmd_close(pmctx);
                    pmctx = NULL;
                }
                break;

               //
               // Get module PE headers.
               //
            case ce_headers:
                get_headers(s, pmctx);
                break;

                //
                // Get module imports.
                //
            case ce_imports:
                get_imports(s, pmctx);
                break;

                //
                // Get module exports.
                //
            case ce_exports:
                get_exports(s, pmctx);
                break;

                //
                // Get module PE data directories.
                //
            case ce_datadirs:
                get_datadirs(s, pmctx);
                break;

                //
                // Return analysis call stats.
                //
            case ce_callstats:
                cmd_callstats(s, pmctx);
                break;

                //
                // Server shutdown.
                //
            case ce_shutdown:
                server_shutdown();
                break;

                //
                // Exit current client thread.
                //
            case ce_exit:
                goto recv_loop_end;
                break;

                //
                // Query known dlls lists.
                //
            case ce_knowndlls:
                cmd_query_knowndlls_list(s, params);
                break;

                //
                // Retrieve apiset namespace information.
                //
            case ce_apisetnsinfo:
                cmd_apisetnamespace_info(s);
                break;

                //
                // Resolve apiset contract filename.
                //
            case ce_apisetresolve:
                if (params && pmctx) {
                    cmd_resolve_apiset_name(s, params, pmctx);
                }
                break;
            
                //
                // Select source of apiset map (peb or file).
                //
            case ce_apisetmapsrc:
                cmd_set_apisetmap_src(s, params);
                break;

                //
                // Unknown command handler.
                //
            case ce_unknown:
            default:
                cmd_unknown_command(s);
                break;
            }

        }

recv_loop_end:

        break;
    };

    if (rcvbuf)
        HeapFree(hheap, 0, rcvbuf);

    if (hheap)
        HeapDestroy(hheap);

    if (pmctx) {
        cmd_close(pmctx);
    }

    closesocket(s);
    InterlockedIncrement64(&g_client_sockets_closed);
    InterlockedDecrement(&g_threads);

    printf("MAIN LOOP stats: g_threads=%i, APP_MAXUSERS=%i, g_client_sockets_created=%lli, g_client_sockets_closed=%lli\r\n",
        InterlockedCompareExchange(&g_threads, 0, 0),
        APP_MAXUSERS,
        InterlockedCompareExchange64(&g_client_sockets_created, 0, 0),
        InterlockedCompareExchange64(&g_client_sockets_closed, 0, 0)
    );
    return 0;
}

void socket_set_keepalive(SOCKET s) 
{
    int opt;
    DWORD bytesReturned;
    struct tcp_keepalive keepalive;
    int e;

    opt = 1;
    if (setsockopt(s, SOL_SOCKET, SO_KEEPALIVE, (const char*)&opt, sizeof(opt)) != 0)
    {
        printf("SO_KEEPALIVE set failed.\r\n");
        return;
    }

    keepalive.onoff = 1;
    keepalive.keepalivetime = 16000;      /* 16 seconds idle */
    keepalive.keepaliveinterval = 16000;  /* 16 seconds between probes */

    e = WSAIoctl(s,
        SIO_KEEPALIVE_VALS,
        &keepalive,
        sizeof(keepalive),
        NULL,
        0,
        &bytesReturned,
        NULL,
        NULL);

    if (e == SOCKET_ERROR) {
        printf("SIO_KEEPALIVE_VALS set failed.\r\n");
    }
}

void connect_loop()
{
    DWORD   tid;
    HANDLE  th = NULL;
    int     inaddr_size;
    SOCKET  clientsocket = 0;
    struct  sockaddr_in client_saddr = { 0 };

    while ((g_appsocket != INVALID_SOCKET) && (!g_shutdown)) {

        memset(&client_saddr, 0, sizeof(client_saddr));
        inaddr_size = sizeof(client_saddr);
        clientsocket = accept(g_appsocket, (struct sockaddr*)&client_saddr, &inaddr_size);
        if (clientsocket == INVALID_SOCKET)
            continue;

        InterlockedIncrement64(&g_client_sockets_created);

        if (InterlockedCompareExchange(&g_threads, 0, 0) < APP_MAXUSERS)
        {
            if (APP_KEEPALIVE)
                socket_set_keepalive(clientsocket);

            th = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)client_thread, (LPVOID)(DWORD_PTR)clientsocket, 0, &tid);
            if (!th) {
                printf("Error starting client thread.\r\n");
            }
        }
        else
        {
            printf("Maximum allowed clients connected.\r\n");
        }

        if (!th)
        {
            closesocket(clientsocket);
            InterlockedIncrement64(&g_client_sockets_closed);
        }

        printf("MAIN LOOP stats: g_threads=%i, APP_MAXUSERS=%i, g_client_sockets_created=%lli, g_client_sockets_closed=%lli\r\n",
            InterlockedCompareExchange(&g_threads, 0, 0),
            APP_MAXUSERS,
            InterlockedCompareExchange64(&g_client_sockets_created, 0, 0),
            InterlockedCompareExchange64(&g_client_sockets_closed, 0, 0)
        );
    }

    if (th != NULL)
        CloseHandle(th);
}

DWORD WINAPI server_watchdog_thread(
    _In_ PVOID parameter
)
{
    UNREFERENCED_PARAMETER(parameter);

#ifdef _DEBUG
    INT timeout = 60;
#else
    INT timeout = 10;
#endif

    do {

        Sleep(1000);

        if (InterlockedCompareExchange(&g_threads, 0, 0) == 0) {

            --timeout;
            printf("waiting for clients, timeout %i\r\n", timeout);

            if (timeout == 0) {
                server_shutdown();
                break;
            }
        }
        else {
#ifdef _DEBUG
            timeout = 60;
#else
            timeout = 10;
#endif
        }

    } while (TRUE);

    return 0;
}

u_short select_server_port(
    VOID
)
{
    ULONG   param_length = 0;
    WCHAR   option_buffer[32];
    LPCWSTR params = GetCommandLineW();

    if (get_params_option(
        params,
        L"port",
        TRUE,
        option_buffer,
        ARRAYSIZE(option_buffer),
        &param_length))
    {
        return (u_short)strtoul_w(option_buffer);
    }

    return APP_PORT_DEFAULT;
}

#if defined _DEBUG || defined _CONSOLE
void main()
{
#else
int CALLBACK WinMain(
    _In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPSTR     lpCmdLine,
    _In_ int       nCmdShow
)
{
    UNREFERENCED_PARAMETER(hInstance);
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);
    UNREFERENCED_PARAMETER(nCmdShow);
#endif
    HANDLE      th;
    DWORD       tid, error_code = ERROR_SUCCESS;
    WORD        wVersionRequested;
    WSADATA     wsadat = { 0 };
    int         wsaerr, e;
    BOOL        opt;
 
    struct sockaddr_in app_saddr = { 0 };

    u_short     server_port;

#ifdef _CONSOLE
    printf("Starting WinDepends.Core, verbose mode\r\n");
#else
    printf("Starting WinDepends.Core . . .\r\n");
#endif

    utils_init();

    server_port = select_server_port();

    th = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)server_watchdog_thread, NULL, 0, &tid);
    if (th) {
        CloseHandle(th);
    }
    else {
        printf("Error starting server watchdog.\r\n");
    }

    wVersionRequested = MAKEWORD(2, 2);
    wsaerr = WSAStartup(wVersionRequested, &wsadat);
    if (wsaerr != 0)
    {
        printf("Failed to initialize Winsock.\r\n");
        ExitProcess(SERVER_ERROR_WSASTARTUP);
    }

    g_appsocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (g_appsocket == INVALID_SOCKET)
        printf("Socket create error.\r\n");

    while (g_appsocket != INVALID_SOCKET)
    {
        opt = 1;
        e = setsockopt(g_appsocket, SOL_SOCKET, SO_REUSEADDR, (const char*)&opt, sizeof(opt));
        if (e != 0) {
            printf("Socket init error.\r\n");
            error_code = SERVER_ERROR_SOCKETINIT;
            break;
        }

        app_saddr.sin_family = AF_INET;
        app_saddr.sin_port = htons(server_port);
        e = inet_pton(AF_INET, APP_ADDR, &app_saddr.sin_addr);
        if (e != 1) {
            printf("Invalid IP address.\r\n");
            error_code = SERVER_ERROR_INVALIDIP;
            break;
        }

        e = bind(g_appsocket, (const struct sockaddr*)&app_saddr, sizeof(app_saddr));
        if (e != 0) {
            printf("Failed to start server. Can not bind to address.\r\n");
            error_code = SERVER_ERROR_BIND;
            break;
        }

        e = listen(g_appsocket, SOMAXCONN);
        if (e != 0) {
            printf("Unable to listen socket.\r\n");
            error_code = SERVER_ERROR_LISTEN;
            break;
        }

        connect_loop();

        break;
    }

    if (g_appsocket != INVALID_SOCKET)
        closesocket(g_appsocket);

    printf("Goodbye!\r\n");
    WSACleanup();
    ExitProcess(error_code);
}
