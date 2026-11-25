using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ResxBridge.Models;

namespace ResxBridge.Services;

/// <summary>
/// Manages persistent application settings and
/// resource directory backup configurations.
/// </summary>
public sealed class SettingsService
{
    private const string ApplicationDirectoryName = "ResxBridge";
    private const string SettingsFileName = "Settings.json";
    private const string BackupDirectoryName = "Backups";

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    /// <summary>
    /// Gets the application data directory.
    /// </summary>
    public string ApplicationDirectory { get; }

    /// <summary>
    /// Gets the application settings file path.
    /// </summary>
    public string SettingsFilePath { get; }

    /// <summary>
    /// Gets the built-in default backup root directory.
    /// </summary>
    public string DefaultBackupDirectory { get; }

    public SettingsService()
    {
        ApplicationDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            ApplicationDirectoryName);

        SettingsFilePath = Path.Combine(
            ApplicationDirectory,
            SettingsFileName);

        DefaultBackupDirectory = Path.Combine(
            ApplicationDirectory,
            BackupDirectoryName);
    }

    /// <summary>
    /// Loads saved application settings or returns default
    /// settings when no configuration file exists.
    /// </summary>
    public ApplicationSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new ApplicationSettings();
        }

        using var stream = File.OpenRead(SettingsFilePath);

        var settings =
            JsonSerializer.Deserialize<ApplicationSettings>(
                stream,
                SerializerOptions);

        if (settings is null)
        {
            throw new InvalidDataException(
                "The application settings file is invalid.");
        }

        settings.Projects ??= new List<ProjectBackupSettings>();

        return settings;
    }

    /// <summary>
    /// Saves application settings using a temporary file
    /// to protect the existing configuration.
    /// </summary>
    public void Save(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(ApplicationDirectory);

        string temporaryPath = Path.Combine(
            ApplicationDirectory,
            $"settings-{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                JsonSerializer.Serialize(
                    stream,
                    settings,
                    SerializerOptions);

                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(SettingsFilePath))
            {
                File.Replace(
                    temporaryPath,
                    SettingsFilePath,
                    null);
            }
            else
            {
                File.Move(
                    temporaryPath,
                    SettingsFilePath);
            }
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // Cleanup must not obscure the save result.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup must not obscure the save result.
            }
        }
    }

    /// <summary>
    /// Finds the saved backup configuration associated
    /// with a resource directory.
    /// </summary>
    public ProjectBackupSettings? FindProject(
        ApplicationSettings settings,
        string resourceDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string normalizedDirectory =
            NormalizeDirectory(resourceDirectory);

        return settings.Projects.FirstOrDefault(
            project => string.Equals(
                NormalizeDirectory(project.ResourceDirectory),
                normalizedDirectory,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds or replaces the backup configuration
    /// associated with a resource directory.
    /// </summary>
    public void UpsertProject(
        ApplicationSettings settings,
        ProjectBackupSettings project)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            project.ProjectName);

        project.ResourceDirectory =
            NormalizeDirectory(project.ResourceDirectory);

        project.DirectoryId =
            CreateDirectoryId(project.ResourceDirectory);

        if (!string.IsNullOrWhiteSpace(
                project.BackupDirectoryOverride))
        {
            project.BackupDirectoryOverride =
                NormalizeDirectory(
                    project.BackupDirectoryOverride);
        }
        else
        {
            project.BackupDirectoryOverride = null;
        }

        int index = settings.Projects.FindIndex(
            existing => string.Equals(
                NormalizeDirectory(existing.ResourceDirectory),
                project.ResourceDirectory,
                StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            settings.Projects[index] = project;
        }
        else
        {
            settings.Projects.Add(project);
        }
    }

    /// <summary>
    /// Resolves the effective backup root directory
    /// for a project.
    /// </summary>
    public string ResolveBackupDirectory(
        ApplicationSettings settings,
        ProjectBackupSettings? project = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!string.IsNullOrWhiteSpace(
                project?.BackupDirectoryOverride))
        {
            return NormalizeDirectory(
                project.BackupDirectoryOverride);
        }

        if (!string.IsNullOrWhiteSpace(
                settings.DefaultBackupDirectory))
        {
            return NormalizeDirectory(
                settings.DefaultBackupDirectory);
        }

        return DefaultBackupDirectory;
    }

    /// <summary>
    /// Normalizes an absolute directory path for storage
    /// and case-insensitive comparison on Windows.
    /// </summary>
    public static string NormalizeDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        string expandedDirectory =
            Environment.ExpandEnvironmentVariables(directory);

        if (!Path.IsPathFullyQualified(expandedDirectory))
        {
            throw new ArgumentException(
                "An absolute directory path is required.",
                nameof(directory));
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(expandedDirectory));
    }

    /// <summary>
    /// Generates a stable identifier derived from
    /// the normalized resource directory path.
    /// </summary>
    public static string CreateDirectoryId(
        string resourceDirectory)
    {
        string normalizedDirectory =
            NormalizeDirectory(resourceDirectory)
                .ToUpperInvariant();

        byte[] pathBytes =
            Encoding.UTF8.GetBytes(normalizedDirectory);

        byte[] hash = SHA256.HashData(pathBytes);

        return Convert.ToHexString(hash)
            [..16]
            .ToLowerInvariant();
    }
}
