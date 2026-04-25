
using System.Text.RegularExpressions;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.Validation;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Validates a <see cref="ConfigurationModel"/> against the shared rule catalog in
/// <see cref="Raycoon.RayMigrator.Validation"/> and layers wizard-specific checks on top.
/// Public surface is stable — callers (Blazor UI, WizardStateService, bUnit tests) are unchanged.
/// </summary>
public static class ConfigurationValidator
{
    // Re-exported for wizard/UI tests and razor call sites.
    internal static readonly Regex CliToolAliasPattern = WizardOnlyChecks.CliToolAliasPattern;
    internal static readonly string[] ValidCliToolInputModes = WizardOnlyChecks.ValidCliToolInputModes;

    // ── Top-level entry points ────────────────────────────────────────────

    public static WizardValidationResult ValidateAll(ConfigurationModel model) =>
        ValidateAll(model, ValidationCapability.Structural);

    public static WizardValidationResult ValidateAll(ConfigurationModel model, ValidationCapability capabilities)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var result = new WizardValidationResult();

        // 1. Structural + cross-field rules from the shared catalog.
        var input = WizardValidationInputAdapter.ToInput(model);
        var report = RuleCatalog.RunAll(input);
        ValidationReportToWizardResultMapper.MergeInto(result, report);

        // 2. Wizard-only UI / input-sanitation checks.
        WizardOnlyChecks.RunAll(model, result, capabilities);

        // 3. Capability-gated checks.
        if (capabilities.HasFlag(ValidationCapability.Filesystem))
            FilesystemChecks.ValidateMigrationFilesRootDirectories(model, result);

