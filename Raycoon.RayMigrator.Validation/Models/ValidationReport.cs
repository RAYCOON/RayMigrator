namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>
/// Aggregate validation result. Collects <see cref="ValidationIssue"/> instances from one or more rules.
/// </summary>
public sealed class ValidationReport
{
    private readonly List<ValidationIssue> _issues = new();

    public IReadOnlyList<ValidationIssue> Issues => _issues;

    public IEnumerable<ValidationIssue> Errors =>
        _issues.Where(i => i.Severity == ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Warnings =>
        _issues.Where(i => i.Severity == ValidationSeverity.Warning);

    public bool IsValid => !_issues.Any(i => i.Severity == ValidationSeverity.Error);

    public int TotalIssues => _issues.Count;

    public void AddError(string code, string path, string message)
        => _issues.Add(new ValidationIssue(code, ValidationSeverity.Error, path, message));

    public void AddWarning(string code, string path, string message)
        => _issues.Add(new ValidationIssue(code, ValidationSeverity.Warning, path, message));

    public void Merge(ValidationReport? other)
    {
        if (other is null) return;
        _issues.AddRange(other._issues);
    }
}
