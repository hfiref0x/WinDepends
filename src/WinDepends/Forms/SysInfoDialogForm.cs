/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       SYSINFODIALOGFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        02 May 2026
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
namespace WinDepends;

public partial class SysInfoDialogForm : Form
{
    readonly List<PropertyElement> m_SysInfo;
    readonly bool bIsLocal;
    readonly float fSize;

    public SysInfoDialogForm(List<PropertyElement> SystemInformation, bool isLocal, float fontSize)
    {
        InitializeComponent();
        m_SysInfo = SystemInformation;
        bIsLocal = isLocal;
        fSize = fontSize;
    }

    private void ShowSystemInformation()
    {
        // 1. Set Font first so metrics are available if needed later
        richTextBox1.Font = new Font(richTextBox1.Font.FontFamily, fSize);

        // 2. Calculate Tab Stop dynamically based on font size
        int dynamicTabStop = (int)(fSize * 18);

        // Ensure a minimum width so short keys don't look squashed
        int finalTabStop = Math.Max(150, dynamicTabStop);

        richTextBox1.Clear();

        if (bIsLocal)
        {
            Text = "System information (local)";
        }
        else
        {
            PropertyElement? computerName = m_SysInfo.Find(x => x.Name.Equals("Computer Name"));
            PropertyElement? userName = m_SysInfo.Find(x => x.Name.Equals("User Name"));
            Text = $"System information ({(computerName?.Value ?? "Unknown")}\\{(userName?.Value ?? "Unknown")})";
        }

        // 3. Set the tabs once for the control before starting the loop
        richTextBox1.SelectionTabs = new int[] { finalTabStop };

        foreach (var element in m_SysInfo)
        {
            richTextBox1.AppendTabbedText(element.Name, element.Value, finalTabStop);
        }

        richTextBox1.DeselectAll();
        ActiveControl = button1;
    }

    private void SysInfoForm_Load(object sender, EventArgs e)
    {
        richTextBox1.BackColor = Color.White;
        ShowSystemInformation();
    }

    private void Button3_Click(object sender, EventArgs e)
    {
        richTextBox1.SelectAll();
        ActiveControl = richTextBox1;
    }

    private void Button4_Click(object sender, EventArgs e)
    {
        richTextBox1.Copy();
    }

    private void RichTextBox1_SelectionChanged(object sender, EventArgs e)
    {
        button4.Enabled = !string.IsNullOrEmpty(richTextBox1.SelectedText);
    }

    private void SysInfoRefresh(object sender, EventArgs e)
    {
        if (bIsLocal)
        {
            m_SysInfo.Clear();
            CUtils.CollectSystemInformation(m_SysInfo);
            ShowSystemInformation();
        }
    }
}
