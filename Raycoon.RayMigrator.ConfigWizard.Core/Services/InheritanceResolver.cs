// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Resolves effective configuration values by applying the inheritance hierarchy:
/// ProductDefaults -> Product -> TargetGroup -> Target
/// </summary>
public static class InheritanceResolver
{
    // ── Product-level ──────────────────────────────────────────────

    public static string GetEffectiveMigrationErrorAction(ProductModel product, ProductDefaultsModel defaults) =>
        product.MigrationErrorAction.GetEffectiveValue(defaults.MigrationErrorAction);

    public static string GetEffectiveRollbackErrorAction(ProductModel product, ProductDefaultsModel defaults) =>
        product.RollbackErrorAction.GetEffectiveValue(defaults.RollbackErrorAction);

    public static string GetEffectiveMigrationFilesExtension(ProductModel product, ProductDefaultsModel defaults) =>
        product.MigrationFilesExtension.GetEffectiveValue(defaults.MigrationFilesExtension);

    public static string GetEffectiveMigrationRollbackFilesPreExtension(ProductModel product, ProductDefaultsModel defaults) =>
        product.MigrationRollbackFilesPreExtension.GetEffectiveValue(defaults.MigrationRollbackFilesPreExtension);

    public static string GetEffectiveMigrationFilesEncoding(ProductModel product, ProductDefaultsModel defaults) =>
        product.MigrationFilesEncoding.GetEffectiveValue(defaults.MigrationFilesEncoding);

    public static bool GetEffectiveRequireRollbackFile(ProductModel product, ProductDefaultsModel defaults) =>
        product.RequireRollbackFile.GetEffectiveValue(defaults.RequireRollbackFile);

    public static bool GetEffectiveStopRollbackOnMissingRollbackFile(ProductModel product, ProductDefaultsModel defaults) =>
        product.StopRollbackOnMissingRollbackFile.GetEffectiveValue(defaults.StopRollbackOnMissingRollbackFile);

    // ── TargetGroup-level ──────────────────────────────────────────

    public static string GetEffectiveTargetMigrationOrder(TargetGroupModel tg, ProductDefaultsModel defaults) =>
        tg.TargetMigrationOrder.GetEffectiveValue(defaults.TargetGroupDefaults.TargetMigrationOrder);

    public static string GetEffectiveHashValidationScope(TargetGroupModel tg, ProductDefaultsModel defaults) =>
        tg.HashValidationScope.GetEffectiveValue(defaults.TargetGroupDefaults.HashValidationScope);

    public static bool GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup(TargetGroupModel tg, ProductDefaultsModel defaults) =>
        tg.StopRollbackOnMissingRollbackFile.GetEffectiveValue(defaults.TargetGroupDefaults.StopRollbackOnMissingRollbackFile);

    // ── Target-level ───────────────────────────────────────────────

    public static int GetEffectiveTimeout(TargetModel target, ProductDefaultsModel defaults) =>
        target.DbCommandTimeoutInSeconds.GetEffectiveValue(defaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds);

    public static int GetEffectiveMaxRetries(TargetModel target, ProductDefaultsModel defaults) =>
        target.DbCommandMaxRetries.GetEffectiveValue(defaults.TargetGroupDefaults.TargetDefaults.DbCommandMaxRetries);

    public static int GetEffectiveWaitTime(TargetModel target, ProductDefaultsModel defaults) =>
        target.DbCommandWaitTimeInMsBeforeRetry.GetEffectiveValue(defaults.TargetGroupDefaults.TargetDefaults.DbCommandWaitTimeInMsBeforeRetry);

    // ── UseCliToolAlias inheritance (4-level: ProductDefaults -> Product -> TargetGroup -> Target) ──

    public static string? GetEffectiveUseCliToolAlias(
        TargetModel target, TargetGroupModel tg, ProductModel product, ProductDefaultsModel defaults)
    {
        if (target.UseCliToolAlias is { IsOverridden: true, Value: not null })
            return target.UseCliToolAlias.Value;
        if (tg.UseCliToolAlias is { IsOverridden: true, Value: not null })
            return tg.UseCliToolAlias.Value;
        if (product.UseCliToolAlias is { IsOverridden: true, Value: not null })
            return product.UseCliToolAlias.Value;
        return defaults.UseCliToolAlias;
    }

    // ── CliToolParameters inheritance (Product -> TargetGroup -> Target) ──

