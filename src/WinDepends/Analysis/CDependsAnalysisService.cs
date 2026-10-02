/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CDEPENDSANALYSISSERVICE.CS
*
*  VERSION:     1.00
*
*  DATE:        02 Oct 2026
*
*  CDepends analysis service.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Numerics;
using System.Reflection.PortableExecutable;

namespace WinDepends;

internal sealed class CDependsAnalysisService
{
    private readonly CCoreClient _coreClient;

    public CDependsAnalysisService(CCoreClient coreClient)
    {
        ArgumentNullException.ThrowIfNull(coreClient);

        _coreClient = coreClient;
    }

    private static void ReportModuleProgress(
    CDependsAnalysisContext context,
    CDependsAnalysisProgressStage stage,
    CModule module)
    {
        context.ReportProgress?.Invoke(new CDependsAnalysisProgress(
            stage,
            module.FileName,
            -1));
    }

    public CDependsAnalysisScope BeginAnalysis(
        string rootFileName, 
        CDependsAnalysisContext context)
    {
        CActCtxHelper activationContext;

        ArgumentException.ThrowIfNullOrWhiteSpace(rootFileName);
        ArgumentNullException.ThrowIfNull(context);

        CPathResolver.Initialized = false;

        activationContext = new CActCtxHelper(rootFileName);
        CPathResolver.ActCtxHelper = activationContext;

        return new CDependsAnalysisScope(activationContext, context);
    }

    public void ProcessModule(
        CModule module,
        CFileOpenSettings fileOpenSettings,
        bool currentModuleIsRoot,
        CDependsAnalysisContext context)
    {
        CFileOpenSettings effectiveSettings;
        ModuleOpenStatus openStatus;

        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(fileOpenSettings);
        ArgumentNullException.ThrowIfNull(context);

        effectiveSettings = new CFileOpenSettings(fileOpenSettings);

        //
        // If this is a dependency and propagation is disabled, reset to defaults.
        //
        if (!currentModuleIsRoot &&
            !fileOpenSettings.PropagateSettingsOnDependencies)
        {
            effectiveSettings.ProcessRelocsForImage = false;
            effectiveSettings.UseStats = false;
            effectiveSettings.UseCustomImageBase = false;
            effectiveSettings.CustomImageBase = 0;
        }

        module.InstanceId = module.GetHashCode();

        ReportModuleProgress(
            context,
            CDependsAnalysisProgressStage.OpeningModule,
            module);

        openStatus = _coreClient.OpenModule(
            ref module,
            effectiveSettings);

        if (openStatus != ModuleOpenStatus.Okay)
        {
            ReportModuleProgress(
                context,
                CDependsAnalysisProgressStage.ModuleProcessingFailed,
                module);
        }

        HandleModuleOpenStatus(
            module,
            openStatus,
            effectiveSettings,
            currentModuleIsRoot,
            context);
    }

    public TreeNode? PopulateDependencyTree(
        CModule rootModule,
        CFileOpenSettings fileOpenSettings,
        CDependsModuleProcessor processModule,
        Action<CDependsAnalysisProgress> reportProgress)
    {
        ArgumentNullException.ThrowIfNull(rootModule);
        ArgumentNullException.ThrowIfNull(fileOpenSettings);
        ArgumentNullException.ThrowIfNull(processModule);
        ArgumentNullException.ThrowIfNull(reportProgress);

        return PopulateTreeCore(
            rootModule,
            (module, parentNode) => processModule(
                module,
                parentNode,
                fileOpenSettings),
            reportProgress);
    }

    public TreeNode? PopulateSessionTree(
        CModule rootModule,
        CDependsSessionModuleProcessor processModule,
        Action<CDependsAnalysisProgress> reportProgress)
    {
        ArgumentNullException.ThrowIfNull(rootModule);
        ArgumentNullException.ThrowIfNull(processModule);
        ArgumentNullException.ThrowIfNull(reportProgress);

        return PopulateTreeCore(
            rootModule,
            (module, parentNode) => processModule(
                module,
                parentNode),
            reportProgress);
    }

    private static TreeNode? PopulateTreeCore(
        CModule rootModule,
        Func<CModule, TreeNode?, TreeNode?> processModule,
        Action<CDependsAnalysisProgress> reportProgress)
    {
        List<TreeNode> baseNodes = [];

        reportProgress(new CDependsAnalysisProgress(
            CDependsAnalysisProgressStage.Populating,
            rootModule.FileName,
            0));

        TreeNode? rootNode = processModule(
            rootModule,
            null);

        if (rootNode == null)
            return null;

        foreach (CModule importModule in rootModule.Dependents)
        {
            TreeNode? addedNode;

            reportProgress(new CDependsAnalysisProgress(
                CDependsAnalysisProgressStage.Populating,
                importModule.FileName,
                1));

            addedNode = processModule(
                importModule,
                rootNode);

            if (addedNode != null)
            {
                baseNodes.Add(addedNode);
            }
        }

        foreach (TreeNode node in baseNodes)
        {
            if (node.Tag is not CModule nodeModule)
                continue;

            foreach (CModule dependent in nodeModule.Dependents)
            {

                PopulateDependentModulesCore(
                    dependent,
                    node,
                    processModule,
                    reportProgress,
                    2);
            }
        }

        return rootNode;
    }

