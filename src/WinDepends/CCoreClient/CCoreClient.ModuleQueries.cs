/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       CCORECLIENT.MODULEQUERIES.CS
*
*  VERSION:     1.00
*
*  DATE:        23 May 2026
*  
*  Module and server query routines for 
*  Core Server communication class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

public partial class CCoreClient
{
    /// <summary>
    /// Resolves an API Set contract name to its implementation module name.
    /// </summary>
    /// <param name="moduleName">The API Set contract name to resolve.</param>
    /// <param name="contextModule">The module context for resolution.</param>
    /// <returns>The resolved module name, or the original name if resolution fails.</returns>
    private string ResolveApiSetName(string moduleName, CModule contextModule)
    {
        // Try cache first
        string resolvedName = CApiSetCacheManager.GetResolvedNameByApiSetName(moduleName);
        if (resolvedName != null)
            return resolvedName;

        // Cache miss - resolve via server request
        var resolvedInfo = (CCoreResolvedFileName)GetModuleInformationByType(
            ModuleInformationType.ApiSetName, contextModule, moduleName);

        if (resolvedInfo != null)
        {
            resolvedName = resolvedInfo.Name;
            CApiSetCacheManager.AddApiSet(moduleName, resolvedName);
            return resolvedName;
        }

        return moduleName;
    }

    /// <summary>
    /// Opens a COFF module on the server for analysis.
    /// </summary>
    /// <param name="module">The module to open.</param>
    /// <param name="settings">Settings for opening the module.</param>
    /// <returns>A <see cref="ModuleOpenStatus"/> indicating the result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when module is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
    public ModuleOpenStatus OpenModule(ref CModule module, CFileOpenSettings settings)
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        ThrowIfDisposed();

        var openRequest = CCoreProtocolMapper.BuildOpenModuleRequest(module, settings);
        if (!SendRequest(openRequest))
        {
            return ModuleOpenStatus.ErrorSendCommand;
        }

        CBufferChain idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        var openStatus = CCoreProtocolMapper.CreateStatusResponse(idata);
        var openResult = CCoreProtocolMapper.MapOpenModuleStatus(openStatus, module);
        if (openResult != ModuleOpenStatus.Okay)
        {
            return openResult;
        }

        idata = ReceiveReply();
        if (IsNullOrEmptyResponse(idata))
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        var payloadResponse = CCoreProtocolMapper.CreatePayloadResponse(idata);
        if (payloadResponse.IsEmpty)
        {
            return ModuleOpenStatus.ErrorReceivedDataInvalid;
        }

