/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CDEPENDSANALYSISSERVICE.CS
*
*  VERSION:     1.00
*
*  DATE:        10 Oct 2026
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

/// <summary>
/// Drives dependency analysis: walks a module's dependency graph, opens and
/// reads each module through the core client, and logs diagnostics for modules
/// that are missing, invalid or inconsistent.
/// </summary>
/// <remarks>
/// <para>
/// Three entry points build on the same traversal order:
/// <see cref="PopulateLiveAnalysis"/> analyzes modules and builds the tree at the
/// same time, <see cref="PopulateSessionTree"/> builds the tree from modules that
/// are already loaded, and <see cref="AnalyzeModel"/> analyzes modules into a
/// <see cref="CDepends"/> model without building a tree.
/// </para>
/// <para>
/// <b>Traversal order.</b> The root module is visited first (depth 0), followed
/// by all of its direct dependents (depth 1). Only then does the traversal
/// descend, one depth-1 module at a time, depth-first through that module's
/// dependents (depth 2 and deeper). Because duplicates are detected by "first
/// module seen with this file name", this order determines which instance of a
/// module becomes the canonical one.
/// </para>
/// <para>
/// <b>Thread safety.</b> Analysis entry points that open files
/// (<see cref="PopulateLiveAnalysis"/> and <see cref="AnalyzeModel"/>) change
/// process-wide state on <see cref="CPathResolver"/> for the duration of the
/// run, so analyses must not run concurrently.
/// </para>
/// </remarks>
internal sealed class CDependsAnalysisService
{
    /// <summary>
    /// A single module visit recorded during a traversal, used to compare the
    /// live and model traversals.
    /// </summary>
    /// <param name="Module">The visited module.</param>
    /// <param name="ParentModule">The module that depends on it, or <see langword="null"/> for the root.</param>
    /// <param name="Depth">The depth of the module in the traversal (root is 0).</param>
    /// <param name="WasAccepted">
    /// Whether the visit was accepted. For the live traversal this means a tree
    /// node was produced; for the model traversal it means the module's
    /// dependents are traversed.
    /// </param>
    private sealed record CDependsTraversalObservation(
        CModule Module,
        CModule? ParentModule,
        int Depth,
        bool WasAccepted);

    /// <summary>
    /// A duplicate classification recorded by the model traversal.
    /// </summary>
    /// <param name="Module">The visited module.</param>
    /// <param name="OriginalInstanceId">
    /// The instance ID of the earlier module with the same file name, or 0 if
    /// the module is the first occurrence (canonical).
    /// </param>
    private sealed record CDependsModelDuplicateObservation(
        CModule Module,
        int OriginalInstanceId);

    /// <summary>
    /// Wraps the per-module processor of a tree population run, counting modules
    /// and recording each visit.
    /// </summary>
    private sealed class CDependsPopulationMetrics
    {
        private readonly List<CDependsTraversalObservation> _observations = [];

        /// <summary>Gets the number of modules passed to <see cref="ProcessModule"/>.</summary>
        public int ProcessedModuleCount { get; private set; }

        /// <summary>Gets the number of modules for which a tree node was produced.</summary>
        public int AcceptedModuleCount { get; private set; }

        /// <summary>Gets the visits recorded so far, in visit order.</summary>
        public IReadOnlyList<CDependsTraversalObservation> Observations => _observations;

        /// <summary>
        /// Processes a module through <paramref name="processModule"/> and records
        /// the visit.
        /// </summary>
        /// <param name="module">The module to process.</param>
        /// <param name="parentNode">
        /// The parent tree node, or <see langword="null"/> for the root. The parent
        /// module is read from the node's <c>Tag</c> and the depth is derived from
        /// the node's <c>Level</c>.
        /// </param>
        /// <param name="processModule">The processor that creates the node for the module.</param>
        /// <returns>The node produced by <paramref name="processModule"/>, or <see langword="null"/>.</returns>
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

