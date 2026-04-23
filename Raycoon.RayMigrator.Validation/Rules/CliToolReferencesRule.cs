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
/// <see cref="RuleIds.RULE_3_3"/> — Every <c>UseCliToolAlias</c> reference (at ProductDefaults,
/// Product, TargetGroup, or Target level) must match a defined CLI tool alias.
/// </summary>
internal sealed class CliToolReferencesRule : IValidationRule
{
    public void Execute(ValidationInput input, ValidationReport report)
    {
        var validAliases = new HashSet<string>(
            input.CliTools
                .Select(t => t.Alias)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Cast<string>(),
            StringComparer.OrdinalIgnoreCase);

        // ProductDefaults.UseCliToolAlias
        CheckReference(input.Defaults.UseCliToolAlias, "ProductDefaults", validAliases, report);

        foreach (var product in input.Products)
        {
            CheckReference(
                product.UseCliToolAlias,
                $"Product '{product.Alias}'",
                validAliases,
                report,
                path: $"Products > {product.Alias}");

            foreach (var tg in product.TargetGroups)
            {
                CheckReference(
                    tg.UseCliToolAlias,
                    $"TargetGroup '{tg.Alias}'",
                    validAliases,
                    report,
                    path: $"Products > {product.Alias} > TargetGroups > {tg.Alias}");

                foreach (var target in tg.Targets)
                {
                    CheckReference(
                        target.UseCliToolAlias,
                        $"Target '{target.Alias}'",
                        validAliases,
                        report,
                        path: $"Products > {product.Alias} > TargetGroups > {tg.Alias} > Targets > {target.Alias}");
                }
            }
        }
    }

    private static void CheckReference(
        string? alias,
        string context,
        HashSet<string> validAliases,
        ValidationReport report,
        string? path = null)
    {
        if (string.IsNullOrWhiteSpace(alias)) return;
        if (validAliases.Contains(alias)) return;

        var available = validAliases.Count == 0 ? "" : string.Join(", ", validAliases.OrderBy(a => a, StringComparer.OrdinalIgnoreCase));
        report.AddError(
            RuleIds.RULE_3_3,
            path ?? context,
            ValidationMessages.Format(ValidationMessages.UseCliToolAliasInvalid, context, alias, available));
    }
}
