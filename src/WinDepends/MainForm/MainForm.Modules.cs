/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       MAINFORM.MODULES.CS
*
*  VERSION:     1.00
*
*  DATE:        26 May 2026
*  
*  Module tree, list, and navigation routines for main form.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Reflection.PortableExecutable;
using System.Text;

namespace WinDepends;

public partial class MainForm
{
    private void HandleModuleOpenStatus(CModule module, ModuleOpenStatus openStatus, CFileOpenSettings settings, bool currentModuleIsRoot)
    {
        switch (openStatus)
        {
            case ModuleOpenStatus.Okay:

                module.IsProcessed = _coreClient.GetModuleHeadersInformation(module);

                //
                // If this is root module, setup resolver.
                //
                if (currentModuleIsRoot)
                {
                    CPathResolver.QueryFileInformation(module);
                }

                _coreClient.GetModuleImportExportInformation(module,
                    _configuration.SearchOrderListUM,
                    _configuration.SearchOrderListKM,
                    _parentImportsHashTable,
                    settings.EnableExperimentalFeatures,
                    settings.ExpandForwarders);

                //
                // Collect forwarders if exists.
                // Has local settings priority over global.
                //
                if (settings.ExpandForwarders)
                {
                    _coreClient.ExpandAllForwarderModules(module, _configuration.SearchOrderListUM,
                        _configuration.SearchOrderListKM,
                        _parentImportsHashTable);

                    // Validate forwarded exports after expansion
                    _coreClient.ValidateForwardedExports(module);
                }

                CCoreCallStats stats = null;
                if (settings.UseStats)
                {
                    stats = _coreClient.GetCoreCallStats();
                }

                _coreClient.CloseModule();

                //
                // Display statistics.
                //
                if (settings.UseStats && stats != null)
                {
                    LogModuleStats(stats, module.FileName);
                }

                if (module.ExportContainErrors)
                {
                    AddLogMessage($"Module \"{module.FileName}\" contains export errors.",
                        LogMessageType.ErrorOrWarning, null, true, true, module);
                }

                // Add warning for modules with forwarding issues
                if (module.OtherErrorsPresent && module.ForwarderEntries?.Count > 0)
                {
                    AddLogMessage($"Module \"{Path.GetFileName(module.FileName)}\" has unresolved forwarded exports.",
                        LogMessageType.ErrorOrWarning, null, true, true, module);
                }

                bool isCpuMismatch = IsCpuMismatchForDisplay(module, _depends.RootModule);

                if (isCpuMismatch)
                {
                    module.OtherErrorsPresent = true;
                    AddLogMessage($"Module \"{module.FileName}\" with different CPU type was found.",
                        LogMessageType.ErrorOrWarning, null, true, true, module);
                }

                // Skip this message for kernel modules, dotnet files, and when relocation processing is disabled
                if (module.ModuleData.ImageFixed != 0 &&
                    !module.IsKernelModule &&
                    module.ModuleData.ImageDotNet != 1 &&
                    settings.ProcessRelocsForImage)  // Only warn if relocation processing was requested (as per issue #39)
                {
                    module.OtherErrorsPresent = true;
                    AddLogMessage($"Module \"{Path.GetFileName(module.FileName)}\" has no relocations.",
                        LogMessageType.ErrorOrWarning, null, true, true, module);
                }

                if (!module.IsProcessed)
                {
                    AddLogMessage($"Module \"{module.FileName}\" was not fully processed.",
                        LogMessageType.ErrorOrWarning,
                        null, true, true, module);
                }
                break;

            case ModuleOpenStatus.ErrorUnspecified:
                AddLogMessage($"Module \"{module.FileName}\" analysis failed.", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorSendCommand:
                AddLogMessage($"Send command has failed for module \"{module.FileName}\".", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorReceivedDataInvalid:
                AddLogMessage($"Received invalid data for module \"{module.FileName}\".", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorFileNotMapped:
                AddLogMessage($"Server failed to map input module \"{module.FileName}\".", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorCannotReadFileHeaders:
                AddLogMessage($"Server failed to read headers of module \"{module.FileName}\".", LogMessageType.ErrorOrWarning,
                    null, true, true, module);
                break;
            case ModuleOpenStatus.ErrorInvalidHeadersOrSignatures:
                if (module.IsDelayLoad)
                {
                    AddLogMessage($"Delay-load module \"{module.FileName}\" has invalid headers or signatures.", LogMessageType.ErrorOrWarning,
                        null, true, true, module);
                }
                else
                {
                    AddLogMessage($"Module \"{module.FileName}\" has invalid headers or signatures.", LogMessageType.ErrorOrWarning,
                        null, true, true, module);
                }
                break;

            case ModuleOpenStatus.ErrorFileNotFound:

                // In case if this is ApiSets failure.
                // API-* are mandatory to load, while EXT-* are not.
                bool bExtApiSet = module.IsApiSetContract && module.RawFileName.StartsWith("EXT-", StringComparison.OrdinalIgnoreCase);

                string messageText;
                LogMessageType messageType = bExtApiSet ? LogMessageType.Information : LogMessageType.ErrorOrWarning;

                if (module.IsDelayLoad)
                {
                    if (bExtApiSet)
                    {
                        messageText = $"Delay-load extension apiset module \"{module.FileName}\" was not found.";
                    }
                    else
                    {
                        messageText = $"Delay-load dependency module \"{module.FileName}\" was not found.";
                    }
                }
                else
                {
                    if (bExtApiSet)
                    {
                        messageText = $"Extension apiset module \"{module.FileName}\" was not found.";
                    }
                    else
                    {
                        messageText = $"Required implicit or forwarded dependency \"{module.FileName}\" was not found.";
                    }
                }

                AddLogMessage(messageText, messageType, null, true, true, module);
                break;
        }
    }

    /// <summary>
    /// Validates whether a module can be added based on tree depth settings.
    /// </summary>
    /// <param name="parentNode">The parent node to check depth against.</param>
    /// <param name="maxDepth">The maximum allowed depth.</param>
    /// <returns>true if the node is within the depth limit; otherwise, false.</returns>
    private bool ValidateTreeDepth(TreeNode parentNode, int maxDepth)
    {
        if (parentNode == null)
            return true; // Root node is always valid

        if (parentNode.Tag is CModule parentModule)
        {
            return parentModule.Depth <= maxDepth;
        }

        return true;
    }

    /// <summary>
    /// Builds a display name for a module from the given raw name.
    /// </summary>
    /// <param name="rawName">The original path or module name.</param>
    /// <param name="fullPaths">If true, returns the original value; otherwise extracts the last path segment when appropriate.</param>
    /// <param name="upperCase">If true, converts the resulting display name to uppercase.</param>
    /// <returns>The formatted module display name.</returns>
    private static string BuildModuleDisplayName(string rawName, bool fullPaths, bool upperCase)
    {
        string result;
        string normalized;

        if (string.IsNullOrEmpty(rawName))
            return rawName;

        if (fullPaths)
        {
            result = rawName;
        }
        else
        {
            normalized = rawName.Replace('/', '\\');
            if (string.IsNullOrEmpty(normalized))
                return rawName;

            // Preserve roots before any trimming/normalization that could drop the last separator.
            // Drive root: C:\
            // UNC share root: \\server\share\
            if (IsDriveRootPath(normalized) || IsUncShareRootPath(normalized))
            {
                result = normalized;
            }
            else
            {
                normalized = normalized.TrimEnd('\\');
                if (string.IsNullOrEmpty(normalized))
                    return rawName;

                // Handle extended UNC: \\?\UNC\server\share\...
                if (normalized.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(8);
                }
                // Handle \\?\ prefix
                else if (normalized.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(4);
                }

                int lastSep = normalized.LastIndexOf('\\');
                result = lastSep >= 0 ? normalized.Substring(lastSep + 1) : normalized;
            }
        }

        if (upperCase && !string.IsNullOrEmpty(result))
        {
            result = result.ToUpperInvariant();
        }

        return result;
    }

    private static bool IsDriveRootPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // C:\
        if (path.Length == 3 &&
            char.IsLetter(path[0]) &&
            path[1] == ':' &&
            path[2] == '\\')
        {
            return true;
        }

        // \\?\C:\
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) &&
            path.Length == 7 &&
            char.IsLetter(path[4]) &&
            path[5] == ':' &&
            path[6] == '\\')
        {
            return true;
        }

        return false;
    }

    private static bool IsUncShareRootPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalized = path;

        // \\?\UNC\server\share\ => \\server\share\
        if (normalized.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            normalized = @"\\" + normalized.Substring(8);

        // \\?\UNC\ already handled above; handle generic \\?\ prefix
        if (normalized.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(4);

        if (!normalized.StartsWith(@"\\", StringComparison.Ordinal))
            return false;

        // Strip trailing slashes but keep path with two segments: \\server\share
        normalized = normalized.TrimEnd('\\');

        int firstSep = normalized.IndexOf('\\', 2);
        if (firstSep < 0)
            return false;

        int secondSep = normalized.IndexOf('\\', firstSep + 1);
        return secondSep < 0;
    }

    /// <summary>
    /// AddModuleEntry core implementation. Shared between normal and session files.
    /// </summary>
    /// <param name="module">The module to add</param>
    /// <param name="parentNode">Parent node, or null for root level</param>
    /// <param name="maxDepth">Maximum allowed node depth</param>
    /// <param name="moduleProcessor">Optional processing to perform on new modules</param>
    /// <returns>Created TreeNode or null if validation fails</returns>
    private TreeNode AddModuleEntryCore(
        CModule module,
        TreeNode parentNode,
        int maxDepth,
        Action<CModule> moduleProcessor = null)
    {
        // 1. Validate tree depth
        if (!ValidateTreeDepth(parentNode, maxDepth))
            return null;

        // 2. Check if module already exists
        bool isNewModule = true;
        CModule origInstance = CUtils.GetModuleByHash(module.FileName, _loadedModulesList);

        if (origInstance != null)
        {
            isNewModule = false;
            module.OriginalInstanceId = origInstance.InstanceId;
            module.FileNotFound = origInstance.FileNotFound;
            module.ExportContainErrors = origInstance.ExportContainErrors;
            module.IsInvalid = origInstance.IsInvalid;
            // Do not copy OtherErrorsPresent from original instance, must set it directly
            // module.OtherErrorsPresent = origInstance.OtherErrorsPresent;
            module.IsDotNetModule = origInstance.IsDotNetModule;
            module.ModuleData = new(origInstance.ModuleData);

            // Propagate errors from duplicate to parent if this is not root
            if (parentNode?.Tag is CModule parent)
            {
                // Only propagate genuine errors, not from apiset contracts or stopped nodes
                bool shouldPropagate = origInstance.ExportContainErrors ||
                                       origInstance.OtherErrorsPresent ||
                                       origInstance.FileNotFound;

                // Don't propagate from apiset contracts
                if (origInstance.IsApiSetContract)
                    shouldPropagate = false;

                // Don't propagate from stopped/duplicate nodes that have forwarders
                // (these are expected to have "unprocessed" forwarders)
                if (shouldPropagate)
                {
                    if (origInstance.IsStoppedNode)
                        shouldPropagate = false;
                }

                if (shouldPropagate)
                {
                    parent.OtherErrorsPresent = true;
                    parent.ModuleImageIndex = parent.GetIconIndexForModule();
                    parentNode.ImageIndex = parent.ModuleImageIndex;
                    parentNode.SelectedImageIndex = parent.ModuleImageIndex;
                }
            }
        }

        // 3. Run custom processing if this is a new module
        if (isNewModule && moduleProcessor != null)
        {
            moduleProcessor(module);
        }

        // 4. Format display name
        string moduleDisplayName = BuildModuleDisplayName(
                   module.GetModuleNameRespectApiSet(_configuration.ResolveAPIsets),
                   _configuration.FullPaths,
                   _configuration.UpperCaseModuleNames);

        // Mark forward user as red if there is no forward module.
        if (module.IsForward && module.FileNotFound && parentNode?.Tag is CModule parentMod)
        {
            parentMod.OtherErrorsPresent = true;
            parentMod.ModuleImageIndex = parentMod.GetIconIndexForModule();
            parentNode.ImageIndex = parentMod.ModuleImageIndex;
            parentNode.SelectedImageIndex = parentMod.ModuleImageIndex;
        }

        // 5. Create the tree node
        module.ModuleImageIndex = module.GetIconIndexForModule();
        TreeNode tvNode = new(moduleDisplayName)
        {
            Tag = module,
            ImageIndex = module.ModuleImageIndex,
            SelectedImageIndex = module.ModuleImageIndex,
            ForeColor = (module.IsApiSetContract && _configuration.HighlightApiSet) ? Color.Blue : Color.Black
        };

        // 6. Add to tree in correct location
        if (parentNode != null)
        {
            if (parentNode.Tag is CModule parentModule)
            {
                module.Depth = parentModule.Depth + 1;
            }
            parentNode?.Nodes?.Add(tvNode);
        }
        else
        {
            TVModules.Nodes?.Add(tvNode);
        }

        // 7. Update module collections if new
        if (isNewModule)
        {
            _loadedModulesList.Add(module);
        }

        return tvNode;
    }

    /// <summary>
    /// Insert module entry to TVModules treeview.
    /// </summary>
    /// <returns>Tree node.</returns>
    private TreeNode AddModuleEntry(CModule module, CFileOpenSettings fileOpenSettings, TreeNode parentNode = null)
    {
        bool isRootModule = (parentNode == null);

        // Define action processor (callback)
        Action<CModule> processModule = (mod) =>
        {
            var effectiveSettings = new CFileOpenSettings(fileOpenSettings);

            // If this is a dependency and propagation is disabled, reset to defaults
            if (!isRootModule && !fileOpenSettings.PropagateSettingsOnDependencies)
            {
                effectiveSettings.ProcessRelocsForImage = false;
                effectiveSettings.UseStats = false;
                effectiveSettings.UseCustomImageBase = false;
                effectiveSettings.CustomImageBase = 0;
            }

            // Open and process module
            mod.InstanceId = mod.GetHashCode();
            ModuleOpenStatus openStatus = _coreClient.OpenModule(
                ref mod,
                effectiveSettings);

            HandleModuleOpenStatus(mod, openStatus, effectiveSettings, isRootModule);

            // Set module icon index
            mod.ModuleImageIndex = mod.GetIconIndexForModule();
        };

        // Use shared implementation with our specific processor
        return AddModuleEntryCore(
            module,
            parentNode,
            _configuration.ModuleNodeDepthMax,
            processModule);
    }

    /// <summary>
    /// Adds module entry from loaded session object (saved session view).
    /// </summary>
    /// <param name="module">Module entry to be added.</param>
    /// <param name="parentNode">Parent node if present.</param>
    /// <returns>Tree node entry.</returns>
    private TreeNode AddSessionModuleEntry(CModule module, TreeNode parentNode = null)
    {
        // Just use the shared implementation without any specific processor set
        return AddModuleEntryCore(
            module,
            parentNode,
            _depends.SessionNodeMaxDepth);
    }

    /// <summary>
    /// Populates the tree and related lists for the root module and its dependencies.
    /// </summary>
    /// <param name="module">The root module to populate.</param>
    /// <param name="loadFromObject">If true, populates from a restored session object.</param>
    /// <param name="fileOpenSettings">Specific file open settings from the program configuration.</param>
    private void PopulateObjectToLists(CModule module, bool loadFromObject, CFileOpenSettings fileOpenSettings)
    {
        List<TreeNode> baseNodes = [];

        UpdateOperationStatus($"Populating {module.FileName}");

        if (loadFromObject)
        {
            // Add root session module.
            _rootNode = AddSessionModuleEntry(module, null);

            // Add root session module dependencies.
            foreach (var importModule in module.Dependents)
            {
                UpdateOperationStatus($"Populating {importModule.FileName}");
                var addedNode = AddSessionModuleEntry(importModule, _rootNode);
                if (addedNode != null)
                    baseNodes.Add(addedNode);
            }
        }
        else
        {
            // Add root module.
            _rootNode = AddModuleEntry(module, fileOpenSettings, null);

            // Add root module dependencies.
            foreach (var importModule in module.Dependents)
            {
                UpdateOperationStatus($"Populating {importModule.FileName}");
                var addedNode = AddModuleEntry(importModule, fileOpenSettings, _rootNode);
                if (addedNode != null)
                    baseNodes.Add(addedNode);
            }
        }

        // Add sub dependencies.
        foreach (var node in baseNodes)
        {
            if (node.Tag is not CModule nodeModule)
                continue;

            foreach (var dependent in nodeModule.Dependents)
            {
                UpdateOperationStatus($"Populating {dependent.FileName}");
                PopulateDependentObjectsToLists(dependent, node, loadFromObject, fileOpenSettings);
            }
        }

    }

    /// <summary>
    /// Populates dependent modules under the specified parent node, respecting the configured depth limit.
    /// </summary>
    /// <param name="module">The dependent module to populate.</param>
    /// <param name="parentNode">The parent tree node.</param>
    /// <param name="loadFromObject">If true, populates from a restored session object.</param>
    /// <param name="fileOpenSettings">Specific file open settings from the program configuration.</param>
    private void PopulateDependentObjectsToLists(CModule module, TreeNode parentNode, bool loadFromObject, CFileOpenSettings fileOpenSettings)
    {
        TreeNode tvNode;

        if (loadFromObject)
        {
            tvNode = AddSessionModuleEntry(module, parentNode);
        }
        else
        {
            tvNode = AddModuleEntry(module, fileOpenSettings, parentNode);
        }

        if (tvNode == null)
            return;

        foreach (CModule dependentModule in module.Dependents)
        {
            UpdateOperationStatus($"Populating {dependentModule.FileName}");
            PopulateDependentObjectsToLists(dependentModule, tvNode, loadFromObject, fileOpenSettings);
        }
    }

    /// <summary>
    /// Updates tree nodes respecting module display settings using non-recursive iteration.
    /// </summary>
    /// <param name="startNode"></param>
    private void TreeViewUpdateNode(TreeNode startNode)
    {
        if (startNode == null)
            return;

        Stack<TreeNode> nodeStack = new(Math.Min(TVModules.GetNodeCount(true), CConsts.MaxTreeNodeStackSize));
        nodeStack.Push(startNode);

        bool fullPaths = _configuration.FullPaths;
        bool upperCase = _configuration.UpperCaseModuleNames;
        bool highlightApiSet = _configuration.HighlightApiSet;
        bool resolveApiSets = _configuration.ResolveAPIsets;

        while (nodeStack.Count > 0)
        {
            TreeNode node = nodeStack.Pop();
            if (node == null) continue;

            if (node.Tag is CModule module)
            {
                string displayName = module.GetModuleNameRespectApiSet(resolveApiSets);
                displayName = BuildModuleDisplayName(displayName, fullPaths, upperCase);

                node.Text = displayName;
                node.ForeColor = (module.IsApiSetContract && highlightApiSet) ? Color.Blue : Color.Black;
            }

            if (node.Nodes.Count > 0)
                nodeStack.Push(node.Nodes[0]);

            if (node.NextNode != null)
                nodeStack.Push(node.NextNode);
        }
    }

    void ExpandAllModulesWithUpdate()
    {
        TVModules.BeginUpdate();
        try
        {
            TVModules.ExpandAll();
        }
        finally { TVModules.EndUpdate(); }
    }

    private void CollapseAllMenuItem_Click(object sender, EventArgs e)
    {
        TVModules.BeginUpdate();
        try
        {
            TVModules.CollapseAll();
        }
        finally { TVModules.EndUpdate(); }
    }

    private void ExpandAllMenuItem_Click(object sender, EventArgs e)
    {
        TVModules.BeginUpdate();
        try
        {
            TVModules.ExpandAll();
            TVModules.SelectedNode?.EnsureVisible();
        }
        finally { TVModules.EndUpdate(); }
    }

    private void TVModules_AfterSelect(object sender, TreeViewEventArgs e)
    {
        CModule module = (CModule)e.Node.Tag;
        if (module != null)
        {
            if (_configuration.UseSymbols) PreloadSymbolForSelectedModule(module);
            BuildFunctionListForSelectedModule(module);
        }
    }

    public void ResetModulesList()
    {
        ResetDisplayCache(DisplayCacheType.Modules);
        LVModules.VirtualListSize = 0;
        _loadedModulesList.Clear();
        LVModules.Invalidate();
    }

    /// <summary>
    /// Creates a module entry for the LVModules virtual list view.
    /// </summary>
    /// <param name="module">The module to be displayed.</param>
    /// <returns>The created list view item.</returns>
    private ListViewItem LVCreateModuleEntry(CModule module)
    {
        //
        // ModuleImage | Module | File TimeStamp | Link TimeStamp | FileSize | Attr. | LinkChecksum | Real Checksum | CPU
        // Subsystem | Preffered Base | VirtualSize | FileVer | ProductVer | ImageVer | LinkerVer | OSVer | SubsystemVer | 
        //

        //
        // Add item to ListView.
        //
        ListViewItem lvItem = new()
        {
            Tag = module,
            ImageIndex = module.GetIconIndexForModuleCompact()
        };

        string moduleDisplayName = BuildModuleDisplayName(
                    module.GetModuleNameRespectApiSet(_configuration.ResolveAPIsets),
                    _configuration.FullPaths,
                    _configuration.UpperCaseModuleNames);

        // Module
        lvItem.SubItems.Add(moduleDisplayName);

        if (!module.IsProcessed)
        {
            // Empty row
            // i = 2, 0 - ModuleImage, 1 - ModuleName.
            for (int i = 2; i < LVModules.Columns.Count; i++)
            {
                lvItem.SubItems.Add("");
            }
        }
        else
        {
            var moduleData = module.ModuleData;

            // File time stamp
            lvItem.SubItems.Add(moduleData.FileTimeStamp.ToString(CConsts.DateTimeFormat24Hours));

            // Linker stamp
            lvItem.SubItems.Add(module.IsReproducibleBuild
                ? $"Repro hash: 0x{moduleData.LinkTimeStamp:X8}"
                : CUtils.TimeSince1970ToString(moduleData.LinkTimeStamp));

            // File size
            lvItem.SubItems.Add($"{moduleData.FileSize:#,###0}");

            // Attributes
            lvItem.SubItems.Add(moduleData.Attributes.ShortName());

            // Link/Real Checksum
            lvItem.SubItems.Add($"0x{moduleData.LinkChecksum:X8}");

            if (moduleData.LinkChecksum != 0 && (moduleData.LinkChecksum != moduleData.RealChecksum))
            {
                lvItem.UseItemStyleForSubItems = false;
                lvItem.SubItems.Add($"0x{moduleData.RealChecksum:X8}", Color.Red, Color.White, lvItem.Font);
            }
            else
            {
                lvItem.SubItems.Add($"0x{moduleData.RealChecksum:X8}");
            }

            // CPU
            string value = Enum.IsDefined(typeof(Machine), moduleData.Machine)
                ? ((Machine)moduleData.Machine).FriendlyName()
                : $"0x{moduleData.Machine:X4}";

            bool isCpuMismatch = IsCpuMismatchForDisplay(module, _depends.RootModule);
            if (isCpuMismatch)
            {
                lvItem.UseItemStyleForSubItems = false;
                lvItem.SubItems.Add(value, Color.Red, Color.White, lvItem.Font);
            }
            else
            {
                lvItem.SubItems.Add(value);
            }

            // Subsystem
            lvItem.SubItems.Add(Enum.IsDefined(typeof(Subsystem), moduleData.Subsystem)
                ? ((Subsystem)moduleData.Subsystem).FriendlyName()
                : $"0x{moduleData.Subsystem:X4}");

            // Debug Symbols
            if (moduleData.DebugDirTypes.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var entry in moduleData.DebugDirTypes.Distinct())
                {
                    if (_debugAbbreviations.TryGetValue(entry, out var abbr))
                    {
                        if (sb.Length > 0) sb.Append(',');
                        sb.Append(abbr);
                    }
                }
                value = sb.Length > 0 ? sb.ToString() : CConsts.NoneMsg;
            }
            else
            {
                value = CConsts.NoneMsg;
            }
            lvItem.SubItems.Add(value);

            // Preferred base
            // Calculate the hexadecimal format string based on architecture
            string hexFormat = $"X{UIntPtr.Size * (module.Is64bitArchitecture() ? 2 : 1)}";
            lvItem.SubItems.Add($"0x{moduleData.PreferredBase.ToString(hexFormat)}");

            // Virtual size
            lvItem.SubItems.Add($"0x{moduleData.VirtualSize:X8}");

            // Versions
            lvItem.SubItems.Add(moduleData.FileVersion);
            lvItem.SubItems.Add(moduleData.ProductVersion);
            lvItem.SubItems.Add(moduleData.ImageVersion);
            lvItem.SubItems.Add(moduleData.LinkerVersion);
            lvItem.SubItems.Add(moduleData.OSVersion);
            lvItem.SubItems.Add(moduleData.SubsystemVersion);
        }
        return lvItem;
    }

    /// <summary>
    /// Retrieve list item from cache or build it from modules list.
    /// Automatically called when ListView wants to populate items.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVModulesRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
    {
        if (_lvModulesCache != null &&
            e.ItemIndex >= _lvModulesFirstItem &&
            e.ItemIndex < _lvModulesFirstItem + _lvModulesCache.Length)
        {
            e.Item = _lvModulesCache[e.ItemIndex - _lvModulesFirstItem];
        }
        else
        {
            e.Item = LVCreateModuleEntry(_loadedModulesList[e.ItemIndex]);
        }
    }

    /// <summary>
    /// Cache listview modules entry.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVModulesCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
    {
        if (_lvModulesCache != null && e.StartIndex >= _lvModulesFirstItem && e.EndIndex <= _lvModulesFirstItem + _lvModulesCache.Length)
        {
            return;
        }

        _lvModulesFirstItem = e.StartIndex;
        int length = e.EndIndex - e.StartIndex + 1;
        _lvModulesCache = new ListViewItem[length];

        for (int i = 0, j = _lvModulesFirstItem; i < length && j < _loadedModulesList.Count; i++, j++)
        {
            _lvModulesCache[i] = LVCreateModuleEntry(_loadedModulesList[j]);
        }
    }

    /// <summary>
    /// LVModules virtual listview search handler.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LVModulesSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
    {
        foreach (var module in _loadedModulesList)
        {
            if (module.FileName.Equals(e.Text, StringComparison.OrdinalIgnoreCase))
            {
                e.Index = _loadedModulesList.IndexOf(module);
                return;
            }
        }
    }

    /// <summary>
    /// LVModules virtual listview sort handler.
    /// </summary>
    /// <param name="listView"></param>
    /// <param name="columnIndex"></param>
    /// <param name="sortOrder"></param>
    /// <param name="moduleList"></param>
    /// <param name="cacheType"></param>
    private void LVModulesSort(ListView listView, int columnIndex, SortOrder sortOrder, List<CModule> moduleList, DisplayCacheType cacheType)
    {
        IComparer<CModule> modulesComparer = new CModuleComparer(sortOrder, columnIndex, _configuration.FullPaths);
        moduleList.Sort(modulesComparer);
        //
        // Reset listview items cache.
        //
        UpdateItemsView(listView, cacheType);
        UpdateListViewColumnSortMark(listView, columnIndex, sortOrder);
    }

    private void LVModulesColumnClick(object sender, ColumnClickEventArgs e)
    {
        int columnIndex = e.Column;
        _configuration.SortColumnModules = columnIndex;
        _lvModulesSortOrder = (_lvModulesSortOrder == SortOrder.Descending) ? SortOrder.Ascending : SortOrder.Descending;
        LVModulesSort(LVModules, columnIndex, _lvModulesSortOrder, _loadedModulesList, DisplayCacheType.Modules);
    }

    private void HighlightModuleInTreeOrList(object sender, EventArgs e)
    {
        if (TVModules.Focused && TVModules.SelectedNode?.Tag is CModule selectedModule)
        {
            LVModules.BeginUpdate();
            try
            {
                ListViewItem lvResult = LVModules.FindItemWithText(selectedModule.FileName);
                if (lvResult != null)
                {
                    LVModules.SelectedIndices.Clear();
                    lvResult.Selected = true;
                    lvResult.EnsureVisible();
                    LVModules.Focus();
                }
            }
            finally { LVModules.EndUpdate(); }
        }
        else if (LVModules.Focused && LVModules.SelectedIndices.Count > 0)
        {
            int selectedItemIndex = LVModules.SelectedIndices[0];

            if (selectedItemIndex < _loadedModulesList.Count)
            {
                CModule selectedListViewModule = _loadedModulesList[selectedItemIndex];

                TVModules.BeginUpdate();
                try
                {
                    TreeNode resultNode = CUtils.TreeViewFindModuleNodeByObject(selectedListViewModule, _rootNode);

                    if (resultNode != null)
                    {
                        TVModules.SelectedNode = resultNode;
                        TVModules.SelectedNode.Expand();
                        TVModules.SelectedNode.EnsureVisible();
                        TVModules.Select();
                    }
                }
                finally { TVModules.EndUpdate(); }
            }
        }
    }

    private void HighlightOriginalInstance_Click(object sender, EventArgs e)
    {
        TVModules.BeginUpdate();
        try
        {
            CModule origInstance = CUtils.TreeViewGetOriginalInstanceFromNode(TVModules.SelectedNode, _loadedModulesList);
            if (origInstance != null)
            {
                var tvNode = CUtils.TreeViewFindModuleNodeByObject(origInstance, _rootNode);
                if (tvNode != null)
                {
                    TVModules.SelectedNode = tvNode;
                    TVModules.SelectedNode.Expand();
                    TVModules.SelectedNode.EnsureVisible();
                    TVModules.Select();
                }
            }
        }
        finally { TVModules.EndUpdate(); }
    }

    TreeNode TreeViewFindNodeInstancePrev(TreeNode currentNode, TreeNode selectedNode, string moduleName)
    {
        TreeNode lastNode = null;

        while (currentNode != null && !_instanceStopSearch)
        {
            if (currentNode.Tag is CModule obj && obj.FileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentNode == selectedNode)
                {
                    _instanceStopSearch = true;
                    break;
                }
                else
                {
                    lastNode = currentNode;
                }
            }

            if (currentNode.Nodes.Count != 0 && !_instanceStopSearch)
            {
                var tvNode = TreeViewFindNodeInstancePrev(currentNode.Nodes[0], selectedNode, moduleName);
                if (tvNode != null)
                {
                    lastNode = tvNode;
                }
            }

            currentNode = currentNode.NextNode;
        }

        return lastNode;
    }