    /// <summary>
    /// Model traversal processor that performs no analysis and only records how
    /// the model traversal behaves, for comparison with the live traversal in
    /// <see cref="ValidateModelTraversalParity"/>.
    /// </summary>
    private sealed class CDependsModelTraversalMetrics
    {
        private readonly int _maximumDepth;
        private readonly List<CModule> _canonicalModules = [];
        private readonly List<CDependsModelDuplicateObservation> _duplicateObservations = [];
        private readonly List<CDependsTraversalObservation> _observations = [];

        /// <summary>Gets the number of modules visited, including those skipped by the depth limit.</summary>
        public int ProcessedModuleCount { get; private set; }

        /// <summary>Gets the number of modules whose dependents are traversed.</summary>
        public int AcceptedModuleCount { get; private set; }

        /// <summary>Gets the visits recorded so far, in visit order.</summary>
        public IReadOnlyList<CDependsTraversalObservation> Observations => _observations;

        /// <summary>Gets the duplicate classification recorded for each accepted module.</summary>
        public IReadOnlyList<CDependsModelDuplicateObservation> DuplicateObservations => _duplicateObservations;

        /// <summary>
        /// Initializes a new instance of the <see cref="CDependsModelTraversalMetrics"/> class.
        /// </summary>
        /// <param name="maximumDepth">The configured maximum module node depth.</param>
        public CDependsModelTraversalMetrics(int maximumDepth)
        {
            _maximumDepth = maximumDepth;
        }

        /// <summary>
        /// Records a visit and decides whether the traversal continues.
        /// </summary>
        /// <param name="module">The visited module.</param>
        /// <param name="parentModule">The parent module, or <see langword="null"/> for the root.</param>
        /// <param name="depth">The depth of the module in the traversal.</param>
        /// <param name="fileOpenSettings">Unused; present to match <see cref="CDependsModelModuleProcessor"/>.</param>
        /// <param name="context">Unused; present to match <see cref="CDependsModelModuleProcessor"/>.</param>
        /// <returns>
        /// <see cref="CDependsModelTraversalVisitResult.Stop"/> if the module is
        /// beyond the depth limit; otherwise <see cref="CDependsModelTraversalVisitResult.Continue"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// A non-root module is accepted only when its parent's
        /// <see cref="CModule.Depth"/> is less than or equal to the maximum depth.
        /// The parent's stored depth is used, not the <paramref name="depth"/>
        /// argument, so it must already have been assigned (by the live pass) when
        /// this runs.
        /// </para>
        /// <para>
        /// Accepted modules are classified as canonical (first occurrence of the
        /// file name) or duplicate (an earlier module with that file name exists).
        /// </para>
        /// </remarks>
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

    /// <summary>
    /// Model traversal processor used by <see cref="AnalyzeModel"/>: analyzes each
    /// canonical module, applies the original's state to duplicates, and counts
    /// both.
    /// </summary>
    private sealed class CDependsModelAnalysisMetrics
    {
        private readonly CDependsAnalysisService _analysisService;
        private readonly int _maximumDepth;
        private readonly List<CModule> _canonicalModules = [];

        /// <summary>Gets the number of modules visited, including those skipped by the depth limit.</summary>
        public int ProcessedModuleCount { get; private set; }

