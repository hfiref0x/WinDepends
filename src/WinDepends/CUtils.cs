/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CUTILS.CS
*
*  VERSION:     1.00
*
*  DATE:        11 Aug 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.Versioning;
using System.Security.Principal;
using static WinDepends.NativeMethods;

namespace WinDepends;

public delegate void UpdateLoadStatusCallback(string status);
public delegate void UpdateSymbolsStatus(bool enabled);
public delegate void AddLogMessageCallback(string message, LogMessageType messageType,
    Color? color = null, bool useBold = false, bool moduleMessage = false, CModule relatedModule = null);

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

public class CFileOpenSettings
{
    public bool UseAsDefault { get; set; }
    public bool ProcessRelocsForImage { get; set; }
    public bool UseStats { get; set; }
    public bool PropagateSettingsOnDependencies { get; set; }
    public bool ExpandForwarders { get; set; }
    public bool EnableExperimentalFeatures { get; set; }
    public bool UseCustomImageBase { get; set; }
    public uint CustomImageBase { get; set; }

    public CFileOpenSettings(CConfiguration configuration)
    {
        if (configuration == null)
            throw new System.ArgumentNullException(nameof(configuration));

        ProcessRelocsForImage = configuration.ProcessRelocsForImage;
        UseStats = configuration.UseStats;
        UseAsDefault = configuration.AnalysisSettingsUseAsDefault;
        PropagateSettingsOnDependencies = configuration.PropagateSettingsOnDependencies;
        ExpandForwarders = configuration.ExpandForwarders;
        EnableExperimentalFeatures = configuration.EnableExperimentalFeatures;
        UseCustomImageBase = configuration.UseCustomImageBase;
        CustomImageBase = configuration.CustomImageBase;
    }

    // Copy constructor
    public CFileOpenSettings(CFileOpenSettings other)
    {
        if (other == null)
            throw new System.ArgumentNullException(nameof(other));

        ProcessRelocsForImage = other.ProcessRelocsForImage;
        UseStats = other.UseStats;
        UseAsDefault = other.UseAsDefault;
        PropagateSettingsOnDependencies = other.PropagateSettingsOnDependencies;
        ExpandForwarders = other.ExpandForwarders;
        EnableExperimentalFeatures = other.EnableExperimentalFeatures;
        UseCustomImageBase = other.UseCustomImageBase;
        CustomImageBase = other.CustomImageBase;
    }
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
        box.SelectionLength = 0;
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

        NativeMethods.GetSystemInfo(out var systemInfo);
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
        string extKeyName = $"{extension}{CConsts.ShellIntegrationCommand}";

