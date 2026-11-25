
namespace ResxBridge.Models;

/// <summary>
/// Contains a resource file's metadata and its extracted string resources.
/// </summary>
public sealed class ResourceFileData
{
    public required ResourceFileInfo File { get; init; }

    public required IReadOnlyDictionary<string, string> Entries { get; init; }
}