        var fileInformation = (CCoreFileInformation)DeserializeDataJSON(typeof(CCoreFileInformation), payloadResponse.Value);
        return CCoreDomainMapper.ApplyFileInformation(module, fileInformation);
    }

    /// <summary>
    /// Closes the currently opened module on the server.
    /// </summary>
    /// <returns>true if the close command was sent successfully; otherwise, false.</returns>
    public bool CloseModule()
    {
        return SendRequest(CConsts.CMD_CLOSE);
    }

    /// <summary>
    /// Retrieves module information of the specified type from the server.
    /// </summary>
    /// <param name="moduleInformationType">The type of information to retrieve.</param>
    /// <param name="module">The module context.</param>
    /// <param name="parameters">Optional parameters for the request.</param>
    /// <returns>The requested module information object, or null if the request fails.</returns>
    public object GetModuleInformationByType(ModuleInformationType moduleInformationType, CModule module, string parameters = null)
    {
        if (!CCoreProtocolMapper.TryBuildModuleInformationRequest(
                    moduleInformationType,
                    parameters,
                    out var request,
                    out var responseType))
        {
            return null;
        }

        return SendCommandAndReceiveReplyAsObjectJSON(request.Command, responseType, module);
    }

    /// <summary>
    /// Gets the data directories for the specified module.
    /// </summary>
    /// <param name="module">The module to query.</param>
    /// <returns>A list of data directory entries, or null if the request fails.</returns>
    public List<CCoreDirectoryEntry> GetModuleDataDirectories(CModule module)
    {
        return (List<CCoreDirectoryEntry>)GetModuleInformationByType(ModuleInformationType.DataDirectories, module);
    }

    /// <summary>
    /// Gets API Set namespace information from the server.
    /// </summary>
    /// <param name="apiSetFilePath">Optional path to ApiSet schema file.  If null, queries current server ApiSet.</param>
    /// <returns>API Set namespace information, or null if the request fails.</returns>
    public CCoreApiSetNamespaceInfo GetApiSetNamespaceInfo(string apiSetFilePath = null)
    {
        var request = CCoreProtocolMapper.BuildApiSetNamespaceInfoRequest(apiSetFilePath);
        return (CCoreApiSetNamespaceInfo)SendCommandAndReceiveReplyAsObjectJSON(
                    request.Command, typeof(CCoreApiSetNamespaceInfo), null);
    }

    /// <summary>
    /// Sets the API Set schema namespace source for the server.
    /// </summary>
    /// <param name="fileName">Path to the API Set schema file, or null/empty to use default.</param>
    /// <returns>true if the command was sent and acknowledged successfully; otherwise, false.</returns>
    public bool SetApiSetSchemaNamespaceUse(string fileName)
    {
        var request = CCoreProtocolMapper.BuildApiSetSchemaNamespaceUseRequest(fileName);
        return SendRequest(request) && IsRequestSuccessful();
    }

    /// <summary>
    /// Gets call statistics from the server.
    /// </summary>
    /// <returns>Call statistics information, or null if the request fails.</returns>
    public CCoreCallStats GetCoreCallStats()
    {
        return (CCoreCallStats)SendCommandAndReceiveReplyAsObjectJSON(
              CConsts.CMD_CALLSTATS, typeof(CCoreCallStats), null);
    }

    /// <summary>
    /// Retrieves and populates module header information.
    /// </summary>
    /// <param name="module">The module to populate with header information.</param>
    /// <returns>true if headers were retrieved successfully; otherwise, false.</returns>
    public bool GetModuleHeadersInformation(CModule module)
    {
        if (module == null)
        {
            return false;
        }

        var fh = (CCoreImageHeaders)GetModuleInformationByType(ModuleInformationType.Headers, module);
        if (fh == null)
        {
            return false;
        }

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
        else
        {
            moduleData.FileVersion = "N/A";
            moduleData.ProductVersion = "N/A";
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

    /// <summary>
    /// Retrieves Known DLLs information of a specific type from the server.
    /// </summary>
    /// <param name="command">The command to send to the server.</param>
    /// <param name="knownDllsList">List to populate with Known DLL names.</param>
    /// <param name="knownDllsPath">Outputs the Known DLLs directory path.</param>
    /// <returns>true if the information was retrieved successfully; otherwise, false.</returns>
    private bool GetKnownDllsByType(string command, List<string> knownDllsList, out string knownDllsPath)
    {
        if (knownDllsList == null)
        {
            knownDllsPath = string.Empty;
            return false;
        }

        if (SendCommandAndReceiveReplyAsObjectJSON(command, typeof(CCoreKnownDlls), null)
            is CCoreKnownDlls knownDllsObject)
        {
            knownDllsList.Clear();
            if (knownDllsObject.Entries != null)
            {
                knownDllsList.AddRange(knownDllsObject.Entries);
            }
            knownDllsPath = knownDllsObject.DllPath ?? string.Empty;
            return true;
        }

        knownDllsPath = string.Empty;
        return false;
    }

    /// <summary>
    /// Retrieves both 32-bit and 64-bit Known DLLs information from the server.
    /// </summary>
    /// <param name="knownDlls">List to populate with 64-bit Known DLL names.</param>
    /// <param name="knownDlls32">List to populate with 32-bit Known DLL names.</param>
    /// <param name="knownDllsPath">Outputs the 64-bit Known DLLs directory path.</param>
    /// <param name="knownDllsPath32">Outputs the 32-bit Known DLLs directory path.</param>
    /// <returns>true if both 32-bit and 64-bit information was retrieved successfully; otherwise, false.</returns>
    public bool GetKnownDllsAll(List<string> knownDlls, List<string> knownDlls32, out string knownDllsPath, out string knownDllsPath32)
    {
        if (knownDlls == null || knownDlls32 == null)
        {
            knownDllsPath = string.Empty;
            knownDllsPath32 = string.Empty;
            return false;
        }

        bool result32 = GetKnownDllsByType(CConsts.CMD_KNOWNDLLS32, knownDlls32, out knownDllsPath32);
        bool result64 = GetKnownDllsByType(CConsts.CMD_KNOWNDLLS64, knownDlls, out knownDllsPath);

        return result32 && result64;
    }
}
