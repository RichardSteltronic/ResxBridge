
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ResxBridge.Models;
using ResxBridge.Utilities;

namespace ResxBridge.Services;

/// <summary>
/// Applies validated translation changes to RESX files,
/// preserving original files through backups and rollback protection.
/// </summary>
public sealed class ResxApplyService
{
    private readonly BackupService _backupService = new();

    /// <summary>
    /// Applies the changes contained in a validated import preview.
    /// </summary>
    public ApplyResult Apply(
	    ImportPreview preview,
	    string resourceDirectory,
	    ProjectBackupSettings project,
	    string backupRoot)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceDirectory);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);

        if (!preview.CanApply)
        {
            throw new InvalidOperationException(
                "The import preview contains no applicable changes.");
        }

        string rootDirectory =
            Path.GetFullPath(resourceDirectory);

        if (!string.Equals(
		        SettingsService.NormalizeDirectory(rootDirectory),
		        SettingsService.NormalizeDirectory(project.ResourceDirectory),
		        StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The backup configuration does not belong " +
                "to the selected resource directory.");
        }

        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Resource directory was not found: '{rootDirectory}'.");
        }

        var changes = preview.Changes.ToArray();

        var preparedFiles = PrepareFiles(
            changes,
            rootDirectory);

        var originalFiles = preparedFiles.ToDictionary(
	        file => file.FilePath,
	        file => file.OriginalBytes,
	        StringComparer.OrdinalIgnoreCase);

        string backupDirectory =
            _backupService.CreateSnapshot(
                backupRoot,
                project,
                originalFiles,
                changes.Length);

        var committedFiles = new List<PreparedFile>();

        try
        {
            StageFiles(preparedFiles);

            VerifyOriginalFiles(preparedFiles);

            foreach (var file in preparedFiles)
            {
                File.Replace(
                    file.TemporaryPath,
                    file.FilePath,
                    null);

                committedFiles.Add(file);
            }

            return new ApplyResult(
                changes.Length,
                preparedFiles.Count,
                backupDirectory);
        }
        catch (Exception applyException)
        {
            var rollbackErrors = Rollback(
                committedFiles,
                backupDirectory);

            if (rollbackErrors.Count > 0)
            {
                var exceptions = new List<Exception>
                {
                    applyException
                };

                exceptions.AddRange(rollbackErrors);

                throw new AggregateException(
                    "The update failed and one or more files " +
                    "could not be restored automatically. " +
                    $"Original backups are located at '{backupDirectory}'.",
                    exceptions);
            }

            throw new InvalidOperationException(
                "The resource update failed. " +
                "Any successfully replaced files were restored. " +
                $"Backup directory: '{backupDirectory}'.",
                applyException);
        }
        finally
        {
            CleanupTemporaryFiles(preparedFiles);
        }
    }

    private static List<PreparedFile> PrepareFiles(
        IReadOnlyList<ImportChange> changes,
        string rootDirectory)
    {
        var preparedFiles = new List<PreparedFile>();

        var fileGroups = changes.GroupBy(
            change => Path.GetFullPath(change.FilePath),
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in fileGroups)
        {
            string filePath = group.Key;

            ValidateFilePath(
                filePath,
                rootDirectory);

            byte[] originalBytes =
                File.ReadAllBytes(filePath);

            var document = LoadDocument(originalBytes);

            ApplyChangesToDocument(
                document,
                group,
                filePath);

            string temporaryPath = Path.Combine(
                rootDirectory,
                $".resxbridge-{Guid.NewGuid():N}.tmp");

            preparedFiles.Add(new PreparedFile(
                filePath,
                temporaryPath,
                originalBytes,
                document));
        }

        return preparedFiles;
    }

    private static void ValidateFilePath(
        string filePath,
        string rootDirectory)
    {
        string? parentDirectory =
            Path.GetDirectoryName(filePath);

        if (!string.Equals(
                Path.TrimEndingDirectorySeparator(
                    parentDirectory ?? string.Empty),
                Path.TrimEndingDirectorySeparator(rootDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Resource file is outside the selected directory: " +
                $"'{filePath}'.");
        }

        if (!string.Equals(
                Path.GetExtension(filePath),
                ".resx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The target is not a RESX file: '{filePath}'.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "The resource file no longer exists.",
                filePath);
        }

        var attributes = File.GetAttributes(filePath);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"Symbolic links and reparse points are not " +
                $"supported: '{filePath}'.");
        }
    }

    private static XDocument LoadDocument(byte[] contents)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var stream = new MemoryStream(contents);
        using var reader = XmlReader.Create(stream, settings);

        return XDocument.Load(
            reader,
            LoadOptions.PreserveWhitespace);
    }

    private static void ApplyChangesToDocument(
        XDocument document,
        IEnumerable<ImportChange> changes,
        string filePath)
    {
        if (document.Root is null ||
            document.Root.Name != "root")
        {
            throw new InvalidDataException(
                $"Invalid RESX document: '{filePath}'.");
        }

        var entries = new Dictionary<string, XElement>(
            StringComparer.Ordinal);

        foreach (var element in document.Root.Elements("data"))
        {
            string? key = element.Attribute("name")?.Value;

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidDataException(
                    $"A resource entry has no valid key in '{filePath}'.");
            }

            if (!entries.TryAdd(key, element))
            {
                throw new InvalidDataException(
                    $"Duplicate resource key '{key}' in '{filePath}'.");
            }
        }

        var changedKeys = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var change in changes)
        {
            if (!changedKeys.Add(change.Key))
            {
                throw new InvalidOperationException(
                    $"Multiple changes target resource key " +
                    $"'{change.Key}' in '{filePath}'.");
            }

            if (!entries.TryGetValue(
                    change.Key,
                    out var element))
            {
                throw new InvalidOperationException(
                    $"Resource key '{change.Key}' no longer " +
                    $"exists in '{filePath}'.");
            }

            if (!ResxResourceHelper.IsStringResource(element))
            {
                throw new InvalidOperationException(
                    $"Resource '{change.Key}' is not a string resource.");
            }

            var valueElement = element.Element("value");

            if (valueElement is null)
            {
                throw new InvalidDataException(
                    $"Resource '{change.Key}' has no value element.");
            }

            if (!string.Equals(
                    valueElement.Value,
                    change.Before,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Resource '{change.Key}' in '{filePath}' " +
                    "has changed since the import preview. " +
                    "Generate a new preview before applying changes.");
            }

            if (string.IsNullOrWhiteSpace(change.After))
            {
                throw new InvalidOperationException(
                    $"An empty update was supplied for '{change.Key}'.");
            }

            XmlConvert.VerifyXmlChars(change.After);

            valueElement.Value = change.After;
        }
    }

    private static void StageFiles(
        IEnumerable<PreparedFile> files)
    {
        foreach (var file in files)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = ResolveEncoding(file),
                Indent = false,
                NewLineHandling = NewLineHandling.Entitize,
                OmitXmlDeclaration =
                    file.Document.Declaration is null
            };

            using var stream = new FileStream(
                file.TemporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);

            using (var writer = XmlWriter.Create(
                       stream,
                       settings))
            {
                file.Document.Save(writer);
            }

            stream.Flush(flushToDisk: true);
        }
    }

    private static Encoding ResolveEncoding(PreparedFile file)
    {
        byte[] contents = file.OriginalBytes;

        if (contents.AsSpan().StartsWith(
                new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
        {
            return new UTF32Encoding(false, true, true);
        }

        if (contents.AsSpan().StartsWith(
                new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
        {
            return new UTF32Encoding(true, true, true);
        }

        if (contents.AsSpan().StartsWith(
                new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return new UTF8Encoding(true, true);
        }

        if (contents.AsSpan().StartsWith(
                new byte[] { 0xFF, 0xFE }))
        {
            return new UnicodeEncoding(false, true, true);
        }

        if (contents.AsSpan().StartsWith(
                new byte[] { 0xFE, 0xFF }))
        {
            return new UnicodeEncoding(true, true, true);
        }

        string? encodingName =
            file.Document.Declaration?.Encoding;

        if (string.IsNullOrWhiteSpace(encodingName) ||
            string.Equals(
                encodingName,
                "utf-8",
                StringComparison.OrdinalIgnoreCase))
        {
            return new UTF8Encoding(false, true);
        }

        return Encoding.GetEncoding(
            encodingName,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private static void VerifyOriginalFiles(
        IEnumerable<PreparedFile> files)
    {
        foreach (var file in files)
        {
            byte[] currentBytes =
                File.ReadAllBytes(file.FilePath);

            if (!currentBytes.AsSpan().SequenceEqual(
                    file.OriginalBytes))
            {
                throw new InvalidOperationException(
                    $"Resource file '{file.FilePath}' changed " +
                    "during update preparation.");
            }
        }
    }

    private List<Exception> Rollback(
        IReadOnlyList<PreparedFile> committedFiles,
        string backupDirectory)
    {
        var errors = new List<Exception>();

        for (int index = committedFiles.Count - 1;
             index >= 0;
             index--)
        {
            var file = committedFiles[index];

            try
            {
                string backupPath =
	                _backupService.GetBackupFilePath(
		                backupDirectory,
		                file.FilePath);

                File.Copy(
                    backupPath,
                    file.FilePath,
                    overwrite: true);
            }
            catch (Exception exception)
            {
                errors.Add(new IOException(
                    $"Could not restore '{file.FilePath}'.",
                    exception));
            }
        }

        return errors;
    }

    private static void CleanupTemporaryFiles(
        IEnumerable<PreparedFile> files)
    {
        foreach (var file in files)
        {
            try
            {
                File.Delete(file.TemporaryPath);
            }
            catch (IOException)
            {
                // Cleanup must not obscure the update result.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup must not obscure the update result.
            }
        }
    }

    private sealed record PreparedFile(
        string FilePath,
        string TemporaryPath,
        byte[] OriginalBytes,
        XDocument Document);
}
