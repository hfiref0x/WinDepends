/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CDEPENDSANALYSISSERVICE.CS
*
*  VERSION:     1.00
*
*  DATE:        02 Oct 2026
*
*  CDepends analysis service.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Reflection.PortableExecutable;

namespace WinDepends;


internal sealed class CDependsAnalysisService
{
    private sealed record CDependsTraversalObservation(
        CModule Module,
        CModule? ParentModule,
        int Depth,
        bool WasAccepted);

    private sealed record CDependsModelDuplicateObservation(
        CModule Module,
        int OriginalInstanceId);

    private sealed class CDependsPopulationMetrics
    {
        private readonly List<CDependsTraversalObservation> _observations = [];
        public int ProcessedModuleCount { get; private set; }

        public int AcceptedModuleCount { get; private set; }
        public IReadOnlyList<CDependsTraversalObservation> Observations => _observations;

        public TreeNode? ProcessModule(
            CModule module,
            TreeNode? parentNode,
            Func<CModule, TreeNode?, TreeNode?> processModule)
        {
            CModule? parentModule;
            int traversalDepth;
            TreeNode? node;

            ProcessedModuleCount++;
            parentModule = parentNode?.Tag as CModule;
            traversalDepth = parentNode == null
                ? 0
                : parentNode.Level + 1;

            node = processModule(
                module,
                parentNode);

            _observations.Add(new CDependsTraversalObservation(
                module,
                parentModule,
                traversalDepth,
                node != null));

            if (node != null)
            {
                AcceptedModuleCount++;
            }

            return node;
        }
    }

    private sealed class CDependsModelTraversalMetrics
    {
        private readonly int _maximumDepth;
        private readonly List<CModule> _canonicalModules = [];
        private readonly List<CDependsModelDuplicateObservation> _duplicateObservations = [];
        private readonly List<CDependsTraversalObservation> _observations = [];

        public int ProcessedModuleCount { get; private set; }

        public int AcceptedModuleCount { get; private set; }

        public IReadOnlyList<CDependsTraversalObservation> Observations => _observations;
        public IReadOnlyList<CDependsModelDuplicateObservation> DuplicateObservations => _duplicateObservations;

        public CDependsModelTraversalMetrics(int maximumDepth)
        {
            _maximumDepth = maximumDepth;
        }

        public CDependsModelTraversalVisitResult ProcessModule(
            CModule module,
            CModule? parentModule,
            int depth,
            CFileOpenSettings fileOpenSettings,
            CDependsAnalysisContext context)
        {
            CModule originalModule;
            int originalInstanceId;
            bool shouldTraverseDependents;

            ProcessedModuleCount++;

            shouldTraverseDependents = parentModule == null ||
                parentModule.Depth <= _maximumDepth;

            _observations.Add(new CDependsTraversalObservation(
                module,
                parentModule,
                depth,
                shouldTraverseDependents));

            if (!shouldTraverseDependents)
                return CDependsModelTraversalVisitResult.Stop();

            originalModule = CUtils.GetModuleByHash(
                module.FileName,
                _canonicalModules);

            originalInstanceId = originalModule?.InstanceId ?? 0;

            _duplicateObservations.Add(
                new CDependsModelDuplicateObservation(
                    module,
                    originalInstanceId));

            if (originalModule == null)
            {
                _canonicalModules.Add(module);
            }

            AcceptedModuleCount++;
            return CDependsModelTraversalVisitResult.Continue();
        }
    }

    private sealed class CDependsModelAnalysisMetrics
    {
        private readonly CDependsAnalysisService _analysisService;
        private readonly int _maximumDepth;
        private readonly List<CModule> _canonicalModules = [];

        public int ProcessedModuleCount { get; private set; }

        public int DuplicateModuleCount { get; private set; }

        public CDependsModelAnalysisMetrics(
            CDependsAnalysisService analysisService,
            int maximumDepth)
        {
            ArgumentNullException.ThrowIfNull(analysisService);

            _analysisService = analysisService;
            _maximumDepth = maximumDepth;
        }

