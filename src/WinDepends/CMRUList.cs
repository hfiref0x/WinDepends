/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CMRULIST.CS
*
*  VERSION:     1.00
*
*  DATE:        25 Sep 2024
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
namespace WinDepends;

/// <summary>
/// Class that manages Most Recently Used files history.
/// </summary>
public class CMRUList
{
    private List<FileInfo> FileInfos { get; set; }
    public List<string> FileList { get; set; }
    private int NumFiles { get; set; }

    public bool HistoryShowFullPath { get; set; }

    private readonly ToolStripMenuItem menuBase;
    private readonly ToolStripSeparator separator;
    private readonly ToolStripMenuItem[] menuItems;
    private readonly ToolStripStatusLabel statusLabel;

    public delegate bool SelectedFileEventCallback(string fileName);
    public event SelectedFileEventCallback SelectedFileCallback;

    public CMRUList(ToolStripMenuItem mruMenu,
                    int insertAfter,
                    List<string> initialFileList,
                    int numFiles,
                    bool historyShowFullPath,
                    SelectedFileEventCallback fileSelected,
                    ToolStripStatusLabel statusBarLabel)
    {
        menuBase = mruMenu;
        NumFiles = numFiles;
        statusLabel = statusBarLabel;
        HistoryShowFullPath = historyShowFullPath;

        SelectedFileCallback = fileSelected;
        FileList = initialFileList;
        FileInfos = [];

        separator = new ToolStripSeparator
        {
            Visible = false
        };

        int itemIndex = insertAfter;

        menuBase.DropDownItems.Insert(++itemIndex, separator);

        menuItems = new ToolStripMenuItem[CConsts.HistoryDepthMax];
        for (int i = 0; i < CConsts.HistoryDepthMax; i++)
        {
            menuItems[i] = new ToolStripMenuItem
            {
                Visible = false
            };
            menuBase.DropDownItems.Insert(++itemIndex, menuItems[i]);
        }

        if (FileList != null)
        {
            LoadFiles();
        }
        ShowFiles();
    }

    private void LoadFiles()
    {
        FileList = FileList.Where(f => !string.IsNullOrEmpty(f) && File.Exists(f)).ToList();
        FileInfos = FileList.Select(f => new FileInfo(f)).Take(NumFiles).ToList();
    }

    private void RememberFiles()
    {
        FileList = FileInfos.Select(fi => fi.FullName).ToList();
    }

    public void AddFile(string fileName)
    {
        FileInfo fileInfo = new(fileName);
        FileInfos.RemoveAll(fi => fi.FullName == fileInfo.FullName);

        FileInfos.Insert(0, fileInfo);

        if (FileInfos.Count > NumFiles)
        {
            FileInfos.RemoveAt(NumFiles);
        }

        ShowFiles();
        RememberFiles();
    }

    public void RemoveFile(string fileName)
    {
        FileInfos.RemoveAll(fi => fi.FullName == fileName);
        ShowFiles();
        RememberFiles();
    }

    public void ShowFiles()
    {
        separator.Visible = FileInfos.Count > 0;

        FileInfos = FileInfos.Where(fi => File.Exists(fi.FullName)).ToList();

        for (int i = 0; i < FileInfos.Count; i++)
        {
            UpdateMenuItem(i, FileInfos[i]);
        }

        for (int i = FileInfos.Count; i < NumFiles; i++)
        {
            HideMenuItem(i);
        }
    }

    private void UpdateMenuItem(int index, FileInfo fileInfo)
    {
        ToolStripMenuItem menuItem = menuItems[index];
        menuItem.Text = $"&{index + 1} {(HistoryShowFullPath ? fileInfo.FullName : fileInfo.Name)}";
        menuItem.Visible = true;
        menuItem.Tag = fileInfo;
        menuItem.Click -= File_Click;
        menuItem.Click += File_Click;
        menuItem.MouseEnter -= File_MouseEnter;
        menuItem.MouseEnter += File_MouseEnter;
        menuItem.MouseLeave -= File_MouseLeave;
        menuItem.MouseLeave += File_MouseLeave;
    }

    private void HideMenuItem(int index)
    {
        ToolStripMenuItem menuItem = menuItems[index];
        menuItem.Visible = false;
        menuItem.Click -= File_Click;
        menuItem.MouseEnter -= File_MouseEnter;
        menuItem.MouseLeave -= File_MouseLeave;
    }

    public void UpdateFileView(int newMaxEntries, bool historyShowFullPath)
    {
        if (newMaxEntries > CConsts.HistoryDepthMax)
        {
            newMaxEntries = CConsts.HistoryDepthMax;
        }

        NumFiles = newMaxEntries;
        HistoryShowFullPath = historyShowFullPath;

        if (FileInfos.Count > NumFiles)
        {
            FileInfos = FileInfos.Take(NumFiles).ToList();
        }

        for (int i = NumFiles; i < CConsts.HistoryDepthMax; i++)
        {
            HideMenuItem(i);
        }

        ShowFiles();
    }

    void File_Click(object sender, EventArgs e)
    {
        SelectedFileEventCallback fileHandler = SelectedFileCallback;

        if (fileHandler == null)
        {
            return;
        }

        if (sender is ToolStripMenuItem menuItem && menuItem.Tag is FileInfo fileInfo)
        {
            //
            // Check if file exists.
            //
            if (File.Exists(fileInfo.FullName))
            {
                fileHandler(fileInfo.FullName);
            }
            else
            {
                MessageBox.Show($"{fileInfo.FullName} was not found.",
                    CConsts.ShortProgramName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                RemoveFile(fileInfo.FullName);
                ShowFiles();
            }
        }
    }

    void File_MouseEnter(object sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem)
        {
            string text = Properties.Resources.ResourceManager.GetString("mruListItem");
            if (!string.IsNullOrEmpty(text))
            {
                statusLabel.Text = text;
            }
        }
    }
    void File_MouseLeave(object sender, EventArgs e)
    {
        statusLabel.Text = string.Empty;
    }
}
