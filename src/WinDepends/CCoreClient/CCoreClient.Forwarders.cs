/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       CCORECLIENT.FORWARDERS.CS
*
*  VERSION:     1.00
*
*  DATE:        23 May 2026
*  
*  Forwarded export resolution and validation routines for  
*  Core Server communication class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

public partial class CCoreClient
{
    /// <summary>
    /// Determines whether a module name or path matches a candidate module identifier.
    /// </summary>
    /// <param name="moduleName">The module name or full path to test.</param>
    /// <param name="candidate">The candidate module identifier to compare against.</param>
    /// <returns>true if the values match directly, by file name, or by file name without extension; otherwise, false.</returns>
    private static bool IsModuleMatch(string moduleName, string candidate)
    {
        if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(candidate))
            return false;

        return moduleName.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
               Path.GetFileName(moduleName).Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
               Path.GetFileNameWithoutExtension(moduleName).Equals(candidate, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the full path of a forwarder target module by trying multiple file extensions
    /// if the raw target has none, using the appropriate search order for the given module context.
    /// </summary>
    /// <param name="rawTarget">
    /// The raw forwarder target module name, as found in the export table
    /// (e.g., "NTDLL", "kernel32.dll"). May or may not include a file extension.
    /// </param>
    /// <param name="module">
    /// The owning module providing context for path resolution (e.g., its directory,
    /// whether it is a kernel-mode module).
    /// </param>
    /// <param name="searchOrderUM">
    /// The ordered list of search locations to apply when resolving user-mode modules.
    /// </param>
    /// <param name="searchOrderKM">
    /// The ordered list of search locations to apply when resolving kernel-mode modules.
    /// </param>
    /// <param name="resolvedBy">
    /// Outputs the <see cref="SearchOrderType"/> entry that successfully located the module,
    /// or <see cref="SearchOrderType.None"/> if resolution failed.
    /// </param>
    /// <returns>
    /// The fully resolved file-system path of the target module if found; otherwise the
    /// original <paramref name="rawTarget"/> string is returned as a fallback, or
    /// <see cref="string.Empty"/> if <paramref name="rawTarget"/> is null or empty.
    /// </returns>
    private static string ResolveForwarderTargetModuleName(
        string rawTarget,
        CModule module,
        List<SearchOrderType> searchOrderUM,
        List<SearchOrderType> searchOrderKM,
        out SearchOrderType resolvedBy)
    {
        string candidate;
        string resolvedPath;

        resolvedBy = SearchOrderType.None;

        if (string.IsNullOrEmpty(rawTarget))
            return string.Empty;

        candidate = rawTarget;
        resolvedPath = CPathResolver.ResolvePathForModule(candidate, module, searchOrderUM, searchOrderKM, out resolvedBy);
        if (!string.IsNullOrEmpty(resolvedPath))
            return resolvedPath;

        if (!Path.HasExtension(rawTarget))
        {
            candidate = rawTarget + CConsts.DllFileExt;
            resolvedPath = CPathResolver.ResolvePathForModule(candidate, module, searchOrderUM, searchOrderKM, out resolvedBy);
            if (!string.IsNullOrEmpty(resolvedPath))
                return resolvedPath;

            candidate = rawTarget + CConsts.ExeFileExt;
            resolvedPath = CPathResolver.ResolvePathForModule(candidate, module, searchOrderUM, searchOrderKM, out resolvedBy);
            if (!string.IsNullOrEmpty(resolvedPath))
                return resolvedPath;


            candidate = rawTarget + CConsts.SysFileExt;
            resolvedPath = CPathResolver.ResolvePathForModule(candidate, module, searchOrderUM, searchOrderKM, out resolvedBy);
            if (!string.IsNullOrEmpty(resolvedPath))
                return resolvedPath;
        }

        return rawTarget;
    }

    /// <summary>
    /// Parses a forwarder string to extract the target module and function information.
    /// </summary>
    /// <param name="forwarder">The forwarder string (e.g., "MODULE.Function" or "MODULE.#123").</param>
    /// <param name="targetModule">Outputs the target module name.</param>
    /// <param name="targetFunctionName">Outputs the target function name (empty if by ordinal).</param>
    /// <param name="targetOrdinal">Outputs the target ordinal (CConsts.OrdinalNotPresent if by name).</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    private static bool TryParseForwarderTarget(string forwarder,
                                                   out string targetModule,
                                                   out string targetFunctionName,
                                                   out uint targetOrdinal)
    {
        targetModule = string.Empty;
        targetFunctionName = string.Empty;
        targetOrdinal = CConsts.OrdinalNotPresent;

        if (string.IsNullOrEmpty(forwarder))
            return false;

        int dot = forwarder.IndexOf('.');
        if (dot <= 0 || dot == forwarder.Length - 1)
            return false;

        targetModule = forwarder.Substring(0, dot);

        ReadOnlySpan<char> rest = forwarder.AsSpan(dot + 1);
        if (rest.Length == 0)
            return false;

        // Ordinal forwarder (e.g. "KERNEL32.#123")
        if (rest[0] == '#')
        {
            ulong value = 0;
            int i = 1;
            while (i < rest.Length && char.IsDigit(rest[i]))
            {
                value = value * 10 + (uint)(rest[i] - '0');
                if (value > CConsts.OrdinalNotPresent) break;
                i++;
            }
            if (i == 1)
                return false;

            targetOrdinal = (uint)value;
            return true;
        }

        // Name forwarder: copy until whitespace (whitespace not expected but safe stop)
        int fnEnd = 0;
        while (fnEnd < rest.Length && !char.IsWhiteSpace(rest[fnEnd]))
            fnEnd++;

        targetFunctionName = rest.Slice(0, fnEnd).ToString();
        return !string.IsNullOrEmpty(targetFunctionName);
    }

