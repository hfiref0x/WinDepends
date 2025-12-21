/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CCORECLIENT.CS
*
*  VERSION:     1.00
*
*  DATE:        20 Dec 2025
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
using System.Text;

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
/// Represents a linked chain of character buffers used for receiving data from the server.
/// </summary>
/// <remarks>
/// This class implements a simple linked list of character arrays to handle variable-length
/// server responses without requiring pre-allocation of large buffers.
/// </remarks>
public class CBufferChain // keep as simple as possible
{
    private CBufferChain _next;

    /// <summary>
    /// Gets or sets the number of characters stored in this buffer node.
    /// </summary>
    public uint DataSize;

    /// <summary>
    /// Gets or sets the character array containing the data.
    /// </summary>
    public char[] Data;

    /// <summary>
    /// Gets or sets the next buffer node in the chain.
    /// </summary>
    public CBufferChain Next { get => _next; set => _next = value; }

    public CBufferChain()
    {
        Data = new char[CConsts.CoreServerChainSizeMax];
    }

    /// <summary>
    /// Concatenates all nodes in this buffer chain into a single string, 
    /// trimming trailing nulls per node and skipping carriage-return and line-feed characters.
    /// </summary>
    /// <returns>The concatenated content without CR/LF characters.</returns>
    public string BufferToStringNoCRLF()
    {
        int estimatedLength = 0;
        var chain = this;

        // First pass: calculate total length
        do
        {
            if (chain.Data is { Length: > 0 })
            {
                int length = chain.Data.Length;
                while (length > 0 && chain.Data[length - 1] == '\0')
                    length--;
                estimatedLength += length;
            }
            chain = chain.Next;
        } while (chain != null);

        var sb = new StringBuilder(estimatedLength > 0 ? estimatedLength : 256);
        chain = this;

        // Second pass: build string
        do
        {
            if (chain.Data is { Length: > 0 } data)
            {
                // Find last non-null character index
                int length = data.Length;
                while (length > 0 && data[length - 1] == '\0')
                    length--;

                // Process characters
                for (int i = 0; i < length; i++)
                {
                    char c = data[i];
                    if (c is not ('\n' or '\r'))
                        sb.Append(c);
                }
            }
            chain = chain.Next;
        } while (chain != null);

        return sb.ToString();
    }
}

/// <summary>
/// Provides communication interface with the WinDepends.Core server process.
/// </summary>
/// <remarks>
/// This class manages the lifecycle of the server process, handles network communication,
/// and provides methods for querying PE module information. It implements thread-safe
/// disposal using atomic operations and ensures proper cleanup of network resources.
/// </remarks>
public class CCoreClient : IDisposable
{
    private int _disposed;
    private Process _serverProcess;     // WinDepends.Core instance.
    private TcpClient _clientConnection;
    private NetworkStream _dataStream;
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

    private static readonly HashSet<string> s_forbiddenKernelLibs = new(StringComparer.OrdinalIgnoreCase)
    {
        CConsts.NtdllDll,
        CConsts.Kernel32Dll
    };

    private static readonly HashSet<string> s_requiredKernelLibs = new(StringComparer.OrdinalIgnoreCase)
    {
        CConsts.NtoskrnlExe,
        CConsts.HalDll,
        CConsts.KdComDll,
        CConsts.BootVidDll
    };

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
    /// Gets the path to the server application executable.
    /// </summary>
    /// <returns>The server application path.</returns>
    public string GetServerApplication()
    {
        return _serverApplication;
    }

