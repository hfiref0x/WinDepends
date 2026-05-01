/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CFILEOPENORCHESTRATIONSERVICE.CS
*
*  VERSION:     1.00
*
*  DATE:        21 Apr 2026
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
namespace WinDepends;

internal sealed class CFileOpenPipelineState
{
    public CFileOpenPipelineState(string? originalFileName)
    {
        OriginalFileName = originalFileName;
        ResolvedFileName = originalFileName;
        Result = FileOpenResult.Cancelled;
        LogMessage = string.Empty;
        LogMessageType = LogMessageType.Normal;
    }

    public string? OriginalFileName { get; }
    public string? ResolvedFileName { get; set; }
    public FileOpenResult Result { get; set; }
    public bool IsSuccess { get; set; }
    public string LogMessage { get; set; }
    public LogMessageType LogMessageType { get; set; }
}

internal sealed class CFileOpenOrchestrationService
{
    public async Task<bool> ExecuteAsync(
        CFileOpenPipelineState state,
        Func<string?, CancellationToken, Task<FileOpenResult>> openInputFileInternal,
        Action<string, LogMessageType> addLogMessage,
        Action<string> updateOperationStatus,
        Action<bool> setUiEnabled,
        Action focusTreeView,
        CancellationToken cancellationToken)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (openInputFileInternal == null)
            throw new ArgumentNullException(nameof(openInputFileInternal));

        if (addLogMessage == null)
            throw new ArgumentNullException(nameof(addLogMessage));

        if (updateOperationStatus == null)
            throw new ArgumentNullException(nameof(updateOperationStatus));

        if (setUiEnabled == null)
            throw new ArgumentNullException(nameof(setUiEnabled));

        if (focusTreeView == null)
            throw new ArgumentNullException(nameof(focusTreeView));

        try
        {
            setUiEnabled(false);

            cancellationToken.ThrowIfCancellationRequested();
            state.ResolvedFileName = ResolveShortcut(state.OriginalFileName);
            state.Result = await openInputFileInternal(state.ResolvedFileName, cancellationToken).ConfigureAwait(true);
            state.IsSuccess = state.Result == FileOpenResult.Success || state.Result == FileOpenResult.SuccessSession;

            PopulateResultMessage(state);
            addLogMessage(state.LogMessage, state.LogMessageType);
            updateOperationStatus(state.LogMessage);
        }
        catch (OperationCanceledException)
        {
            state.IsSuccess = false;
            state.Result = FileOpenResult.Cancelled;
            PopulateResultMessage(state);
            addLogMessage(state.LogMessage, state.LogMessageType);
            updateOperationStatus(state.LogMessage);
        }
        catch
        {
            state.IsSuccess = false;
            state.Result = FileOpenResult.Failure;
            state.LogMessage = $"There is an error while processing \"{state.OriginalFileName}\" file.";
            state.LogMessageType = LogMessageType.ErrorOrWarning;

            addLogMessage(state.LogMessage, state.LogMessageType);
            updateOperationStatus(state.LogMessage);
        }
        finally
        {
            setUiEnabled(true);
            focusTreeView();
        }

        return state.IsSuccess;
    }

    private static string? ResolveShortcut(string? fileName)
    {
        var fileExtension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(fileExtension) &&
            fileExtension.Equals(CConsts.ShortcutFileExt, StringComparison.OrdinalIgnoreCase))
        {
            return NativeMethods.ResolveShortcutTarget(fileName);
        }

        return fileName;
    }

    private static void PopulateResultMessage(CFileOpenPipelineState state)
    {
        switch (state.Result)
        {
            case FileOpenResult.SuccessSession:
                state.LogMessage = $"Session file \"{state.ResolvedFileName}\" has been opened.";
                state.LogMessageType = LogMessageType.System;
                break;
            case FileOpenResult.Success:
                state.LogMessage = $"Analysis of \"{state.ResolvedFileName}\" has been completed.";
                state.LogMessageType = LogMessageType.Information;
                break;
            case FileOpenResult.Failure:
                state.LogMessage = $"There is an error while processing \"{state.ResolvedFileName}\" file.";
                state.LogMessageType = LogMessageType.ErrorOrWarning;
                break;
            case FileOpenResult.Cancelled:
            default:
                state.LogMessage = "Operation has been cancelled.";
                state.LogMessageType = LogMessageType.Normal;
                break;
        }
    }
}
