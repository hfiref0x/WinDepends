/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CDEPENDSANALYSISCONTRACTS.CS
*
*  VERSION:     1.00
*  
*  DATE:        08 Oct 2026
*
*  CDepends analysis service contracts.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

/// <summary>
/// Shared, read-mostly state for a single dependency analysis run.
/// </summary>
/// <remarks>
/// One context is created per analysis and is handed to every module processor
/// invoked during traversal, so that configuration, logging and progress
/// reporting do not have to be threaded through each call individually.
/// </remarks>
internal sealed class CDependsAnalysisContext
{
    /// <summary>
    /// Gets the configuration that controls how the analysis is performed.
    /// </summary>
    public CConfiguration Configuration { get; }

    /// <summary>
    /// Gets the root module of the analysis, i.e. the module the user opened
    /// and from which dependent modules are discovered.
    /// </summary>
    public CModule RootModule { get; }

    /// <summary>
    /// Gets the table of the parent module's imported functions, keyed by an
    /// integer hash of the function.
    /// </summary>
    public Dictionary<int, FunctionHashObject> ParentImportsHashTable { get; }

    /// <summary>
    /// Gets the callback used to append messages to the analysis log.
    /// </summary>
    public AddLogMessageCallback AddLogMessage { get; }

    /// <summary>
    /// Gets the optional callback used to report analysis progress, or
    /// <see langword="null"/> if progress reporting is not requested.
    /// </summary>
    public Action<CDependsAnalysisProgress>? ReportProgress { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CDependsAnalysisContext"/> class.
    /// </summary>
    /// <param name="configuration">The analysis configuration.</param>
    /// <param name="rootModule">The root module of the analysis.</param>
    /// <param name="parentImportsHashTable">The parent imports hash table.</param>
    /// <param name="addLogMessage">The log message callback.</param>
    /// <param name="reportProgress">
    /// An optional progress callback; may be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="configuration"/>, <paramref name="rootModule"/>,
    /// <paramref name="parentImportsHashTable"/> or <paramref name="addLogMessage"/>
    /// is <see langword="null"/>.
    /// </exception>
    public CDependsAnalysisContext(
        CConfiguration configuration,
        CModule rootModule,
        Dictionary<int, FunctionHashObject> parentImportsHashTable,
        AddLogMessageCallback addLogMessage,
        Action<CDependsAnalysisProgress>? reportProgress = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(rootModule);
        ArgumentNullException.ThrowIfNull(parentImportsHashTable);
        ArgumentNullException.ThrowIfNull(addLogMessage);

        Configuration = configuration;
        RootModule = rootModule;
        ParentImportsHashTable = parentImportsHashTable;
        AddLogMessage = addLogMessage;
        ReportProgress = reportProgress;
    }
}

/// <summary>
/// Processes a single module during live analysis and produces the tree node
/// that represents it.
/// </summary>
/// <param name="module">The module to process.</param>
/// <param name="parentNode">
/// The tree node of the module that depends on <paramref name="module"/>, or
/// <see langword="null"/> when processing the root module.
/// </param>
/// <param name="fileOpenSettings">The settings used when opening the module file.</param>
/// <param name="context">The shared analysis context.</param>
/// <returns>
/// The node created for the module, or <see langword="null"/> if no node was
/// produced. A <see langword="null"/> result means the module's dependents are
/// not traversed (see <see cref="CDependsTraversalVisitResult.FromNode"/>).
/// </returns>
internal delegate TreeNode? CDependsModuleProcessor(
    CModule module,
    TreeNode? parentNode,
    CFileOpenSettings fileOpenSettings,
    CDependsAnalysisContext context);

/// <summary>
/// Processes a single module while populating the tree from an existing
/// session, and produces the tree node that represents it.
/// </summary>
/// <param name="module">The module to process.</param>
/// <param name="parentNode">
/// The tree node of the parent module, or <see langword="null"/> for the root.
/// </param>
/// <returns>
/// The node created for the module, or <see langword="null"/> if the module
/// was not accepted into the tree.
/// </returns>
/// <remarks>
/// Unlike <see cref="CDependsModuleProcessor"/>, this delegate takes neither
/// file-open settings nor an analysis context.
/// </remarks>
internal delegate TreeNode? CDependsSessionModuleProcessor(
    CModule module,
    TreeNode? parentNode);

/// <summary>
/// Identifies the stage an analysis has reached for the module currently being
/// processed.
/// </summary>
internal enum CDependsAnalysisProgressStage
{
    /// <summary>The tree is being populated.</summary>
    Populating,

