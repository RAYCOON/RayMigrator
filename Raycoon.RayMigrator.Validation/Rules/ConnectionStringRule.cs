using System.Text.RegularExpressions;
using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Connection-string hygiene rules:
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_7_1"/> — Repository and Target share the same ConnectionString (Warning)</item>
/// <item><see cref="RuleIds.RULE_7_2"/> — Two Targets in the same TargetGroup share a ConnectionString (Warning)</item>
/// <item><see cref="RuleIds.RULE_7_3"/> — Hardcoded credentials in a ConnectionString (Warning)</item>
/// </list>
/// RULE_7_1 and RULE_7_2 compare the effective values (<see cref="TargetInput.EffectiveConnectionString"/> where the
/// caller resolved placeholders, the configured value otherwise). RULE_7_3 inspects the value as configured, so a
/// credential that enters through an <c>{ENV:...}</c> placeholder is never reported (#30).
/// Regex-based only — no ADO.NET parsing (keeps this rule WASM-safe).
/// </summary>
internal sealed class ConnectionStringRule : IValidationRule
{
    private static readonly Regex HardcodedCredentialPattern =
        new(@"(?:password|pwd)\s*=\s*(?!\{ENV:)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public void Execute(ValidationInput input, ValidationReport report)
    {
        CheckHardcodedCredentials(input.Repository?.ConnectionString, "Repository > ConnectionString", report);
        CheckHardcodedCredentials(input.DatabaseLogging?.ConnectionString, "DatabaseLogging > ConnectionString", report);

        var repoCs = input.Repository?.EffectiveConnectionString ?? input.Repository?.ConnectionString;

        foreach (var product in input.Products)
        {
            foreach (var tg in product.TargetGroups)
            {
                var seenConnections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var target in tg.Targets)
                {
                    var tPath = $"Products > {product.Alias} > TargetGroups > {tg.Alias} > Targets > {target.Alias} > ConnectionString";

                    CheckHardcodedCredentials(target.ConnectionString, tPath, report);

                    var targetCs = target.EffectiveConnectionString ?? target.ConnectionString;
                    if (string.IsNullOrEmpty(targetCs)) continue;

                    // RULE_7_1 — repo shares connection string with a target
                    if (!string.IsNullOrEmpty(repoCs)
                        && string.Equals(repoCs, targetCs, StringComparison.OrdinalIgnoreCase))
                    {
                        report.AddWarning(
                            RuleIds.RULE_7_1,
                            tPath,
                            ValidationMessages.Format(ValidationMessages.RepoAndTargetSameDb, target.Alias, product.Alias, tg.Alias));
                    }

                    // RULE_7_2 — duplicate target connection within a TargetGroup
                    if (seenConnections.TryGetValue(targetCs, out var firstAlias))
                    {
                        report.AddWarning(
                            RuleIds.RULE_7_2,
                            tPath,
                            ValidationMessages.Format(ValidationMessages.DuplicateTargetConnection, product.Alias, tg.Alias, firstAlias, target.Alias));
                    }
                    else
                    {
                        seenConnections[targetCs] = target.Alias ?? "";
                    }
                }
            }
        }
    }

    private static void CheckHardcodedCredentials(string? connectionString, string path, ValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        if (connectionString.StartsWith("{ENV:", StringComparison.Ordinal)) return;

        if (HardcodedCredentialPattern.IsMatch(connectionString))
        {
            // The constant is a string.Format template with escaped braces ({{ENV:VARIABLE}}); an argument-less
            // Format renders them as single braces. Passing the constant verbatim printed the doubled braces (#26).
            report.AddWarning(
                RuleIds.RULE_7_3,
                path,
                ValidationMessages.Format(ValidationMessages.HardcodedCredentials));
        }
    }
}
