/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       CCORECLIENT.DEPENDENCYANALYSIS.CS
*
*  VERSION:     1.00
*
*  DATE:        14 Jul 2026
*  
*  Module dependency/import/export analysis for 
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
    private static readonly HashSet<string> s_forbiddenKernelLibs = new(StringComparer.OrdinalIgnoreCase)
    {
        CConsts.NtdllDll,
        CConsts.Kernel32Dll
    };

    private static readonly HashSet<string> s_requiredKernelLibs = new(StringComparer.OrdinalIgnoreCase)
    {
        CConsts.NtoskrnlExe,
        CConsts.HalDll,
        CConsts.KdComDll,
        CConsts.BootVidDll
    };

    /// <summary>
    /// Handles and logs import processing exceptions.
    /// </summary>
    /// <param name="imports">The import information containing exception data.</param>
    private void HandleImportExceptions(CCoreImports imports)
    {
        bool exceptStd = (imports.Exception & 1) != 0;
        bool exceptDelay = (imports.Exception & 2) != 0;

        if (PeExceptionHelper.IsInvalidImageFormatException(imports.ExceptionCodeStd))
        {
            _addLogMessage($"Exception occured while processing file, imports seems destroyed",
                LogMessageType.ErrorOrWarning);
            return;
        }

        if (exceptStd && exceptDelay)
        {
            _addLogMessage(
                $"Exceptions occurred while processing imports:\n" +
                $"  Standard: {PeExceptionHelper.TranslateExceptionCode(imports.ExceptionCodeStd)} (0x{imports.ExceptionCodeStd:X8})\n" +
                $"  Delay-load: {PeExceptionHelper.TranslateExceptionCode(imports.ExceptionCodeDelay)} (0x{imports.ExceptionCodeDelay:X8})",
                LogMessageType.ErrorOrWarning);
        }
        else if (exceptStd)
        {
            _addLogMessage(
                $"Exception {PeExceptionHelper.TranslateExceptionCode(imports.ExceptionCodeStd)} (0x{imports.ExceptionCodeStd:X8}) occurred while processing standard imports",
                LogMessageType.ErrorOrWarning);
        }
        else if (exceptDelay)
        {
            _addLogMessage(
                $"Exception {PeExceptionHelper.TranslateExceptionCode(imports.ExceptionCodeDelay)} (0x{imports.ExceptionCodeDelay:X8}) occurred while processing delay-load imports",
                LogMessageType.ErrorOrWarning);
        }
    }

    /// <summary>
    /// Determines whether a module is a kernel-mode module based on its imports.
    /// </summary>
    /// <param name="module">The module to check.</param>
    /// <param name="imports">The import information for the module.</param>
    private static void CheckIfKernelModule(CModule module, CCoreImports imports)
    {
        // Skip check if already determined to be a kernel module
        if (module.IsKernelModule ||
            module.ModuleData.Subsystem != NativeMethods.IMAGE_SUBSYSTEM_NATIVE)
            return;

        bool hasForbiddenLibrary = false;
        bool hasRequiredLibrary = false;

        foreach (var entry in imports.Library)
        {
            // Check for forbidden user-mode DLL's
            if (!hasForbiddenLibrary && s_forbiddenKernelLibs.Contains(entry.Name))
            {
                hasForbiddenLibrary = true;
                break;
            }

            // Check for required kernel-mode components
            if (!hasRequiredLibrary && s_requiredKernelLibs.Contains(entry.Name))
            {
                hasRequiredLibrary = true;
            }
        }

        // Module is kernel-mode if it has required libraries but no forbidden ones
        if (!hasForbiddenLibrary && hasRequiredLibrary)
        {
            module.IsKernelModule = true;
        }
    }

    /// <summary>
    /// Adds a dependent module to a parent module's dependency list.
    /// </summary>
    /// <param name="parentModule">The parent module.</param>
    /// <param name="DelayLibraries">Whether this is a delay-load dependency.</param>
    /// <param name="moduleName">The module name to add.</param>
    /// <param name="rawModuleName">The raw (unresolved) module name.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <returns>The newly created dependent module.</returns>
    public CModule AddDependentModule(CModule parentModule,
                                   bool DelayLibraries,
                                   string moduleName,
                                   string rawModuleName,
                                   List<SearchOrderType> searchOrderUM,
                                   List<SearchOrderType> searchOrderKM)
    {
        // Resolve ApiSet name if any.
        bool isApiSetContract = CUtils.IsModuleNameApiSetContract(moduleName);
        if (isApiSetContract)
        {
            moduleName = ResolveApiSetName(moduleName, parentModule);
        }

        // Resolve module path.
        var moduleFileName = CPathResolver.ResolvePathForModule(moduleName,
                                                                parentModule,
                                                                searchOrderUM,
                                                                searchOrderKM,
                                                                out SearchOrderType resolvedBy);

        if (!string.IsNullOrEmpty(moduleFileName))
        {
            moduleName = moduleFileName;
        }

        CModule dependent = new(moduleName, rawModuleName, resolvedBy, isApiSetContract)
        {
            IsDelayLoad = DelayLibraries,
            IsKernelModule = parentModule.IsKernelModule, //propagate from parent
        };

        parentModule.Dependents.Add(dependent);

        return dependent;
    }

    /// <summary>
    /// Processes .NET assembly references for a module.
    /// </summary>
    /// <param name="module">The module to process.</param>
    void ProcessNetAssemblies(CModule module)
    {
        var dependencies = CAssemblyRefAnalyzer.GetAssemblyDependencies(module);
        CModule netDependent;
        foreach (var reference in dependencies)
        {
            if (reference.IsResolved)
            {
                netDependent = new(reference.ResolvedPath, reference.ResolvedPath, SearchOrderType.None, false);
            }
            else
            {
                netDependent = new(reference.Name, reference.Name, SearchOrderType.None, false);
            }
            netDependent.ModuleData.RuntimeVersion = module.ModuleData.RuntimeVersion;
            netDependent.ModuleData.FrameworkKind = module.ModuleData.FrameworkKind;
            netDependent.ModuleData.ResolutionSource = reference.ResolutionSource;
            netDependent.ModuleData.ReferenceVersion = reference.Version;
            netDependent.ModuleData.ReferencePublicKeyToken = reference.PublicKeyToken;
            netDependent.ModuleData.ReferenceCulture = reference.Culture;
            netDependent.IsDotNetModule = true;
            module.Dependents.Add(netDependent);
        }

        CAssemblyRefAnalyzer.ClearCache();
    }

    /// <summary>
    /// Processes import libraries for a module and adds them as dependencies.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="DelayLibraries">Whether these are delay-load imports.</param>
    /// <param name="LibraryList">The list of imported libraries.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <param name="parentImportsHashTable">Hash table for tracking parent imports.</param>
    public void ProcessImports(CModule module,
                                bool DelayLibraries,
                                List<CCoreImportLibrary> LibraryList,
                                List<SearchOrderType> searchOrderUM,
                                List<SearchOrderType> searchOrderKM,
                                Dictionary<int, FunctionHashObject> parentImportsHashTable)
    {
        foreach (var entry in LibraryList)
        {
            var dependent = AddDependentModule(module, DelayLibraries,
                entry.Name, entry.Name, searchOrderUM, searchOrderKM);

            foreach (var func in entry.Function)
            {
                dependent.ParentImports.Add(new CFunction(func));
                FunctionHashObject funcHashObject = new(dependent.FileName, func.Name, func.Ordinal);
                var uniqueKey = funcHashObject.GenerateUniqueKey();
                parentImportsHashTable.TryAdd(uniqueKey, funcHashObject);
            }
        }
    }

    /// <summary>
    /// Populates module exports, validates existing parent imports, and processes forwarders.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="collectForwarders">Whether to collect forwarder information.</param>
    /// <param name="rawExports">The raw export data from the server.</param>
    void ProcessExports(CModule module,
                        bool collectForwarders,
                        CCoreExports rawExports)
    {
        if (module == null || rawExports?.Library == null)
            return;

        foreach (var entry in rawExports.Library.Function)
        {
            var cf = new CFunction(entry);
            module.ModuleData.Exports.Add(cf);

            // Collect forwarder metadata
            if (collectForwarders && !string.IsNullOrEmpty(cf.ForwardName))
            {
                if (TryParseForwarderTarget(cf.ForwardName,
                    out _, out string targetFn, out uint targetOrd))
                {
                    string rawTargetModule = CFunction.ExtractForwarderModule(cf.ForwardName);
                    if (!string.IsNullOrEmpty(rawTargetModule))
                    {
                        string moduleFileName = Path.GetFileName(module.FileName);
                        string moduleFileNameNoExt = Path.GetFileNameWithoutExtension(module.FileName);
                        string rawFileName = Path.GetFileName(module.RawFileName);
                        string rawFileNameNoExt = Path.GetFileNameWithoutExtension(module.RawFileName);

                        // Skip self-forward
                        if (!rawTargetModule.Equals(moduleFileName, StringComparison.OrdinalIgnoreCase) &&
                                               !rawTargetModule.Equals(moduleFileNameNoExt, StringComparison.OrdinalIgnoreCase) &&
                                               !rawTargetModule.Equals(rawFileName, StringComparison.OrdinalIgnoreCase) &&
                                               !rawTargetModule.Equals(rawFileNameNoExt, StringComparison.OrdinalIgnoreCase))
                        {
                            var fe = new CForwarderEntry
                            {
                                TargetModuleName = rawTargetModule,
                                TargetFunctionName = (targetOrd == CConsts.OrdinalNotPresent) ? targetFn : string.Empty,
                                TargetOrdinal = (targetOrd == CConsts.OrdinalNotPresent) ? CConsts.OrdinalNotPresent : targetOrd
                            };
                            if (!module.ForwarderEntries.Contains(fe))
                                module.ForwarderEntries.Add(fe);
                        }
                    }
                }
            }
        }

        // Validate previously collected parent imports against exports
        foreach (var entry in module.ParentImports)
        {
            bool resolved;
            if (entry.Ordinal != CConsts.OrdinalNotPresent)
            {
                resolved = module.ModuleData.Exports.Any(f => f.Ordinal == entry.Ordinal);
            }
            else
            {
                resolved = module.ModuleData.Exports.Any(f =>
                    f.RawName.Equals(entry.RawName, StringComparison.Ordinal));
            }

            if (!resolved)
            {
                module.ExportContainErrors = true;
                break;
            }
        }
    }

    /// <summary>
    /// Checks and logs invalid import entries.
    /// </summary>
    /// <param name="module">The module being processed.</param>
    /// <param name="imports">The import information.</param>
    private void CheckInvalidImports(CModule module, CCoreImports imports)
    {
        string modName = module?.FileName ?? "<unknown>";

        if (imports.InvalidImportModuleCount != 0)
        {
            _addLogMessage($"Invalid import entries found ({imports.InvalidImportModuleCount}) while processing {modName}",
                LogMessageType.ErrorOrWarning);
        }
        if (imports.InvalidDelayImportModuleCount != 0)
        {
            _addLogMessage($"Invalid delay-load import entries found ({imports.InvalidDelayImportModuleCount}) while processing {modName}",
                LogMessageType.ErrorOrWarning);
        }
    }

    /// <summary>
    /// Retrieves and processes import and export information for a module.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="searchOrderUM">User-mode search order list.</param>
    /// <param name="searchOrderKM">Kernel-mode search order list.</param>
    /// <param name="parentImportsHashTable">Hash table for tracking parent imports.</param>
    /// <param name="EnableExperimentalFeatures">Whether to enable experimental features (.NET analysis).</param>
    /// <param name="CollectForwarders">Whether to collect forwarder information from exports.</param>
    public void GetModuleImportExportInformation(CModule module,
                                                 List<SearchOrderType> searchOrderUM,
                                                 List<SearchOrderType> searchOrderKM,
                                                 Dictionary<int, FunctionHashObject> parentImportsHashTable,
                                                 bool EnableExperimentalFeatures,
                                                 bool CollectForwarders)
    {
        if (module == null)
            return;
        //
        // Process exports.
        //
        CCoreExports rawExports = (CCoreExports)GetModuleInformationByType(ModuleInformationType.Exports, module);
        if (rawExports != null)
        {
            ProcessExports(module, CollectForwarders, rawExports);
        }

        //
        // Process imports.
        //
        CCoreImports rawImports = (CCoreImports)GetModuleInformationByType(ModuleInformationType.Imports, module);
        if (rawImports != null)
        {
            CheckInvalidImports(module, rawImports);
            CheckIfKernelModule(module, rawImports);
            ProcessImports(module, false, rawImports.Library, searchOrderUM, searchOrderKM, parentImportsHashTable);
            ProcessImports(module, true, rawImports.LibraryDelay, searchOrderUM, searchOrderKM, parentImportsHashTable);
            if (rawImports.Exception != 0)
            {
                HandleImportExceptions(rawImports);
                module.OtherErrorsPresent = true;
            }
        }

        //
        // Process .NET assembly references.
        // This feature is experimental and not production ready.
        //
        module.IsDotNetModule = module.ModuleData.ImageDotNet == 1;
        if (module.IsDotNetModule && EnableExperimentalFeatures)
        {
            ProcessNetAssemblies(module);
        }
    }
}
