/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       SYSINFODIALOGFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        19 Sep 2024
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

    public SysInfoDialogForm(List<PropertyElement> SystemInformation, bool isLocal)
    {
        InitializeComponent();
        m_SysInfo = SystemInformation;
        bIsLocal = isLocal;
    }

    private void AddTabbedText(string name, string value)
    {
        richTextBox1.AppendText(name + ":", Color.Black, true, false);
        richTextBox1.AppendText("\t" + value, Color.Black, false, true);
    }

    private void ShowSystemInformation()
    {
        richTextBox1.Clear();

        if (bIsLocal)
        {
            Text = "System information (local)";
        }
        else
        {
            PropertyElement computerName = m_SysInfo.Find(x => x.Name.Equals("Computer Name"));
            PropertyElement userName = m_SysInfo.Find(x => x.Name.Equals("User Name"));
            Text = $"System information ({computerName.Value}\\{userName.Value})";
        }

        foreach (var element in m_SysInfo)
        {
            AddTabbedText(element.Name, element.Value);
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
