
using System.Globalization;

namespace ResxBridge.Utilities;

/// <summary>
/// Provides culture identification and display names for RESX resources.
/// </summary>
public static class CultureHelper
{
    public const string DefaultCulture = "Default";

    public const string DefaultLanguage = "Neutral / Default";

    /// <summary>
    /// Determines whether the supplied identifier represents
    /// the neutral resource culture.
    /// </summary>
    public static bool IsNeutral(string? cultureName)
    {
        return string.IsNullOrWhiteSpace(cultureName) ||
               string.Equals(
                   cultureName,
                   DefaultCulture,
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to resolve a valid .NET culture identifier.
    /// </summary>
    public static bool TryResolveCulture(
        string? cultureName,
        out CultureInfo? culture)
    {
        culture = null;

        if (IsNeutral(cultureName))
        {
            return false;
        }

        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName!);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns a normalized culture identifier.
    /// </summary>
    public static string NormalizeCulture(string? cultureName)
    {
        if (IsNeutral(cultureName))
        {
            return DefaultCulture;
        }

        if (TryResolveCulture(cultureName, out var culture))
        {
            return culture!.Name;
        }

        throw new ArgumentException(
            $"Invalid culture identifier: '{cultureName}'.",
            nameof(cultureName));
    }

    /// <summary>
    /// Returns a readable English display name for a culture.
    /// </summary>
    public static string GetLanguageName(string? cultureName)
    {
        if (IsNeutral(cultureName))
        {
            return DefaultLanguage;
        }

        if (TryResolveCulture(cultureName, out var culture))
        {
            return culture!.EnglishName;
        }

        return cultureName!;
    }
}
