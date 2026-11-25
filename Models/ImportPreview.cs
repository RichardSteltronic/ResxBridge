
namespace ResxBridge.Models;

/// <summary>
/// Contains proposed resource changes and their associated validation issues.
/// </summary>
public sealed record ImportPreview(
    IReadOnlyList<ImportChange> Changes,
    IReadOnlyList<ImportIssue> Issues)
{
    public bool HasErrors =>
        Issues.Any(issue => issue.Severity == ImportIssueSeverity.Error);

    public bool HasWarnings =>
        Issues.Any(issue => issue.Severity == ImportIssueSeverity.Warning);

    public bool HasChanges => Changes.Count > 0;

    public bool CanApply => HasChanges && !HasErrors;
}
