﻿/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CFUNCTION.CS
*
*  VERSION:     1.00
*
*  DATE:        09 Aug 2025
*  
*  Implementation of CFunction and CFunctionComparer classes.
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
/// Defines the possible kinds of functions in modules.
/// </summary>
/// <remarks>
/// This enum categorizes functions based on multiple attributes:
/// - Whether they are imports or exports
/// - Whether they are resolved or unresolved
/// - Whether they are C++ functions (with decorated names)
/// - Whether they are referenced by ordinal
/// - Whether they are forwarded to another module
/// - How they are called by other modules in the dependency tree
/// 
/// The enum values also serve as image indices in the UI's image list.
/// </remarks>
public enum FunctionKind : ushort
{
    /// <summary>Unresolved imported function referenced by name.</summary>
    ImportUnresolvedFunction = 0,
    /// <summary>Unresolved imported C++ function referenced by name.</summary>
    ImportUnresolvedCPlusPlusFunction,
    /// <summary>Unresolved imported function referenced by ordinal.</summary>
    ImportUnresolvedOrdinal,

    /// <summary>Unresolved imported dynamic-load function referenced by name.</summary>
    ImportUnresolvedDynamicFunction,
    /// <summary>Unresolved imported dynamic-load C++ function referenced by name.</summary>
    ImportUnresolvedDynamicCPlusPlusFunction,
    /// <summary>Unresolved imported dynamic-load function referenced by ordinal.</summary>
    ImportUnresolvedDynamicOrdinal,

    /// <summary>Resolved imported function referenced by name.</summary>
    ImportResolvedFunction,
    /// <summary>Resolved imported C++ function referenced by name.</summary>
    ImportResolvedCPlusPlusFunction,
    /// <summary>Resolved imported function referenced by ordinal.</summary>
    ImportResolvedOrdinal,

    /// <summary>Resolved imported dynamic-load function referenced by name.</summary>
    ImportResolvedDynamicFunction,
    /// <summary>Resolved imported dynamic-load C++ function referenced by name.</summary>
    ImportResolvedDynamicCPlusPlusFunction,
    /// <summary>Resolved imported dynamic-load function referenced by ordinal.</summary>
    ImportResolvedDynamicOrdinal,

    /// <summary>Exported function called by a module in the dependency tree.</summary>
    ExportFunctionCalledByModuleInTree,
    /// <summary>Exported C++ function called by a module in the dependency tree.</summary>
    ExportCPlusPlusFunctionCalledByModuleInTree,
    /// <summary>Exported function referenced by ordinal called by a module in the dependency tree.</summary>
    ExportOrdinalCalledByModuleInTree,

    /// <summary>Forwarded exported function called by a module in the dependency tree.</summary>
    ExportForwardedFunctionCalledByModuleInTree,
    /// <summary>Forwarded exported C++ function called by a module in the dependency tree.</summary>
    ExportForwardedCPlusPlusFunctionCalledByModuleInTree,
    /// <summary>Forwarded exported function referenced by ordinal called by a module in the dependency tree.</summary>
    ExportForwardedOrdinalCalledByModuleInTree,

    /// <summary>Exported function called at least once by any module.</summary>
    ExportFunctionCalledAtLeastOnce,
    /// <summary>Exported C++ function called at least once by any module.</summary>
    ExportCPlusPlusFunctionCalledAtLeastOnce,
    /// <summary>Exported function referenced by ordinal called at least once by any module.</summary>
    ExportOrdinalCalledAtLeastOnce,

    /// <summary>Forwarded exported function called at least once by any module.</summary>
    ExportForwardedFunctionCalledAtLeastOnce,
    /// <summary>Forwarded exported C++ function called at least once by any module.</summary>
    ExportForwardedCPlusPlusFunctionCalledAtLeastOnce,
    /// <summary>Forwarded exported function referenced by ordinal called at least once by any module.</summary>
    ExportForwardedOrdinalCalledAtLeastOnce,

    /// <summary>Exported function not called by any known module.</summary>
    ExportFunction,
    /// <summary>Exported C++ function not called by any known module.</summary>
    ExportCPlusPlusFunction,
    /// <summary>Exported function referenced by ordinal not called by any known module.</summary>
    ExportOrdinal,

