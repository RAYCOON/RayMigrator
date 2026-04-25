
using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_2_11"/> — Rollback-class error action requires RollbackErrorAction (Error)</item>
/// <item><see cref="RuleIds.RULE_2_13"/> — MigrationFilesExtension and RollbackFilesPreExtension must differ (Error)</item>
/// </list>
/// </summary>
internal sealed class SemanticContradictionsRule : IValidationRule
{
    private static readonly HashSet<string> RollbackErrorActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rollback", "RollbackErrorOnly", "RollbackRelease"
    };

    public void Execute(ValidationInput input, ValidationReport report)
    {
        // Rule 2.13 at ProductDefaults level
        var dExt = input.Defaults.MigrationFilesExtension;
        var dPre = input.Defaults.MigrationRollbackFilesPreExtension;
        if (!string.IsNullOrWhiteSpace(dExt)
            && !string.IsNullOrWhiteSpace(dPre)
            && string.Equals(dExt, dPre, StringComparison.OrdinalIgnoreCase))
        {
            report.AddError(
                RuleIds.RULE_2_13,
                "ProductDefaults > MigrationFilesExtension",
                ValidationMessages.Format(ValidationMessages.ExtensionEqualsPreExtension, dExt));
        }

        foreach (var product in input.Products)
        {
            var path = $"Products > {product.Alias}";

            // Rule 2.11
            var errorAction = product.EffectiveMigrationErrorAction;
            if (!string.IsNullOrWhiteSpace(errorAction) && RollbackErrorActions.Contains(errorAction))
            {
                if (string.IsNullOrWhiteSpace(product.EffectiveRollbackErrorAction))
                {
                    report.AddError(
                        RuleIds.RULE_2_11,
                        $"{path} > RollbackErrorAction",
                        ValidationMessages.Format(ValidationMessages.RollbackWithoutRollbackErrorAction, errorAction));
                }
            }

            // Rule 2.13
            var ext = product.EffectiveMigrationFilesExtension;
            var pre = product.EffectiveRollbackPreExtension;
            if (!string.IsNullOrWhiteSpace(ext)
                && !string.IsNullOrWhiteSpace(pre)
                && string.Equals(ext, pre, StringComparison.OrdinalIgnoreCase))
            {
                report.AddError(
                    RuleIds.RULE_2_13,
                    $"{path} > MigrationFilesExtension",
                    ValidationMessages.Format(ValidationMessages.ExtensionEqualsPreExtension, ext));
            }
        }
    }
}