    private static void PopulateDependentModulesCore(
        CModule module,
        TreeNode parentNode,
        Func<CModule, TreeNode?, TreeNode?> processModule,
        Action<CDependsAnalysisProgress> reportProgress,
        int depth)
    {
        reportProgress(new CDependsAnalysisProgress(
            CDependsAnalysisProgressStage.Populating,
            module.FileName,
            depth));

        TreeNode? treeNode = processModule(
            module,
            parentNode);

        if (treeNode == null)
            return;

        foreach (CModule dependentModule in module.Dependents)
        {
            PopulateDependentModulesCore(
                dependentModule,
                treeNode,
                processModule,
                reportProgress,
                depth + 1);
        }
    }

    public void HandleModuleOpenStatus(
        CModule module,
        ModuleOpenStatus openStatus,
        CFileOpenSettings settings,
        bool currentModuleIsRoot,
        CDependsAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(context);

        switch (openStatus)
        {
            case ModuleOpenStatus.Okay:

                HandleOpenedModule(
                    module,
                    settings,
                    currentModuleIsRoot,
                    context);
                break;

            case ModuleOpenStatus.ErrorUnspecified:
                context.AddLogMessage(
                    $"Module \"{module.FileName}\" analysis failed.",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorSendCommand:
                context.AddLogMessage(
                    $"Send command has failed for module \"{module.FileName}\".",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorReceivedDataInvalid:
                context.AddLogMessage(
                    $"Received invalid data for module \"{module.FileName}\".",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorFileNotMapped:
                context.AddLogMessage(
                    $"Server failed to map input module \"{module.FileName}\".",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorCannotReadFileHeaders:
                context.AddLogMessage(
                    $"Server failed to read headers of module \"{module.FileName}\".",
                    LogMessageType.ErrorOrWarning,
                    null,
                    true,
                    true,
                    module);
                break;

            case ModuleOpenStatus.ErrorInvalidHeadersOrSignatures:
                HandleInvalidHeadersOrSignatures(module, context);
                break;

            case ModuleOpenStatus.ErrorFileNotFound:
                HandleFileNotFound(module, context);
                break;
        }
    }

    private void HandleOpenedModule(
        CModule module,
        CFileOpenSettings settings,
        bool currentModuleIsRoot,
        CDependsAnalysisContext context)
    {
        CCoreCallStats? stats = null;

        ReportModuleProgress(context, CDependsAnalysisProgressStage.ReadingHeaders, module);

        module.IsProcessed = _coreClient.GetModuleHeadersInformation(module);

        //
        // If this is root module, setup resolver.
        //
        if (currentModuleIsRoot)
        {
            CPathResolver.QueryFileInformation(module);
        }

        ReportModuleProgress(context, CDependsAnalysisProgressStage.ReadingImportsAndExports, module);

        _coreClient.GetModuleImportExportInformation(
            module,
            context.Configuration.SearchOrderListUM,
            context.Configuration.SearchOrderListKM,
            context.ParentImportsHashTable,
            settings.EnableExperimentalFeatures,
            settings.ExpandForwarders);

        //
        // Collect forwarders if exists.
        // Has local settings priority over global.
        //
        if (settings.ExpandForwarders)
        {
            ReportModuleProgress(context, CDependsAnalysisProgressStage.ExpandingForwarders, module);

            _coreClient.ExpandAllForwarderModules(
                module,
                context.Configuration.SearchOrderListUM,
                context.Configuration.SearchOrderListKM,
                context.ParentImportsHashTable);

            // Validate forwarded exports after expansion.
            _coreClient.ValidateForwardedExports(module);
        }

        if (settings.UseStats)
        {
            ReportModuleProgress(context, CDependsAnalysisProgressStage.ReadingStatistics, module);
            stats = _coreClient.GetCoreCallStats();
        }

        _coreClient.CloseModule();

        //
        // Display statistics.
        //
        if (settings.UseStats && stats != null)
        {
            LogModuleStats(stats, module.FileName, context);
        }

        if (module.ExportContainErrors)
        {
            context.AddLogMessage(
                $"Module \"{module.FileName}\" contains export errors.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }

        // Add warning for modules with forwarding issues.
        if (module.OtherErrorsPresent &&
            module.ForwarderEntries?.Count > 0)
        {
            context.AddLogMessage(
                $"Module \"{Path.GetFileName(module.FileName)}\" has unresolved forwarded exports.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }

        bool isCpuMismatch = IsCpuMismatchForAnalysis(
            module,
            context.RootModule);

        if (isCpuMismatch)
        {
            module.OtherErrorsPresent = true;

            context.AddLogMessage(
                $"Module \"{module.FileName}\" with different CPU type was found.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }

        // Skip this message for kernel modules, dotnet files, and when relocation processing is disabled.
        if (module.ModuleData.ImageFixed != 0 &&
            !module.IsKernelModule &&
            module.ModuleData.ImageDotNet != 1 &&
            settings.ProcessRelocsForImage)
        {
            module.OtherErrorsPresent = true;

            context.AddLogMessage(
                $"Module \"{Path.GetFileName(module.FileName)}\" has no relocations.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }

        if (!module.IsProcessed)
        {
            context.AddLogMessage(
                $"Module \"{module.FileName}\" was not fully processed.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }
    }

    private static void HandleInvalidHeadersOrSignatures(
        CModule module,
        CDependsAnalysisContext context)
    {
        if (module.IsDelayLoad)
        {
            context.AddLogMessage(
                $"Delay-load module \"{module.FileName}\" has invalid headers or signatures.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }
        else
        {
            context.AddLogMessage(
                $"Module \"{module.FileName}\" has invalid headers or signatures.",
                LogMessageType.ErrorOrWarning,
                null,
                true,
                true,
                module);
        }
    }

    private static void HandleFileNotFound(
        CModule module,
        CDependsAnalysisContext context)
    {
        bool isExtensionApiSet = module.IsApiSetContract &&
            module.RawFileName.StartsWith(
                "EXT-",
                StringComparison.OrdinalIgnoreCase);

        string messageText;
        LogMessageType messageType = isExtensionApiSet
            ? LogMessageType.Information
            : LogMessageType.ErrorOrWarning;

        if (module.IsDelayLoad)
        {
            messageText = isExtensionApiSet
                ? $"Delay-load extension apiset module \"{module.FileName}\" was not found."
                : $"Delay-load dependency module \"{module.FileName}\" was not found.";
        }
        else
        {
            messageText = isExtensionApiSet
                ? $"Extension apiset module \"{module.FileName}\" was not found."
                : $"Required implicit or forwarded dependency \"{module.FileName}\" was not found.";
        }

        context.AddLogMessage(
            messageText,
            messageType,
            null,
            true,
            true,
            module);
    }

    private static bool IsCpuMismatchForAnalysis(
        CModule module,
        CModule rootModule)
    {
        bool isRootImageDotNet;

        if (module == null || rootModule == null)
            return false;

        isRootImageDotNet = rootModule.ModuleData.ImageDotNet == 1;
        if (isRootImageDotNet)
            return false;

        if (module.ModuleData.Machine == rootModule.ModuleData.Machine)
            return false;

        if (IsManagedAnyCpuLike(module))
            return false;

        return true;
    }

    private static bool IsManagedAnyCpuLike(CModule module)
    {
        CorFlags flags;
        Machine machine;
        bool ilOnly;
        bool requires32Bit;

        if (module == null)
            return false;

        if (module.ModuleData.ImageDotNet != 1)
            return false;

        try
        {
            using FileStream fileStream = new(
                module.FileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            using PEReader peReader = new(fileStream);

            var corHeader = peReader.PEHeaders?.CorHeader;
            if (corHeader == null)
                return false;

            flags = corHeader.Flags;
            ilOnly = (flags & CorFlags.ILOnly) != 0;
            requires32Bit = (flags & CorFlags.Requires32Bit) != 0;
            machine = peReader.PEHeaders.CoffHeader.Machine;

            return machine == Machine.I386 &&
                ilOnly &&
                !requires32Bit;
        }
        catch
        {
            return false;
        }
    }

    private static void LogModuleStats(
        CCoreCallStats stats,
        string moduleFileName,
        CDependsAnalysisContext context)
    {
        string statsData;

        if (stats == null)
            return;

        statsData = $"[STATS {Path.GetFileName(moduleFileName)}] Received: " +
            $"{FormatByteSize(stats.TotalBytesSent)}, " +
            $"\"send\" calls: {stats.TotalSendCalls}, " +
            $"\"send\" time spent (\u00B5s): {stats.TotalTimeSpent}";

        context.AddLogMessage(
            statsData,
            LogMessageType.ContentDefined,
            Color.Purple,
            true,
            false);
    }

    private static string FormatByteSize(ulong bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024 * 1024)} MB",
            >= 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes} byte"
        };
    }
}
