/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       PROGRAM.CS
*
*  VERSION:     1.00
*
*  DATE:        11 Jun 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.InteropServices;

namespace WinDepends;

internal static class Program
{
    #region "P/Inkove"
    [DllImport("shell32.dll")]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);
    #endregion

    static void ExceptionHandler(object sender, UnhandledExceptionEventArgs args)
    {
        Exception ex = (Exception)args.ExceptionObject;
        MessageBox.Show($"Exception: {ex}, please report to developers",
            CConsts.ProgramName, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        AppDomain currentDomain = AppDomain.CurrentDomain;
        currentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandler);

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();

        SetCurrentProcessExplicitAppUserModelID("hfiref0x.WinDepends");

        Application.Run(new MainForm());
    }
}
