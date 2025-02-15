/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CUTILS.CS
*
*  VERSION:     1.00
*
*  DATE:        15 Feb 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using Microsoft.Win32;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;

namespace WinDepends;

public delegate void UpdateLoadStatusCallback(string status);
public delegate void UpdateSymbolsStatus(bool enabled);
public delegate void LogEventCallback(string? EventName, LogEventType LogEventType, string ExtraInformation = null);

/// <summary>
/// Machine extension to return friendly name of constants.
/// </summary>
public static class MachineExtensions
{
    public static string FriendlyName(this Machine FileMachine)
    {
        return FileMachine switch
        {
            Machine.I386 => "x86",
            Machine.Amd64 => "x64",
            Machine.IA64 => "Intel64",
            Machine.Thumb => "ARM Thumb/Thumb-2",
            Machine.ArmThumb2 => "ARM Thumb-2",
            Machine.Arm64 => "ARM64",
            _ => FileMachine.ToString()
        };

    }
}

/// <summary>
/// Subsystem extension to return friendly name of constants.
/// </summary>
public static class SubSystemExtensions
{
    public static string FriendlyName(this Subsystem subsystem)
    {
        return subsystem switch
        {
            Subsystem.WindowsGui => "GUI",
            Subsystem.WindowsCui => "Console",
            Subsystem.WindowsCEGui => "WinCE 1.x GUI",
            Subsystem.OS2Cui => "OS/2 console",
            Subsystem.PosixCui => "Posix console",
            Subsystem.EfiApplication => "EFI Application",
            Subsystem.EfiBootServiceDriver => "EFI Boot Driver",
            Subsystem.EfiRuntimeDriver => "EFI Runtime Driver",
            Subsystem.EfiRom => "EFI ROM",
            Subsystem.Xbox => "Xbox",
            Subsystem.WindowsBootApplication => "BootApp",
            _ => subsystem.ToString()
        };
    }
}

[DataContract]
public struct PropertyElement(string name, string value)
{
    [DataMember]
    public string Name { get; set; } = name;
    [DataMember]
    public string Value { get; set; } = value;
}

public class CFileOpenSettings(CConfiguration configuration)
{
    public bool UseRelocForImages { get; set; } = configuration.UseRelocForImages;
    public bool UseStats { get; set; } = configuration.UseStats;
    public bool AnalysisSettingsUseAsDefault { get; set; } = configuration.AnalysisSettingsUseAsDefault;
    public bool PropagateSettingsOnDependencies { get; set; } = configuration.PropagateSettingsOnDependencies;
    public uint MinAppAddress { get; set; } = configuration.MinAppAddress;
}

public record TooltipInfo(Control Control, string AssociatedText);

internal static class RichTextBoxExtensions
{
    /// <summary>
    /// Append text with colored selection.
    /// </summary>
    /// <param name="box"></param>
    /// <param name="text"></param>
    /// <param name="color"></param>
    /// <param name="bold"></param>
    /// <param name="newLine"></param>
    public static void AppendText(this RichTextBox box, string text, Color color, bool bold = true, bool newLine = true)
    {
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        int oldLength = box.Text.Length;

        box.SelectionColor = color;
        if (newLine) text += "\r";
        box.AppendText(text);
        box.SelectionColor = box.ForeColor;

        box.Select(oldLength, text.Length);
        box.SelectionFont = new Font(box.Font, bold ? FontStyle.Bold : FontStyle.Regular);
    }
}

public static class CUtils
{
    /// <summary>
    /// Returns true or false depending if current user is in Administrator group
    /// </summary>
    public static bool IsAdministrator { get; private set; }
    public static ushort SystemProcessorArchitecture { get; private set; }
    public static IntPtr MinAppAddress { get; private set; }
    public static IntPtr MaxAppAddress { get; private set; }

