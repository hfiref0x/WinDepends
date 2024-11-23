/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CTESTS.CS
*
*  VERSION:     1.00
*
*  DATE:        19 Sep 2024
*  
*  Collection of tests used during debug.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.InteropServices;

namespace WinDepends;

public enum ModuleTestType
{
    Advapi32,
    Kernel32,
    User32,
    Ntdll,
    Msvcrt
}

public static class CTests
{
    public delegate TreeNode AddModuleCallback(
        [In] CModule module,
        [In] TreeNode parent = null);

    public static TreeNode AddTestModuleEntry(
        [In] TreeNode parent,
        [In] string[] imports,
        [In] bool doNotAddExports,
        [In] bool doNotAddImports,
        [In] ModuleTestType moduleTestType,
        [In] AddModuleCallback AddModuleEntry)
    {
        CModule subModule;
        TreeNode tvNode;

        switch (moduleTestType)
        {
            case ModuleTestType.Advapi32:
                subModule = new("c:\\windows\\system32\\advapi32.dll");
                tvNode = AddModuleEntry(subModule, parent);
                if (tvNode != null)
                {
                    if (subModule.OriginalInstanceId == 0 && !doNotAddExports)
                    {
                        CTests.BuildTestExports(subModule, ["RegOpenKeyExW", "RegCloseKey"], FunctionKind.ExportFunction);
                    }
                    if (!doNotAddImports)
                    {
                        CTests.BuildTestImports(subModule, imports, FunctionKind.ImportResolvedFunction);
                    }
                }
                return tvNode;

            case ModuleTestType.Kernel32:
                subModule = new("C:\\Windows\\System32\\kernel32.dll");
                tvNode = AddModuleEntry(subModule, parent);
                if (tvNode != null)
                {
                    if (subModule.OriginalInstanceId == 0 && !doNotAddExports)
                    {
                        CTests.BuildTestExports(subModule, ["CreateFileA", "CloseHandle",
                            "CreateRemoteThread", "ExitProcess"], FunctionKind.ExportFunction);
                    }
                    if (!doNotAddImports)
                    {
                        CTests.BuildTestImports(subModule, imports, FunctionKind.ImportResolvedFunction);
                    }
                }
                return tvNode;

            case ModuleTestType.User32:
                subModule = new("C:\\Windows\\System32\\user32.dll");
                tvNode = AddModuleEntry(subModule, parent);
                if (tvNode != null)
                {
                    if (subModule.OriginalInstanceId == 0 && !doNotAddExports)
                    {
                        CTests.BuildTestExports(subModule, ["SendMessageW", "SendMessageA",
                            "CreateWindowExW"], FunctionKind.ExportFunction);
                    }
                    if (!doNotAddImports)
                    {
                        CTests.BuildTestImports(subModule, imports, FunctionKind.ImportResolvedFunction);
                    }
                }
                return tvNode;

            case ModuleTestType.Ntdll:
                subModule = new("C:\\Windows\\System32\\ntdll.dll");
                tvNode = AddModuleEntry(subModule, parent);
                if (tvNode != null)
                {
                    if (subModule.OriginalInstanceId == 0 && !doNotAddExports)
                    {
                        CTests.BuildTestExports(subModule, ["ZwOpenFile", "ZwClose", "NtOpenFile",
                            "NtClose"], FunctionKind.ExportFunction);
                    }
                    if (!doNotAddImports)
                    {
                        CTests.BuildTestImports(subModule, imports, FunctionKind.ImportResolvedFunction);
                    }
                }
                return tvNode;

            case ModuleTestType.Msvcrt:
                subModule = new("C:\\Windows\\System32\\msvcrt.dll");
                tvNode = AddModuleEntry(subModule, parent);
                if (tvNode != null)
                {
                    if (subModule.OriginalInstanceId == 0 && !doNotAddExports)
                    {
                        CTests.BuildTestExports(subModule, ["??0exception@@QEAA@AEBQEBD@Z", "??0exception@@QEAA@AEBV0@@Z",
                            "??0exception@@QEAA@XZ", "??0filebuf@@QEAA@AEBV0@@Z"], FunctionKind.ExportCPlusPlusFunction);
                    }
                    if (!doNotAddImports)
                    {
                        CTests.BuildTestImports(subModule, imports, FunctionKind.ImportResolvedCPlusPlusFunction);
                    }
                }
                return tvNode;
        }

        return null;
    }

    public static void BuildTestImports(
        [In] CModule module,
        [In] string[] imports,
        [In] FunctionKind funcKind)
    {
        Random x = new();
        foreach (string fname in imports)
        {
            CFunction f = new(fname, funcKind, false)
            {
                Address = (UIntPtr)x.Next()
            };

            module.ParentImports.Add(f);
        }
    }

    public static void BuildTestExports(
        [In] CModule module,
        [In] string[] exports,
        [In] FunctionKind funcKind)
    {
        CModuleData moduleData = module.ModuleData;

        Random x = new();

        foreach (string fname in exports)
        {
            CFunction f = new(fname, funcKind, true)
            {
                Address = (UIntPtr)x.Next(),
                Ordinal = 0,
                Hint = 0
            };

            moduleData.Exports.Add(f);
        }
    }

}
