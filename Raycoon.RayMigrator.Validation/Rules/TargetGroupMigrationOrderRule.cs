using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Rules governing <c>Product.TargetGroupMigrationOrder</c> (comma-separated alias list).
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_1_10"/> — order references an alias that does not match any TG exactly (Error); an alias
/// that matches a TG only case-insensitively gets the exact alias as a hint</item>
/// <item><see cref="RuleIds.RULE_1_11"/> — an existing TG is missing from the order (Error), not reported for a TG that the
/// order names only case-insensitively, because the hint of RULE_1_10 names it</item>
/// <item><see cref="RuleIds.RULE_1_12"/> — an alias appears more than once in the order (Error)</item>
/// <item><see cref="RuleIds.RULE_1_13"/> — order names exactly the only TG of the product, which the commands ignore
/// (Warning); not reported when one of the other rules found an error in the order</item>
/// </list>
/// </summary>
/// <remarks>
/// The aliases are compared exactly (ordinal), as migrate-up and baseline compare them before they apply an order. The
/// catalog sees the product setting only; the orders of the release level migsettings and of
/// <c>--target-group-migration-order</c> are checked by the commands at run time with the same rules.
/// </remarks>
internal sealed class TargetGroupMigrationOrderRule : IValidationRule
{
    public void Execute(ValidationInput input, ValidationReport report)
    {
        foreach (var product in input.Products)
        {
            if (string.IsNullOrWhiteSpace(product.TargetGroupMigrationOrder)) continue;
            if (product.TargetGroups.Count == 0) continue;

            var path = $"Products > {product.Alias} > TargetGroupMigrationOrder";

            var orderedAliases = product.TargetGroupMigrationOrder!
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            var actualAliases = product.TargetGroups
                .Select(tg => tg.Alias)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Cast<string>()
                .ToList();

            var actualSet = new HashSet<string>(actualAliases, StringComparer.Ordinal);
            bool hasError = false;

            // RULE_1_10 — unknown alias, with a hint when only the case differs
            foreach (var alias in orderedAliases)
            {
                if (actualSet.Contains(alias)) continue;

                var caseOnlyMatch = actualAliases.FirstOrDefault(a => string.Equals(a, alias, StringComparison.OrdinalIgnoreCase));
                report.AddError(
                    RuleIds.RULE_1_10,
                    path,
                    caseOnlyMatch != null
                        ? ValidationMessages.Format(ValidationMessages.TgOrderAliasCaseMismatch, product.Alias, alias, caseOnlyMatch)
                        : ValidationMessages.Format(ValidationMessages.TgOrderInvalidAlias, product.Alias, alias));
                hasError = true;
            }

            // RULE_1_11 & RULE_1_12 — count each actual alias
            foreach (var actual in actualAliases)
            {
                int count = orderedAliases.Count(a => string.Equals(a, actual, StringComparison.Ordinal));
                if (count == 0)
                {
                    // A TG that the order names only case-insensitively is not missing: RULE_1_10 says how to write it
                    if (orderedAliases.Any(a => string.Equals(a, actual, StringComparison.OrdinalIgnoreCase))) continue;

                    report.AddError(
                        RuleIds.RULE_1_11,
                        path,
                        ValidationMessages.Format(ValidationMessages.TgOrderMissingAlias, product.Alias, actual));
                    hasError = true;
                }
                else if (count > 1)
                {
                    report.AddError(
                        RuleIds.RULE_1_12,
                        path,
                        ValidationMessages.Format(ValidationMessages.TgOrderDuplicateAlias, product.Alias, actual));
                    hasError = true;
                }
            }

            // RULE_1_13 — the commands ignore an order that names exactly the only TG; any other order is an error above
            if (product.TargetGroups.Count == 1 && !hasError)
            {
                report.AddWarning(
                    RuleIds.RULE_1_13,
                    path,
                    ValidationMessages.TgOrderIrrelevantForSingleTg);
            }
        }
    }
}
