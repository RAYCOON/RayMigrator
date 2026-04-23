// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Maps a <see cref="ConfigurationModel"/> (wizard-side) onto the neutral
/// <see cref="ValidationInput"/> DTO consumed by <see cref="Validation.RuleCatalog"/>.
/// Resolves every <c>OverridableValue&lt;T&gt;</c> cascade here so rules see pre-merged effective values.
/// </summary>
internal static class WizardValidationInputAdapter
{
    public static ValidationInput ToInput(ConfigurationModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var defaults = model.ProductDefaults;

        return new ValidationInput
        {
            Repository = ToRepositoryInput(model.Repository),
            DatabaseLogging = ToRepositoryInput(model.DatabaseLogging),
            CliTools = model.CliTools.Select(ToCliToolInput).ToList(),
            Defaults = ToDefaultsInput(defaults),
            Products = model.Products.Select(p => ToProductInput(p, defaults)).ToList(),
        };
    }

    private static RepositoryInput? ToRepositoryInput(RepositoryModel? repo) =>
        repo is null ? null : new RepositoryInput
        {
            DatabaseType = repo.DatabaseType,
            ConnectionString = repo.ConnectionString,
            SchemaName = repo.SchemaName,
            TableBaseName = repo.TableBaseName,
        };

    private static RepositoryInput? ToRepositoryInput(DatabaseLoggingModel? dbLog) =>
        dbLog is null ? null : new RepositoryInput
        {
            DatabaseType = dbLog.DatabaseType,
            ConnectionString = dbLog.ConnectionString,
            SchemaName = dbLog.SchemaName,
            TableBaseName = dbLog.TableBaseName,
        };

    private static ProductDefaultsInput ToDefaultsInput(ProductDefaultsModel defaults) =>
        new()
        {
            MigrationErrorAction = defaults.MigrationErrorAction,
            RollbackErrorAction = defaults.RollbackErrorAction,
            MigrationFilesExtension = defaults.MigrationFilesExtension,
            MigrationRollbackFilesPreExtension = defaults.MigrationRollbackFilesPreExtension,
            UseCliToolAlias = defaults.UseCliToolAlias,
            TargetMigrationOrder = defaults.TargetGroupDefaults.TargetMigrationOrder,
            HashValidationScope = defaults.TargetGroupDefaults.HashValidationScope,
        };

    private static CliToolInput ToCliToolInput(CliToolModel tool) =>
        new()
        {
            Alias = tool.Alias,
            ExecutablePath = tool.ExecutablePath,
            ArgumentTemplate = tool.ArgumentTemplate,
            InputMode = tool.InputMode,
            SuccessExitCodes = tool.SuccessExitCodes,
            CliToolTimeoutInSeconds = tool.CliToolTimeoutInSeconds,
        };

    private static ProductInput ToProductInput(ProductModel p, ProductDefaultsModel defaults) =>
        new()
        {
            Alias = p.Alias,
            MigrationFilesRootDirectory = p.MigrationFilesRootDirectory,
            TargetGroupMigrationOrder = p.TargetGroupMigrationOrder,
            UseCliToolAlias = p.UseCliToolAlias.IsOverridden ? p.UseCliToolAlias.Value : null,
            EffectiveMigrationErrorAction = InheritanceResolver.GetEffectiveMigrationErrorAction(p, defaults),
            EffectiveRollbackErrorAction = InheritanceResolver.GetEffectiveRollbackErrorAction(p, defaults),
            EffectiveMigrationFilesExtension = InheritanceResolver.GetEffectiveMigrationFilesExtension(p, defaults),
            EffectiveRollbackPreExtension = InheritanceResolver.GetEffectiveMigrationRollbackFilesPreExtension(p, defaults),
            TargetGroups = p.TargetGroups.Select(tg => ToTargetGroupInput(tg, p, defaults)).ToList(),
        };

    private static TargetGroupInput ToTargetGroupInput(TargetGroupModel tg, ProductModel p, ProductDefaultsModel defaults) =>
        new()
        {
            Alias = tg.Alias,
            DatabaseType = tg.DatabaseType,
            UseCliToolAlias = tg.UseCliToolAlias.IsOverridden ? tg.UseCliToolAlias.Value : null,
            EffectiveTargetMigrationOrder = InheritanceResolver.GetEffectiveTargetMigrationOrder(tg, defaults),
            EffectiveHashValidationScope = InheritanceResolver.GetEffectiveHashValidationScope(tg, defaults),
            Targets = tg.Targets.Select(t => ToTargetInput(t, tg, p, defaults)).ToList(),
        };

    private static TargetInput ToTargetInput(TargetModel t, TargetGroupModel tg, ProductModel p, ProductDefaultsModel defaults) =>
        new()
        {
            Alias = t.Alias,
            ConnectionString = t.ConnectionString,
            UseCliToolAlias = t.UseCliToolAlias.IsOverridden ? t.UseCliToolAlias.Value : null,
            EffectiveUseCliToolAlias = InheritanceResolver.GetEffectiveUseCliToolAlias(t, tg, p, defaults),
            CliToolParameters = t.CliToolParameters,
            // The wizard serializer propagates inherited CliToolParameters onto each Target on save
            // (see ConfigurationSerializer.BuildRayMigratorNode / BuildTargetDiff). The validator must
            // mirror that post-save state, otherwise pre-save validation understates mismatches
            // (Alias-Mismatch, missing-required-keys) for params defined at Product/TargetGroup level.
            // Engine-side adapter stays target-only because the engine runtime does not walk inheritance.
            EffectiveCliToolParameters = InheritanceResolver.GetEffectiveCliToolParameters(t, tg, p),
        };
}
