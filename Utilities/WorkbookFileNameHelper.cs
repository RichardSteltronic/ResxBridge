using System.IO;

namespace ResxBridge.Utilities;

/// <summary>
/// Generates descriptive filenames for exported translation workbooks.
/// </summary>
public static class WorkbookFileNameHelper
{
    private const int MaxListedCultures = 5;
    private const int MaxProjectNameLength = 80;

    public static string Create(
        string? projectName,
        IEnumerable<string> selectedCultures,
        int availableCultureCount)
    {
        ArgumentNullException.ThrowIfNull(selectedCultures);

        var cultures = selectedCultures
            .Where(culture => !string.IsNullOrWhiteSpace(culture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(culture => culture, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string languageSuffix = cultures.Length switch
        {
            0 => "Default",
            1 => cultures[0],
            _ when cultures.Length == availableCultureCount => "All",
            _ when cultures.Length <= MaxListedCultures =>
                string.Join("_", cultures),
            _ => $"{cultures.Length}Languages"
        };

        string safeProjectName = SanitizeProjectName(projectName);

        string prefix = string.IsNullOrWhiteSpace(safeProjectName)
            ? "Translations"
            : $"{safeProjectName}_Translations";

        return $"{prefix}_{languageSuffix}_{DateTime.Now:yyyyMMdd}.xlsx";
    }

    private static string SanitizeProjectName(string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return string.Empty;
        }

        char[] invalidCharacters = Path.GetInvalidFileNameChars();

        string sanitized = new string(
            projectName.Trim()
                .Select(character =>
                    invalidCharacters.Contains(character)
                        ? '_'
                        : character)
                .ToArray());

        if (sanitized.Length > MaxProjectNameLength)
        {
            sanitized = sanitized[..MaxProjectNameLength];
        }

        return sanitized.TrimEnd('.', ' ');
    }
}
