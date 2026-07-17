/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       MAINFORM.FILEOPERATIONS.CS
*
*  VERSION:     1.00
*
*  DATE:        14 Jul 2026
*  
*  File and session open/save routines for main form.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

public partial class MainForm
{
    private void PostOpenFileUpdateControls(string fileName)
    {
        var suffix = CUtils.IsAdministrator
            ? Environment.Is64BitProcess ? CConsts.Admin64Msg : CConsts.AdminMsg
            : Environment.Is64BitProcess ? CConsts.SixtyFourBitsMsg : "";

        _mruList.AddFile(fileName);
        this.Text = $"{CConsts.ProgramName}{suffix} [{Path.GetFileName(fileName)}]";
    }

    private void MenuCloseItem_Click(object sender, EventArgs e)
    {
        if (_depends != null)
        {
            CloseInputFile();
        }
    }

    /// <summary>
    /// Disposes resources allocated for currently opened file and resets output controls.
    /// </summary>
    private void CloseInputFile()
    {
        if (_depends != null)
        {
            var programTitle = CUtils.IsAdministrator
           ? Environment.Is64BitProcess ? CConsts.Admin64Msg : CConsts.AdminMsg
           : Environment.Is64BitProcess ? CConsts.SixtyFourBitsMsg : "";

            this.Text = $"{CConsts.ProgramName}{programTitle}";

            _depends = null;
            _rootNode = null;
        }

        ResetFileView();
    }

    /// <summary>
    /// Opens input file from file system.
    /// </summary>
    /// <param name="fileName"></param>
    private FileOpenResult OpenInputFileInternal(string? fileName)
    {
        bool bResult = false;

        string fileExt = Path.GetExtension(fileName);

        bool bSessionFile = fileExt.Equals(CConsts.WinDependsSessionFileExt, StringComparison.OrdinalIgnoreCase);

        if (bSessionFile)
        {
            if (_configuration.ClearLogOnFileOpen)
            {
                RichEditLog_ClearLog();
            }
            bResult = OpenSessionFile(fileName);
        }
        else
        {
            //
            // Display file open settings dialog depending on global settings.
            //
            CFileOpenSettings fileOpenSettings = new(_configuration);

            if (!fileOpenSettings.UseAsDefault)
            {
                using (FileOpenForm fileOpenForm = new(_configuration.EscKeyEnabled, fileOpenSettings, fileName))
                {
                    fileOpenForm.Font = this.Font;

                    //
                    // Update global settings if use as default is checked.
                    //
                    if (fileOpenForm.ShowDialog() == DialogResult.OK)
                    {
                        if (fileOpenSettings.UseAsDefault)
                        {
                            _configuration.UseStats = fileOpenSettings.UseStats;
                            _configuration.ProcessRelocsForImage = fileOpenSettings.ProcessRelocsForImage;
                            _configuration.UseCustomImageBase = fileOpenSettings.UseCustomImageBase;
                            _configuration.CustomImageBase = fileOpenSettings.CustomImageBase;
                            _configuration.PropagateSettingsOnDependencies = fileOpenSettings.PropagateSettingsOnDependencies;
                            _configuration.ExpandForwarders = fileOpenSettings.ExpandForwarders;
                            _configuration.EnableExperimentalFeatures = fileOpenSettings.EnableExperimentalFeatures;
                            _configuration.AnalysisSettingsUseAsDefault = true;
                        }
                    }
                    else
                    {
                        //
                        // User cancelled file opening, return.
                        //
                        return FileOpenResult.Cancelled;
                    }
                }

                Activate();
            }

            CloseInputFile();

            AppLogger.LogExt($"Opening \"{fileName}\" for analysis.", LogMessageType.Information);

            if (_configuration.ClearLogOnFileOpen)
            {
                RichEditLog_ClearLog();
            }

            _parentImportsHashTable.Clear();

            // Create new root object and output it submodules.
            _depends = new(fileName)
            {
                // Remember node max depth value.
                SessionNodeMaxDepth = _configuration.ModuleNodeDepthMax
            };

            if (_depends.RootModule != null)
            {
                CPathResolver.Initialized = false;
                using (CActCtxHelper sxsHelper = new(fileName))
                {
                    CPathResolver.ActCtxHelper = sxsHelper;

                    TVModules.BeginUpdate();
                    try
                    {
                        PopulateObjectToLists(_depends.RootModule, false, fileOpenSettings);
                        _rootNode?.Expand();
                    }
                    finally { TVModules.EndUpdate(); }

                    LVModules.BeginUpdate();
                    try
                    {
                        LVModules.VirtualListSize = _loadedModulesList.Count;
                        LVModulesSort(LVModules, _configuration.SortColumnModules,
                           _lvModulesSortOrder, _loadedModulesList, DisplayCacheType.Modules);

                    }
                    finally { LVModules.EndUpdate(); }

                    bResult = true;
                }//CActCtxHelper
            }

        }

        if (bResult)
        {
            PostOpenFileUpdateControls(fileName);

            if (_configuration.AutoExpands)
            {
                ExpandAllModulesWithUpdate();
            }
        }

        SaveToolButton.Enabled = bResult;
        CopyToolButton.Enabled = bResult;

        if (TVModules.Nodes.Count > 0)
        {
            TVModules.SelectedNode = TVModules.Nodes[0];
        }

        if (bResult && bSessionFile)
            return FileOpenResult.SuccessSession;

        return bResult ? FileOpenResult.Success : FileOpenResult.Failure;
    }

