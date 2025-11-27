/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CMRULIST.CS
*
*  VERSION:     1.00
*
*  DATE:        25 Nov 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Diagnostics;
using WinDepends.Properties;

namespace WinDepends;

/// <summary>
/// Class that manages Most Recently Used files history.
/// </summary>
public sealed class CMRUList : IDisposable
{
    private readonly HashSet<string> _filePaths;
    private readonly LinkedList<FileInfo> _files = new();
    private readonly object _syncRoot = new();

    private readonly ToolStripMenuItem _menuBase;
    private readonly ToolStripSeparator _separator;
    private readonly List<ToolStripMenuItem> _menuItems = new(CConsts.HistoryDepthMax);
    private readonly ToolStripStatusLabel _statusLabel;

    private readonly string _statusText;
    private bool _disposed;

    public int MaxEntries { get; private set; }
    public bool ShowFullPath { get; set; }

    public delegate bool SelectedFileEventCallback(string fileName);
    public event SelectedFileEventCallback SelectedFileCallback;

    public CMRUList(ToolStripMenuItem mruMenu,
                    int insertAfter,
                    IEnumerable<string> initialFiles,
                    int maxEntries,
                    bool showFullPath,
                     SelectedFileEventCallback fileSelected,
                    ToolStripStatusLabel statusLabel)
    {
        _menuBase = mruMenu ?? throw new ArgumentNullException(nameof(mruMenu));
        _statusLabel = statusLabel;
        _statusText = Resources.mruListItem;

        // Initialize collections
        _filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MaxEntries = Math.Clamp(maxEntries, 1, CConsts.HistoryDepthMax);
        ShowFullPath = showFullPath;
        SelectedFileCallback = fileSelected;

        // Create UI elements
        _separator = new ToolStripSeparator { Visible = false };
        _menuBase.DropDownItems.Insert(++insertAfter, _separator);

        // Initialize menu items dynamically
        InitializeMenuItems(insertAfter);

        // Load initial files
        LoadInitialFiles(initialFiles);
        RefreshUI();
    }

    private void InitializeMenuItems(int baseIndex)
    {
        for (int i = 0; i < CConsts.HistoryDepthMax; i++)
        {
            var menuItem = new ToolStripMenuItem { Visible = false };
            AttachMenuEvents(menuItem);
            _menuItems.Add(menuItem);
            _menuBase.DropDownItems.Insert(baseIndex + i + 1, menuItem);
        }
    }

    private void LoadInitialFiles(IEnumerable<string> initialFiles)
    {
        if (initialFiles == null)
            return;

        foreach (var file in initialFiles)
        {
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                AddFileInternal(file);
            }
        }
    }

    private void AddFileInternal(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) 
            return;

        lock (_syncRoot)
        {
            if (!File.Exists(filePath)) 
                return;

            FileInfo fi;

            try
            {
                fi = new FileInfo(filePath);
            }
            catch (Exception ex) when (ex is ArgumentException or
                                        PathTooLongException or
                                        System.Security.SecurityException or
                                        UnauthorizedAccessException or
                                        NotSupportedException)
            { 
                return; 
            }

            string fullPath = fi.FullName;

            // Remove existing entry if present
            if (_filePaths.Contains(fullPath))
            {
                var existing = FindFileNode(fullPath);
                if (existing != null)
                {
                    _files.Remove(existing);
                }
                _filePaths.Remove(fullPath);
            }

            // Add new entry to front
            _files.AddFirst(fi);
            _filePaths.Add(fullPath);

            // Enforce maximum entries
            while (_files.Count > MaxEntries && _files.Last != null)
            {
                var last = _files.Last.Value;
                _files.RemoveLast();
                _filePaths.Remove(last.FullName);
            }
        }
    }

    private LinkedListNode<FileInfo> FindFileNode(string fullPath)
    {
        var node = _files.First;
        while (node != null)
        {
            if (node.Value.FullName.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
            node = node.Next;
        }
        return null;
    }

    public void AddFile(string filePath)
    {
        AddFileInternal(filePath);
        RefreshUI();
    }

    public void RemoveFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        lock (_syncRoot)
        {
            if (!_filePaths.Contains(filePath))
                return;

            var existing = FindFileNode(filePath);
            if (existing != null)
            {
                _files.Remove(existing);
                _filePaths.Remove(existing.Value.FullName);
            }
        }

        RefreshUI();
    }

    public void UpdateSettings(int newMaxEntries, bool showFullPath)
    {
        MaxEntries = Math.Clamp(newMaxEntries, 1, CConsts.HistoryDepthMax);
        ShowFullPath = showFullPath;

        lock (_syncRoot)
        {
            while (_files.Count > MaxEntries && _files.Last != null)
            {
                var last = _files.Last.Value;
                _files.RemoveLast();
                _filePaths.Remove(last.FullName);
            }
        }

        RefreshUI();
    }

    public List<string> GetCurrentItems()
    {
        lock (_syncRoot)
        {
            return _files
                .Select(f => f.FullName)
                .ToList();
        }
    }

    private void RefreshUI()
    {
        List<FileInfo> snapshot;
        lock (_syncRoot)
        {
            snapshot = [.. _files];
        }

        _separator.Visible = snapshot.Count > 0;

        // Update visible menu items
        int index = 0;
        foreach (var file in snapshot)
        {
            if (index >= CConsts.HistoryDepthMax)
                break;

            UpdateMenuItem(_menuItems[index], file, index + 1);
            index++;
        }

        // Hide remaining items
        for (; index < CConsts.HistoryDepthMax; index++)
        {
            _menuItems[index].Visible = false;
        }
    }

    private void UpdateMenuItem(ToolStripMenuItem item, FileInfo fi, int number)
    {
        item.Text = $"&{number} {(ShowFullPath ? fi.FullName : fi.Name)}";
        item.Tag = fi;
        item.Visible = true;
    }

    private void AttachMenuEvents(ToolStripMenuItem item)
    {
        item.Click += HandleFileClick;
        item.MouseEnter += HandleMouseEnter;
        item.MouseLeave += HandleMouseLeave;
    }

    private void DetachMenuEvents(ToolStripMenuItem item)
    {
        item.Click -= HandleFileClick;
        item.MouseEnter -= HandleMouseEnter;
        item.MouseLeave -= HandleMouseLeave;
    }

    private void HandleFileClick(object sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem { Tag: FileInfo fi })
        {
            if (File.Exists(fi.FullName))
            {
                var callback = SelectedFileCallback;
                if (callback != null)
                {
                    try
                    {
                        callback(fi.FullName);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"MRU callback failed: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show($"{fi.FullName} was not found.",
                    CConsts.ShortProgramName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                RemoveFile(fi.FullName);
            }
        }
    }

    private void HandleMouseEnter(object sender, EventArgs e)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = _statusText;
        }
    }

    private void HandleMouseLeave(object sender, EventArgs e)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = string.Empty;
        }
    }

    public void Dispose()
    {
        if (_disposed) 
            return;

        _disposed = true;

        foreach (var item in _menuItems)
        {
            DetachMenuEvents(item);
            item.Dispose();
        }

        _separator.Dispose();
        _menuItems.Clear();
        lock (_syncRoot)
        {
            _files.Clear();
            _filePaths.Clear();
        }
    }
}