    /// <summary>The module file is being opened.</summary>
    OpeningModule,

    /// <summary>The module's headers are being read.</summary>
    ReadingHeaders,

    /// <summary>The module's import and export tables are being read.</summary>
    ReadingImportsAndExports,

    /// <summary>Forwarded exports are being expanded.</summary>
    ExpandingForwarders,

    /// <summary>Module statistics are being read.</summary>
    ReadingStatistics,

    /// <summary>Processing of the module failed.</summary>
    ModuleProcessingFailed
}

/// <summary>
/// A progress notification emitted during analysis.
/// </summary>
/// <param name="Stage">The stage reached.</param>
/// <param name="ModuleFileName">The file name of the module being processed.</param>
/// <param name="Depth">
/// The depth of the module in the dependency tree.
/// </param>
internal sealed record CDependsAnalysisProgress(
    CDependsAnalysisProgressStage Stage,
    string ModuleFileName,
    int Depth);

/// <summary>
/// Owns the activation context for the lifetime of an analysis and exposes the
/// analysis context that runs inside it.
/// </summary>
/// <remarks>
/// Disposing the scope releases the activation context and, if it is still the
/// one registered on <see cref="CPathResolver.ActCtxHelper"/>, clears that
/// registration first. <see cref="Dispose"/> is idempotent. The type performs no
/// synchronization and is not thread-safe.
/// </remarks>
internal sealed class CDependsAnalysisScope : IDisposable
{
    private CActCtxHelper? _activationContext;

    /// <summary>
    /// Gets the analysis context associated with this scope.
    /// </summary>
    public CDependsAnalysisContext Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CDependsAnalysisScope"/> class.
    /// </summary>
    /// <param name="activationContext">
    /// The activation context helper whose lifetime this scope takes ownership of.
    /// </param>
    /// <param name="context">The analysis context exposed by this scope.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="activationContext"/> or <paramref name="context"/> is
    /// <see langword="null"/>.
    /// </exception>
    internal CDependsAnalysisScope(
        CActCtxHelper activationContext,
        CDependsAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(activationContext);
        ArgumentNullException.ThrowIfNull(context);

        _activationContext = activationContext;
        Context = context;
    }

