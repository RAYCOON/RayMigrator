// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Analyzes the configuration and promotes identical overrides to the defaults level.
/// </summary>
public static class DefaultsPromoter
{
    /// <summary>
    /// Analyzes all products and promotes identical product-level overrides to ProductDefaults.
    /// Returns a list of promotions that were applied.
    /// </summary>
    public static List<PromotionResult> Promote(ConfigurationModel model)
    {
        var results = new List<PromotionResult>();

        if (model.Products.Count == 0)
            return results;

        // Product-level string overrides
        TryPromoteStringOverride(model, "MigrationErrorAction",
            p => p.MigrationErrorAction,
            (defaults, val) => defaults.MigrationErrorAction = val,
            results);

        TryPromoteStringOverride(model, "RollbackErrorAction",
            p => p.RollbackErrorAction,
            (defaults, val) => defaults.RollbackErrorAction = val,
            results);

        TryPromoteStringOverride(model, "MigrationFilesExtension",
            p => p.MigrationFilesExtension,
            (defaults, val) => defaults.MigrationFilesExtension = val,
            results);

        TryPromoteStringOverride(model, "MigrationRollbackFilesPreExtension",
            p => p.MigrationRollbackFilesPreExtension,
            (defaults, val) => defaults.MigrationRollbackFilesPreExtension = val,
            results);

        TryPromoteStringOverride(model, "MigrationFilesEncoding",
            p => p.MigrationFilesEncoding,
            (defaults, val) => defaults.MigrationFilesEncoding = val,
            results);

        TryPromoteStringOverride(model, "UseCliToolAlias",
            p => p.UseCliToolAlias,
            (defaults, val) => defaults.UseCliToolAlias = val,
            results);

        // Product-level bool overrides
        TryPromoteBoolOverride(model, "RequireRollbackFile",
            p => p.RequireRollbackFile,
            (defaults, val) => defaults.RequireRollbackFile = val,
            results);

        TryPromoteBoolOverride(model, "StopRollbackOnMissingRollbackFile",
            p => p.StopRollbackOnMissingRollbackFile,
            (defaults, val) => defaults.StopRollbackOnMissingRollbackFile = val,
            results);

        // TargetGroup-level string overrides
        TryPromoteTargetGroupStringOverride(model, "TargetMigrationOrder",
            tg => tg.TargetMigrationOrder,
            (tgDefaults, val) => tgDefaults.TargetMigrationOrder = val,
            results);

        TryPromoteTargetGroupStringOverride(model, "HashValidationScope",
            tg => tg.HashValidationScope,
            (tgDefaults, val) => tgDefaults.HashValidationScope = val,
            results);

        TryPromoteTargetGroupStringOverride(model, "UseCliToolAlias (TargetGroup)",
            tg => tg.UseCliToolAlias,
            (_, _) => { }, // TargetGroupDefaults doesn't have UseCliToolAlias directly
            results,
            clearOnly: true);

        // TargetGroup-level bool overrides
        TryPromoteTargetGroupBoolOverride(model, "StopRollbackOnMissingRollbackFile (TargetGroup)",
            tg => tg.StopRollbackOnMissingRollbackFile,
            (tgDefaults, val) => tgDefaults.StopRollbackOnMissingRollbackFile = val,
            results);

        // Target-level int overrides
        TryPromoteTargetIntOverride(model, "DbCommandTimeoutInSeconds",
            t => t.DbCommandTimeoutInSeconds,
            (tDefaults, val) => tDefaults.DbCommandTimeoutInSeconds = val,
            results);

        TryPromoteTargetIntOverride(model, "DbCommandMaxRetries",
            t => t.DbCommandMaxRetries,
            (tDefaults, val) => tDefaults.DbCommandMaxRetries = val,
            results);

        TryPromoteTargetIntOverride(model, "DbCommandWaitTimeInMsBeforeRetry",
            t => t.DbCommandWaitTimeInMsBeforeRetry,
            (tDefaults, val) => tDefaults.DbCommandWaitTimeInMsBeforeRetry = val,
            results);

        return results;
    }

