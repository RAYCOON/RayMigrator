
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// Maps a <see cref="RayMigratorOptions"/> instance (engine-side) onto the neutral
/// <see cref="ValidationInput"/> DTO consumed by <see cref="Validation.RuleCatalog"/>.
/// Assumes <see cref="ProductDefaultsPostConfigureOptions.MergeDefaults"/> has already run
/// so effective values are present on the per-product/per-TG properties.
/// </summary>
internal static class OptionsValidationInputAdapter
{
    public static ValidationInput ToInput(RayMigratorOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        return new ValidationInput
        {
            Repository = ToRepositoryInput(options.Repository),
            DatabaseLogging = ToRepositoryInput(options.DatabaseLogging),
            CliTools = options.CliTools?.Select(ToCliToolInput).ToList() ?? new List<CliToolInput>(),
            Defaults = ToDefaultsInput(options.ProductDefaults),
            Products = options.Products?.Select(ToProductInput).ToList() ?? new List<ProductInput>(),
        };
    }

    private static RepositoryInput? ToRepositoryInput(RepositoryOptions? repo) =>
        repo is null ? null : new RepositoryInput
        {
            DatabaseType = repo.DatabaseType,
            ConnectionString = repo.ConnectionString,
            SchemaName = repo.SchemaName,
            TableBaseName = repo.TableBaseName,
        };

    private static RepositoryInput? ToRepositoryInput(DatabaseLoggingOptions? dbLog) =>
        dbLog is null ? null : new RepositoryInput
        {
            DatabaseType = dbLog.DatabaseType,
            ConnectionString = dbLog.ConnectionString,
            SchemaName = dbLog.SchemaName,
            TableBaseName = dbLog.TableBaseName,
        };

    private static ProductDefaultsInput ToDefaultsInput(ProductDefaultOptions? defaults) =>
        new()
        {
            MigrationErrorAction = defaults?.MigrationErrorAction,
            RollbackErrorAction = defaults?.RollbackErrorAction,
            MigrationFilesExtension = defaults?.MigrationFilesExtension,
            MigrationRollbackFilesPreExtension = defaults?.MigrationRollbackFilesPreExtension,
            UseCliToolAlias = defaults?.UseCliToolAlias,
            TargetMigrationOrder = defaults?.TargetGroupDefaults?.TargetMigrationOrder,
            HashValidationScope = defaults?.TargetGroupDefaults?.HashValidationScope,
        };

    private static CliToolInput ToCliToolInput(CliToolOptions tool) =>
        new()
        {
            Alias = tool.Alias,
            ExecutablePath = tool.ExecutablePath,
            ArgumentTemplate = tool.ArgumentTemplate,
            InputMode = tool.InputMode,
            SuccessExitCodes = tool.SuccessExitCodes,
            CliToolTimeoutInSeconds = tool.CliToolTimeoutInSeconds,
        };

    private static ProductInput ToProductInput(ProductOptions p) =>
        new()
        {
            Alias = p.Alias,
            MigrationFilesRootDirectory = p.MigrationFilesRootDirectory,
            TargetGroupMigrationOrder = p.TargetGroupMigrationOrder,
            UseCliToolAlias = p.UseCliToolAlias,

            // After MergeDefaults, product-level fields carry the effective value.
            EffectiveMigrationErrorAction = p.MigrationErrorAction,
            EffectiveRollbackErrorAction = p.RollbackErrorAction,
            EffectiveMigrationFilesExtension = p.MigrationFilesExtension,
            EffectiveRollbackPreExtension = p.MigrationRollbackFilesPreExtension,

            TargetGroups = p.TargetGroups?.Select(tg => ToTargetGroupInput(tg, p)).ToList() ?? new List<TargetGroupInput>(),
        };

    private static TargetGroupInput ToTargetGroupInput(TargetGroupOptions tg, ProductOptions parentProduct) =>
        new()
        {
            Alias = tg.Alias,
            DatabaseType = tg.DatabaseType,
            UseCliToolAlias = tg.UseCliToolAlias,
            EffectiveTargetMigrationOrder = tg.TargetMigrationOrder,
            EffectiveHashValidationScope = tg.HashValidationScope,
            Targets = tg.Targets?.Select(t => ToTargetInput(t, tg, parentProduct)).ToList() ?? new List<TargetInput>(),
        };

    private static TargetInput ToTargetInput(TargetOptions t, TargetGroupOptions tg, ProductOptions p) =>
        new()
        {
            Alias = t.Alias,
            ConnectionString = t.ConnectionString,
            UseCliToolAlias = t.UseCliToolAlias,
            // Runtime cascade: Target -> TargetGroup -> Product. Defaults are merged elsewhere.
            EffectiveUseCliToolAlias = t.UseCliToolAlias ?? tg.UseCliToolAlias ?? p.UseCliToolAlias,
            CliToolParameters = t.CliToolParameters,
            EffectiveCliToolParameters = t.CliToolParameters,
        };
}
