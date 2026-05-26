/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CFILEOPENSTATE.CS
*
*  VERSION:     1.00
*
*  DATE:        21 Apr 2026
*
*  File/Session open state class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

/// <summary>
/// Represents the state of a single file or session open operation,
/// carrying both the input parameters and the outcome of the operation.
/// Instances are created before the operation begins and mutated as it
/// progresses through <see cref="CFileOpenOrchestrationService.Execute"/>.
/// </summary>
internal sealed class CFileOpenState
{
    /// <summary>
    /// Initialises a new <see cref="CFileOpenState"/> for the given file.
    /// </summary>
    /// <param name="originalFileName">
    /// The raw file path as supplied by the caller (may be a .lnk shortcut).
    /// <c>null</c> is accepted; the operation will handle it gracefully.
    /// </param>
    public CFileOpenState(string? originalFileName)
    {
        OriginalFileName = originalFileName;

        // ResolvedFileName starts as the original; it is updated to the
        // shortcut target path (if applicable) before the file is opened.
        ResolvedFileName = originalFileName;

        // Default result: nothing has been attempted yet.
        Result = FileOpenResult.Cancelled;

        LogMessage = string.Empty;
        LogMessageType = LogMessageType.Normal;
    }

    /// <summary>
    /// Gets the file path exactly as it was provided by the caller,
    /// before any shortcut resolution takes place.
    /// This value never changes after construction.
    /// </summary>
    public string? OriginalFileName { get; }

