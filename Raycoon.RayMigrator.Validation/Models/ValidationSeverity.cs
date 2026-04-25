
namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>
/// Severity level of a validation issue.
/// Order matches <see cref="Raycoon.RayMigrator.ConfigWizard.Core.Models.ValidationSeverity"/> for backwards compatibility.
/// </summary>
public enum ValidationSeverity
{
    Warning,
    Error
}