        /// <summary>Gets the number of modules identified as duplicates.</summary>
        public int DuplicateModuleCount { get; private set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="CDependsModelAnalysisMetrics"/> class.
        /// </summary>
        /// <param name="analysisService">The service used to analyze canonical modules.</param>
        /// <param name="maximumDepth">The configured maximum module node depth.</param>
        /// <exception cref="ArgumentNullException"><paramref name="analysisService"/> is <see langword="null"/>.</exception>
        public CDependsModelAnalysisMetrics(
            CDependsAnalysisService analysisService,
            int maximumDepth)
        {
            ArgumentNullException.ThrowIfNull(analysisService);

            _analysisService = analysisService;
            _maximumDepth = maximumDepth;
        }

        /// <summary>
        /// Analyzes or classifies a module and decides whether the traversal continues.
        /// </summary>
        /// <param name="module">The visited module.</param>
        /// <param name="parentModule">The parent module, or <see langword="null"/> for the root.</param>
        /// <param name="depth">The depth of the module in the traversal.</param>
        /// <param name="fileOpenSettings">The settings used when opening the module file.</param>
        /// <param name="context">The shared analysis context.</param>
        /// <returns>
        /// <see cref="CDependsModelTraversalVisitResult.Stop"/> if the module is
        /// beyond the depth limit; otherwise <see cref="CDependsModelTraversalVisitResult.Continue"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The depth rule is the same as in <see cref="CDependsModelTraversalMetrics.ProcessModule"/>:
        /// a non-root module is processed only when its parent's
        /// <see cref="CModule.Depth"/> is less than or equal to the maximum depth.
        /// A module skipped this way is counted as processed but is not analyzed
        /// and does not have its depth assigned.
        /// </para>
        /// <para>
        /// For a duplicate, the original's state is applied to it; if that state
        /// should propagate errors, the parent is flagged with
        /// <c>OtherErrorsPresent</c>. A canonical module is analyzed through
        /// <see cref="ProcessModule(CModule, CFileOpenSettings, bool, CDependsAnalysisContext)"/>
        /// and remembered as canonical.
        /// </para>
        /// <para>
        /// For both, a forwarded module whose file was not found flags the parent
        /// with <c>OtherErrorsPresent</c>, and the module's
        /// <see cref="CModule.Depth"/> is set to <paramref name="depth"/>.
        /// </para>
        /// </remarks>
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

                DuplicateModuleCount++;

            }
            else
            {
                _analysisService.ProcessModule(
                    module,
                    fileOpenSettings,
                    parentModule == null,
                    context);

                _canonicalModules.Add(module);
            }

            if (parentModule != null &&
                module.IsForward &&
                module.FileNotFound)
            {
                parentModule.OtherErrorsPresent = true;
            }

            module.Depth = depth;
            return CDependsModelTraversalVisitResult.Continue();
        }
    }

    /// <summary>
    /// Progress callback used when the caller does not supply one.
    /// </summary>
    private static readonly Action<CDependsAnalysisProgress> s_ignoreProgress = _ => { };
    private readonly CCoreClient _coreClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="CDependsAnalysisService"/> class.
    /// </summary>
    /// <param name="coreClient">The client used to open and read modules.</param>
    /// <exception cref="ArgumentNullException"><paramref name="coreClient"/> is <see langword="null"/>.</exception>
    public CDependsAnalysisService(CCoreClient coreClient)
    {
        ArgumentNullException.ThrowIfNull(coreClient);

        _coreClient = coreClient;
    }

    /// <summary>
    /// Reports a per-module progress stage through the context's progress callback, if any.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="stage">The stage reached.</param>
    /// <param name="module">The module being processed.</param>
    /// <remarks>
    /// The reported depth is always -1, because the tree depth is not known at
    /// this level. Tree depth is reported by the traversal itself.
    /// </remarks>
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

    /// <summary>
    /// Prepares path resolution for an analysis and returns a scope that undoes it.
    /// </summary>
    /// <param name="rootFileName">The file name of the root module.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>A scope that owns the activation context; dispose it when the analysis ends.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="rootFileName"/> is empty or white space.</exception>
    /// <remarks>
    /// Marks <see cref="CPathResolver"/> as uninitialized and installs a new
    /// activation context for the root file as <see cref="CPathResolver.ActCtxHelper"/>.
    /// This is process-wide state, so only one analysis can be active at a time.
    /// </remarks>
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

