using System.IO;
using ClosedXML.Excel;
using ResxBridge.Models;
using ResxBridge.Utilities;

namespace ResxBridge.Services;

/// <summary>
/// Exports RESX string resources to a formatted XLSX workbook
/// for external translation and review.
/// </summary>
public sealed class ResxExportService
{
    private static readonly XLColor _headerColor =
        XLColor.FromHtml("#34465D");

    private static readonly XLColor _defaultColor =
        XLColor.FromHtml("#DBEAFE");

    private static readonly XLColor _translatedColor =
        XLColor.FromHtml("#DCFCE7");

    private static readonly XLColor _unchangedColor =
        XLColor.FromHtml("#FEF3C7");

    private static readonly XLColor _missingColor =
        XLColor.FromHtml("#FECACA");

    private static readonly XLColor _unavailableColor =
        XLColor.FromHtml("#E5E7EB");

    /// <summary>
    /// Creates an XLSX workbook containing the supplied resource files.
    /// </summary>
    public void Export(
        IReadOnlyList<ResourceFileData> resources,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (resources.Count == 0)
        {
            throw new ArgumentException(
                "No resource files were supplied.",
                nameof(resources));
        }

        if (!string.Equals(
                Path.GetExtension(outputPath),
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The output file must have an .xlsx extension.",
                nameof(outputPath));
        }

        var families = resources
            .GroupBy(
                resource => resource.File.ResourceFamily,
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                family => family.Key,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        ValidateResourceFamilies(families);

        var cultures = resources
            .Where(resource => !resource.File.IsNeutral)
            .Select(resource => resource.File.Culture)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                culture => CultureHelper.GetLanguageName(culture),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                culture => culture,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var workbook = new XLWorkbook();

        var metadata = CreateTranslationsWorksheet(
            workbook,
            families,
            cultures);

        CreateSummaryWorksheet(
            workbook,
            resources);

        var metadataService = new WorkbookMetadataService();
        metadataService.Write(workbook, metadata);

        workbook.Protect(
            allowedElements: XLWorkbookProtectionElements.Windows);

        workbook.SaveAs(outputPath);
    }

    private static void ValidateResourceFamilies(
        IEnumerable<IGrouping<string, ResourceFileData>> families)
    {
        foreach (var family in families)
        {
            var duplicateCulture = family
                .GroupBy(
                    resource => resource.File.Culture,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateCulture is not null)
            {
                throw new InvalidDataException(
                    $"Resource family '{family.Key}' contains " +
                    $"multiple files for culture " +
                    $"'{duplicateCulture.Key}'.");
            }
        }
    }

    private static WorkbookMetadata CreateTranslationsWorksheet(
        XLWorkbook workbook,
        IReadOnlyList<IGrouping<string, ResourceFileData>> families,
        IReadOnlyList<string> cultures)
    {
        var worksheet = workbook.Worksheets.Add(
            WorkbookSchema.TranslationsSheet);

        WriteTranslationHeaders(worksheet, cultures);

        var snapshots = new List<WorkbookResourceSnapshot>();

        int row = WorkbookSchema.FirstDataRow;

        foreach (var family in families)
        {
            var files = family.ToDictionary(
                resource => resource.File.Culture,
                StringComparer.OrdinalIgnoreCase);

            files.TryGetValue(
                CultureHelper.DefaultCulture,
                out var defaultResource);

            var keys = family
                .SelectMany(resource => resource.Entries.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            foreach (string key in keys)
            {
                snapshots.Add(WriteResourceRow(
                    worksheet,
                    row,
                    family.Key,
                    key,
                    defaultResource,
                    files,
                    cultures));

                row++;
            }
        }

        int lastColumn =
            WorkbookSchema.DefaultColumn + cultures.Count;

        int lastRow = Math.Max(
            WorkbookSchema.HeaderRow,
            row - 1);

        FormatTranslationsWorksheet(
            worksheet,
            lastRow,
            lastColumn);

        ProtectWorksheet(worksheet);

        var headers = Enumerable.Range(1, lastColumn)
            .Select(column => worksheet.Cell(
                WorkbookSchema.HeaderRow, column).GetString())
            .ToArray();

        return new WorkbookMetadata(
            WorkbookSchema.MetadataSchemaVersion,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            cultures.Count == 0,
            Array.AsReadOnly(headers),
            snapshots.AsReadOnly());
    }

    private static void WriteTranslationHeaders(
        IXLWorksheet worksheet,
        IReadOnlyList<string> cultures)
    {
        worksheet.Cell(
            WorkbookSchema.HeaderRow,
            WorkbookSchema.ResourceFamilyColumn)
            .Value = WorkbookSchema.ResourceFamilyHeader;

        worksheet.Cell(
            WorkbookSchema.HeaderRow,
            WorkbookSchema.KeyColumn)
            .Value = WorkbookSchema.KeyHeader;

        worksheet.Cell(
            WorkbookSchema.HeaderRow,
            WorkbookSchema.DefaultColumn)
            .Value = WorkbookSchema.DefaultHeader;

        for (int index = 0; index < cultures.Count; index++)
        {
            string culture = cultures[index];

            string language =
                CultureHelper.GetLanguageName(culture);

            int column =
                WorkbookSchema.FirstTranslationColumn + index;

            worksheet.Cell(
                WorkbookSchema.HeaderRow,
                column)
                .Value = $"{language} ({culture})";
        }
    }

    private static WorkbookResourceSnapshot WriteResourceRow(
        IXLWorksheet worksheet,
        int row,
        string resourceFamily,
        string key,
        ResourceFileData? defaultResource,
        IReadOnlyDictionary<string, ResourceFileData> files,
        IReadOnlyList<string> cultures)
    {
        WriteText(
            worksheet.Cell(
                row,
                WorkbookSchema.ResourceFamilyColumn),
            resourceFamily);

        WriteText(
            worksheet.Cell(
                row,
                WorkbookSchema.KeyColumn),
            key);

        string? defaultValue = null;

        bool defaultExists = defaultResource is not null &&
            defaultResource.Entries.TryGetValue(
                key, out defaultValue);

        var values = new List<WorkbookValueSnapshot>
        {
            new WorkbookValueSnapshot(
                CultureHelper.DefaultCulture,
                defaultExists,
                defaultValue ?? string.Empty)
        };

        var defaultCell = worksheet.Cell(
            row,
            WorkbookSchema.DefaultColumn);

        WriteText(defaultCell, defaultValue ?? string.Empty);

        defaultCell.Style.Fill.BackgroundColor =
            defaultValue is null
                ? _missingColor
                : _defaultColor;

        defaultCell.Style.Protection.Locked =
            cultures.Count != 0 || !defaultExists;

        for (int index = 0; index < cultures.Count; index++)
        {
            string culture = cultures[index];

            int column =
                WorkbookSchema.FirstTranslationColumn + index;

            var cell = worksheet.Cell(row, column);

            if (!files.TryGetValue(culture, out var resource))
            {
                cell.Style.Fill.BackgroundColor =
                    _unavailableColor;

                values.Add(new WorkbookValueSnapshot(
                    culture, false, string.Empty));

                continue;
            }

            bool keyExists = resource.Entries.TryGetValue(
                key,
                out string? translation);

            WriteText(cell, translation ?? string.Empty);

            cell.Style.Protection.Locked = !keyExists;

            values.Add(new WorkbookValueSnapshot(
                culture,
                keyExists,
                translation ?? string.Empty));

            cell.Style.Fill.BackgroundColor =
                GetTranslationColor(
                    defaultValue,
                    translation);
        }

        return new WorkbookResourceSnapshot(
            resourceFamily,
            key,
            values.AsReadOnly());
    }

    private static XLColor GetTranslationColor(
        string? defaultValue,
        string? translation)
    {
        if (string.IsNullOrWhiteSpace(translation))
        {
            return _missingColor;
        }

        if (defaultValue is not null &&
            string.Equals(
                defaultValue,
                translation,
                StringComparison.Ordinal))
        {
            return _unchangedColor;
        }

        return _translatedColor;
    }

    private static void WriteText(
        IXLCell cell,
        string value)
    {
        cell.Style.NumberFormat.Format = "@";
        cell.Value = value;
    }

    private static void FormatTranslationsWorksheet(
        IXLWorksheet worksheet,
        int lastRow,
        int lastColumn)
    {
        var header = worksheet.Range(
            WorkbookSchema.HeaderRow,
            1,
            WorkbookSchema.HeaderRow,
            lastColumn);

        FormatHeader(header);

        var usedRange = worksheet.Range(
            WorkbookSchema.HeaderRow,
            1,
            lastRow,
            lastColumn);

        usedRange.SetAutoFilter();

        worksheet.SheetView.FreezeRows(1);
        worksheet.SheetView.FreezeColumns(2);

        worksheet.Column(
            WorkbookSchema.ResourceFamilyColumn)
            .Width = 26;

        worksheet.Column(
            WorkbookSchema.KeyColumn)
            .Width = 36;

        for (int column = WorkbookSchema.DefaultColumn;
             column <= lastColumn;
             column++)
        {
            worksheet.Column(column).Width = 45;
        }

        if (lastRow >= WorkbookSchema.FirstDataRow)
        {
            var dataRange = worksheet.Range(
                WorkbookSchema.FirstDataRow,
                1,
                lastRow,
                lastColumn);

            dataRange.Style.Alignment.WrapText = true;

            dataRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Top;

            worksheet.Rows(
                WorkbookSchema.FirstDataRow,
                lastRow)
                .AdjustToContents();
        }
    }

    private static void CreateSummaryWorksheet(
        XLWorkbook workbook,
        IReadOnlyList<ResourceFileData> resources)
    {
        var worksheet = workbook.Worksheets.Add(
            WorkbookSchema.SummarySheet);

        worksheet.Cell(1, 1).Value = "Resource Family";
        worksheet.Cell(1, 2).Value = "Resource File";
        worksheet.Cell(1, 3).Value = "Culture";
        worksheet.Cell(1, 4).Value = "Language";
        worksheet.Cell(1, 5).Value = "String Entries";

        FormatHeader(worksheet.Range(1, 1, 1, 5));

        int row = 2;

        foreach (var resource in resources)
        {
            WriteText(
                worksheet.Cell(row, 1),
                resource.File.ResourceFamily);

            WriteText(
                worksheet.Cell(row, 2),
                resource.File.FileName);

            WriteText(
                worksheet.Cell(row, 3),
                resource.File.Culture);

            WriteText(
                worksheet.Cell(row, 4),
                resource.File.Language);

            worksheet.Cell(row, 5).Value =
                resource.Entries.Count;

            row++;
        }

        worksheet.Range(1, 1, row - 1, 5)
            .SetAutoFilter();

        worksheet.Columns(1, 5).AdjustToContents();
        worksheet.SheetView.FreezeRows(1);

        int legendRow = row + 2;

        worksheet.Cell(legendRow, 1).Value =
            "Translation Color Legend";

        worksheet.Cell(legendRow, 1)
            .Style.Font.Bold = true;

        WriteLegendEntry(
            worksheet,
            legendRow + 1,
            _defaultColor,
            "Default resource value");

        WriteLegendEntry(
            worksheet,
            legendRow + 2,
            _translatedColor,
            "Translation differs from default");

        WriteLegendEntry(
            worksheet,
            legendRow + 3,
            _unchangedColor,
            "Translation matches default");

        WriteLegendEntry(
            worksheet,
            legendRow + 4,
            _missingColor,
            "Missing or empty resource value");

        WriteLegendEntry(
            worksheet,
            legendRow + 5,
            _unavailableColor,
            "Culture file does not exist for this family");

        ProtectWorksheet(worksheet);
    }

    private static void ProtectWorksheet(IXLWorksheet worksheet)
    {
        worksheet.Protect(
            allowedElements:
                XLSheetProtectionElements.SelectEverything |
                XLSheetProtectionElements.AutoFilter |
                XLSheetProtectionElements.FormatRows |
                XLSheetProtectionElements.FormatColumns);
    }

    private static void WriteLegendEntry(
        IXLWorksheet worksheet,
        int row,
        XLColor color,
        string description)
    {
        worksheet.Cell(row, 1)
            .Style.Fill.BackgroundColor = color;

        worksheet.Cell(row, 2).Value = description;
    }

    private static void FormatHeader(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = _headerColor;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;

        range.Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;

        range.Style.Alignment.WrapText = true;

        range.FirstRow().WorksheetRow().Height = 30;
    }
}
