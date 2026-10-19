/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2026
*
*  TITLE:       CDEPENDSDUPLICATEMODULESTATE.CS
*
*  VERSION:     1.00
*
*  DATE:        06 Oct 2026
*
*  Duplicate module state helpers.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

internal static class CDependsDuplicateModuleState
{
    public static void Apply(
        CModule duplicateModule,
        CModule originalModule)
    {
        ArgumentNullException.ThrowIfNull(duplicateModule);
        ArgumentNullException.ThrowIfNull(originalModule);

        duplicateModule.OriginalInstanceId = originalModule.InstanceId;
        duplicateModule.FileNotFound = originalModule.FileNotFound;
        duplicateModule.ExportContainErrors = originalModule.ExportContainErrors;
        duplicateModule.IsInvalid = originalModule.IsInvalid;

        // Do not copy OtherErrorsPresent from original instance, must set it directly.
        // duplicateModule.OtherErrorsPresent = originalModule.OtherErrorsPresent;

        duplicateModule.IsDotNetModule = originalModule.IsDotNetModule;
        duplicateModule.ModuleData = new(originalModule.ModuleData);
    }

    public static bool ShouldPropagateErrors(
        CModule originalModule)
    {
        bool shouldPropagate;

        ArgumentNullException.ThrowIfNull(originalModule);

        // Only propagate genuine errors, not from apiset contracts or stopped nodes.
        shouldPropagate = originalModule.ExportContainErrors ||
                          originalModule.OtherErrorsPresent ||
                          originalModule.FileNotFound;

        // Don't propagate from apiset contracts.
        if (originalModule.IsApiSetContract)
            return false;

        // Don't propagate from stopped/duplicate nodes that have forwarders
        // (these are expected to have "unprocessed" forwarders).
        if (shouldPropagate && originalModule.IsStoppedNode)
            return false;

        return shouldPropagate;
    }
}