    /// <summary>
    /// Gets or sets the effective file path used for the open operation.
    /// If <see cref="OriginalFileName"/> points to a .lnk shortcut, this
    /// property is updated to the resolved target before the file is opened;
    /// otherwise it remains equal to <see cref="OriginalFileName"/>.
    /// </summary>
    public string? ResolvedFileName { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the open operation.
    /// Initialised to <see cref="FileOpenResult.Cancelled"/> and updated
    /// by <see cref="CFileOpenOrchestrationService"/> once the operation
    /// completes (or fails).
    /// </summary>
    public FileOpenResult Result { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operation ended
    /// successfully.  <c>true</c> when <see cref="Result"/> is either
    /// <see cref="FileOpenResult.Success"/> or
    /// <see cref="FileOpenResult.SuccessSession"/>; <c>false</c> otherwise.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the human-readable message that describes the outcome
    /// of the operation.  Written to the application log and displayed in
    /// the status bar.
    /// </summary>
    public string LogMessage { get; set; }

    /// <summary>
    /// Gets or sets the severity/category of <see cref="LogMessage"/>,
    /// controlling how the message is rendered in the application log
    /// (e.g. normal, information, error/warning, system).
    /// </summary>
    public LogMessageType LogMessageType { get; set; }
}

/// <summary>
/// Orchestrates the end-to-end workflow of opening a file or session.
/// <para>
/// Responsibilities (in order):
/// <list type="number">
///   <item>Disable the UI to prevent re-entrant operations.</item>
///   <item>Resolve any .lnk shortcut to its real target path.</item>
///   <item>Delegate the actual file parsing to the caller-supplied open function.</item>
///   <item>Derive and record an appropriate log message for the result.</item>
///   <item>Re-enable the UI and return focus to the tree-view.</item>
/// </list>
/// </para>
/// <para>
/// All UI interactions are injected as delegates so that this class remains
/// independent of any specific UI framework and is straightforward to unit-test.
/// </para>
/// </summary>
internal sealed class CFileOpenOrchestrationService
{
    /// <summary>
    /// Executes the file-open workflow described in the class summary.
    /// </summary>
    /// <param name="state">
    /// A pre-created <see cref="CFileOpenState"/> whose
    /// <see cref="CFileOpenState.OriginalFileName"/> identifies the file to
    /// open.  The remaining properties are populated by this method.
    /// Must not be <c>null</c>.
    /// </param>
    /// <param name="openInputFileInternal">
    /// The core open function provided by the caller.  Receives the resolved
    /// file path and returns a <see cref="FileOpenResult"/> that reflects
    /// whether the file was opened successfully, whether it was a session
    /// file, or whether it failed.
    /// Must not be <c>null</c>.
    /// </param>
    /// <param name="addLogMessage">
    /// Callback that appends a message to the application log.
    /// Receives the message text and its <see cref="LogMessageType"/>.
    /// Must not be <c>null</c>.
    /// </param>
    /// <param name="updateOperationStatus">
    /// Callback that pushes a short status string to the UI status bar
    /// (or equivalent).
    /// Must not be <c>null</c>.
    /// </param>
    /// <param name="setUiEnabled">
    /// Callback that enables (<c>true</c>) or disables (<c>false</c>) the
    /// interactive portions of the UI.  Called with <c>false</c> at the start
    /// and with <c>true</c> in the <c>finally</c> block, guaranteeing
    /// re-enablement even when an exception is thrown.
    /// Must not be <c>null</c>.
    /// </param>
    /// <param name="focusTreeView">
    /// Callback that returns keyboard focus to the dependency tree-view after
    /// the operation completes.  Called in the <c>finally</c> block.
    /// Must not be <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when the file was opened successfully (i.e.
    /// <see cref="CFileOpenState.IsSuccess"/> is <c>true</c>);
    /// <c>false</c> for any other outcome including cancellation and errors.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required parameter is <c>null</c>.
    /// </exception>
    public bool Execute(
        CFileOpenState state,
        Func<string?, FileOpenResult> openInputFileInternal,
        Action<string, LogMessageType> addLogMessage,
        Action<string> updateOperationStatus,
        Action<bool> setUiEnabled,
        Action focusTreeView)
    {
        // Validate all required delegates up-front so that callers receive
        // a clear exception rather than a NullReferenceException deep inside
        // the workflow.
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
            // Lock the UI for the duration of the (potentially expensive)
            // file-open operation.
            setUiEnabled(false);

            // If the caller supplied a .lnk file, transparently follow the
            // shortcut to its real target before handing the path on.
            state.ResolvedFileName = ResolveShortcut(state.OriginalFileName);

            // Delegate actual parsing/loading to the caller-provided function.
            state.Result = openInputFileInternal(state.ResolvedFileName);

            // Treat both a plain file success and a session-file success as
            // overall success.
            state.IsSuccess = state.Result == FileOpenResult.Success
                           || state.Result == FileOpenResult.SuccessSession;

            // Build the log message that matches the result code.
            PopulateResultMessage(state);
            addLogMessage(state.LogMessage, state.LogMessageType);
            updateOperationStatus(state.LogMessage);
        }
        catch
        {
            // An unexpected exception occurred (e.g. I/O error, access
            // denied).  Mark the operation as failed and surface a generic
            // error message; the original exception is intentionally swallowed
            // here because the caller communicates failure through the state
            // object rather than via exceptions.
            state.IsSuccess = false;
            state.Result = FileOpenResult.Failure;
            state.LogMessage = $"There is an error while processing \"{state.OriginalFileName}\" file.";
            state.LogMessageType = LogMessageType.ErrorOrWarning;

            addLogMessage(state.LogMessage, state.LogMessageType);
            updateOperationStatus(state.LogMessage);
        }
        finally
        {
            // Always re-enable the UI and restore focus, regardless of
            // whether the operation succeeded, failed, or threw.
            setUiEnabled(true);
            focusTreeView();
        }

        return state.IsSuccess;
    }

    /// <summary>
    /// Resolves a Windows Shell shortcut (.lnk) to its real target path.
    /// If <paramref name="fileName"/> does not have the .lnk extension the
    /// original value is returned unchanged.
    /// </summary>
    /// <param name="fileName">
    /// The file path to examine.  May be <c>null</c>.
    /// </param>
    /// <returns>
    /// The resolved target path when <paramref name="fileName"/> is a
    /// shortcut; otherwise the original <paramref name="fileName"/> value.
    /// </returns>
    private static string? ResolveShortcut(string? fileName)
    {
        var fileExtension = Path.GetExtension(fileName);

        // Only attempt resolution when the file actually carries the
        // shortcut extension (case-insensitive comparison for robustness
        // on case-preserving file systems).
        if (!string.IsNullOrEmpty(fileExtension) &&
            fileExtension.Equals(CConsts.ShortcutFileExt, StringComparison.OrdinalIgnoreCase))
        {
            return NativeMethods.ResolveShortcutTarget(fileName);
        }

        return fileName;
    }

    /// <summary>
    /// Populates <see cref="CFileOpenState.LogMessage"/> and
    /// <see cref="CFileOpenState.LogMessageType"/> based on the value of
    /// <see cref="CFileOpenState.Result"/>.
    /// </summary>
    /// <param name="state">
    /// The state object to update in-place.  Its <see cref="CFileOpenState.Result"/>
    /// must already reflect the outcome of the open operation.
    /// </param>
    /// <remarks>
    /// Each <see cref="FileOpenResult"/> value maps to a distinct message
    /// template and log severity:
    /// <list type="table">
    ///   <listheader>
    ///     <term>Result</term>
    ///     <description>Message / Severity</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="FileOpenResult.SuccessSession"/></term>
    ///     <description>Session-opened confirmation / System</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="FileOpenResult.Success"/></term>
    ///     <description>Analysis-completed confirmation / Information</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="FileOpenResult.Failure"/></term>
    ///     <description>Processing error / ErrorOrWarning</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="FileOpenResult.Cancelled"/> (default)</term>
    ///     <description>Cancellation notice / Normal</description>
    ///   </item>
    /// </list>
    /// </remarks>
    private static void PopulateResultMessage(CFileOpenState state)
    {
        switch (state.Result)
        {
            case FileOpenResult.SuccessSession:
                // A persisted session was loaded rather than a raw binary.
                state.LogMessage = $"Session file \"{state.ResolvedFileName}\" has been opened.";
                state.LogMessageType = LogMessageType.System;
                break;

            case FileOpenResult.Success:
                // A regular file was parsed and its dependency tree built.
                state.LogMessage = $"Analysis of \"{state.ResolvedFileName}\" has been completed.";
                state.LogMessageType = LogMessageType.Information;
                break;

            case FileOpenResult.Failure:
                // The open function signalled a failure (distinct from an
                // unexpected exception, which is caught in Execute).
                state.LogMessage = $"There is an error while processing \"{state.ResolvedFileName}\" file.";
                state.LogMessageType = LogMessageType.ErrorOrWarning;
                break;

            case FileOpenResult.Cancelled:
            default:
                // The user cancelled the dialog, or an unrecognised result
                // code was returned — treat both as a no-op.
                state.LogMessage = "Operation has been cancelled.";
                state.LogMessageType = LogMessageType.Normal;
                break;
        }
    }
}
