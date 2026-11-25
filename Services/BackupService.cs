using System.IO;
using System.Reflection;
using System.Text.Json;
using ResxBridge.Models;

namespace ResxBridge.Services;

/// <summary>
/// Manages backup destinations, project backup directories,
/// resource file snapshots and backup metadata.
/// </summary>
public sealed class BackupService
{
    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    /// <summary>
    /// Resolves the project-specific directory within
    /// the selected backup root.
    /// </summary>
    public string GetProjectBackupDirectory(
        string backupRoot,
        ProjectBackupSettings project)
    {
        ArgumentNullException.ThrowIfNull(project);

        string normalizedRoot =
            SettingsService.NormalizeDirectory(backupRoot);

        string directoryId =
            SettingsService.CreateDirectoryId(
                project.ResourceDirectory);

        string projectName =
            SanitizeProjectName(project.ProjectName);

        return Path.Combine(
            normalizedRoot,
            $"{projectName}_{directoryId}");
    }

    /// <summary>
    /// Validates that the backup destination is outside
    /// the resource directory and associated project directory.
    /// </summary>
    public void ValidateBackupDirectory(
        string backupRoot,
        ProjectBackupSettings project)
    {
        ArgumentNullException.ThrowIfNull(project);

        string resourceDirectory =
            SettingsService.NormalizeDirectory(
                project.ResourceDirectory);

        string projectBackupDirectory =
            GetProjectBackupDirectory(
                backupRoot,
                project);

        if (IsSameOrDescendant(
                projectBackupDirectory,
                resourceDirectory))
        {
            throw new InvalidOperationException(
                "The backup destination cannot be inside " +
                "the selected resource directory.");
        }

        if (!string.IsNullOrWhiteSpace(
                project.ProjectFilePath))
        {
            string? projectDirectory =
                Path.GetDirectoryName(
                    Path.GetFullPath(project.ProjectFilePath));

            if (projectDirectory is not null &&
                IsSameOrDescendant(
                    projectBackupDirectory,
                    projectDirectory))
            {
                throw new InvalidOperationException(
                    "The backup destination cannot be inside " +
                    "the associated project directory.");
            }
        }

        ValidateDirectoryHierarchy(
            projectBackupDirectory);
    }