    /// <summary>
    /// Releases the activation context. If it is the instance currently held by
    /// <see cref="CPathResolver.ActCtxHelper"/>, that property is set to
    /// <see langword="null"/> before the context is disposed. Subsequent calls
    /// have no effect.
    /// </summary>
    public void Dispose()
    {
        CActCtxHelper? activationContext = _activationContext;

        if (activationContext == null)
            return;

        _activationContext = null;

        if (ReferenceEquals(CPathResolver.ActCtxHelper, activationContext))
        {
            CPathResolver.ActCtxHelper = null;
        }

        activationContext.Dispose();
    }
}

/// <summary>
/// A recorded occurrence of a module that was encountered again after it had
/// already been processed (a duplicate).
/// </summary>
/// <param name="ModuleFileName">The file name of the duplicate module.</param>
/// <param name="OriginalInstanceId">
/// The instance ID of the original (canonical) module the duplicate refers to.
/// </param>
/// <param name="FileNotFound">
/// Whether the original module's file was not found.
/// </param>
/// <param name="IsInvalid">Whether the original module is invalid.</param>
/// <param name="ExportContainErrors">
/// Whether the original module's export table contains errors.
/// </param>
/// <param name="IsApiSetContract">
/// Whether the original module is an API set contract.
/// </param>
/// <param name="IsStoppedNode">
/// Whether the original module's node was stopped (not expanded further).
/// </param>
/// <remarks>
/// The state flags are captured from the original module, not from the
/// duplicate.
/// </remarks>
internal sealed record CDependsDuplicateObservation(
    string ModuleFileName,
    int OriginalInstanceId,
    bool FileNotFound,
    bool IsInvalid,
    bool ExportContainErrors,
    bool IsApiSetContract,
    bool IsStoppedNode);

/// <summary>
/// A recorded snapshot of a canonical module, i.e. the first instance of a
/// module that duplicates later refer back to.
/// </summary>
/// <param name="ModuleFileName">The file name of the module.</param>
/// <param name="InstanceId">The module's instance ID.</param>
/// <param name="FileNotFound">Whether the module's file was not found.</param>
/// <param name="IsInvalid">Whether the module is invalid.</param>
/// <param name="ExportContainErrors">
/// Whether the module's export table contains errors.
/// </param>
/// <param name="IsApiSetContract">Whether the module is an API set contract.</param>
/// <param name="IsStoppedNode">
/// Whether the module's node was stopped (not expanded further).
/// </param>
internal sealed record CDependsCanonicalModuleObservation(
    string ModuleFileName,
    int InstanceId,
    bool FileNotFound,
    bool IsInvalid,
    bool ExportContainErrors,
    bool IsApiSetContract,
    bool IsStoppedNode);

/// <summary>
/// Collects observations of canonical and duplicate modules during analysis and
/// checks that they are consistent with each other.
/// </summary>
/// <remarks>
/// The type is not thread-safe. Call <see cref="Validate"/> once recording is
/// complete, and <see cref="Clear"/> to reuse the observer for another run.
/// </remarks>
internal sealed class CDependsDuplicateObserver
{
    private readonly List<CDependsCanonicalModuleObservation> _canonicalModules = [];
    private readonly List<CDependsDuplicateObservation> _observations = [];

    /// <summary>
    /// Gets the canonical modules recorded so far, in recording order.
    /// </summary>
    public IReadOnlyList<CDependsCanonicalModuleObservation> CanonicalModules => _canonicalModules;

    /// <summary>
    /// Gets the duplicate observations recorded so far, in recording order.
    /// </summary>
    public IReadOnlyList<CDependsDuplicateObservation> Observations => _observations;

    /// <summary>
    /// Records a module as canonical.
    /// </summary>
    /// <param name="module">The module to record.</param>
    /// <remarks>
    /// The call is ignored if <paramref name="module"/> is <see langword="null"/>
    /// or if its <c>OriginalInstanceId</c> is non-zero, because such a module is
    /// itself a duplicate of another.
    /// </remarks>
    public void RecordCanonical(CModule module)
    {
        if (module == null || module.OriginalInstanceId != 0)
            return;

        _canonicalModules.Add(new CDependsCanonicalModuleObservation(
            module.FileName,
            module.InstanceId,
            module.FileNotFound,
            module.IsInvalid,
            module.ExportContainErrors,
            module.IsApiSetContract,
            module.IsStoppedNode));
    }

    /// <summary>
    /// Records that <paramref name="duplicateModule"/> was found to duplicate
    /// <paramref name="originalModule"/>.
    /// </summary>
    /// <param name="duplicateModule">The module that was encountered again.</param>
    /// <param name="originalModule">The original module it duplicates.</param>
    /// <remarks>
    /// The file name is taken from the duplicate; the instance ID and state
    /// flags are taken from the original. The call is ignored if either argument
    /// is <see langword="null"/>.
    /// </remarks>
    public void Record(CModule duplicateModule, CModule originalModule)
    {
        if (duplicateModule == null || originalModule == null)
            return;

        _observations.Add(new CDependsDuplicateObservation(
            duplicateModule.FileName,
            originalModule.InstanceId,
            originalModule.FileNotFound,
            originalModule.IsInvalid,
            originalModule.ExportContainErrors,
            originalModule.IsApiSetContract,
            originalModule.IsStoppedNode));
    }