    TreeNode TreeViewFindNodeInstanceNext(TreeNode currentNode, TreeNode selectedNode, string moduleName)
    {
        TreeNode lastNode = null;

        while (currentNode != null && !_instanceStopSearch)
        {
            if (currentNode.Tag is CModule obj && obj.FileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentNode == selectedNode)
                {
                    _instanceSelfFound = true;
                }
                else if (_instanceSelfFound)
                {
                    _instanceStopSearch = true;
                    lastNode = currentNode;
                }
            }

            if (currentNode.Nodes.Count != 0 && !_instanceStopSearch)
            {
                var tvNode = TreeViewFindNodeInstanceNext(currentNode.Nodes[0], selectedNode, moduleName);
                if (tvNode != null)
                {
                    lastNode = tvNode;
                }
            }

            currentNode = currentNode.NextNode;
        }

        return lastNode;
    }

    private void HighlightInstanceHandler(bool bNextInstance = false)
    {
        if (TVModules.SelectedNode == null || TVModules.SelectedNode.Tag is not CModule obj)
        {
            return;
        }

        _instanceStopSearch = false;

        if (bNextInstance)
        {
            _instanceSelfFound = false;
        }

        TVModules.BeginUpdate();
        try
        {
            TreeNode tvNode;

            if (bNextInstance)
            {
                tvNode = TreeViewFindNodeInstanceNext(_rootNode, TVModules.SelectedNode, obj.FileName);
            }
            else
            {
                tvNode = TreeViewFindNodeInstancePrev(_rootNode, TVModules.SelectedNode, obj.FileName);
            }

            if (tvNode != null)
            {
                TVModules.SelectedNode = tvNode;
                TVModules.SelectedNode.Expand();
                TVModules.SelectedNode.EnsureVisible();
                TVModules.Select();
            }
        }
        finally { TVModules.EndUpdate(); }
    }

    private void HighlightPreviousInstance_Click(object sender, EventArgs e)
    {
        HighlightInstanceHandler(false);
    }

    private void HighlightNextInstance_Click(object sender, EventArgs e)
    {
        HighlightInstanceHandler(true);
    }

    /// <summary>
    /// Finds a module node by instance ID, expands its parent chain, and selects it in the tree view.
    /// </summary>
    /// <param name="instanceId">Module instance ID to locate.</param>
    private void FindAndSelectModuleNode(int instanceId)
    {
        TreeNode foundNode = FindModuleNodeByInstanceId(TVModules.Nodes, instanceId);
        if (foundNode != null)
        {
            TreeNode parent = foundNode.Parent;
            while (parent != null)
            {
                parent.Expand();
                parent = parent.Parent;
            }

            TVModules.SelectedNode = foundNode;
            foundNode.EnsureVisible();

            TVModules.Focus();
        }
    }

    /// <summary>
    /// Finds a tree node by module instance ID using breadth-first search.
    /// </summary>
    /// <param name="nodes">Collection of tree nodes to search.</param>
    /// <param name="instanceId">Module instance ID to find.</param>
    /// <returns>The found tree node, or null if no matching node exists.</returns>
    private static TreeNode FindModuleNodeByInstanceId(TreeNodeCollection nodes, int instanceId)
    {
        Queue<TreeNode> nodesToSearch = new Queue<TreeNode>();

        foreach (TreeNode rootNode in nodes)
        {
            if (rootNode != null)
                nodesToSearch.Enqueue(rootNode);
        }

        while (nodesToSearch.Count > 0)
        {
            TreeNode currentNode = nodesToSearch.Dequeue();
            if (currentNode.Tag is CModule module && module.InstanceId == instanceId)
            {
                return currentNode;
            }

            foreach (TreeNode childNode in currentNode.Nodes)
            {
                if (childNode != null)
                    nodesToSearch.Enqueue(childNode);
            }
        }

        return null;
    }

    private void LVModules_KeyPress(object sender, KeyPressEventArgs e)
    {
        CModule matchingModule;
        ListViewItem lvResult;

        if (!LVModules.Focused || LVModules.VirtualListSize <= 0)
        {
            e.Handled = true;
            return;
        }

        if (e.KeyChar == (char)Keys.Back)
        {
            if (_moduleLookupText.Length > 0)
            {
                _moduleLookupText = _moduleLookupText[..^1]; //use fancy new range syntax
            }

            if (_moduleLookupText.Length == 0)
            {
                _moduleLookupTimer.Stop();
                _moduleLookupText = string.Empty;
                HideTypeSearchHint();
                return;
            }

            ShowTypeSearchHint(LVModules, "Search: " + _moduleLookupText);
        }
        else
        {
            if (char.IsControl(e.KeyChar))
            {
                e.Handled = true;
                return;
            }

            _moduleLookupText += char.ToLowerInvariant(e.KeyChar);
            ShowTypeSearchHint(LVModules, "Search: " + _moduleLookupText);
        }

        matchingModule = _loadedModulesList.FirstOrDefault(module =>
        {
            string moduleName = Path.GetFileName(module.GetModuleNameRespectApiSet(_configuration.ResolveAPIsets));
            return moduleName.StartsWith(_moduleLookupText, StringComparison.OrdinalIgnoreCase);
        });

        if (matchingModule != null)
        {
            LVModules.BeginUpdate();
            try
            {
                LVModules.SelectedIndices.Clear();
                lvResult = LVModules.FindItemWithText(matchingModule.FileName);
                if (lvResult != null)
                {
                    lvResult.Selected = true;
                    lvResult.EnsureVisible();
                    LVModules.Focus();
                }
            }
            finally { LVModules.EndUpdate(); }
        }

        _moduleLookupTimer.Stop();
        _moduleLookupTimer.Start();

        e.Handled = true;
    }

    private void LVModules_Leave(object sender, EventArgs e)
    {
        _moduleLookupTimer.Stop();
        HideTypeSearchHint();
        _moduleLookupText = string.Empty;
    }

    private void LVModules_Click(object sender, EventArgs e)
    {
        CopyToolButton.Enabled = LVModules.SelectedIndices.Count > 0;
    }

    private void TVModules_Click(object sender, EventArgs e)
    {
        CopyToolButton.Enabled = TVModules.SelectedNode != null;
    }
    private void ViewOpenModuleLocationItem_Click(object sender, EventArgs e)
    {
        ProcessModuleEntry(ProcessModuleAction.OpenFileLocation);
    }
}