        public CDependsModelTraversalVisitResult ProcessModule(
            CModule module,
            CModule? parentModule,
            int depth,
            CFileOpenSettings fileOpenSettings,
            CDependsAnalysisContext context)
        {
            CModule? originalModule;
            bool shouldTraverseDependents;

            ProcessedModuleCount++;

            shouldTraverseDependents = parentModule == null ||
                parentModule.Depth <= _maximumDepth;

            if (!shouldTraverseDependents)
                return CDependsModelTraversalVisitResult.Stop();

            originalModule = CUtils.GetModuleByHash(
                module.FileName,
                _canonicalModules);

            if (originalModule != null)
            {
                CDependsDuplicateModuleState.Apply(
                    module,
                    originalModule);

                if (parentModule != null &&
                    CDependsDuplicateModuleState.ShouldPropagateErrors(
                        originalModule))
                {
                    parentModule.OtherErrorsPresent = true;
                }

                module.Depth = depth;
                DuplicateModuleCount++;

                return CDependsModelTraversalVisitResult.Continue();
            }

            _analysisService.ProcessModule(
                module,
                fileOpenSettings,
                parentModule == null,
                context);

            module.Depth = depth;
            _canonicalModules.Add(module);

            return CDependsModelTraversalVisitResult.Continue();
        }
    }

    private static readonly Action<CDependsAnalysisProgress> s_ignoreProgress = _ => { };
    private readonly CCoreClient _coreClient;

    public CDependsAnalysisService(CCoreClient coreClient)
    {
        ArgumentNullException.ThrowIfNull(coreClient);

        _coreClient = coreClient;
    }

    private static void ReportModuleProgress(
        CDependsAnalysisContext context,
        CDependsAnalysisProgressStage stage,
        CModule module)
    {
        context.ReportProgress?.Invoke(new CDependsAnalysisProgress(
            stage,
            module.FileName,
            -1));
    }

    private CDependsAnalysisScope BeginAnalysis(
        string rootFileName,
        CDependsAnalysisContext context)
    {
        CActCtxHelper activationContext;

        ArgumentException.ThrowIfNullOrWhiteSpace(rootFileName);
        ArgumentNullException.ThrowIfNull(context);

        CPathResolver.Initialized = false;

        activationContext = new CActCtxHelper(rootFileName);
        CPathResolver.ActCtxHelper = activationContext;

        return new CDependsAnalysisScope(activationContext, context);
    }

    public CDependsPopulationResult PopulateLiveAnalysis(
        CDependsLiveAnalysisRequest request)
    {
        Action<CDependsAnalysisProgress> reportProgress;
        CDependsPopulationMetrics metrics;
        TreeNode? rootNode;
        ArgumentNullException.ThrowIfNull(request);
        reportProgress = request.Context.ReportProgress ?? s_ignoreProgress;
        metrics = new CDependsPopulationMetrics();

        using (BeginAnalysis(request.RootFileName, request.Context))
        {
            rootNode = PopulateDependencyTree(
                request.RootModule,
                request.FileOpenSettings,
                request.ProcessModule,
                request.Context,
                reportProgress,
                metrics);
        }

        ValidateModelTraversalParity(request, metrics);
        return new CDependsPopulationResult(rootNode, metrics.ProcessedModuleCount, metrics.AcceptedModuleCount);
    }

    public CDependsModelAnalysisResult AnalyzeModel(
        CDependsModelAnalysisRequest request)
    {
        Action<CDependsAnalysisProgress> reportProgress;
        CDependsModelAnalysisMetrics metrics;

        ArgumentNullException.ThrowIfNull(request);

        reportProgress = request.Context.ReportProgress ?? s_ignoreProgress;

        metrics = new CDependsModelAnalysisMetrics(
            this,
            request.Context.Configuration.ModuleNodeDepthMax);

        using (BeginAnalysis(
            request.RootFileName,
            request.Context))
        {
            TraverseModelCore(
                request.Depends.RootModule,
                request.FileOpenSettings,
                request.Context,
                metrics.ProcessModule,
                reportProgress);
        }

        return new CDependsModelAnalysisResult(
            request.Depends,
            metrics.ProcessedModuleCount,
            metrics.DuplicateModuleCount);
    }