    /// <summary>
    /// Checks the recorded observations for internal consistency.
    /// </summary>
    /// <returns>
    /// A result listing every issue found; <see cref="CDependsDuplicateValidationResult.IsValid"/>
    /// is <see langword="true"/> when there are none.
    /// </returns>
    /// <remarks>
    /// <para>For canonical modules, an issue is reported when:</para>
    /// <list type="bullet">
    /// <item><description>the instance ID is zero (the module is then skipped by the remaining checks);</description></item>
    /// <item><description>the instance ID was already recorded for another canonical module;</description></item>
    /// <item><description>the file name was already recorded (compared case-insensitively).</description></item>
    /// </list>
    /// <para>For duplicates, an issue is reported when:</para>
    /// <list type="bullet">
    /// <item><description>the original instance ID is zero (the duplicate is then skipped by the remaining checks);</description></item>
    /// <item><description>the original instance ID does not match any canonical module;</description></item>
    /// <item><description>no canonical module has the duplicate's file name (compared case-insensitively).</description></item>
    /// </list>
    /// </remarks>
    public CDependsDuplicateValidationResult Validate()
    {
        CDependsDuplicateValidationResult result = new();
        HashSet<int> canonicalInstanceIds = [];
        HashSet<string> canonicalModuleNames =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (CDependsCanonicalModuleObservation canonicalModule in _canonicalModules)
        {
            if (canonicalModule.InstanceId == 0)
            {
                result.AddIssue($"Canonical module \"{canonicalModule.ModuleFileName}\" has an instance ID of zero.");
                continue;
            }

            if (!canonicalInstanceIds.Add(canonicalModule.InstanceId))
            {
                result.AddIssue($"Canonical module instance ID {canonicalModule.InstanceId} is duplicated.");
            }

            if (!canonicalModuleNames.Add(canonicalModule.ModuleFileName))
            {
                result.AddIssue($"Canonical module \"{canonicalModule.ModuleFileName}\" was recorded more than once.");
            }
        }

        foreach (CDependsDuplicateObservation duplicate in _observations)
        {
            if (duplicate.OriginalInstanceId == 0)
            {
                result.AddIssue($"Duplicate module \"{duplicate.ModuleFileName}\" has no original instance ID.");
                continue;
            }

            if (!canonicalInstanceIds.Contains(duplicate.OriginalInstanceId))
            {
                result.AddIssue($"Duplicate module \"{duplicate.ModuleFileName}\" references missing original instance ID {duplicate.OriginalInstanceId}.");
            }

            if (!canonicalModuleNames.Contains(duplicate.ModuleFileName))
            {
                result.AddIssue($"Duplicate module \"{duplicate.ModuleFileName}\" has no canonical module observation.");
            }
        }

        return result;
    }

    /// <summary>
    /// Discards all recorded canonical and duplicate observations.
    /// </summary>
    public void Clear()
    {
        _canonicalModules.Clear();
        _observations.Clear();
    }
}

/// <summary>
/// A single inconsistency found by <see cref="CDependsDuplicateObserver.Validate"/>.
/// </summary>
/// <param name="Message">A human-readable description of the issue.</param>
internal sealed record CDependsDuplicateValidationIssue(
    string Message);

/// <summary>
/// The outcome of <see cref="CDependsDuplicateObserver.Validate"/>.
/// </summary>
internal sealed class CDependsDuplicateValidationResult
{
    private readonly List<CDependsDuplicateValidationIssue> _issues = [];

    /// <summary>
    /// Gets the issues found, in the order they were detected.
    /// </summary>
    public IReadOnlyList<CDependsDuplicateValidationIssue> Issues => _issues;

    /// <summary>
    /// Gets a value indicating whether validation found no issues.
    /// </summary>
    public bool IsValid => _issues.Count == 0;

    /// <summary>
    /// Adds an issue to the result.
    /// </summary>
    /// <param name="message">The issue description. Null or empty messages are ignored.</param>
    internal void AddIssue(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _issues.Add(new CDependsDuplicateValidationIssue(message));
        }
    }
}

