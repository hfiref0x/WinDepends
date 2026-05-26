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
                Array.Clear(_lvImportsCache, 0, _lvImportsCache.Length);
                Array.Resize(ref _lvImportsCache, 0);
                _lvImportsFirstItem = 0;
                break;
            case DisplayCacheType.Exports:
                Array.Clear(_lvExportsCache, 0, _lvExportsCache.Length);
                Array.Resize(ref _lvExportsCache, 0);
                _lvExportsFirstItem = 0;
                break;
            case DisplayCacheType.Modules:
                Array.Clear(_lvModulesCache, 0, _lvModulesCache.Length);
                Array.Resize(ref _lvModulesCache, 0);
                _lvModulesFirstItem = 0;
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
        if (_lvExportsCache != null &&
            e.ItemIndex >= _lvExportsFirstItem &&
            e.ItemIndex < _lvExportsFirstItem + _lvExportsCache.Length)
        {
            e.Item = _lvExportsCache[e.ItemIndex - _lvExportsFirstItem];
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
        if (_lvImportsCache != null &&
            e.ItemIndex >= _lvImportsFirstItem &&
            e.ItemIndex < _lvImportsFirstItem + _lvImportsCache.Length)
        {
            e.Item = _lvImportsCache[e.ItemIndex - _lvImportsFirstItem];
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
        if (_lvExportsCache != null && e.StartIndex >= _lvExportsFirstItem && e.EndIndex <= _lvExportsFirstItem + _lvExportsCache.Length)
        {
            return;
        }

        _lvExportsFirstItem = e.StartIndex;
        int length = e.EndIndex - e.StartIndex + 1;
        _lvExportsCache = new ListViewItem[length];
        var selectedModule = TVModules.SelectedNode?.Tag as CModule;

        for (int i = 0, j = _lvExportsFirstItem; i < length && j < _currentExportsList.Count; i++, j++)
        {
            _lvExportsCache[i] = LVCreateFunctionEntry(_currentExportsList[j], selectedModule);
        }
    }

    /// <summary>
    /// Cache listview import entry.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVImportsCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
    {
        if (_lvImportsCache != null && e.StartIndex >= _lvImportsFirstItem && e.EndIndex <= _lvImportsFirstItem + _lvImportsCache.Length)
        {
            return;
        }

        _lvImportsFirstItem = e.StartIndex;
        int length = e.EndIndex - e.StartIndex + 1;
        _lvImportsCache = new ListViewItem[length];
        var selectedModule = TVModules.SelectedNode?.Tag as CModule;

        for (int i = 0, j = _lvImportsFirstItem; i < length && j < _currentImportsList.Count; i++, j++)
        {
            _lvImportsCache[i] = LVCreateFunctionEntry(_currentImportsList[j], selectedModule);
        }
    }
}