    public void ProcessModule(
        CModule module,
        CFileOpenSettings fileOpenSettings,
        bool currentModuleIsRoot,
        CDependsAnalysisContext context)
    {
        CFileOpenSettings effectiveSettings;
        ModuleOpenStatus openStatus;

        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(fileOpenSettings);
        ArgumentNullException.ThrowIfNull(context);

        effectiveSettings = new CFileOpenSettings(fileOpenSettings);

        //
        // If this is a dependency and propagation is disabled, reset to defaults.
        //
        if (!currentModuleIsRoot &&
            !fileOpenSettings.PropagateSettingsOnDependencies)
        {
            effectiveSettings.ProcessRelocsForImage = false;
            effectiveSettings.UseStats = false;
            effectiveSettings.UseCustomImageBase = false;
            effectiveSettings.CustomImageBase = 0;
        }

        module.InstanceId = module.GetHashCode();

        ReportModuleProgress(
            context,
            CDependsAnalysisProgressStage.OpeningModule,
            module);

        openStatus = _coreClient.OpenModule(
            ref module,
            effectiveSettings);

        if (openStatus != ModuleOpenStatus.Okay)
        {
            ReportModuleProgress(
                context,
                CDependsAnalysisProgressStage.ModuleProcessingFailed,
                module);
        }

        HandleModuleOpenStatus(
            module,
            openStatus,
            effectiveSettings,
            currentModuleIsRoot,
            context);
    }

    private TreeNode? PopulateDependencyTree(
        CModule rootModule,
        CFileOpenSettings fileOpenSettings,
        CDependsModuleProcessor processModule,
        CDependsAnalysisContext context,
        Action<CDependsAnalysisProgress> reportProgress,
        CDependsPopulationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(rootModule);
        ArgumentNullException.ThrowIfNull(fileOpenSettings);
        ArgumentNullException.ThrowIfNull(processModule);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reportProgress);
        ArgumentNullException.ThrowIfNull(metrics);

