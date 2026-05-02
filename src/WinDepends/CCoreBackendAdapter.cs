/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CCOREBACKENDADAPTER.CS
*
*  VERSION:     1.00
*
*  DATE:        26 Apr 2026
*
*  Core backend transport/protocol/domain adapter objects.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Net.Sockets;
using System.Text;

namespace WinDepends;

/// <summary>
/// Represents a single command request to be sent to the core backend server.
/// </summary>
/// <param name="Command">The raw command string, including any arguments and CRLF terminator.</param>
internal readonly record struct CCoreBackendRequest(string Command);

/// <summary>
/// Represents a status-line response received from the core backend server.
/// </summary>
/// <param name="Value">The raw status string as received from the server.</param>
internal readonly record struct CCoreBackendStatusResponse(string Value)
{
    /// <summary>
    /// Gets a value indicating whether the server acknowledged the request with a 200 OK status.
    /// </summary>
    public bool IsSuccess =>
        string.Equals(Value, CConsts.WDEP_STATUS_200, StringComparison.Ordinal);

    /// <summary>
    /// Gets a value indicating whether the server reported an internal exception (6xx range).
    /// </summary>
    public bool HasServerException =>
        Value.StartsWith(CConsts.WDEP_STATUS_600, StringComparison.Ordinal);
}

/// <summary>
/// Represents a payload (data) response received from the core backend server.
/// </summary>
/// <param name="Value">The deserialized payload string with CRLF characters stripped.</param>
internal readonly record struct CCoreBackendPayloadResponse(string Value)
{
    /// <summary>
    /// Gets a value indicating whether the server returned no payload data.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);
}

/// <summary>
/// Provides static methods for building backend request objects and mapping raw server
/// responses to domain-level results.
/// </summary>
/// <remarks>
/// All methods are stateless. No instances of this class are created.
/// </remarks>
internal static class CCoreProtocolMapper
{
    /// <summary>
    /// Wraps a raw command string into a <see cref="CCoreBackendRequest"/> value.
    /// </summary>
    /// <param name="command">The fully-formed command string to send to the server.</param>
    /// <returns>A new <see cref="CCoreBackendRequest"/> carrying the supplied command.</returns>
    public static CCoreBackendRequest CreateRequest(string command)
    {
        return new(command);
    }

    /// <summary>
    /// Constructs a <see cref="CCoreBackendStatusResponse"/> from a raw buffer chain
    /// returned by the transport layer.
    /// </summary>
    /// <remarks>
    /// Only the first <see cref="CBufferChain.DataSize"/> characters of the buffer are
    /// consumed to avoid reading stale data beyond the server response boundary.
    /// </remarks>
    /// <param name="reply">
    /// Buffer chain as returned by <see cref="CCoreTransportAdapter.ReceiveReply"/>;
    /// may be null, in which case an empty status is produced.
    /// </param>
    /// <returns>
    /// A <see cref="CCoreBackendStatusResponse"/> whose Value holds the raw status string,
    /// or an empty-value response if <paramref name="reply"/> is null or contains no data.
    /// </returns>
    public static CCoreBackendStatusResponse CreateStatusResponse(CBufferChain reply)
    {
        string value = string.Empty;
        if (reply?.Data is { Length: > 0 } data)
        {
            value = new string(data, 0, (int)Math.Min(reply.DataSize, data.Length));
        }

        return new CCoreBackendStatusResponse(value);
    }

    /// <summary>
    /// Constructs a <see cref="CCoreBackendPayloadResponse"/> by converting the full buffer
    /// chain to a string and stripping any embedded CRLF sequences.
    /// </summary>
    /// <param name="reply">
    /// Buffer chain as returned by <see cref="CCoreTransportAdapter.ReceiveReply"/>;
    /// may be null, in which case an empty payload is produced.
    /// </param>
    /// <returns>
    /// A <see cref="CCoreBackendPayloadResponse"/> whose Value holds the cleaned payload,
    /// or an empty-value response if <paramref name="reply"/> is null.
    /// </returns>
    public static CCoreBackendPayloadResponse CreatePayloadResponse(CBufferChain reply)
    {
        string value = reply?.BufferToStringNoCRLF() ?? string.Empty;
        return new CCoreBackendPayloadResponse(value);
    }

