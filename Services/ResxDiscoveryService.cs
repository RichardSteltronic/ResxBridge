
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using ResxBridge.Models;
using ResxBridge.Utilities;

namespace ResxBridge.Services;

/// <summary>
/// Discovers RESX resource files and extracts their translatable
/// string entries without modifying the source files.
/// </summary>
public sealed class ResxDiscoveryService
{
    private static readonly HashSet<string> _knownCultures = new(
        CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Select(culture => culture.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name)),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Discovers and reads RESX files within the specified directory.
    /// </summary>
    public IReadOnlyList<ResourceFileData> Discover(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException(
                $"Resource directory was not found: '{directoryPath}'.");
        }

        var resourceFiles = Directory
            .EnumerateFiles(
                directoryPath,
                "*.resx",
                SearchOption.TopDirectoryOnly)
            .Select(ReadResourceFile)
            .OrderBy(
                resource => resource.File.ResourceFamily,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(resource => !resource.File.IsNeutral)
            .ThenBy(
                resource => resource.File.Culture,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        return resourceFiles.AsReadOnly();
    }

    /// <summary>
    /// Reads a RESX file and extracts its translatable string entries.
    /// </summary>
    public ResourceFileData ReadResourceFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The resource file was not found.",
                fullPath);
        }

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".resx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The specified file is not a RESX resource file.",
                nameof(filePath));
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(fullPath, settings);

        var document = XDocument.Load(
            reader,
            LoadOptions.PreserveWhitespace);

        if (document.Root is null ||
            document.Root.Name != "root")
        {
            throw new InvalidDataException(
                $"Invalid RESX document: '{fullPath}'.");
        }

        var entries = ExtractStringEntries(
            document.Root,
            fullPath);

        string fileName = Path.GetFileName(fullPath);

        var (resourceFamily, culture) =
            ParseResourceFileName(fileName);

        var fileInfo = new ResourceFileInfo
        {
            FilePath = fullPath,
            FileName = fileName,
            ResourceFamily = resourceFamily,
            Culture = culture,
            Language = CultureHelper.GetLanguageName(culture),
            EntryCount = entries.Count
        };

        return new ResourceFileData
        {
            File = fileInfo,
            Entries = new ReadOnlyDictionary<string, string>(entries)
        };
    }

    /// <summary>
    /// Extracts string resources while excluding serialized objects,
    /// binary resources, and external file references.
    /// </summary>
    private static Dictionary<string, string> ExtractStringEntries(
        XElement root,
        string filePath)
    {
        var entries = new Dictionary<string, string>(
            StringComparer.Ordinal);

        var encounteredKeys = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var element in root.Elements("data"))
        {
            string? key = element.Attribute("name")?.Value;

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidDataException(
                    $"A resource entry has no valid key in '{filePath}'.");
            }

            if (!encounteredKeys.Add(key))
            {
                throw new InvalidDataException(
                    $"Duplicate resource key '{key}' in '{filePath}'.");
            }

            if (!ResxResourceHelper.IsStringResource(element))
            {
                continue;
            }

            var valueElement = element.Element("value");

            if (valueElement is null)
            {
                throw new InvalidDataException(
                    $"Resource '{key}' has no value element in '{filePath}'.");
            }

            entries.Add(key, valueElement.Value);
        }

        return entries;
    }

    /// <summary>
    /// Determines whether a resource entry contains a plain string.
    /// </summary>
    private static bool IsStringResource(XElement element)
    {
        if (element.Attribute("mimetype") is not null)
        {
            return false;
        }

        string? resourceType = element.Attribute("type")?.Value;

        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return true;
        }

        string typeName = resourceType
            .Split(',', 2)[0]
            .Trim();

        return string.Equals(
            typeName,
            "System.String",
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Separates a resource filename into its family and culture.
    /// </summary>
    private static (string ResourceFamily, string Culture)
        ParseResourceFileName(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);

        int separatorIndex = name.LastIndexOf('.');

        if (separatorIndex <= 0 ||
            separatorIndex == name.Length - 1)
        {
            return (name, CultureHelper.DefaultCulture);
        }

        string candidate = name[(separatorIndex + 1)..];

        if (!_knownCultures.Contains(candidate))
        {
            return (name, CultureHelper.DefaultCulture);
        }

        string resourceFamily = name[..separatorIndex];

        string culture = CultureHelper.NormalizeCulture(candidate);

        return (resourceFamily, culture);
    }
}
