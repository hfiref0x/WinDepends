/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CFUNCTION.CS
*
*  VERSION:     1.00
*
*  DATE:        26 Nov 2024
*  
*  Implementation of CFunction related classes.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

namespace WinDepends;

/// <summary>
/// Type of function, also image index in the image strip.
/// </summary>
public enum FunctionKind : ushort
{
    ImportUnresolvedFunction = 0,
    ImportUnresolvedCPlusPlusFunction,
    ImportUnresolvedOrdinal,

    ImportUnresolvedDynamicFunction,
    ImportUnresolvedDynamicCPlusPlusFunction,
    ImportUnresolvedDynamicOrdinal,

    ImportResolvedFunction,
    ImportResolvedCPlusPlusFunction,
    ImportResolvedOrdinal,

    ImportResolvedDynamicFunction,
    ImportResolvedDynamicCPlusPlusFunction,
    ImportResolvedDynamicOrdinal,

    ExportFunctionCalledByModuleInTree,
    ExportCPlusPlusFunctionCalledByModuleInTree,
    ExportOrdinalCalledByModuleInTree,

    ExportForwardedFunctionCalledByModuleInTree,
    ExportForwardedCPlusPlusFunctionCalledByModuleInTree,
    ExportForwardedOrdinalCalledByModuleInTree,

    ExportFunctionCalledAtLeastOnce,
    ExportCPlusPlusFunctionCalledAtLeastOnce,
    ExportOrdinalCalledAtLeastOnce,

    ExportForwardedFunctionCalledAtLeastOnce,
    ExportForwardedCPlusPlusFunctionCalledAtLeastOnce,
    ExportForwardedOrdinalCalledAtLeastOnce,

    ExportFunction,
    ExportCPlusPlusFunction,
    ExportOrdinal,

    ExportForwardedFunction,
    ExportForwardedCPlusPlusFunction,
    ExportForwardedOrdinal,
}

[Serializable()]
public class CFunction
{
    public string RawName { get; set; } = string.Empty;
    public string ForwardName { get; set; } = string.Empty;
    public string UndecoratedName { get; set; } = string.Empty;
    public UInt32 Ordinal { get; set; } = UInt32.MaxValue;
    public UInt32 Hint { get; set; } = UInt32.MaxValue;
    public UInt64 Address { get; set; }
    public bool IsExportFunction { get; set; }
    public FunctionKind Kind { get; set; } = FunctionKind.ImportUnresolvedFunction;

    public bool SnapByOrdinal() => (Ordinal != UInt32.MaxValue && string.IsNullOrEmpty(RawName));
    public bool IsForward() => (!string.IsNullOrEmpty(ForwardName));
    public bool IsNameDecorated() => RawName.StartsWith('?');

    public FunctionKind MakeDefaultFunctionKind()
    {
        FunctionKind result;
        bool isOrdinal = SnapByOrdinal();
        bool isForward = IsForward();
        bool isCppName = IsNameDecorated();

        if (IsExportFunction)
        {
            if (isOrdinal)
            {
                result = (isForward) ? FunctionKind.ExportForwardedOrdinal : FunctionKind.ExportOrdinal;
            }
            else if (isForward)
            {
                result = (isCppName) ? FunctionKind.ExportForwardedCPlusPlusFunction : FunctionKind.ExportForwardedFunction;
            }
            else
            {
                result = (isCppName) ? FunctionKind.ExportCPlusPlusFunction : FunctionKind.ExportFunction;
            }
        }
        else
        {
            if (isOrdinal)
            {
                result = FunctionKind.ImportResolvedOrdinal;
            }
            else if (isCppName)
            {
                result = FunctionKind.ImportResolvedCPlusPlusFunction;
            }
            else
            {
                result = FunctionKind.ImportResolvedFunction;
            }
        }

        return result;
    }

    public string UndecorateFunctionName()
    {
        if (string.IsNullOrEmpty(UndecoratedName))
        {
            UndecoratedName = NativeMethods.UndecorateFunctionName(RawName);
        }

        return UndecoratedName;
    }