/// <summary>
/// Describes a request to analyze a module live, by opening files and walking
/// their dependencies.
/// </summary>
internal sealed class CDependsLiveAnalysisRequest
{
    /// <summary>Gets the file name of the root module.</summary>
    public string RootFileName { get; }

    /// <summary>Gets the root module to analyze.</summary>
    public CModule RootModule { get; }

    /// <summary>Gets the settings used when opening module files.</summary>
    public CFileOpenSettings FileOpenSettings { get; }

    /// <summary>Gets the shared analysis context.</summary>
    public CDependsAnalysisContext Context { get; }

    /// <summary>
    /// Gets the delegate invoked for each module visited during the analysis.
    /// </summary>
    public CDependsModuleProcessor ProcessModule { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CDependsLiveAnalysisRequest"/> class.
    /// </summary>
    /// <param name="rootFileName">The file name of the root module.</param>
    /// <param name="rootModule">The root module to analyze.</param>
    /// <param name="fileOpenSettings">The settings used when opening module files.</param>
    /// <param name="context">The shared analysis context.</param>
    /// <param name="processModule">The per-module processing delegate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rootModule"/>, <paramref name="fileOpenSettings"/>,
    /// <paramref name="context"/> or <paramref name="processModule"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="rootFileName"/> is empty or consists only of white space.
    /// It is also thrown as <see cref="ArgumentNullException"/> if it is
    /// <see langword="null"/>.
    /// </exception>
    public CDependsLiveAnalysisRequest(
        string rootFileName,
        CModule rootModule,
        CFileOpenSettings fileOpenSettings,
        CDependsAnalysisContext context,
        CDependsModuleProcessor processModule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFileName);
        ArgumentNullException.ThrowIfNull(rootModule);
        ArgumentNullException.ThrowIfNull(fileOpenSettings);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(processModule);

        RootFileName = rootFileName;
        RootModule = rootModule;
        FileOpenSettings = fileOpenSettings;
        Context = context;
        ProcessModule = processModule;
    }
}

/// <summary>
/// Describes a request to populate the module tree from an existing session.
/// </summary>
internal sealed class CDependsSessionPopulationRequest
{
    /// <summary>Gets the root module of the session.</summary>
    public CModule RootModule { get; }

    /// <summary>
    /// Gets the delegate invoked for each module visited during population.
    /// </summary>
    public CDependsSessionModuleProcessor ProcessModule { get; }

    /// <summary>Gets the callback used to report population progress.</summary>
    public Action<CDependsAnalysisProgress> ReportProgress { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CDependsSessionPopulationRequest"/> class.
    /// </summary>
    /// <param name="rootModule">The root module of the session.</param>
    /// <param name="processModule">The per-module processing delegate.</param>
    /// <param name="reportProgress">The progress callback.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>. Unlike
    /// <see cref="CDependsAnalysisContext"/>, the progress callback is required here.
    /// </exception>
    public CDependsSessionPopulationRequest(
        CModule rootModule,
        CDependsSessionModuleProcessor processModule,
        Action<CDependsAnalysisProgress> reportProgress)
    {
        ArgumentNullException.ThrowIfNull(rootModule);
        ArgumentNullException.ThrowIfNull(processModule);
        ArgumentNullException.ThrowIfNull(reportProgress);

        RootModule = rootModule;
        ProcessModule = processModule;
        ReportProgress = reportProgress;
    }
}

/// <summary>
/// The outcome of populating the module tree.
/// </summary>
internal sealed class CDependsPopulationResult
{
    /// <summary>
    /// Gets the root node of the populated tree, or <see langword="null"/> if
    /// population produced no root.
    /// </summary>
    public TreeNode? RootNode { get; }

    /// <summary>
    /// Gets a value indicating whether population succeeded, i.e. whether
    /// <see cref="RootNode"/> is not <see langword="null"/>.
    /// </summary>
    public bool IsSuccess => RootNode != null;

    /// <summary>Gets the number of modules that were processed.</summary>
    public int ProcessedModuleCount { get; }

