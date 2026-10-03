/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CDEPENDSANALYSISCONTRACTS.CS
*
*  VERSION:     1.00
*  
*  DATE:        02 Oct 2026
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

internal sealed class CDependsAnalysisContext
{
    public CConfiguration Configuration { get; }
    public CModule RootModule { get; }
    public Dictionary<int, FunctionHashObject> ParentImportsHashTable { get; }
    public AddLogMessageCallback AddLogMessage { get; }
    public Action<CDependsAnalysisProgress>? ReportProgress { get; }

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

internal delegate TreeNode? CDependsModuleProcessor(
    CModule module,
    TreeNode? parentNode,
    CFileOpenSettings fileOpenSettings,
    CDependsAnalysisContext context);

internal delegate TreeNode? CDependsSessionModuleProcessor(
    CModule module,
    TreeNode? parentNode);

internal enum CDependsAnalysisProgressStage
{
    Populating,
    OpeningModule,
    ReadingHeaders,
    ReadingImportsAndExports,
    ExpandingForwarders,
    ReadingStatistics,
    ModuleProcessingFailed
}

internal sealed record CDependsAnalysisProgress(
    CDependsAnalysisProgressStage Stage,
    string ModuleFileName,
    int Depth);

internal sealed class CDependsAnalysisScope : IDisposable
{
    private CActCtxHelper? _activationContext;
    public CDependsAnalysisContext Context { get; }

    internal CDependsAnalysisScope(
        CActCtxHelper activationContext,
        CDependsAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(activationContext);
        ArgumentNullException.ThrowIfNull(context);

        _activationContext = activationContext;
        Context = context;
    }

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

internal sealed record CDependsDuplicateObservation(
    string ModuleFileName,
    int OriginalInstanceId,
    bool FileNotFound,
    bool IsInvalid,
    bool ExportContainErrors,
    bool IsApiSetContract,
    bool IsStoppedNode);

internal sealed record CDependsCanonicalModuleObservation(
    string ModuleFileName,
    int InstanceId,
    bool FileNotFound,
    bool IsInvalid,
    bool ExportContainErrors,
    bool IsApiSetContract,
    bool IsStoppedNode);

internal sealed class CDependsDuplicateObserver
{
    private readonly List<CDependsCanonicalModuleObservation> _canonicalModules = [];
    private readonly List<CDependsDuplicateObservation> _observations = [];

    public IReadOnlyList<CDependsCanonicalModuleObservation> CanonicalModules => _canonicalModules;

    public IReadOnlyList<CDependsDuplicateObservation> Observations =>
        _observations;

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
                result.AddIssue(
                    $"Canonical module \"{canonicalModule.ModuleFileName}\" has an instance ID of zero.");
                continue;
            }

            if (!canonicalInstanceIds.Add(canonicalModule.InstanceId))
            {
                result.AddIssue(
                    $"Canonical module instance ID {canonicalModule.InstanceId} is duplicated.");
            }

            if (!canonicalModuleNames.Add(canonicalModule.ModuleFileName))
            {
                result.AddIssue(
                    $"Canonical module \"{canonicalModule.ModuleFileName}\" was recorded more than once.");
            }
        }

        foreach (CDependsDuplicateObservation duplicate in _observations)
        {
            if (duplicate.OriginalInstanceId == 0)
            {
                result.AddIssue(
                    $"Duplicate module \"{duplicate.ModuleFileName}\" has no original instance ID.");
                continue;
            }

            if (!canonicalInstanceIds.Contains(duplicate.OriginalInstanceId))
            {
                result.AddIssue(
                    $"Duplicate module \"{duplicate.ModuleFileName}\" references missing original instance ID {duplicate.OriginalInstanceId}.");
            }

            if (!canonicalModuleNames.Contains(duplicate.ModuleFileName))
            {
                result.AddIssue(
                    $"Duplicate module \"{duplicate.ModuleFileName}\" has no canonical module observation.");
            }
        }

        return result;
    }

    public void Clear()
    {
        _canonicalModules.Clear();
        _observations.Clear();
    }
}

internal sealed record CDependsDuplicateValidationIssue(
    string Message);

internal sealed class CDependsDuplicateValidationResult
{
    private readonly List<CDependsDuplicateValidationIssue> _issues = [];

    public IReadOnlyList<CDependsDuplicateValidationIssue> Issues =>
        _issues;

    public bool IsValid => _issues.Count == 0;

    internal void AddIssue(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _issues.Add(new CDependsDuplicateValidationIssue(message));
        }
    }
}

internal sealed class CDependsLiveAnalysisRequest
{
    public string RootFileName { get; }
    public CModule RootModule { get; }
    public CFileOpenSettings FileOpenSettings { get; }
    public CDependsAnalysisContext Context { get; }
    public CDependsModuleProcessor ProcessModule { get; }

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

internal sealed class CDependsSessionPopulationRequest
{
    public CModule RootModule { get; }
    public CDependsSessionModuleProcessor ProcessModule { get; }
    public Action<CDependsAnalysisProgress> ReportProgress { get; }

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

internal sealed class CDependsPopulationResult
{
    public TreeNode? RootNode { get; }

    public bool IsSuccess => RootNode != null;

    public int ProcessedModuleCount { get; }

    public int AcceptedModuleCount { get; }

    public int RejectedModuleCount =>
        ProcessedModuleCount - AcceptedModuleCount;

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

internal sealed class CDependsModelAnalysisRequest
{
    public string RootFileName { get; }
    public CFileOpenSettings FileOpenSettings { get; }
    public CDependsAnalysisContext Context { get; }

    public CDependsModelAnalysisRequest(
        string rootFileName,
        CFileOpenSettings fileOpenSettings,
        CDependsAnalysisContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFileName);
        ArgumentNullException.ThrowIfNull(fileOpenSettings);
        ArgumentNullException.ThrowIfNull(context);

        RootFileName = rootFileName;
        FileOpenSettings = fileOpenSettings;
        Context = context;
    }
}

internal sealed class CDependsModelAnalysisResult
{
    public CDepends Depends { get; }

    public int ProcessedModuleCount { get; }

    public int DuplicateModuleCount { get; }

    public bool IsSuccess => Depends.RootModule != null;

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

internal readonly struct CDependsTraversalVisitResult
{
    public TreeNode? Node { get; }

    public bool ShouldTraverseDependents { get; }

    private CDependsTraversalVisitResult(
        TreeNode? node,
        bool shouldTraverseDependents)
    {
        Node = node;
        ShouldTraverseDependents = shouldTraverseDependents;
    }

    public static CDependsTraversalVisitResult FromNode(TreeNode? node)
    {
        return new CDependsTraversalVisitResult(
            node,
            node != null);
    }
}
