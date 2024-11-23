/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024
*
*  TITLE:       CSXSMANIFEST.CS
*
*  VERSION:     1.00
*
*  DATE:        19 Oct 2024
*  
*  Implementation of basic sxs manifest parser class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Xml.Linq;

namespace WinDepends;

public class CSxsEntry
{
    public string Name { get; set; }
    public string FilePath { get; set; }

    public CSxsEntry(string name, string filePath)
    {
        Name = name;
        FilePath = filePath;
    }

    public CSxsEntry(CSxsEntry entry) : this(entry.Name, entry.FilePath)
    {
    }

    public CSxsEntry(XElement SxsFile, string directoryName)
    {
        string loadFrom = SxsFile?.Attribute("loadFrom")?.Value ?? string.Empty;
        Name = Path.GetFileName(SxsFile?.Attribute("name")?.Value ?? string.Empty);

        if (!string.IsNullOrEmpty(loadFrom))
        {
            loadFrom = Environment.ExpandEnvironmentVariables(loadFrom);

            if (!Path.IsPathRooted(loadFrom))
            {
                loadFrom = Path.Combine(directoryName, loadFrom);
            }

            FilePath = loadFrom.EndsWith(Path.DirectorySeparatorChar.ToString()) ?
                Path.Combine(loadFrom, Name) :
                Path.ChangeExtension(loadFrom, ".dll");
        }
        else
        {
            FilePath = Path.Combine(directoryName, Name);
        }
    }
}

public class CSxsEntries : List<CSxsEntry>
{
    public static CSxsEntries FromSxsAssemblyElementFile(XElement SxsAssembly, XNamespace Namespace, string directoryName)
    {
        CSxsEntries entries = [];

        foreach (XElement SxsFile in SxsAssembly.Elements(Namespace + "file"))
        {
            entries.Add(new(SxsFile, directoryName));
        }

        return entries;
    }
}

public class CSxsManifest
{
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
            int startIndex = 0;
            while ((startIndex = manifestText.IndexOf("\"\"", startIndex)) != -1)
            {
                int endIndex = manifestText.IndexOf("\"\"", startIndex + 2);
                if (endIndex != -1)
                {
                    string capturedGroup = manifestText.Substring(startIndex + 2, endIndex - startIndex - 2);
                    manifestText = manifestText.Remove(startIndex, endIndex - startIndex + 2).Insert(startIndex, "\"" + capturedGroup + "\"");
                }
            }

            // Replace specific strings (garbage or bug).
            manifestText = manifestText.Replace("SXS_PROCESSOR_ARCHITECTURE", "\"amd64\"", StringComparison.OrdinalIgnoreCase)
                                   .Replace("SXS_ASSEMBLY_VERSION", "\"\"", StringComparison.OrdinalIgnoreCase)
                                   .Replace("SXS_ASSEMBLY_NAME", "\"\"", StringComparison.OrdinalIgnoreCase);

            // Remove blank lines.
            manifestText = string.Join(Environment.NewLine, manifestText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));

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

        var bytes = module.GetManifestDataAsArray();
        if (bytes != null)
        {
            module.SetManifestData(string.Empty);
            using (Stream manifestStream = new System.IO.MemoryStream(bytes))
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
}
