/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       MAINFORM.VIRTUALLISTS.CS
*
*  VERSION:     1.00
*
*  DATE:        24 May 2026
*  
*  Virtual list caching and retrieval routines for main form.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Text;

namespace WinDepends;

public partial class MainForm
{
    private void ResetDisplayCache(DisplayCacheType cacheType)
    {
        switch (cacheType)
        {
            case DisplayCacheType.Imports:
                Array.Clear(LVImportsCache, 0, LVImportsCache.Length);
                Array.Resize(ref LVImportsCache, 0);
                LVImportsFirstItem = 0;
                break;
            case DisplayCacheType.Exports:
                Array.Clear(LVExportsCache, 0, LVExportsCache.Length);
                Array.Resize(ref LVExportsCache, 0);
                LVExportsFirstItem = 0;
                break;
            case DisplayCacheType.Modules:
                Array.Clear(LVModulesCache, 0, LVModulesCache.Length);
                Array.Resize(ref LVModulesCache, 0);
                LVModulesFirstItem = 0;
                break;
        }
    }

    private void ResetFunctionLists()
    {
        ResetDisplayCache(DisplayCacheType.Imports);
        ResetDisplayCache(DisplayCacheType.Exports);
        LVImports.VirtualListSize = LVExports.VirtualListSize = 0;
        LVImports.Invalidate();
        LVExports.Invalidate();
    }

    public void UpdateItemsView(ListView listView, DisplayCacheType cacheType)
    {
        listView.BeginUpdate();
        try
        {
            ResetDisplayCache(cacheType);
            listView.Invalidate();
        }
        finally { listView.EndUpdate(); }
    }

    private static void UpdateListViewColumnSortMark(ListView listView, int columnIndex, SortOrder sortOrder)
    {
        static string GetProcessedText(string columnText)
        {
            return new StringBuilder(columnText)
                .Replace(CConsts.AscendSortMark, string.Empty)
                .Replace(CConsts.DescendSortMark, string.Empty)
                .ToString().Trim();
        }

        string commonText = GetProcessedText(listView.Columns[columnIndex].Text);

        foreach (ColumnHeader column in listView.Columns)
        {
            if (column.Index == columnIndex)
            {
                string newMark = (sortOrder == SortOrder.Ascending) ? CConsts.AscendSortMark : CConsts.DescendSortMark;
                column.Text = $"{newMark} {commonText}";
            }
            else
            {
                column.Text = GetProcessedText(column.Text);
            }
        }
    }

    /// <summary>
    /// Retrieve list item from cache or build it from export list.
    /// Automatically called when ListView wants to populate items.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVExportsRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
        if (LVExportsCache != null &&
            e.ItemIndex >= LVExportsFirstItem &&
            e.ItemIndex < LVExportsFirstItem + LVExportsCache.Length)
        {
            e.Item = LVExportsCache[e.ItemIndex - LVExportsFirstItem];
        }
        else
        {
            var selectedModule = TVModules.SelectedNode?.Tag as CModule;
            e.Item = LVCreateFunctionEntry(_currentExportsList[e.ItemIndex], selectedModule);
        }
    }

    /// <summary>
    /// Retrieve list item from cache or build it from import list.
    /// Automatically called when ListView wants to populate items.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVImportsRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
        if (LVImportsCache != null &&
            e.ItemIndex >= LVImportsFirstItem &&
            e.ItemIndex < LVImportsFirstItem + LVImportsCache.Length)
        {
            e.Item = LVImportsCache[e.ItemIndex - LVImportsFirstItem];
        }
        else
        {
            var selectedModule = TVModules.SelectedNode?.Tag as CModule;
            e.Item = LVCreateFunctionEntry(_currentImportsList[e.ItemIndex], selectedModule);
        }
    }

    /// <summary>
    /// Cache listview export entry.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVExportsCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
    {
        if (LVExportsCache != null && e.StartIndex >= LVExportsFirstItem && e.EndIndex <= LVExportsFirstItem + LVExportsCache.Length)
        {
            return;
        }

        LVExportsFirstItem = e.StartIndex;
        int length = e.EndIndex - e.StartIndex + 1;
        LVExportsCache = new ListViewItem[length];
        var selectedModule = TVModules.SelectedNode?.Tag as CModule;

        for (int i = 0, j = LVExportsFirstItem; i < length && j < _currentExportsList.Count; i++, j++)
        {
            LVExportsCache[i] = LVCreateFunctionEntry(_currentExportsList[j], selectedModule);
        }
    }

    /// <summary>
    /// Cache listview import entry.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVImportsCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
    {
        if (LVImportsCache != null && e.StartIndex >= LVImportsFirstItem && e.EndIndex <= LVImportsFirstItem + LVImportsCache.Length)
        {
            return;
        }

        LVImportsFirstItem = e.StartIndex;
        int length = e.EndIndex - e.StartIndex + 1;
        LVImportsCache = new ListViewItem[length];
        var selectedModule = TVModules.SelectedNode?.Tag as CModule;

        for (int i = 0, j = LVImportsFirstItem; i < length && j < _currentImportsList.Count; i++, j++)
        {
            LVImportsCache[i] = LVCreateFunctionEntry(_currentImportsList[j], selectedModule);
        }
    }
}
