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
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_8_1"/> — every product must have an effective MigrationErrorAction (Error)</item>
/// <item><see cref="RuleIds.RULE_8_2"/> — every TargetGroup must have an effective TargetMigrationOrder (Error)</item>
/// <item><see cref="RuleIds.RULE_8_3"/> — every TargetGroup must have an effective HashValidationScope (Error)</item>
/// </list>
/// </summary>
internal sealed class DefaultCascadeRule : IValidationRule
{
    public void Execute(ValidationInput input, ValidationReport report)
    {
        foreach (var product in input.Products)
        {
            var pPath = $"Products > {product.Alias}";

            if (string.IsNullOrWhiteSpace(product.EffectiveMigrationErrorAction))
            {
                report.AddError(
                    RuleIds.RULE_8_1,
                    $"{pPath} > MigrationErrorAction",
                    ValidationMessages.MissingEffectiveMigrationErrorAction);
            }

            foreach (var tg in product.TargetGroups)
            {
                var tgPath = $"{pPath} > TargetGroups > {tg.Alias}";

                if (string.IsNullOrWhiteSpace(tg.EffectiveTargetMigrationOrder))
                {
                    report.AddError(
                        RuleIds.RULE_8_2,
                        $"{tgPath} > TargetMigrationOrder",
                        ValidationMessages.MissingEffectiveMigrationOrder);
                }

                if (string.IsNullOrWhiteSpace(tg.EffectiveHashValidationScope))
                {
                    report.AddError(
                        RuleIds.RULE_8_3,
                        $"{tgPath} > HashValidationScope",
                        ValidationMessages.MissingEffectiveHashValidationScope);
                }
            }
        }
    }
}
