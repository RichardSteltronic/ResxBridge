using System.IO;

namespace ResxBridge.Services;

/// <summary>
/// Discovers candidate project files associated with
/// a selected resource directory.
/// </summary>
public sealed class ProjectDetectionService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj",
            ".vbproj",
            ".fsproj"
        };

    /// <summary>
    /// Finds supported project files in the nearest ancestor
    /// directory containing one or more project files.
    /// </summary>
    public IReadOnlyList<string> FindProjectFiles(
        string resourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            resourceDirectory);

        string fullPath = Path.GetFullPath(
            resourceDirectory);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Resource directory was not found: '{fullPath}'.");
        }

        DirectoryInfo? currentDirectory =
            new DirectoryInfo(fullPath);

        while (currentDirectory is not null)
        {
            var projectFiles = currentDirectory
                .EnumerateFiles(
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(file =>
                    SupportedExtensions.Contains(file.Extension))
                .Select(file => file.FullName)
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (projectFiles.Length > 0)
            {
                return projectFiles;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Derives a suggested project name from a project file.
    /// </summary>
    public string GetProjectName(string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectFilePath);

        string extension = Path.GetExtension(
            projectFilePath);

        if (!SupportedExtensions.Contains(extension))
        {
            throw new ArgumentException(
                "The supplied file is not a supported project file.",
                nameof(projectFilePath));
        }

        return Path.GetFileNameWithoutExtension(
            projectFilePath);
    }
}
