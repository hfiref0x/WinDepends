/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       MAINFORM.FUNCTIONS.CS
*
*  VERSION:     1.00
*
*  DATE:        12 Jul 2026
*  
*  Import and export function view routines for main form.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Diagnostics;

namespace WinDepends;

public partial class MainForm
{
    /// <summary>
    /// Imports/Export list menu handler.
    /// </summary>
    void ProcessFunctionEntry()
    {
        int selectedItemIndex;
        CFunction function = null;
        string fName;

        try
        {
            if (LVImports.Focused && LVImports.SelectedIndices.Count > 0)
            {
                selectedItemIndex = LVImports.SelectedIndices[0];
                if (selectedItemIndex < _currentImportsList.Count)
                {
                    function = _currentImportsList[selectedItemIndex];
                }
            }
            else if (LVExports.Focused && LVExports.SelectedIndices.Count > 0)
            {
                selectedItemIndex = LVExports.SelectedIndices[0];
                if (selectedItemIndex < _currentExportsList.Count)
                {
                    function = _currentExportsList[selectedItemIndex];
                }
            }

            if (function != null)
            {
                fName = function.RawName;
                if (!CUtils.TryBuildExternalHelpUrl(_configuration.ExternalFunctionHelpURL, fName, out var url))
                {
                    AddLogMessage("External help launch failed: invalid help URL template.", LogMessageType.ErrorOrWarning);
                    return;
                }

                Process.Start(new ProcessStartInfo()
                {
                    FileName = url,
                    Verb = "open",
                    UseShellExecute = true
                });
            }

        }
        catch (Exception ex)
        {
            AddLogMessage($"External help launch failed: {ex.Message}", LogMessageType.ErrorOrWarning);
        }
    }

    /// <summary>
    /// Invoked each time user selects a module. Builds parent import and export lists.
    /// </summary>
    /// <param name="module">Currently selected module.</param>
    private void BuildFunctionListForSelectedModule(CModule module)
    {
        ResetFunctionLists();

        //
        // Parent imports.
        //       
        _currentImportsList = module.ParentImports;

        if (module.OriginalInstanceId != 0)
        {
            // Duplicate module, exports from the original instance.
            CModule origInstance = CUtils.InstanceIdToModule(module.OriginalInstanceId, _loadedModulesList);

            // Set list from original instance if it present, otherwise create new empty list. 
            _currentExportsList = origInstance?.ModuleData.Exports ?? [];
        }
        else
        {
            //
            // Original module
            //
            _currentExportsList = module.ModuleData.Exports;
        }

        //
        // Update function icons.
        //
        ResolveFunctionKindForList(_currentImportsList, module, _loadedModulesList, _configuration);
        ResolveFunctionKindForList(_currentExportsList, module, _loadedModulesList, _configuration);

        UpdateListViewInternal(LVExports, _currentExportsList, _configuration.SortColumnExports, _lvExportsSortOrder, DisplayCacheType.Exports);
        UpdateListViewInternal(LVImports, _currentImportsList, _configuration.SortColumnImports, _lvImportsSortOrder, DisplayCacheType.Imports);

        void UpdateListViewInternal(ListView listView, List<CFunction> functionList, int sortColumn, SortOrder sortOrder, DisplayCacheType displayCacheType)
        {
            listView.VirtualListSize = functionList.Count;
            if (listView.VirtualListSize > 0)
            {
                LVFunctionsSort(listView, sortColumn, sortOrder, functionList, displayCacheType);
            }
        }

        void ResolveFunctionKindForList(List<CFunction> currentList, CModule module, List<CModule> modulesList, CConfiguration config)
        {
            foreach (CFunction function in currentList)
            {
                function.ResolveFunctionKind(module, modulesList, _parentImportsHashTable, config.ModuleNodeDepthMax, config.ExpandForwarders);
            }
        }
    }

    static string TryUndecorateFunction(CSymbolResolver symbolResolver, bool viewUndecorated, CFunction function)
    {
        if (!symbolResolver.UndecorationReady)
        {
            return function.RawName;
        }

        return viewUndecorated && function.IsNameDecorated() ? 
            function.UndecorateFunctionName(symbolResolver) : 
            function.RawName;
    }

