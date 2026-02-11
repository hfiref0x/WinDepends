/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CDEPENDS.CS
*
*  VERSION:     1.00
*
*  DATE:        11 Feb 2026
*  
*  Implementation of base session class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.Serialization;

namespace WinDepends;

[DataContract]
public record LogEntry
{
    [DataMember]
    public string LoggedMessage { get; init; }

    [DataMember]
    public Color EntryColor { get; init; }

    public LogEntry(string loggedMessage, Color entryColor)
    {
        LoggedMessage = loggedMessage;
        EntryColor = entryColor;
    }
}

[DataContract]
public class CDepends
{
    [DataMember]
    public bool IsSavedSessionView { get; set; }
    [DataMember]
    public string SessionFileName { get; set; } = string.Empty;
    [DataMember]
    public int SessionNodeMaxDepth { get; set; }
    [DataMember]
    public CModule RootModule { get; set; }
    [DataMember]
    public List<PropertyElement> SystemInformation { get; set; } = [];

    [DataMember]
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
        RootModule = module ?? throw new ArgumentNullException(nameof(module));
    }
}
