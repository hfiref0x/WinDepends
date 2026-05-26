/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       FILEOPENFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        09 Aug 2025
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
        settings = fileOpenSettings ?? throw new ArgumentNullException(nameof(fileOpenSettings));
        escKeyEnabled = bEscKeyEnabled;
        displayedFileName = fileName ?? string.Empty;
        InitializeComponent();
    }

    private void FileOpenForm_Load(object sender, EventArgs e)
    {
        chBoxProcessRelocs.Checked = settings.ProcessRelocsForImage;
        chBoxUseCustomImageBase.Checked = settings.UseCustomImageBase;

        textBoxCustomImageBase.Enabled = settings.UseCustomImageBase;
        textBoxCustomImageBase.Text = settings.CustomImageBase.ToString("X");
        labelAllocGran.Enabled = settings.UseCustomImageBase;

        textBoxFileName.Text = displayedFileName;
        chBoxUseStats.Checked = settings.UseStats;
        chBoxPropagateSettings.Checked = settings.PropagateSettingsOnDependencies;
        chBoxAnalysisDefaultEnabled.Checked = settings.UseAsDefault;
        chBoxExpandForwarders.Checked = settings.ExpandForwarders;
        chBoxEnableExperimentalFeatures.Checked = settings.EnableExperimentalFeatures;

        labelAllocGran.Text = $"Value will be aligned to allocation granularity:\r\n0x{CUtils.AllocationGranularity:X}";
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
        settings.ProcessRelocsForImage = chBoxProcessRelocs.Checked;
        settings.PropagateSettingsOnDependencies = chBoxPropagateSettings.Checked;
        settings.UseAsDefault = chBoxAnalysisDefaultEnabled.Checked;
        settings.UseCustomImageBase = chBoxUseCustomImageBase.Checked;
        settings.UseStats = chBoxUseStats.Checked;
        settings.ExpandForwarders = chBoxExpandForwarders.Checked;
        settings.EnableExperimentalFeatures = chBoxEnableExperimentalFeatures.Checked;
        if (settings.UseCustomImageBase)
        {
            settings.CustomImageBase = CUtils.ParseMinAppAddressValue(textBoxCustomImageBase.Text);
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

    private void ChBoxUseCustomImageBase(object sender, EventArgs e)
    {
        textBoxCustomImageBase.Enabled = chBoxUseCustomImageBase.Checked;
        labelAllocGran.Enabled = chBoxUseCustomImageBase.Checked;
    }

}
