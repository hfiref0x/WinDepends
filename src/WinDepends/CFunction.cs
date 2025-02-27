/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CFUNCTION.CS
*
*  VERSION:     1.00
*
*  DATE:        27 Feb 2025
*  
*  Implementation of CFunction related classes.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/

using System.Runtime.Serialization;

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

public struct FunctionHashObject(string functionName, string importLibrary, UInt32 ordinal)
{
    public string FunctionName { get; set; } = functionName;
    public UInt32 FunctionOrdinal { get; set; } = ordinal;
    public string ImportLibrary { get; set; } = importLibrary;

    public readonly int GenerateUniqueKey()
    {
        unchecked
        {
            int hash = FunctionName.GetHashCode() + ImportLibrary.GetHashCode(StringComparison.OrdinalIgnoreCase);

            if (FunctionOrdinal != UInt32.MaxValue)
            {
                hash += FunctionOrdinal.GetHashCode();
            }
            return hash;
        }
    }
}

[DataContract]
public class CFunction
{
    [DataMember]
    public string RawName { get; set; } = string.Empty;
    [DataMember]
    public string ForwardName { get; set; } = string.Empty;
    [DataMember]
    public string UndecoratedName { get; set; } = string.Empty;
    [DataMember]
    public UInt32 Ordinal { get; set; } = UInt32.MaxValue;
    [DataMember]
    public UInt32 Hint { get; set; } = UInt32.MaxValue;
    [DataMember]
    public UInt64 Address { get; set; }
    [DataMember]
    public bool IsExportFunction { get; set; }
    [DataMember]
    public bool IsNameFromSymbols { get; set; }
    [DataMember]
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
            UndecoratedName = CSymbolResolver.UndecorateFunctionName(RawName);
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

    public static bool IsFunctionCalledAtLeastOnce(Dictionary<int, FunctionHashObject> parentImportsHashTable,
        CModule module, CFunction function)
    {
        FunctionHashObject funcHashObject = new(module.FileName, function.RawName, function.Ordinal);
        var uniqueKey = funcHashObject.GenerateUniqueKey();

        funcHashObject.FunctionOrdinal = UInt32.MaxValue;
        var uniqueKeyNoOrdinal = funcHashObject.GenerateUniqueKey();

        if (parentImportsHashTable.ContainsKey(uniqueKey) ||
            parentImportsHashTable.ContainsKey(uniqueKeyNoOrdinal))
        {
            return true;
        }
        return false;
    }

    public bool ResolveFunctionKind(CModule module, List<CModule> modulesList,
        Dictionary<int, FunctionHashObject> parentImportsHashTable)
    {
        FunctionKind newKind;
        List<CFunction> functionList;
        bool isOrdinal = SnapByOrdinal();
        bool isForward = IsForward();
        bool isCPlusPlusName = IsNameDecorated();
        bool bResolved;
        bool bCalledAtLeastOnce;

        if (module == null)
        {
            Kind = FunctionKind.ImportUnresolvedFunction;
            return false;
        }

        if (IsExportFunction)
        {
            // Export function processing.

            bCalledAtLeastOnce = IsFunctionCalledAtLeastOnce(parentImportsHashTable, module, this);

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
                    if (bCalledAtLeastOnce)
                    {
                        newKind = isForward ? FunctionKind.ExportForwardedOrdinalCalledAtLeastOnce : FunctionKind.ExportOrdinalCalledAtLeastOnce;
                    }
                    else
                    {
                        newKind = isForward ? FunctionKind.ExportForwardedOrdinal : FunctionKind.ExportOrdinal;
                    }
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
                else
                {
                    if (bCalledAtLeastOnce)
                    {
                        if (isCPlusPlusName)
                        {
                            newKind = isForward ? FunctionKind.ExportForwardedCPlusPlusFunctionCalledAtLeastOnce : FunctionKind.ExportCPlusPlusFunctionCalledAtLeastOnce;
                        }
                        else
                        {
                            newKind = isForward ? FunctionKind.ExportForwardedFunctionCalledAtLeastOnce : FunctionKind.ExportFunctionCalledAtLeastOnce;
                        }
                    }

                }
            }

        }
        else
        {
            // Import function processing.

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