    /// <summary>
    /// Opens input file from file system.
    /// </summary>
    /// <param name="fileName"></param>
    private bool OpenInputFile(string? fileName)
    {
        CFileOpenState state = new(fileName);
        return _fileOpenOrchestrationService.Execute(
             state,
             OpenInputFileInternal,
             UpdateOperationStatus,
             SetOpenInputUiEnabled,
             () => TVModules.Focus());
    }

    /// <summary>
    /// Opens WinDepends serialized session file.
    /// </summary>
    /// <param name="fileName"></param>
    private bool OpenSessionFile(string? fileName)
    {
        CloseInputFile();

        _depends = LoadSessionObjectFromFile(fileName, _configuration.CompressSessionFiles);
        if (_depends == null)
        {
            // Deserialization failed, leaving.
            return false;
        }

        AppLogger.LogExt($"Openning session file \"{fileName}\"", LogMessageType.System);

        if (_depends.RootModule != null)
        {
            // Insert tree modules from session file.
            TVModules.BeginUpdate();
            try
            {
                PopulateObjectToLists(_depends.RootModule, true, null);
                // Expand root module.
                _rootNode?.Expand();
            }
            finally { TVModules.EndUpdate(); }

            // Insert list modules from session file.
            LVModules.BeginUpdate();
            try
            {
                LVModules.VirtualListSize = _loadedModulesList.Count;
                LVModulesSort(LVModules, _configuration.SortColumnModules,
                    _lvModulesSortOrder, _loadedModulesList, DisplayCacheType.Modules);
            }
            finally { LVModules.EndUpdate(); }

            // Restore important module related warnings/errors in the log.
            foreach (var entry in _depends.ModuleAnalysisLog)
            {
                AppLogger.LogExt(entry.LoggedMessage, LogMessageType.ContentDefined,
                    entry.EntryColor, true, false);
            }

        }
        else
        {
            // Corrupted file, leaving.
            _depends = null;
            _rootNode = null;
            return false;
        }

        return true;
    }

    private CDepends? LoadSessionObjectFromFile(string fileName, bool bIsCompressed = true)
    {
        try
        {
            if (bIsCompressed)
            {
                return (CDepends)CUtils.LoadPackedObjectFromFile(fileName, typeof(CDepends), UpdateOperationStatus);
            }
            else
            {
                return (CDepends)CUtils.LoadObjectFromFilePlainText(fileName, typeof(CDepends));
            }
        }
        catch (Exception ex)
        {
            string exceptionMessage;

            if (ex.InnerException != null)
            {
                exceptionMessage = ex.InnerException.Message;
            }
            else
            {
                exceptionMessage = ex.Message;
            }

            AppLogger.LogExt($"Session file \"{fileName}\" could not be opened because \"" +
                $"{exceptionMessage}\"",
                LogMessageType.ErrorOrWarning);
        }

        UpdateOperationStatus(string.Empty);
        return null;
    }

