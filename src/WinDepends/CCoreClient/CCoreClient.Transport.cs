/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       CCORECLIENT.TRANSPORT.CS
*
*  VERSION:     1.00
*
*  DATE:        14 Jul 2026
*  
*  Transport and reply handling routines for Core Server communication class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

public partial class CCoreClient
{
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
    /// Checks if the server reply indicates a successful operation.
    /// </summary>
    /// <param name="module">Optional module context for error reporting.</param>
    /// <returns>true if the request was successful; otherwise, false.</returns>
    public bool IsRequestSuccessful(CModule module = null)
    {
        CBufferChain idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
            return false;

        var statusResponse = CCoreProtocolMapper.CreateStatusResponse(idata);
        if (statusResponse.IsSuccess)
            return true;

        if (statusResponse.HasServerException)
            CheckExceptionInReply(module);

        return false;
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

        if (!SendRequest(CCoreProtocolMapper.CreateRequest(command)))
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

        var payloadResponse = CCoreProtocolMapper.CreatePayloadResponse(idata);
        if (payloadResponse.IsEmpty)
        {
            return null;
        }

        return DeserializeDataJSON(module?.FileName, objectType, payloadResponse.Value);
    }

    /// <summary>
    /// Sends a typed request message to the server.
    /// </summary>
    /// <param name="request">The backend request to send.</param>
    /// <returns>true if the message was sent successfully; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the client has been disposed.</exception>
    private bool SendRequest(CCoreBackendRequest request)
    {
        ThrowIfDisposed();
        bool result = _transportAdapter.TrySend(request, out var status);
        ErrorStatus = status;
        return result;
    }

    /// <summary>
    /// Sends a command message to the server.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>true if the message was sent successfully; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the client has been disposed.</exception>
    private bool SendRequest(string message)
    {
        return SendRequest(CCoreProtocolMapper.CreateRequest(message));
    }

    /// <summary>
    /// Receives a reply from the server and stores it in a buffer chain.
    /// </summary>
    /// <returns>A <see cref="CBufferChain"/> containing the server response, or null if an error occurred.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the client has been disposed.</exception>
    private CBufferChain ReceiveReply()
    {
        ThrowIfDisposed();

        var reply = _transportAdapter.ReceiveReply(out var status);
        ErrorStatus = status;
        return reply;
    }
}
