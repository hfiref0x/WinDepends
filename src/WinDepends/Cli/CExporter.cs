/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CEXPORTER.CS
*
*  VERSION:     1.00
*
*  DATE:        29 Nov 2025
*  
*  Implementation of dependency export functionality. 
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace WinDepends;

/// <summary>
/// Supported export formats.
/// </summary>
public enum ExportFormat
{
    Json,
    Csv,
    Html,
    Dot,
    Text
}

/// <summary>
/// Options for export operations.
/// </summary>
public class ExportOptions
{
    public bool IncludeImports { get; set; } = true;
    public bool IncludeExports { get; set; } = true;
    public bool FullPaths { get; set; } = true;
    public bool IncludeSystemInfo { get; set; } = false;
    public int MaxDepth { get; set; } = int.MaxValue;
}

/// <summary>
/// Handles exporting dependency data to various formats.
/// </summary>
public static class CExporter
{
    /// <summary>
    /// Export dependency data to the specified format.
    /// </summary>
    public static bool Export(CDepends session, string outputPath, ExportFormat format, ExportOptions options)
    {
        if (session?.RootModule == null)
            return false;

        try
        {
            string content = format switch
            {
                ExportFormat.Json => ExportToJson(session, options),
                ExportFormat.Csv => ExportToCsv(session, options),
                ExportFormat.Html => ExportToHtml(session, options),
                ExportFormat.Dot => ExportToDot(session, options),
                ExportFormat.Text => ExportToText(session, options),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };

            File.WriteAllText(outputPath, content, Encoding.UTF8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Export to JSON format.
    /// </summary>
    public static string ExportToJson(CDepends session, ExportOptions options)
    {
        var exportData = BuildExportData(session, options);

        using var ms = new MemoryStream();
        var settings = new DataContractJsonSerializerSettings
        {
            UseSimpleDictionaryFormat = true
        };

        var serializer = new DataContractJsonSerializer(typeof(ExportData), settings);
        using var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, true, true, "  ");
        serializer.WriteObject(writer, exportData);
        writer.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Export to CSV format (flat module list).
    /// </summary>
    public static string ExportToCsv(CDepends session, ExportOptions options)
    {
        var sb = new StringBuilder();
        var modules = new List<CModule>();

        CollectAllModules(session.RootModule, modules, new HashSet<string>(StringComparer.OrdinalIgnoreCase), options.MaxDepth, 0);

        sb.AppendLine("\"Module\",\"Path\",\"Status\",\"FileSize\",\"LinkChecksum\",\"RealChecksum\"," +
                      "\"Machine\",\"Subsystem\",\"PreferredBase\",\"VirtualSize\",\"FileVersion\"," +
                      "\"ProductVersion\",\"LinkerVersion\",\"IsDelayLoad\",\"Is64Bit\",\"IsDotNet\"," +
                      "\"FileTimestamp\",\"LinkTimestamp\",\"ResolvedBy\",\"ExportCount\",\"ImportCount\"");

        foreach (var module in modules)
        {
            string status = GetModuleStatus(module);
            string machine = GetMachineName(module.ModuleData?.Machine ?? 0);
            string subsystem = GetSubsystemName(module.ModuleData?.Subsystem ?? 0);
            bool is64Bit = module.Is64bitArchitecture();

            int exportCount = module.ModuleData?.Exports?.Count ?? 0;
            int importCount = module.ParentImports?.Count ?? 0;

            sb.AppendLine($"\"{EscapeCsv(Path.GetFileName(module.FileName))}\"," +
                          $"\"{EscapeCsv(module.FileName)}\"," +
                          $"\"{status}\"," +
                          $"\"{module.ModuleData?.FileSize ?? 0}\"," +
                          $"\"0x{module.ModuleData?.LinkChecksum ?? 0:X8}\"," +
                          $"\"0x{module.ModuleData?.RealChecksum ?? 0:X8}\"," +
                          $"\"{machine}\"," +
                          $"\"{subsystem}\"," +
                          $"\"0x{module.ModuleData?.PreferredBase ?? 0:X}\"," +
                          $"\"{module.ModuleData?.VirtualSize ?? 0}\"," +
                          $"\"{EscapeCsv(module.ModuleData?.FileVersion ?? "")}\"," +
                          $"\"{EscapeCsv(module.ModuleData?.ProductVersion ?? "")}\"," +
                          $"\"{EscapeCsv(module.ModuleData?.LinkerVersion ?? "")}\"," +
                          $"\"{module.IsDelayLoad}\"," +
                          $"\"{is64Bit}\"," +
                          $"\"{module.IsDotNetModule}\"," +
                          $"\"{module.ModuleData?.FileTimeStamp:yyyy-MM-dd HH:mm:ss}\"," +
                          $"\"{module.ModuleData?.LinkTimeStamp}\"," +
                          $"\"{module.FileNameResolvedBy}\"," +
                          $"\"{exportCount}\"," +
                          $"\"{importCount}\"");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Export to HTML format with collapsible tree.
    /// </summary>
    public static string ExportToHtml(CDepends session, ExportOptions options)
    {
        var sb = new StringBuilder();

        string rootFileName = Path.GetFileName(session.RootModule.FileName);
        string generatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>Dependency Report - {HtmlEncode(rootFileName)}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(GetHtmlStyles());
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine($"        <h1>Dependency Analysis Report</h1>");
        sb.AppendLine($"        <div class=\"subtitle\">{HtmlEncode(session.RootModule.FileName)}</div>");
        sb.AppendLine($"        <div class=\"meta\">Generated: {generatedDate} | WinDepends v{CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}</div>");
        sb.AppendLine("    </div>");

        var allModules = new List<CModule>();
        CollectAllModules(session.RootModule, allModules, new HashSet<string>(StringComparer.OrdinalIgnoreCase), options.MaxDepth, 0);

        int totalModules = allModules.Count;
        int missingModules = allModules.Count(m => m.FileNotFound);
        int warningModules = allModules.Count(m => m.ExportContainErrors || m.OtherErrorsPresent);

        sb.AppendLine("    <div class=\"summary\">");
        sb.AppendLine("        <div class=\"summary-item\"><span class=\"count\">" + totalModules + "</span><span class=\"label\"> Total Modules</span></div>");
        sb.AppendLine("        <div class=\"summary-item missing\"><span class=\"count\">" + missingModules + "</span><span class=\"label\"> Missing</span></div>");
        sb.AppendLine("        <div class=\"summary-item warning\"><span class=\"count\">" + warningModules + "</span><span class=\"label\"> Warnings</span></div>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <div class=\"controls\">");
        sb.AppendLine("        <button onclick=\"expandAll()\">Expand All</button>");
        sb.AppendLine("        <button onclick=\"collapseAll()\">Collapse All</button>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <div class=\"tree-container\">");
        sb.AppendLine("        <h2>Dependency Tree</h2>");
        sb.AppendLine("        <ul class=\"tree\">");
        BuildHtmlTree(sb, session.RootModule, options, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
        sb.AppendLine("        </ul>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <div class=\"table-container\">");
        sb.AppendLine("        <h2>Module List</h2>");
        sb.AppendLine("        <table>");
        sb.AppendLine("            <thead>");
        sb.AppendLine("                <tr>");
        sb.AppendLine("                    <th>Module</th>");
        sb.AppendLine("                    <th>Status</th>");
        sb.AppendLine("                    <th>Machine</th>");
        sb.AppendLine("                    <th>File Size</th>");
        sb.AppendLine("                    <th>Version</th>");
        sb.AppendLine("                    <th>Path</th>");
        sb.AppendLine("                </tr>");
        sb.AppendLine("            </thead>");
        sb.AppendLine("            <tbody>");

        foreach (var module in allModules.OrderBy(m => Path.GetFileName(m.FileName), StringComparer.OrdinalIgnoreCase))
        {
            string statusClass = module.FileNotFound ? "missing" : (module.ExportContainErrors || module.OtherErrorsPresent ? "warning" : "ok");
            string status = GetModuleStatus(module);
            string machine = GetMachineName(module.ModuleData?.Machine ?? 0);
            string fileSize = module.ModuleData != null ? FormatFileSize(module.ModuleData.FileSize) : "N/A";
            string version = module.ModuleData?.FileVersion ?? "N/A";

            sb.AppendLine($"                <tr class=\"{statusClass}\">");
            sb.AppendLine($"                    <td>{HtmlEncode(Path.GetFileName(module.FileName))}</td>");
            sb.AppendLine($"                    <td><span class=\"status-badge {statusClass}\">{status}</span></td>");
            sb.AppendLine($"                    <td>{machine}</td>");
            sb.AppendLine($"                    <td>{fileSize}</td>");
            sb.AppendLine($"                    <td>{HtmlEncode(version)}</td>");
            sb.AppendLine($"                    <td class=\"path\">{HtmlEncode(module.FileName)}</td>");
            sb.AppendLine("                </tr>");
        }

        sb.AppendLine("            </tbody>");
        sb.AppendLine("        </table>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <script>");
        sb.AppendLine(GetHtmlScript());
        sb.AppendLine("    </script>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Export to DOT format for Graphviz visualization.
    /// </summary>
    public static string ExportToDot(CDepends session, ExportOptions options)
    {
        var sb = new StringBuilder();
        var edges = new HashSet<string>();
        var nodes = new Dictionary<string, CModule>(StringComparer.OrdinalIgnoreCase);

        CollectGraphData(session.RootModule, nodes, edges, new HashSet<string>(StringComparer.OrdinalIgnoreCase), options.MaxDepth, 0);

        sb.AppendLine("digraph Dependencies {");
        sb.AppendLine("    rankdir=LR;");
        sb.AppendLine("    node [shape=box, style=filled, fontname=\"Segoe UI\", fontsize=10];");
        sb.AppendLine("    edge [fontname=\"Segoe UI\", fontsize=8];");
        sb.AppendLine();

        foreach (var kvp in nodes)
        {
            string nodeId = GetDotNodeId(kvp.Key);
            string label = Path.GetFileName(kvp.Key);
            string color = GetDotNodeColor(kvp.Value);
            string tooltip = kvp.Key.Replace("\\", "\\\\").Replace("\"", "\\\"");

            sb.AppendLine($"    {nodeId} [label=\"{EscapeDotLabel(label)}\", fillcolor=\"{color}\", tooltip=\"{tooltip}\"];");
        }

        sb.AppendLine();

        foreach (var edge in edges)
        {
            sb.AppendLine($"    {edge};");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Export to plain text format (tree view).
    /// </summary>
    public static string ExportToText(CDepends session, ExportOptions options)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Dependency Analysis: {session.RootModule.FileName}");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();

        BuildTextTree(sb, session.RootModule, "", true, new HashSet<string>(StringComparer.OrdinalIgnoreCase), options.MaxDepth, 0);

        return sb.ToString();
    }

    #region Private Helper Methods

    private static ExportData BuildExportData(CDepends session, ExportOptions options)
    {
        var data = new ExportData
        {
            GeneratedAt = DateTime.Now.ToString("o"),
            ToolVersion = $"{CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild}",
            RootModule = session.RootModule.FileName
        };

        var modules = new List<CModule>();
        CollectAllModules(session.RootModule, modules, new HashSet<string>(StringComparer.OrdinalIgnoreCase), options.MaxDepth, 0);

        foreach (var module in modules)
        {
            var exportModule = new ExportModuleData
            {
                FileName = module.FileName,
                RawFileName = module.RawFileName,
                Status = GetModuleStatus(module),
                IsDelayLoad = module.IsDelayLoad,
                IsForward = module.IsForward,
                Is64Bit = module.Is64bitArchitecture(),
                IsDotNet = module.IsDotNetModule,
                FileNotFound = module.FileNotFound,
                IsInvalid = module.IsInvalid,
                ResolvedBy = module.FileNameResolvedBy.ToString()
            };

            if (module.ModuleData != null)
            {
                exportModule.FileSize = module.ModuleData.FileSize;
                exportModule.LinkChecksum = module.ModuleData.LinkChecksum;
                exportModule.RealChecksum = module.ModuleData.RealChecksum;
                exportModule.PreferredBase = module.ModuleData.PreferredBase;
                exportModule.VirtualSize = module.ModuleData.VirtualSize;
                exportModule.Machine = module.ModuleData.Machine;
                exportModule.Subsystem = module.ModuleData.Subsystem;
                exportModule.FileVersion = module.ModuleData.FileVersion;
                exportModule.ProductVersion = module.ModuleData.ProductVersion;
                exportModule.LinkerVersion = module.ModuleData.LinkerVersion;
                exportModule.FileTimestamp = module.ModuleData.FileTimeStamp;
                exportModule.LinkTimestamp = module.ModuleData.LinkTimeStamp;
            }

            if (options.IncludeExports && module.ModuleData?.Exports != null)
            {
                exportModule.Exports = module.ModuleData.Exports.Select(f => new ExportFunctionData
                {
                    Name = f.RawName,
                    Ordinal = f.Ordinal,
                    Address = f.Address,
                    ForwardName = f.ForwardName
                }).ToList();
            }

            if (options.IncludeImports && module.ParentImports != null)
            {
                exportModule.Imports = module.ParentImports.Select(f => new ExportFunctionData
                {
                    Name = f.RawName,
                    Ordinal = f.Ordinal,
                    Hint = f.Hint
                }).ToList();
            }

            exportModule.Dependencies = module.Dependents?.Select(d => Path.GetFileName(d.FileName)).ToList() ?? [];

            data.Modules.Add(exportModule);
        }

        return data;
    }

    private static void CollectAllModules(CModule module, List<CModule> modules, HashSet<string> visited, int maxDepth, int currentDepth)
    {
        if (module == null || currentDepth > maxDepth)
            return;

        string key = module.FileName?.ToLowerInvariant() ?? "";
        if (!visited.Add(key))
            return;

        modules.Add(module);

        if (module.Dependents != null)
        {
            foreach (var dep in module.Dependents)
            {
                CollectAllModules(dep, modules, visited, maxDepth, currentDepth + 1);
            }
        }
    }

    private static void CollectGraphData(CModule module, Dictionary<string, CModule> nodes, HashSet<string> edges, HashSet<string> visited, int maxDepth, int currentDepth)
    {
        if (module == null || currentDepth > maxDepth)
            return;

        string key = module.FileName?.ToLowerInvariant() ?? "";
        if (!visited.Add(key))
            return;

        if (!string.IsNullOrEmpty(module.FileName))
        {
            nodes.TryAdd(module.FileName, module);
        }

        if (module.Dependents != null)
        {
            foreach (var dep in module.Dependents)
            {
                if (!string.IsNullOrEmpty(dep.FileName))
                {
                    string fromId = GetDotNodeId(module.FileName);
                    string toId = GetDotNodeId(dep.FileName);
                    string edgeStyle = dep.IsDelayLoad ? " [style=dashed]" : "";
                    edges.Add($"{fromId} -> {toId}{edgeStyle}");
                }

                CollectGraphData(dep, nodes, edges, visited, maxDepth, currentDepth + 1);
            }
        }
    }

    private static void BuildHtmlTree(StringBuilder sb, CModule module, ExportOptions options, HashSet<string> visited, int depth)
    {
        if (module == null || depth > options.MaxDepth)
            return;

        string key = module.FileName?.ToLowerInvariant() ?? "";
        bool isDuplicate = !visited.Add(key);

        string fileName = Path.GetFileName(module.FileName);
        string statusClass = module.FileNotFound ? "missing" : (module.ExportContainErrors || module.OtherErrorsPresent ? "warning" : "");
        string duplicateClass = isDuplicate ? " duplicate" : "";
        string delayLoadBadge = module.IsDelayLoad ? "<span class=\"badge delay\">Delay</span>" : "";
        string dotNetBadge = module.IsDotNetModule ? "<span class=\"badge dotnet\">.NET</span>" : "";

        bool hasChildren = !isDuplicate && module.Dependents != null && module.Dependents.Count > 0;

        if (hasChildren)
        {
            sb.AppendLine($"            <li class=\"collapsible{duplicateClass}\">");
            sb.AppendLine($"                <span class=\"toggle\">▶</span>");
            sb.AppendLine($"                <span class=\"module-name {statusClass}\" title=\"{HtmlEncode(module.FileName)}\">{HtmlEncode(fileName)}</span>");
            sb.AppendLine($"                {delayLoadBadge}{dotNetBadge}");
            sb.AppendLine("                <ul class=\"nested\">");

            foreach (var dep in module.Dependents)
            {
                BuildHtmlTree(sb, dep, options, visited, depth + 1);
            }

            sb.AppendLine("                </ul>");
            sb.AppendLine("            </li>");
        }
        else
        {
            sb.AppendLine($"            <li class=\"{duplicateClass}\">");
            sb.AppendLine($"                <span class=\"module-name {statusClass}\" title=\"{HtmlEncode(module.FileName)}\">{HtmlEncode(fileName)}</span>");
            sb.AppendLine($"                {delayLoadBadge}{dotNetBadge}");
            if (isDuplicate) sb.AppendLine("                <span class=\"badge dup\">↺</span>");
            sb.AppendLine("            </li>");
        }
    }

    private static void BuildTextTree(StringBuilder sb, CModule module, string indent, bool isLast, HashSet<string> visited, int maxDepth, int depth)
    {
        if (module == null || depth > maxDepth)
            return;

        string key = module.FileName?.ToLowerInvariant() ?? "";
        bool isDuplicate = !visited.Add(key);

        string connector = isLast ? "└── " : "├── ";
        string fileName = Path.GetFileName(module.FileName);
        string status = "";

        if (module.FileNotFound) status = " [MISSING]";
        else if (module.IsInvalid) status = " [INVALID]";
        else if (module.ExportContainErrors) status = " [WARNING]";

        if (module.IsDelayLoad) status += " (delay-load)";
        if (isDuplicate) status += " (duplicate)";

        sb.AppendLine($"{indent}{connector}{fileName}{status}");

        if (!isDuplicate && module.Dependents != null && module.Dependents.Count > 0)
        {
            string childIndent = indent + (isLast ? "    " : "│   ");
            for (int i = 0; i < module.Dependents.Count; i++)
            {
                bool childIsLast = i == module.Dependents.Count - 1;
                BuildTextTree(sb, module.Dependents[i], childIndent, childIsLast, visited, maxDepth, depth + 1);
            }
        }
    }

    private static string GetModuleStatus(CModule module)
    {
        if (module.FileNotFound) return "Missing";
        if (module.IsInvalid) return "Invalid";
        if (module.ExportContainErrors || module.OtherErrorsPresent) return "Warning";
        return "OK";
    }

    private static string GetMachineName(ushort machine)
    {
        return machine switch
        {
            0x014c => "x86",
            0x0200 => "IA64",
            0x8664 => "x64",
            0xAA64 => "ARM64",
            0x01c4 => "ARM",
            0x6264 => "LoongArch64",
            _ => $"0x{machine:X4}"
        };
    }

    private static string GetSubsystemName(ushort subsystem)
    {
        return subsystem switch
        {
            0 => "Unknown",
            1 => "Native",
            2 => "Windows GUI",
            3 => "Windows CUI",
            5 => "OS/2 CUI",
            7 => "POSIX CUI",
            9 => "Windows CE",
            10 => "EFI Application",
            11 => "EFI Boot Driver",
            12 => "EFI Runtime Driver",
            13 => "EFI ROM",
            14 => "Xbox",
            16 => "Boot Application",
            _ => $"{subsystem}"
        };
    }

    private static string FormatFileSize(ulong bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private static string GetDotNodeId(string fileName)
    {
        string name = Path.GetFileName(fileName) ?? fileName;
        return "n" + Math.Abs(name.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString();
    }

    private static string GetDotNodeColor(CModule module)
    {
        if (module.FileNotFound) return "#ffcccc";
        if (module.IsInvalid) return "#ff9999";
        if (module.ExportContainErrors || module.OtherErrorsPresent) return "#ffffcc";
        if (module.IsDelayLoad) return "#ccffcc";
        if (module.IsDotNetModule) return "#ccccff";
        return "#e6f3ff";
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
    }

    private static string EscapeDotLabel(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string HtmlEncode(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return System.Net.WebUtility.HtmlEncode(value);
    }

    private static string GetHtmlStyles()
    {
        return @"
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f5f5f5; color: #333; line-height: 1.6; padding: 20px; }
        .header { background: linear-gradient(135deg, #1e3a5f 0%, #2d5a87 100%); color: white; padding: 30px; border-radius: 8px; margin-bottom: 20px; }
        .header h1 { font-size: 24px; margin-bottom: 10px; }
        .header.subtitle { font-size: 14px; opacity: 0.9; word-break: break-all; }
        .header.meta { font-size: 12px; opacity: 0.7; margin-top: 10px; }
        .summary { display: flex; gap: 20px; margin-bottom: 20px; }
        .summary-item { background: white; padding: 20px; border-radius: 8px; text-align: center; flex: 1; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .summary-item.count { display: block; font-size: 32px; font-weight: bold; color: #2d5a87; }
        .summary-item.label { font-size: 12px; color: #666; text-transform: uppercase; }
        .summary-item.missing.count { color: #dc3545; }
        .summary-item.warning.count { color: #ffc107; }
        .controls { margin-bottom: 20px; }
        .controls button { padding: 8px 16px; margin-right: 10px; border: none; border-radius: 4px; background: #2d5a87; color: white; cursor: pointer; }
        .controls button:hover { background: #1e3a5f; }
        .tree-container, .table-container { background: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        h2 { font-size: 18px; margin-bottom: 15px; color: #1e3a5f; border-bottom: 2px solid #e0e0e0; padding-bottom: 10px; }
        .tree { list-style: none; }
        .tree ul { list-style: none; padding-left: 25px; }
        .tree li { padding: 4px 0; }
        .tree .toggle { cursor: pointer; user-select: none; display: inline-block; width: 16px; color: #666; }
        .tree .collapsible > .toggle { color: #2d5a87; }
        .tree .nested { display: none; }
        .tree .active > .nested { display: block; }
        .tree .active > .toggle { transform: rotate(90deg); display: inline-block; }
        .module-name { cursor: default; }
        .module-name.missing { color: #dc3545; font-weight: bold; }
        .module-name.warning { color: #856404; }
        .badge { font-size: 10px; padding: 2px 6px; border-radius: 3px; margin-left: 5px; }
        .badge.delay { background: #d4edda; color: #155724; }
        .badge.dotnet { background: #cce5ff; color: #004085; }
        .badge.dup { background: #e2e3e5; color: #383d41; }
        .duplicate { opacity: 0.6; }
        table { width: 100%; border-collapse: collapse; font-size: 13px; }
        th, td { padding: 10px; text-align: left; border-bottom: 1px solid #e0e0e0; }
        th { background: #f8f9fa; font-weight: 600; color: #1e3a5f; }
        tr:hover { background: #f8f9fa; }
        tr.missing { background: #fff5f5; }
        tr.warning { background: #fffbf0; }
        .status-badge { padding: 3px 8px; border-radius: 3px; font-size: 11px; font-weight: 500; }
        .status-badge.ok { background: #d4edda; color: #155724; }
        .status-badge.missing { background: #f8d7da; color: #721c24; }
        .status-badge.warning { background: #fff3cd; color: #856404; }
        .path { font-family: 'Consolas', monospace; font-size: 11px; color: #666; max-width: 400px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        ";
    }

    private static string GetHtmlScript()
    {
        return @"
        document.querySelectorAll('.collapsible').forEach(function(item) {
            item.querySelector('.toggle').addEventListener('click', function() {
                item.classList.toggle('active');
            });
        });
        function expandAll() {
            document.querySelectorAll('.collapsible').forEach(function(item) {
                item.classList.add('active');
            });
        }
        function collapseAll() {
            document.querySelectorAll('.collapsible').forEach(function(item) {
                item.classList.remove('active');
            });
        }
        ";
    }

    #endregion
}

#region Export Data Models

[DataContract]
public class ExportData
{
    [System.Runtime.Serialization.DataMember(Order = 0)]
    public string GeneratedAt { get; set; }

    [System.Runtime.Serialization.DataMember(Order = 1)]
    public string ToolVersion { get; set; }

    [System.Runtime.Serialization.DataMember(Order = 2)]
    public string RootModule { get; set; }

    [System.Runtime.Serialization.DataMember(Order = 3)]
    public List<ExportModuleData> Modules { get; set; } = [];
}

[DataContract]
public class ExportModuleData
{
    [DataMember]
    public string FileName { get; set; }

    [DataMember]
    public string RawFileName { get; set; }

    [DataMember]
    public string Status { get; set; }

    [DataMember]
    public bool IsDelayLoad { get; set; }

    [DataMember]
    public bool IsForward { get; set; }

    [DataMember]
    public bool Is64Bit { get; set; }

    [DataMember]
    public bool IsDotNet { get; set; }

    [DataMember]
    public bool FileNotFound { get; set; }

    [DataMember]
    public bool IsInvalid { get; set; }

    [DataMember]
    public string ResolvedBy { get; set; }

    [DataMember]
    public ulong FileSize { get; set; }

    [DataMember]
    public uint LinkChecksum { get; set; }

    [DataMember]
    public uint RealChecksum { get; set; }

    [DataMember]
    public ulong PreferredBase { get; set; }

    [DataMember]
    public uint VirtualSize { get; set; }

    [DataMember]
    public ushort Machine { get; set; }

    [DataMember]
    public ushort Subsystem { get; set; }

    [DataMember]
    public string FileVersion { get; set; }

    [DataMember]
    public string ProductVersion { get; set; }

    [DataMember]
    public string LinkerVersion { get; set; }

    [DataMember]
    public DateTime FileTimestamp { get; set; }

    [DataMember]
    public uint LinkTimestamp { get; set; }

    [DataMember]
    public List<ExportFunctionData> Exports { get; set; }

    [DataMember]
    public List<ExportFunctionData> Imports { get; set; }

    [DataMember]
    public List<string> Dependencies { get; set; }
}

[DataContract]
public class ExportFunctionData
{
    [DataMember]
    public string Name { get; set; }

    [DataMember]
    public uint Ordinal { get; set; }

    [DataMember]
    public uint Hint { get; set; }

    [DataMember]
    public ulong Address { get; set; }

    [DataMember]
    public string ForwardName { get; set; }
}

#endregion
