/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       MAINFORM.LOGGING.CS
*
*  VERSION:     1.00
*
*  DATE:        14 Jul 2026
*  
*  Log view rendering, interaction, and search routines for main form.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

public class LogMessageEventArgs(string message, LogMessageType messageType, Color? color, bool useBold, bool moduleMessage, CModule? relatedModule, RichTextBox? richTextBox) : EventArgs
{
    public string Message { get; } = message;
    public LogMessageType MessageType { get; } = messageType;
    public Color? Color { get; } = color;
    public bool UseBold { get; } = useBold;
    public bool ModuleMessage { get; } = moduleMessage;
    public CModule? RelatedModule { get; } = relatedModule;
    public RichTextBox RichTextBox { get; } = richTextBox;
}

public static class AppLogger
{
    public static event Action<LogMessageEventArgs>? OnLogMessage;
    private static RichTextBox? _defaultRichTextBox;

    public static void Initialize(RichTextBox richTextBox)
    {
        _defaultRichTextBox = richTextBox;
    }

    public static void Log(string message, LogMessageType messageType)
    {
        LogExt(message, messageType);
    }

    public static void LogExt(string message,
        LogMessageType messageType,
        Color? color = null,
        bool useBold = false,
        bool moduleMessage = false,
        CModule relatedModule = null,
        RichTextBox richTextBox = null)
    {
        RichTextBox targetControl = richTextBox ?? _defaultRichTextBox;

        OnLogMessage?.Invoke(new LogMessageEventArgs(
            message, messageType, color, useBold, moduleMessage, relatedModule, targetControl));
    }
}

public partial class MainForm
{
    private void AppLogger_OnLogMessage(LogMessageEventArgs e)
    {
        RichTextBox richTextBox = e.RichTextBox;

        Color outputColor = Color.Black;
        bool boldText = false;

        if (_shutdownInProgress)
            return;

        switch (e.MessageType)
        {
            case LogMessageType.ErrorOrWarning:
                boldText = true;
                outputColor = Color.Red;
                break;

            case LogMessageType.Information:
                boldText = true;
                break;

            case LogMessageType.System:
                boldText = true;
                outputColor = Color.Blue;
                break;

            case LogMessageType.ContentDefined:
                boldText = e.UseBold;
                outputColor = e.Color ?? Color.Black;
                break;

            case LogMessageType.Normal:
            default:
                break;
        }

        if (e.ModuleMessage)
        {
            _depends.ModuleAnalysisLog.Add(new LogEntry(e.Message, outputColor));
        }

        if (richTextBox != null && !richTextBox.IsDisposed)
        {
            richTextBox.SuspendLayout();

            int startPosition = richTextBox.TextLength;
            richTextBox.SelectionStart = startPosition;
            richTextBox.SelectionLength = 0;
            richTextBox.SelectionColor = outputColor;

            var baseFont = richTextBox.Font;
            if (boldText)
            {
                richTextBox.SelectionFont = new Font(baseFont, FontStyle.Bold);
            }
            else
            {
                richTextBox.SelectionFont = baseFont;
            }

            richTextBox.SelectedText = e.Message + Environment.NewLine;

            if (e.RelatedModule != null)
            {
                string justFileName = Path.GetFileName(e.RelatedModule.FileName);

                string[] patternsToCheck = {
                    $"\"{justFileName}\"",    // "filename.dll"
                    $"{justFileName}"         // filename.dll
                };

                foreach (var pattern in patternsToCheck)
                {
                    int moduleNameIndex = e.Message.IndexOf(pattern);
                    if (moduleNameIndex >= 0)
                    {
                        // Calculate the exact position in the RichTextBox
                        int linkStart = startPosition + moduleNameIndex;
                        int linkLength = pattern.Length;

                        // Apply ONLY underline to the module name, preserving the color
                        richTextBox.Select(linkStart, linkLength);
                        Font currentFont = richTextBox.SelectionFont ?? baseFont;
                        richTextBox.SelectionFont = new Font(
                            currentFont.FontFamily,
                            currentFont.Size,
                            currentFont.Style | FontStyle.Underline);
                        richTextBox.SelectionColor = outputColor;

                        // Store the link information
                        _moduleLinks.Add(new ModuleLinkInfo
                        {
                            Start = linkStart,
                            Length = linkLength,
                            InstanceId = e.RelatedModule.InstanceId
                        });
                        break;
                    }
                }
            }

            richTextBox.SelectionStart = richTextBox.TextLength;
            richTextBox.SelectionLength = 0;
            richTextBox.SelectionFont = baseFont;
            richTextBox.SelectionColor = richTextBox.ForeColor;
            richTextBox.ScrollToCaret();
            richTextBox.ResumeLayout();
        }
    }

