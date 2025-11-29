/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       FINDDIALOGFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        29 Nov 2025
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

        this.StartPosition = FormStartPosition.Manual;
        this.TopMost = false;

        PositionDialogRelativeToMainForm();

        // Keep FindDialog position relative to MainForm which is cool
        mainForm.Move += (s, e) => PositionDialogRelativeToMainForm();
        mainForm.Resize += (s, e) => PositionDialogRelativeToMainForm();
    }

    private void PositionDialogRelativeToMainForm()
    {
        if (mainForm != null && !mainForm.IsDisposed)
        {
            Point mainFormPoint = mainForm.PointToScreen(new Point(0, 0));
            this.Location = new Point(
                mainFormPoint.X + mainForm.Width - this.Width - 20,
                mainFormPoint.Y + CConsts.FindDialogVerticalOffset);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FindTextBox.Focus();
        FindTextBox.SelectAll();
        CenterToParent();
    }

    // Override shown event to ensure it starts centered
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        CenterToParent();
    }

    private void FindTextBox_TextChanged(object sender, EventArgs e)
    {
        FindButton.Enabled = !string.IsNullOrEmpty(FindTextBox.Text);
    }

    private void FindButton_Click(object sender, EventArgs e)
    {
        mainForm.LogSearchState.FindOptions = RichTextBoxFinds.None;
        if (MatchWholeCheckBox.Checked) mainForm.LogSearchState.FindOptions |= RichTextBoxFinds.WholeWord;
        if (MatchCaseCheckBox.Checked) mainForm.LogSearchState.FindOptions |= RichTextBoxFinds.MatchCase;
        mainForm.LogSearchState.FindText = FindTextBox.Text;
        mainForm.LogFindString();
    }

    private void FindDialogForm_Load(object sender, EventArgs e)
    {
        MatchWholeCheckBox.Checked = mainForm.LogSearchState.FindOptions.HasFlag(RichTextBoxFinds.WholeWord);
        MatchCaseCheckBox.Checked = mainForm.LogSearchState.FindOptions.HasFlag(RichTextBoxFinds.MatchCase);
        FindTextBox.Text = mainForm.LogSearchState.FindText;
    }

    private void MatchWholeCheckBox_Click(object sender, EventArgs e)
    {
        if (MatchWholeCheckBox.Checked) mainForm.LogSearchState.FindOptions |= RichTextBoxFinds.WholeWord;
    }

    private void MatchCaseCheckBox_Click(object sender, EventArgs e)
    {
        if (MatchCaseCheckBox.Checked) mainForm.LogSearchState.FindOptions |= RichTextBoxFinds.MatchCase;
    }

    private void FindDialogForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && escKeyEnabled)
        {
            this.Hide();
        }
    }

    private void FindDialogForm_Closing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing &&
            mainForm != null && !mainForm.IsDisposed)
        {
            e.Cancel = true;
            this.Hide();
        }
    }

    private void FindDialog_Cancel(object sender, EventArgs e)
    {
        this.Hide();
    }
}