    /// <summary>
    /// Resolves the effective CliToolParameters by walking up:
    /// Target -> TargetGroup -> Product.
    /// Returns the first non-null, non-empty dictionary found, or null.
    /// </summary>
    public static Dictionary<string, string>? GetEffectiveCliToolParameters(
        TargetModel target, TargetGroupModel tg, ProductModel product)
    {
        if (target.CliToolParameters is { Count: > 0 })
            return target.CliToolParameters;
        if (tg.CliToolParameters is { Count: > 0 })
            return tg.CliToolParameters;
        if (product.CliToolParameters is { Count: > 0 })
            return product.CliToolParameters;
        return null;
    }

    /// <summary>
    /// Returns the source label for the effective CliToolParameters.
    /// </summary>
    public static string? GetCliToolParametersSourceLabel(
        TargetModel target, TargetGroupModel tg, ProductModel product)
    {
        if (target.CliToolParameters is { Count: > 0 })
            return "Target";
        if (tg.CliToolParameters is { Count: > 0 })
            return "TargetGroup";
        if (product.CliToolParameters is { Count: > 0 })
            return "Product";
        return null;
    }

    // ── Effective config entries ────────────────────────────────────

    /// <summary>
    /// Returns a list of all effective configuration values for a target with their source info.
    /// </summary>
    public static List<EffectiveConfigEntry> GetEffectiveTargetConfig(
        TargetModel target, TargetGroupModel tg, ProductModel product, ProductDefaultsModel defaults)
    {
        var entries = new List<EffectiveConfigEntry>();

        entries.Add(new("Alias", target.Alias, "target"));
        entries.Add(new("ConnectionString", target.ConnectionString, "target"));
        entries.Add(new("DbCommandTimeoutInSeconds",
            GetEffectiveTimeout(target, defaults).ToString(),
            target.DbCommandTimeoutInSeconds.IsOverridden ? "override" : "default"));
        entries.Add(new("DbCommandMaxRetries",
            GetEffectiveMaxRetries(target, defaults).ToString(),
            target.DbCommandMaxRetries.IsOverridden ? "override" : "default"));
        entries.Add(new("DbCommandWaitTimeInMsBeforeRetry",
            GetEffectiveWaitTime(target, defaults).ToString(),
            target.DbCommandWaitTimeInMsBeforeRetry.IsOverridden ? "override" : "default"));

        entries.Add(new("DatabaseType", tg.DatabaseType, "target-group"));
        entries.Add(new("TargetMigrationOrder",
            GetEffectiveTargetMigrationOrder(tg, defaults),
            tg.TargetMigrationOrder.IsOverridden ? "override" : "default"));
        entries.Add(new("HashValidationScope",
            GetEffectiveHashValidationScope(tg, defaults),
            tg.HashValidationScope.IsOverridden ? "override" : "default"));
        entries.Add(new("StopRollbackOnMissingRollbackFile (TargetGroup)",
            GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup(tg, defaults).ToString(),
            tg.StopRollbackOnMissingRollbackFile.IsOverridden ? "override" : "default"));

        entries.Add(new("MigrationErrorAction",
            GetEffectiveMigrationErrorAction(product, defaults),
            product.MigrationErrorAction.IsOverridden ? "override at Product" : "default"));
        entries.Add(new("MigrationFilesEncoding",
            GetEffectiveMigrationFilesEncoding(product, defaults),
            product.MigrationFilesEncoding.IsOverridden ? "override at Product" : "default"));
        entries.Add(new("RequireRollbackFile",
            GetEffectiveRequireRollbackFile(product, defaults).ToString(),
            product.RequireRollbackFile.IsOverridden ? "override at Product" : "default"));
        entries.Add(new("StopRollbackOnMissingRollbackFile (Product)",
            GetEffectiveStopRollbackOnMissingRollbackFile(product, defaults).ToString(),
            product.StopRollbackOnMissingRollbackFile.IsOverridden ? "override at Product" : "default"));

        var effectiveCliAlias = GetEffectiveUseCliToolAlias(target, tg, product, defaults);
        entries.Add(new("UseCliToolAlias",
            effectiveCliAlias ?? "(none)",
            target.UseCliToolAlias.IsOverridden ? "override at Target"
            : tg.UseCliToolAlias.IsOverridden ? "override at TargetGroup"
            : product.UseCliToolAlias.IsOverridden ? "override at Product"
            : defaults.UseCliToolAlias != null ? "default" : "not set"));

        var effectiveParams = GetEffectiveCliToolParameters(target, tg, product);
        var paramsSource = GetCliToolParametersSourceLabel(target, tg, product);
        entries.Add(new("CliToolParameters",
            effectiveParams is { Count: > 0 }
                ? string.Join(", ", effectiveParams.Select(kv => $"{kv.Key}={kv.Value}"))
                : "(none)",
            paramsSource != null ? $"override at {paramsSource}" : "not set"));

        return entries;
    }
}