    /// <summary>
    /// Expands all forwarder modules in the dependency tree starting from the root module.
    /// </summary>
    /// <param name="root">The root module to start from.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <param name="parentImportsHashTable">Hash table for tracking parent imports.</param>
    public void ExpandAllForwarderModules(CModule root,
                                        List<SearchOrderType> searchOrderUM,
                                        List<SearchOrderType> searchOrderKM,
                                        Dictionary<int, FunctionHashObject> parentImportsHashTable)
    {
        if (root == null) return;

        Queue<CModule> q = new();
        HashSet<CModule> visited = new();
        q.Enqueue(root);

        while (q.Count > 0)
        {
            var m = q.Dequeue();
            if (!visited.Add(m))
                continue;

            // Expand forwarders for this module once
            if (!m.ForwardersExpanded && m.ForwarderEntries.Count > 0)
            {
                ExpandForwardersForModule(m, searchOrderUM, searchOrderKM, parentImportsHashTable, null);
                m.ForwardersExpanded = true;
            }

            if (m.Dependents != null)
            {
                foreach (var d in m.Dependents)
                {
                    q.Enqueue(d);

                    // Check forward target export errors and log them
                    // Skip error propagation for apiset contracts and duplicate/stopped nodes
                    if (d.IsForward && d.ExportContainErrors)
                    {
                        // Don't propagate errors from apiset contracts - they are virtual redirectors
                        if (d.IsApiSetContract)
                            continue;

                        // Don't propagate errors from duplicate/stopped nodes (no dependents but has forwarders)
                        if (d.IsStoppedNode)
                            continue;

                        _addLogMessage($"Forwarded module \"{Path.GetFileName(d.FileName)}\" contains export errors (referenced by \"{Path.GetFileName(m.FileName)}\").",
                            LogMessageType.ErrorOrWarning);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates that forwarded exports in a module can be resolved in their target modules.
    /// Should be called after the target forward modules have been processed.
    /// </summary>
    /// <param name="module">The module containing forwarded exports.</param>
    /// <param name="addLogMessage">Delegate for logging messages.</param>
    public void ValidateForwardedExports(CModule module)
    {
        if (module?.ForwarderEntries == null || module.ForwarderEntries.Count == 0)
            return;

        // Skip validation for apiset contracts - they are virtual redirectors
        // whose forwards are always valid by design
        if (module.IsApiSetContract)
            return;

        // Skip validation for duplicate/stopped nodes
        if (module.IsStoppedNode)
            return;

        foreach (var fe in module.ForwarderEntries)
        {
            var targetModule = module.Dependents.FirstOrDefault(d =>
                IsModuleMatch(d.FileName, fe.TargetModuleName) ||
                IsModuleMatch(d.RawFileName, fe.TargetModuleName));

            if (targetModule == null)
                continue;

            if (targetModule.FileNotFound)
            {
                module.OtherErrorsPresent = true;
                continue;
            }

            if (targetModule.ModuleData?.Exports == null || targetModule.ModuleData.Exports.Count == 0)
                continue;

            bool resolved;
            if (fe.TargetOrdinal != CConsts.OrdinalNotPresent)
            {
                resolved = targetModule.ModuleData.Exports.Any(f => f.Ordinal == fe.TargetOrdinal);
            }
            else
            {
                resolved = targetModule.ModuleData.Exports.Any(f =>
                    f.RawName.Equals(fe.TargetFunctionName, StringComparison.Ordinal));
            }

            if (!resolved)
            {
                module.OtherErrorsPresent = true;
                string funcDesc = fe.TargetOrdinal != CConsts.OrdinalNotPresent
                    ? $"ordinal {fe.TargetOrdinal}"
                    : $"function \"{fe.TargetFunctionName}\"";

                _addLogMessage($"Forward from \"{Path.GetFileName(module.FileName)}\" to {funcDesc} in \"{fe.TargetModuleName}\" cannot be resolved.",
                    LogMessageType.ErrorOrWarning);
            }
        }
    }

    /// <summary>
    /// Creates or retrieves a forward node for a target module.
    /// </summary>
    /// <param name="parentModule">The parent module.</param>
    /// <param name="rawTarget">The raw target module name.</param>
    /// <param name="canonicalName">The canonical module name.</param>
    /// <param name="finalTargetName">The final resolved target name.</param>
    /// <param name="resolvedBy">How the name was resolved.</param>
    /// <param name="isApiSetContract">Whether this is an API Set contract.</param>
    /// <returns>The forward node module.</returns>
    private CModule CreateOrGetForwardNode(CModule parentModule,
                                      string rawTarget,
                                      string canonicalName,
                                      string finalTargetName,
                                      SearchOrderType resolvedBy,
                                      bool isApiSetContract)
    {
        // First check for existing real (non-forward) module
        var existingReal = parentModule.Dependents.FirstOrDefault(d =>
            !d.IsForward &&
            (IsModuleMatch(d.FileName, finalTargetName) ||
             IsModuleMatch(d.FileName, canonicalName) ||
             IsModuleMatch(d.RawFileName, rawTarget)));

        if (existingReal != null)
            return existingReal;

        // Check for existing synthetic forward node
        var forwardNode = parentModule.Dependents.FirstOrDefault(d =>
            d.IsForward &&
            (IsModuleMatch(d.FileName, finalTargetName) ||
             IsModuleMatch(d.FileName, canonicalName) ||
             IsModuleMatch(d.RawFileName, rawTarget)));

        if (forwardNode == null)
        {
            forwardNode = new CModule(finalTargetName,
                                     rawTarget, // keep original forward string module token
                                     resolvedBy,
                                     isApiSetContract)
            {
                IsKernelModule = parentModule.IsKernelModule,
                IsForward = true
            };
            parentModule.Dependents.Add(forwardNode);
        }

        return forwardNode;
    }

    /// <summary>
    /// Expands forwarders for a specific module, creating synthetic dependency nodes as needed.
    /// Uses a chain tracking mechanism to detect and prevent circular forwarding.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <param name="parentImportsHashTable">Hash table for tracking parent imports.</param>
    /// <param name="forwardingChain">Set of modules in current forwarding chain to detect cycles.</param>
    public void ExpandForwardersForModule(CModule module,
                                        List<SearchOrderType> searchOrderUM,
                                        List<SearchOrderType> searchOrderKM,
                                        Dictionary<int, FunctionHashObject> parentImportsHashTable,
                                        HashSet<string> forwardingChain = null)
    {
        if (module?.ForwarderEntries == null || module.ForwarderEntries.Count == 0)
            return;

        // Initialize tracking set for first call in chain
        bool isRootCall = forwardingChain == null;
        forwardingChain ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Track this module to detect cycles
        string moduleKey = Path.GetFileName(module.FileName).ToLowerInvariant();
        if (!isRootCall && !forwardingChain.Add(moduleKey))
        {
            string cyclePath = string.Join(" → ", forwardingChain) + " → " + moduleKey;
            _addLogMessage($"Circular forwarding detected: {cyclePath}",
                LogMessageType.ErrorOrWarning);
            return;
        }

        var groups = module.ForwarderEntries
                           .Where(f => f.TargetModuleName != null)
                           .GroupBy(f => f.TargetModuleName, StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            string rawTarget = g.Key;

            // Skip if we've detected this as part of a cycle
            string targetKey = Path.GetFileName(rawTarget).ToLowerInvariant();
            if (forwardingChain.Contains(targetKey))
            {
                _addLogMessage($"Skipping circular forward from {moduleKey} to {targetKey}",
                    LogMessageType.ErrorOrWarning);
                continue;
            }

            bool isApiSetContract = CUtils.IsModuleNameApiSetContract(rawTarget);
            string canonicalName = isApiSetContract ? ResolveApiSetName(rawTarget, module) : rawTarget;
            string finalTargetName = ResolveForwarderTargetModuleName(
                canonicalName, module, searchOrderUM, searchOrderKM, out SearchOrderType resolvedBy);

            // Create the synthetic forward node
            var forwardNode = CreateOrGetForwardNode(
                module, rawTarget, canonicalName, finalTargetName, resolvedBy, isApiSetContract);

            // If this returned an existing real module, skip processing this group
            if (!forwardNode.IsForward)
                continue;

            foreach (var fe in g)
            {
                bool exists;
                if (fe.TargetOrdinal != CConsts.OrdinalNotPresent)
                {
                    exists = forwardNode.ParentImports.Any(f => f.Ordinal == fe.TargetOrdinal);
                }
                else
                {
                    exists = forwardNode.ParentImports.Any(f =>
                        f.Ordinal == CConsts.OrdinalNotPresent &&
                        f.RawName.Equals(fe.TargetFunctionName, StringComparison.Ordinal));
                }
                if (exists)
                    continue;

                var synthetic = new CFunction
                {
                    RawName = (fe.TargetOrdinal == CConsts.OrdinalNotPresent) ? fe.TargetFunctionName : string.Empty,
                    Ordinal = (fe.TargetOrdinal == CConsts.OrdinalNotPresent) ? CConsts.OrdinalNotPresent : fe.TargetOrdinal,
                    Hint = CConsts.HintNotPresent,
                    IsExportFunction = false
                };
                synthetic.Kind = synthetic.MakeDefaultFunctionKind();

                forwardNode.ParentImports.Add(synthetic);

                var fho = new FunctionHashObject(
                    forwardNode.FileName,
                    (fe.TargetOrdinal == CConsts.OrdinalNotPresent) ? synthetic.RawName : string.Empty,
                    synthetic.Ordinal);

                parentImportsHashTable.TryAdd(fho.GenerateUniqueKey(), fho);
            }

            // After processing, check if forward target has errors and propagate to parent
            // Skip propagation for apiset contracts and stopped nodes
            if (forwardNode.ExportContainErrors || forwardNode.FileNotFound)
            {
                // Don't propagate from apiset contracts
                if (forwardNode.IsApiSetContract)
                    continue;

                // Don't propagate from stopped/duplicate nodes
                bool isStoppedNode = (forwardNode.Dependents == null || forwardNode.Dependents.Count == 0) &&
                                     (forwardNode.ForwarderEntries != null && forwardNode.ForwarderEntries.Count > 0);
                if (isStoppedNode)
                    continue;

                module.OtherErrorsPresent = true;
            }
        }

        // Remove this module from chain when done with this branch
        if (!isRootCall)
        {
            forwardingChain.Remove(moduleKey);
        }
    }
}
