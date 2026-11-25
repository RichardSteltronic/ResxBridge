
namespace ResxBridge.Models;

/// <summary>
/// Represents a validation issue identified during import analysis.
/// </summary>
public sealed record ImportIssue(
    ImportIssueSeverity Severity,
    string Location,
    string Message);