    /// <summary>
    /// Analyzes the root module and its dependencies and builds the dependency tree in one pass.
    /// </summary>
    /// <param name="request">The analysis request.</param>
    /// <returns>
    /// The population result, containing the root node (or <see langword="null"/>
    /// if the root was rejected) and the processed/accepted module counts.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The tree is built inside an analysis scope (see <see cref="BeginAnalysis"/>);
    /// the scope is disposed before this method returns.
    /// </para>
    /// <para>
    /// In DEBUG builds, the finished tree is then re-traversed with the model
    /// traversal and compared against the live traversal
    /// (see <see cref="ValidateModelTraversalParity"/>). Mismatches are written
    /// to the debug output only.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Analyzes the model's root module and its dependencies, updating the modules
    /// in place without building a tree.
    /// </summary>
    /// <param name="request">The analysis request.</param>
    /// <returns>The result with the processed and duplicate module counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Modules beyond the configured maximum depth
    /// (<c>Configuration.ModuleNodeDepthMax</c>) are not analyzed. The traversal
    /// runs inside an analysis scope that is disposed before this method returns.
    /// </remarks>
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

    /// <summary>
    /// Opens a single module through the core client, reads its information and
    /// logs any problems found.
    /// </summary>
    /// <param name="module">The module to process.</param>
    /// <param name="fileOpenSettings">The file open settings requested by the caller.</param>
    /// <param name="currentModuleIsRoot"><see langword="true"/> if the module is the root of the analysis.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="module"/>, <paramref name="fileOpenSettings"/> or
    /// <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The settings are copied before use. For a dependency (not the root) when
    /// <c>PropagateSettingsOnDependencies</c> is off, the copy has relocation
    /// processing, statistics and the custom image base reset to their defaults.
    /// </para>
    /// <para>
    /// The module's <c>InstanceId</c> is set from its hash code, progress is
    /// reported, and the module is opened. If opening fails, a
    /// <see cref="CDependsAnalysisProgressStage.ModuleProcessingFailed"/> progress
    /// notification is reported. In every case the status is then passed to
    /// <see cref="HandleModuleOpenStatus"/>.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Builds the dependency tree for a live analysis, routing each module through
    /// <paramref name="metrics"/> so visits are counted and recorded.
    /// </summary>
    /// <param name="rootModule">The root module.</param>
    /// <param name="fileOpenSettings">The settings used when opening module files.</param>
    /// <param name="processModule">The processor that analyzes a module and creates its tree node.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <param name="reportProgress">The progress callback.</param>
    /// <param name="metrics">The collector that counts and records visits.</param>
    /// <returns>The root node, or <see langword="null"/> if the root was rejected.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Builds the dependency tree from modules that are already loaded, for example
    /// when restoring a session.
    /// </summary>
    /// <param name="request">The population request.</param>
    /// <returns>
    /// The population result, containing the root node (or <see langword="null"/>
    /// if the root was rejected) and the processed/accepted module counts.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Unlike <see cref="PopulateLiveAnalysis"/>, no files are opened and no
    /// analysis scope is created.
    /// </remarks>
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

    /// <summary>
    /// Walks the module graph in the service's traversal order and builds tree
    /// nodes through <paramref name="processModule"/>.
    /// </summary>
    /// <param name="rootModule">The root module.</param>
    /// <param name="processModule">Creates the node for a module and says whether to descend into its dependents.</param>
    /// <param name="reportProgress">The progress callback; receives a <see cref="CDependsAnalysisProgressStage.Populating"/> notification before each visit.</param>
    /// <returns>
    /// The root node, or <see langword="null"/> if the root was rejected. If the
    /// root's visit does not allow traversal, only the root is visited.
    /// </returns>
    /// <remarks>
    /// The root is visited first (depth 0), then all of its direct dependents
    /// (depth 1). Each depth-1 module that produced a node and allows traversal is
    /// then expanded depth-first, in order, from depth 2 downward.
    /// </remarks>
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

        rootVisit = processModule(rootModule, null);
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

