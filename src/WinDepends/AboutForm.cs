/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       ABOUTFORM.CS
*
*  VERSION:     1.00
*
*  DATE:        22 Dec 2024
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Diagnostics;

namespace WinDepends;

internal partial class AboutForm : Form
{
    readonly bool escKeyEnabled;

    public AboutForm(bool bEscKeyEnabled)
    {
        InitializeComponent();
        escKeyEnabled = bEscKeyEnabled;
    }

    private void AboutForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && escKeyEnabled)
        {
            this.Close();
        }
    }

    private void AboutForm_Load(object sender, EventArgs e)
    {
        Text = $"About {CConsts.ProgramName}";
        AboutVersionLabel.Text = $"Version: {CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild}";
        AboutNameLabel.Text = $"{CConsts.ProgramName} for Windows 10/11";
        AboutCopyrightLabel.Text = CConsts.CopyrightString;
        AboutBuildLabel.Text = $"Build date: {Properties.Resources.BuildDate}";
        AboutAssemblyLabel.Text = $"Framework version: {CUtils.GetRunningFrameworkVersion()}";
        AboutOSLabel.Text = System.Environment.OSVersion.ToString();
    }

    private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            linkLabel1.LinkVisited = true;
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/hfiref0x/WinDepends", UseShellExecute = true });
        }
        catch { }
    }
}