    /// <summary>
    /// Analyzes all ProductEnvironmentModels in the WizardState and promotes identical values upward:
    /// - Values common to ALL combinations → BaseModel
    /// - Values common to all combinations of one environment → EnvironmentModels[env]
    /// Returns a list of promotions that were applied.
    /// </summary>
    public static List<PromotionResult> PromoteAcrossModels(WizardState state)
    {
        var results = new List<PromotionResult>();

        if (state.ProductEnvironmentModels.Count < 2)
            return results;

        var peModels = state.ProductEnvironmentModels;

        // Promote Repository.DatabaseType if identical across all combinations
        TryPromoteAcrossAll(peModels, state.BaseModel, "Repository.DatabaseType",
            m => m.Repository.DatabaseType,
            (target, val) => target.Repository.DatabaseType = val,
            results);

        // Promote Repository.SchemaName if identical across all combinations
        TryPromoteAcrossAll(peModels, state.BaseModel, "Repository.SchemaName",
            m => m.Repository.SchemaName,
            (target, val) => target.Repository.SchemaName = val,
            results);

        // Promote Repository.ConnectionString if identical across all combinations
        TryPromoteAcrossAll(peModels, state.BaseModel, "Repository.ConnectionString",
            m => m.Repository.ConnectionString,
            (target, val) => target.Repository.ConnectionString = val,
            results);

        // Promote ProductDefaults fields
        TryPromoteAcrossAll(peModels, state.BaseModel, "ProductDefaults.MigrationErrorAction",
            m => m.ProductDefaults.MigrationErrorAction,
            (target, val) => target.ProductDefaults.MigrationErrorAction = val,
            results);

        TryPromoteAcrossAll(peModels, state.BaseModel, "ProductDefaults.RollbackErrorAction",
            m => m.ProductDefaults.RollbackErrorAction,
            (target, val) => target.ProductDefaults.RollbackErrorAction = val,
            results);

        TryPromoteAcrossAll(peModels, state.BaseModel, "ProductDefaults.MigrationFilesExtension",
            m => m.ProductDefaults.MigrationFilesExtension,
            (target, val) => target.ProductDefaults.MigrationFilesExtension = val,
            results);

        TryPromoteAcrossAll(peModels, state.BaseModel, "ProductDefaults.MigrationFilesEncoding",
            m => m.ProductDefaults.MigrationFilesEncoding,
            (target, val) => target.ProductDefaults.MigrationFilesEncoding = val,
            results);

        TryPromoteAcrossAll(peModels, state.BaseModel, "ProductDefaults.RequireRollbackFile",
            m => m.ProductDefaults.RequireRollbackFile.ToString(),
            (target, val) => target.ProductDefaults.RequireRollbackFile = bool.Parse(val),
            results);

        TryPromoteAcrossAll(peModels, state.BaseModel, "ProductDefaults.StopRollbackOnMissingRollbackFile",
            m => m.ProductDefaults.StopRollbackOnMissingRollbackFile.ToString(),
            (target, val) => target.ProductDefaults.StopRollbackOnMissingRollbackFile = bool.Parse(val),
            results);

        // Promote Serilog.MinimumLevelDefault
        TryPromoteAcrossAll(peModels, state.BaseModel, "Serilog.MinimumLevelDefault",
            m => m.Serilog.MinimumLevelDefault,
            (target, val) => target.Serilog.MinimumLevelDefault = val,
            results);

        // Per-environment promotion: promote connection strings common within an environment
        var envGroups = peModels
            .GroupBy(kv => kv.Key.Split('.', 2).Length == 2 ? kv.Key.Split('.', 2)[1] : "")
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1);

        foreach (var envGroup in envGroups)
        {
            var env = envGroup.Key;
            var envModels = envGroup.ToDictionary(kv => kv.Key, kv => kv.Value);

            if (!state.EnvironmentModels.ContainsKey(env))
            {
                state.EnvironmentModels[env] = new ConfigurationModel
                {
                    FilePath = $"appsettings.{env}.json",
                    FileRole = ConfigFileRole.Environment,
                    Repository = new RepositoryModel(),
                };
            }

            TryPromoteAcrossAll(envModels, state.EnvironmentModels[env], $"Repository.ConnectionString ({env})",
                m => m.Repository.ConnectionString,
                (target, val) => target.Repository.ConnectionString = val,
                results);
        }

        // Reconcile base Products from PE models — update base Product/TargetGroup/Target
        // aliases and ConnectionStrings to match the current PE structure
        ReconcileBaseProducts(state, results);

