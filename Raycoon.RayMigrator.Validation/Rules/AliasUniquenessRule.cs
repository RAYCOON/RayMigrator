using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Enforces that aliases are unique within their scope (case-insensitive):
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_1_1"/> — TargetGroup aliases per Product</item>
/// <item><see cref="RuleIds.RULE_1_2"/> — Target aliases per TargetGroup</item>
/// <item><see cref="RuleIds.RULE_1_8"/> — Product aliases (global)</item>
/// <item><see cref="RuleIds.RULE_1_9"/> — CliTool aliases (global)</item>
/// </list>
/// </summary>
internal sealed class AliasUniquenessRule : IValidationRule
{
    public void Execute(ValidationInput input, ValidationReport report)
    {
        CheckDuplicateProductAliases(input, report);
        CheckDuplicateCliToolAliases(input, report);

        foreach (var product in input.Products)
        {
            CheckDuplicateTargetGroupAliases(product, report);

            foreach (var tg in product.TargetGroups)
            {
                CheckDuplicateTargetAliases(product, tg, report);
            }
        }
    }

    private static void CheckDuplicateProductAliases(ValidationInput input, ValidationReport report)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in input.Products)
        {
            if (string.IsNullOrWhiteSpace(product.Alias)) continue;
            if (!seen.Add(product.Alias))
            {
                report.AddError(
                    RuleIds.RULE_1_8,
                    $"Products > {product.Alias}",
                    ValidationMessages.Format(ValidationMessages.DuplicateProductAlias, product.Alias));
            }
        }
    }

    private static void CheckDuplicateCliToolAliases(ValidationInput input, ValidationReport report)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in input.CliTools)
        {
            if (string.IsNullOrWhiteSpace(tool.Alias)) continue;
            if (!seen.Add(tool.Alias))
            {
                report.AddError(
                    RuleIds.RULE_1_9,
                    $"CliTools > {tool.Alias}",
                    ValidationMessages.Format(ValidationMessages.DuplicateCliToolAlias, tool.Alias));
            }
        }
    }

    private static void CheckDuplicateTargetGroupAliases(ProductInput product, ValidationReport report)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tg in product.TargetGroups)
        {
            if (string.IsNullOrWhiteSpace(tg.Alias)) continue;
            if (!seen.Add(tg.Alias))
            {
                report.AddError(
                    RuleIds.RULE_1_1,
                    $"Products > {product.Alias} > TargetGroups > {tg.Alias}",
                    ValidationMessages.Format(ValidationMessages.DuplicateTargetGroupAlias, product.Alias, tg.Alias));
            }
        }
    }

    private static void CheckDuplicateTargetAliases(ProductInput product, TargetGroupInput tg, ValidationReport report)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in tg.Targets)
        {
            if (string.IsNullOrWhiteSpace(target.Alias)) continue;
            if (!seen.Add(target.Alias))
            {
                report.AddError(
                    RuleIds.RULE_1_2,
                    $"Products > {product.Alias} > TargetGroups > {tg.Alias} > Targets > {target.Alias}",
                    ValidationMessages.Format(ValidationMessages.DuplicateTargetAlias, product.Alias, tg.Alias, target.Alias));
            }
        }
    }
}
