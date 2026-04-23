// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.Validation.Models;
using CoreValidationSeverity = Raycoon.RayMigrator.ConfigWizard.Core.Models.ValidationSeverity;
using LibValidationSeverity = Raycoon.RayMigrator.Validation.Models.ValidationSeverity;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Converts <see cref="ValidationReport"/> (central library output) into
/// <see cref="WizardValidationResult"/> (wizard UI input). Path is preserved verbatim;
/// the <c>Code</c> field is surfaced on each <see cref="ValidationEntry"/>.
/// </summary>
internal static class ValidationReportToWizardResultMapper
{
    public static WizardValidationResult Map(ValidationReport report)
    {
        var result = new WizardValidationResult();
        MergeInto(result, report);
        return result;
    }

    public static void MergeInto(WizardValidationResult target, ValidationReport report)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (report is null) return;

        foreach (var issue in report.Issues)
        {
            var entry = new ValidationEntry(
                issue.Path,
                issue.Message,
                ToCoreSeverity(issue.Severity),
                issue.Code);

            if (issue.Severity == LibValidationSeverity.Error)
                target.Errors.Add(entry);
            else
                target.Warnings.Add(entry);
        }
    }

    private static CoreValidationSeverity ToCoreSeverity(LibValidationSeverity s) =>
        s == LibValidationSeverity.Error ? CoreValidationSeverity.Error : CoreValidationSeverity.Warning;
}