        return results;
    }

    /// <summary>
    /// Updates the base model's Products to reflect the current structure in PE models.
    /// For each product, collects the union of TargetGroup and Target aliases from all PEs,
    /// then updates the base to match. This ensures the base file is not stale and the
    /// Products field-level diff can eliminate redundancy.
    /// </summary>
    private static void ReconcileBaseProducts(WizardState state, List<PromotionResult> results)
    {
        if (state.ProductEnvironmentModels.Count == 0)
            return;

        foreach (var baseProduct in state.BaseModel.Products)
        {
            // Find all PE models for this product
            var peProducts = state.ProductEnvironmentModels
                .Select(kv =>
                {
                    var parts = kv.Key.Split('.', 2);
                    return parts.Length == 2 && string.Equals(parts[0], baseProduct.Alias, StringComparison.OrdinalIgnoreCase)
                        ? kv.Value.Products.FirstOrDefault(p => string.Equals(p.Alias, baseProduct.Alias, StringComparison.OrdinalIgnoreCase))
                        : null;
                })
                .Where(p => p != null)
                .Cast<ProductModel>()
                .ToList();

            if (peProducts.Count == 0)
                continue;

            // Use the first PE product as reference for MigrationFilesRootDirectory
            // (pick the value that's most common across PEs, or keep base if all differ)
            var rootDirs = peProducts.Select(p => p.MigrationFilesRootDirectory).Distinct().ToList();
            if (rootDirs.Count == 1 && rootDirs[0] != baseProduct.MigrationFilesRootDirectory)
            {
                baseProduct.MigrationFilesRootDirectory = rootDirs[0];
                results.Add(new PromotionResult
                {
                    PropertyName = $"Products[{baseProduct.Alias}].MigrationFilesRootDirectory",
                    PromotedValue = rootDirs[0],
                    AffectedProducts = peProducts.Count,
                    Level = "BaseModel"
                });
            }

            // Reconcile TargetGroups — collect union of aliases from all PEs
            var allTgAliases = peProducts
                .SelectMany(p => p.TargetGroups.Select(tg => tg.Alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var tgAlias in allTgAliases)
            {
                var baseTg = baseProduct.TargetGroups.FirstOrDefault(tg =>
                    string.Equals(tg.Alias, tgAlias, StringComparison.OrdinalIgnoreCase));

                // Collect PE target groups with this alias
                var peTgs = peProducts
                    .SelectMany(p => p.TargetGroups)
                    .Where(tg => string.Equals(tg.Alias, tgAlias, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (peTgs.Count == 0)
                    continue;

                if (baseTg == null)
                {
                    // New TG in PEs but not in base — add it
                    baseTg = new TargetGroupModel
                    {
                        Alias = peTgs[0].Alias,
                        DatabaseType = peTgs[0].DatabaseType,
                    };
                    baseProduct.TargetGroups.Add(baseTg);
                    results.Add(new PromotionResult
                    {
                        PropertyName = $"Products[{baseProduct.Alias}].TargetGroups[{tgAlias}]",
                        PromotedValue = "(added from PE models)",
                        AffectedProducts = peTgs.Count,
                        Level = "BaseModel"
                    });
                }
                else
                {
                    // Update DatabaseType if all PEs agree
                    var dbTypes = peTgs.Select(tg => tg.DatabaseType).Distinct().ToList();
                    if (dbTypes.Count == 1 && dbTypes[0] != baseTg.DatabaseType)
                    {
                        baseTg.DatabaseType = dbTypes[0];
                    }
                }

                // Reconcile Targets within this TargetGroup
                var allTargetAliases = peTgs
                    .SelectMany(tg => tg.Targets.Select(t => t.Alias))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var targetAlias in allTargetAliases)
                {
                    var baseTarget = baseTg.Targets.FirstOrDefault(t =>
                        string.Equals(t.Alias, targetAlias, StringComparison.OrdinalIgnoreCase));

                    if (baseTarget == null)
                    {
                        // New target in PEs — add to base with generic ConnectionString
                        var sanitizedProduct = SanitizeForEnv(baseProduct.Alias);
                        var sanitizedTg = SanitizeForEnv(tgAlias);
                        var sanitizedTarget = SanitizeForEnv(targetAlias);

                        baseTarget = new TargetModel
                        {
                            Alias = targetAlias,
                            ConnectionString = $"{{ENV:{sanitizedProduct}_{sanitizedTg}_{sanitizedTarget}_CONNECTION_STRING}}",
                        };
                        baseTg.Targets.Add(baseTarget);
                        results.Add(new PromotionResult
                        {
                            PropertyName = $"Products[{baseProduct.Alias}].TargetGroups[{tgAlias}].Targets[{targetAlias}]",
                            PromotedValue = "(added from PE models)",
                            AffectedProducts = 1,
                            Level = "BaseModel"
                        });
                    }
                    else if (string.IsNullOrWhiteSpace(baseTarget.ConnectionString))
                    {
                        // Existing target has empty ConnectionString — backfill with ENV placeholder
                        var sanitizedProduct = SanitizeForEnv(baseProduct.Alias);
                        var sanitizedTg = SanitizeForEnv(tgAlias);
                        var sanitizedTarget = SanitizeForEnv(targetAlias);

                        baseTarget.ConnectionString = $"{{ENV:{sanitizedProduct}_{sanitizedTg}_{sanitizedTarget}_CONNECTION_STRING}}";
                        results.Add(new PromotionResult
                        {
                            PropertyName = $"Products[{baseProduct.Alias}].TargetGroups[{tgAlias}].Targets[{targetAlias}].ConnectionString",
                            PromotedValue = baseTarget.ConnectionString,
                            AffectedProducts = 1,
                            Level = "BaseModel"
                        });
                    }
                }

                // Remove targets from base that no longer exist in ANY PE
                var targetsToRemove = baseTg.Targets
                    .Where(t => !allTargetAliases.Any(a => string.Equals(a, t.Alias, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                foreach (var staleTarget in targetsToRemove)
                {
                    baseTg.Targets.Remove(staleTarget);
                    results.Add(new PromotionResult
                    {
                        PropertyName = $"Products[{baseProduct.Alias}].TargetGroups[{tgAlias}].Targets[{staleTarget.Alias}]",
                        PromotedValue = "(removed — no longer in any PE model)",
                        AffectedProducts = 0,
                        Level = "BaseModel"
                    });
                }
            }

            // Remove TGs from base that no longer exist in ANY PE
            var tgsToRemove = baseProduct.TargetGroups
                .Where(tg => !allTgAliases.Any(a => string.Equals(a, tg.Alias, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (var staleTg in tgsToRemove)
            {
                baseProduct.TargetGroups.Remove(staleTg);
            }
        }
    }

    private static string SanitizeForEnv(string alias)
    {
        return alias.ToUpperInvariant()
            .Replace(' ', '_')
            .Replace('-', '_')
            .Replace('.', '_');
    }

    private static void TryPromoteAcrossAll(
        Dictionary<string, ConfigurationModel> models,
        ConfigurationModel target,
        string propertyName,
        Func<ConfigurationModel, string?> getValue,
        Action<ConfigurationModel, string> setValue,
        List<PromotionResult> results)
    {
        var values = models.Values
            .Select(getValue)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (values.Count != 1)
            return;

        // All models share the same non-empty value
        var currentTargetValue = getValue(target);
        if (string.Equals(currentTargetValue, values[0], StringComparison.Ordinal))
            return; // Already promoted

        setValue(target, values[0]!);

        // Clear from source models (set to empty/null to indicate it's inherited)
        foreach (var model in models.Values)
        {
            try { setValue(model, ""); } catch { /* ignore if setting fails */ }
        }

        results.Add(new PromotionResult
        {
            PropertyName = propertyName,
            PromotedValue = values[0]!,
            AffectedProducts = models.Count,
            Level = "BaseModel"
        });
    }

    private static void TryPromoteStringOverride(
        ConfigurationModel model,
        string propertyName,
        Func<ProductModel, OverridableValue<string>> getOverride,
        Action<ProductDefaultsModel, string> setDefault,
        List<PromotionResult> results)
    {
        var overrides = model.Products
            .Select(p => getOverride(p))
            .Where(o => o.IsOverridden && o.Value != null)
            .ToList();

        if (overrides.Count != model.Products.Count || overrides.Count == 0)
            return;

        var distinctValues = overrides.Select(o => o.Value!).Distinct(StringComparer.Ordinal).ToList();
        if (distinctValues.Count != 1)
            return;

        string promotedValue = distinctValues[0];
        setDefault(model.ProductDefaults, promotedValue);

        foreach (var product in model.Products)
        {
            var ov = getOverride(product);
            ov.IsOverridden = false;
            ov.Value = default;
        }

        results.Add(new PromotionResult
        {
            PropertyName = propertyName,
            PromotedValue = promotedValue,
            AffectedProducts = model.Products.Count,
            Level = "ProductDefaults"
        });
    }

    private static void TryPromoteBoolOverride(
        ConfigurationModel model,
        string propertyName,
        Func<ProductModel, OverridableValue<bool>> getOverride,
        Action<ProductDefaultsModel, bool> setDefault,
        List<PromotionResult> results)
    {
        var overrides = model.Products
            .Select(p => getOverride(p))
            .Where(o => o.IsOverridden)
            .ToList();

        if (overrides.Count != model.Products.Count || overrides.Count == 0)
            return;

        var distinctValues = overrides.Select(o => o.Value).Distinct().ToList();
        if (distinctValues.Count != 1)
            return;

        bool promotedValue = distinctValues[0];
        setDefault(model.ProductDefaults, promotedValue);

        foreach (var product in model.Products)
        {
            var ov = getOverride(product);
            ov.IsOverridden = false;
            ov.Value = default;
        }

        results.Add(new PromotionResult
        {
            PropertyName = propertyName,
            PromotedValue = promotedValue.ToString(),
            AffectedProducts = model.Products.Count,
            Level = "ProductDefaults"
        });
    }

    private static void TryPromoteTargetGroupStringOverride(
        ConfigurationModel model,
        string propertyName,
        Func<TargetGroupModel, OverridableValue<string>> getOverride,
        Action<TargetGroupDefaultsModel, string> setDefault,
        List<PromotionResult> results,
        bool clearOnly = false)
    {
        var allTgs = model.Products.SelectMany(p => p.TargetGroups).ToList();
        if (allTgs.Count == 0)
            return;

        var overrides = allTgs
            .Select(tg => getOverride(tg))
            .Where(o => o.IsOverridden && o.Value != null)
            .ToList();

        if (overrides.Count != allTgs.Count || overrides.Count == 0)
            return;

        var distinctValues = overrides.Select(o => o.Value!).Distinct(StringComparer.Ordinal).ToList();
        if (distinctValues.Count != 1)
            return;

        string promotedValue = distinctValues[0];

        if (!clearOnly)
            setDefault(model.ProductDefaults.TargetGroupDefaults, promotedValue);

        foreach (var tg in allTgs)
        {
            var ov = getOverride(tg);
            ov.IsOverridden = false;
            ov.Value = default;
        }

        results.Add(new PromotionResult
        {
            PropertyName = propertyName,
            PromotedValue = promotedValue,
            AffectedProducts = model.Products.Count,
            Level = "TargetGroupDefaults"
        });
    }

    private static void TryPromoteTargetGroupBoolOverride(
        ConfigurationModel model,
        string propertyName,
        Func<TargetGroupModel, OverridableValue<bool>> getOverride,
        Action<TargetGroupDefaultsModel, bool> setDefault,
        List<PromotionResult> results)
    {
        var allTgs = model.Products.SelectMany(p => p.TargetGroups).ToList();
        if (allTgs.Count == 0)
            return;

        var overrides = allTgs
            .Select(tg => getOverride(tg))
            .Where(o => o.IsOverridden)
            .ToList();

        if (overrides.Count != allTgs.Count || overrides.Count == 0)
            return;

        var distinctValues = overrides.Select(o => o.Value).Distinct().ToList();
        if (distinctValues.Count != 1)
            return;

        bool promotedValue = distinctValues[0];
        setDefault(model.ProductDefaults.TargetGroupDefaults, promotedValue);

        foreach (var tg in allTgs)
        {
            var ov = getOverride(tg);
            ov.IsOverridden = false;
            ov.Value = default;
        }

        results.Add(new PromotionResult
        {
            PropertyName = propertyName,
            PromotedValue = promotedValue.ToString(),
            AffectedProducts = model.Products.Count,
            Level = "TargetGroupDefaults"
        });
    }

    private static void TryPromoteTargetIntOverride(
        ConfigurationModel model,
        string propertyName,
        Func<TargetModel, OverridableValue<int>> getOverride,
        Action<TargetDefaultsModel, int> setDefault,
        List<PromotionResult> results)
    {
        var allTargets = model.Products.SelectMany(p => p.TargetGroups.SelectMany(tg => tg.Targets)).ToList();
        if (allTargets.Count == 0)
            return;

        var overrides = allTargets
            .Select(t => getOverride(t))
            .Where(o => o.IsOverridden)
            .ToList();

        if (overrides.Count != allTargets.Count || overrides.Count == 0)
            return;

        var distinctValues = overrides.Select(o => o.Value).Distinct().ToList();
        if (distinctValues.Count != 1)
            return;

        int promotedValue = distinctValues[0];
        setDefault(model.ProductDefaults.TargetGroupDefaults.TargetDefaults, promotedValue);

        foreach (var target in allTargets)
        {
            var ov = getOverride(target);
            ov.IsOverridden = false;
            ov.Value = default;
        }

        results.Add(new PromotionResult
        {
            PropertyName = propertyName,
            PromotedValue = promotedValue.ToString(),
            AffectedProducts = model.Products.Count,
            Level = "TargetGroupDefaults"
        });
    }
}
