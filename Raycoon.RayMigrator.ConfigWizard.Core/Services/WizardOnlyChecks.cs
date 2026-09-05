using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Validation checks that are meaningful only for the wizard UI (input sanitation, UX warnings,
/// UI-only data-shape rules). None of these are part of the shared <c>Raycoon.RayMigrator.Validation</c>
/// rule catalog because they either depend on the wizard's own invariants or touch WASM-unsafe APIs
/// guarded by <see cref="ValidationCapability"/>.
/// </summary>
internal static class WizardOnlyChecks
{
    private static readonly string[] ValidDatabaseTypes = { "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite" };
    private static readonly string[] ValidMigrationErrorActions = { "Terminate", "Rollback", "RollbackErrorOnly", "RollbackRelease", "Ignore" };
    private static readonly string[] ValidRollbackErrorActions = { "Terminate", "Ignore" };
    private static readonly string[] ValidTargetMigrationOrders = { "Simultaneously", "Successively" };
    private static readonly string[] ValidHashValidationScopes = { "File", "SqlBlocks", "Disabled" };
    private static readonly string[] ValidLogLevels = { "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None" };
    private static readonly string[] ValidSerilogLevels = { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };

    internal static readonly string[] ValidCliToolInputModes = { "File", "Stdin" };
    private static readonly Regex AliasPattern = new(@"^(?=.{1,50}$)[\p{L}\p{N}_]+$", RegexOptions.Compiled);
    internal static readonly Regex CliToolAliasPattern = new(@"^(?=.{1,50}$)[\p{L}\p{N}_\-]+$", RegexOptions.Compiled);
    private static readonly Regex FileExtensionPattern = new(@"^[a-zA-Z_]+$", RegexOptions.Compiled);
    private static readonly Regex ConnectionStringKeyValuePattern = new(@"\w+\s*=", RegexOptions.Compiled);

    // ── Repository ────────────────────────────────────────────────────────

    public static void RunRepositoryChecks(RepositoryModel repo, WizardValidationResult result,
        ValidationCapability capabilities)
    {
        if (string.IsNullOrWhiteSpace(repo.DatabaseType))
            result.AddError("Repository > DatabaseType", "DatabaseType is required.");
        else if (!ValidDatabaseTypes.Contains(repo.DatabaseType))
            result.AddError("Repository > DatabaseType",
                $"Invalid DatabaseType '{repo.DatabaseType}'. Valid: {string.Join(", ", ValidDatabaseTypes)}");

        ValidateConnectionString(repo.ConnectionString, "Repository > ConnectionString", result, isRequired: false, capabilities);

        ValidateNonNegativeInt(repo.DbCommandTimeoutInSeconds, "Repository > DbCommandTimeoutInSeconds", result);
        ValidateNonNegativeInt(repo.DbCommandMaxRetries, "Repository > DbCommandMaxRetries", result);
        ValidateNonNegativeInt(repo.DbCommandWaitTimeInMsBeforeRetry, "Repository > DbCommandWaitTimeInMsBeforeRetry", result);
    }

    // ── DatabaseLogging ───────────────────────────────────────────────────

    public static void RunDatabaseLoggingChecks(DatabaseLoggingModel dbLog, WizardValidationResult result,
        ValidationCapability capabilities)
    {
        if (!string.IsNullOrWhiteSpace(dbLog.DatabaseType) && !ValidDatabaseTypes.Contains(dbLog.DatabaseType))
            result.AddError("DatabaseLogging > DatabaseType",
                $"Invalid DatabaseType '{dbLog.DatabaseType}'.");

        if (dbLog.DatabaseType == "Sqlite" && !string.IsNullOrWhiteSpace(dbLog.SchemaName))
            result.AddWarning("DatabaseLogging > SchemaName",
                "Sqlite does not support schemas. SchemaName will be ignored.");

        if (!string.IsNullOrWhiteSpace(dbLog.MinimumLevel) && !ValidLogLevels.Contains(dbLog.MinimumLevel))
            result.AddError("DatabaseLogging > MinimumLevel",
                $"Invalid MinimumLevel '{dbLog.MinimumLevel}'. Valid: {string.Join(", ", ValidLogLevels)}");

        ValidateConnectionString(dbLog.ConnectionString, "DatabaseLogging > ConnectionString", result, isRequired: false, capabilities);
        ValidateNonNegativeInt(dbLog.DbCommandTimeoutInSeconds, "DatabaseLogging > DbCommandTimeoutInSeconds", result);
    }

    // ── ProductDefaults ───────────────────────────────────────────────────

    public static void RunProductDefaultsChecks(ProductDefaultsModel defaults, WizardValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(defaults.MigrationErrorAction)
            && !ValidMigrationErrorActions.Contains(defaults.MigrationErrorAction, StringComparer.OrdinalIgnoreCase))
            result.AddError("ProductDefaults > MigrationErrorAction",
                $"Invalid value '{defaults.MigrationErrorAction}'.");

        if (!string.IsNullOrWhiteSpace(defaults.RollbackErrorAction)
            && !ValidRollbackErrorActions.Contains(defaults.RollbackErrorAction, StringComparer.OrdinalIgnoreCase))
            result.AddError("ProductDefaults > RollbackErrorAction",
                $"Invalid value '{defaults.RollbackErrorAction}'.");

        ValidateFileExtension(defaults.MigrationFilesExtension, "ProductDefaults > MigrationFilesExtension", result);
        ValidateFileExtension(defaults.MigrationRollbackFilesPreExtension, "ProductDefaults > MigrationRollbackFilesPreExtension", result);
        ValidateEncoding(defaults.MigrationFilesEncoding, "ProductDefaults > MigrationFilesEncoding", result);

        var tgd = defaults.TargetGroupDefaults;
        if (!string.IsNullOrWhiteSpace(tgd.TargetMigrationOrder) && !ValidTargetMigrationOrders.Contains(tgd.TargetMigrationOrder, StringComparer.OrdinalIgnoreCase))
            result.AddError("ProductDefaults > TargetGroupDefaults > TargetMigrationOrder",
                $"Invalid value '{tgd.TargetMigrationOrder}'.");

        if (!string.IsNullOrWhiteSpace(tgd.HashValidationScope) && !ValidHashValidationScopes.Contains(tgd.HashValidationScope, StringComparer.OrdinalIgnoreCase))
            result.AddError("ProductDefaults > TargetGroupDefaults > HashValidationScope",
                $"Invalid value '{tgd.HashValidationScope}'.");

        ValidateNonNegativeInt(tgd.TargetDefaults.DbCommandTimeoutInSeconds, "ProductDefaults > TargetDefaults > DbCommandTimeoutInSeconds", result);
        ValidateNonNegativeInt(tgd.TargetDefaults.DbCommandMaxRetries, "ProductDefaults > TargetDefaults > DbCommandMaxRetries", result);
        ValidateNonNegativeInt(tgd.TargetDefaults.DbCommandWaitTimeInMsBeforeRetry, "ProductDefaults > TargetDefaults > DbCommandWaitTimeInMsBeforeRetry", result);
    }

    // ── CliTool (single definition) ───────────────────────────────────────

    public static void RunCliToolChecks(CliToolModel tool, string prefix, WizardValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(tool.Alias))
            result.AddError($"{prefix} > Alias", "Alias is required.");
        else if (!CliToolAliasPattern.IsMatch(tool.Alias))
            result.AddError($"{prefix} > Alias", "Only letters, numbers, underscores and hyphens (max 50 chars) allowed.");

        if (string.IsNullOrWhiteSpace(tool.ExecutablePath))
            result.AddError($"{prefix} > ExecutablePath", "ExecutablePath is required.");

        if (string.IsNullOrWhiteSpace(tool.ArgumentTemplate))
            result.AddError($"{prefix} > ArgumentTemplate", "ArgumentTemplate is required.");

        if (!string.IsNullOrWhiteSpace(tool.InputMode) && !ValidCliToolInputModes.Contains(tool.InputMode, StringComparer.OrdinalIgnoreCase))
            result.AddError($"{prefix} > InputMode",
                $"Invalid InputMode '{tool.InputMode}'. Valid: {string.Join(", ", ValidCliToolInputModes)}");

        if (tool.CliToolTimeoutInSeconds <= 0)
            result.AddError($"{prefix} > CliToolTimeoutInSeconds", "Timeout must be greater than 0.");
    }

    // ── Product / TargetGroup / Target (aliases + required fields) ────────

    public static void RunProductChecks(ProductModel product, string prefix, WizardValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(product.Alias))
            result.AddError($"{prefix} > Alias", "Alias is required.");
        else if (!AliasPattern.IsMatch(product.Alias))
            result.AddError($"{prefix} > Alias", "Only letters, numbers and underscores (max 50 chars) allowed.");

        if (string.IsNullOrWhiteSpace(product.MigrationFilesRootDirectory))
            result.AddError($"{prefix} > MigrationFilesRootDirectory", "MigrationFilesRootDirectory is required.");

        if (product.TargetGroups.Count == 0)
            result.AddError($"{prefix} > TargetGroups", "At least one TargetGroup is required.");
    }

    public static void RunTargetGroupChecks(TargetGroupModel tg, string prefix, WizardValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(tg.Alias))
            result.AddError($"{prefix} > Alias", "Alias is required.");
        else if (!AliasPattern.IsMatch(tg.Alias))
            result.AddError($"{prefix} > Alias", "Only letters, numbers and underscores (max 50 chars) allowed.");

        if (string.IsNullOrWhiteSpace(tg.DatabaseType))
            result.AddError($"{prefix} > DatabaseType", "DatabaseType is required.");
        else if (!ValidDatabaseTypes.Contains(tg.DatabaseType))
            result.AddError($"{prefix} > DatabaseType", $"Invalid DatabaseType '{tg.DatabaseType}'.");

        if (tg.Targets.Count == 0)
            result.AddError($"{prefix} > Targets", "At least one Target is required.");

        if (tg.Targets.Count == 1 && tg.TargetMigrationOrder.IsOverridden)
            result.AddWarning($"{prefix} > TargetMigrationOrder",
                "TargetMigrationOrder is irrelevant when the TargetGroup has only one Target.");
    }

    public static void RunTargetChecks(TargetModel target, string prefix, WizardValidationResult result,
        ValidationCapability capabilities)
    {
        if (string.IsNullOrWhiteSpace(target.Alias))
            result.AddError($"{prefix} > Alias", "Alias is required.");
        else if (!AliasPattern.IsMatch(target.Alias))
            result.AddError($"{prefix} > Alias", "Only letters, numbers and underscores (max 50 chars) allowed.");

        ValidateConnectionString(target.ConnectionString, $"{prefix} > ConnectionString", result, isRequired: true, capabilities);
    }

    // ── Serilog ───────────────────────────────────────────────────────────

    public static void RunSerilogChecks(SerilogModel serilog, WizardValidationResult result)
    {
        if (!ValidSerilogLevels.Contains(serilog.MinimumLevelDefault))
            result.AddWarning("Serilog > MinimumLevel", $"Unusual minimum level '{serilog.MinimumLevelDefault}'.");

        if (serilog.WriteTo.Count == 0)
            result.AddWarning("Serilog > WriteTo", "No sinks configured. Logs will not be written.");
    }

    // ── Global helpers ────────────────────────────────────────────────────

    public static void RunAll(ConfigurationModel model, WizardValidationResult result, ValidationCapability capabilities)
    {
        RunRepositoryChecks(model.Repository, result, capabilities);

        if (model.DatabaseLogging is { } dbLog)
            RunDatabaseLoggingChecks(dbLog, result, capabilities);

        RunProductDefaultsChecks(model.ProductDefaults, result);
        RunSerilogChecks(model.Serilog, result);

        // Per-CliTool shape checks
        for (int i = 0; i < model.CliTools.Count; i++)
        {
            var tool = model.CliTools[i];
            var prefix = string.IsNullOrWhiteSpace(tool.Alias) ? $"CliTools[{i}]" : $"CliTools > {tool.Alias}";
            RunCliToolChecks(tool, prefix, result);
        }

        // "No products in this file" behaviour depends on FileRole.
        if (model.Products.Count == 0)
        {
            if (model.FileRole is ConfigFileRole.Base or ConfigFileRole.Environment)
                result.AddWarning("Products", "No Products in this file. Products can be defined in a product-specific file.");
            else
                result.AddError("Products", "At least one Product is required.");
        }

        // UseCliToolAlias reference when no CliTools are defined
        if (model.CliTools.Count == 0)
        {
            bool hasAnyReference = !string.IsNullOrWhiteSpace(model.ProductDefaults.UseCliToolAlias);
            foreach (var product in model.Products)
            {
                if (hasAnyReference) break;
                if (product.UseCliToolAlias.IsOverridden) { hasAnyReference = true; break; }
                foreach (var tg in product.TargetGroups)
                {
                    if (hasAnyReference) break;
                    if (tg.UseCliToolAlias.IsOverridden) { hasAnyReference = true; break; }
                    foreach (var t in tg.Targets)
                    {
                        if (t.UseCliToolAlias.IsOverridden) { hasAnyReference = true; break; }
                    }
                }
            }
            if (hasAnyReference)
                result.AddError("CliTools", "UseCliToolAlias references exist but no CLI tools are defined.");
        }

        // Per-product / per-TG / per-target structural checks
        foreach (var product in model.Products)
        {
            var pPrefix = $"Products > {product.Alias}";
            RunProductChecks(product, pPrefix, result);

            foreach (var tg in product.TargetGroups)
            {
                var tgPrefix = $"{pPrefix} > TargetGroups > {tg.Alias}";
                RunTargetGroupChecks(tg, tgPrefix, result);

                foreach (var target in tg.Targets)
                {
                    var tPrefix = $"{tgPrefix} > Targets > {target.Alias}";
                    RunTargetChecks(target, tPrefix, result, capabilities);
                }
            }
        }
    }

    // ── Small helpers ─────────────────────────────────────────────────────

    private static void ValidateConnectionString(string? connectionString, string path, WizardValidationResult result,
        bool isRequired, ValidationCapability capabilities)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (isRequired)
                result.AddError(path, "ConnectionString is required.");
            return;
        }

        if (connectionString.Contains("{ENV:"))
            return;

        if (capabilities.HasFlag(ValidationCapability.AdoNetParsing))
        {
            try
            {
                _ = new DbConnectionStringBuilder { ConnectionString = connectionString };
            }
            catch (ArgumentException)
            {
                result.AddError(path, "Invalid connection string syntax.");
            }
        }
        else
        {
            if (!ConnectionStringKeyValuePattern.IsMatch(connectionString))
                result.AddError(path, "Invalid connection string syntax. Expected at least one key=value pair.");
        }
    }

    private static void ValidateNonNegativeInt(int value, string path, WizardValidationResult result)
    {
        if (value < 0)
            result.AddError(path, $"Value must be >= 0, but was {value}.");
    }

    private static void ValidateFileExtension(string value, string path, WizardValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(value) && !FileExtensionPattern.IsMatch(value))
            result.AddError(path, "Only letters and underscores are allowed.");
    }

    private static void ValidateEncoding(string value, string path, WizardValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try
        {
            Encoding.GetEncoding(value);
        }
        catch
        {
            result.AddError(path, $"Invalid encoding '{value}'. Use a valid encoding like UTF-8.");
        }
    }
}