        return result;
    }

    // ── Section-scoped entry points (public signatures unchanged) ─────────

    public static WizardValidationResult ValidateRepository(
        RepositoryModel repo,
        WizardValidationResult? existing = null,
        ValidationCapability capabilities = ValidationCapability.Structural)
    {
        var result = existing ?? new WizardValidationResult();

        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = repo.DatabaseType,
                ConnectionString = repo.ConnectionString,
                SchemaName = repo.SchemaName,
                TableBaseName = repo.TableBaseName,
            },
        };
        MergeCatalogIssuesWithPrefix(result, input, "Repository");

        WizardOnlyChecks.RunRepositoryChecks(repo, result, capabilities);

        return result;
    }

    public static WizardValidationResult ValidateDatabaseLogging(
        DatabaseLoggingModel dbLog,
        WizardValidationResult? existing = null,
        ValidationCapability capabilities = ValidationCapability.Structural)
    {
        var result = existing ?? new WizardValidationResult();

        // Include a placeholder Repository to keep SchemaRule's "Repository required" check silent for section-only calls.
        var input = new ValidationInput
        {
            Repository = new RepositoryInput { DatabaseType = "SqlServer", ConnectionString = "x", SchemaName = "x" },
            DatabaseLogging = new RepositoryInput
            {
                DatabaseType = dbLog.DatabaseType,
                ConnectionString = dbLog.ConnectionString,
                SchemaName = dbLog.SchemaName,
                TableBaseName = dbLog.TableBaseName,
            },
        };
        MergeCatalogIssuesWithPrefix(result, input, "DatabaseLogging");

        WizardOnlyChecks.RunDatabaseLoggingChecks(dbLog, result, capabilities);

        return result;
    }

    public static WizardValidationResult ValidateProductDefaults(
        ProductDefaultsModel defaults,
        WizardValidationResult? existing = null)
    {
        var result = existing ?? new WizardValidationResult();

        // Section-only: we do NOT invoke the catalog here because its rules at Defaults level
        // (RULE_2_13, RULE_3_3) need surrounding context (Products, CliTools) to be meaningful.
        // Those run in ValidateAll. Here we only check wizard-only shape.
        WizardOnlyChecks.RunProductDefaultsChecks(defaults, result);

        return result;
    }

    public static WizardValidationResult ValidateCliTools(
        List<CliToolModel> cliTools,
        WizardValidationResult? existing = null)
    {
        var result = existing ?? new WizardValidationResult();

        if (cliTools.Count == 0)
            return result;

        var input = new ValidationInput
        {
            Repository = new RepositoryInput { DatabaseType = "SqlServer", ConnectionString = "x", SchemaName = "x" },
            CliTools = cliTools.Select(t => new CliToolInput
            {
                Alias = t.Alias,
                ExecutablePath = t.ExecutablePath,
                ArgumentTemplate = t.ArgumentTemplate,
                InputMode = t.InputMode,
                SuccessExitCodes = t.SuccessExitCodes,
                CliToolTimeoutInSeconds = t.CliToolTimeoutInSeconds,
            }).ToList(),
        };
        MergeCatalogIssuesWithPrefix(result, input, "CliTools");

        for (int i = 0; i < cliTools.Count; i++)
        {
            var tool = cliTools[i];
            var prefix = string.IsNullOrWhiteSpace(tool.Alias) ? $"CliTools[{i}]" : $"CliTools > {tool.Alias}";
            WizardOnlyChecks.RunCliToolChecks(tool, prefix, result);
        }

        return result;
    }

    /// <summary>
    /// Runs the rule catalog on the given input and merges only those issues whose path
    /// begins with <paramref name="pathPrefix"/> into <paramref name="result"/>. Used by
    /// section-scoped public methods so callers that ask "validate just this repository"
    /// don't receive cross-section issues.
    /// </summary>
    private static void MergeCatalogIssuesWithPrefix(WizardValidationResult result, ValidationInput input, string pathPrefix)
    {
        var report = RuleCatalog.RunAll(input);
        foreach (var issue in report.Issues)
        {
            if (!issue.Path.StartsWith(pathPrefix, StringComparison.Ordinal)) continue;

            var entry = new ValidationEntry(issue.Path, issue.Message,
                issue.Severity == Validation.Models.ValidationSeverity.Error
                    ? Models.ValidationSeverity.Error
                    : Models.ValidationSeverity.Warning,
                issue.Code);

            if (issue.Severity == Validation.Models.ValidationSeverity.Error)
                result.Errors.Add(entry);
            else
                result.Warnings.Add(entry);
        }
    }

    public static WizardValidationResult ValidateProduct(
        ProductModel product,
        string prefix,
        WizardValidationResult? existing = null,
        ValidationCapability capabilities = ValidationCapability.Structural)
    {
        var result = existing ?? new WizardValidationResult();

        // Build a minimal input containing just this one product so catalog rules that operate
        // at Product / TargetGroup / Target scope (RULE_1_1/1_2/1_10…1_13/3_x/8_x) also run.
        var model = new ConfigurationModel { Products = { product } };
        var input = WizardValidationInputAdapter.ToInput(model);
        var report = RuleCatalog.RunAll(input);
        foreach (var issue in report.Issues.Where(i => i.Path.StartsWith($"Products > {product.Alias}", StringComparison.Ordinal)))
        {
            var entry = new ValidationEntry(issue.Path, issue.Message,
                issue.Severity == Validation.Models.ValidationSeverity.Error
                    ? Models.ValidationSeverity.Error
                    : Models.ValidationSeverity.Warning,
                issue.Code);

            if (issue.Severity == Validation.Models.ValidationSeverity.Error)
                result.Errors.Add(entry);
            else
                result.Warnings.Add(entry);
        }

        WizardOnlyChecks.RunProductChecks(product, prefix, result);

        if (capabilities.HasFlag(ValidationCapability.Filesystem))
            FilesystemChecks.ValidateProductDirectory(product, prefix, result);

        for (int i = 0; i < product.TargetGroups.Count; i++)
        {
            var tg = product.TargetGroups[i];
            WizardOnlyChecks.RunTargetGroupChecks(tg, $"{prefix} > TargetGroups > {tg.Alias}", result);
            for (int j = 0; j < tg.Targets.Count; j++)
            {
                var t = tg.Targets[j];
                WizardOnlyChecks.RunTargetChecks(t, $"{prefix} > TargetGroups > {tg.Alias} > Targets > {t.Alias}", result, capabilities);
            }
        }

        return result;
    }

    public static WizardValidationResult ValidateTargetGroup(
        TargetGroupModel tg,
        string prefix,
        WizardValidationResult? existing = null,
        ValidationCapability capabilities = ValidationCapability.Structural)
    {
        var result = existing ?? new WizardValidationResult();

        WizardOnlyChecks.RunTargetGroupChecks(tg, prefix, result);

        for (int i = 0; i < tg.Targets.Count; i++)
        {
            ValidateTarget(tg.Targets[i], $"{prefix} > Targets > {tg.Targets[i].Alias}", result, capabilities);
        }

        return result;
    }

    public static WizardValidationResult ValidateTarget(
        TargetModel target,
        string prefix,
        WizardValidationResult? existing = null,
        ValidationCapability capabilities = ValidationCapability.Structural)
    {
        var result = existing ?? new WizardValidationResult();
        WizardOnlyChecks.RunTargetChecks(target, prefix, result, capabilities);
        return result;
    }

    public static WizardValidationResult ValidateCliTool(
        CliToolModel tool,
        string prefix,
        WizardValidationResult? existing = null)
    {
        var result = existing ?? new WizardValidationResult();
        WizardOnlyChecks.RunCliToolChecks(tool, prefix, result);
        return result;
    }

    public static WizardValidationResult ValidateUseCliToolAliasReferences(
        ConfigurationModel model,
        WizardValidationResult? existing = null)
    {
        var result = existing ?? new WizardValidationResult();
        var input = WizardValidationInputAdapter.ToInput(model);
        var report = RuleCatalog.RunAll(input);
        // Only keep RULE_3_3 issues so callers that expect "cross-reference only" output aren't flooded.
        foreach (var issue in report.Issues.Where(i => i.Code == RuleIds.RULE_3_3))
        {
            var entry = new ValidationEntry(issue.Path, issue.Message,
                issue.Severity == Validation.Models.ValidationSeverity.Error
                    ? Models.ValidationSeverity.Error
                    : Models.ValidationSeverity.Warning,
                issue.Code);

            if (issue.Severity == Validation.Models.ValidationSeverity.Error)
                result.Errors.Add(entry);
            else
                result.Warnings.Add(entry);
        }
        return result;
    }
}