        return PopulateTreeCore(
            rootModule,
            (module, parentNode) => CDependsTraversalVisitResult.FromNode(
                metrics.ProcessModule(
                    module,
                    parentNode,
                    (currentModule, currentParentNode) => processModule(
                        currentModule,
                        currentParentNode,
                        fileOpenSettings,
                        context))),
            reportProgress);
    }

    public CDependsPopulationResult PopulateSessionTree(
        CDependsSessionPopulationRequest request)
    {
        CDependsPopulationMetrics metrics;
        TreeNode? rootNode;
        ArgumentNullException.ThrowIfNull(request);

        metrics = new CDependsPopulationMetrics();

        rootNode = PopulateTreeCore(
            request.RootModule,
                (module, parentNode) => CDependsTraversalVisitResult.FromNode(
                    metrics.ProcessModule(
                        module,
                        parentNode,
                        (currentModule, currentParentNode) => request.ProcessModule(
                            currentModule,
                            currentParentNode))),
             request.ReportProgress);

        return new CDependsPopulationResult(rootNode, metrics.ProcessedModuleCount, metrics.AcceptedModuleCount);
    }

    private static TreeNode? PopulateTreeCore(
       CModule rootModule,
       Func<CModule, TreeNode?, CDependsTraversalVisitResult> processModule,
       Action<CDependsAnalysisProgress> reportProgress)
    {
        List<(CModule Module, TreeNode Node)> baseModules = [];
        CDependsTraversalVisitResult rootVisit;
        TreeNode? rootNode;

        reportProgress(new CDependsAnalysisProgress(
            CDependsAnalysisProgressStage.Populating,
            rootModule.FileName,
            0));

        rootVisit = processModule(
            rootModule,
            null);

        rootNode = rootVisit.Node;

        if (rootNode == null || !rootVisit.ShouldTraverseDependents)
            return rootNode;

        foreach (CModule importModule in rootModule.Dependents)
        {
            CDependsTraversalVisitResult importVisit;
            TreeNode? importNode;

            reportProgress(new CDependsAnalysisProgress(
                CDependsAnalysisProgressStage.Populating,
                importModule.FileName,
                1));

            importVisit = processModule(
                importModule,
                rootNode);

            importNode = importVisit.Node;

            if (importNode != null &&
                importVisit.ShouldTraverseDependents)
            {
                baseModules.Add((
                    importModule,
                    importNode));
            }
        }

        foreach ((CModule module, TreeNode node) in baseModules)
        {
            foreach (CModule dependent in module.Dependents)
            {
                PopulateDependentModulesCore(
                    dependent,
                    node,
                    processModule,
                    reportProgress,
                    2);
            }
        }

        return rootNode;
    }

    private static void PopulateDependentModulesCore(
        CModule module,
        TreeNode parentNode,
        Func<CModule, TreeNode?, CDependsTraversalVisitResult> processModule,
        Action<CDependsAnalysisProgress> reportProgress,
        int depth)
    {
        CDependsTraversalVisitResult moduleVisit;
        TreeNode? treeNode;

        reportProgress(new CDependsAnalysisProgress(
            CDependsAnalysisProgressStage.Populating,
            module.FileName,
            depth));

        moduleVisit = processModule(
            module,
            parentNode);

        treeNode = moduleVisit.Node;

        if (treeNode == null ||
            !moduleVisit.ShouldTraverseDependents)
        {
            return;
        }

        foreach (CModule dependentModule in module.Dependents)
        {
            PopulateDependentModulesCore(
                dependentModule,
                treeNode,
                processModule,
                reportProgress,
                depth + 1);
        }
    }

    private static void TraverseModelCore(
        CModule rootModule,
        CFileOpenSettings fileOpenSettings,
        CDependsAnalysisContext context,
        CDependsModelModuleProcessor processModule,
        Action<CDependsAnalysisProgress> reportProgress)
    {
        List<CModule> baseModules = [];
        CDependsModelTraversalVisitResult rootVisit;

        ArgumentNullException.ThrowIfNull(rootModule);
        ArgumentNullException.ThrowIfNull(fileOpenSettings);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(processModule);
        ArgumentNullException.ThrowIfNull(reportProgress);

        reportProgress(new CDependsAnalysisProgress(
            CDependsAnalysisProgressStage.Populating,
            rootModule.FileName,
            0));

        rootVisit = processModule(
            rootModule,
            null,
            0,
            fileOpenSettings,
            context);

        if (!rootVisit.ShouldTraverseDependents)
            return;

        foreach (CModule importModule in rootModule.Dependents)
        {
            CDependsModelTraversalVisitResult importVisit;

            reportProgress(new CDependsAnalysisProgress(
                CDependsAnalysisProgressStage.Populating,
                importModule.FileName,
                1));

            importVisit = processModule(
                importModule,
                rootModule,
                1,
                fileOpenSettings,
                context);

            if (importVisit.ShouldTraverseDependents)
            {
                baseModules.Add(importModule);
            }
        }

        foreach (CModule baseModule in baseModules)
        {
            foreach (CModule dependentModule in baseModule.Dependents)
            {
                TraverseModelDependentModulesCore(
                    dependentModule,
                    baseModule,
                    2,
                    fileOpenSettings,
                    context,
                    processModule,
                    reportProgress);
            }
        }
    }

    private static void TraverseModelDependentModulesCore(
        CModule module,
        CModule parentModule,
        int depth,
        CFileOpenSettings fileOpenSettings,
        CDependsAnalysisContext context,
        CDependsModelModuleProcessor processModule,
        Action<CDependsAnalysisProgress> reportProgress)
    {
        CDependsModelTraversalVisitResult moduleVisit;

        reportProgress(new CDependsAnalysisProgress(
            CDependsAnalysisProgressStage.Populating,
            module.FileName,
            depth));

        moduleVisit = processModule(
            module,
            parentModule,
            depth,
            fileOpenSettings,
            context);

        if (!moduleVisit.ShouldTraverseDependents)
            return;

        foreach (CModule dependentModule in module.Dependents)
        {
            TraverseModelDependentModulesCore(
                dependentModule,
                module,
                depth + 1,
                fileOpenSettings,
                context,
                processModule,
                reportProgress);
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void ValidateModelTraversalParity(
        CDependsLiveAnalysisRequest request,
        CDependsPopulationMetrics populationMetrics)
    {
        CDependsModelTraversalMetrics modelMetrics = new(
            request.Context.Configuration.ModuleNodeDepthMax);

        TraverseModelCore(
            request.RootModule,
            request.FileOpenSettings,
            request.Context,
            modelMetrics.ProcessModule,
            s_ignoreProgress);

        if (!TryFindTraversalMismatch(
            populationMetrics.Observations,
            modelMetrics.Observations,
            out string mismatchMessage))
        {
            if (!TryFindModelDuplicateMismatch(
                modelMetrics.DuplicateObservations,
                out mismatchMessage))
            {
                return;
            }
        }

        System.Diagnostics.Debug.WriteLine(
            "[WinDepends][ModelTraversal] Live/model traversal mismatch: " +
            $"live processed={populationMetrics.ProcessedModuleCount}, " +
            $"live accepted={populationMetrics.AcceptedModuleCount}, " +
            $"model processed={modelMetrics.ProcessedModuleCount}, " +
            $"model accepted={modelMetrics.AcceptedModuleCount}. " +
            mismatchMessage);
    }

    private static bool TryFindModelDuplicateMismatch(
        IReadOnlyList<CDependsModelDuplicateObservation> observations,
        out string message)
    {
        foreach (CDependsModelDuplicateObservation observation in observations)
        {
            int liveOriginalInstanceId =
                observation.Module.OriginalInstanceId;

            if (liveOriginalInstanceId == 0 &&
                observation.OriginalInstanceId == 0)
            {
                continue;
            }

            if (liveOriginalInstanceId == observation.OriginalInstanceId)
                continue;

            message =
                $"Duplicate classification differs for " +
                $"\"{observation.Module.FileName}\"; " +
                $"live original={liveOriginalInstanceId}, " +
                $"model original={observation.OriginalInstanceId}.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static bool TryFindTraversalMismatch(
        IReadOnlyList<CDependsTraversalObservation> liveObservations,
        IReadOnlyList<CDependsTraversalObservation> modelObservations,
        out string message)
    {
        int comparisonCount = Math.Min(
            liveObservations.Count,
            modelObservations.Count);

        for (int index = 0; index < comparisonCount; index++)
        {
            CDependsTraversalObservation liveObservation =
                liveObservations[index];

            CDependsTraversalObservation modelObservation =
                modelObservations[index];

            if (!ReferenceEquals(
                    liveObservation.Module,
                    modelObservation.Module))
            {
                message =
                    $"Visit {index}: module differs; " +
                    $"live=\"{liveObservation.Module.FileName}\", " +
                    $"model=\"{modelObservation.Module.FileName}\".";
                return true;
            }

            if (!ReferenceEquals(
                    liveObservation.ParentModule,
                    modelObservation.ParentModule))
            {
                message =
                    $"Visit {index}: parent differs for " +
                    $"\"{liveObservation.Module.FileName}\"; " +
                    $"live parent=\"{liveObservation.ParentModule?.FileName ?? "<root>"}\", " +
                    $"model parent=\"{modelObservation.ParentModule?.FileName ?? "<root>"}\".";
                return true;
            }

            if (liveObservation.Depth != modelObservation.Depth)
            {
                message =
                    $"Visit {index}: depth differs for " +
                    $"\"{liveObservation.Module.FileName}\"; " +
                    $"live={liveObservation.Depth}, " +
                    $"model={modelObservation.Depth}.";
                return true;
            }

            if (liveObservation.WasAccepted != modelObservation.WasAccepted)
            {
                message =
                    $"Visit {index}: acceptance differs for " +
                    $"\"{liveObservation.Module.FileName}\"; " +
                    $"live={liveObservation.WasAccepted}, " +
                    $"model={modelObservation.WasAccepted}.";
                return true;
            }
        }

        if (liveObservations.Count != modelObservations.Count)
        {
            message =
                $"Observation count differs; " +
                $"live={liveObservations.Count}, " +
                $"model={modelObservations.Count}.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    public void HandleModuleOpenStatus(
        CModule module,
        ModuleOpenStatus openStatus,
        CFileOpenSettings settings,
        bool currentModuleIsRoot,
        CDependsAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(context);

        switch (openStatus)
        {
            case ModuleOpenStatus.Okay:

                HandleOpenedModule(
                    module,
                    settings,
                    currentModuleIsRoot,
                    context);
                break;

            case ModuleOpenStatus.ErrorUnspecified:
                context.AddLogMessage(
                    $"Module \"{module.FileName}\" analysis failed.",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorSendCommand:
                context.AddLogMessage(
                    $"Send command has failed for module \"{module.FileName}\".",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorReceivedDataInvalid:
                context.AddLogMessage(
                    $"Received invalid data for module \"{module.FileName}\".",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorFileNotMapped:
                context.AddLogMessage(
                    $"Server failed to map input module \"{module.FileName}\".",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorCannotReadFileHeaders:
                context.AddLogMessage(
                    $"Server failed to read headers of module \"{module.FileName}\".",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorInvalidHeadersOrSignatures:
                HandleInvalidHeadersOrSignatures(module, context);
                break;

            case ModuleOpenStatus.ErrorFileNotFound:
                HandleFileNotFound(module, context);
                break;
        }
    }

    private void HandleOpenedModule(
        CModule module,
        CFileOpenSettings settings,
        bool currentModuleIsRoot,
        CDependsAnalysisContext context)
    {
        CCoreCallStats? stats = null;

        ReportModuleProgress(context, CDependsAnalysisProgressStage.ReadingHeaders, module);

        module.IsProcessed = _coreClient.GetModuleHeadersInformation(module);

        //
        // If this is root module, setup resolver.
        //
        if (currentModuleIsRoot)
        {
            CPathResolver.QueryFileInformation(module);
        }

        ReportModuleProgress(context, CDependsAnalysisProgressStage.ReadingImportsAndExports, module);

        _coreClient.GetModuleImportExportInformation(
            module,
            context.Configuration.SearchOrderListUM,
            context.Configuration.SearchOrderListKM,
            context.ParentImportsHashTable,
            settings.EnableExperimentalFeatures,
            settings.ExpandForwarders);

        //
        // Collect forwarders if exists.
        // Has local settings priority over global.
        //
        if (settings.ExpandForwarders)
        {
            ReportModuleProgress(context, CDependsAnalysisProgressStage.ExpandingForwarders, module);

            _coreClient.ExpandAllForwarderModules(
                module,
                context.Configuration.SearchOrderListUM,
                context.Configuration.SearchOrderListKM,
                context.ParentImportsHashTable);

            // Validate forwarded exports after expansion.
            _coreClient.ValidateForwardedExports(module);
        }

        if (settings.UseStats)
        {
            ReportModuleProgress(context, CDependsAnalysisProgressStage.ReadingStatistics, module);
            stats = _coreClient.GetCoreCallStats();
        }

        _coreClient.CloseModule();

        //
        // Display statistics.
        //
        if (settings.UseStats && stats != null)
        {
            LogModuleStats(stats, module.FileName, context);
        }

        if (module.ExportContainErrors)
        {
            context.AddLogMessage(
                $"Module \"{module.FileName}\" contains export errors.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }

        // Add warning for modules with forwarding issues.
        if (module.OtherErrorsPresent &&
            module.ForwarderEntries?.Count > 0)
        {
            context.AddLogMessage(
                $"Module \"{Path.GetFileName(module.FileName)}\" has unresolved forwarded exports.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }

        bool isCpuMismatch = IsCpuMismatchForAnalysis(
            module,
            context.RootModule);

        if (isCpuMismatch)
        {
            module.OtherErrorsPresent = true;

            context.AddLogMessage(
                $"Module \"{module.FileName}\" with different CPU type was found.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }

        // Skip this message for kernel modules, dotnet files, and when relocation processing is disabled.
        if (module.ModuleData.ImageFixed != 0 &&
            !module.IsKernelModule &&
            module.ModuleData.ImageDotNet != 1 &&
            settings.ProcessRelocsForImage)
        {
            module.OtherErrorsPresent = true;

            context.AddLogMessage(
                $"Module \"{Path.GetFileName(module.FileName)}\" has no relocations.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }

        if (!module.IsProcessed)
        {
            context.AddLogMessage(
                $"Module \"{module.FileName}\" was not fully processed.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }
    }

    private static void HandleInvalidHeadersOrSignatures(
        CModule module,
        CDependsAnalysisContext context)
    {
        if (module.IsDelayLoad)
        {
            context.AddLogMessage(
                $"Delay-load module \"{module.FileName}\" has invalid headers or signatures.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }
        else
        {
            context.AddLogMessage(
                $"Module \"{module.FileName}\" has invalid headers or signatures.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }
    }

    private static void HandleFileNotFound(
        CModule module,
        CDependsAnalysisContext context)
    {
        bool isExtensionApiSet = module.IsApiSetContract &&
            module.RawFileName.StartsWith(
                "EXT-",
                StringComparison.OrdinalIgnoreCase);

        string messageText;
        LogMessageType messageType = isExtensionApiSet
            ? LogMessageType.Information
            : LogMessageType.ErrorOrWarning;

        if (module.IsDelayLoad)
        {
            messageText = isExtensionApiSet
                ? $"Delay-load extension apiset module \"{module.FileName}\" was not found."
                : $"Delay-load dependency module \"{module.FileName}\" was not found.";
        }
        else
        {
            messageText = isExtensionApiSet
                ? $"Extension apiset module \"{module.FileName}\" was not found."
                : $"Required implicit or forwarded dependency \"{module.FileName}\" was not found.";
        }

        context.AddLogMessage(
            messageText,
            messageType,
            null,
            true,
            true,
            module);
    }

    private static bool IsCpuMismatchForAnalysis(
        CModule module,
        CModule rootModule)
    {
        bool isRootImageDotNet;

        if (module == null || rootModule == null)
            return false;

        isRootImageDotNet = rootModule.ModuleData.ImageDotNet == 1;
        if (isRootImageDotNet)
            return false;

        if (module.ModuleData.Machine == rootModule.ModuleData.Machine)
            return false;

        if (IsManagedAnyCpuLike(module))
            return false;

        return true;
    }

    private static bool IsManagedAnyCpuLike(CModule module)
    {
        CorFlags flags;
        Machine machine;
        bool ilOnly;
        bool requires32Bit;

        if (module == null)
            return false;

        if (module.ModuleData.ImageDotNet != 1)
            return false;

        try
        {
            using FileStream fileStream = new(
                module.FileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            using PEReader peReader = new(fileStream);

            var corHeader = peReader.PEHeaders?.CorHeader;
            if (corHeader == null)
                return false;

            flags = corHeader.Flags;
            ilOnly = (flags & CorFlags.ILOnly) != 0;
            requires32Bit = (flags & CorFlags.Requires32Bit) != 0;
            machine = peReader.PEHeaders.CoffHeader.Machine;

            return machine == Machine.I386 &&
                ilOnly &&
                !requires32Bit;
        }
        catch
        {
            return false;
        }
    }

    private static void LogModuleStats(
        CCoreCallStats stats,
        string moduleFileName,
        CDependsAnalysisContext context)
    {
        string statsData;

        if (stats == null)
            return;

        statsData = $"[STATS {Path.GetFileName(moduleFileName)}] Received: " +
            $"{FormatByteSize(stats.TotalBytesSent)}, " +
            $"\"send\" calls: {stats.TotalSendCalls}, " +
            $"\"send\" time spent (\u00B5s): {stats.TotalTimeSpent}";

        context.AddLogMessage(
            statsData,
            LogMessageType.ContentDefined,
            Color.Purple,
            true,
            false);
    }

    private static string FormatByteSize(ulong bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024 * 1024)} MB",
            >= 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes} byte"
        };
    }
}
