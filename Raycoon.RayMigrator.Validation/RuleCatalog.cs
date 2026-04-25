
using Raycoon.RayMigrator.Validation.Models;
using Raycoon.RayMigrator.Validation.Rules;

namespace Raycoon.RayMigrator.Validation;

/// <summary>
/// Entry point for running the complete validation rule catalog.
/// Fixed, explicit rule list — no reflection, no DI — so WASM trimming stays trivial.
/// </summary>
public static class RuleCatalog
{
    private static readonly IReadOnlyList<IValidationRule> _rules = new IValidationRule[]
    {
        new AliasUniquenessRule(),
        new TargetGroupMigrationOrderRule(),
        new SemanticContradictionsRule(),
        new CliToolDefinitionsRule(),
        new CliToolReferencesRule(),
        new CliToolParametersRule(),
        new SchemaRule(),
        new ConnectionStringRule(),
        new DefaultCascadeRule(),
    };

    /// <summary>
    /// Runs all rules against the provided input and returns a fresh <see cref="ValidationReport"/>.
    /// </summary>
    public static ValidationReport RunAll(ValidationInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var report = new ValidationReport();
        foreach (var rule in _rules)
        {
            rule.Execute(input, report);
        }
        return report;
    }
}
