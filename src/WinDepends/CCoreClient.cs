/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CCORECLIENT.CS
*
*  VERSION:     1.00
*
*  DATE:        22 Jan 2025
*  
*  Core Server communication class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Text;

namespace WinDepends;

public enum ModuleInformationType
{
    Headers,
    Imports,
    Exports,
    DataDirectories,
    ApiSetName
}

public enum RequestSendStatus
{
    Okay,
    ErrorServerNeedRestart,
    ErrorNetworkStreamNotInitialized,
    ErrorSocketException,
    ErrorGeneralException
}

public enum ModuleOpenStatus
{
    Okay,
    ErrorUnspecified,
    ErrorSendCommand,
    ErrorReceivedDataInvalid,
    ErrorFileNotFound,
    ErrorCannotReadFileHeaders,
    ErrorInvalidHeadersOrSignatures
}

public class CBufferChain
{
    private CBufferChain next;
    public uint DataSize;
    public char[] Data;

    public CBufferChain Next { get => next; set => next = value; }

    public CBufferChain()
    {
        Data = new char[CConsts.CoreServerChainSizeMax];
    }

    public string BufferToString()
    {
        CBufferChain chain = this;

        StringBuilder sb = new();

        do
        {
            string dataWithoutZeroes = new string(chain.Data).TrimEnd('\0').Replace("\n", "").Replace("\r", "");
            sb.Append(dataWithoutZeroes);
            chain = chain.Next;

        } while (chain != null);

        return sb.ToString();
    }
}

public class CCoreClient : IDisposable
{
    private bool IsDisposed;
    private Process ServerProcess;     // WinDepends.Core instance.
    public TcpClient ClientConnection;
    private NetworkStream DataStream;
    readonly LogEventCallback LogEvent;
    private string serverApplication;
    public string IPAddress { get; }
    public int PortNumber { get; }

    public int ServerProcessId
    {
        get
        {
            if (ServerProcess != null)
            {
                return ServerProcess.Id;
            }

            return -1;
        }
    }

    public string GetServerApplication()
    {
        return serverApplication;
    }

    public void SetServerApplication(string value)
    {
        serverApplication = value;
    }

    public CCoreClient(string serverApplication, string ipAddress, int portNumber, LogEventCallback logEventCallback)
    {
        LogEvent = logEventCallback;
        SetServerApplication(serverApplication);
        IPAddress = ipAddress;
        PortNumber = portNumber;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }
        if (disposing)
        {
            DisconnectClient();
            ClientConnection?.Dispose();
            ServerProcess?.Dispose();
        }

        IsDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Checks if the server reply indicate success.
    /// </summary>
    /// <returns></returns>
    public bool IsRequestSuccessful()
    {
        CBufferChain idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return false;
        }