    /// <summary>
    /// Constructs the "open file" command request for the given module, appending optional
    /// processing flags derived from the supplied settings.
    /// </summary>
    /// <param name="module">Module descriptor whose FileName is used as the file path argument.</param>
    /// <param name="settings">File-open options controlling which optional flags are appended.</param>
    /// <returns>
    /// A <see cref="CCoreBackendRequest"/> containing the fully formed "open file ..." command
    /// terminated with CRLF.
    /// </returns>
    public static CCoreBackendRequest BuildOpenModuleRequest(CModule module, CFileOpenSettings settings)
    {
        var sb = new StringBuilder($"open file \"{module.FileName}\"");

        if (settings.UseStats)
            sb.Append(" use_stats");

        if (settings.ProcessRelocsForImage)
            sb.Append(" process_relocs");

        if (settings.UseCustomImageBase)
            sb.Append($" custom_image_base {settings.CustomImageBase}");

        sb.Append("\r\n");
        return new CCoreBackendRequest(sb.ToString());
    }

    /// <summary>
    /// Translates a raw server status response received after an "open file" command into
    /// a typed <see cref="ModuleOpenStatus"/> value, updating module error flags as appropriate.
    /// </summary>
    /// <param name="response">Status response as produced by <see cref="CreateStatusResponse"/>.</param>
    /// <param name="module">
    /// Module descriptor updated in-place on failure; its <c>FileNotFound</c> or
    /// <c>IsInvalid</c> flag is set as appropriate.
    /// </param>
    /// <returns>
    /// <see cref="ModuleOpenStatus.Okay"/> on HTTP 200.
    /// <see cref="ModuleOpenStatus.ErrorFileNotFound"/> on HTTP 404.
    /// <see cref="ModuleOpenStatus.ErrorCannotReadFileHeaders"/> on HTTP 403.
    /// <see cref="ModuleOpenStatus.ErrorInvalidHeadersOrSignatures"/> on HTTP 415.
    /// <see cref="ModuleOpenStatus.ErrorFileNotMapped"/> on HTTP 502.
    /// <see cref="ModuleOpenStatus.ErrorUnspecified"/> for any other status.
    /// </returns>
    public static ModuleOpenStatus MapOpenModuleStatus(CCoreBackendStatusResponse response, CModule module)
    {
        if (response.IsSuccess)
            return ModuleOpenStatus.Okay;

        return response.Value switch
        {
            var s when string.Equals(s, CConsts.WDEP_STATUS_404, StringComparison.Ordinal) =>
               MarkModule(module, true, false, ModuleOpenStatus.ErrorFileNotFound),

            var s when string.Equals(s, CConsts.WDEP_STATUS_403, StringComparison.Ordinal) =>
                MarkModule(module, false, true, ModuleOpenStatus.ErrorCannotReadFileHeaders),

            var s when string.Equals(s, CConsts.WDEP_STATUS_415, StringComparison.Ordinal) =>
                MarkModule(module, false, true, ModuleOpenStatus.ErrorInvalidHeadersOrSignatures),

            var s when string.Equals(s, CConsts.WDEP_STATUS_502, StringComparison.Ordinal) =>
               MarkModule(module, false, true, ModuleOpenStatus.ErrorFileNotMapped),

            _ => MarkModule(module, false, true, ModuleOpenStatus.ErrorUnspecified)
        };
    }

