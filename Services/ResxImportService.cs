using System.IO;
using ClosedXML.Excel;
using ResxBridge.Models;
using ResxBridge.Utilities;

namespace ResxBridge.Services;

/// <summary>
/// Analyzes an XLSX translation workbook and identifies proposed
/// changes to the corresponding RESX resource files.
/// </summary>
public sealed class ResxImportService
{
    /// <summary>
    /// Validates a translation workbook and creates a read-only
    /// preview of the proposed resource changes.
    /// </summary>
    public ImportPreview Preview(
        string workbookPath,
        IReadOnlyList<ResourceFileData> resources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        ArgumentNullException.ThrowIfNull(resources);

        if (!File.Exists(workbookPath))
        {
            throw new FileNotFoundException(
                "The translation workbook was not found.",
                workbookPath);
        }

        if (!string.Equals(
                Path.GetExtension(workbookPath),
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The translation workbook must be an .xlsx file.",
                nameof(workbookPath));
        }

        if (resources.Count == 0)
        {
            throw new ArgumentException(
                "No resource files were supplied.",
                nameof(resources));
        }

        var changes = new List<ImportChange>();
        var issues = new List<ImportIssue>();

        var resourceLookup = BuildResourceLookup(resources);

        using var workbook = new XLWorkbook(workbookPath);

        if (!workbook.Worksheets.TryGetWorksheet(
                WorkbookSchema.TranslationsSheet,
                out var worksheet))
        {
            issues.Add(new ImportIssue(
                ImportIssueSeverity.Error,
                workbookPath,
                $"The '{WorkbookSchema.TranslationsSheet}' " +
                "worksheet was not found."));

            return CreatePreview(changes, issues);
        }

        WorkbookMetadata metadata;

        try
        {
            metadata = new WorkbookMetadataService().Read(workbook);
        }
        catch (InvalidDataException ex)
        {
            issues.Add(new ImportIssue(
                ImportIssueSeverity.Error,
                WorkbookSchema.MetadataSheet,
                ex.Message));

            return CreatePreview(changes, issues);
        }

        if (!ValidateStructure(worksheet, metadata, issues))
        {
            return CreatePreview(changes, issues);
        }

        for (int index = 0; index < metadata.Resources.Count; index++)
        {
            ProcessRow(
                worksheet,
                WorkbookSchema.FirstDataRow + index,
                metadata.Resources[index],
                metadata.IsDefaultOnly,
                resourceLookup,
                changes,
                issues);
        }

        return CreatePreview(changes, issues);
    }

    private static Dictionary<
        string,
        Dictionary<string, ResourceFileData>>
        BuildResourceLookup(
            IReadOnlyList<ResourceFileData> resources)
    {
        var lookup = new Dictionary<
            string,
            Dictionary<string, ResourceFileData>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var resource in resources)
        {
            string family = resource.File.ResourceFamily;
            string culture = resource.File.Culture;

            if (!lookup.TryGetValue(family, out var cultures))
            {
                cultures = new Dictionary<string, ResourceFileData>(
                    StringComparer.OrdinalIgnoreCase);

                lookup.Add(family, cultures);
            }

            if (!cultures.TryAdd(culture, resource))
            {
                throw new InvalidDataException(
                    $"Resource family '{family}' contains multiple " +
                    $"files for culture '{culture}'.");
            }
        }

