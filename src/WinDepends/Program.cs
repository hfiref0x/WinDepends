/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       PROGRAM.CS
*
*  VERSION:     1.00
*
*  DATE:        11 Feb 2026
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
    #region "P/Invoke"
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
    static int Main(string[] args)
    {
        // Check if running in CLI mode
        if (CCliHandler.ShouldRunAsCli(args))
        {
            return CCliHandler.Run(args);
        }

        // GUI mode
        AppDomain currentDomain = AppDomain.CurrentDomain;
        currentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandler);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        ApplicationConfiguration.Initialize();

        SetCurrentProcessExplicitAppUserModelID("hfiref0x.WinDepends");

        Application.Run(new MainForm());

        return 0;
    }
}
