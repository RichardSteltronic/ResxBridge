using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using ResxBridge.Models;
using ResxBridge.Utilities;

namespace ResxBridge.Services;

/// <summary>
/// Writes and reads the original resource snapshot embedded
/// in a translation exchange workbook.
/// </summary>
internal sealed class WorkbookMetadataService
{
    /// <summary>
    /// Writes the original export snapshot to a protected,
    /// very hidden metadata worksheet.
    /// </summary>
    public void Write(
        XLWorkbook workbook,
        WorkbookMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(metadata);

        ValidateMetadata(metadata);

        var worksheet = workbook.Worksheets.Add(
            WorkbookSchema.MetadataSheet);

        WriteProperty(
            worksheet,
            WorkbookSchema.MetadataIdentifierRow,
            "Identifier",
            WorkbookSchema.MetadataIdentifier);

        WriteProperty(
            worksheet,
            WorkbookSchema.MetadataSchemaVersionRow,
            "SchemaVersion",
            metadata.SchemaVersion.ToString(
                CultureInfo.InvariantCulture));

        WriteProperty(
            worksheet,
            WorkbookSchema.MetadataExportIdRow,
            "ExportId",
            metadata.ExportId.ToString("D"));

        WriteProperty(
            worksheet,
            WorkbookSchema.MetadataCreatedUtcRow,
            "CreatedUtc",
            metadata.CreatedUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));

        WriteProperty(
            worksheet,
            WorkbookSchema.MetadataExportModeRow,
            "ExportMode",
            metadata.IsDefaultOnly
                ? WorkbookSchema.DefaultOnlyExportMode
                : WorkbookSchema.TranslationExportMode);

        WriteProperty(
            worksheet,
            WorkbookSchema.MetadataRowCountRow,
            "RowCount",
            metadata.Resources.Count.ToString(
                CultureInfo.InvariantCulture));

        WriteProperty(
            worksheet,
            WorkbookSchema.MetadataColumnCountRow,
            "ColumnCount",
            metadata.ColumnHeaders.Count.ToString(
                CultureInfo.InvariantCulture));

        WriteSnapshotHeaders(
            worksheet,
            metadata.ColumnHeaders);

        WriteResourceSnapshots(
            worksheet,
            metadata.Resources);

        worksheet.Protect();