    private ListViewItem FindMatchingFunctionInList(ListView SourceList, ListView DestinationList, List<CFunction> FunctionsList)
    {
        if (SourceList.SelectedIndices.Count > 0)
        {
            int selectedIndex = SourceList.SelectedIndices[0];
            if (selectedIndex >= 0 && selectedIndex < FunctionsList.Count)
            {
                _searchFunctionName = FunctionsList[selectedIndex].RawName;
                _searchOrdinal = FunctionsList[selectedIndex].Ordinal;

                return DestinationList.FindItemWithText(null);
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a function entry for the LVImports or LVExports virtual list views.
    /// </summary>
    /// <param name="function">Function information to display.</param>
    /// <param name="module">Module associated with the function.</param>
    /// <returns>The created list view item.</returns>
    private ListViewItem LVCreateFunctionEntry(CFunction function, CModule module)
    {
        ListViewItem lvItem = new();
        if (module == null)
        {
            return lvItem;
        }

        // Ordinal
        lvItem.SubItems.Add(function.Ordinal == uint.MaxValue
            ? CConsts.NotAvailableMsg
            : $"{function.Ordinal} (0x{function.Ordinal:X4})");

        // Hint
        lvItem.SubItems.Add(function.Hint == uint.MaxValue
            ? CConsts.NotAvailableMsg
            : $"{function.Hint} (0x{function.Hint:X4})");

        // FunctionName
        string functionName = string.Empty;
        if (function.SnapByOrdinal())
        {
            var resolvedFunction = module.ResolveFunctionForOrdinal(function.Ordinal);
            if (resolvedFunction != null)
            {
                functionName = TryUndecorateFunction(_symbolResolver, _configuration.ViewUndecorated, resolvedFunction);
            }
        }
        else
        {
            functionName = TryUndecorateFunction(_symbolResolver, _configuration.ViewUndecorated, function);
        }

        //
        // Nothing found, attempt to resolve using symbols.
        //
        if (string.IsNullOrEmpty(functionName) && _configuration.UseSymbols)
        {
            var moduleBase = _symbolResolver.RetrieveCachedSymModule(module.FileName);
            if (moduleBase != IntPtr.Zero)
            {
                UInt64 functionAddress = 0;
                UInt64 moduleBaseAddress;
                UInt64 symAddress;

                //
                // If function is parent import then lookup it by ordinal in module exports
                // to query function actual address for symbols resolving.
                //
                if (!function.IsExportFunction && function.Address == 0)
                {
                    var exportFunction = module.ModuleData.Exports.Find(item => item.Ordinal == function.Ordinal);
                    if (exportFunction != null)
                    {
                        functionAddress = exportFunction.Address;
                    }
                }
                else
                {
                    functionAddress = function.Address;
                }

                if (functionAddress != 0 && !function.IsForward())
                {
                    moduleBaseAddress = unchecked((UInt64)moduleBase.ToInt64());
                    symAddress = moduleBaseAddress + functionAddress;

                    if (_symbolResolver.QuerySymbolForAddress(symAddress, out string symName))
                    {
                        function.IsNameFromSymbols = true;
                        function.RawName = symName;
                        functionName = TryUndecorateFunction(_symbolResolver,_configuration.ViewUndecorated, function);
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(functionName))
        {
            functionName = CConsts.NotAvailableMsg;
        }

        if (function.IsNameFromSymbols)
        {
            lvItem.UseItemStyleForSubItems = false;
            lvItem.SubItems.Add(functionName, Color.Black, _configuration.SymbolsHighlightColor, lvItem.Font);
        }
        else
        {
            lvItem.SubItems.Add(functionName);
        }

        // EntryPoint
        lvItem.SubItems.Add(function.IsForward()
            ? function.ForwardName
            : function.Address == 0 ? CConsts.NotBoundMsg : $"0x{function.Address:X8}");

        lvItem.ImageIndex = (int)function.Kind;
        return lvItem;
    }

    private void HighlightFunction_Click(object sender, EventArgs e)
    {
        ListView lvSrc, lvDst;
        List<CFunction> funcSrc;

        if (LVImports.Focused)
        {
            lvSrc = LVImports;
            lvDst = LVExports;
            funcSrc = _currentImportsList;
        }
        else if (LVExports.Focused)
        {
            funcSrc = _currentExportsList;
            lvSrc = LVExports;
            lvDst = LVImports;
        }
        else
        {
            return;
        }

        ListViewItem lvResult = FindMatchingFunctionInList(lvSrc, lvDst, funcSrc);
        if (lvResult != null)
        {
            lvDst.BeginUpdate();
            try
            {
                lvDst.SelectedIndices.Clear();
                lvResult.Selected = true;
                lvResult.EnsureVisible();
                lvDst.Focus();
            }
            finally { lvDst.EndUpdate(); }
        }
    }

    private void LVFunctions_KeyPress(object sender, KeyPressEventArgs e)
    {
        ListView lvDst;
        List<CFunction> currentList;
        CFunction matchingItem;
        ListViewItem lvResult;

        lvDst = LVImports.Focused ? LVImports : LVExports;

        if (!lvDst.Focused || lvDst.VirtualListSize <= 0)
        {
            e.Handled = true;
            return;
        }

        if (e.KeyChar == (char)Keys.Back)
        {
            if (_functionLookupText.Length > 0)
            {
                _functionLookupText = _functionLookupText[..^1]; //use fancy new range syntax
            }

            if (_functionLookupText.Length == 0)
            {
                _functionLookupTimer.Stop();
                _functionLookupText = string.Empty;
                HideTypeSearchHint();
                _searchOrdinal = CConsts.OrdinalNotPresent;
                _searchFunctionName = string.Empty;
                return;
            }

            ShowTypeSearchHint(lvDst, "Search: " + _functionLookupText);
        }
        else
        {
            if (char.IsControl(e.KeyChar))
            {
                e.Handled = true;
                return;
            }

            _functionLookupText += char.ToLowerInvariant(e.KeyChar);
            ShowTypeSearchHint(lvDst, "Search: " + _functionLookupText);
        }

        currentList = lvDst == LVImports ? _currentImportsList : _currentExportsList;

        _searchOrdinal = CConsts.OrdinalNotPresent;
        _searchFunctionName = string.Empty;

        matchingItem = currentList.FirstOrDefault(item =>
            item.RawName.StartsWith(_functionLookupText, StringComparison.OrdinalIgnoreCase));

        if (matchingItem != null)
        {
            _searchFunctionName = matchingItem.RawName;
            _searchOrdinal = matchingItem.Ordinal;

            lvDst.BeginUpdate();
            try
            {
                lvDst.SelectedIndices.Clear();
                lvResult = lvDst.FindItemWithText(null);

                if (lvResult != null)
                {
                    lvResult.Selected = true;
                    lvResult.EnsureVisible();
                    lvDst.Focus();
                }
            }
            finally { lvDst.EndUpdate(); }
        }

        _functionLookupTimer.Stop();
        _functionLookupTimer.Start();

        e.Handled = true;
    }

    private void LVFunctions_Leave(object sender, EventArgs e)
    {
        _functionLookupTimer.Stop();
        HideTypeSearchHint();
        _functionLookupText = string.Empty;
    }

    private void LVFunctions_Click(object sender, EventArgs e)
    {
        if (LVExports.Focused)
        {
            CopyToolButton.Enabled = LVExports.SelectedIndices.Count > 0;
        }
        else
        {
            CopyToolButton.Enabled = LVImports.SelectedIndices.Count > 0;
        }
    }

    /// <summary>
    /// LVImport/LVExport list view MouseDown handler.
    /// Depending on currently active list modify popup menu and show it to the user.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVFunctions_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ListView listView = sender as ListView;
            ListViewItem item = listView.GetItemAt(e.X, e.Y);
            if (item != null)
            {
                string viewType;
                if (listView == LVImports)
                {
                    viewType = "Export";
                    LVImports.SelectedIndices.Clear();
                    LVImports.SelectedIndices.Add(item.Index);
                }
                else
                {
                    LVExports.SelectedIndices.Clear();
                    LVExports.SelectedIndices.Add(item.Index);
                    viewType = "Import";
                }

                MatchingFunctionPopupMenuItem.Text = "Highlight Matching " + viewType + " Function";
                functionPopupMenu.Show(Cursor.Position);
            }
        }
    }

    private void FunctionPopupMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        undecorateFunctionToolStripMenuItem.Checked = _configuration.ViewUndecorated;
        resolveAPIsetsToolStripMenuItem.Checked = _configuration.ResolveAPIsets;
        SetPopupMenuItemText(true);
    }

    private void UndecorateFunctionToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _configuration.ViewUndecorated = !_configuration.ViewUndecorated;
        ViewUndecoratedToolButton.Checked = _configuration.ViewUndecorated;
        UpdateFileView(FileViewUpdateAction.FunctionsUndecorateChange);
    }

    private void ExternalHelpMenuItem_Click(object sender, EventArgs e)
    {
        ProcessFunctionEntry();
    }

    /// <summary>
    /// LVImports/LVExports virtual listview search handler.
    /// </summary>
    /// <param name="itemList"></param>
    /// <param name="e"></param>
    private void LVFunctionsSearchForVirtualItem(List<CFunction> itemList, SearchForVirtualItemEventArgs e)
    {
        // Search by ordinal.
        if (string.IsNullOrEmpty(_searchFunctionName))
        {
            if (_searchOrdinal != CConsts.OrdinalNotPresent)
            {
                foreach (var entry in itemList)
                {
                    if (entry.Ordinal == _searchOrdinal)
                    {
                        e.Index = itemList.IndexOf(entry);
                        return;
                    }
                }
            }
        }
        else
        {
            // Search by name.
            foreach (var entry in itemList)
            {
                if (entry.RawName.Equals(_searchFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    e.Index = itemList.IndexOf(entry);
                    return;
                }
            }

            // If item is not found, search by ordinal if possible.
            if (_searchOrdinal != CConsts.OrdinalNotPresent)
            {
                foreach (var entry in itemList)
                {
                    if (entry.Ordinal == _searchOrdinal)
                    {
                        e.Index = itemList.IndexOf(entry);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// LVImports virtual listview search handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVImportsSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
    {
        LVFunctionsSearchForVirtualItem(_currentImportsList, e);
    }

    /// <summary>
    /// LVExports virtual listview search handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVExportsSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
    {
        LVFunctionsSearchForVirtualItem(_currentExportsList, e);
    }

    /// <summary>
    /// LVImports/LVExports virtual listview sort handler.
    /// </summary>
    /// <param name="listView"></param>
    /// <param name="columnIndex"></param>
    /// <param name="sortOrder"></param>
    /// <param name="data"></param>
    /// <param name="cacheType"></param>
    private void LVFunctionsSort(ListView listView, int columnIndex, SortOrder sortOrder, List<CFunction> data, DisplayCacheType cacheType)
    {
        IComparer<CFunction> funcComparer = new CFunctionComparer(sortOrder, columnIndex, _symbolResolver);
        data.Sort(funcComparer);
        //
        // Reset listview items cache.
        //
        UpdateItemsView(listView, cacheType);
        UpdateListViewColumnSortMark(listView, columnIndex, sortOrder);
    }

    private void LVExportsColumnClick(object sender, ColumnClickEventArgs e)
    {
        int columnIndex = e.Column;
        _configuration.SortColumnExports = columnIndex;
        _lvExportsSortOrder = (_lvExportsSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVFunctionsSort(LVExports, columnIndex, _lvExportsSortOrder, _currentExportsList, DisplayCacheType.Exports);
    }

    private void LVImportsColumnClick(object sender, ColumnClickEventArgs e)
    {
        int columnIndex = e.Column;
        _configuration.SortColumnImports = columnIndex;
        _lvImportsSortOrder = (_lvImportsSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVFunctionsSort(LVImports, columnIndex, _lvImportsSortOrder, _currentImportsList, DisplayCacheType.Imports);
    }
}