    /// <summary>
    /// Sets the path to the server application executable.
    /// </summary>
    /// <param name="value">The server application path.</param>
    public void SetServerApplication(string value)
    {
        _serverApplication = value;
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
        SetServerApplication(serverApplication);
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

    /// <summary>
    /// Resolves an API Set contract name to its implementation module name.
    /// </summary>
    /// <param name="moduleName">The API Set contract name to resolve.</param>
    /// <param name="contextModule">The module context for resolution.</param>
    /// <returns>The resolved module name, or the original name if resolution fails.</returns>
    private string ResolveApiSetName(string moduleName, CModule contextModule)
    {
        // Try cache first
        string resolvedName = CApiSetCacheManager.GetResolvedNameByApiSetName(moduleName);
        if (resolvedName != null)
            return resolvedName;

        // Cache miss - resolve via server request
        var resolvedInfo = (CCoreResolvedFileName)GetModuleInformationByType(
            ModuleInformationType.ApiSetName, contextModule, moduleName);

        if (resolvedInfo != null)
        {
            resolvedName = resolvedInfo.Name;
            CApiSetCacheManager.AddApiSet(moduleName, resolvedName);
            return resolvedName;
        }

        return moduleName;
    }

    /// <summary>
    /// Checks if the server reply indicates a successful operation.
    /// </summary>
    /// <param name="module">Optional module context for error reporting.</param>
    /// <returns>true if the request was successful; otherwise, false.</returns>
    public bool IsRequestSuccessful(CModule module = null)
    {
        CBufferChain idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
            return false;

        string response = new(idata.Data, 0, (int)Math.Min(idata.DataSize, idata.Data.Length));
        if (string.Equals(response, CConsts.WDEP_STATUS_200, StringComparison.Ordinal))
            return true;

        if (response.StartsWith(CConsts.WDEP_STATUS_600, StringComparison.Ordinal))
            CheckExceptionInReply(module);

        return false;
    }

    /// <summary>
    /// Determines whether a module name represents an API Set contract.
    /// </summary>
    /// <param name="moduleName">The module name to check.</param>
    /// <returns>true if the name represents an API Set contract; otherwise, false.</returns>
    public static bool IsModuleNameApiSetContract(string moduleName)
    {
        return moduleName?.Length >= 4 &&
              (moduleName.StartsWith("API-", StringComparison.OrdinalIgnoreCase) ||
               moduleName.StartsWith("EXT-", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Outputs exception information to the log.
    /// </summary>
    /// <param name="module">The module where the exception occurred.</param>
    /// <param name="reply">The exception data from the server.</param>
    public void OutputException(CModule module, string reply)
    {
        var ex = (CCoreException)DeserializeDataJSON(typeof(CCoreException), reply);
        if (ex == null) return;

        var locations = new[] { "image headers", "data directories", "imports", "exports" };
        var location = ex.Location < locations.Length ? locations[ex.Location] : string.Empty;

        if (module != null)
            module.OtherErrorsPresent = true;

        var moduleName = module?.FileName != null ? Path.GetFileName(module.FileName) : string.Empty;
        var exceptionText = PeExceptionHelper.TranslateExceptionCode(ex.Code);

        _addLogMessage(
            $"An exception {exceptionText} 0x{ex.Code:X8} occurred while processing {location}" +
            (!string.IsNullOrEmpty(moduleName) ? $" of {moduleName}" : ""),
            LogMessageType.ErrorOrWarning);
    }

    /// <summary>
    /// Checks the server reply for exception information and logs it.
    /// </summary>
    /// <param name="module">The module context for the exception.</param>
    public void CheckExceptionInReply(CModule module)
    {
        CBufferChain idata = ReceiveReply();
        if (!IsNullOrEmptyResponse(idata))
        {
            var reply = idata.BufferToStringNoCRLF();
            if (!string.IsNullOrEmpty(reply))
            {
                OutputException(module, reply);
            }
        }
    }

    /// <summary>
    /// Sends a command to the server and receives the reply as a deserialized object.
    /// </summary>
    /// <param name="command">The command string to send to the server.</param>
    /// <param name="objectType">The type to deserialize the response into.</param>
    /// <param name="module">The module context for error reporting.</param>
    /// <returns>The deserialized object, or null if the request failed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the client has been disposed.</exception>
    public object SendCommandAndReceiveReplyAsObjectJSON(string command, Type objectType, CModule module)
    {
        ThrowIfDisposed();

        if (!SendRequest(command))
        {
            return null;
        }

        if (!IsRequestSuccessful(module))
        {
            return null;
        }

        CBufferChain idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return null;
        }

        string result = idata.BufferToStringNoCRLF();
        if (string.IsNullOrEmpty(result))
        {
            return null;
        }

        return DeserializeDataJSON(module?.FileName, objectType, result);
    }

    /// <summary>
    /// Sends a command message to the server.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>true if the message was sent successfully; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the client has been disposed.</exception>
    private bool SendRequest(string message)
    {
        ThrowIfDisposed();

        // Communication failure, server need restart.
        if (_clientConnection == null || !_clientConnection.Connected)
        {
            ErrorStatus = ServerErrorStatus.ServerNeedRestart;
            return false;
        }

        if (_dataStream == null)
        {
            ErrorStatus = ServerErrorStatus.NetworkStreamNotInitialized;
            return false;
        }

        try
        {
            using (BinaryWriter bw = new(_dataStream, Encoding.Unicode, true))
            {
                bw.Write(Encoding.Unicode.GetBytes(message));
            }
        }
        catch (Exception ex)
        {
            _addLogMessage($"Failed to send data to the server, error message: {ex.Message}", LogMessageType.ErrorOrWarning);
            ErrorStatus = (ex is IOException) ? ServerErrorStatus.SocketException : ServerErrorStatus.GeneralException;
            return false;
        }

        ErrorStatus = ServerErrorStatus.NoErrors;
        return true;
    }

    /// <summary>
    /// Receives a reply from the server and stores it in a buffer chain.
    /// </summary>
    /// <returns>A <see cref="CBufferChain"/> containing the server response, or null if an error occurred.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the client has been disposed.</exception>
    private CBufferChain ReceiveReply()
    {
        ThrowIfDisposed();

        if (_dataStream == null)
        {
            ErrorStatus = ServerErrorStatus.NetworkStreamNotInitialized;
            return null;
        }

        try
        {
            using (BinaryReader br = new(_dataStream, Encoding.Unicode, true))
            {
                CBufferChain bufferChain = new(), currentBuffer;
                char previousChar = '\0';
                currentBuffer = bufferChain;

                while (true)
                {
                    for (int i = 0; i < CConsts.CoreServerChainSizeMax; i++)
                    {
                        try
                        {
                            bufferChain.Data[i] = br.ReadChar();
                            bufferChain.DataSize++;
                            if (bufferChain.Data[i] == '\n' && previousChar == '\r')
                            {
                                ErrorStatus = ServerErrorStatus.NoErrors;
                                return currentBuffer;
                            }
                        }
                        catch (EndOfStreamException)
                        {
                            bufferChain.DataSize = (uint)i;
                            ErrorStatus = ServerErrorStatus.SocketException;
                            return i > 0 || currentBuffer != bufferChain ? currentBuffer : null;
                        }
                        catch (IOException)
                        {
                            bufferChain.DataSize = (uint)i;
                            ErrorStatus = ServerErrorStatus.SocketException;
                            return i > 0 || currentBuffer != bufferChain ? currentBuffer : null;
                        }

                        previousChar = bufferChain.Data[i];
                    }

                    bufferChain.Next = new();
                    bufferChain = bufferChain.Next;
                }
            }
        }
        catch (Exception ex)
        {
            _addLogMessage($"Receive data failed. Server message: {ex.Message}", LogMessageType.ErrorOrWarning);
            ErrorStatus = ServerErrorStatus.GeneralException;
        }
        return null;
    }

    /// <summary>
    /// Gets a cached JSON serializer for the specified type.
    /// </summary>
    /// <param name="objectType">The type to get a serializer for.</param>
    /// <returns>A <see cref="DataContractJsonSerializer"/> for the specified type.</returns>
    private DataContractJsonSerializer GetSerializerForType(Type objectType)
    {
        if (_serializerCache.TryGetValue(objectType, out var serializer))
            return serializer;

        // Fallback for unknown types
        return new DataContractJsonSerializer(objectType);
    }

    /// <summary>
    /// Deserializes JSON data into an object of the specified type.
    /// </summary>
    /// <param name="FileName">The filename for error reporting.</param>
    /// <param name="objectType">The type to deserialize into.</param>
    /// <param name="data">The JSON data string.</param>
    /// <returns>The deserialized object, or null if deserialization fails.</returns>
    object DeserializeDataJSON(string FileName, Type objectType, string data)
    {
        if (string.IsNullOrEmpty(data))
            return null;

        try
        {
            // Try to find pre-created serializer
            DataContractJsonSerializer serializer = GetSerializerForType(objectType);
            using MemoryStream ms = new(Encoding.Unicode.GetBytes(data));
            return serializer.ReadObject(ms);
        }
        catch (Exception ex)
        {
            _addLogMessage($"Data deserialization failed: {ex.Message}", LogMessageType.ErrorOrWarning);
            _addLogMessage($"Failed to analyze {FileName}", LogMessageType.ErrorOrWarning);
            return null;
        }
    }

    /// <summary>
    /// Deserializes JSON data into an object of the specified type.
    /// </summary>
    /// <param name="objectType">The type to deserialize into.</param>
    /// <param name="data">The JSON data string.</param>
    /// <returns>The deserialized object, or null if deserialization fails.</returns>
    object DeserializeDataJSON(Type objectType, string data)
    {
        if (string.IsNullOrEmpty(data))
            return null;

        try
        {
            // Try to find pre-created serializer
            DataContractJsonSerializer serializer = GetSerializerForType(objectType);
            using MemoryStream ms = new(Encoding.Unicode.GetBytes(data));
            return serializer.ReadObject(ms);
        }
        catch (Exception ex)
        {
            _addLogMessage($"Data deserialization failed: {ex.Message}", LogMessageType.ErrorOrWarning);
            return null;
        }
    }

    /// <summary>
    /// Determines whether a buffer chain is null or contains an empty response.
    /// </summary>
    /// <param name="buffer">The buffer chain to check.</param>
    /// <returns>true if the buffer is null or empty; otherwise, false.</returns>
    public static bool IsNullOrEmptyResponse(CBufferChain buffer)
    {
        // Check for null.
        if (buffer == null || buffer.DataSize == 0 || buffer.Data == null)
        {
            return true;
        }

        // Check for empty.
        if (buffer.DataSize == 2)
        {
            return buffer.Data[0] == '\r' && buffer.Data[1] == '\n';
        }
        return false;
    }

    /// <summary>
    /// Sets error flags on a module based on the open operation result.
    /// </summary>
    /// <param name="module">The module to update.</param>
    /// <param name="fileNotFound">Whether the file was not found.</param>
    /// <param name="invalid">Whether the file is invalid.</param>
    /// <param name="status">The module open status to return.</param>
    /// <returns>The specified <paramref name="status"/>.</returns>
    private static ModuleOpenStatus SetModuleError(CModule module, bool fileNotFound, bool invalid, ModuleOpenStatus status)
    {
        if (fileNotFound)
            module.FileNotFound = true;

        if (invalid)
            module.IsInvalid = true;

        return status;
    }

    /// <summary>
    /// Generates a cryptographically secure random port number within the specified range.
    /// </summary>
    /// <param name="minPort">The minimum port number (inclusive).</param>
    /// <param name="maxPort">The maximum port number (inclusive).</param>
    /// <returns>A random port number between minPort and maxPort.</returns>
    private static int GenerateSecureRandomPort(int minPort, int maxPort)
    {
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            byte[] randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            uint randomValue = BitConverter.ToUInt32(randomBytes, 0);
            return minPort + (int)(randomValue % (uint)(maxPort - minPort + 1));
        }
    }

    /// <summary>
    /// Validates and canonicalizes a server executable path.
    /// </summary>
    /// <param name="filePath">The path to validate.</param>
    /// <param name="canonicalPath">Outputs the canonical (full) path if validation succeeds.</param>
    /// <param name="errorMessage">Outputs an error message if validation fails.</param>
    /// <returns>true if the path is valid; otherwise, false.</returns>
    private static bool ValidateServerPath(string filePath, out string canonicalPath, out string errorMessage)
    {
        canonicalPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "Server application path is not specified";
            return false;
        }

        try
        {
            canonicalPath = Path.GetFullPath(filePath);

            if (!File.Exists(canonicalPath))
            {
                errorMessage = "Server application file does not exist";
                return false;
            }

            string extension = Path.GetExtension(canonicalPath);
            if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Server application must be an executable file";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                                   ex is PathTooLongException || ex is UnauthorizedAccessException)
        {
            errorMessage = $"Invalid server path: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Opens a COFF module on the server for analysis.
    /// </summary>
    /// <param name="module">The module to open.</param>
    /// <param name="settings">Settings for opening the module.</param>
    /// <returns>A <see cref="ModuleOpenStatus"/> indicating the result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when module is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
    public ModuleOpenStatus OpenModule(ref CModule module, CFileOpenSettings settings)
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        ThrowIfDisposed();

        string cmd = $"open file \"{module.FileName}\"";

        if (settings.UseStats)
        {
            cmd += " use_stats";
        }

        if (settings.ProcessRelocsForImage)
        {
            cmd += $" process_relocs";
        }

        if (settings.UseCustomImageBase)
        {
            cmd += $" custom_image_base {settings.CustomImageBase}";
        }

        cmd += "\r\n";

        if (!SendRequest(cmd))
        {
            return ModuleOpenStatus.ErrorSendCommand;
        }

        CBufferChain idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        string response = new(idata.Data, 0, (int)Math.Min(idata.DataSize, idata.Data.Length));

        if (!string.Equals(response, CConsts.WDEP_STATUS_200, StringComparison.Ordinal))
        {
            return response switch
            {
                var s when string.Equals(s, CConsts.WDEP_STATUS_404, StringComparison.Ordinal) =>
                   SetModuleError(module, true, false, ModuleOpenStatus.ErrorFileNotFound),

                var s when string.Equals(s, CConsts.WDEP_STATUS_403, StringComparison.Ordinal) =>
                    SetModuleError(module, false, true, ModuleOpenStatus.ErrorCannotReadFileHeaders),

                var s when string.Equals(s, CConsts.WDEP_STATUS_415, StringComparison.Ordinal) =>
                    SetModuleError(module, false, true, ModuleOpenStatus.ErrorInvalidHeadersOrSignatures),

                var s when string.Equals(s, CConsts.WDEP_STATUS_502, StringComparison.Ordinal) =>
                   SetModuleError(module, false, true, ModuleOpenStatus.ErrorFileNotMapped),

                _ => SetModuleError(module, false, true, ModuleOpenStatus.ErrorUnspecified)
            };
        }

        idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        response = idata.BufferToStringNoCRLF();
        if (string.IsNullOrEmpty(response))
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        var fileInformation = (CCoreFileInformation)DeserializeDataJSON(typeof(CCoreFileInformation), response);
        if (fileInformation != null)
        {
            module.ModuleData.Attributes = (ModuleFileAttributes)fileInformation.FileAttributes;
            module.ModuleData.RealChecksum = fileInformation.RealChecksum;
            module.ModuleData.ImageFixed = fileInformation.ImageFixed;
            module.ModuleData.ImageDotNet = fileInformation.ImageDotNet;
            module.ModuleData.FileSize = fileInformation.FileSizeLow | ((ulong)fileInformation.FileSizeHigh << 32);

            long fileTime = ((long)fileInformation.LastWriteTimeHigh << 32) | fileInformation.LastWriteTimeLow;
            try
            {
                module.ModuleData.FileTimeStamp = DateTime.FromFileTime(fileTime < 0 ? 0 : fileTime);
            }
            catch
            {
                module.ModuleData.FileTimeStamp = DateTime.MinValue;
            }
            return ModuleOpenStatus.Okay;
        }

        return ModuleOpenStatus.ErrorUnspecified;
    }

    /// <summary>
    /// Closes the currently opened module on the server.
    /// </summary>
    /// <returns>true if the close command was sent successfully; otherwise, false.</returns>
    public bool CloseModule()
    {
        return SendRequest(CConsts.CMD_CLOSE);
    }

    /// <summary>
    /// Sends an exit request to the server.
    /// </summary>
    /// <returns>true if the exit command was sent successfully; otherwise, false.</returns>
    public bool ExitRequest()
    {
        return SendRequest(CConsts.CMD_EXIT);
    }

    /// <summary>
    /// Sends a shutdown request to the server.
    /// </summary>
    /// <returns>true if the shutdown command was sent successfully; otherwise, false.</returns>
    public bool ShutdownRequest()
    {
        return SendRequest(CConsts.CMD_SHUTDOWN);
    }

    /// <summary>
    /// Retrieves module information of the specified type from the server.
    /// </summary>
    /// <param name="moduleInformationType">The type of information to retrieve.</param>
    /// <param name="module">The module context.</param>
    /// <param name="parameters">Optional parameters for the request.</param>
    /// <returns>The requested module information object, or null if the request fails.</returns>
    public object GetModuleInformationByType(ModuleInformationType moduleInformationType, CModule module, string parameters = null)
    {
        string cmd;
        Type objectType;

        switch (moduleInformationType)
        {
            case ModuleInformationType.Headers:
                cmd = CConsts.CMD_HEADERS;
                objectType = typeof(CCoreImageHeaders);
                break;
            case ModuleInformationType.Imports:
                cmd = CConsts.CMD_IMPORTS;
                objectType = typeof(CCoreImports);
                break;
            case ModuleInformationType.Exports:
                cmd = CConsts.CMD_EXPORTS;
                objectType = typeof(CCoreExports);
                break;
            case ModuleInformationType.DataDirectories:
                cmd = CConsts.CMD_DATADIRS;
                objectType = typeof(CCoreDirectoryEntry);
                break;
            case ModuleInformationType.ApiSetName:
                cmd = $"apisetresolve {parameters}\r\n";
                objectType = typeof(CCoreResolvedFileName);
                break;
            default:
                return null;
        }

        return SendCommandAndReceiveReplyAsObjectJSON(cmd, objectType, module);
    }

    /// <summary>
    /// Gets the data directories for the specified module.
    /// </summary>
    /// <param name="module">The module to query.</param>
    /// <returns>A list of data directory entries, or null if the request fails.</returns>
    public List<CCoreDirectoryEntry> GetModuleDataDirectories(CModule module)
    {
        return (List<CCoreDirectoryEntry>)GetModuleInformationByType(ModuleInformationType.DataDirectories, module);
    }

    /// <summary>
    /// Gets API Set namespace information from the server.
    /// </summary>
    /// <param name="apiSetFilePath">Optional path to ApiSet schema file.  If null, queries current server ApiSet.</param>
    /// <returns>API Set namespace information, or null if the request fails.</returns>
    public CCoreApiSetNamespaceInfo GetApiSetNamespaceInfo(string apiSetFilePath = null)
    {
        string cmd = CConsts.CMD_APISETNINFO;

        if (!string.IsNullOrEmpty(apiSetFilePath))
        {
            cmd = $"apisetnsinfo file \"{apiSetFilePath}\"\r\n";
        }

        return (CCoreApiSetNamespaceInfo)SendCommandAndReceiveReplyAsObjectJSON(
            cmd, typeof(CCoreApiSetNamespaceInfo), null);
    }

    /// <summary>
    /// Gets call statistics from the server.
    /// </summary>
    /// <returns>Call statistics information, or null if the request fails.</returns>
    public CCoreCallStats GetCoreCallStats()
    {
        return (CCoreCallStats)SendCommandAndReceiveReplyAsObjectJSON(
              CConsts.CMD_CALLSTATS, typeof(CCoreCallStats), null);
    }

    /// <summary>
    /// Handles and logs import processing exceptions.
    /// </summary>
    /// <param name="imports">The import information containing exception data.</param>
    private void HandleImportExceptions(CCoreImports imports)
    {
        bool exceptStd = (imports.Exception & 1) != 0;
        bool exceptDelay = (imports.Exception & 2) != 0;

        if (PeExceptionHelper.IsInvalidImageFormatException(imports.ExceptionCodeStd))
        {
            _addLogMessage($"Exception occured while processing file, imports seems destroyed",
                LogMessageType.ErrorOrWarning);
            return;
        }

        if (exceptStd && exceptDelay)
        {
            _addLogMessage(
                $"Exceptions occurred while processing imports:\n" +
                $"  Standard: {PeExceptionHelper.TranslateExceptionCode(imports.ExceptionCodeStd)} (0x{imports.ExceptionCodeStd:X8})\n" +
                $"  Delay-load: {PeExceptionHelper.TranslateExceptionCode(imports.ExceptionCodeDelay)} (0x{imports.ExceptionCodeDelay:X8})",
                LogMessageType.ErrorOrWarning);
        }
        else if (exceptStd)
        {
            _addLogMessage(
                $"Exception {PeExceptionHelper.TranslateExceptionCode(imports.ExceptionCodeStd)} (0x{imports.ExceptionCodeStd:X8}) occurred while processing standard imports",
                LogMessageType.ErrorOrWarning);
        }
        else if (exceptDelay)
        {
            _addLogMessage(
                $"Exception {PeExceptionHelper.TranslateExceptionCode(imports.ExceptionCodeDelay)} (0x{imports.ExceptionCodeDelay:X8}) occurred while processing delay-load imports",
                LogMessageType.ErrorOrWarning);
        }
    }

    /// <summary>
    /// Retrieves and populates module header information.
    /// </summary>
    /// <param name="module">The module to populate with header information.</param>
    /// <returns>true if headers were retrieved successfully; otherwise, false.</returns>
    public bool GetModuleHeadersInformation(CModule module)
    {
        if (module == null)
        {
            return false;
        }

        var fh = (CCoreImageHeaders)GetModuleInformationByType(ModuleInformationType.Headers, module);
        if (fh == null)
        {
            return false;
        }

        CModuleData moduleData = module.ModuleData;

        // Set various module data properties
        moduleData.LinkerVersion = $"{fh.OptionalHeader.MajorLinkerVersion}.{fh.OptionalHeader.MinorLinkerVersion}";
        moduleData.SubsystemVersion = $"{fh.OptionalHeader.MajorSubsystemVersion}.{fh.OptionalHeader.MinorSubsystemVersion}";
        moduleData.ImageVersion = $"{fh.OptionalHeader.MajorImageVersion}.{fh.OptionalHeader.MinorImageVersion}";
        moduleData.OSVersion = $"{fh.OptionalHeader.MajorOperatingSystemVersion}.{fh.OptionalHeader.MinorOperatingSystemVersion}";
        moduleData.LinkChecksum = fh.OptionalHeader.CheckSum;
        moduleData.Machine = fh.FileHeader.Machine;
        moduleData.LinkTimeStamp = fh.FileHeader.TimeDateStamp;
        moduleData.Characteristics = fh.FileHeader.Characteristics;
        moduleData.Subsystem = fh.OptionalHeader.Subsystem;
        moduleData.VirtualSize = fh.OptionalHeader.SizeOfImage;
        moduleData.PreferredBase = fh.OptionalHeader.ImageBase;
        moduleData.DllCharacteristics = fh.OptionalHeader.DllCharacteristics;
        moduleData.ExtendedCharacteristics = fh.ExtendedDllCharacteristics;

        if (fh.FileVersion != null)
        {
            moduleData.FileVersion = $"{fh.FileVersion.FileVersionMS.HIWORD()}." +
                $"{fh.FileVersion.FileVersionMS.LOWORD()}." +
                $"{fh.FileVersion.FileVersionLS.HIWORD()}." +
                $"{fh.FileVersion.FileVersionLS.LOWORD()}";

            moduleData.ProductVersion = $"{fh.FileVersion.ProductVersionMS.HIWORD()}." +
                $"{fh.FileVersion.ProductVersionMS.LOWORD()}." +
                $"{fh.FileVersion.ProductVersionLS.HIWORD()}." +
                $"{fh.FileVersion.ProductVersionLS.LOWORD()}";
        }
        else
        {
            moduleData.FileVersion = "N/A";
            moduleData.ProductVersion = "N/A";
        }

        //
        // Remember debug directory types.
        //
        if (fh.DebugDirectory != null)
        {
            foreach (var entry in fh.DebugDirectory)
            {
                moduleData.DebugDirTypes.Add(entry.Type);
                if (entry.Type == (uint)DebugEntryType.Reproducible)
                {
                    module.IsReproducibleBuild = true;
                }
            }
        }

        if (!string.IsNullOrEmpty(fh.Base64Manifest))
        {
            module.ManifestData = fh.Base64Manifest;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a module is a kernel-mode module based on its imports.
    /// </summary>
    /// <param name="module">The module to check.</param>
    /// <param name="imports">The import information for the module.</param>
    private static void CheckIfKernelModule(CModule module, CCoreImports imports)
    {
        // Skip check if already determined to be a kernel module
        if (module.IsKernelModule ||
            module.ModuleData.Subsystem != NativeMethods.IMAGE_SUBSYSTEM_NATIVE)
            return;

        bool hasForbiddenLibrary = false;
        bool hasRequiredLibrary = false;

        foreach (var entry in imports.Library)
        {
            // Check for forbidden user-mode DLL's
            if (!hasForbiddenLibrary && s_forbiddenKernelLibs.Contains(entry.Name))
            {
                hasForbiddenLibrary = true;
                break;
            }

            // Check for required kernel-mode components
            if (!hasRequiredLibrary && s_requiredKernelLibs.Contains(entry.Name))
            {
                hasRequiredLibrary = true;
            }
        }

        // Module is kernel-mode if it has required libraries but no forbidden ones
        if (!hasForbiddenLibrary && hasRequiredLibrary)
        {
            module.IsKernelModule = true;
        }
    }

    /// <summary>
    /// Adds a dependent module to a parent module's dependency list.
    /// </summary>
    /// <param name="parentModule">The parent module.</param>
    /// <param name="DelayLibraries">Whether this is a delay-load dependency.</param>
    /// <param name="moduleName">The module name to add.</param>
    /// <param name="rawModuleName">The raw (unresolved) module name.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <returns>The newly created dependent module.</returns>
    public CModule AddDependentModule(CModule parentModule,
                                   bool DelayLibraries,
                                   string moduleName,
                                   string rawModuleName,
                                   List<SearchOrderType> searchOrderUM,
                                   List<SearchOrderType> searchOrderKM)
    {
        // Resolve ApiSet name if any.
        bool isApiSetContract = IsModuleNameApiSetContract(moduleName);
        if (isApiSetContract)
        {
            moduleName = ResolveApiSetName(moduleName, parentModule);
        }

        // Resolve module path.
        var moduleFileName = CPathResolver.ResolvePathForModule(moduleName,
                                                                parentModule,
                                                                searchOrderUM,
                                                                searchOrderKM,
                                                                out SearchOrderType resolvedBy);

        if (!string.IsNullOrEmpty(moduleFileName))
        {
            moduleName = moduleFileName;
        }

        CModule dependent = new(moduleName, rawModuleName, resolvedBy, isApiSetContract)
        {
            IsDelayLoad = DelayLibraries,
            IsKernelModule = parentModule.IsKernelModule, //propagate from parent
        };

        parentModule.Dependents.Add(dependent);

        return dependent;
    }

    /// <summary>
    /// Processes .NET assembly references for a module.
    /// </summary>
    /// <param name="module">The module to process.</param>
    void ProcessNetAssemblies(CModule module)
    {
        var dependencies = CAssemblyRefAnalyzer.GetAssemblyDependencies(module);
        CModule netDependent;
        foreach (var reference in dependencies)
        {
            if (reference.IsResolved)
            {
                netDependent = new(reference.ResolvedPath, reference.ResolvedPath, SearchOrderType.None, false);
            }
            else
            {
                netDependent = new(reference.Name, reference.Name, SearchOrderType.None, false);
            }
            netDependent.ModuleData.RuntimeVersion = module.ModuleData.RuntimeVersion;
            netDependent.ModuleData.FrameworkKind = module.ModuleData.FrameworkKind;
            netDependent.ModuleData.ResolutionSource = reference.ResolutionSource;
            netDependent.ModuleData.ReferenceVersion = reference.Version;
            netDependent.ModuleData.ReferencePublicKeyToken = reference.PublicKeyToken;
            netDependent.ModuleData.ReferenceCulture = reference.Culture;
            netDependent.IsDotNetModule = true;
            module.Dependents.Add(netDependent);
        }

        CAssemblyRefAnalyzer.ClearCache();
    }

    /// <summary>
    /// Processes import libraries for a module and adds them as dependencies.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="DelayLibraries">Whether these are delay-load imports.</param>
    /// <param name="LibraryList">The list of imported libraries.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <param name="parentImportsHashTable">Hash table for tracking parent imports.</param>
    public void ProcessImports(CModule module,
                                bool DelayLibraries,
                                List<CCoreImportLibrary> LibraryList,
                                List<SearchOrderType> searchOrderUM,
                                List<SearchOrderType> searchOrderKM,
                                Dictionary<int, FunctionHashObject> parentImportsHashTable)
    {
        foreach (var entry in LibraryList)
        {
            var dependent = AddDependentModule(module, DelayLibraries,
                entry.Name, entry.Name, searchOrderUM, searchOrderKM);

            foreach (var func in entry.Function)
            {
                dependent.ParentImports.Add(new CFunction(func));
                FunctionHashObject funcHashObject = new(dependent.FileName, func.Name, func.Ordinal);
                var uniqueKey = funcHashObject.GenerateUniqueKey();
                parentImportsHashTable.TryAdd(uniqueKey, funcHashObject);
            }
        }
    }

    /// <summary>
    /// Parses a forwarder string to extract the target module and function information.
    /// </summary>
    /// <param name="forwarder">The forwarder string (e.g., "MODULE.Function" or "MODULE.#123").</param>
    /// <param name="targetModule">Outputs the target module name.</param>
    /// <param name="targetFunctionName">Outputs the target function name (empty if by ordinal).</param>
    /// <param name="targetOrdinal">Outputs the target ordinal (UInt32.MaxValue if by name).</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    private static bool TryParseForwarderTarget(string forwarder,
                                                   out string targetModule,
                                                   out string targetFunctionName,
                                                   out uint targetOrdinal)
    {
        targetModule = string.Empty;
        targetFunctionName = string.Empty;
        targetOrdinal = UInt32.MaxValue;

        if (string.IsNullOrEmpty(forwarder))
            return false;

        int dot = forwarder.IndexOf('.');
        if (dot <= 0 || dot == forwarder.Length - 1)
            return false;

        targetModule = forwarder.Substring(0, dot);

        ReadOnlySpan<char> rest = forwarder.AsSpan(dot + 1);
        if (rest.Length == 0)
            return false;

        // Ordinal forwarder (e.g. "KERNEL32.#123")
        if (rest[0] == '#')
        {
            ulong value = 0;
            int i = 1;
            while (i < rest.Length && char.IsDigit(rest[i]))
            {
                value = value * 10 + (uint)(rest[i] - '0');
                if (value > UInt32.MaxValue) break;
                i++;
            }
            if (i == 1)
                return false;

            targetOrdinal = (uint)value;
            return true;
        }

        // Name forwarder: copy until whitespace (whitespace not expected but safe stop)
        int fnEnd = 0;
        while (fnEnd < rest.Length && !char.IsWhiteSpace(rest[fnEnd]))
            fnEnd++;

        targetFunctionName = rest.Slice(0, fnEnd).ToString();
        return !string.IsNullOrEmpty(targetFunctionName);
    }

    /// <summary>
    /// Expands all forwarder modules in the dependency tree starting from the root module.
    /// </summary>
    /// <param name="root">The root module to start from.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <param name="parentImportsHashTable">Hash table for tracking parent imports.</param>
    public void ExpandAllForwarderModules(CModule root,
                                        List<SearchOrderType> searchOrderUM,
                                        List<SearchOrderType> searchOrderKM,
                                        Dictionary<int, FunctionHashObject> parentImportsHashTable)
    {
        if (root == null) return;

        Queue<CModule> q = new();
        HashSet<CModule> visited = new();
        q.Enqueue(root);

        while (q.Count > 0)
        {
            var m = q.Dequeue();
            if (!visited.Add(m))
                continue;

            // Expand forwarders for this module once
            if (!m.ForwardersExpanded && m.ForwarderEntries.Count > 0)
            {
                ExpandForwardersForModule(m, searchOrderUM, searchOrderKM, parentImportsHashTable, null);
                m.ForwardersExpanded = true;
            }

            if (m.Dependents != null)
            {
                foreach (var d in m.Dependents)
                    q.Enqueue(d);
            }
        }
    }

    /// <summary>
    /// Creates or retrieves a forward node for a target module.
    /// </summary>
    /// <param name="parentModule">The parent module.</param>
    /// <param name="rawTarget">The raw target module name.</param>
    /// <param name="canonicalName">The canonical module name.</param>
    /// <param name="finalTargetName">The final resolved target name.</param>
    /// <param name="resolvedBy">How the name was resolved.</param>
    /// <param name="isApiSetContract">Whether this is an API Set contract.</param>
    /// <returns>The forward node module.</returns>
    private CModule CreateOrGetForwardNode(CModule parentModule,
                                      string rawTarget,
                                      string canonicalName,
                                      string finalTargetName,
                                      SearchOrderType resolvedBy,
                                      bool isApiSetContract)
    {
        // First check for existing real (non-forward) module
        var existingReal = parentModule.Dependents.FirstOrDefault(d =>
            !d.IsForward &&
            (d.FileName.Equals(finalTargetName, StringComparison.OrdinalIgnoreCase) ||
             d.FileName.Equals(canonicalName, StringComparison.OrdinalIgnoreCase) ||
             d.RawFileName.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)));

        if (existingReal != null)
            return existingReal;

        // Check for existing synthetic forward node
        var forwardNode = parentModule.Dependents.FirstOrDefault(d =>
            d.IsForward &&
            (d.FileName.Equals(finalTargetName, StringComparison.OrdinalIgnoreCase) ||
             d.FileName.Equals(canonicalName, StringComparison.OrdinalIgnoreCase) ||
             d.RawFileName.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)));

        if (forwardNode == null)
        {
            forwardNode = new CModule(finalTargetName,
                                     rawTarget, // keep original forward string module token
                                     resolvedBy,
                                     isApiSetContract)
            {
                IsKernelModule = parentModule.IsKernelModule,
                IsForward = true
            };
            parentModule.Dependents.Add(forwardNode);
        }

        return forwardNode;
    }

    /// <summary>
    /// Expands forwarders for a specific module, creating synthetic dependency nodes as needed.
    /// Uses a chain tracking mechanism to detect and prevent circular forwarding.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <param name="parentImportsHashTable">Hash table for tracking parent imports.</param>
    /// <param name="forwardingChain">Set of modules in current forwarding chain to detect cycles.</param>
    public void ExpandForwardersForModule(CModule module,
                                        List<SearchOrderType> searchOrderUM,
                                        List<SearchOrderType> searchOrderKM,
                                        Dictionary<int, FunctionHashObject> parentImportsHashTable,
                                        HashSet<string> forwardingChain = null)
    {
        if (module?.ForwarderEntries == null || module.ForwarderEntries.Count == 0)
            return;

        // Initialize tracking set for first call in chain
        bool isRootCall = forwardingChain == null;
        forwardingChain ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Track this module to detect cycles
        string moduleKey = Path.GetFileName(module.FileName).ToLowerInvariant();
        if (!isRootCall && !forwardingChain.Add(moduleKey))
        {
            string cyclePath = string.Join(" → ", forwardingChain) + " → " + moduleKey;
            _addLogMessage($"Circular forwarding detected: {cyclePath}",
                LogMessageType.ErrorOrWarning);
            return;
        }

        var groups = module.ForwarderEntries
                           .Where(f => f.TargetModuleName != null)
                           .GroupBy(f => f.TargetModuleName, StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            string rawTarget = g.Key;

            // Skip if we've detected this as part of a cycle
            string targetKey = Path.GetFileName(rawTarget).ToLowerInvariant();
            if (forwardingChain.Contains(targetKey))
            {
                _addLogMessage($"Skipping circular forward from {moduleKey} to {targetKey}",
                    LogMessageType.ErrorOrWarning);
                continue;
            }

            bool isApiSetContract = IsModuleNameApiSetContract(rawTarget);
            string canonicalName = isApiSetContract ? ResolveApiSetName(rawTarget, module) : rawTarget;
            string resolvedTargetPath = CPathResolver.ResolvePathForModule(
                canonicalName, module, searchOrderUM, searchOrderKM, out SearchOrderType resolvedBy);

            string finalTargetName = string.IsNullOrEmpty(resolvedTargetPath) ? canonicalName : resolvedTargetPath;

            // Create the synthetic forward node
            var forwardNode = CreateOrGetForwardNode(
                module, rawTarget, canonicalName, finalTargetName, resolvedBy, isApiSetContract);

            // If this returned an existing real module, skip processing this group
            if (!forwardNode.IsForward)
                continue;

            foreach (var fe in g)
            {
                bool exists;
                if (fe.TargetOrdinal != UInt32.MaxValue)
                {
                    exists = forwardNode.ParentImports.Any(f => f.Ordinal == fe.TargetOrdinal);
                }
                else
                {
                    exists = forwardNode.ParentImports.Any(f =>
                        f.Ordinal == UInt32.MaxValue &&
                        f.RawName.Equals(fe.TargetFunctionName, StringComparison.Ordinal));
                }
                if (exists)
                    continue;

                var synthetic = new CFunction
                {
                    RawName = (fe.TargetOrdinal == UInt32.MaxValue) ? fe.TargetFunctionName : string.Empty,
                    Ordinal = (fe.TargetOrdinal == UInt32.MaxValue) ? UInt32.MaxValue : fe.TargetOrdinal,
                    Hint = UInt32.MaxValue,
                    IsExportFunction = false
                };
                synthetic.Kind = synthetic.MakeDefaultFunctionKind();

                forwardNode.ParentImports.Add(synthetic);

                var fho = new FunctionHashObject(
                    forwardNode.FileName,
                    (fe.TargetOrdinal == UInt32.MaxValue) ? synthetic.RawName : string.Empty,
                    synthetic.Ordinal);

                parentImportsHashTable.TryAdd(fho.GenerateUniqueKey(), fho);
            }
        }

        // Remove this module from chain when done with this branch
        if (!isRootCall)
        {
            forwardingChain.Remove(moduleKey);
        }
    }

    /// <summary>
    /// Populates module exports, validates existing parent imports, and processes forwarders.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="collectForwarders">Whether to collect forwarder information.</param>
    /// <param name="rawExports">The raw export data from the server.</param>
    void ProcessExports(CModule module,
                        bool collectForwarders,
                        CCoreExports rawExports)
    {
        if (module == null || rawExports?.Library == null)
            return;

        foreach (var entry in rawExports.Library.Function)
        {
            var cf = new CFunction(entry);
            module.ModuleData.Exports.Add(cf);

            // Collect forwarder metadata
            if (collectForwarders && !string.IsNullOrEmpty(cf.ForwardName))
            {
                if (TryParseForwarderTarget(cf.ForwardName,
                    out _, out string targetFn, out uint targetOrd))
                {
                    string rawTargetModule = CFunction.ExtractForwarderModule(cf.ForwardName);
                    if (!string.IsNullOrEmpty(rawTargetModule))
                    {
                        string moduleFileName = Path.GetFileName(module.FileName);
                        string rawFileName = Path.GetFileName(module.RawFileName);

                        // Skip self-forward
                        if (!rawTargetModule.Equals(moduleFileName, StringComparison.OrdinalIgnoreCase) &&
                            !rawTargetModule.Equals(rawFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            var fe = new CForwarderEntry
                            {
                                TargetModuleName = rawTargetModule,
                                TargetFunctionName = (targetOrd == UInt32.MaxValue) ? targetFn : string.Empty,
                                TargetOrdinal = (targetOrd == UInt32.MaxValue) ? UInt32.MaxValue : targetOrd
                            };
                            if (!module.ForwarderEntries.Contains(fe))
                                module.ForwarderEntries.Add(fe);
                        }
                    }
                }
            }
        }

        // Validate previously collected parent imports against exports
        foreach (var entry in module.ParentImports)
        {
            bool resolved;
            if (entry.Ordinal != UInt32.MaxValue)
            {
                resolved = module.ModuleData.Exports.Any(f => f.Ordinal == entry.Ordinal);
            }
            else
            {
                resolved = module.ModuleData.Exports.Any(f =>
                    f.RawName.Equals(entry.RawName, StringComparison.Ordinal));
            }

            if (!resolved)
            {
                module.ExportContainErrors = true;
                break;
            }
        }
    }

    /// <summary>
    /// Checks and logs invalid import entries.
    /// </summary>
    /// <param name="module">The module being processed.</param>
    /// <param name="imports">The import information.</param>
    private void CheckInvalidImports(CModule module, CCoreImports imports)
    {
        string modName = module?.FileName ?? "<unknown>";

        if (imports.InvalidImportModuleCount != 0)
        {
            _addLogMessage($"Invalid import entries found ({imports.InvalidImportModuleCount}) while processing {modName}",
                LogMessageType.ErrorOrWarning);
        }
        if (imports.InvalidDelayImportModuleCount != 0)
        {
            _addLogMessage($"Invalid delay-load import entries found ({imports.InvalidDelayImportModuleCount}) while processing {modName}",
                LogMessageType.ErrorOrWarning);
        }
    }

    /// <summary>
    /// Retrieves and processes import and export information for a module.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <param name="parentImportsHashTable">Hash table for tracking parent imports.</param>
    /// <param name="EnableExperimentalFeatures">Whether to enable experimental features (.NET analysis).</param>
    /// <param name="CollectForwarders">Whether to collect forwarder information from exports.</param>
    public void GetModuleImportExportInformation(CModule module,
                                                 List<SearchOrderType> searchOrderUM,
                                                 List<SearchOrderType> searchOrderKM,
                                                 Dictionary<int, FunctionHashObject> parentImportsHashTable,
                                                 bool EnableExperimentalFeatures,
                                                 bool CollectForwarders)
    {
        if (module == null)
            return;
        //
        // Process exports.
        //
        CCoreExports rawExports = (CCoreExports)GetModuleInformationByType(ModuleInformationType.Exports, module);
        if (rawExports != null)
        {
            ProcessExports(module, CollectForwarders, rawExports);
        }

        //
        // Process imports.
        //
        CCoreImports rawImports = (CCoreImports)GetModuleInformationByType(ModuleInformationType.Imports, module);
        if (rawImports != null)
        {
            CheckInvalidImports(module, rawImports);
            CheckIfKernelModule(module, rawImports);
            ProcessImports(module, false, rawImports.Library, searchOrderUM, searchOrderKM, parentImportsHashTable);
            ProcessImports(module, true, rawImports.LibraryDelay, searchOrderUM, searchOrderKM, parentImportsHashTable);
            if (rawImports.Exception != 0)
            {
                HandleImportExceptions(rawImports);
                module.OtherErrorsPresent = true;
            }
        }

        //
        // Process .NET assembly references.
        // This feature is experimental and not production ready.
        //
        module.IsDotNetModule = module.ModuleData.ImageDotNet == 1;
        if (module.IsDotNetModule && EnableExperimentalFeatures)
        {
            ProcessNetAssemblies(module);
        }
    }

    /// <summary>
    /// Sets the API Set schema namespace source for the server.
    /// </summary>
    /// <param name="fileName">Path to the API Set schema file, or null/empty to use default.</param>
    /// <returns>true if the command was sent and acknowledged successfully; otherwise, false.</returns>
    public bool SetApiSetSchemaNamespaceUse(string fileName)
    {
        string cmd = "apisetmapsrc";

        if (!string.IsNullOrEmpty(fileName))
        {
            cmd += $" file \"{fileName}\"";
        }

        cmd += "\r\n";

        return SendRequest(cmd) && IsRequestSuccessful();
    }

    /// <summary>
    /// Retrieves Known DLLs information of a specific type from the server.
    /// </summary>
    /// <param name="command">The command to send to the server.</param>
    /// <param name="knownDllsList">List to populate with Known DLL names.</param>
    /// <param name="knownDllsPath">Outputs the Known DLLs directory path.</param>
    /// <returns>true if the information was retrieved successfully; otherwise, false.</returns>
    private bool GetKnownDllsByType(string command, List<string> knownDllsList, out string knownDllsPath)
    {
        if (knownDllsList == null)
        {
            knownDllsPath = string.Empty;
            return false;
        }

        if (SendCommandAndReceiveReplyAsObjectJSON(command, typeof(CCoreKnownDlls), null)
            is CCoreKnownDlls knownDllsObject)
        {
            knownDllsList.Clear();
            if (knownDllsObject.Entries != null)
            {
                knownDllsList.AddRange(knownDllsObject.Entries);
            }
            knownDllsPath = knownDllsObject.DllPath ?? string.Empty;
            return true;
        }

        knownDllsPath = string.Empty;
        return false;
    }

    /// <summary>
    /// Retrieves both 32-bit and 64-bit Known DLLs information from the server.
    /// </summary>
    /// <param name="knownDlls">List to populate with 64-bit Known DLL names.</param>
    /// <param name="knownDlls32">List to populate with 32-bit Known DLL names.</param>
    /// <param name="knownDllsPath">Outputs the 64-bit Known DLLs directory path.</param>
    /// <param name="knownDllsPath32">Outputs the 32-bit Known DLLs directory path.</param>
    /// <returns>true if both 32-bit and 64-bit information was retrieved successfully; otherwise, false.</returns>
    public bool GetKnownDllsAll(List<string> knownDlls, List<string> knownDlls32, out string knownDllsPath, out string knownDllsPath32)
    {
        if (knownDlls == null || knownDlls32 == null)
        {
            knownDllsPath = string.Empty;
            knownDllsPath32 = string.Empty;
            return false;
        }

        bool result32 = GetKnownDllsByType(CConsts.CMD_KNOWNDLLS32, knownDlls32, out knownDllsPath32);
        bool result64 = GetKnownDllsByType(CConsts.CMD_KNOWNDLLS64, knownDlls, out knownDllsPath);

        return result32 && result64;
    }

    /// <summary>
    /// Cleans up resources after a failed connection attempt.
    /// </summary>
    /// <remarks>
    /// This method forcibly terminates the server process if it's still running,
    /// closes network streams and connections, and sets the error status.
    /// </remarks>
    private void CleanupFailedConnection()
    {
        // Safely terminate process if it's still running
        try
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _serverProcess.Kill();
                _serverProcess.Dispose();
            }
        }
        catch { }

        _serverProcess = null;

        _dataStream?.Close();
        _dataStream = null;

        _clientConnection?.Close();
        _clientConnection = null;

        ErrorStatus = ServerErrorStatus.GeneralException;
    }

    /// <summary>
    /// Starts the server process and establishes a network connection.
    /// </summary>
    /// <returns>true if the server started and connection was established successfully; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// This method performs the following operations:
    /// </para>
    /// <list type="number">
    /// <item>Validates the server executable path</item>
    /// <item>Attempts to start the server process with a cryptographically secure random port</item>
    /// <item>Establishes a TCP connection to the server</item>
    /// <item>Waits for and validates the server's HELLO message</item>
    /// </list>
    /// <para>
    /// The method will retry up to <see cref="SERVER_START_ATTEMPTS"/> times if port conflicts occur.
    /// In DEBUG builds, the server console window is visible for debugging purposes.
    /// In RELEASE builds, the server runs hidden with no console window.
    /// </para>
    /// </remarks>
    public bool ConnectClient()
    {
        Process tempProcess = null;
        TcpClient tempConnection = null;
        NetworkStream tempStream = null;
        string errMessage, fileName, canonicalPath, validationError, arguments;

        try
        {
            fileName = GetServerApplication();

            if (!ValidateServerPath(fileName, out canonicalPath, out validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            int startAttempts = SERVER_START_ATTEMPTS;
            int portNumber;

            do
            {
                portNumber = GenerateSecureRandomPort(CConsts.MinPortNumber, CConsts.MaxPortNumber);
                arguments = $"port {portNumber}";

                ProcessStartInfo processInfo = new()
                {
                    FileName = canonicalPath,
                    Arguments = arguments,
                    UseShellExecute = false,
#if DEBUG
                    CreateNoWindow = false,
#else
                    CreateNoWindow = true,
#endif
                    WorkingDirectory = Path.GetDirectoryName(canonicalPath)
                };

                tempProcess = Process.Start(processInfo);

                if (tempProcess == null)
                {
                    throw new Exception("Core process start failure");
                }

                Thread.Sleep(SERVER_START_DELAY_MS);

                if (tempProcess.HasExited)
                {
                    if (tempProcess.ExitCode != CConsts.SERVER_ERROR_INVALIDIP)
                    {
                        throw new Exception($"Server process exited with code {tempProcess.ExitCode}");
                    }
                    tempProcess.Dispose();
                    tempProcess = null;
                }
                else
                {
                    tempConnection = new();

                    Task connectTask = tempConnection.ConnectAsync(IPAddress, portNumber);
                    if (Task.WaitAny(new[] { connectTask }, CORE_CONNECTION_TIMEOUT) == 0)
                    {
                        if (tempConnection.Connected)
                        {
                            tempStream = tempConnection.GetStream();
                            tempStream.ReadTimeout = CORE_NETWORK_TIMEOUT;
                            tempStream.WriteTimeout = CORE_NETWORK_TIMEOUT;
                            Port = portNumber;
                            break;
                        }
                    }

                    tempConnection.Dispose();
                    tempConnection = null;
                }

            } while (--startAttempts > 0);

            // We couldn't connect server after all attempts.
            if (tempConnection == null || !tempConnection.Connected)
            {
                throw new Exception("Failed to connect to server after multiple attempts");
            }

            _serverProcess = tempProcess;
            _clientConnection = tempConnection;
            _dataStream = tempStream;

            CBufferChain idata = ReceiveReply();
            if (idata != null)
            {
                ErrorStatus = ServerErrorStatus.NoErrors;
                _addLogMessage($"Server has been started: {idata.BufferToStringNoCRLF()}", LogMessageType.System);
                return true;
            }
            else
            {
                ErrorStatus = ServerErrorStatus.ServerNeedRestart;
                _addLogMessage($"Server initialization failed, missing server HELLO", LogMessageType.ErrorOrWarning);
                CleanupFailedConnection();
                return false;
            }
        }
        catch (Exception ex)
        {
            if (ex is FileNotFoundException)
            {
                errMessage = $"{ex.Message} was not found, make sure it exist or change path to it: " +
                    $"Main menu -> Options -> Configuration, select Server tab, specify server application location and then press Connect button.";
            }
            else
            {
                errMessage = ex.Message;
            }

            tempStream?.Close();
            tempConnection?.Close();
            tempProcess?.Dispose();

            CleanupFailedConnection();
            _addLogMessage($"Server failed to start: {errMessage}", LogMessageType.ErrorOrWarning);
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the server and performs cleanup of all network resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method attempts a graceful shutdown by:
    /// </para>
    /// <list type="number">
    /// <item>Sending a shutdown request to the server</item>
    /// <item>Waiting briefly for the server to exit gracefully</item>
    /// <item>Forcibly terminating the server process if necessary</item>
    /// <item>Closing and disposing all network resources</item>
    /// </list>
    /// <para>
    /// This method is safe to call multiple times and handles exceptions internally.
    /// </para>
    /// </remarks>
    public void DisconnectClient()
    {
        try
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                ShutdownRequest();
                Thread.Sleep(SHUTDOWN_WAIT_MS);

                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            _addLogMessage($"Error during server shutdown: {ex.Message}", LogMessageType.ErrorOrWarning);
        }
        finally
        {
            if (_dataStream != null)
            {
                _dataStream.Close();
                _dataStream = null;
            }
            if (_clientConnection != null)
            {
                _clientConnection.Close();
                _clientConnection = null;
            }

            if (_serverProcess != null)
            {
                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }
    }
}