        return lookup;
    }

    private static bool ValidateStructure(
        IXLWorksheet worksheet,
        WorkbookMetadata metadata,
        ICollection<ImportIssue> issues)
    {
        int expectedLastRow = WorkbookSchema.HeaderRow +
                              metadata.Resources.Count;
        int expectedLastColumn = metadata.ColumnHeaders.Count;

        if ((worksheet.LastRowUsed()?.RowNumber() ?? 0) != expectedLastRow ||
            (worksheet.LastColumnUsed()?.ColumnNumber() ?? 0) != expectedLastColumn)
        {
            AddError(issues, WorkbookSchema.TranslationsSheet,
                "The workbook row or column count differs from the original export.");
            return false;
        }

        bool valid = true;

        for (int index = 0; index < expectedLastColumn; index++)
        {
            var cell = worksheet.Cell(WorkbookSchema.HeaderRow, index + 1);

            if (!MatchesText(cell, metadata.ColumnHeaders[index]))
            {
                AddError(issues, cell.Address.ToString(),
                    "The original column header was modified.");
                valid = false;
            }
        }

        for (int index = 0; index < metadata.Resources.Count; index++)
        {
            int row = WorkbookSchema.FirstDataRow + index;
            var snapshot = metadata.Resources[index];

            if (!MatchesText(worksheet.Cell(row, WorkbookSchema.ResourceFamilyColumn),
                    snapshot.ResourceFamily) ||
                !MatchesText(worksheet.Cell(row, WorkbookSchema.KeyColumn),
                    snapshot.Key))
            {
                AddError(issues, $"Row {row}",
                    "The original resource identifiers or row order were modified.");
                valid = false;
            }
        }

        return valid;
    }

    private static bool MatchesText(IXLCell cell, string expected)
    {
        return !cell.HasFormula &&
               cell.DataType == XLDataType.Text &&
               string.Equals(cell.GetString(), expected, StringComparison.Ordinal);
    }

    private static void ProcessRow(
        IXLWorksheet worksheet,
        int row,
        WorkbookResourceSnapshot snapshot,
        bool isDefaultOnly,
        IReadOnlyDictionary<string, Dictionary<string, ResourceFileData>> resourceLookup,
        ICollection<ImportChange> changes,
        ICollection<ImportIssue> issues)
    {
        resourceLookup.TryGetValue(snapshot.ResourceFamily, out var resources);

        string? currentDefault = null;
        if (resources is not null &&
            resources.TryGetValue(CultureHelper.DefaultCulture, out var defaultResource))
        {
            defaultResource.Entries.TryGetValue(snapshot.Key, out currentDefault);
        }

        var originalDefault = snapshot.Values[0];

        bool sourceChanged = originalDefault.IsAvailable != (currentDefault is not null) ||
            (originalDefault.IsAvailable &&
             !string.Equals(originalDefault.OriginalValue, currentDefault,
                 StringComparison.Ordinal));

        for (int index = 0; index < snapshot.Values.Count; index++)
        {
            var value = snapshot.Values[index];
            var cell = worksheet.Cell(row, WorkbookSchema.DefaultColumn + index);
            string location = cell.Address.ToString();

            if (cell.HasFormula ||
                (!cell.IsEmpty() && cell.DataType != XLDataType.Text))
            {
                AddError(issues, location,
                    "Resource values must be text. Formulas are not permitted.");
                continue;
            }

            string returnedValue = cell.IsEmpty() ? string.Empty : cell.GetString();
            bool isProtected = !value.IsAvailable || (index == 0 && !isDefaultOnly);

            if (isProtected)
            {
                if (!string.Equals(returnedValue, value.OriginalValue,
                        StringComparison.Ordinal))
                {
                    AddError(issues, location,
                        "A protected resource value was modified.");
                }
                continue;
            }

            if (string.Equals(returnedValue, value.OriginalValue,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(returnedValue))
            {
                continue;
            }

            if (resources is null ||
                !resources.TryGetValue(value.Culture, out var resource) ||
                !resource.Entries.TryGetValue(snapshot.Key, out string? currentValue))
            {
                AddError(issues, location,
                    $"Resource '{snapshot.ResourceFamily}/{snapshot.Key}' " +
                    $"for culture '{value.Culture}' no longer exists.");
                continue;
            }

            if (index != 0 && sourceChanged)
            {
                AddError(issues, location,
                    "The default resource value changed after export. " +
                    "Export a new workbook and review the translation.");
                continue;
            }

            if (string.Equals(currentValue, returnedValue, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(currentValue, value.OriginalValue,
                    StringComparison.Ordinal))
            {
                AddError(issues, location,
                    "The resource was changed in both the workbook and RESX file " +
                    "after export. Review the conflicting values before importing.");
                continue;
            }

            string validationSource = resource.File.IsNeutral
                ? currentValue
                : currentDefault ?? currentValue;

            if (!PlaceholderValidator.AreCompatible(validationSource, returnedValue))
            {
                AddError(issues, location,
                    $"Formatting placeholders do not match for resource key '{snapshot.Key}'.");
                continue;
            }

            changes.Add(new ImportChange(
                resource.File.FilePath,
                snapshot.Key,
                resource.File.Language,
                currentValue,
                returnedValue));
        }
    }

    private static void AddError(
        ICollection<ImportIssue> issues,
        string location,
        string message)
    {
        issues.Add(new ImportIssue(ImportIssueSeverity.Error, location, message));
    }

    private static ImportPreview CreatePreview(
        List<ImportChange> changes,
        List<ImportIssue> issues)
    {
        return new ImportPreview(
            changes.AsReadOnly(),
            issues.AsReadOnly());
    }
}
