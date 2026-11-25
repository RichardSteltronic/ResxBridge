namespace ResxBridge.Models;

/// <summary>
/// Represents persistent application preferences and
/// resource directory backup configurations.
/// </summary>
public sealed class ApplicationSettings
{
    /// <summary>
    /// Gets or sets the default backup root directory.
    /// A null value indicates the application default.
    /// </summary>
    public string? DefaultBackupDirectory { get; set; }

    /// <summary>
    /// Gets or sets the last selected resource directory.
    /// </summary>
    public string? LastResourceDirectory { get; set; }

    /// <summary>
    /// Gets or sets the saved backup configurations
    /// associated with resource directories.
    /// </summary>
    public List<ProjectBackupSettings> Projects { get; set; } = new();
}
