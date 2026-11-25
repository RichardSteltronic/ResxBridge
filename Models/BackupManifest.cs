namespace ResxBridge.Models;

/// <summary>
/// Describes the origin and contents of a backup snapshot.
/// </summary>
public sealed class BackupManifest
{
    /// <summary>
    /// Gets the manifest format version.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets the confirmed project name.
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// Gets the unique resource directory identifier.
    /// </summary>
    public required string DirectoryId { get; init; }

    /// <summary>
    /// Gets the original resource directory.
    /// </summary>
    public required string SourceDirectory { get; init; }

    /// <summary>
    /// Gets the associated project file path, when available.
    /// </summary>
    public string? ProjectFilePath { get; init; }

    /// <summary>
    /// Gets the UTC date and time the backup was created.
    /// </summary>
    public required DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    /// Gets the application version that created the backup.
    /// </summary>
    public required string ApplicationVersion { get; init; }

    /// <summary>
    /// Gets the number of proposed resource value changes
    /// associated with the backup operation.
    /// </summary>
    public required int ProposedChanges { get; init; }

    /// <summary>
    /// Gets the names of the resource files included
    /// in the backup snapshot.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }
}