    private void RichEditLog_ClearLog()
    {
        if (!reLog.IsDisposed)
        {
            reLog.Clear();
            ClearModuleLinks(); // Clear the links when log is cleared
        }
    }

    private void MenuClearLogItem_Click(object sender, EventArgs e)
    {
        reLog.Clear();
        ClearModuleLinks();
    }

    private void RichEditLog_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            richEditPopupMenu.Show(Cursor.Position);
        }
    }

    /// <summary>
    /// Handles clicks on module links in the log
    /// </summary>
    private void RichEditLog_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        int charIndex = reLog.GetCharIndexFromPosition(new Point(e.X, e.Y));
        ModuleLinkInfo linkInfo = FindModuleLinkAtPosition(charIndex);

        if (linkInfo != null)
        {
            FindAndSelectModuleNode(linkInfo.InstanceId);
        }
    }

    /// <summary>
    /// Handles mouse movement over the rich text log to provide cursor feedback for module links
    /// </summary>
    private void RichEditLog_MouseMove(object sender, MouseEventArgs e)
    {
        // Get character index at current mouse position
        int charIndex = reLog.GetCharIndexFromPosition(new Point(e.X, e.Y));
        ModuleLinkInfo linkInfo = FindModuleLinkAtPosition(charIndex);

        bool overLink = (linkInfo != null);
        int instanceId = overLink ? linkInfo.InstanceId : 0;

        if (overLink != _isCurrentlyOverLink ||
            (overLink && instanceId != _currentLinkInstanceId))
        {
            reLog.Cursor = overLink ? Cursors.Hand : Cursors.Default;
            _isCurrentlyOverLink = overLink;
            _currentLinkInstanceId = instanceId;

            if (overLink)
            {
                CModule module = _loadedModulesList.FirstOrDefault(m => m.InstanceId == instanceId);
                if (module != null)
                {
                    string tooltipText = $"Click to navigate to module: {Path.GetFileName(module.FileName)}";
                    if (_moduleToolTip.GetToolTip(reLog) != tooltipText)
                        _moduleToolTip.SetToolTip(reLog, tooltipText);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_moduleToolTip.GetToolTip(reLog)))
                    _moduleToolTip.SetToolTip(reLog, string.Empty);
            }
        }
    }

    private void RichEditEnableInterfaceButtons(bool enable)
    {
        ViewPropertiesItem.Enabled = enable;
        PropertiesToolButton.Enabled = enable;
        toolStripMenuItem13.Enabled = enable;
        ContextPropertiesItem.Enabled = enable;
    }

    private void RichEditLog_Enter(object sender, EventArgs e)
    {
        RichEditEnableInterfaceButtons(false);
    }

    private void RichEditLog_Leave(object sender, EventArgs e)
    {
        RichEditEnableInterfaceButtons(true);
    }

    private void RichEditLog_SelectionChanged(object sender, EventArgs e)
    {
        CopyToolButton.Enabled = (reLog.SelectionLength > 0);
    }

    private static string FormatByteSize(UInt64 bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024 * 1024)} MB",
            >= 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes} byte"
        };
    }

    private void LogModuleStats(CCoreCallStats stats, string moduleFileName)
    {
        if (stats == null)
            return;

        var statsData = $"[STATS {Path.GetFileName(moduleFileName)}] Received: {FormatByteSize(stats.TotalBytesSent)}, " +
                        $"\"send\" calls: {stats.TotalSendCalls}, \"send\" time spent (\u00B5s): {stats.TotalTimeSpent}";
        AppLogger.LogExt(statsData, LogMessageType.ContentDefined, Color.Purple, true, false);
    }

    private void ClearModuleLinks()
    {
        _moduleLinks.Clear();
    }

    /// <summary>
    /// Searches the log text in chunks to reduce UI stalls when processing large content.
    /// </summary>
    /// <param name="searchText">Text to find.</param>
    /// <param name="startIndex">Position to start searching from.</param>
    /// <param name="options">Search options.</param>
    /// <returns>Index of the found text, or -1 if not found.</returns>
    private int FindMyText(string searchText, int startIndex, RichTextBoxFinds options)
    {
        if (string.IsNullOrEmpty(searchText) || reLog.TextLength == 0)
            return -1;

        if (!options.HasFlag(RichTextBoxFinds.Reverse) && startIndex >= reLog.TextLength)
            startIndex = 0;

        if (options.HasFlag(RichTextBoxFinds.Reverse) && startIndex <= 0)
            startIndex = reLog.TextLength;

        if (reLog.TextLength > 500000) // >500KB
        {
            return FindMyTextChunked(searchText, startIndex, options);
        }

        int result = reLog.Find(searchText, startIndex, options);
        if (result == -1)
        {
            if (!options.HasFlag(RichTextBoxFinds.Reverse))
            {
                result = reLog.Find(searchText, 0, options);
            }
            else
            {
                result = reLog.Find(searchText, reLog.TextLength, options);
            }
        }

        return result;
    }

    /// <summary>
    /// Performs search in chunks for very large logs to maintain UI responsiveness
    /// </summary>
    private int FindMyTextChunked(string searchText, int startIndex, RichTextBoxFinds options)
    {
        reLog.SuspendLayout();

        try
        {
            const int ChunkSize = 200000; // 200KB chunks
            bool isReverse = options.HasFlag(RichTextBoxFinds.Reverse);

            int chunkStart, chunkEnd;

            if (!isReverse)
            {
                chunkStart = startIndex;
                chunkEnd = Math.Min(chunkStart + ChunkSize, reLog.TextLength);
            }
            else
            {
                chunkStart = Math.Max(0, startIndex - ChunkSize);
                chunkEnd = startIndex;
            }

            int result = reLog.Find(searchText, chunkStart, chunkEnd - chunkStart, options);

            if (result != -1)
                return result;

            if (!isReverse)
            {
                for (int pos = chunkEnd; pos < reLog.TextLength; pos += ChunkSize)
                {
                    int end = Math.Min(pos + ChunkSize, reLog.TextLength);
                    result = reLog.Find(searchText, pos, end - pos, options);

                    if (result != -1)
                        return result;
                }

                for (int pos = 0; pos < startIndex; pos += ChunkSize)
                {
                    int end = Math.Min(pos + ChunkSize, startIndex);
                    result = reLog.Find(searchText, pos, end - pos, options);

                    if (result != -1)
                        return result;
                }
            }
            else
            {
                for (int pos = chunkStart; pos > 0; pos = Math.Max(0, pos - ChunkSize))
                {
                    int start = pos;
                    int length = Math.Min(ChunkSize, pos);
                    result = reLog.Find(searchText, start - length, length, options);

                    if (result != -1)
                        return result;

                    if (pos < ChunkSize) break;
                }

                for (int pos = reLog.TextLength; pos > startIndex; pos -= ChunkSize)
                {
                    int start = pos;
                    int length = Math.Min(ChunkSize, pos - startIndex);
                    result = reLog.Find(searchText, start - length, length, options);

                    if (result != -1)
                        return result;
                }
            }

            return -1;
        }
        finally
        {
            reLog.ResumeLayout();
        }
    }

    public void LogFindString()
    {
        int startPos;
        int index;

        if (string.IsNullOrEmpty(_logSearchState.FindText))
            return;

        this.Activate();
        reLog.Focus();

        startPos = _logSearchState.SearchPosition;

        if (_logSearchState.IndexOfSearchText >= 0 &&
            _logSearchState.IndexOfSearchText == reLog.SelectionStart &&
            reLog.SelectionLength == _logSearchState.FindText.Length)
        {
            if (!_logSearchState.FindOptions.HasFlag(RichTextBoxFinds.Reverse))
                startPos = _logSearchState.IndexOfSearchText + _logSearchState.FindText.Length;
            else
                startPos = _logSearchState.IndexOfSearchText;
        }

        index = FindMyText(_logSearchState.FindText, startPos, _logSearchState.FindOptions);

        if (index != -1)
        {
            reLog.Select(index, _logSearchState.FindText.Length);
            reLog.ScrollToCaret();

            _logSearchState.SearchPosition = !_logSearchState.FindOptions.HasFlag(RichTextBoxFinds.Reverse)
                ? index + _logSearchState.FindText.Length
                : index - 1;

            _logSearchState.IndexOfSearchText = index;
        }
        else
        {
            reLog.SelectionLength = 0;
            _logSearchState.IndexOfSearchText = -1;
        }
    }

    private void ShowFindDialog()
    {
        // If dialog doesn't exist or was disposed, create a new one
        if (_findDialog == null || _findDialog.IsDisposed)
        {
            _findDialog = new FindDialogForm(this, _configuration);
            _findDialog.Font = this.Font;
            _findDialog.Owner = this;
            _findDialog.Show();
        }
        else
        {
            _findDialog.Show();
            _findDialog.Activate();
        }
    }

    private void FindMenuItem_Click(object sender, EventArgs e)
    {
        ShowFindDialog();
    }

    private void FindNextMenuItemClick(object sender, EventArgs e)
    {
        LogFindString();
    }
}
