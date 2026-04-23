// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Rules governing <c>Product.TargetGroupMigrationOrder</c> (comma-separated alias list).
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_1_10"/> — order references an alias that does not match any TG (Error)</item>
/// <item><see cref="RuleIds.RULE_1_11"/> — an existing TG is missing from the order (Error)</item>
/// <item><see cref="RuleIds.RULE_1_12"/> — an alias appears more than once in the order (Error)</item>
/// <item><see cref="RuleIds.RULE_1_13"/> — order specified but product has only one TG (Warning)</item>
/// </list>
/// </summary>
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

            var actualSet = new HashSet<string>(actualAliases, StringComparer.OrdinalIgnoreCase);

            // RULE_1_10 — unknown alias
            foreach (var alias in orderedAliases)
            {
                if (!actualSet.Contains(alias))
                {
                    report.AddError(
                        RuleIds.RULE_1_10,
                        path,
                        ValidationMessages.Format(ValidationMessages.TgOrderInvalidAlias, product.Alias, alias));
                }
            }

            // RULE_1_11 & RULE_1_12 — count each actual alias
            foreach (var actual in actualAliases)
            {
                int count = orderedAliases.Count(a => string.Equals(a, actual, StringComparison.OrdinalIgnoreCase));
                if (count == 0)
                {
                    report.AddError(
                        RuleIds.RULE_1_11,
                        path,
                        ValidationMessages.Format(ValidationMessages.TgOrderMissingAlias, product.Alias, actual));
                }
                else if (count > 1)
                {
                    report.AddError(
                        RuleIds.RULE_1_12,
                        path,
                        ValidationMessages.Format(ValidationMessages.TgOrderDuplicateAlias, product.Alias, actual));
                }
            }

            // RULE_1_13 — irrelevant for single TG
            if (product.TargetGroups.Count == 1)
            {
                report.AddWarning(
                    RuleIds.RULE_1_13,
                    path,
                    ValidationMessages.TgOrderIrrelevantForSingleTg);
            }
        }
    }
}
