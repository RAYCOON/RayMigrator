// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Validation.Helpers;
using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// CLI tool definition-level rules:
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_3_1"/> — File-mode CliTool must contain <c>{FilePath}</c> in ArgumentTemplate (Error)</item>
/// <item><see cref="RuleIds.RULE_3_2"/> — Stdin-mode CliTool should not contain <c>{FilePath}</c> (Warning)</item>
/// <item><see cref="RuleIds.RULE_3_7"/> — SuccessExitCodes expressions must parse (Error)</item>
/// </list>
/// </summary>
internal sealed class CliToolDefinitionsRule : IValidationRule
{
    public void Execute(ValidationInput input, ValidationReport report)
    {
        foreach (var tool in input.CliTools)
        {
            if (string.IsNullOrWhiteSpace(tool.Alias)) continue;

            CheckFilePathPlacement(tool, report);
            CheckExitCodeExpressions(tool, report);
        }
    }

    private static void CheckFilePathPlacement(CliToolInput tool, ValidationReport report)
    {
        var effectiveMode = string.IsNullOrWhiteSpace(tool.InputMode) ? "File" : tool.InputMode!;
        var template = tool.ArgumentTemplate ?? "";
        var hasFilePath = template.Contains("{FilePath}", StringComparison.Ordinal);

        var path = $"CliTools > {tool.Alias}";

        if (string.Equals(effectiveMode, "File", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(template) && !hasFilePath)
        {
            report.AddError(
                RuleIds.RULE_3_1,
                path,
                ValidationMessages.Format(ValidationMessages.FileModeMissingFilePath, tool.Alias));
        }

        if (string.Equals(effectiveMode, "Stdin", StringComparison.OrdinalIgnoreCase) && hasFilePath)
        {
            report.AddWarning(
                RuleIds.RULE_3_2,
                path,
                ValidationMessages.Format(ValidationMessages.StdinModeWithFilePath, tool.Alias));
        }
    }

    private static void CheckExitCodeExpressions(CliToolInput tool, ValidationReport report)
    {
        if (tool.SuccessExitCodes is null || tool.SuccessExitCodes.Count == 0) return;

        var path = $"CliTools > {tool.Alias}";
        foreach (var expr in tool.SuccessExitCodes)
        {
            if (!ExitCodeExpressionValidator.IsValid(expr, out var err))
            {
                report.AddError(
                    RuleIds.RULE_3_7,
                    path,
                    ValidationMessages.Format(ValidationMessages.ExitCodeExpressionInvalid, tool.Alias, expr ?? "", err ?? "invalid"));
            }
        }
    }
}
