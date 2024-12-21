/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       FILEOPENFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        18 Dec 2024
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

public partial class FileOpenForm : Form
{
    readonly CFileOpenSettings settings;
    readonly bool escKeyEnabled;
    readonly string displayedFileName;

    public FileOpenForm(bool bEscKeyEnabled, CFileOpenSettings fileOpenSettings, string fileName)
    {
        settings = fileOpenSettings;
        escKeyEnabled = bEscKeyEnabled;
        displayedFileName = fileName;
        InitializeComponent();
    }

    private void FileOpenForm_Load(object sender, EventArgs e)
    {
        chBoxUseReloc.Checked = settings.UseRelocForImages;
        textBoxMinAppAddress.Enabled = chBoxUseReloc.Checked;
        textBoxMinAppAddress.Text = settings.MinAppAddress.ToString("X");
        textBoxFileName.Text = displayedFileName;
        chBoxUseStats.Checked = settings.UseStats;
        chBoxPropagateSettings.Checked = settings.PropagateSettingsOnDependencies;
        chBoxAnalysisDefaultEnabled.Checked = settings.AnalysisSettingsUseAsDefault;
        labelAllocGran.Text = $"Min. app. address will be aligned to system allocation\r\ngranularity: 0x{CUtils.AllocationGranularity:X}";
    }

    private void FileOpenForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && escKeyEnabled)
        {
            this.Close();
        }
    }

    private void ButtonOK_Click(object sender, EventArgs e)
    {
        settings.UseRelocForImages = chBoxUseReloc.Checked;
        settings.PropagateSettingsOnDependencies = chBoxPropagateSettings.Checked;
        settings.AnalysisSettingsUseAsDefault = chBoxAnalysisDefaultEnabled.Checked;
        settings.UseStats = chBoxUseStats.Checked;
        if (settings.UseRelocForImages)
        {
            settings.MinAppAddress = CUtils.ParseMinAppAddressValue(textBoxMinAppAddress.Text);
        }
    }

    private static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }

    private void TextBoxMinAppAddress_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (!IsHexDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private void chBoxUseReloc_CheckedChanged(object sender, EventArgs e)
    {
        textBoxMinAppAddress.Enabled = chBoxUseReloc.Checked;
    }
}