    /// <summary>Gets the number of processed modules that were accepted into the tree.</summary>
    public int AcceptedModuleCount { get; }

    /// <summary>
    /// Gets the number of processed modules that were not accepted, computed as
    /// <see cref="ProcessedModuleCount"/> minus <see cref="AcceptedModuleCount"/>.
    /// </summary>
    public int RejectedModuleCount => ProcessedModuleCount - AcceptedModuleCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="CDependsPopulationResult"/> class.
    /// </summary>
    /// <param name="rootNode">The root node, or <see langword="null"/> on failure.</param>
    /// <param name="processedModuleCount">The number of modules processed.</param>
    /// <param name="acceptedModuleCount">The number of modules accepted.</param>
    public CDependsPopulationResult(
        TreeNode? rootNode,
        int processedModuleCount,
        int acceptedModuleCount)
    {
        RootNode = rootNode;
        ProcessedModuleCount = processedModuleCount;
        AcceptedModuleCount = acceptedModuleCount;
    }
}

/// <summary>
/// Describes a request to analyze a module and record the result in an
/// existing <see cref="CDepends"/> model rather than in a tree.
/// </summary>
internal sealed class CDependsModelAnalysisRequest
{
    /// <summary>Gets the model that receives the analysis result.</summary>
    public CDepends Depends { get; }

    /// <summary>
    /// Gets the file name of the root module, taken from
    /// <c>Depends.RootModule.FileName</c>.
    /// </summary>
    public string RootFileName { get; }

    /// <summary>Gets the settings used when opening module files.</summary>
    public CFileOpenSettings FileOpenSettings { get; }

    /// <summary>Gets the shared analysis context.</summary>
    public CDependsAnalysisContext Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CDependsModelAnalysisRequest"/> class.
    /// </summary>
    /// <param name="depends">The model to analyze. Its root module must be set.</param>
    /// <param name="fileOpenSettings">The settings used when opening module files.</param>
    /// <param name="context">
    /// The shared analysis context. Its root module must be the same instance as
    /// the root module of <paramref name="depends"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="depends"/>, <c>depends.RootModule</c>,
    /// <paramref name="fileOpenSettings"/> or <paramref name="context"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The root module of <paramref name="context"/> is not the same instance
    /// (reference equality) as the root module of <paramref name="depends"/>.
    /// </exception>
    public CDependsModelAnalysisRequest(
        CDepends depends,
        CFileOpenSettings fileOpenSettings,
        CDependsAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(depends);
        ArgumentNullException.ThrowIfNull(depends.RootModule);
        ArgumentNullException.ThrowIfNull(fileOpenSettings);
        ArgumentNullException.ThrowIfNull(context);

        if (!ReferenceEquals(depends.RootModule, context.RootModule))
        {
            throw new ArgumentException("The analysis context root module must belong to the supplied model.", nameof(context));
        }

        Depends = depends;
        RootFileName = depends.RootModule.FileName;
        FileOpenSettings = fileOpenSettings;
        Context = context;
    }
}

/// <summary>
/// The outcome of a model-based analysis.
/// </summary>
internal sealed class CDependsModelAnalysisResult
{
    /// <summary>Gets the analyzed model.</summary>
    public CDepends Depends { get; }

    /// <summary>Gets the number of modules that were processed.</summary>
    public int ProcessedModuleCount { get; }

    /// <summary>Gets the number of modules that were found to be duplicates.</summary>
    public int DuplicateModuleCount { get; }

