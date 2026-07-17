/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       CCORECLIENT.SERVERPROCESS.CS
*
*  VERSION:     1.00
*
*  DATE:        14 Jul 2026
*  
*  Server process lifecycle routines for Core Server communication class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Diagnostics;
using System.Net.Sockets;

namespace WinDepends;

public partial class CCoreClient
{

    /// <summary>
    /// Generates a cryptographically secure random port number within the specified range.
    /// </summary>
    /// <param name="minPort">The minimum port number (inclusive).</param>
    /// <param name="maxPort">The maximum port number (inclusive).</param>
    /// <returns>A random port number between minPort and maxPort.</returns>
    private static int GenerateSecureRandomPort(int minPort, int maxPort)
    {
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            byte[] randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            uint randomValue = BitConverter.ToUInt32(randomBytes, 0);
            return minPort + (int)(randomValue % (uint)(maxPort - minPort + 1));
        }
    }

    /// <summary>
    /// Validates and canonicalizes a server executable path.
    /// </summary>
    /// <param name="filePath">The path to validate.</param>
    /// <param name="canonicalPath">Outputs the canonical (full) path if validation succeeds.</param>
    /// <param name="errorMessage">Outputs an error message if validation fails.</param>
    /// <returns>true if the path is valid; otherwise, false.</returns>
    private static bool ValidateServerPath(string filePath, out string canonicalPath, out string errorMessage)
    {
        canonicalPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "Server application path is not specified";
            return false;
        }

        try
        {
            canonicalPath = Path.GetFullPath(filePath);

            if (!File.Exists(canonicalPath))
            {
                errorMessage = "Server application file does not exist";
                return false;
            }

            string extension = Path.GetExtension(canonicalPath);
            if (!extension.Equals(CConsts.ExeFileExt, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Server application must be an executable file";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                                   ex is PathTooLongException || ex is UnauthorizedAccessException)
        {
            errorMessage = $"Invalid server path: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Cleans up resources after a failed connection attempt.
    /// </summary>
    /// <remarks>
    /// This method forcibly terminates the server process if it's still running,
    /// closes network streams and connections, and sets the error status.
    /// </remarks>
    private void CleanupFailedConnection()
    {
        // Safely terminate process if it's still running
        try
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _serverProcess.Kill();
                _serverProcess.Dispose();
            }
        }
        catch
        {
            // Intentionally silent.
        }

        _serverProcess = null;

        _dataStream?.Close();
        _dataStream = null;

        _clientConnection?.Close();
        _clientConnection = null;

        ErrorStatus = ServerErrorStatus.GeneralException;
    }

    /// <summary>
    /// Starts the server process and establishes a network connection.
    /// </summary>
    /// <returns>true if the server started and connection was established successfully; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// This method performs the following operations:
    /// </para>
    /// <list type="number">
    /// <item>Validates the server executable path</item>
    /// <item>Attempts to start the server process with a cryptographically secure random port</item>
    /// <item>Establishes a TCP connection to the server</item>
    /// <item>Waits for and validates the server's HELLO message</item>
    /// </list>
    /// <para>
    /// The method will retry up to <see cref="SERVER_START_ATTEMPTS"/> times if port conflicts occur.
    /// In DEBUG builds, the server console window is visible for debugging purposes.
    /// In RELEASE builds, the server runs hidden with no console window.
    /// </para>
    /// </remarks>
    public bool ConnectClient()
    {
        Process tempProcess = null;
        TcpClient tempConnection = null;
        NetworkStream tempStream = null;
        string errMessage, fileName, canonicalPath, validationError, arguments;

        try
        {
            fileName = ServerApplication;

            if (!ValidateServerPath(fileName, out canonicalPath, out validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            int startAttempts = SERVER_START_ATTEMPTS;
            int portNumber;

            do
            {
                portNumber = GenerateSecureRandomPort(CConsts.MinPortNumber, CConsts.MaxPortNumber);
                arguments = $"port {portNumber}";

                ProcessStartInfo processInfo = new()
                {
                    FileName = canonicalPath,
                    Arguments = arguments,
                    UseShellExecute = false,
#if DEBUG
                    CreateNoWindow = false,
#else
                    CreateNoWindow = true,
#endif
                    WorkingDirectory = Path.GetDirectoryName(canonicalPath)
                };

                tempProcess = Process.Start(processInfo);

                if (tempProcess == null)
                {
                    throw new Exception("Core process start failure");
                }

                Thread.Sleep(SERVER_START_DELAY_MS);

                if (tempProcess.HasExited)
                {
                    if (tempProcess.ExitCode != CConsts.SERVER_ERROR_INVALIDIP)
                    {
                        throw new Exception($"Server process exited with code {tempProcess.ExitCode}");
                    }
                    tempProcess.Dispose();
                    tempProcess = null;
                }
                else
                {
                    tempConnection = new();

                    Task connectTask = tempConnection.ConnectAsync(IPAddress, portNumber);
                    if (Task.WaitAny(new[] { connectTask }, CORE_CONNECTION_TIMEOUT) == 0)
                    {
                        if (tempConnection.Connected)
                        {
                            tempStream = tempConnection.GetStream();
                            tempStream.ReadTimeout = CORE_NETWORK_TIMEOUT;
                            tempStream.WriteTimeout = CORE_NETWORK_TIMEOUT;
                            Port = portNumber;
                            break;
                        }
                    }

                    tempConnection.Dispose();
                    tempConnection = null;
                }

            } while (--startAttempts > 0);

            // We couldn't connect server after all attempts.
            if (tempConnection == null || !tempConnection.Connected)
            {
                throw new Exception("Failed to connect to server after multiple attempts");
            }

            _serverProcess = tempProcess;
            _clientConnection = tempConnection;
            _dataStream = tempStream;

            CBufferChain idata = ReceiveReply();
            if (idata != null)
            {
                ErrorStatus = ServerErrorStatus.NoErrors;
                _addLogMessage($"Server has been started: {idata.BufferToStringNoCRLF()}", LogMessageType.System);
                return true;
            }
            else
            {
                ErrorStatus = ServerErrorStatus.ServerNeedRestart;
                _addLogMessage($"Server initialization failed, missing server HELLO", LogMessageType.ErrorOrWarning);
                CleanupFailedConnection();
                return false;
            }
        }
        catch (Exception ex)
        {
            if (ex is FileNotFoundException)
            {
                errMessage = $"{ex.Message} was not found, make sure it exist or change path to it: " +
                    $"Main menu -> Options -> Configuration, select Server tab, specify server application location and then press Connect button.";
            }
            else
            {
                errMessage = ex.Message;
            }

            tempStream?.Close();
            tempConnection?.Close();
            tempProcess?.Dispose();

            CleanupFailedConnection();
            _addLogMessage($"Server failed to start: {errMessage}", LogMessageType.ErrorOrWarning);
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the server and performs cleanup of all network resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method attempts a graceful shutdown by:
    /// </para>
    /// <list type="number">
    /// <item>Sending a shutdown request to the server</item>
    /// <item>Waiting briefly for the server to exit gracefully</item>
    /// <item>Forcibly terminating the server process if necessary</item>
    /// <item>Closing and disposing all network resources</item>
    /// </list>
    /// <para>
    /// This method is safe to call multiple times and handles exceptions internally.
    /// </para>
    /// </remarks>
    public void DisconnectClient()
    {
        try
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                ShutdownRequest();
                Thread.Sleep(SHUTDOWN_WAIT_MS);

                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            _addLogMessage($"Error during server shutdown: {ex.Message}", LogMessageType.ErrorOrWarning);
        }
        finally
        {
            if (_dataStream != null)
            {
                _dataStream.Close();
                _dataStream = null;
            }
            if (_clientConnection != null)
            {
                _clientConnection.Close();
                _clientConnection = null;
            }

            if (_serverProcess != null)
            {
                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }
    }

    /// <summary>
    /// Sends an exit request to the server.
    /// </summary>
    /// <returns>true if the exit command was sent successfully; otherwise, false.</returns>
    public bool ExitRequest()
    {
        return SendRequest(CConsts.CMD_EXIT);
    }

    /// <summary>
    /// Sends a shutdown request to the server.
    /// </summary>
    /// <returns>true if the shutdown command was sent successfully; otherwise, false.</returns>
    public bool ShutdownRequest()
    {
        return SendRequest(CConsts.CMD_SHUTDOWN);
    }
}