    public static bool FindFunctionByOrdinal(uint Ordinal, List<CFunction> list)
    {
        if (list == null)
        {
            return false;
        }
        return list.Exists(item => item.Ordinal == Ordinal);
    }

    public static bool FindFunctionByRawName(string RawName, List<CFunction> list)
    {
        if (list == null)
        {
            return false;
        }
        return list.Exists(item => item.RawName.Equals(RawName, StringComparison.Ordinal));
    }

    public bool ResolveFunctionKind(CModule module, List<CModule> modulesList)
    {
        FunctionKind newKind;
        List<CFunction> functionList;
        bool isOrdinal = SnapByOrdinal();
        bool isForward = IsForward();
        bool isCPlusPlusName = IsNameDecorated();
        bool bResolved;

        if (module == null)
        {
            Kind = FunctionKind.ImportUnresolvedFunction;
            return false;
        }

        if (IsExportFunction)
        {
            // Export function.
            newKind = FunctionKind.ExportFunction;

            functionList = module.ParentImports;

            if (isOrdinal)
            {
                // Search by ordinal.
                bResolved = FindFunctionByOrdinal(Ordinal, functionList);
                if (bResolved)
                {
                    newKind = isForward ? FunctionKind.ExportForwardedOrdinalCalledByModuleInTree : FunctionKind.ExportOrdinalCalledByModuleInTree;
                }
                else
                {
                    newKind = isForward ? FunctionKind.ExportForwardedOrdinal : FunctionKind.ExportOrdinal;
                }

            }
            else
            {
                // Search by name first.
                bResolved = FindFunctionByRawName(RawName, functionList);
                if (!bResolved)
                {
                    // Possible imported by ordinal.
                    bResolved = FindFunctionByOrdinal(Ordinal, functionList);
                }

                if (bResolved)
                {
                    if (isCPlusPlusName)
                    {
                        newKind = isForward ? FunctionKind.ExportForwardedCPlusPlusFunctionCalledByModuleInTree : FunctionKind.ExportCPlusPlusFunctionCalledByModuleInTree;
                    }
                    else
                    {
                        newKind = isForward ? FunctionKind.ExportForwardedFunctionCalledByModuleInTree : FunctionKind.ExportFunctionCalledByModuleInTree;
                    }
                }
            }

        }
        else
        {
            // Import function.
            functionList = module.OriginalInstanceId != 0 ?
                    CUtils.InstanceIdToModule(module.OriginalInstanceId, modulesList)?.ModuleData.Exports : module.ModuleData.Exports;

            if (isOrdinal)
            {
                bResolved = FindFunctionByOrdinal(Ordinal, functionList);
            }
            else
            {
                bResolved = FindFunctionByRawName(RawName, functionList);
            }

            newKind = bResolved switch
            {
                true when isOrdinal => FunctionKind.ImportResolvedOrdinal,
                true when isCPlusPlusName => FunctionKind.ImportResolvedCPlusPlusFunction,
                true => FunctionKind.ImportResolvedFunction,
                false when isOrdinal => FunctionKind.ImportUnresolvedOrdinal,
                false when isCPlusPlusName => FunctionKind.ImportUnresolvedCPlusPlusFunction,
                false => FunctionKind.ImportUnresolvedFunction,
            };

        }

        Kind = newKind;
        return true;
    }

    public CFunction()
    {
    }

    public CFunction(string name, FunctionKind functionKind, bool isExportFunction)
    {
        RawName = name;
        IsExportFunction = isExportFunction;
        Kind = functionKind;
    }

    public CFunction(CCoreExportFunction function)
    {
        RawName = function.Name;
        ForwardName = function.Forward;

        Ordinal = function.Ordinal;
        Hint = function.Hint;
        Address = function.PointerAddress;

        IsExportFunction = true;
        Kind = MakeDefaultFunctionKind();
    }

    public CFunction(CCoreImportFunction function)
    {
        RawName = function.Name;

        Ordinal = function.Ordinal;
        Hint = function.Hint;
        Address = function.Bound;

        IsExportFunction = false;
        Kind = MakeDefaultFunctionKind();
    }

}
