
namespace ResxBridge.Models;

/// <summary>
/// Describes the outcome of a successful resource update operation.
/// </summary>
public sealed record ApplyResult(
    int ChangedValues,
    int ChangedFiles,
    string BackupFolder);