    public static UInt32 AllocationGranularity { get; private set; }
    static CUtils()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new(identity);
            IsAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        var systemInfo = new NativeMethods.SYSTEM_INFO();
        NativeMethods.GetSystemInfo(ref systemInfo);
        SystemProcessorArchitecture = systemInfo.wProcessorArchitecture;
        MinAppAddress = systemInfo.lpMinimumApplicationAddress;
        MaxAppAddress = systemInfo.lpMaximumApplicationAddress;
        AllocationGranularity = systemInfo.dwAllocationGranularity;
    }

    //
    /// <summary>
    /// Windows Forms Focus glitch workaround.
    /// </summary>
    /// <param name="controls">Controls objects collection</param>
    /// <returns>Control object</returns>
    static internal Control? IsControlFocused(Control.ControlCollection controls)
    {
        foreach (Control? x in controls)
        {
            if (x.Focused)
            {
                return x;
            }
            else if (x.ContainsFocus)
            {
                return IsControlFocused(x.Controls);
            }
        }

        return null;
    }

    /// <summary>
    /// Check file association in Windows Registry
    /// </summary>
    /// <param name="extension"></param>
    /// <returns>Returns true if the given file association present, false otherwise</returns>
    static internal bool GetAssoc(string extension)
    {
        bool result;

        string extKeyName = $"{extension}{CConsts.ShellIntegrationCommand}";

        try
        {
            using (var regKey = Registry.ClassesRoot.OpenSubKey(extKeyName, false))
            {
                result = regKey != null;
            }
        }
        catch
        {
            result = false;
        }

        return result;
    }

    /// <summary>
    /// Sets file association in the Windows Registry
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    static internal bool SetAssoc(string extension)
    {
        bool result = true;

        string extKeyName = $"{extension}{CConsts.ShellIntegrationCommand}";

        try
        {
            using (var regKey = Registry.ClassesRoot.CreateSubKey(extKeyName, true))
            {
                if (regKey != null)
                {
                    // Set command value.
                    using (var subKey = regKey.CreateSubKey("command"))
                    {
                        subKey?.SetValue("", $"\"{Application.ExecutablePath}\" \"%1\"", RegistryValueKind.String);
                    }

                    // Set icon value.
                    regKey.SetValue("Icon", $"\"{Application.ExecutablePath}\", 0", RegistryValueKind.String);
                }
            }
        }
        catch
        {
            result = false;
        }

        return result;
    }

    /// <summary>
    /// Removes Windows Registry association key for given extension
    /// </summary>
    /// <param name="extension"></param>
    static internal bool RemoveAssoc(string extension)
    {
        bool result = true;
        string keyName = $"{extension}{CConsts.ShellIntegrationCommand}";

        try
        {
            using (var regKey = Registry.ClassesRoot.OpenSubKey(keyName))
            {
                if (regKey != null)
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(keyName, false);
                }
            }
        }
        catch
        {
            return false;
        }

        return result;
    }

    /// <summary>
    /// Create imagelist from given bitmap
    /// </summary>
    /// <param name="bigImage"></param>
    /// <returns></returns>
    static internal ImageList CreateImageList(Bitmap bigImage, int smallImageWidth, int smallImageHeight, Color transparentColor)
    {
        try
        {
            var imageList = new ImageList
            {
                TransparentColor = transparentColor,
                ImageSize = new Size(smallImageWidth, smallImageHeight)
            };
            imageList.Images.AddStrip(bigImage);

            return imageList;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get currently running framework version.
    /// </summary>
    /// <returns></returns>
    static internal string GetRunningFrameworkVersion()
    {
        var envVersion = Environment.Version.ToString();
        var assemblyObject = typeof(Object).GetTypeInfo().Assembly;

        if (assemblyObject != null)
        {
            var attr = assemblyObject.GetCustomAttribute<AssemblyFileVersionAttribute>();
            if (attr != null)
            {
                envVersion = attr.Version;
            }
        }

        return envVersion;
    }

    /// <summary>
    /// Return converted time stamp.
    /// </summary>
    /// <param name="timeStamp"></param>
    /// <returns></returns>
    static internal string TimeSince1970ToString(uint timeStamp)
    {
        DateTime ts = new(1970, 1, 1, 0, 0, 0);
        ts = ts.AddSeconds(timeStamp);
        ts += TimeZoneInfo.Utc.GetUtcOffset(ts);
        return ts.ToString(CConsts.DateTimeFormat24Hours);
    }

    /// <summary>
    /// Create a collection of system information items
    /// </summary>
    /// <param name="systemInformation"></param>
    static internal void CollectSystemInformation(List<PropertyElement> systemInformation)
    {
        systemInformation.Add(new(CConsts.ProgramName,
            $"{CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild}"));

        using (var winKey = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion", RegistryKeyPermissionCheck.ReadSubTree))
        {
            if (winKey != null)
            {
                var winName = winKey.GetValue("ProductName");
                if (winName != null)
                {
                    systemInformation.Add(new("Operating System", winName.ToString()));
                }
            }
        }

        systemInformation.Add(new("OS Version", "\t" + System.Environment.OSVersion.Version.ToString()));

        using (var cpuKey = Registry.LocalMachine.OpenSubKey("Hardware\\Description\\System\\CentralProcessor\\0", RegistryKeyPermissionCheck.ReadSubTree))
        {
            if (cpuKey != null)
            {
                string cpuIdentifier = cpuKey.GetValue("Identifier")?.ToString() ?? string.Empty;
                string cpuVendor = cpuKey.GetValue("VendorIdentifier")?.ToString() ?? string.Empty;
                string cpuFreq = cpuKey.GetValue("~MHz")?.ToString() ?? string.Empty;

                if (!string.IsNullOrEmpty(cpuIdentifier) && !string.IsNullOrEmpty(cpuVendor))
                {
                    systemInformation.Add(new("Processor", $"\t{cpuIdentifier}, {cpuVendor}, ~{cpuFreq}MHz"));
                }
            }
        }

        var systemInfo = new NativeMethods.SYSTEM_INFO();
        NativeMethods.GetSystemInfo(ref systemInfo);

        var digitMultiply = UIntPtr.Size * 2;

        systemInformation.Add(new("Number of Processors", $"{systemInfo.dwNumberOfProcessors}, " +
            $"Mask: 0x{systemInfo.dwActiveProcessorMask.ToString($"X{digitMultiply}")}"));

        systemInformation.Add(new("Computer Name", "\t" + Environment.MachineName));
        systemInformation.Add(new("User Name", "\t" + Environment.UserName));

        DateTime localDateTime = DateTime.Now;
        systemInformation.Add(new("Local Date", "\t" + localDateTime.ToLongDateString()));

        string text = $"\t{localDateTime.ToLongTimeString()} {TimeZoneInfo.Local.DaylightName} " +
            $"(GMT {TimeZoneInfo.Local.GetUtcOffset(localDateTime)})";

        systemInformation.Add(new("Local Time", text));

        CultureInfo ci = CultureInfo.InstalledUICulture;
        text = $"\t0x{ci.LCID:X4}: {ci.DisplayName}";
        systemInformation.Add(new("OS Language", text));

        var gms = new NativeMethods.MEMORYSTATUSEX();
        if (NativeMethods.GlobalMemoryStatusEx(gms))
        {
            systemInformation.Add(new("Memory Load", $"\t{gms.dwMemoryLoad}%"));
        }

        systemInformation.Add(new("Physical Memory Total", $"{gms.ullTotalPhys:#,###0}"));
        systemInformation.Add(new("Physical Memory Used", $"{gms.ullTotalPhys - gms.ullAvailPhys:#,###0}"));
        systemInformation.Add(new("Physical Memory Free", $"{gms.ullAvailPhys:#,###0}"));

        systemInformation.Add(new("Page File Memory Total", $"{gms.ullTotalPageFile:#,###0}"));
        systemInformation.Add(new("Page File Memory Used", $"{gms.ullTotalPageFile - gms.ullAvailPageFile:#,###0}"));
        systemInformation.Add(new("Page File Memory Free", $"{gms.ullAvailPageFile:#,###0}"));

        systemInformation.Add(new("Virtual Memory Total", $"{gms.ullTotalVirtual:#,###0}"));
        systemInformation.Add(new("Virtual Memory Used", $"{gms.ullTotalVirtual - gms.ullAvailVirtual:#,###0}"));
        systemInformation.Add(new("Virtual Memory Free", $"{gms.ullAvailVirtual:#,###0}"));

        systemInformation.Add(new("Page Size", $"\t0x{systemInfo.dwPageSize:X8} ({systemInfo.dwPageSize:#,###0})"));
        systemInformation.Add(new("Allocation Granularity", $"0x{systemInfo.dwAllocationGranularity:X8} ({systemInfo.dwAllocationGranularity:#,###0})"));

        systemInformation.Add(new("Min. App. Address", $"0x{systemInfo.lpMinimumApplicationAddress.ToString($"X{digitMultiply}")} " +
            $"({systemInfo.lpMinimumApplicationAddress:#,###0})"));
        systemInformation.Add(new("Max. App. Address", $"0x{systemInfo.lpMaximumApplicationAddress.ToString($"X{digitMultiply}")} " +
            $"({systemInfo.lpMaximumApplicationAddress:#,###0})"));
    }

    /// <summary>
    /// Reads, decompresses and deserializes object.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    static internal object LoadPackedObjectFromFile(string fileName, Type objectType, UpdateLoadStatusCallback UpdateStatusCallback)
    {
        object deserializedObject = null;

        using (var inputStream = new MemoryStream())
        {
            using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                UpdateStatusCallback?.Invoke($"Loading data from {fileName} to memory, please wait");
                fileStream.CopyTo(inputStream);
                inputStream.Seek(0, SeekOrigin.Begin);

                using (var outputStream = new MemoryStream(inputStream.ToArray(), true))
                {
                    using (var decompressionStream = new BrotliStream(outputStream, CompressionMode.Decompress, false))
                    {
                        UpdateStatusCallback?.Invoke("Deserializing data, please wait");
                        var serializer = new DataContractJsonSerializer(objectType);
                        deserializedObject = serializer.ReadObject(decompressionStream);
                    }
                }
            }
        }

        return deserializedObject;
    }

    /// <summary>
    /// Serialize object, compress it with GZIP and save to file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="pbjectInstance"></param>
    /// <returns></returns>
    static internal bool SavePackedObjectToFile(string fileName, object objectInstance, Type objectType, UpdateLoadStatusCallback UpdateStatusCallback)
    {
        bool bResult = false;

        using (var memoryStream = new MemoryStream())
        {
            UpdateStatusCallback?.Invoke("Serializing data to the memory stream, please wait");
            var serializer = new DataContractJsonSerializer(objectType);
            serializer.WriteObject(memoryStream, objectInstance);
            memoryStream.Seek(0, SeekOrigin.Begin);

            using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                UpdateStatusCallback?.Invoke($"Compressing serialized data and writing it to the {fileName}, please wait");
                using (var compressionStream = new BrotliStream(fileStream, CompressionMode.Compress, false))
                {
                    memoryStream.CopyTo(compressionStream);
                    bResult = true;
                }
            }
        }

        return bResult;
    }

    static internal bool SaveObjectToFilePlainText(string fileName, object objectInstance, Type objectType)
    {
        bool bResult = false;

        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
        {
            var serializer = new DataContractJsonSerializer(objectType);
            serializer.WriteObject(fileStream, objectInstance);
            bResult = true;
        }

        return bResult;
    }

    static internal object LoadObjectFromFilePlainText(string fileName, Type objectType)
    {
        object deserializedObject = null;

        using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
            var serializer = new DataContractJsonSerializer(objectType);
            deserializedObject = serializer.ReadObject(fileStream);
        }

        return deserializedObject;
    }

    /// <summary>
    /// Find module by it InstanceId
    /// </summary>
    /// <param name="lookupModuleInstanceId"></param>
    /// <param name="dataList"></param>
    /// <returns></returns>
    static internal CModule InstanceIdToModule(int lookupModuleInstanceId, List<CModule> moduleList)
    {
        foreach (var module in moduleList)
        {
            if (module.InstanceId == lookupModuleInstanceId)
            {
                return module;
            }
        }
        return null;
    }

    /// <summary>
    /// Find module by it InstanceId from treeview node.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="moduleList"></param>
    /// <returns></returns>
    static internal CModule TreeViewGetOriginalInstanceFromNode(TreeNode node, List<CModule> moduleList)
    {
        if (node?.Tag is CModule obj && obj.OriginalInstanceId != 0)
        {
            return CUtils.InstanceIdToModule(obj.OriginalInstanceId, moduleList);
        }

        return null;
    }

    static internal CModule GetModuleByHash(string moduleName, List<CModule> moduleList)
    {
        var hash = moduleName.GetHashCode(StringComparison.OrdinalIgnoreCase);
        var moduleDict = moduleList.ToDictionary(m => m.FileName.GetHashCode(StringComparison.OrdinalIgnoreCase), m => m);

        if (moduleDict.TryGetValue(hash, out var module))
        {
            return module;
        }

        return null;
    }

    /// <summary>
    /// Find corresponding module node by it module InstanceId.
    /// </summary>
    /// <param name="moduleInstanceId"></param>
    /// <param name="startNode"></param>
    /// <returns></returns>
    static internal TreeNode TreeViewFindModuleNodeByInstanceId(int moduleInstanceId, TreeNode startNode)
    {
        TreeNode lastNode = null;

        while (startNode != null)
        {
            CModule obj = (CModule)startNode.Tag;
            if (obj != null &&
                obj.GetHashCode() == moduleInstanceId)
            {
                lastNode = startNode;
                break;
            }

            if (startNode.Nodes.Count != 0)
            {
                var treeNode = TreeViewFindModuleNodeByInstanceId(moduleInstanceId, startNode.Nodes[0]);
                if (treeNode != null)
                {
                    lastNode = treeNode;
                    break;
                }
            }

            startNode = startNode.NextNode;
        }

        return lastNode;
    }

    /// <summary>
    /// Find corresponding module node by object value.
    /// </summary>
    /// <param name="lookupModule"></param>
    /// <param name="startNode"></param>
    /// <returns></returns>
    static internal TreeNode TreeViewFindModuleNodeByObject(CModule lookupModule, TreeNode startNode)
    {
        TreeNode lastNode = null;

        while (startNode != null)
        {
            CModule obj = (CModule)startNode.Tag;
            if (obj != null && (obj.OriginalInstanceId == 0 && lookupModule.Equals(obj)))
            {
                lastNode = startNode;
                break;
            }

            if (startNode.Nodes.Count != 0)
            {
                var treeNode = TreeViewFindModuleNodeByObject(lookupModule, startNode.Nodes[0]);
                if (treeNode != null)
                {
                    lastNode = treeNode;
                    break;
                }
            }

            startNode = startNode.NextNode;
        }

        return lastNode;
    }
    public static void SetClipboardData(string data)
    {
        if (!string.IsNullOrEmpty(data))
        {
            Clipboard.Clear();
            Clipboard.SetText(data);
        }
    }
    public static UInt32 ParseMinAppAddressValue(string value)
    {
        try
        {
            string selectedHex;

            if (value.StartsWith("0x"))
            {
                selectedHex = value.Substring(2); //remove prefix
            }
            else
            {
                selectedHex = value;
            }

            uint selectedValue = uint.Parse(selectedHex, System.Globalization.NumberStyles.HexNumber);
            selectedValue &= ~(CUtils.AllocationGranularity - 1);
            return selectedValue;
        }
        catch
        {
            return CConsts.DefaultAppStartAddress;
        }
    }

    public static TreeNode FindNodeByTag(TreeNodeCollection nodes, object tagValue)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag != null && node.Tag.Equals(tagValue))
            {
                return node;
            }

            TreeNode foundNode = FindNodeByTag(node.Nodes, tagValue);
            if (foundNode != null)
            {
                return foundNode;
            }
        }

        return null;
    }
}
