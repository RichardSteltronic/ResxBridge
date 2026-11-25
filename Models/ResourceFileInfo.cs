
using ResxBridge.Utilities;

namespace ResxBridge.Models;

/// <summary>
/// Describes a discovered RESX resource file and its associated culture.
/// </summary>
public sealed class ResourceFileInfo
{
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public required string ResourceFamily { get; init; }

    public required string Language { get; init; }

    public required string Culture { get; init; }

    public required int EntryCount { get; init; }

    public bool IsNeutral => CultureHelper.IsNeutral(Culture);
}
