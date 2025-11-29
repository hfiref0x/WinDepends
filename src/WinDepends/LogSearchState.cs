/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025
*
*  TITLE:       LOGSEARCHSTATE.CS
*
*  VERSION:     1.00
*  
*  DATE:        29 Nov 2025
*  
*  Implementation of LogSearchState class used for text search.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
namespace WinDepends;

/// <summary>
/// Encapsulates the state for log search operations.
/// </summary>
public class LogSearchState
{
    public RichTextBoxFinds FindOptions { get; set; }
    public int SearchPosition { get; set; }
    public string FindText { get; set; } = string.Empty;
    public int IndexOfSearchText { get; set; } = -1;

    public void Reset()
    {
        FindOptions = RichTextBoxFinds.None;
        SearchPosition = 0;
        FindText = string.Empty;
        IndexOfSearchText = -1;
    }
}
