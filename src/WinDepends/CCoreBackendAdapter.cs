/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CCOREBACKENDADAPTER.CS
*
*  VERSION:     1.00
*
*  DATE:        22 Apr 2026
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

internal readonly record struct CCoreBackendRequest(string Command);

internal readonly record struct CCoreBackendStatusResponse(string Value)
{
    public bool IsSuccess =>
        string.Equals(Value, CConsts.WDEP_STATUS_200, StringComparison.Ordinal);

    public bool HasServerException =>
        Value.StartsWith(CConsts.WDEP_STATUS_600, StringComparison.Ordinal);
}

internal readonly record struct CCoreBackendPayloadResponse(string Value)
{
    public bool IsEmpty => string.IsNullOrEmpty(Value);
}

internal static class CCoreProtocolMapper
{
    public static CCoreBackendRequest CreateRequest(string command)
    {
        return new(command);
    }

    public static CCoreBackendStatusResponse CreateStatusResponse(CBufferChain reply)
    {
        string value = string.Empty;
        if (reply?.Data is { Length: > 0 } data)
        {
            value = new string(data, 0, (int)Math.Min(reply.DataSize, data.Length));
        }

        return new CCoreBackendStatusResponse(value);
    }

    public static CCoreBackendPayloadResponse CreatePayloadResponse(CBufferChain reply)
    {
        string value = reply?.BufferToStringNoCRLF() ?? string.Empty;
        return new CCoreBackendPayloadResponse(value);
    }

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

    public static CCoreBackendRequest BuildApiSetNamespaceInfoRequest(string apiSetFilePath)
    {
        if (!string.IsNullOrEmpty(apiSetFilePath))
            return new($"apisetnsinfo file \"{apiSetFilePath}\"\r\n");

        return new(CConsts.CMD_APISETNINFO);
    }

    public static CCoreBackendRequest BuildApiSetSchemaNamespaceUseRequest(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return new("apisetmapsrc\r\n");

        return new($"apisetmapsrc file \"{fileName}\"\r\n");
    }

    private static ModuleOpenStatus MarkModule(CModule module, bool fileNotFound, bool invalid, ModuleOpenStatus status)
    {
        if (fileNotFound)
            module.FileNotFound = true;

        if (invalid)
            module.IsInvalid = true;

        return status;
    }
}

internal static class CCoreDomainMapper
{
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
