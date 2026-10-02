/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CDEPENDSANALYSISCONTRACTS.CS
*
*  VERSION:     1.00
*  
*  DATE:        14 Sep 2026
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
    public CDepends Depends { get; }
    public Dictionary<int, FunctionHashObject> ParentImportsHashTable { get; }
    public AddLogMessageCallback AddLogMessage { get; }

    public CDependsAnalysisContext(
        CConfiguration configuration,
        CDepends depends,
        Dictionary<int, FunctionHashObject> parentImportsHashTable,
        AddLogMessageCallback addLogMessage)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(depends);
        ArgumentNullException.ThrowIfNull(parentImportsHashTable);
        ArgumentNullException.ThrowIfNull(addLogMessage);

        Configuration = configuration;
        Depends = depends;
        ParentImportsHashTable = parentImportsHashTable;
        AddLogMessage = addLogMessage;
    }
}