            importVisit = processModule(importModule, rootNode);

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

    /// <summary>
    /// Visits a module below depth 1 and recursively visits its dependents,
    /// depth-first.
    /// </summary>
    /// <param name="module">The module to visit.</param>
    /// <param name="parentNode">The tree node of the module that depends on <paramref name="module"/>.</param>
    /// <param name="processModule">Creates the node for a module and says whether to descend into its dependents.</param>
    /// <param name="reportProgress">The progress callback.</param>
    /// <param name="depth">The depth of <paramref name="module"/> in the tree.</param>
    /// <remarks>
    /// Recursion stops at a module for which no node was produced or whose visit
    /// does not allow traversal.
    /// </remarks>
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

    /// <summary>
    /// Walks the module graph in the service's traversal order, calling
    /// <paramref name="processModule"/> for each module without building a tree.
    /// </summary>
    /// <param name="rootModule">The root module.</param>
    /// <param name="fileOpenSettings">The settings passed to the processor.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <param name="processModule">Processes a module and says whether to descend into its dependents.</param>
    /// <param name="reportProgress">The progress callback; receives a <see cref="CDependsAnalysisProgressStage.Populating"/> notification before each visit.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// The order is the same as in <see cref="PopulateTreeCore"/>: root, then all
    /// direct dependents, then each accepted depth-1 module expanded depth-first.
    /// </remarks>
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

    /// <summary>
    /// Visits a module below depth 1 and recursively visits its dependents,
    /// depth-first, for model traversal.
    /// </summary>
    /// <param name="module">The module to visit.</param>
    /// <param name="parentModule">The module that depends on <paramref name="module"/>.</param>
    /// <param name="depth">The depth of <paramref name="module"/> in the traversal.</param>
    /// <param name="fileOpenSettings">The settings passed to the processor.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <param name="processModule">Processes a module and says whether to descend into its dependents.</param>
    /// <param name="reportProgress">The progress callback.</param>
    /// <remarks>
    /// Recursion stops at a module whose visit does not allow traversal.
    /// </remarks>
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


