
using System.Text.RegularExpressions;

namespace ResxBridge.Utilities;

/// <summary>
/// Validates that translations preserve numbered .NET formatting
/// placeholders found in the original resource value.
/// </summary>
internal static partial class PlaceholderValidator
{
    [GeneratedRegex(
        @"\{\{|\}\}|\{(?<index>[0-9]+)(?:\s*,\s*-?[0-9]+)?(?::[^{}]*)?\}",
        RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    public static bool AreCompatible(
        string original,
        string translation)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(translation);

        var originalPlaceholders = ExtractPlaceholders(original);
        var translatedPlaceholders = ExtractPlaceholders(translation);

        return originalPlaceholders.SequenceEqual(
            translatedPlaceholders,
            StringComparer.Ordinal);
    }

    private static string[] ExtractPlaceholders(string value)
    {
        return PlaceholderPattern()
            .Matches(value)
            .Cast<Match>()
            .Where(match => match.Groups["index"].Success)
            .Select(match => match.Groups["index"].Value)
            .OrderBy(index => index, StringComparer.Ordinal)
            .ToArray();
    }
}