        string response = new(idata.Data);
#pragma warning disable CA1309 // Use ordinal string comparison
        return string.Equals(response, CConsts.WDEP_STATUS_200, StringComparison.InvariantCulture);
#pragma warning restore CA1309 // Use ordinal string comparison
    }

    public static bool IsModuleNameApiSetContract(string moduleName)
    {
        if (moduleName.Length < 4)
        {
            return false;
        }

        return moduleName.StartsWith("API-", StringComparison.OrdinalIgnoreCase) || moduleName.StartsWith("EXT-", StringComparison.OrdinalIgnoreCase);
    }

    public object SendCommandAndReceiveReplyAsObjectJSON(string command, Type objectType, bool preProcessData = false)
    {
        // Communication failure, server need restart.
        if (ClientConnection == null || !ClientConnection.Connected)
        {
            return null;
        }

        var status = SendRequest(command);
        if (status != RequestSendStatus.Okay)
        {
            return null;
        }

        if (!IsRequestSuccessful())
        {
            return null;
        }

        CBufferChain idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return null;
        }

        string result = idata.BufferToString();
        if (string.IsNullOrEmpty(result))
        {
            return null;
        }

        if (preProcessData)
        {
            result = result.Replace("\\", "\\\\");
        }

        return DeserializeDataJSON(objectType, result);
    }

    /// <summary>
    /// Send command to depends-core
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private RequestSendStatus SendRequest(string message)
    {
        // Communication failure, server need restart.
        if (ClientConnection == null || !ClientConnection.Connected)
        {
            return RequestSendStatus.ErrorServerNeedRestart;
        }

        if (DataStream == null)
        {
            return RequestSendStatus.ErrorNetworkStreamNotInitialized;
        }

        try
        {
            using (BinaryWriter bw = new(DataStream, Encoding.Unicode, true))
            {
                bw.Write(Encoding.Unicode.GetBytes(message));
            }
        }
        catch (IOException ex)
        {
            LogEvent(null, LogEventType.CoreServerSendError, ex.Message);
            return RequestSendStatus.ErrorSocketException;
        }
        catch (Exception ex)
        {
            LogEvent(null, LogEventType.CoreServerSendError, ex.Message);
            return RequestSendStatus.ErrorGeneralException;
        }

        return RequestSendStatus.Okay;
    }

    /// <summary>
    /// Receive reply from depends-core and store it into temporary object.
    /// </summary>
    /// <returns></returns>
    private CBufferChain ReceiveReply()
    {
        if (DataStream == null)
        {
            return null;
        }

        try
        {
            using (BinaryReader br = new(DataStream, Encoding.Unicode, true))
            {
                CBufferChain buf = new(), buf0;
                char prev = '\0';
                buf0 = buf;

                while (true)
                {
                    for (int i = 0; i < CConsts.CoreServerChainSizeMax; i++)
                    {
                        try
                        {
                            buf.Data[i] = br.ReadChar();
                        }
                        catch
                        {
                            return buf0;
                        }

                        buf.DataSize++;

                        if (buf.Data[i] == '\n' && prev == '\r')
                            return buf0;

                        prev = buf.Data[i];
                    }

                    buf.Next = new();
                    buf = buf.Next;
                }
            }
        }
        catch (Exception ex)
        {
            LogEvent(null, LogEventType.CoreServerReceiveError, ex.Message);
        }
        return null;
    }

    object DeserializeDataJSON(Type objectType, string data)
    {
        object deserializedObject = null;

        try
        {
            var serializer = new DataContractJsonSerializer(objectType);
            using (MemoryStream ms = new(Encoding.Unicode.GetBytes(data)))
            {
                deserializedObject = serializer.ReadObject(ms);
            }
        }
        catch (Exception ex)
        {
            LogEvent(null, LogEventType.CoreServerDeserializeError, ex.Message);
        }

        return deserializedObject;
    }

    public static bool IsNullOrEmptyResponse(CBufferChain buffer)
    {
        // Check for null.
        if (buffer == null || buffer.DataSize == 0 || buffer.Data == null)
        {
            return true;
        }

        // Check for empty.
        if (buffer.DataSize == 2)
        {
            string response = new(buffer.Data);
            if (response.Equals("\r\n", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Open Coff module and read for futher operations.
    /// </summary>
    /// <param name="module"></param>
    /// <param name="fileOpenSettings"></param>
    /// <returns></returns>
    public ModuleOpenStatus OpenModule(ref CModule module, bool useStats, bool useReloc, uint minAppAddress)
    {
        string cmd = $"open file \"{module.FileName}\"";

        if (useStats)
        {
            cmd += " use_stats";
        }

        if (useReloc)
        {
            cmd += $" reloc {minAppAddress}";
        }

        cmd += "\r\n";

        var status = SendRequest(cmd);
        if (status != RequestSendStatus.Okay)
        {
            return ModuleOpenStatus.ErrorSendCommand;
        }

        CBufferChain idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        string response = new(idata.Data);
        if (!string.Equals(response, CConsts.WDEP_STATUS_200, StringComparison.InvariantCulture))
        {
            if (string.Equals(response, CConsts.WDEP_STATUS_404, StringComparison.InvariantCulture))
            {
                module.FileNotFound = true;
                return ModuleOpenStatus.ErrorFileNotFound;
            }
            else if (string.Equals(response, CConsts.WDEP_STATUS_403, StringComparison.InvariantCulture))
            {
                module.Invalid = true;
                return ModuleOpenStatus.ErrorCannotReadFileHeaders;
            }
            else if (string.Equals(response, CConsts.WDEP_STATUS_415, StringComparison.InvariantCulture))
            {
                module.Invalid = true;
                return ModuleOpenStatus.ErrorInvalidHeadersOrSignatures;
            }

            return ModuleOpenStatus.ErrorUnspecified;
        }

        idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        response = idata.BufferToString();
        if (string.IsNullOrEmpty(response))
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        var dataObject = (CCoreFileInformationRoot)DeserializeDataJSON(typeof(CCoreFileInformationRoot), response);
        if (dataObject != null && dataObject.FileInformation != null)
        {
            var fileInformation = dataObject.FileInformation;

            module.ModuleData.Attributes = (FileAttributes)fileInformation.FileAttributes;
            module.ModuleData.RealChecksum = fileInformation.RealChecksum;
            module.ModuleData.FileSize = fileInformation.FileSizeLow | ((ulong)fileInformation.FileSizeHigh << 32);

            long fileTime = ((long)fileInformation.LastWriteTimeHigh << 32) | fileInformation.LastWriteTimeLow;
            module.ModuleData.FileTimeStamp = DateTime.FromFileTime(fileTime);

            return ModuleOpenStatus.Okay;
        }

        return ModuleOpenStatus.ErrorUnspecified;
    }

    /// <summary>
    /// Close previously opened module.
    /// </summary>
    /// <returns></returns>
    public bool CloseModule()
    {
        return SendRequest("close\r\n") == RequestSendStatus.Okay;
    }

    public bool ExitRequest()
    {
        return SendRequest("exit\r\n") == RequestSendStatus.Okay;
    }

    public bool ShudownRequest()
    {
        return SendRequest("shutdown\r\n") == RequestSendStatus.Okay;
    }

    public object GetModuleInformationByType(ModuleInformationType moduleInformationType, string parameters = null)
    {
        string cmd;
        bool preProcessData = false;
        Type objectType;

        switch (moduleInformationType)
        {
            case ModuleInformationType.Headers:
                cmd = "headers\r\n";
                objectType = typeof(CCoreStructsRoot);
                break;
            case ModuleInformationType.Imports:
                preProcessData = true;
                cmd = "imports\r\n";
                objectType = typeof(CCoreImportsRoot);
                break;
            case ModuleInformationType.Exports:
                preProcessData = true;
                cmd = "exports\r\n";
                objectType = typeof(CCoreExportsRoot);
                break;
            case ModuleInformationType.DataDirectories:
                cmd = "datadirs\r\n";
                objectType = typeof(CCoreDataDirectoryRoot);
                break;
            case ModuleInformationType.ApiSetName:
                cmd = $"apisetresolve {parameters}\r\n";
                objectType = typeof(CCoreResolvedFileNameRoot);
                break;
            default:
                return null;
        }

        return SendCommandAndReceiveReplyAsObjectJSON(cmd, objectType, preProcessData);
    }

    /*
        public CCoreDataDirectoryRoot GetModuleDataDirectories(CModule module)
        {
            //fixme
            return (CCoreDataDirectoryRoot)GetModuleInformationByType(ModuleInformationType.DataDirectories);
        }
    */

    public CCoreApiSetNamespaceInfo GetApiSetNamespaceInfo()
    {
        string cmd = "apisetnsinfo\r\n";
        var rootObject = (CCoreApiSetNamespaceInfoRoot)SendCommandAndReceiveReplyAsObjectJSON(cmd, typeof(CCoreApiSetNamespaceInfoRoot));
        if (rootObject != null)
            return rootObject.Namespace;

        return null;
    }

    public CCoreCallStats GetCoreCallStats()
    {
        string cmd = "callstats\r\n";
        var rootObject = (CCoreCallStatsRoot)SendCommandAndReceiveReplyAsObjectJSON(cmd, typeof(CCoreCallStatsRoot));
        if (rootObject != null)
            return rootObject.CallStats;

        return null;
    }

    public CCoreServStats GetCoreServStats()
    {
        var rootObject = (CCoreServStatsRoot)SendCommandAndReceiveReplyAsObjectJSON("servstats\r\n", typeof(CCoreServStatsRoot));
        if (rootObject != null)
            return rootObject.ServStats;

        return null;
    }

    public bool GetModuleHeadersInformation(CModule module)
    {
        if (module == null)
        {
            return false;
        }

        var dataObject = (CCoreStructsRoot)GetModuleInformationByType(ModuleInformationType.Headers);
        if (dataObject == null)
        {
            return false;
        }

        var fh = dataObject.HeadersInfo;
        if (fh == null)
        {
            return false;
        }

        //
        // Move data.
        //
        CModuleData moduleData = module.ModuleData;

        // Set various module data properties
        moduleData.LinkerVersion = $"{fh.OptionalHeader.MajorLinkerVersion}.{fh.OptionalHeader.MinorLinkerVersion}";
        moduleData.SubsystemVersion = $"{fh.OptionalHeader.MajorSubsystemVersion}.{fh.OptionalHeader.MinorSubsystemVersion}";
        moduleData.ImageVersion = $"{fh.OptionalHeader.MajorImageVersion}.{fh.OptionalHeader.MinorImageVersion}";
        moduleData.OSVersion = $"{fh.OptionalHeader.MajorOperatingSystemVersion}.{fh.OptionalHeader.MinorOperatingSystemVersion}";
        moduleData.LinkChecksum = fh.OptionalHeader.CheckSum;
        moduleData.Machine = fh.FileHeader.Machine;
        moduleData.LinkTimeStamp = fh.FileHeader.TimeDateStamp;
        moduleData.Characteristics = fh.FileHeader.Characteristics;
        moduleData.Subsystem = fh.OptionalHeader.Subsystem;
        moduleData.VirtualSize = fh.OptionalHeader.SizeOfImage;
        moduleData.PreferredBase = fh.OptionalHeader.ImageBase;
        moduleData.DllCharacteristics = fh.OptionalHeader.DllCharacteristics;
        moduleData.ExtendedCharacteristics = fh.ExtendedDllCharacteristics;

        if (fh.FileVersion != null)
        {
            moduleData.FileVersion = $"{fh.FileVersion.FileVersionMS.HIWORD()}." +
                $"{fh.FileVersion.FileVersionMS.LOWORD()}." +
                $"{fh.FileVersion.FileVersionLS.HIWORD()}." +
                $"{fh.FileVersion.FileVersionLS.LOWORD()}";

            moduleData.ProductVersion = $"{fh.FileVersion.ProductVersionMS.HIWORD()}." +
                $"{fh.FileVersion.ProductVersionMS.LOWORD()}." +
                $"{fh.FileVersion.ProductVersionLS.HIWORD()}." +
                $"{fh.FileVersion.ProductVersionLS.LOWORD()}";
        }

        //
        // Remember debug directory types.
        //
        if (fh.DebugDirectory != null)
        {
            foreach (var entry in fh.DebugDirectory)
            {
                moduleData.DebugDirTypes.Add(entry.Type);
                if (entry.Type == (uint)DebugEntryType.Reproducible)
                {
                    module.IsReproducibleBuild = true;
                }
            }
        }

        if (!string.IsNullOrEmpty(fh.Base64Manifest))
        {
            module.ManifestData = fh.Base64Manifest;
        }

        return true;
    }

    public void GetModuleImportExportInformation(CModule module,
                                                 List<SearchOrderType> searchOrderUM,
                                                 List<SearchOrderType> searchOrderKM)
    {
        //
        // Process exports.
        //
        CCoreExports rawExports;
        CCoreExportsRoot exportsObject = (CCoreExportsRoot)GetModuleInformationByType(ModuleInformationType.Exports);
        if (exportsObject != null && exportsObject.Export != null)
        {
            rawExports = exportsObject.Export;
            foreach (var entry in rawExports.Library.Function)
            {
                module.ModuleData.Exports.Add(new(entry));
            }

            foreach (var entry in module.ParentImports)
            {
                bool bResolved = false;

                if (entry.Ordinal != UInt32.MaxValue)
                {
                    bResolved = module.ModuleData.Exports?.Any(func => func.Ordinal == entry.Ordinal) == true;
                }
                else
                {
                    bResolved = module.ModuleData.Exports?.Any(func => func.RawName.Equals(entry.RawName, StringComparison.Ordinal)) == true;
                }

                if (!bResolved)
                {
                    module.ExportContainErrors = true;
                    break;
                }
            }
        }

        //
        // Process imports.
        //
        CCoreImports rawImports;
        CCoreImportsRoot importsObject = (CCoreImportsRoot)GetModuleInformationByType(ModuleInformationType.Imports);
        if (importsObject != null && importsObject.Import != null)
        {
            rawImports = importsObject.Import;

            // Query if this is kernel module.
            // If not flag already set (propagated from parent module)
            // File have:
            //    1. native subsystem
            //    2. no ntdll.dll/kernel32.dll in imports
            //    3. one of the hardcoded kernel modules in imports
            //
            if (!module.IsKernelModule)
            {
                if (module.ModuleData.Subsystem == NativeMethods.IMAGE_SUBSYSTEM_NATIVE &&
                !rawImports.Library.Any(entry => entry.Name.Equals(CConsts.NtdllDll, StringComparison.OrdinalIgnoreCase) ||
                                                 entry.Name.Equals(CConsts.Kernel32Dll, StringComparison.OrdinalIgnoreCase)) &&
                rawImports.Library.Any(entry => entry.Name.Equals(CConsts.NtoskrnlExe, StringComparison.OrdinalIgnoreCase) ||
                                                entry.Name.Equals(CConsts.HalDll, StringComparison.OrdinalIgnoreCase) ||
                                                entry.Name.Equals(CConsts.KdComDll, StringComparison.OrdinalIgnoreCase) ||
                                                entry.Name.Equals(CConsts.BootVidDll, StringComparison.OrdinalIgnoreCase)))
                {
                    module.IsKernelModule = true;
                }
            }

            foreach (var entry in rawImports.Library)
            {
                string moduleName = entry.Name;
                string rawModuleName = entry.Name;

                bool isApiSetContract = IsModuleNameApiSetContract(moduleName);
                CCoreResolvedFileName resolvedName = null;

                if (isApiSetContract)
                {
                    string cachedName = CApiSetCacheManager.GetResolvedNameByApiSetName(moduleName);

                    if (cachedName == null)
                    {
                        var resolvedNameRoot = (CCoreResolvedFileNameRoot)GetModuleInformationByType(ModuleInformationType.ApiSetName, moduleName);

                        if (resolvedNameRoot != null && resolvedNameRoot.FileName != null)
                        {
                            resolvedName = resolvedNameRoot.FileName;
                            CApiSetCacheManager.AddApiSet(moduleName, resolvedName.Name);
                            moduleName = resolvedName.Name;
                        }
                    }
                    else
                    {
                        moduleName = cachedName;
                    }

                }

                var moduleFileName = CPathResolver.ResolvePathForModule(moduleName,
                                                                        module,
                                                                        searchOrderUM,
                                                                        searchOrderKM,
                                                                        out SearchOrderType resolvedBy);

                if (!string.IsNullOrEmpty(moduleFileName))
                {
                    moduleName = moduleFileName;
                }

                CModule dependent = new(moduleName, rawModuleName, resolvedBy, isApiSetContract)
                {
                    IsDelayLoad = (entry.IsDelayLibrary == 1),
                    IsKernelModule = module.IsKernelModule, //propagate from parent
                };

                module.Dependents.Add(dependent);

                foreach (var func in entry.Function)
                {
                    dependent.ParentImports.Add(new CFunction(func));
                }
            }
        }

    }

    public bool SetApiSetSchemaNamespaceUse(string fileName)
    {
        string cmd = "apisetmapsrc";

        if (!string.IsNullOrEmpty(fileName))
        {
            cmd += $" file \"{fileName}\"";
        }

        cmd += "\r\n";

        var status = SendRequest(cmd);

        if (status != RequestSendStatus.Okay || !IsRequestSuccessful())
        {
            return false;
        }

        return true;
    }

    private bool GetKnownDllsByType(string command, List<string> knownDllsList, out string knownDllsPath)
    {
        CCoreKnownDllsRoot rootObject = (CCoreKnownDllsRoot)SendCommandAndReceiveReplyAsObjectJSON(command, typeof(CCoreKnownDllsRoot), true);
        if (rootObject != null && rootObject.KnownDlls != null)
        {
            knownDllsList.Clear();
            knownDllsList.AddRange(rootObject.KnownDlls.Entries);
            knownDllsPath = rootObject.KnownDlls.DllPath;
            return true;
        }

        knownDllsPath = string.Empty;
        return false;
    }

    public bool GetKnownDllsAll(List<string> knownDlls, List<string> knownDlls32, out string knownDllsPath, out string knownDllsPath32)
    {
        if (knownDlls == null || knownDlls32 == null)
        {
            knownDllsPath = string.Empty;
            knownDllsPath32 = string.Empty;
            return false;
        }

        bool result32 = GetKnownDllsByType("knowndlls 32\r\n", knownDlls32, out knownDllsPath32);
        bool result64 = GetKnownDllsByType("knowndlls 64\r\n", knownDlls, out knownDllsPath);

        return result32 && result64;
    }

    public bool ConnectClient()
    {
        bool bFailure = false;
        string errMessage = string.Empty;

        ServerProcess = null;

        try
        {
            string fileName = GetServerApplication();
            string processName = Path.GetFileNameWithoutExtension(fileName);

            Process[] processList = Process.GetProcessesByName(processName);

            foreach (Process process in processList)
            {
                try
                {
                    var name = Path.GetFileName(process.MainModule.FileName);
                    if (Path.GetFileName(fileName).Equals(name))
                    {
                        ServerProcess = process;
                        break;
                    }

                }
                catch
                {
                }

            }

            if (ServerProcess == null)
            {
                if (!File.Exists(fileName))
                {
                    throw new FileNotFoundException(fileName);
                }

                ProcessStartInfo processInfo = new()
                {
                    FileName = $"\"{fileName}\"",
                    UseShellExecute = false
                };
                ServerProcess = Process.Start(processInfo);
            }

            if (ServerProcess == null)
            {
                throw new Exception("Core process start failure");
            }
            else
            {
                ClientConnection = new();
                ClientConnection.Connect(IPAddress, PortNumber);
                if (ClientConnection.Connected)
                {
                    DataStream = ClientConnection.GetStream();
                }
            }

        }
        catch (SocketException ex)
        {
            bFailure = true;
            errMessage = ex.Message;
        }
        catch (FileNotFoundException ex)
        {
            bFailure = true;
            errMessage = $"{ex.Message} was not found, make sure it exist or change path to it: " +
                $"Main menu -> Options -> Configuration, select Server tab, specify server application location and then press Connect button.";
        }
        catch (Exception ex)
        {
            bFailure = true;
            errMessage = ex.Message;
        }

        if (bFailure)
        {
            if (ServerProcess != null && !ServerProcess.HasExited)
            {
                ServerProcess.Kill();
                ServerProcess = null;
            }
            LogEvent(null, LogEventType.CoreServerStartError, errMessage);
        }
        else
        {
            CBufferChain idata = ReceiveReply();
            if (idata != null)
            {
                LogEvent(null, LogEventType.CoreServerStartOK, new string(idata.Data));
            }
            else
            {
                LogEvent(null, LogEventType.CoreServerStartError, "Missing server HELLO");
            }
        }
        return ServerProcess != null;
    }

    public void DisconnectClient()
    {
        if (ServerProcess == null || ServerProcess.HasExited)
        {
            return;
        }

        ExitRequest();

        DataStream?.Close();
    }

}
