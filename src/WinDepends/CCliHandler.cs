/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CCLIHANDLER.CS
*
*  VERSION:     1.00
*
*  DATE:        29 Nov 2025
*  
*  Implementation of command-line interface handler.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.InteropServices;

namespace WinDepends;

/// <summary>
/// Command-line options for CLI mode.
/// </summary>
public class CliOptions
{
    public string InputFile { get; set; }
    public string OutputFile { get; set; }
    public ExportFormat Format { get; set; } = ExportFormat.Json;
    public int MaxDepth { get; set; } = int.MaxValue;
    public bool IncludeExports { get; set; } = true;
    public bool IncludeImports { get; set; } = true;
    public bool Quiet { get; set; } = false;
    public bool ResolveApiSets { get; set; } = true;
    public bool UseKernelSearchOrder { get; set; } = false;
    public bool ShowHelp { get; set; } = false;
    public bool ShowVersion { get; set; } = false;
    public bool FullPaths { get; set; } = true;
}

/// <summary>
/// Handles command-line interface mode of the application.
/// </summary>
public static class CCliHandler
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    private const int ATTACH_PARENT_PROCESS = -1;

    /// <summary>
    /// Determines if the application should run in CLI mode based on command-line arguments.
    /// </summary>
    public static bool ShouldRunAsCli(string[] args)
    {
        if (args == null || args.Length == 0)
            return false;

        foreach (var arg in args)
        {
            string lowerArg = arg.ToLowerInvariant();

            if (lowerArg == "-o" || lowerArg == "--output" ||
                lowerArg == "-f" || lowerArg == "--format" ||
                lowerArg == "-q" || lowerArg == "--quiet" ||
                lowerArg == "-h" || lowerArg == "--help" ||
                lowerArg == "-v" || lowerArg == "--version" ||
                lowerArg == "-?" ||
                lowerArg.StartsWith("--output=") ||
                lowerArg.StartsWith("--format="))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parse command-line arguments into CliOptions.
    /// </summary>
    public static CliOptions ParseArguments(string[] args)
    {
        var options = new CliOptions();
        int i = 0;

        while (i < args.Length)
        {
            string arg = args[i];
            string lowerArg = arg.ToLowerInvariant();

            if (lowerArg == "-h" || lowerArg == "--help" || lowerArg == "-? ")
            {
                options.ShowHelp = true;
                i++;
            }
            else if (lowerArg == "-v" || lowerArg == "--version")
            {
                options.ShowVersion = true;
                i++;
            }
            else if (lowerArg == "-o" || lowerArg == "--output")
            {
                if (i + 1 < args.Length)
                {
                    options.OutputFile = args[++i];
                }
                i++;
            }
            else if (lowerArg.StartsWith("--output="))
            {
                options.OutputFile = arg.Substring(9);
                i++;
            }
            else if (lowerArg == "-f" || lowerArg == "--format")
            {
                if (i + 1 < args.Length)
                {
                    options.Format = ParseFormat(args[++i]);
                }
                i++;
            }
            else if (lowerArg.StartsWith("--format="))
            {
                options.Format = ParseFormat(arg.Substring(9));
                i++;
            }
            else if (lowerArg == "-d" || lowerArg == "--depth")
            {
                if (i + 1 < args.Length && int.TryParse(args[++i], out int depth))
                {
                    options.MaxDepth = depth;
                }
                i++;
            }
            else if (lowerArg.StartsWith("--depth="))
            {
                if (int.TryParse(arg.Substring(8), out int depth))
                {
                    options.MaxDepth = depth;
                }
                i++;
            }
            else if (lowerArg == "-q" || lowerArg == "--quiet")
            {
                options.Quiet = true;
                i++;
            }
            else if (lowerArg == "-e" || lowerArg == "--exports")
            {
                options.IncludeExports = true;
                i++;
            }
            else if (lowerArg == "--no-exports")
            {
                options.IncludeExports = false;
                i++;
            }
            else if (lowerArg == "-i" || lowerArg == "--imports")
            {
                options.IncludeImports = true;
                i++;
            }
            else if (lowerArg == "--no-imports")
            {
                options.IncludeImports = false;
                i++;
            }
            else if (lowerArg == "--no-resolve")
            {
                options.ResolveApiSets = false;
                i++;
            }
            else if (lowerArg == "--kernel" || lowerArg == "-k")
            {
                options.UseKernelSearchOrder = true;
                i++;
            }
            else if (lowerArg == "--short-paths")
            {
                options.FullPaths = false;
                i++;
            }
            else if (!arg.StartsWith("-") && string.IsNullOrEmpty(options.InputFile))
            {
                options.InputFile = arg;
                i++;
            }
            else
            {
                i++;
            }
        }

        return options;
    }

    /// <summary>
    /// Run the CLI handler.
    /// </summary>
    public static int Run(string[] args)
    {
        if (!AttachConsole(ATTACH_PARENT_PROCESS))
        {
            AllocConsole();
        }

        try
        {
            var options = ParseArguments(args);

            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            if (options.ShowVersion)
            {
                PrintVersion();
                return 0;
            }

            if (string.IsNullOrEmpty(options.InputFile))
            {
                Console.Error.WriteLine("Error: No input file specified.");
                Console.Error.WriteLine("Use --help for usage information.");
                return 1;
            }

            if (!File.Exists(options.InputFile))
            {
                Console.Error.WriteLine($"Error: File not found: {options.InputFile}");
                return 1;
            }

            if (string.IsNullOrEmpty(options.OutputFile))
            {
                string ext = options.Format switch
                {
                    ExportFormat.Json => ".json",
                    ExportFormat.Csv => ".csv",
                    ExportFormat.Html => ".html",
                    ExportFormat.Dot => ".dot",
                    ExportFormat.Text => ".txt",
                    _ => ".json"
                };
                options.OutputFile = Path.ChangeExtension(options.InputFile, ext);
            }

            return RunAnalysis(options);
        }
        finally
        {
            FreeConsole();
        }
    }

    /// <summary>
    /// Resolves the core server application path.
    /// First tries configuration path, then falls back to default location.
    /// </summary>
    private static string ResolveCoreServerPath(CConfiguration config)
    {
        if (!string.IsNullOrEmpty(config.CoreServerAppLocation) && File.Exists(config.CoreServerAppLocation))
        {
            return config.CoreServerAppLocation;
        }

        string cpuArch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? "";
        string defaultPath = Path.Combine(exeDir, $"{CConsts.WinDependsCoreApp}.{cpuArch}.exe");

        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        return null;
    }

    private static int RunAnalysis(CliOptions options)
    {
        if (!options.Quiet)
        {
            Console.WriteLine($"WinDepends CLI v{CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}");
            Console.WriteLine($"Analyzing: {options.InputFile}");
        }

        var config = CConfigManager.LoadConfiguration();
        config.ResolveAPIsets = options.ResolveApiSets;
        config.FullPaths = options.FullPaths;

        string serverApp = ResolveCoreServerPath(config);

        if (string.IsNullOrEmpty(serverApp))
        {
            Console.Error.WriteLine("Error: Core server not found.");
            Console.Error.WriteLine("Checked locations:");
            if (!string.IsNullOrEmpty(config.CoreServerAppLocation))
            {
                Console.Error.WriteLine($"  Configuration: {config.CoreServerAppLocation}");
            }
            string cpuArch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? "";
            Console.Error.WriteLine($"  Default: {Path.Combine(exeDir, $"{CConsts.WinDependsCoreApp}.{cpuArch}.exe")}");
            return 1;
        }

        void LogMessage(string message, LogMessageType type, Color? color = null,
            bool useBold = false, bool moduleMessage = false, CModule relatedModule = null)
        {
            if (!options.Quiet)
            {
                string prefix = type == LogMessageType.ErrorOrWarning ? "[! ] " : "[*] ";
                Console.WriteLine(prefix + message);
            }
        }

        using var coreClient = new CCoreClient(serverApp, CConsts.CoreServerAddress, LogMessage);

        if (!coreClient.ConnectClient())
        {
            Console.Error.WriteLine("Error: Failed to connect to core server.");
            return 1;
        }

        CActCtxHelper actCtxHelper = null;

        try
        {
            CPathResolver.KnownDlls.Clear();
            CPathResolver.KnownDlls32.Clear();
            coreClient.GetKnownDllsAll(
                CPathResolver.KnownDlls,
                CPathResolver.KnownDlls32,
                out string knownDllsPath,
                out string knownDllsPath32);
            CPathResolver.KnownDllsPath = knownDllsPath;
            CPathResolver.KnownDllsPath32 = knownDllsPath32;

            CPathResolver.UserDirectoriesUM = config.UserSearchOrderDirectoriesUM;
            CPathResolver.UserDirectoriesKM = config.UserSearchOrderDirectoriesKM;

            var session = new CDepends(options.InputFile);
            var rootModule = session.RootModule;

            var searchOrderUM = options.UseKernelSearchOrder ? config.SearchOrderListKM : config.SearchOrderListUM;
            var searchOrderKM = config.SearchOrderListKM;

            var fileOpenSettings = new CFileOpenSettings(config);

            var status = coreClient.OpenModule(ref rootModule, fileOpenSettings);
            if (status != ModuleOpenStatus.Okay)
            {
                if (!options.Quiet)
                {
                    Console.WriteLine($"Warning: Module open status: {status}");
                }

                if (rootModule.FileNotFound)
                {
                    Console.Error.WriteLine($"Error: File not found or inaccessible: {options.InputFile}");
                    return 1;
                }
            }

            coreClient.GetModuleHeadersInformation(rootModule);

            actCtxHelper = new CActCtxHelper(rootModule.FileName);
            CPathResolver.ActCtxHelper = actCtxHelper;
            CPathResolver.Initialized = false;
            CPathResolver.QueryFileInformation(rootModule);

            var parentImportsHashTable = new Dictionary<int, FunctionHashObject>();
            coreClient.GetModuleImportExportInformation(
                rootModule,
                searchOrderUM.ToList(),
                searchOrderKM.ToList(),
                parentImportsHashTable,
                config.EnableExperimentalFeatures,
                config.ExpandForwarders);

            coreClient.CloseModule();

            if (!options.Quiet)
            {
                Console.WriteLine("Processing dependencies...");
            }

            var processedModulesData = new Dictionary<string, CModule>(StringComparer.OrdinalIgnoreCase);
            processedModulesData[rootModule.FileName.ToLowerInvariant()] = rootModule;

            int processedCount = 0;
            ProcessDependentsRecursive(
                coreClient,
                rootModule,
                searchOrderUM.ToList(),
                searchOrderKM.ToList(),
                parentImportsHashTable,
                fileOpenSettings,
                config,
                options.MaxDepth,
                0,
                processedModulesData,
                options.Quiet,
                ref processedCount);

            if (!options.Quiet)
            {
                Console.WriteLine($"Exporting to {options.Format}: {options.OutputFile}");
            }

            var exportOptions = new ExportOptions
            {
                IncludeExports = options.IncludeExports,
                IncludeImports = options.IncludeImports,
                FullPaths = options.FullPaths,
                MaxDepth = options.MaxDepth
            };

            if (CExporter.Export(session, options.OutputFile, options.Format, exportOptions))
            {
                if (!options.Quiet)
                {
                    Console.WriteLine("Export completed successfully.");
                }
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Error: Export failed.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            actCtxHelper?.Dispose();
            CPathResolver.ActCtxHelper = null;
            coreClient.DisconnectClient();
        }
    }

    private static void ProcessDependentsRecursive(
        CCoreClient coreClient,
        CModule parentModule,
        List<SearchOrderType> searchOrderUM,
        List<SearchOrderType> searchOrderKM,
        Dictionary<int, FunctionHashObject> parentImportsHashTable,
        CFileOpenSettings fileOpenSettings,
        CConfiguration config,
        int maxDepth,
        int currentDepth,
        Dictionary<string, CModule> processedModulesData,
        bool quiet,
        ref int processedCount)
    {
        if (currentDepth >= maxDepth || parentModule.Dependents == null)
            return;

        for (int i = 0; i < parentModule.Dependents.Count; i++)
        {
            var dep = parentModule.Dependents[i];
            string key = dep.FileName?.ToLowerInvariant() ?? "";

            if (string.IsNullOrEmpty(key))
                continue;

            if (processedModulesData.TryGetValue(key, out CModule existingModule))
            {
                dep.ModuleData = new CModuleData(existingModule.ModuleData);
                dep.FileNotFound = existingModule.FileNotFound;
                dep.IsInvalid = existingModule.IsInvalid;
                dep.ExportContainErrors = existingModule.ExportContainErrors;
                dep.OtherErrorsPresent = existingModule.OtherErrorsPresent;
                dep.IsDotNetModule = existingModule.IsDotNetModule;
                dep.OriginalInstanceId = existingModule.InstanceId;
                parentModule.Dependents[i] = dep;
                continue;
            }

            if (dep.FileNotFound || dep.IsInvalid)
            {
                processedModulesData[key] = dep;
                continue;
            }

            if (!quiet)
            {
                processedCount++;
                Console.WriteLine($"  [{processedCount}] Analyzing: {Path.GetFileName(dep.FileName)}");
            }

            var status = coreClient.OpenModule(ref dep, fileOpenSettings);
            if (status == ModuleOpenStatus.Okay)
            {
                coreClient.GetModuleHeadersInformation(dep);

                coreClient.GetModuleImportExportInformation(
                    dep,
                    searchOrderUM,
                    searchOrderKM,
                    parentImportsHashTable,
                    config.EnableExperimentalFeatures,
                    config.ExpandForwarders);

                coreClient.CloseModule();
            }
            else if (status == ModuleOpenStatus.ErrorFileNotFound)
            {
                dep.FileNotFound = true;
            }
            else
            {
                dep.IsInvalid = true;
            }

            dep.InstanceId = dep.GetHashCode();
            processedModulesData[key] = dep;
            parentModule.Dependents[i] = dep;

            ProcessDependentsRecursive(
                coreClient,
                dep,
                searchOrderUM,
                searchOrderKM,
                parentImportsHashTable,
                fileOpenSettings,
                config,
                maxDepth,
                currentDepth + 1,
                processedModulesData,
                quiet,
                ref processedCount);
        }
    }

    private static ExportFormat ParseFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => ExportFormat.Json,
            "csv" => ExportFormat.Csv,
            "html" => ExportFormat.Html,
            "dot" => ExportFormat.Dot,
            "graphviz" => ExportFormat.Dot,
            "text" => ExportFormat.Text,
            "txt" => ExportFormat.Text,
            _ => ExportFormat.Json
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine($@"
WinDepends - Windows Dependency Analyzer
Version {CConsts.VersionMajor}.{CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild}.{CConsts.VersionRevision}

Usage: WinDepends.exe <input-file> [options]

Options:
  -o, --output <file>     Output file path (default: input file with format extension)
  -f, --format <format>   Output format: json, csv, html, dot, text (default: json)
  -d, --depth <n>         Maximum dependency depth (default: from configuration)
  -q, --quiet             Suppress console output
  -e, --exports           Include export information (default: on)
  --no-exports            Exclude export information
  -i, --imports           Include import information (default: on)
  --no-imports            Exclude import information
  --no-resolve            Don't resolve API set names (default: from configuration)
  -k, --kernel            Use kernel-mode search order
  --short-paths           Use short file names instead of full paths (default: from configuration)
  -h, --help              Show this help message
  -v, --version           Show version information

Note: Many default values are read from the program configuration file.
      Use the GUI to configure these settings.

Examples:
  WinDepends.exe myapp.exe -o report.html -f html
  WinDepends.exe driver.sys -f json -k --no-imports
  WinDepends.exe module.dll -f dot | dot -Tpng -o graph.png

Formats:
  json      Full structured JSON data
  csv       Flat module list as CSV
  html      Interactive HTML report with collapsible tree
  dot       Graphviz DOT format for graph visualization
  text      Plain text tree output
");
    }

    private static void PrintVersion()
    {
        Console.WriteLine($"WinDepends version {CConsts.VersionMajor}. {CConsts.VersionMinor}.{CConsts.VersionRevision}.{CConsts.VersionBuild}");
        string copyright = CConsts.CopyrightString.Replace("\u00A9", "(C)").Replace("©", "(C)");
        Console.WriteLine(copyright);
    }
}
