namespace ResxBridge.Models;

/// <summary>
/// Represents the original state of an exported translation workbook.
/// </summary>
internal sealed record WorkbookMetadata(
    int SchemaVersion,
    Guid ExportId,
    DateTimeOffset CreatedUtc,
    bool IsDefaultOnly,
    IReadOnlyList<string> ColumnHeaders,
    IReadOnlyList<WorkbookResourceSnapshot> Resources);

/// <summary>
/// Represents the original exported state of a resource entry.
/// </summary>
internal sealed record WorkbookResourceSnapshot(
    string ResourceFamily,
    string Key,
    IReadOnlyList<WorkbookValueSnapshot> Values);

/// <summary>
/// Represents the original value and availability of a resource
/// for a specific culture.
/// </summary>
internal sealed record WorkbookValueSnapshot(
    string Culture,
    bool IsAvailable,
    string OriginalValue);
