/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CDEPENDS.CS
*
*  VERSION:     1.00
*
*  DATE:        13 Dec 2024
*  
*  Implementation of base session class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
namespace WinDepends;

[Serializable()]
public record LogEntry(string LoggedMessage, Color EntryColor);

[Serializable()]
public class CDepends
{
    public bool IsSavedSessionView { get; set; }
    public string SessionFileName { get; set; } = string.Empty;
    public int SessionNodeMaxDepth { get; set; }
    public CModule RootModule { get; set; }
    public List<PropertyElement> SystemInformation { get; set; } = [];

    public List<LogEntry> ModuleAnalysisLog { get; set; } = [];

    public CDepends()
    {
    }

    public CDepends(string moduleName)
    {
        RootModule = new(moduleName);
    }

    public CDepends(CModule module)
    {
        RootModule = module;
    }
}
