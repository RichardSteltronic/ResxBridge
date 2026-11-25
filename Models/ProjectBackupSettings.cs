namespace ResxBridge.Models;

/// <summary>
/// Represents the confirmed backup configuration
/// for a specific resource directory.
/// </summary>
public sealed class ProjectBackupSettings
{
    /// <summary>
    /// Gets or sets the user-confirmed project name.
    /// </summary>
    public required string ProjectName { get; set; }

    /// <summary>
    /// Gets or sets the absolute path of the resource directory.
    /// </summary>
    public required string ResourceDirectory { get; set; }

    /// <summary>
    /// Gets or sets the detected or selected project file path,
    /// when available.
    /// </summary>
    public string? ProjectFilePath { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier associated
    /// with the resource directory.
    /// </summary>
    public required string DirectoryId { get; set; }

    /// <summary>
    /// Gets or sets the project-specific backup root directory.
    /// A null value indicates the application-wide default.
    /// </summary>
    public string? BackupDirectoryOverride { get; set; }
}