    /// <summary>
    /// Debug-only check that the model traversal visits the same modules, in the
    /// same order, as the live traversal that just built the tree.
    /// </summary>
    /// <param name="request">The live analysis request that was just executed.</param>
    /// <param name="populationMetrics">The visits recorded by the live traversal.</param>
    /// <remarks>
    /// <para>
    /// Marked <see cref="System.Diagnostics.ConditionalAttribute"/> for
    /// <c>DEBUG</c>: in other builds the call (and evaluation of its arguments)
    /// is removed by the compiler.
    /// </para>
    /// <para>
    /// The model traversal is run with a recording-only processor. It relies on
    /// state left on the modules by the live pass (<see cref="CModule.Depth"/> and
    /// <c>OriginalInstanceId</c>). The first mismatch found is written to the
    /// debug output; traversal mismatches are checked before duplicate
    /// classification mismatches. Nothing is thrown.
    /// </para>
    /// </remarks>
    [System.Diagnostics.Conditional("DEBUG")]
    private static void ValidateModelTraversalParity(
        CDependsLiveAnalysisRequest request,
        CDependsPopulationMetrics populationMetrics)
    {
        CDependsModelTraversalMetrics modelMetrics = new(request.Context.Configuration.ModuleNodeDepthMax);

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

    /// <summary>
    /// Finds the first module whose duplicate classification in the model
    /// traversal differs from the classification left by the live pass.
    /// </summary>
    /// <param name="observations">The duplicate classifications recorded by the model traversal.</param>
    /// <param name="message">A description of the first mismatch, or an empty string if none.</param>
    /// <returns><see langword="true"/> if a mismatch was found; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// The live classification is the module's own <c>OriginalInstanceId</c>.
    /// Modules that are canonical in both (both values 0) are skipped.
    /// </remarks>
    private static bool TryFindModelDuplicateMismatch(
        IReadOnlyList<CDependsModelDuplicateObservation> observations,
        out string message)
    {
        foreach (CDependsModelDuplicateObservation observation in observations)
        {
            int liveOriginalInstanceId = observation.Module.OriginalInstanceId;

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

    /// <summary>
    /// Compares the live and model visit sequences pairwise and finds the first difference.
    /// </summary>
    /// <param name="liveObservations">The visits recorded by the live traversal.</param>
    /// <param name="modelObservations">The visits recorded by the model traversal.</param>
    /// <param name="message">A description of the first mismatch, or an empty string if none.</param>
    /// <returns><see langword="true"/> if a mismatch was found; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Visits are compared by index. For each pair the module and parent are
    /// compared by reference, then depth and acceptance by value. If all compared
    /// pairs match but the sequences have different lengths, the length
    /// difference is reported.
    /// </remarks>
    private static bool TryFindTraversalMismatch(
        IReadOnlyList<CDependsTraversalObservation> liveObservations,
        IReadOnlyList<CDependsTraversalObservation> modelObservations,
        out string message)
    {
        int comparisonCount = Math.Min(liveObservations.Count, modelObservations.Count);

        for (int index = 0; index < comparisonCount; index++)
        {
            CDependsTraversalObservation liveObservation = liveObservations[index];
            CDependsTraversalObservation modelObservation = modelObservations[index];

            if (!ReferenceEquals(liveObservation.Module, modelObservation.Module))
            {
                message =
                    $"Visit {index}: module differs; " +
                    $"live=\"{liveObservation.Module.FileName}\", " +
                    $"model=\"{modelObservation.Module.FileName}\".";
                return true;
            }

            if (!ReferenceEquals(liveObservation.ParentModule, modelObservation.ParentModule))
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

    /// <summary>
    /// Acts on the result of opening a module: analyzes it if it opened, or logs
    /// the reason it did not.
    /// </summary>
    /// <param name="module">The module that was opened.</param>
    /// <param name="openStatus">The status returned by the core client.</param>
    /// <param name="settings">The effective file open settings used for the module.</param>
    /// <param name="currentModuleIsRoot"><see langword="true"/> if the module is the root of the analysis.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="module"/>, <paramref name="settings"/> or
    /// <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <see cref="ModuleOpenStatus.Okay"/> reads the module's information and
    /// runs the post-analysis checks (see <c>HandleOpenedModule</c>). Every
    /// recognized error status logs a warning or error message for the module;
    /// invalid headers and file-not-found have dedicated handlers. A status not
    /// listed in the switch is ignored.
    /// </remarks>
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

    /// <summary>
    /// Reads everything the analysis needs from a successfully opened module,
    /// closes it, and logs any problems found.
    /// </summary>
    /// <param name="module">The opened module.</param>
    /// <param name="settings">The effective file open settings used for the module.</param>
    /// <param name="currentModuleIsRoot"><see langword="true"/> if the module is the root of the analysis.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <remarks>
    /// <para>Steps, in order, each reported as a progress stage where one exists:</para>
    /// <list type="number">
    /// <item><description>Read the headers; the result sets <c>IsProcessed</c>.</description></item>
    /// <item><description>For the root module, query file information for the path resolver.</description></item>
    /// <item><description>Read imports and exports using the configured user-mode and kernel-mode search orders and the parent imports table.</description></item>
    /// <item><description>If forwarders are to be expanded, expand them and validate the forwarded exports.</description></item>
    /// <item><description>If statistics are enabled, read the core call statistics.</description></item>
    /// <item><description>Close the module, then log the statistics if collected.</description></item>
    /// <item><description>Log warnings for: export errors; unresolved forwarded exports; a CPU type that differs from the root; a missing relocation table; a module that was not fully processed.</description></item>
    /// </list>
    /// <para>
    /// The CPU-mismatch and missing-relocation checks also set the module's
    /// <c>OtherErrorsPresent</c> flag. The unresolved-forwarder warning is logged
    /// when that flag is already set and the module has forwarder entries.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Logs that a module has invalid headers or signatures, noting whether it is a delay-load dependency.
    /// </summary>
    /// <param name="module">The affected module.</param>
    /// <param name="context">The shared analysis context.</param>
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

    /// <summary>
    /// Logs that a module's file was not found, choosing the message and severity
    /// from the kind of dependency.
    /// </summary>
    /// <param name="module">The affected module.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <remarks>
    /// An API set contract whose raw file name starts with <c>EXT-</c>
    /// (compared case-insensitively) is an extension API set and is logged as
    /// <see cref="LogMessageType.Information"/>, because such contracts are not
    /// necessarily present. Everything else is logged as
    /// <see cref="LogMessageType.ErrorOrWarning"/>. The wording distinguishes
    /// delay-load dependencies from required implicit or forwarded dependencies.
    /// </remarks>
    private static void HandleFileNotFound(
        CModule module,
        CDependsAnalysisContext context)
    {
        bool isExtensionApiSet = module.IsApiSetContract &&
            module.RawFileName.StartsWith("EXT-", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// Determines whether a module's CPU type differs from the root module's in a
    /// way that should be reported.
    /// </summary>
    /// <param name="module">The module to check.</param>
    /// <param name="rootModule">The root module of the analysis.</param>
    /// <returns>
    /// <see langword="true"/> if the machine types differ; <see langword="false"/>
    /// if either module is <see langword="null"/>, the root is a .NET image, the
    /// machine types match, or the module is a managed image that is AnyCPU-like
    /// (see <see cref="IsManagedAnyCpuLike"/>).
    /// </returns>
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

    /// <summary>
    /// Determines whether a module is a managed image built as AnyCPU, so that its
    /// I386 machine type does not indicate a real CPU mismatch.
    /// </summary>
    /// <param name="module">The module to check.</param>
    /// <returns>
    /// <see langword="true"/> only if the module is a .NET image whose machine type
    /// is I386, whose CLR header has the IL-only flag set, and whose CLR header
    /// does not have the 32-bit-required flag set. Otherwise <see langword="false"/>,
    /// including when the file cannot be read.
    /// </returns>
    /// <remarks>
    /// This reads the PE headers of <c>module.FileName</c> directly from disk,
    /// independently of the core client. Any exception while reading is swallowed
    /// and treated as "not AnyCPU-like".
    /// </remarks>
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

    /// <summary>
    /// Formats a byte count as megabytes, kilobytes or bytes.
    /// </summary>
    /// <param name="bytes">The number of bytes.</param>
    /// <returns>
    /// The value in whole MB (1,048,576 bytes or more), whole KB (1,024 bytes or
    /// more), or bytes, using integer division, so fractions are truncated
    /// (for example 1,535 bytes is "1 KB").
    /// </returns>
    private static string FormatByteSize(ulong bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024 * 1024)} MB",
            >= 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes} byte"
        };
    }

    /// <summary>
    /// Logs the core call statistics collected while a module was processed.
    /// </summary>
    /// <param name="stats">The statistics; if <see langword="null"/> nothing is logged.</param>
    /// <param name="moduleFileName">The file name of the module; only the file name part is shown.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <remarks>
    /// The message reports the byte total (formatted by <see cref="FormatByteSize"/>),
    /// the number of "send" calls and the time spent in them in microseconds. It is
    /// logged as <see cref="LogMessageType.ContentDefined"/> in purple. The byte
    /// total is labelled "Received" in the message and is taken from the
    /// statistics' <c>TotalBytesSent</c> value.
    /// </remarks>
    private static void LogModuleStats(
        CCoreCallStats stats,
        string moduleFileName,
        CDependsAnalysisContext context)
    {
        string statsData;

        if (stats == null)
            return;

        //
        // 'stats' here is a server filled structure.
        //
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

}
