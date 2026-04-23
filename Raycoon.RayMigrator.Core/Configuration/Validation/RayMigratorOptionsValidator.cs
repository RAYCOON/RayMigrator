// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Validation;
using Raycoon.RayMigrator.Validation.Models;
using Serilog;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// Custom <see cref="IValidateOptions{TOptions}"/> implementation for <see cref="RayMigratorOptions"/>.
/// Delegates all structural / cross-field rules to <see cref="RuleCatalog"/> via
/// <see cref="OptionsValidationInputAdapter"/>. Warnings are emitted through the static
/// <see cref="Log"/> channel; errors fail validation.
/// </summary>
public class RayMigratorOptionsValidator : IValidateOptions<RayMigratorOptions>
{
    public ValidateOptionsResult Validate(string? name, RayMigratorOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("RayMigratorOptions instance is null.");
        }

        var input = OptionsValidationInputAdapter.ToInput(options);
        var report = RuleCatalog.RunAll(input);

        foreach (var warning in report.Warnings)
        {
            Log.Warning(
                "Config warning [{RuleCode}] at {Path}: {Message}",
                warning.Code,
                warning.Path,
                warning.Message);
        }

        if (report.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errorLines = report.Errors
            .Select(e => $"[{e.Code}] {e.Path}: {e.Message}");

        var failureMessage = string.Join(Environment.NewLine, errorLines);

        Log.Error(
            "Configuration validation failed with {ErrorCount} error(s). See details below:{NewLine}{Failures}",
            report.Errors.Count(),
            Environment.NewLine,
            failureMessage);

        return ValidateOptionsResult.Fail(failureMessage);
    }

    /// <summary>
    /// Kept for diagnostic tooling: returns the full rule-catalog report (warnings + errors)
    /// without mapping to <see cref="ValidateOptionsResult"/>.
    /// </summary>
    internal static ValidationReport GetReport(RayMigratorOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        var input = OptionsValidationInputAdapter.ToInput(options);
        return RuleCatalog.RunAll(input);
    }
}
