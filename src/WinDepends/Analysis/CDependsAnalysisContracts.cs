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
    CFileOpenSettings fileOpenSettings);

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
