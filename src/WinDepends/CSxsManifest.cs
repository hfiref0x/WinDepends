/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CSXSMANIFEST.CS
*
*  VERSION:     1.00
*
*  DATE:        14 Aug 2025
*  
*  Implementation of basic sxs manifest parser class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WinDepends;

public class CSxsEntry
{
    public string Name { get; }
    public string FilePath { get; }

    public CSxsEntry(string name, string filePath)
    {
        Name = name ?? string.Empty;
        FilePath = filePath ?? string.Empty;
    }

    public CSxsEntry(CSxsEntry entry) : this(entry?.Name, entry?.FilePath)
    {
    }

    public CSxsEntry(XElement sxsFile, string directoryName)
    {
        string loadFrom = sxsFile?.Attribute("loadFrom")?.Value ?? string.Empty;
        Name = Path.GetFileName(sxsFile?.Attribute("name")?.Value ?? string.Empty);

        if (!string.IsNullOrEmpty(loadFrom))
        {
            loadFrom = Environment.ExpandEnvironmentVariables(loadFrom);

            if (!Path.IsPathRooted(loadFrom))
            {
                loadFrom = Path.Combine(directoryName, loadFrom);
            }

            FilePath = loadFrom.EndsWith(Path.DirectorySeparatorChar.ToString()) ?
                Path.Combine(loadFrom, Name) :
                Path.ChangeExtension(loadFrom, CConsts.DllFileExt);
        }
        else
        {
            FilePath = Path.Combine(directoryName, Name);
        }
    }
}

public class CSxsEntries : List<CSxsEntry>
{
    public static CSxsEntries FromSxsAssemblyElementFile(XElement sxsAssembly, XNamespace Namespace, string directoryName)
    {
        CSxsEntries entries = [];

        foreach (XElement sxsFile in sxsAssembly.Elements(Namespace + "file"))
        {
            entries.Add(new(sxsFile, directoryName));
        }

        return entries;
    }
}

internal partial class CSxsManifest
{
    private static readonly Regex s_doubleQuotesRegex = SxsTrimDoubleQuotesRegex();

    public static CSxsEntries QueryInformationFromManifestFile(string fileName, string directoryName, out bool bAutoElevate)
    {
        using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
            return QueryInformationFromManifest(fs, directoryName, out bAutoElevate);
        }
    }

    public static CSxsEntries QueryInformationFromManifest(Stream ManifestStream, string directoryName, out bool bAutoElevate)
    {
        bAutoElevate = false;
        var xDoc = ParseSxsManifest(ManifestStream);
        if (xDoc == null)
        {
            return [];
        }

        XNamespace ns = "http://schemas.microsoft.com/SMI/2005/WindowsSettings";
        var autoElevate = xDoc.Descendants(ns + "autoElevate").Select(x => x.Value).FirstOrDefault();
        if (autoElevate != null)
        {
            bAutoElevate = autoElevate.StartsWith('t');
        }

        var Namespace = xDoc.Root.GetDefaultNamespace();
        var sxsDependencies = new CSxsEntries();

        foreach (XElement SxsAssembly in xDoc.Descendants(Namespace + "assembly"))
        {
            var entries = CSxsEntries.FromSxsAssemblyElementFile(SxsAssembly, Namespace, directoryName);
            sxsDependencies.AddRange(entries);
        }

        return sxsDependencies;
    }

    public static XDocument ParseSxsManifest(Stream ManifestStream)
    {
        XDocument xDoc = null;

        using (StreamReader xStream = new(ManifestStream))
        {
            string manifestText = xStream.ReadToEnd();

            // Cleanup manifest from possible trash:
            // 1. Double quotes in attributes.
            // 2. Replace unknown garbage or undefined macro.
            // 3. Blank lines.

            // Trim double quotes in attributes.
            manifestText = s_doubleQuotesRegex.Replace(manifestText, "\"$1\"");

            // Replace specific strings (garbage or bug).
            manifestText = manifestText.Replace("SXS_PROCESSOR_ARCHITECTURE", (IntPtr.Size == 8) ? "\"amd64\"" : "\"x86\"", StringComparison.OrdinalIgnoreCase)
                                   .Replace("SXS_ASSEMBLY_VERSION", "\"\"", StringComparison.OrdinalIgnoreCase)
                                   .Replace("SXS_ASSEMBLY_NAME", "\"\"", StringComparison.OrdinalIgnoreCase);

            // Remove blank lines.
            manifestText = string.Join(Environment.NewLine, manifestText.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries));

            try
            {
                xDoc = XDocument.Parse(manifestText);
            }
            catch
            {
                return null;
            }
        }

        return xDoc;
    }

    public static CSxsEntries GetManifestInformation(CModule module, string moduleDirectoryName, out bool bAutoElevate)
    {
        CSxsEntries sxsEntries = [];
        bAutoElevate = false;

        if (module == null)
        {
            return sxsEntries;
        }

        // Process manifest entries.
        // First check embedded manifest as it seems now has advantage over external.

        var manifestBytes = module.GetManifestDataAsArray();
        if (manifestBytes != null && manifestBytes.Length > 0)
        {
            module.SetManifestData(string.Empty);
            using (Stream manifestStream = new System.IO.MemoryStream(manifestBytes))
            {
                sxsEntries = QueryInformationFromManifest(manifestStream, moduleDirectoryName, out bAutoElevate);
            }
        }
        else
        {
            // No embedded manifest has been found or there is an error.
            // Is external manifest present?
            string externalManifest = $"{module.FileName}.manifest";

            if (File.Exists(externalManifest))
            {
                sxsEntries = QueryInformationFromManifestFile(externalManifest, moduleDirectoryName, out bAutoElevate);
            }
        }

        return sxsEntries;

    }

    [GeneratedRegex("\\\"\\\"([\\w\\d\\.]*)\\\"\\\"")]
    private static partial Regex SxsTrimDoubleQuotesRegex();
}