    /// <summary>
    /// Save/SaveAs handler.
    /// </summary>
    /// <param name="saveAs">Works as Save or SaveAs depending on this flag.</param>
    /// <param name="useCompression">If true then enabled compression for saved file.</param>
    private void SaveSessionObjectToFile(bool saveAs, bool useCompression = true)
    {
        string fileName;
        bool jsonOutput = false;

        if (_depends == null)
        {
            return;
        }

        // We want new filename for our session view.
        if (saveAs)
        {
            _depends.IsSavedSessionView = false;
            _depends.SessionFileName = string.Empty;
        }

        // This is new session.
        if (!_depends.IsSavedSessionView && string.IsNullOrEmpty(_depends.SessionFileName))
        {
            if (SaveFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            fileName = SaveFileDialog1.FileName;

            if (SaveFileDialog1.FilterIndex == 2) //JSON output file.
            {
                useCompression = false;
                jsonOutput = true;
            }
            else // WDS optionally packed file.
            {
                // Save system information into file also.
                if (_depends.SystemInformation.Count == 0)
                {
                    CUtils.CollectSystemInformation(_depends.SystemInformation);
                }

                _depends.IsSavedSessionView = true;
                _depends.SessionFileName = fileName;
            }
        }
        else
        {
            // This is loaded session, just save as is.
            fileName = _depends.SessionFileName;
        }

        bool bSaved = false;

        try
        {
            if (useCompression)
            {
                bSaved = CUtils.SavePackedObjectToFile(fileName, _depends, typeof(CDepends), UpdateOperationStatus);
            }
            else
            {
                bSaved = CUtils.SaveObjectToFilePlainText(fileName, _depends, typeof(CDepends));
            }
        }
        catch (Exception ex)
        {
            if (!jsonOutput)
            {
                _depends.IsSavedSessionView = false;
                _depends.SessionFileName = string.Empty;
            }

            string exceptionMessage;

            if (ex.InnerException != null)
            {
                exceptionMessage = ex.InnerException.Message;
            }
            else
            {
                exceptionMessage = ex.Message;
            }

            AppLogger.LogExt($"Error: Session file \"{fileName}\" could not be saved because \"" +
                $"{exceptionMessage}\"", LogMessageType.ErrorOrWarning);
        }

        if (bSaved)
        {
            AppLogger.Log($"File \"{fileName}\" has been saved.", LogMessageType.System);
        }

        UpdateOperationStatus(string.Empty);
    }

    private void MainMenuSave_Click(object sender, EventArgs e)
    {
        SaveSessionObjectToFile(false, _configuration.CompressSessionFiles);
    }

    private void MainMenuSaveAs_Click(object sender, EventArgs e)
    {
        SaveSessionObjectToFile(true, _configuration.CompressSessionFiles);
    }

    private void OpenFileHandler(object sender, EventArgs e)
    {
        OpenDialog1.Filter = CConsts.HandledFileExtensionsMsg +
            string.Join(";", InternalFileHandledExtensions.ExtensionList.Select(ext => "*." + ext.Name)) + CConsts.WinDependsFilter;

        if (OpenDialog1.ShowDialog() == DialogResult.OK)
        {
            OpenInputFile(OpenDialog1.FileName);
        }
    }

    private void ViewRefreshItem_Click(object sender, EventArgs e)
    {
        if (_depends != null)
        {
            var fName = _depends.RootModule.RawFileName;
            OpenInputFile(fName);
        }
    }
    private void SetOpenInputUiEnabled(bool isEnabled)
    {
        mainMenu.Enabled = isEnabled;
        MainToolBar.Enabled = isEnabled;
        TVModules.Enabled = isEnabled;
        LVModules.Enabled = isEnabled;
    }
}