    /// <summary>
    /// Attempts to construct the backend request and expected response CLR type for a given
    /// module information query category.
    /// </summary>
    /// <param name="moduleInformationType">
    /// Specifies which kind of module data to query
    /// (Headers, Imports, Exports, DataDirectories, ApiSetName).
    /// </param>
    /// <param name="parameters">
    /// Auxiliary argument string used only for <see cref="ModuleInformationType.ApiSetName"/>
    /// queries; ignored for all other types.
    /// </param>
    /// <param name="request">
    /// On success, receives the fully formed request; set to default on failure.
    /// </param>
    /// <param name="responseType">
    /// On success, receives the CLR <see cref="Type"/> used to deserialize the server payload
    /// (e.g. <c>typeof(CCoreImports)</c>); set to null on failure.
    /// </param>
    /// <returns>
    /// true if <paramref name="moduleInformationType"/> is a recognized value and
    /// <paramref name="request"/>/<paramref name="responseType"/> have been populated;
    /// false if the type is unrecognized.
    /// </returns>
    public static bool TryBuildModuleInformationRequest(
        ModuleInformationType moduleInformationType,
        string parameters,
        out CCoreBackendRequest request,
        out Type? responseType)
    {
        request = default;
        responseType = null;

        switch (moduleInformationType)
        {
            case ModuleInformationType.Headers:
                request = new(CConsts.CMD_HEADERS);
                responseType = typeof(CCoreImageHeaders);
                return true;
            case ModuleInformationType.Imports:
                request = new(CConsts.CMD_IMPORTS);
                responseType = typeof(CCoreImports);
                return true;
            case ModuleInformationType.Exports:
                request = new(CConsts.CMD_EXPORTS);
                responseType = typeof(CCoreExports);
                return true;
            case ModuleInformationType.DataDirectories:
                request = new(CConsts.CMD_DATADIRS);
                responseType = typeof(CCoreDirectoryEntry);
                return true;
            case ModuleInformationType.ApiSetName:
                request = new($"apisetresolve {parameters}\r\n");
                responseType = typeof(CCoreResolvedFileName);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Constructs the request that queries API set namespace information.
    /// </summary>
    /// <remarks>
    /// When a file path is provided the server reads the schema from that file;
    /// otherwise it uses the namespace already loaded in the server process.
    /// </remarks>
    /// <param name="apiSetFilePath">
    /// Optional path to an external API set schema file.
    /// Pass null or empty to query the in-process namespace.
    /// </param>
    /// <returns>A <see cref="CCoreBackendRequest"/> for the "apisetnsinfo" command.</returns>
    public static CCoreBackendRequest BuildApiSetNamespaceInfoRequest(string apiSetFilePath)
    {
        if (!string.IsNullOrEmpty(apiSetFilePath))
            return new($"apisetnsinfo file \"{apiSetFilePath}\"\r\n");

        return new(CConsts.CMD_APISETNINFO);
    }

    /// <summary>
    /// Constructs the request that instructs the server which API set schema source to use
    /// for subsequent resolve operations.
    /// </summary>
    /// <remarks>
    /// Supplying an empty or null <paramref name="fileName"/> switches the server back to its
    /// built-in (in-process) mapping source.
    /// </remarks>
    /// <param name="fileName">
    /// Path to the external API set schema file to activate,
    /// or null/empty to revert to the default in-process source.
    /// </param>
    /// <returns>A <see cref="CCoreBackendRequest"/> for the "apisetmapsrc" command.</returns>
    public static CCoreBackendRequest BuildApiSetSchemaNamespaceUseRequest(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return new("apisetmapsrc\r\n");

        return new($"apisetmapsrc file \"{fileName}\"\r\n");
    }

    /// <summary>
    /// Applies error flags to a module descriptor and returns the specified status code.
    /// </summary>
    /// <param name="module">Module descriptor to update.</param>
    /// <param name="fileNotFound">When true, sets <c>module.FileNotFound</c>.</param>
    /// <param name="invalid">When true, sets <c>module.IsInvalid</c>.</param>
    /// <param name="status">The <see cref="ModuleOpenStatus"/> value to return.</param>
    /// <returns>The <paramref name="status"/> value passed in, unchanged.</returns>
    private static ModuleOpenStatus MarkModule(CModule module, bool fileNotFound, bool invalid, ModuleOpenStatus status)
    {
        if (fileNotFound)
            module.FileNotFound = true;

        if (invalid)
            module.IsInvalid = true;

        return status;
    }
}

/// <summary>
/// Provides static methods that translate raw server-side data objects into the WinDepends domain model.
/// </summary>
/// <remarks>
/// All methods are stateless. No instances of this class are created.
/// </remarks>
internal static class CCoreDomainMapper
{
    /// <summary>
    /// Populates the <c>ModuleData</c> fields of a module descriptor from a
    /// <see cref="CCoreFileInformation"/> object returned by the backend server.
    /// </summary>
     /// <param name="module">
    /// Destination module descriptor whose <c>ModuleData</c> fields are updated in-place;
    /// must not be null.
    /// </param>
    /// <param name="fileInformation">Source data object received from the server; must not be null.</param>
    /// <returns>
    /// <see cref="ModuleOpenStatus.Okay"/> on success.
    /// <see cref="ModuleOpenStatus.ErrorUnspecified"/> if either parameter is null.
    /// </returns>
    public static ModuleOpenStatus ApplyFileInformation(CModule module, CCoreFileInformation fileInformation)
    {
        if (module == null || fileInformation == null)
            return ModuleOpenStatus.ErrorUnspecified;

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
}

/// <summary>
/// Handles low-level TCP send and receive operations between the WinDepends front-end
/// and the core backend server process.
/// </summary>
/// <remarks>
/// The adapter is intentionally decoupled from connection management: it receives accessor
/// delegates for the <see cref="TcpClient"/> and <see cref="NetworkStream"/> so that the
/// owning class retains full control over connection lifetime.
/// </remarks>
internal sealed class CCoreTransportAdapter
{
    private readonly Func<TcpClient?> _clientAccessor;
    private readonly Func<NetworkStream?> _streamAccessor;
    private readonly AddLogMessageCallback _addLogMessage;

    public CCoreTransportAdapter(
        Func<TcpClient?> clientAccessor,
        Func<NetworkStream?> streamAccessor,
        AddLogMessageCallback addLogMessage)
    {
        _clientAccessor = clientAccessor ?? throw new ArgumentNullException(nameof(clientAccessor));
        _streamAccessor = streamAccessor ?? throw new ArgumentNullException(nameof(streamAccessor));
        _addLogMessage = addLogMessage ?? throw new ArgumentNullException(nameof(addLogMessage));
    }


    /// <summary>
    /// Encodes the command string from <paramref name="request"/> as UTF-16LE and writes it
    /// to the network stream.
    /// </summary>
    /// <param name="request">The backend request whose Command string is transmitted.</param>
    /// <param name="status">
    /// On return, holds a <see cref="ServerErrorStatus"/> describing the outcome.</param>
    /// <returns>
    /// true if the command was transmitted without error;
    /// false if a connectivity or I/O failure prevented transmission.
    /// </returns>
    public bool TrySend(CCoreBackendRequest request, out ServerErrorStatus status)
    {
        var client = _clientAccessor();
        if (client == null || !client.Connected)
        {
            status = ServerErrorStatus.ServerNeedRestart;
            return false;
        }

        var stream = _streamAccessor();
        if (stream == null)
        {
            status = ServerErrorStatus.NetworkStreamNotInitialized;
            return false;
        }

        try
        {
            using BinaryWriter bw = new(stream, Encoding.Unicode, true);
            bw.Write(Encoding.Unicode.GetBytes(request.Command));
        }
        catch (Exception ex)
        {
            _addLogMessage($"Failed to send data to the server, error message: {ex.Message}", LogMessageType.ErrorOrWarning);
            status = (ex is IOException) ? ServerErrorStatus.SocketException : ServerErrorStatus.GeneralException;
            return false;
        }

        status = ServerErrorStatus.NoErrors;
        return true;
    }

    /// <summary>
    /// Reads UTF-16LE characters from the network stream into a linked chain of
    /// <see cref="CBufferChain"/> nodes until a CRLF sequence is encountered,
    /// which marks the end of a single server response line.
    /// </summary>
    /// <param name="status">
    /// On return, holds a <see cref="ServerErrorStatus"/> describing the outcome.</param>
    /// <returns>
    /// The root <see cref="CBufferChain"/> node of the received data on success or partial read;
    /// null if the stream is unavailable or a non-I/O exception is thrown.
    /// </returns>
    public CBufferChain ReceiveReply(out ServerErrorStatus status)
    {
        var stream = _streamAccessor();
        if (stream == null)
        {
            status = ServerErrorStatus.NetworkStreamNotInitialized;
            return null;
        }

        try
        {
            using BinaryReader br = new(stream, Encoding.Unicode, true);

            CBufferChain bufferChain = new();
            CBufferChain rootBuffer = bufferChain;
            char previousChar = '\0';

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
                            status = ServerErrorStatus.NoErrors;
                            return rootBuffer;
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        bufferChain.DataSize = (uint)i;
                        status = ServerErrorStatus.SocketException;
                        return i > 0 || rootBuffer != bufferChain ? rootBuffer : null;
                    }
                    catch (IOException)
                    {
                        bufferChain.DataSize = (uint)i;
                        status = ServerErrorStatus.SocketException;
                        return i > 0 || rootBuffer != bufferChain ? rootBuffer : null;
                    }

                    previousChar = bufferChain.Data[i];
                }

                bufferChain.Next = new();
                bufferChain = bufferChain.Next;
            }
        }
        catch (Exception ex)
        {
            _addLogMessage($"Receive data failed. Server message: {ex.Message}", LogMessageType.ErrorOrWarning);
            status = ServerErrorStatus.GeneralException;
            return null;
        }
    }
}
