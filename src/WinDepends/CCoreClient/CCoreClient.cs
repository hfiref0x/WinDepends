/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CCORECLIENT.CS
*
*  VERSION:     1.00
*
*  DATE:        23 May 2026
*  
*  Core Server communication class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;

namespace WinDepends;

/// <summary>
/// Specifies the type of module information to retrieve from the server.
/// </summary>
public enum ModuleInformationType
{
    /// <summary>PE file headers information.</summary>
    Headers,
    /// <summary>Import table information.</summary>
    Imports,
    /// <summary>Export table information.</summary>
    Exports,
    /// <summary>Data directories information.</summary>
    DataDirectories,
    /// <summary>API Set contract resolution.</summary>
    ApiSetName
}

/// <summary>
/// Indicates the current error status of server communication.
/// </summary>
public enum ServerErrorStatus
{
    /// <summary>No errors occurred.</summary>
    NoErrors = 0,
    /// <summary>Server process needs to be restarted.</summary>
    ServerNeedRestart,
    /// <summary>Network stream is not initialized.</summary>
    NetworkStreamNotInitialized,
    /// <summary>Socket communication error occurred.</summary>
    SocketException,
    /// <summary>General exception occurred.</summary>
    GeneralException
}

/// <summary>
/// Represents the result of a module open operation.
/// </summary>
public enum ModuleOpenStatus
{
    /// <summary>Module opened successfully.</summary>
    Okay,
    /// <summary>Unspecified error occurred.</summary>
    ErrorUnspecified,
    /// <summary>Failed to send command to server.</summary>
    ErrorSendCommand,
    /// <summary>Received data is invalid or corrupted.</summary>
    ErrorReceivedDataInvalid,
    /// <summary>Module file was not found.</summary>
    ErrorFileNotFound,
    /// <summary>Failed to map module file into memory.</summary>
    ErrorFileNotMapped,
    /// <summary>Cannot read module file headers.</summary>
    ErrorCannotReadFileHeaders,
    /// <summary>Module has invalid headers or signatures.</summary>
    ErrorInvalidHeadersOrSignatures
}

/// <summary>
/// Provides communication interface with the WinDepends.Core server process.
/// </summary>
/// <remarks>
/// This class manages the lifecycle of the server process, handles network communication,
/// and provides methods for querying PE module information. It implements thread-safe
/// disposal using atomic operations and ensures proper cleanup of network resources.
/// </remarks>
public partial class CCoreClient : IDisposable
{
    private int _disposed;
    private Process _serverProcess;     // WinDepends.Core instance.
    private TcpClient _clientConnection;
    private NetworkStream _dataStream;
    private readonly CCoreTransportAdapter _transportAdapter;
    private readonly AddLogMessageCallback _addLogMessage;
    private string _serverApplication;

    /// <summary>
    /// Gets the TCP client connection to the server.
    /// </summary>
    public TcpClient ClientConnection => _clientConnection;

    /// <summary>
    /// Gets the IP address used for server communication.
    /// </summary>
    public string IPAddress { get; }

    /// <summary>
    /// Gets or sets the port number used for server communication.
    /// </summary>
    public int Port { get; set; }

    private readonly Dictionary<Type, DataContractJsonSerializer> _serializerCache;

    private const int CORE_CONNECTION_TIMEOUT = 3000;
    private const int CORE_NETWORK_TIMEOUT = 5000;
    private const int SERVER_START_ATTEMPTS = 5;
    private const int SERVER_START_DELAY_MS = 100;
    private const int SHUTDOWN_WAIT_MS = 100;

    /// <summary>
    /// Gets or sets the current error status of server communication.
    /// </summary>
    public ServerErrorStatus ErrorStatus { get; set; }

    /// <summary>
    /// Gets the process ID of the running server process.
    /// </summary>
    /// <returns>The process ID, or -1 if the server is not running.</returns>
    public int ServerProcessId => _serverProcess?.Id ?? -1;

    /// <summary>
    /// Get or set the path to the server application executable.
    /// </summary>
    /// <returns>The server application path.</returns>
    public string ServerApplication
    {
        get => _serverApplication;
        set => _serverApplication = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CCoreClient"/> class.
    /// </summary>
    /// <param name="serverApplication">Path to the server executable.</param>
    /// <param name="ipAddress">IP address for server communication.</param>
    /// <param name="logMessageCallback">Callback for logging messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when logMessageCallback is null.</exception>
    public CCoreClient(string serverApplication, string ipAddress, AddLogMessageCallback logMessageCallback)
    {
        _addLogMessage = logMessageCallback ?? throw new ArgumentNullException(nameof(logMessageCallback));
        ServerApplication = serverApplication;
        IPAddress = ipAddress;
        ErrorStatus = ServerErrorStatus.NoErrors;

        _serializerCache = new Dictionary<Type, DataContractJsonSerializer>
        {
            [typeof(CCoreImageHeaders)] = new DataContractJsonSerializer(typeof(CCoreImageHeaders)),
            [typeof(CCoreImports)] = new DataContractJsonSerializer(typeof(CCoreImports)),
            [typeof(CCoreExports)] = new DataContractJsonSerializer(typeof(CCoreExports)),
            [typeof(CCoreDirectoryEntry)] = new DataContractJsonSerializer(typeof(CCoreDirectoryEntry)),
            [typeof(CCoreResolvedFileName)] = new DataContractJsonSerializer(typeof(CCoreResolvedFileName)),
            [typeof(CCoreApiSetNamespaceInfo)] = new DataContractJsonSerializer(typeof(CCoreApiSetNamespaceInfo)),
            [typeof(CCoreCallStats)] = new DataContractJsonSerializer(typeof(CCoreCallStats)),
            [typeof(CCoreKnownDlls)] = new DataContractJsonSerializer(typeof(CCoreKnownDlls)),
            [typeof(CCoreFileInformation)] = new DataContractJsonSerializer(typeof(CCoreFileInformation)),
            [typeof(CCoreException)] = new DataContractJsonSerializer(typeof(CCoreException))
        };

        _transportAdapter = new CCoreTransportAdapter(
            () => _clientConnection,
            () => _dataStream,
            _addLogMessage);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="CCoreClient"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        if (disposing)
        {
            DisconnectClient();
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the client has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
            throw new ObjectDisposedException(nameof(CCoreClient));
    }

}
