
using System.Xml.Linq;

namespace ResxBridge.Utilities;

/// <summary>
/// Provides shared validation for RESX resource elements.
/// </summary>
internal static class ResxResourceHelper
{
    /// <summary>
    /// Determines whether a resource element represents a plain string.
    /// </summary>
    public static bool IsStringResource(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (element.Attribute("mimetype") is not null)
        {
            return false;
        }

        string? resourceType =
            element.Attribute("type")?.Value;

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
}