    /// <summary>Forwarded exported function not called by any known module.</summary>
    ExportForwardedFunction,
    /// <summary>Forwarded exported C++ function not called by any known module.</summary>
    ExportForwardedCPlusPlusFunction,
    /// <summary>Forwarded exported function referenced by ordinal not called by any known module.</summary>
    ExportForwardedOrdinal
}

/// <summary>
/// Represents a unique identifier for a function across modules.
/// </summary>
/// <remarks>
/// This structure combines a function name, its ordinal, and the name of the library
/// it's imported from to create a unique identifier for tracking function usage across modules.
/// </remarks>
public struct FunctionHashObject(string functionName, string importLibrary, UInt32 ordinal)
{
    public string FunctionName { get; set; } = functionName;
    public UInt32 FunctionOrdinal { get; set; } = ordinal;
    public string ImportLibrary { get; set; } = importLibrary;

    /// <summary>
    /// Generates a unique hash key for this function based on its name, library, and ordinal.
    /// </summary>
    /// <returns>
    /// An integer hash code that uniquely identifies this function.
    /// </returns>
    /// <remarks>
    /// The hash code is used as a key in dictionaries to efficiently track functions across modules.
    /// It combines the hash codes of the function name, import library (case-insensitive),
    /// and ordinal (if specified) to create a unique value.
    /// </remarks>
    public readonly int GenerateUniqueKey()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + (FunctionName?.GetHashCode() ?? 0);
            hash = hash * 23 + (ImportLibrary?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0);

            if (FunctionOrdinal != UInt32.MaxValue)
            {
                hash = hash * 23 + FunctionOrdinal.GetHashCode();
            }
            return hash;
        }
    }
}

