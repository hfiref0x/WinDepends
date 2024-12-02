/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       FINDDIALOGFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        01 Dec 2024
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
namespace WinDepends;

public partial class FindDialogForm : Form
{
    private readonly MainForm mainForm;
    readonly bool escKeyEnabled;

    public FindDialogForm(MainForm parent, bool bEscKeyEnabled)
    {
        InitializeComponent();
        mainForm = parent;
        escKeyEnabled = bEscKeyEnabled;
    }

    private void FindTextBox_TextChanged(object sender, EventArgs e)
    {
        FindButton.Enabled = !string.IsNullOrEmpty(FindTextBox.Text);
    }

    private void FindButton_Click(object sender, EventArgs e)
    {
        mainForm.LogFindOptions = RichTextBoxFinds.None;
        if (MatchWholeCheckBox.Checked) mainForm.LogFindOptions |= RichTextBoxFinds.WholeWord;
        if (MatchCaseCheckBox.Checked) mainForm.LogFindOptions |= RichTextBoxFinds.MatchCase;
        mainForm.LogFindText = FindTextBox.Text;
        mainForm.LogFindString();
    }

    private void FindDialogForm_Load(object sender, EventArgs e)
    {
        MatchWholeCheckBox.Checked = mainForm.LogFindOptions.HasFlag(RichTextBoxFinds.WholeWord);
        MatchCaseCheckBox.Checked = mainForm.LogFindOptions.HasFlag(RichTextBoxFinds.MatchCase);
        FindTextBox.Text = mainForm.LogFindText;
    }

    private void MatchWholeCheckBox_Click(object sender, EventArgs e)
    {
        if (MatchWholeCheckBox.Checked) mainForm.LogFindOptions |= RichTextBoxFinds.WholeWord;
    }

    private void MatchCaseCheckBox_Click(object sender, EventArgs e)
    {
        if (MatchCaseCheckBox.Checked) mainForm.LogFindOptions |= RichTextBoxFinds.MatchCase;
    }

    private void FindDialogForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && escKeyEnabled)
        {
            this.Close();
        }
    }

}
