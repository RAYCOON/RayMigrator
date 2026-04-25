
namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Represents a validation result with errors and warnings.
/// </summary>
public class WizardValidationResult
{
    public List<ValidationEntry> Errors { get; set; } = new();
    public List<ValidationEntry> Warnings { get; set; } = new();

    public bool IsValid => Errors.Count == 0;
    public int TotalIssues => Errors.Count + Warnings.Count;

    public void AddError(string path, string message)
    {
        Errors.Add(new ValidationEntry(path, message, ValidationSeverity.Error));
    }

    public void AddWarning(string path, string message)
    {
        Warnings.Add(new ValidationEntry(path, message, ValidationSeverity.Warning));
    }

    public void AddError(string path, string message, string? code)
    {
        Errors.Add(new ValidationEntry(path, message, ValidationSeverity.Error, code));
    }

    public void AddWarning(string path, string message, string? code)
    {
        Warnings.Add(new ValidationEntry(path, message, ValidationSeverity.Warning, code));
    }

    public void Merge(WizardValidationResult other)
    {
        Errors.AddRange(other.Errors);
        Warnings.AddRange(other.Warnings);
    }
}

public class ValidationEntry
{
    public string Path { get; set; }
    public string Message { get; set; }
    public ValidationSeverity Severity { get; set; }

    /// <summary>
    /// Optional rule code from the central validation catalog (e.g. "RULE_3_8").
    /// Null when the issue was raised by a wizard-only helper that has no catalog mapping.
    /// </summary>
    public string? Code { get; set; }

    public ValidationEntry(string path, string message, ValidationSeverity severity)
    {
        Path = path;
        Message = message;
        Severity = severity;
    }

    public ValidationEntry(string path, string message, ValidationSeverity severity, string? code)
        : this(path, message, severity)
    {
        Code = code;
    }
}

public enum ValidationSeverity
{
    Warning,
    Error
}
