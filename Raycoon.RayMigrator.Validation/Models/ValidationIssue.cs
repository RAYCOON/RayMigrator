
namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>
/// A single validation finding. Immutable record so assertions compare by value.
/// </summary>
/// <param name="Code">Rule identifier from <see cref="RuleIds"/> (e.g. "RULE_3_8").</param>
/// <param name="Severity">Error blocks, Warning informs.</param>
/// <param name="Path">Configuration path (e.g. "Products > MyApp > TargetGroups > Backend").</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record ValidationIssue(
    string Code,
    ValidationSeverity Severity,
    string Path,
    string Message);