    /// <summary>
    /// Gets a value indicating whether the analysis succeeded, i.e. whether the
    /// model has a root module.
    /// </summary>
    public bool IsSuccess => Depends.RootModule != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="CDependsModelAnalysisResult"/> class.
    /// </summary>
    /// <param name="depends">The analyzed model.</param>
    /// <param name="processedModuleCount">The number of modules processed.</param>
    /// <param name="duplicateModuleCount">The number of duplicate modules found.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="depends"/> is <see langword="null"/>.
    /// </exception>
    public CDependsModelAnalysisResult(
        CDepends depends,
        int processedModuleCount,
        int duplicateModuleCount)
    {
        ArgumentNullException.ThrowIfNull(depends);

        Depends = depends;
        ProcessedModuleCount = processedModuleCount;
        DuplicateModuleCount = duplicateModuleCount;
    }
}

/// <summary>
/// The result of visiting a module during tree-building traversal: the node
/// produced (if any) and whether traversal should continue into the module's
/// dependents.
/// </summary>
/// <remarks>
/// Instances are created with <see cref="FromNode"/>.
/// </remarks>
internal readonly struct CDependsTraversalVisitResult
{
    /// <summary>
    /// Gets the node produced for the visited module, or <see langword="null"/>
    /// if none was produced.
    /// </summary>
    public TreeNode? Node { get; }

    /// <summary>
    /// Gets a value indicating whether the traversal should descend into the
    /// visited module's dependents.
    /// </summary>
    public bool ShouldTraverseDependents { get; }

    private CDependsTraversalVisitResult(
        TreeNode? node,
        bool shouldTraverseDependents)
    {
        Node = node;
        ShouldTraverseDependents = shouldTraverseDependents;
    }

    /// <summary>
    /// Creates a visit result from the node produced for a module.
    /// </summary>
    /// <param name="node">The produced node, or <see langword="null"/>.</param>
    /// <returns>
    /// A result carrying <paramref name="node"/>. Dependents are traversed only
    /// when <paramref name="node"/> is not <see langword="null"/>.
    /// </returns>
    public static CDependsTraversalVisitResult FromNode(TreeNode? node)
    {
        return new CDependsTraversalVisitResult(node, node != null);
    }
}

/// <summary>
/// The result of visiting a module during model-based traversal: whether
/// traversal should continue into the module's dependents.
/// </summary>
/// <remarks>
/// Instances are created with <see cref="Continue"/> or <see cref="Stop"/>.
/// </remarks>
internal readonly struct CDependsModelTraversalVisitResult
{
    /// <summary>
    /// Gets a value indicating whether the traversal should descend into the
    /// visited module's dependents.
    /// </summary>
    public bool ShouldTraverseDependents { get; }

    private CDependsModelTraversalVisitResult(
        bool shouldTraverseDependents)
    {
        ShouldTraverseDependents = shouldTraverseDependents;
    }

    /// <summary>
    /// Creates a result that lets the traversal continue into the module's dependents.
    /// </summary>
    /// <returns>A result whose <see cref="ShouldTraverseDependents"/> is <see langword="true"/>.</returns>
    public static CDependsModelTraversalVisitResult Continue()
    {
        return new CDependsModelTraversalVisitResult(true);
    }

    /// <summary>
    /// Creates a result that stops the traversal from descending into the module's dependents.
    /// </summary>
    /// <returns>A result whose <see cref="ShouldTraverseDependents"/> is <see langword="false"/>.</returns>
    public static CDependsModelTraversalVisitResult Stop()
    {
        return new CDependsModelTraversalVisitResult(false);
    }
}

/// <summary>
/// Processes a single module during model-based traversal and decides whether
/// the traversal continues into its dependents.
/// </summary>
/// <param name="module">The module being visited.</param>
/// <param name="parentModule">
/// The module that depends on <paramref name="module"/>, or
/// <see langword="null"/> for the root module.
/// </param>
/// <param name="depth">The depth of the module in the dependency graph.</param>
/// <param name="fileOpenSettings">The settings used when opening the module file.</param>
/// <param name="context">The shared analysis context.</param>
/// <returns>
/// <see cref="CDependsModelTraversalVisitResult.Continue"/> to traverse the
/// module's dependents, or <see cref="CDependsModelTraversalVisitResult.Stop"/>
/// to skip them.
/// </returns>
internal delegate CDependsModelTraversalVisitResult
    CDependsModelModuleProcessor(
        CModule module,
        CModule? parentModule,
        int depth,
        CFileOpenSettings fileOpenSettings,
        CDependsAnalysisContext context);