        worksheet.Visibility =
            XLWorksheetVisibility.VeryHidden;
    }

    /// <summary>
    /// Reads and validates the original export snapshot.
    /// </summary>
    public WorkbookMetadata Read(XLWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (!workbook.Worksheets.TryGetWorksheet(
                WorkbookSchema.MetadataSheet,
                out var worksheet))
        {
            throw new InvalidDataException(
                "The workbook does not contain ResX Bridge metadata. " +
                "Export a new workbook before importing translations.");
        }

        string identifier = ReadProperty(
            worksheet,
            WorkbookSchema.MetadataIdentifierRow,
            "Identifier");

        if (!string.Equals(
                identifier,
                WorkbookSchema.MetadataIdentifier,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The workbook metadata identifier is invalid.");
        }

        int schemaVersion = ReadIntegerProperty(
            worksheet,
            WorkbookSchema.MetadataSchemaVersionRow,
            "SchemaVersion");

        if (schemaVersion != WorkbookSchema.MetadataSchemaVersion)
        {
            throw new InvalidDataException(
                $"Workbook schema version {schemaVersion} " +
                "is not supported.");
        }

        string exportIdValue = ReadProperty(
            worksheet,
            WorkbookSchema.MetadataExportIdRow,
            "ExportId");

        if (!Guid.TryParseExact(
                exportIdValue,
                "D",
                out Guid exportId) ||
            exportId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The workbook export identifier is invalid.");
        }

        string createdUtcValue = ReadProperty(
            worksheet,
            WorkbookSchema.MetadataCreatedUtcRow,
            "CreatedUtc");

        if (!DateTimeOffset.TryParseExact(
                createdUtcValue,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset createdUtc))
        {
            throw new InvalidDataException(
                "The workbook export timestamp is invalid.");
        }

        string exportMode = ReadProperty(
            worksheet,
            WorkbookSchema.MetadataExportModeRow,
            "ExportMode");

        bool isDefaultOnly = exportMode switch
        {
            WorkbookSchema.DefaultOnlyExportMode => true,
            WorkbookSchema.TranslationExportMode => false,

            _ => throw new InvalidDataException(
                "The workbook export mode is invalid.")
        };

        int rowCount = ReadIntegerProperty(
            worksheet,
            WorkbookSchema.MetadataRowCountRow,
            "RowCount");

        int columnCount = ReadIntegerProperty(
            worksheet,
            WorkbookSchema.MetadataColumnCountRow,
            "ColumnCount");

        ValidateDimensions(rowCount, columnCount);

        var headers = ReadSnapshotHeaders(
            worksheet,
            columnCount);

        var cultures = ResolveCultures(headers);

        var resources = ReadResourceSnapshots(
            worksheet,
            rowCount,
            cultures);

        var metadata = new WorkbookMetadata(
            schemaVersion,
            exportId,
            createdUtc.ToUniversalTime(),
            isDefaultOnly,
            headers,
            resources);

        ValidateMetadata(metadata);

        return metadata;
    }

    private static void WriteProperty(
        IXLWorksheet worksheet,
        int row,
        string name,
        string value)
    {
        WriteText(
            worksheet.Cell(
                row,
                WorkbookSchema.MetadataPropertyColumn),
            name);

        WriteText(
            worksheet.Cell(
                row,
                WorkbookSchema.MetadataValueColumn),
            value);
    }

    private static string ReadProperty(
        IXLWorksheet worksheet,
        int row,
        string expectedName)
    {
        string name = ReadText(
            worksheet.Cell(
                row,
                WorkbookSchema.MetadataPropertyColumn));

        if (!string.Equals(
                name,
                expectedName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Expected metadata property '{expectedName}'.");
        }

        return ReadText(
            worksheet.Cell(
                row,
                WorkbookSchema.MetadataValueColumn));
    }

    private static int ReadIntegerProperty(
        IXLWorksheet worksheet,
        int row,
        string name)
    {
        string value = ReadProperty(
            worksheet,
            row,
            name);

        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int result))
        {
            throw new InvalidDataException(
                $"Metadata property '{name}' is invalid.");
        }

        return result;
    }

    private static void WriteSnapshotHeaders(
        IXLWorksheet worksheet,
        IReadOnlyList<string> headers)
    {
        WriteText(
            worksheet.Cell(
                WorkbookSchema.MetadataHeaderRow,
                WorkbookSchema.MetadataResourceFamilyColumn),
            headers[0]);

        WriteText(
            worksheet.Cell(
                WorkbookSchema.MetadataHeaderRow,
                WorkbookSchema.MetadataKeyColumn),
            headers[1]);

        for (int index = 2; index < headers.Count; index++)
        {
            int column =
                GetSnapshotValueColumn(index - 2);

            WriteText(
                worksheet.Cell(
                    WorkbookSchema.MetadataHeaderRow,
                    column),
                headers[index]);

            WriteText(
                worksheet.Cell(
                    WorkbookSchema.MetadataHeaderRow,
                    column + 1),
                "Availability");
        }
    }

    private static IReadOnlyList<string> ReadSnapshotHeaders(
        IXLWorksheet worksheet,
        int columnCount)
    {
        var headers = new List<string>
        {
            ReadText(worksheet.Cell(
                WorkbookSchema.MetadataHeaderRow,
                WorkbookSchema.MetadataResourceFamilyColumn)),

            ReadText(worksheet.Cell(
                WorkbookSchema.MetadataHeaderRow,
                WorkbookSchema.MetadataKeyColumn))
        };

        for (int index = 2; index < columnCount; index++)
        {
            int column =
                GetSnapshotValueColumn(index - 2);

            headers.Add(
                ReadText(worksheet.Cell(
                    WorkbookSchema.MetadataHeaderRow,
                    column)));

            string availabilityHeader = ReadText(
                worksheet.Cell(
                    WorkbookSchema.MetadataHeaderRow,
                    column + 1));

            if (!string.Equals(
                    availabilityHeader,
                    "Availability",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "A metadata availability header is invalid.");
            }
        }

        return headers.AsReadOnly();
    }

    private static void WriteResourceSnapshots(
        IXLWorksheet worksheet,
        IReadOnlyList<WorkbookResourceSnapshot> resources)
    {
        int row = WorkbookSchema.MetadataFirstDataRow;

        foreach (var resource in resources)
        {
            WriteText(
                worksheet.Cell(
                    row,
                    WorkbookSchema.MetadataResourceFamilyColumn),
                resource.ResourceFamily);

            WriteText(
                worksheet.Cell(
                    row,
                    WorkbookSchema.MetadataKeyColumn),
                resource.Key);

            for (int index = 0;
                 index < resource.Values.Count;
                 index++)
            {
                var value = resource.Values[index];

                int column =
                    GetSnapshotValueColumn(index);

                WriteText(
                    worksheet.Cell(row, column),
                    value.OriginalValue);

                WriteText(
                    worksheet.Cell(row, column + 1),
                    value.IsAvailable
                        ? WorkbookSchema.MetadataAvailable
                        : WorkbookSchema.MetadataUnavailable);
            }

            row++;
        }
    }

    private static IReadOnlyList<WorkbookResourceSnapshot>
        ReadResourceSnapshots(
            IXLWorksheet worksheet,
            int rowCount,
            IReadOnlyList<string> cultures)
    {
        var resources =
            new List<WorkbookResourceSnapshot>();

        int lastRow =
            WorkbookSchema.MetadataFirstDataRow +
            rowCount - 1;

        int actualLastRow =
            worksheet.LastRowUsed()?.RowNumber()
            ?? WorkbookSchema.MetadataHeaderRow;

        if (actualLastRow != lastRow)
        {
            throw new InvalidDataException(
                "The metadata resource row count is invalid.");
        }

        int expectedLastColumn =
            GetSnapshotValueColumn(cultures.Count - 1) + 1;

        int actualLastColumn =
            worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (actualLastColumn != expectedLastColumn)
        {
            throw new InvalidDataException(
                "The metadata resource column count is invalid.");
        }

        for (int row = WorkbookSchema.MetadataFirstDataRow;
             row <= lastRow;
             row++)
        {
            string family = ReadText(
                worksheet.Cell(
                    row,
                    WorkbookSchema.MetadataResourceFamilyColumn));

            string key = ReadText(
                worksheet.Cell(
                    row,
                    WorkbookSchema.MetadataKeyColumn));

            var values = new List<WorkbookValueSnapshot>();

            for (int index = 0;
                 index < cultures.Count;
                 index++)
            {
                int column =
                    GetSnapshotValueColumn(index);

                string originalValue = ReadText(
                    worksheet.Cell(row, column));

                string availability = ReadText(
                    worksheet.Cell(row, column + 1));

                bool isAvailable = availability switch
                {
                    WorkbookSchema.MetadataAvailable => true,
                    WorkbookSchema.MetadataUnavailable => false,

                    _ => throw new InvalidDataException(
                        $"Invalid resource availability " +
                        $"in metadata row {row}.")
                };

                values.Add(new WorkbookValueSnapshot(
                    cultures[index],
                    isAvailable,
                    originalValue));
            }

            resources.Add(new WorkbookResourceSnapshot(
                family,
                key,
                values.AsReadOnly()));
        }

        return resources.AsReadOnly();
    }

    private static IReadOnlyList<string> ResolveCultures(
        IReadOnlyList<string> headers)
    {
        if (headers.Count < WorkbookSchema.DefaultColumn ||
            !string.Equals(
                headers[0],
                WorkbookSchema.ResourceFamilyHeader,
                StringComparison.Ordinal) ||
            !string.Equals(
                headers[1],
                WorkbookSchema.KeyHeader,
                StringComparison.Ordinal) ||
            !string.Equals(
                headers[2],
                WorkbookSchema.DefaultHeader,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The metadata column headers are invalid.");
        }

        var cultures = new List<string>
        {
            CultureHelper.DefaultCulture
        };

        var encounteredCultures = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            CultureHelper.DefaultCulture
        };

        for (int index = 3; index < headers.Count; index++)
        {
            string header = headers[index];

            int openingIndex = header.LastIndexOf('(');

            if (openingIndex <= 0 ||
                !header.EndsWith(')'))
            {
                throw new InvalidDataException(
                    $"Invalid metadata culture header '{header}'.");
            }

            string candidate =
                header[(openingIndex + 1)..^1].Trim();

            if (!CultureHelper.TryResolveCulture(
                    candidate,
                    out var culture))
            {
                throw new InvalidDataException(
                    $"Invalid metadata culture '{candidate}'.");
            }

            string cultureName = culture!.Name;

            if (!encounteredCultures.Add(cultureName))
            {
                throw new InvalidDataException(
                    $"Duplicate metadata culture '{cultureName}'.");
            }

            cultures.Add(cultureName);
        }

        return cultures.AsReadOnly();
    }

    private static void ValidateMetadata(
        WorkbookMetadata metadata)
    {
        if (metadata.SchemaVersion !=
            WorkbookSchema.MetadataSchemaVersion)
        {
            throw new InvalidDataException(
                "The metadata schema version is not supported.");
        }

        if (metadata.ExportId == Guid.Empty)
        {
            throw new InvalidDataException(
                "The metadata export identifier is invalid.");
        }

        ValidateDimensions(
            metadata.Resources.Count,
            metadata.ColumnHeaders.Count);

        var cultures = ResolveCultures(
            metadata.ColumnHeaders);

        if (metadata.IsDefaultOnly != (cultures.Count == 1))
        {
            throw new InvalidDataException(
                "The metadata export mode does not match " +
                "the exported cultures.");
        }

        var encounteredResources =
            new HashSet<(string Family, string Key)>();

        foreach (var resource in metadata.Resources)
        {
            if (string.IsNullOrWhiteSpace(
                    resource.ResourceFamily) ||
                string.IsNullOrWhiteSpace(resource.Key))
            {
                throw new InvalidDataException(
                    "A metadata resource identifier is missing.");
            }

            if (!encounteredResources.Add(
                    (resource.ResourceFamily.ToUpperInvariant(),
                     resource.Key)))
            {
                throw new InvalidDataException(
                    $"Duplicate metadata resource " +
                    $"'{resource.ResourceFamily}/{resource.Key}'.");
            }

            if (resource.Values.Count != cultures.Count)
            {
                throw new InvalidDataException(
                    $"Invalid metadata value count for " +
                    $"resource key '{resource.Key}'.");
            }

            bool hasAvailableValue = false;

            for (int index = 0;
                 index < cultures.Count;
                 index++)
            {
                var value = resource.Values[index];

                if (!string.Equals(
                        value.Culture,
                        cultures[index],
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Invalid metadata culture for " +
                        $"resource key '{resource.Key}'.");
                }

                if (!value.IsAvailable &&
                    value.OriginalValue.Length != 0)
                {
                    throw new InvalidDataException(
                        $"An unavailable metadata resource " +
                        $"contains a value for key '{resource.Key}'.");
                }

                hasAvailableValue |= value.IsAvailable;
            }

            if (!hasAvailableValue)
            {
                throw new InvalidDataException(
                    $"Metadata resource '{resource.Key}' " +
                    "has no available values.");
            }
        }
    }

    private static void ValidateDimensions(
        int rowCount,
        int columnCount)
    {
        const int maxWorksheetRows = 1_048_576;
        const int maxWorksheetColumns = 16_384;

        int maxResourceRows =
            maxWorksheetRows -
            WorkbookSchema.MetadataFirstDataRow + 1;

        // Each culture requires two metadata columns:
        // its original value and its availability.
        int maxWorkbookColumns =
            2 + (maxWorksheetColumns -
                 WorkbookSchema.MetadataFirstValueColumn + 1) / 2;

        if (rowCount < 0 ||
            rowCount > maxResourceRows ||
            columnCount < WorkbookSchema.DefaultColumn ||
            columnCount > maxWorkbookColumns)
        {
            throw new InvalidDataException(
                "The metadata worksheet dimensions are invalid.");
        }
    }

    private static int GetSnapshotValueColumn(
        int cultureIndex)
    {
        return WorkbookSchema.MetadataFirstValueColumn +
               cultureIndex * 2;
    }

    private static void WriteText(
        IXLCell cell,
        string value)
    {
        cell.Style.NumberFormat.Format = "@";
        cell.Value = value;
    }

    private static string ReadText(IXLCell cell)
    {
        if (cell.HasFormula)
        {
            throw new InvalidDataException(
                $"Formulas are not permitted in metadata " +
                $"cell '{cell.Address}'.");
        }

        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType != XLDataType.Text)
        {
            throw new InvalidDataException(
                $"Invalid metadata value type in " +
                $"cell '{cell.Address}'.");
        }

        return cell.GetString();
    }
}
