namespace ResxBridge.Utilities;

/// <summary>
/// Defines the shared structure of a translation exchange workbook.
/// </summary>
internal static class WorkbookSchema
{
    // Worksheet names

    public const string TranslationsSheet = "Translations";
    public const string SummarySheet = "Summary";
    public const string MetadataSheet = "_ResxBridgeMetadata";

    // Translation worksheet headers

    public const string ResourceFamilyHeader = "Resource Family";
    public const string KeyHeader = "Key";
    public const string DefaultHeader = "Default";

    // Translation worksheet layout

    public const int HeaderRow = 1;
    public const int FirstDataRow = 2;

    public const int ResourceFamilyColumn = 1;
    public const int KeyColumn = 2;
    public const int DefaultColumn = 3;
    public const int FirstTranslationColumn = 4;

    // Metadata schema

    public const int MetadataSchemaVersion = 1;

    public const string MetadataIdentifier = "ResxBridge";
    public const string TranslationExportMode = "Translation";
    public const string DefaultOnlyExportMode = "DefaultOnly";

    // Metadata property rows

    public const int MetadataIdentifierRow = 1;
    public const int MetadataSchemaVersionRow = 2;
    public const int MetadataExportIdRow = 3;
    public const int MetadataCreatedUtcRow = 4;
    public const int MetadataExportModeRow = 5;
    public const int MetadataRowCountRow = 6;
    public const int MetadataColumnCountRow = 7;

    public const int MetadataPropertyColumn = 1;
    public const int MetadataValueColumn = 2;

    // Metadata snapshot layout

    public const int MetadataHeaderRow = 9;
    public const int MetadataFirstDataRow = 10;

    public const int MetadataResourceFamilyColumn = 1;
    public const int MetadataKeyColumn = 2;
    public const int MetadataFirstValueColumn = 3;

    // Metadata resource availability

    public const string MetadataAvailable = "Available";
    public const string MetadataUnavailable = "Unavailable";
}