/// <summary>
/// Represents a function from a module, either an import or export function.
/// </summary>
/// <remarks>
/// <para>
/// This class models both import and export functions found in PE modules. It provides
/// properties for accessing function metadata such as name, ordinal, address, and type,
/// as well as methods for resolving and comparing functions.
/// </para>
/// <para>
/// Functions can be categorized based on several attributes:
/// - Import vs. Export functions
/// - Resolved vs. Unresolved functions
/// - Functions referenced by ordinal vs. by name
/// - C++ decorated vs. undecorated function names
/// - Forwarded vs. non-forwarded functions
/// </para>
/// <para>
/// The <see cref="Kind"/> property categorizes functions based on these attributes.
/// </para>
/// </remarks>
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

    /// <summary>
    /// Extracts forwarder target module file name from a forwarder string (e.g. "NTDLL.Rtl..." -> "NTDLL.dll").
    /// Always returns a name with .dll extension or empty string if invalid.
    /// </summary>
    public static string ExtractForwarderModule(string? forwarder)
    {
        if (string.IsNullOrEmpty(forwarder))
            return string.Empty;

        int dotIndex = forwarder.IndexOf('.');
        if (dotIndex <= 0)
            return string.Empty;

        ReadOnlySpan<char> nameSpan = forwarder.AsSpan(0, dotIndex);

        // Check if name already ends with .dll (case-insensitive)
        if (nameSpan.Length > 4 &&
            nameSpan.Slice(nameSpan.Length - 4).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return nameSpan.ToString();
        }

        // Append .dll extension
        return string.Concat(nameSpan, ".dll");
    }

    /// <summary>
    /// Determines the default function kind based on the function's properties.
    /// </summary>
    /// <returns>
    /// A <see cref="FunctionKind"/> value that represents the function's type and status.
    /// </returns>
    /// <remarks>
    /// The function kind is determined based on:
    /// - Whether it's an export or import
    /// - Whether it's referenced by ordinal
    /// - Whether it's a forwarded function
    /// - Whether it has a C++ decorated name
    /// </remarks>
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

    /// <summary>
    /// Undecorates (demangles) the function name if it's a decorated C++ name.
    /// </summary>
    /// <returns>
    /// The undecorated function name, or the original name if it wasn't decorated.
    /// </returns>
    /// <remarks>
    /// If the <see cref="UndecoratedName"/> is already set, this method returns that value.
    /// Otherwise, it uses <see cref="CSymbolResolver.UndecorateFunctionName"/> to demangle the name
    /// and caches the result in <see cref="UndecoratedName"/>.
    /// </remarks>
    public string UndecorateFunctionName()
    {
        if (string.IsNullOrEmpty(UndecoratedName))
        {
            UndecoratedName = CSymbolResolver.UndecorateFunctionName(RawName);
        }

        return UndecoratedName;
    }

    /// <summary>
    /// Searches for a function with a specific ordinal in a list of functions.
    /// </summary>
    /// <param name="Ordinal">The ordinal to search for.</param>
    /// <param name="list">The list of functions to search in.</param>
    /// <returns>
    /// <c>true</c> if a function with the specified ordinal was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool FindFunctionByOrdinal(uint Ordinal, List<CFunction> list)
    {
        if (list == null)
        {
            return false;
        }
        return list.Exists(item => item.Ordinal == Ordinal);
    }

    /// <summary>
    /// Searches for a function with a specific raw name in a list of functions.
    /// </summary>
    /// <param name="RawName">The raw name to search for.</param>
    /// <param name="list">The list of functions to search in.</param>
    /// <returns>
    /// <c>true</c> if a function with the specified raw name was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool FindFunctionByRawName(string RawName, List<CFunction> list)
    {
        if (list == null)
        {
            return false;
        }
        return list.Exists(item => item.RawName.Equals(RawName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Determines if a function is called at least once by any module in the dependency tree.
    /// </summary>
    /// <param name="parentImportsHashTable">Hash table of parent imports for lookup.</param>
    /// <param name="module">The module containing the function.</param>
    /// <param name="function">The function to check.</param>
    /// <returns>
    /// <c>true</c> if the function is called at least once; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsFunctionCalledAtLeastOnce(Dictionary<int, FunctionHashObject> parentImportsHashTable,
        CModule module, CFunction function)
    {
        if (parentImportsHashTable == null || module == null || function == null)
            return false;

        string fileName = module.FileName ?? string.Empty;
        string rawName = function.RawName ?? string.Empty;

        FunctionHashObject funcHashObject = new(fileName, rawName, function.Ordinal);
        var uniqueKey = funcHashObject.GenerateUniqueKey();

        funcHashObject.FunctionOrdinal = UInt32.MaxValue;
        var uniqueKeyNoOrdinal = funcHashObject.GenerateUniqueKey();

        return parentImportsHashTable.ContainsKey(uniqueKey) ||
               parentImportsHashTable.ContainsKey(uniqueKeyNoOrdinal);
    }

    /// <summary>
    /// Resolves the function kind based on the module context and dependency information.
    /// </summary>
    /// <param name="module">The module containing the function.</param>
    /// <param name="modulesList">The list of all modules in the dependency tree.</param>
    /// <param name="parentImportsHashTable">Hash table of parent imports for lookup.</param>
    /// <returns>
    /// <c>true</c> if the function kind was successfully resolved; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method updates the <see cref="Kind"/> property based on a comprehensive analysis
    /// of the function's relationship to other modules in the dependency tree.
    /// </remarks>
    public bool ResolveFunctionKind(
        CModule module,
        List<CModule> modulesList,
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
                    else
                    {
                        if (isCPlusPlusName)
                        {
                            newKind = isForward ? FunctionKind.ExportForwardedCPlusPlusFunction : FunctionKind.ExportCPlusPlusFunction;
                        }
                        else
                        {
                            newKind = isForward ? FunctionKind.ExportForwardedFunction : FunctionKind.ExportFunction;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="CFunction"/> class.
    /// </summary>
    public CFunction()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CFunction"/> class with the specified name, kind, and export flag.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="functionKind">The function kind.</param>
    /// <param name="isExportFunction">Whether the function is an export function.</param>
    public CFunction(string name, FunctionKind functionKind, bool isExportFunction)
    {
        RawName = name;
        IsExportFunction = isExportFunction;
        Kind = functionKind;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CFunction"/> class from a core export function.
    /// </summary>
    /// <param name="function">The core export function data.</param>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="CFunction"/> class from a core import function.
    /// </summary>
    /// <param name="function">The core import function data.</param>
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

/// <summary>
/// Compares <see cref="CFunction"/> objects for sorting in list views based on different field types.
/// </summary>
/// <remarks>
/// This class implements comparison of function objects based on the selected column index.
/// It handles special comparison logic for entry points, ordinals, hints, function names and image types.
/// 
/// The comparison logic handles decorated function names, forwarded functions, and special values
/// like <see cref="UInt32.MaxValue"/> which are used to indicate unset ordinals or hints.
/// </remarks>
public class CFunctionComparer : IComparer<CFunction>
{
    private readonly int _fieldIndex;
    private readonly SortOrder _sortOrder;
    private readonly StringComparer _stringComparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CFunctionComparer"/> class.
    /// </summary>
    /// <param name="sortOrder">The sort direction to apply to comparisons.</param>
    /// <param name="fieldIndex">The index of the field to compare.</param>
    /// <remarks>
    /// This constructor configures the comparer to sort functions based on the specified field
    /// and in the specified direction.
    /// </remarks>
    public CFunctionComparer(SortOrder sortOrder, int fieldIndex)
    {
        _fieldIndex = fieldIndex;
        _sortOrder = sortOrder;
        _stringComparer = StringComparer.OrdinalIgnoreCase;
    }

    public int Compare(CFunction x, CFunction y)
    {
        // Handle null values
        if (x == null && y == null) return 0;
        if (x == null) return _sortOrder == SortOrder.Ascending ? -1 : 1;
        if (y == null) return _sortOrder == SortOrder.Ascending ? 1 : -1;

        int result;

        switch (_fieldIndex)
        {
            case (int)FunctionsColumns.EntryPoint:
                {
                    bool xIsForward = !string.IsNullOrEmpty(x.ForwardName);
                    bool yIsForward = !string.IsNullOrEmpty(y.ForwardName);

                    if (xIsForward && yIsForward)
                    {
                        result = _stringComparer.Compare(x.ForwardName, y.ForwardName);
                    }
                    else if (xIsForward != yIsForward)
                    {
                        result = xIsForward ? -1 : 1; // Forwarded entries first
                    }
                    else
                    {
                        result = x.Address.CompareTo(y.Address);
                    }
                    break;
                }

            case (int)FunctionsColumns.Ordinal:
                {
                    // Special handling for Ordinal
                    bool xHasOrdinal = x.Ordinal != UInt32.MaxValue;
                    bool yHasOrdinal = y.Ordinal != UInt32.MaxValue;

                    if (!xHasOrdinal && !yHasOrdinal)
                        result = 0;
                    else if (!xHasOrdinal)
                        result = 1; // Items without ordinals go last
                    else if (!yHasOrdinal)
                        result = -1; // Items without ordinals go last
                    else
                        result = x.Ordinal.CompareTo(y.Ordinal);
                    break;
                }

            case (int)FunctionsColumns.Hint:
                {
                    // Similar special handling for Hint
                    bool xHasHint = x.Hint != UInt32.MaxValue;
                    bool yHasHint = y.Hint != UInt32.MaxValue;

                    if (!xHasHint && !yHasHint)
                        result = 0;
                    else if (!xHasHint)
                        result = 1; // Items without hints go last
                    else if (!yHasHint)
                        result = -1; // Items without hints go last
                    else
                        result = x.Hint.CompareTo(y.Hint);
                    break;
                }

            case (int)FunctionsColumns.Name:
                {
                    // Cache UndecoratedName if needed
                    if (string.IsNullOrEmpty(x.UndecoratedName) && !string.IsNullOrEmpty(x.RawName) && x.IsNameDecorated())
                        x.UndecorateFunctionName();

                    if (string.IsNullOrEmpty(y.UndecoratedName) && !string.IsNullOrEmpty(y.RawName) && y.IsNameDecorated())
                        y.UndecorateFunctionName();

                    string nameX = !string.IsNullOrEmpty(x.UndecoratedName) ? x.UndecoratedName : x.RawName;
                    string nameY = !string.IsNullOrEmpty(y.UndecoratedName) ? y.UndecoratedName : y.RawName;

                    if (string.IsNullOrEmpty(nameX) && string.IsNullOrEmpty(nameY))
                        result = 0;
                    else if (string.IsNullOrEmpty(nameX))
                        result = 1; // Items without names go last
                    else if (string.IsNullOrEmpty(nameY))
                        result = -1; // Items without names go last
                    else
                        result = _stringComparer.Compare(nameX, nameY);
                    break;
                }

            case (int)FunctionsColumns.Image:
            default:
                result = x.Kind.CompareTo(y.Kind);
                break;
        }

        return _sortOrder == SortOrder.Descending ? -result : result;
    }
}