        try
        {
            using var regKey = Registry.ClassesRoot.OpenSubKey(extKeyName, false);
            return regKey is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets file association in the Windows Registry
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    static internal bool SetAssoc(string extension)
    {
        string extKeyName = $"{extension}{CConsts.ShellIntegrationCommand}";

        try
        {
            using var regKey = Registry.ClassesRoot.CreateSubKey(extKeyName, true);
            if (regKey is null) return false;

            // Create and configure command subkey
            using var commandKey = regKey.CreateSubKey("command");
            commandKey?.SetValue("", $"\"{Application.ExecutablePath}\" \"%1\"", RegistryValueKind.String);

            // Set icon directly in the main key
            regKey.SetValue("Icon", $"\"{Application.ExecutablePath}\", 0", RegistryValueKind.String);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes Windows Registry association key for given extension
    /// </summary>
    /// <param name="extension"></param>
    static internal bool RemoveAssoc(string extension)
    {
        string keyName = $"{extension}{CConsts.ShellIntegrationCommand}";

        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(keyName);
            return true;
        }
        catch (ArgumentException)
        {
            // Key doesn't exist - consider removal successful
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Create imagelist from given bitmap
    /// </summary>
    /// <param name="bigImage"></param>
    /// <param name="smallImageWidth"></param>
    /// <param name="smallImageHeight"></param>
    /// <param name="transparentColor"></param>
    /// <returns></returns>
    static internal ImageList? CreateImageList(Bitmap bigImage, int smallImageWidth, int smallImageHeight, Color transparentColor)
    {
        try
        {
            // Validate strip dimensions before processing
            if (bigImage.Width % smallImageWidth != 0 || bigImage.Height != smallImageHeight)
                return null;

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
    /// Determines if the application was built with .NET 8 or newer.
    /// </summary>
    /// <returns>Framework version string (.NET 8.0, .NET 9.0, etc.)</returns>
    static internal string GetRunningFrameworkVersion()
    {
        try
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var targetFrameworkAttribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            if (targetFrameworkAttribute != null)
            {
                string frameworkName = targetFrameworkAttribute.FrameworkName;

                // Check for .NET 8 or newer (.NETCoreApp,Version=v8.0 or higher)
                if (frameworkName.StartsWith(".NETCoreApp,Version=v"))
                {
                    string version = frameworkName.Substring(21); // Extract version after "v"
                    if (Version.TryParse(version, out Version parsedVersion))
                    {
                        // For .NET 8 or newer
                        if (parsedVersion.Major >= 8)
                        {
                            return $"{parsedVersion.Major}.{parsedVersion.Minor}";
                        }
                    }
                }
            }

            var description = RuntimeInformation.FrameworkDescription;
            if (description.StartsWith(".NET "))
            {
                string versionPart = description.Substring(5);
                if (Version.TryParse(versionPart, out Version parsedVersion))
                {
                    if (parsedVersion.Major >= 8)
                    {
                        return $"{parsedVersion.Major}.{parsedVersion.Minor}";
                    }
                }
            }

            return "Unknown (.NET < 8.0)";
        }
        catch
        {
            return "Unknown (Error detecting framework)";
        }
    }

    /// <summary>
    /// Return converted time stamp.
    /// </summary>
    /// <param name="timeStamp"></param>
    /// <returns></returns>
    static internal string TimeSince1970ToString(uint timeStamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds(timeStamp)
                        .ToLocalTime()
                        .ToString(CConsts.DateTimeFormat24Hours, CultureInfo.InvariantCulture);
    }

    private static void AddRegistryValue(List<PropertyElement> list, string name,
        string keyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        var value = key?.GetValue(valueName)?.ToString();
        if (!string.IsNullOrEmpty(value))
        {
            list.Add(new(name, $"{value}"));
        }
    }

    private static void AddMemoryInformation(List<PropertyElement> list, MEMORYSTATUSEX mem)
    {
        list.Add(new("Memory Load", $"\t{mem.dwMemoryLoad}%"));

        AddMemoryEntry(list, "Physical Memory", mem.ullTotalPhys, mem.ullAvailPhys);
        AddMemoryEntry(list, "Page File Memory", mem.ullTotalPageFile, mem.ullAvailPageFile);
        AddMemoryEntry(list, "Virtual Memory", mem.ullTotalVirtual, mem.ullAvailVirtual);
    }

    private static void AddMemoryEntry(List<PropertyElement> list, string prefix,
        ulong total, ulong free)
    {
        list.Add(new($"{prefix} Total", $"{total:N0}"));
        list.Add(new($"{prefix} Used", $"{(total - free):N0}"));
        list.Add(new($"{prefix} Free", $"{free:N0}"));
    }

    private static void AddAddressInfo(List<PropertyElement> list, string name,
        IntPtr address, string format)
    {
        list.Add(new(name,
            $"0x{address.ToString(format)} ({address.ToInt64():N0})"));
    }

    /// <summary>
    /// Create a collection of system information items
    /// </summary>
    /// <param name="systemInformation"></param>
    static internal void CollectSystemInformation(List<PropertyElement> systemInformation)
    {
        // Program Version
        systemInformation.Add(new(CConsts.ProgramName,
            $"{CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild}"));

        // OS Information
        AddRegistryValue(systemInformation, "Operating System",
            @"Software\Microsoft\Windows NT\CurrentVersion", "ProductName");

        systemInformation.Add(new("OS Version",
            $"\t{Environment.OSVersion.Version}"));

        // Processor Information
        using (var cpuKey = Registry.LocalMachine.OpenSubKey(
            @"Hardware\Description\System\CentralProcessor\0"))
        {
            var cpuId = cpuKey?.GetValue("Identifier")?.ToString();
            var cpuVendor = cpuKey?.GetValue("VendorIdentifier")?.ToString();
            var cpuFreq = cpuKey?.GetValue("~MHz")?.ToString();

            if (!string.IsNullOrEmpty(cpuId) && !string.IsNullOrEmpty(cpuVendor))
            {
                systemInformation.Add(new("Processor",
                    $"\t{cpuId}, {cpuVendor}, ~{cpuFreq}MHz"));
            }
        }

        // System Info
        GetSystemInfo(out var systemInfo);
        var ptrFormat = $"X{IntPtr.Size * 2}";

        systemInformation.Add(new("Number of Processors",
            $"{systemInfo.dwNumberOfProcessors}, Mask: 0x{systemInfo.dwActiveProcessorMask.ToString(ptrFormat)}"));

        // User/Computer Info
        systemInformation.Add(new("Computer Name", $"\t{Environment.MachineName}"));
        systemInformation.Add(new("User Name", $"\t{Environment.UserName}"));

        // DateTime Info
        var now = DateTime.Now;
        systemInformation.Add(new("Local Date", $"\t{now.ToLongDateString()}"));

        var timeZone = TimeZoneInfo.Local;
        systemInformation.Add(new("Local Time",
            $"\t{now.ToLongTimeString()} {timeZone.DaylightName} (GMT {timeZone.GetUtcOffset(now)})"));

        // Culture Info
        var culture = CultureInfo.InstalledUICulture;
        systemInformation.Add(new("OS Language",
            $"\t0x{culture.LCID:X4}: {culture.DisplayName}"));

        // Memory Info (corrected struct usage)
        var memoryStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(ref memoryStatus))
        {
            AddMemoryInformation(systemInformation, memoryStatus);
        }

        // System Memory Configuration
        systemInformation.Add(new("Page Size",
            $"\t0x{systemInfo.dwPageSize:X8} ({systemInfo.dwPageSize:N0})"));

        systemInformation.Add(new("Allocation Granularity",
            $"0x{systemInfo.dwAllocationGranularity:X8} ({systemInfo.dwAllocationGranularity:N0})"));

        // Address Space Info
        AddAddressInfo(systemInformation, "Min. App. Address",
            systemInfo.lpMinimumApplicationAddress, ptrFormat);
        AddAddressInfo(systemInformation, "Max. App. Address",
            systemInfo.lpMaximumApplicationAddress, ptrFormat);
    }

    /// <summary>
    /// Reads, decompresses and deserializes object.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="objectType"></param>
    /// <param name="updateStatusCallback"></param>
    /// <returns></returns>
    static internal object LoadPackedObjectFromFile(string fileName, Type objectType, UpdateLoadStatusCallback updateStatusCallback)
    {
        updateStatusCallback?.Invoke($"Loading and decompressing data from {fileName}");

        using var fileStream = new FileStream(
            fileName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan
        );

        using var decompressionStream = new BrotliStream(fileStream, CompressionMode.Decompress);

        updateStatusCallback?.Invoke("Deserializing data, please wait");
        var serializer = new DataContractJsonSerializer(objectType);
        return serializer.ReadObject(decompressionStream);
    }

    /// <summary>
    /// Serialize object, compress it with Brotli algorithm and save to file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="objectInstance"></param>
    /// <param name="objectType"></param>
    /// <param name="updateStatusCallback"></param>
    /// <returns></returns>
    static internal bool SavePackedObjectToFile(string fileName, object objectInstance, Type objectType, UpdateLoadStatusCallback updateStatusCallback)
    {
        updateStatusCallback?.Invoke($"Saving data to {fileName}");

        using var fileStream = new FileStream(
            fileName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.SequentialScan
        );

        using var compressionStream = new BrotliStream(fileStream, CompressionLevel.Optimal);

        updateStatusCallback?.Invoke("Serializing and compressing data");
        var serializer = new DataContractJsonSerializer(objectType);
        serializer.WriteObject(compressionStream, objectInstance);

        return true;
    }

    static internal bool SaveObjectToFilePlainText(string fileName, object objectInstance, Type objectType)
    {
        try
        {
            using var fileStream = new FileStream(
                fileName,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.SequentialScan
            );

            new DataContractJsonSerializer(objectType).WriteObject(fileStream, objectInstance);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static internal object LoadObjectFromFilePlainText(string fileName, Type objectType)
    {
        try
        {
            using var fileStream = new FileStream(
                fileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan
            );

            return new DataContractJsonSerializer(objectType).ReadObject(fileStream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Find module by its InstanceId
    /// </summary>
    /// <param name="lookupModuleInstanceId">The instance ID to search for</param>
    /// <param name="moduleList">The collection of modules to search</param>
    /// <returns>The matching module if found; otherwise, null</returns>
    static internal CModule? InstanceIdToModule(int lookupModuleInstanceId, List<CModule> moduleList)
    {
        // Fast exit for empty lists
        if (moduleList == null || moduleList.Count == 0)
            return null;

        for (int i = 0; i < moduleList.Count; i++)
        {
            if (moduleList[i]?.InstanceId == lookupModuleInstanceId)
                return moduleList[i];
        }
        return null;
    }

    /// <summary>
    /// Find module by it InstanceId from treeview node.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="moduleList"></param>
    /// <returns></returns>
    static internal CModule? TreeViewGetOriginalInstanceFromNode(TreeNode node, List<CModule> moduleList)
    {
        if (node?.Tag is CModule obj && obj.OriginalInstanceId != 0 && moduleList != null)
        {
            return CUtils.InstanceIdToModule(obj.OriginalInstanceId, moduleList);
        }

        return null;
    }

    /// <summary>
    /// Finds a module by its name hash in a collection of modules.
    /// </summary>
    /// <param name="moduleName">The name of the module to find</param>
    /// <param name="moduleList">The collection of modules to search</param>
    /// <returns>The matching module if found; otherwise, null</returns>
    static internal CModule? GetModuleByHash(string moduleName, List<CModule> moduleList)
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
    /// Finds a TreeView node representing a specific module.
    /// </summary>
    /// <param name="lookupModule">The module to search for</param>
    /// <param name="startNode">The TreeView node to start searching from</param>
    /// <returns>The matching TreeView node if found; otherwise, null</returns>
    static internal TreeNode TreeViewFindModuleNodeByObject(CModule lookupModule, TreeNode startNode)
    {
        var stack = new Stack<TreeNode>();

        // Initialize with start node and its siblings
        for (var node = startNode; node != null; node = node.NextNode)
            stack.Push(node);

        while (stack.TryPop(out var current))
        {
            // Pattern match with type check and null check
            if (current.Tag is CModule module &&
                module.OriginalInstanceId == 0 &&
                lookupModule.Equals(module))
            {
                return current;
            }

            // Process children depth-first (first child then siblings)
            if (current.Nodes.Count > 0)
            {
                // Push children in reverse order to maintain original search sequence
                for (var i = current.Nodes.Count - 1; i >= 0; i--)
                    stack.Push(current.Nodes[i]);
            }
        }

        return null;
    }

    /// <summary>
    /// Add data to the system clipboard.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static bool SetClipboardData(string data)
    {
        if (string.IsNullOrEmpty(data))
            return false;

        try
        {
            Clipboard.SetText(data, TextDataFormat.UnicodeText);
            return true;
        }
        catch { return false; }
    }

    public static UInt32 ParseMinAppAddressValue(string value)
    {
        ReadOnlySpan<char> hexSpan = value.AsSpan();

        // Handle hex prefix
        if (hexSpan.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hexSpan = hexSpan.Slice(2);
        }

        // Use span-based parsing with invariant culture
        if (uint.TryParse(hexSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint selectedValue))
        {
            // Use bitwise AND with complement for alignment
            return selectedValue & ~(CUtils.AllocationGranularity - 1u);
        }

        return CConsts.DefaultAppStartAddress;
    }

    public static TreeNode FindNodeByTag(TreeNodeCollection nodes, object tagValue)
    {
        var queue = new Queue<TreeNode>(nodes.Cast<TreeNode>());

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Tag is { } tag && tag.Equals(tagValue))
                return current;

            foreach (TreeNode child in current.Nodes)
                queue.Enqueue(child);
        }
        return null;
    }

    /// <summary>
    /// Launches an external application or document.
    /// </summary>
    /// <param name="fileName">The path to the file to open</param>
    /// <param name="useShellExecute">Whether to use the operating system shell to open the file</param>
    public static void RunExternalCommand(string fileName, bool useShellExecute)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = useShellExecute
            });
        }
        catch { }
    }

    public static Bitmap ByteArrayToBitmap(byte[] byteArray)
    {
        using (MemoryStream ms = new MemoryStream(byteArray))
        {
            // Create a Bitmap from the MemoryStream and return it
            return new Bitmap(ms);
        }
    }

    public static float GetDpiScalingFactor()
    {
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
        {
            return g.DpiX / CConsts.DefaultDPIValue;
        }
    }

    public static Size CalculateToolBarImageSize(bool useClassic, float scalingFactor, Size baseSize)
    {
        if (useClassic && scalingFactor > CConsts.DpiScaleFactor150)
        {
            return new Size(
                (int)(baseSize.Width * scalingFactor),
                (int)(baseSize.Height * scalingFactor)
            );
        }
        return baseSize;
    }

    public static List<Bitmap> ProcessImageStrip(bool useClassic, float scalingFactor, Color transparencyColor, Size desiredImageSize)
    {
        Bitmap sourceStrip = useClassic
            ? Properties.Resources.ToolBarIcons
            : CUtils.ByteArrayToBitmap(Properties.Resources.ToolBarIconsNew);

        Size originalSize = desiredImageSize;

        return (useClassic && scalingFactor > CConsts.DpiScaleFactor150)
            ? SplitAndScaleImageStrip(sourceStrip, originalSize, scalingFactor, transparencyColor)
            : SplitImageStrip(sourceStrip, originalSize, transparencyColor);
    }

    /// <summary>
    /// Splits an image strip into individual images.
    /// </summary>
    /// <param name="strip">Source bitmap strip</param>
    /// <param name="imageSize">Size of each individual image</param>
    /// <param name="transparencyColor">Color to be treated as transparent</param>
    /// <returns>List of individual bitmap images</returns>
    public static List<Bitmap> SplitImageStrip(Bitmap strip, Size imageSize, Color transparencyColor)
    {
        List<Bitmap> images = new List<Bitmap>();
        for (int i = 0; i < CConsts.ToolBarImageMax; i++)
        {
            Rectangle rect = new Rectangle(i * imageSize.Width, 0, imageSize.Width, imageSize.Height);
            Bitmap image = strip.Clone(rect, strip.PixelFormat);
            image.MakeTransparent(transparencyColor);
            images.Add(image);
        }
        return images;
    }

    /// <summary>
    /// Splits an image strip and scales each resulting image.
    /// </summary>
    /// <param name="strip">Source bitmap strip</param>
    /// <param name="originalSize">Original size of each image</param>
    /// <param name="scaleFactor">Scale factor to apply</param>
    /// <param name="transparencyColor">Color to be treated as transparent</param>
    /// <returns>List of scaled bitmap images</returns>
    public static List<Bitmap> SplitAndScaleImageStrip(Bitmap strip, Size originalSize,
                                           float scaleFactor, Color transparencyColor)
    {
        List<Bitmap> scaledImages = new List<Bitmap>();
        Size scaledSize = new Size(
            (int)(originalSize.Width * scaleFactor),
            (int)(originalSize.Height * scaleFactor)
        );

        for (int i = 0; i < CConsts.ToolBarImageMax; i++)
        {
            Rectangle rect = new Rectangle(
                i * originalSize.Width,
                0,
                originalSize.Width,
                originalSize.Height
            );

            using (Bitmap originalImage = strip.Clone(rect, strip.PixelFormat))
            {
                originalImage.MakeTransparent(transparencyColor);

                Bitmap scaledImage = new Bitmap(scaledSize.Width, scaledSize.Height,
                                              PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(scaledImage))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(originalImage, 0, 0, scaledSize.Width, scaledSize.Height);
                }
                scaledImages.Add(scaledImage);
            }
        }
        return scaledImages;
    }

    /// <summary>
    /// Loads toolbar images and configures the toolbar image list.
    /// </summary>
    /// <param name="toolStrip">The toolbar to configure</param>
    /// <param name="desiredImageSize">Desired image size</param>
    /// <param name="useClassic">Whether to use classic images</param>
    /// <param name="transparencyColor">Color to treat as transparent</param>
    public static void LoadToolbarImages(ToolStrip toolStrip, Size desiredImageSize, bool useClassic, Color transparencyColor)
    {
        float scalingFactor = GetDpiScalingFactor();

        // Dispose previous ImageList if exists
        if (toolStrip.ImageList != null)
        {
            toolStrip.ImageList.Dispose();
            toolStrip.ImageList = null;
        }

        // Create and configure new ImageList
        var imageList = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = CalculateToolBarImageSize(useClassic, scalingFactor, desiredImageSize)
        };

        // Get appropriate image strip
        var images = ProcessImageStrip(useClassic, scalingFactor, transparencyColor, desiredImageSize);

        imageList.Images.AddRange(images
            .OrderBy(img => (ToolBarIconType)images.IndexOf(img))
            .ToArray());

        toolStrip.ImageList = imageList;
    }

    public static Rectangle GetAdjustedBounds(Rectangle originalBounds)
    {
        Screen screen = Screen.AllScreens.FirstOrDefault(s => s.WorkingArea.Contains(originalBounds.Location))
                        ?? Screen.PrimaryScreen;

        Rectangle area = screen.WorkingArea;

        int width = Math.Min(originalBounds.Width, area.Width);
        int height = Math.Min(originalBounds.Height, area.Height);

        int left = Math.Max(area.Left, Math.Min(originalBounds.Left, area.Right - width));
        int top = Math.Max(area.Top, Math.Min(originalBounds.Top, area.Bottom - height));

        return new Rectangle(left, top, width, height);
    }

    public static bool IsPointVisible(Point pt)
    {
        return Screen.AllScreens.Any(screen => screen.WorkingArea.Contains(pt));
    }

}
public static class PeExceptionHelper
{
    private const uint STATUS_ACCESS_VIOLATION = 0xC0000005;
    private const uint STATUS_INTEGER_OVERFLOW = 0xC0000095;
    private const uint STATUS_STACK_OVERFLOW = 0xC00000FD;
    private const uint STATUS_IN_PAGE_ERROR = 0xC0000006;
    private const uint STATUS_INVALID_IMAGE_FORMAT = 0xC000007B;

    public static bool IsInvalidImageFormatException(uint exceptionCode)
    {
        return exceptionCode == STATUS_INVALID_IMAGE_FORMAT;
    }

    /// <summary>
    /// Translates a Windows exception code to a human-readable string description
    /// </summary>
    /// <param name="exceptionCode">The exception code</param>
    /// <returns>A human-readable description of the exception</returns>
    public static string TranslateExceptionCode(uint exceptionCode)
    {
        switch (exceptionCode)
        {
            case STATUS_ACCESS_VIOLATION:
                return "STATUS_ACCESS_VIOLATION";
            case STATUS_INTEGER_OVERFLOW:
                return "STATUS_INTEGER_OVERFLOW";
            case STATUS_STACK_OVERFLOW:
                return "STATUS_STACK_OVERFLOW";
            case STATUS_IN_PAGE_ERROR:
                return "STATUS_IN_PAGE_ERROR";
            case STATUS_INVALID_IMAGE_FORMAT:
                return "STATUS_INVALID_IMAGE_FORMAT";
            default:
                return "Unknown";
        }
    }
}
