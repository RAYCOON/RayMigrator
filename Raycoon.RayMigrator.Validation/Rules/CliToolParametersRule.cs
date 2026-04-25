using Raycoon.RayMigrator.Validation.Helpers;
using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Target-level CLI tool parameter rules:
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_3_4"/> — CliToolParameters set but no UseCliToolAlias resolves (Warning)</item>
/// <item><see cref="RuleIds.RULE_3_8"/> — Required parameters for the resolved tool are missing or empty (Error)</item>
/// <item><see cref="RuleIds.RULE_3_9"/> — CliToolParameters contains a reserved key (e.g. FilePath) (Error)</item>
/// <item><see cref="RuleIds.RULE_3_10"/> — CliToolParameters contains keys unused by the tool's ArgumentTemplate (Warning)</item>
/// </list>
/// </summary>
internal sealed class CliToolParametersRule : IValidationRule
{
    public void Execute(ValidationInput input, ValidationReport report)
    {
        var toolsByAlias = input.CliTools
            .Where(t => !string.IsNullOrWhiteSpace(t.Alias))
            .GroupBy(t => t.Alias!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var product in input.Products)
        {
            foreach (var tg in product.TargetGroups)
            {
                foreach (var target in tg.Targets)
                {
                    var path = $"Products > {product.Alias} > TargetGroups > {tg.Alias} > Targets > {target.Alias}";
                    CheckTarget(target, path, toolsByAlias, report);
                }
            }
        }
    }

    private static void CheckTarget(
        TargetInput target,
        string path,
        IReadOnlyDictionary<string, CliToolInput> toolsByAlias,
        ValidationReport report)
    {
        var effective = target.EffectiveCliToolParameters;
        var aliasResolved = target.EffectiveUseCliToolAlias;

        // RULE_3_4 — params set but no alias resolves
        var hasParams = effective is { Count: > 0 };
        if (hasParams && string.IsNullOrWhiteSpace(aliasResolved))
        {
            report.AddWarning(
                RuleIds.RULE_3_4,
                $"{path} > CliToolParameters",
                ValidationMessages.CliParamsWithoutAlias);
        }

        // RULE_3_9 — reserved key collisions (checked against raw + effective)
        var source = target.CliToolParameters ?? effective;
        if (source is not null)
        {
            foreach (var key in source.Keys)
            {
                if (CliToolPlaceholderExtractor.ReservedKeys.Contains(key))
                {
                    report.AddError(
                        RuleIds.RULE_3_9,
                        $"{path} > CliToolParameters",
                        ValidationMessages.Format(ValidationMessages.CliParamsReservedKeyCollision, key));
                }
            }
        }

        // RULE_3_8 / 3_10 — require a resolved tool definition
        if (string.IsNullOrWhiteSpace(aliasResolved)) return;
        if (!toolsByAlias.TryGetValue(aliasResolved, out var tool)) return;

        var expectedKeys = CliToolPlaceholderExtractor.ExtractParameterKeys(tool.ArgumentTemplate);

        // Rebuild effective parameters with a case-insensitive comparer. The incoming dictionary
        // (bound from JSON on the engine side) uses ordinal comparison by default, which would cause
        // false-positive RULE_3_8 reports when the user writes a parameter key in a different case
        // than the placeholder (e.g. "server": "..." vs. {Server}).
        var effectiveCaseInsensitive = effective is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(effective, StringComparer.OrdinalIgnoreCase);

        // RULE_3_8 — missing required keys
        if (expectedKeys.Count > 0)
        {
            var missing = expectedKeys
                .Where(k => !effectiveCaseInsensitive.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v))
                .ToList();

            if (missing.Count > 0)
            {
                report.AddError(
                    RuleIds.RULE_3_8,
                    $"{path} > CliToolParameters",
                    ValidationMessages.Format(
                        ValidationMessages.CliParamsMissingRequiredKeys,
                        aliasResolved,
                        string.Join(", ", expectedKeys),
                        string.Join(", ", missing)));
            }
        }

        // RULE_3_10 — unused keys (keys in params that are not placeholders)
        if (effectiveCaseInsensitive.Count > 0)
        {
            var expectedSet = new HashSet<string>(expectedKeys, StringComparer.OrdinalIgnoreCase);
            var unused = effectiveCaseInsensitive.Keys
                .Where(k => !expectedSet.Contains(k) && !CliToolPlaceholderExtractor.ReservedKeys.Contains(k))
                .ToList();

            if (unused.Count > 0)
            {
                report.AddWarning(
                    RuleIds.RULE_3_10,
                    $"{path} > CliToolParameters",
                    ValidationMessages.Format(
                        ValidationMessages.CliParamsUnusedKeys,
                        string.Join(", ", unused),
                        aliasResolved));
            }
        }
    }
}
