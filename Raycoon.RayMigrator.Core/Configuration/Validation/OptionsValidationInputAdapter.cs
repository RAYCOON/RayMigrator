using System.Globalization;
using Microsoft.Extensions.Configuration;
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
    /// <summary>
    /// Flattens the bound options. <paramref name="sectionAsWritten"/> is the RayMigrator section before
    /// <c>{ENV:...}</c> placeholders were resolved; the connection strings are taken from it as configured, so that
    /// RULE_7_3 sees the placeholder and not the resolved credential (#30), while the bound values stay the
    /// effective connection strings. Without the section the bound values serve as both.
    /// </summary>
    public static ValidationInput ToInput(RayMigratorOptions options, IConfigurationSection? sectionAsWritten = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var repository = ToRepositoryInput(options.Repository, Child(sectionAsWritten, nameof(RayMigratorOptions.Repository)));

        return new ValidationInput
        {
            Repository = repository,
            DatabaseLogging = options.IsDatabaseLoggingEnabled()
                ? ToDatabaseLoggingInput(options.DatabaseLogging!, Child(sectionAsWritten, nameof(RayMigratorOptions.DatabaseLogging)), repository)
                : null,
            CliTools = options.CliTools?.Select(ToCliToolInput).ToList() ?? new List<CliToolInput>(),
            Defaults = ToDefaultsInput(options.ProductDefaults),
            Products = options.Products?
                .Select((p, i) => ToProductInput(p, Child(Child(sectionAsWritten, nameof(RayMigratorOptions.Products)), i)))
                .ToList() ?? new List<ProductInput>(),
        };
    }

    private static RepositoryInput? ToRepositoryInput(RepositoryOptions? repo, IConfigurationSection? written) =>
        repo == null ? null : new RepositoryInput
        {
            DatabaseType = repo.DatabaseType,
            ConnectionString = AsWrittenOrBound(written, nameof(RepositoryOptions.ConnectionString), repo.ConnectionString),
            EffectiveConnectionString = repo.ConnectionString,
            SchemaName = repo.SchemaName,
            TableBaseName = repo.TableBaseName,
        };

    /// <summary>
    /// The logging section after <see cref="DatabaseLoggingDefaults"/>. A connection string the section inherited from
    /// the repository (nothing written, bound value equal to the repository's) is reported as the repository wrote it,
    /// so that RULE_7_3 sees the <c>{ENV:...}</c> placeholder and not the resolved credential.
    /// </summary>
    private static RepositoryInput ToDatabaseLoggingInput(DatabaseLoggingOptions dbLog, IConfigurationSection? written, RepositoryInput? repository)
    {
        string? connectionString = AsWrittenOrBound(written, nameof(DatabaseLoggingOptions.ConnectionString), dbLog.ConnectionString);
        bool inherited = written?[nameof(DatabaseLoggingOptions.ConnectionString)] == null
                         && repository != null
                         && dbLog.ConnectionString != null
                         && dbLog.ConnectionString == repository.EffectiveConnectionString;
        if (inherited)
            connectionString = repository!.ConnectionString;

        return new RepositoryInput
        {
            DatabaseType = dbLog.DatabaseType,
            ConnectionString = connectionString,
            EffectiveConnectionString = dbLog.ConnectionString,
            SchemaName = dbLog.SchemaName,
            TableBaseName = dbLog.TableBaseName,
        };
    }

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

    private static ProductInput ToProductInput(ProductOptions p, IConfigurationSection? written) =>
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

            TargetGroups = p.TargetGroups?
                .Select((tg, i) => ToTargetGroupInput(tg, p, Child(Child(written, nameof(ProductOptions.TargetGroups)), i)))
                .ToList() ?? new List<TargetGroupInput>(),
        };

    private static TargetGroupInput ToTargetGroupInput(TargetGroupOptions tg, ProductOptions parentProduct, IConfigurationSection? written) =>
        new()
        {
            Alias = tg.Alias,
            DatabaseType = tg.DatabaseType,
            UseCliToolAlias = tg.UseCliToolAlias,
            EffectiveTargetMigrationOrder = tg.TargetMigrationOrder,
            EffectiveHashValidationScope = tg.HashValidationScope,
            Targets = tg.Targets?
                .Select((t, i) => ToTargetInput(t, tg, parentProduct, Child(Child(written, nameof(TargetGroupOptions.Targets)), i)))
                .ToList() ?? new List<TargetInput>(),
        };

    private static TargetInput ToTargetInput(TargetOptions t, TargetGroupOptions tg, ProductOptions p, IConfigurationSection? written) =>
        new()
        {
            Alias = t.Alias,
            ConnectionString = AsWrittenOrBound(written, nameof(TargetOptions.ConnectionString), t.ConnectionString),
            EffectiveConnectionString = t.ConnectionString,
            UseCliToolAlias = t.UseCliToolAlias,
            // Runtime cascade: Target -> TargetGroup -> Product. Defaults are merged elsewhere.
            EffectiveUseCliToolAlias = t.UseCliToolAlias ?? tg.UseCliToolAlias ?? p.UseCliToolAlias,
            CliToolParameters = t.CliToolParameters,
            EffectiveCliToolParameters = t.CliToolParameters,
        };

    private static IConfigurationSection? Child(IConfigurationSection? section, string key) =>
        section?.GetSection(key);

    /// <summary>
    /// The list element at <paramref name="index"/>. The bound lists and the section as written come from the same
    /// merged document, so a list index equals the configuration key of the element.
    /// </summary>
    private static IConfigurationSection? Child(IConfigurationSection? section, int index) =>
        Child(section, index.ToString(CultureInfo.InvariantCulture));

    /// <summary>The value as written in the configuration when the section is known, otherwise the bound value.</summary>
    private static string? AsWrittenOrBound(IConfigurationSection? written, string key, string? bound) =>
        written?[key] ?? bound;
}
