
using System.IO;

namespace ResxBridge.Models;

/// <summary>
/// Represents a proposed modification to an existing RESX resource.
/// </summary>
public sealed record ImportChange(
    string FilePath,
    string Key,
    string Language,
    string Before,
    string After)
{
    public string FileName => Path.GetFileName(FilePath);
}