    /// <summary>
    /// Creates a backup snapshot containing the original
    /// resource files and a descriptive manifest.
    /// </summary>
    public string CreateSnapshot(
        string backupRoot,
        ProjectBackupSettings project,
        IReadOnlyDictionary<string, byte[]> originalFiles,
        int proposedChanges)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(originalFiles);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            proposedChanges);

        if (originalFiles.Count == 0)
        {
            throw new ArgumentException(
                "At least one resource file is required.",
                nameof(originalFiles));
        }

        ValidateBackupDirectory(
            backupRoot,
            project);

        string sourceDirectory =
            SettingsService.NormalizeDirectory(
                project.ResourceDirectory);

        var fileNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in originalFiles)
        {
            string filePath = Path.GetFullPath(entry.Key);

            string? parentDirectory =
                Path.GetDirectoryName(filePath);

            if (parentDirectory is null ||
                !string.Equals(
                    SettingsService.NormalizeDirectory(
                        parentDirectory),
                    sourceDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Resource file is outside the selected " +
                    $"directory: '{filePath}'.");
            }

            if (!string.Equals(
                    Path.GetExtension(filePath),
                    ".resx",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The backup source is not a RESX file: " +
                    $"'{filePath}'.");
            }

            if (!fileNames.Add(Path.GetFileName(filePath)))
            {
                throw new InvalidOperationException(
                    $"Duplicate backup filename: '{filePath}'.");
            }

            ArgumentNullException.ThrowIfNull(entry.Value);
        }

        string projectBackupDirectory =
            GetProjectBackupDirectory(
                backupRoot,
                project);

        DateTimeOffset createdUtc =
            DateTimeOffset.UtcNow;

        string snapshotName =
	        $"Backup_{createdUtc:yyyyMMdd_HHmmss_fff}Z_" +
	        $"{Guid.NewGuid():N}";

        string snapshotDirectory = Path.Combine(
            projectBackupDirectory,
            snapshotName);

        Directory.CreateDirectory(snapshotDirectory);

        try
        {
            foreach (var entry in originalFiles)
            {
                string backupPath = Path.Combine(
                    snapshotDirectory,
                    Path.GetFileName(entry.Key));

                WriteBackupFile(
                    backupPath,
                    entry.Value);
            }

            var manifest = new BackupManifest
            {
                ProjectName = project.ProjectName,
                DirectoryId =
                    SettingsService.CreateDirectoryId(
                        sourceDirectory),
                SourceDirectory = sourceDirectory,
                ProjectFilePath = project.ProjectFilePath,
                CreatedUtc = createdUtc,
                ApplicationVersion = GetApplicationVersion(),
                ProposedChanges = proposedChanges,
                Files = fileNames
                    .OrderBy(
                        name => name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };

            WriteManifest(
                snapshotDirectory,
                manifest);

            return snapshotDirectory;
        }
        catch
        {
            CleanupIncompleteSnapshot(
                snapshotDirectory);

            throw;
        }
    }

    /// <summary>
    /// Resolves the backup file corresponding to
    /// an original resource file.
    /// </summary>
    public string GetBackupFilePath(
        string snapshotDirectory,
        string originalFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            snapshotDirectory);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            originalFilePath);

        return Path.Combine(
            snapshotDirectory,
            Path.GetFileName(originalFilePath));
    }

    private static string SanitizeProjectName(
        string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectName);

        char[] invalidCharacters =
            Path.GetInvalidFileNameChars();

        string sanitizedName = new string(
            projectName
                .Trim()
                .Select(character =>
                    invalidCharacters.Contains(character)
                        ? '_'
                        : character)
                .ToArray());

        sanitizedName =
            sanitizedName.TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            sanitizedName = "Project";
        }

        if (sanitizedName.Length > 80)
        {
            sanitizedName =
                sanitizedName[..80].TrimEnd('.', ' ');
        }

        return sanitizedName;
    }

    private static bool IsSameOrDescendant(
        string candidateDirectory,
        string parentDirectory)
    {
        string candidate =
            SettingsService.NormalizeDirectory(
                candidateDirectory);

        string parent =
            SettingsService.NormalizeDirectory(
                parentDirectory);

        if (string.Equals(
                candidate,
                parent,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string parentPrefix =
            Path.EndsInDirectorySeparator(parent)
                ? parent
                : parent + Path.DirectorySeparatorChar;

        return candidate.StartsWith(
            parentPrefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateDirectoryHierarchy(
        string directory)
    {
        DirectoryInfo? currentDirectory =
            new DirectoryInfo(directory);

        while (currentDirectory is not null)
        {
            if (File.Exists(currentDirectory.FullName))
            {
                throw new IOException(
                    $"A file occupies the backup directory path: " +
                    $"'{currentDirectory.FullName}'.");
            }

            if (currentDirectory.Exists &&
                (currentDirectory.Attributes &
                 FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Backup destinations containing symbolic " +
                    "links or reparse points are not supported: " +
                    $"'{currentDirectory.FullName}'.");
            }

            currentDirectory = currentDirectory.Parent;
        }
    }

    private static void WriteBackupFile(
        string backupPath,
        byte[] contents)
    {
        using var stream = new FileStream(
            backupPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        stream.Write(contents);
        stream.Flush(flushToDisk: true);
    }

    private static void WriteManifest(
        string snapshotDirectory,
        BackupManifest manifest)
    {
        string manifestPath = Path.Combine(
            snapshotDirectory,
            ManifestFileName);

        using var stream = new FileStream(
            manifestPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        JsonSerializer.Serialize(
            stream,
            manifest,
            SerializerOptions);

        stream.Flush(flushToDisk: true);
    }

    private static string GetApplicationVersion()
    {
        var assembly =
            typeof(BackupService).Assembly;

        return assembly
            .GetCustomAttribute<
                AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "Unknown";
    }

    private static void CleanupIncompleteSnapshot(
        string snapshotDirectory)
    {
        try
        {
            Directory.Delete(
                snapshotDirectory,
                recursive: true);
        }
        catch (IOException)
        {
            // Cleanup must not obscure the original failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup must not obscure the original failure.
        }
    }
}
