
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

/// <summary>Test-only factory methods for building <see cref="ValidationInput"/> fixtures.</summary>
internal static class InputFactory
{
    public static ValidationInput Minimal(
        IReadOnlyList<ProductInput>? products = null,
        IReadOnlyList<CliToolInput>? cliTools = null,
        ProductDefaultsInput? defaults = null,
        RepositoryInput? repository = null,
        RepositoryInput? databaseLogging = null)
        => new()
        {
            Repository = repository,
            DatabaseLogging = databaseLogging,
            Products = products ?? Array.Empty<ProductInput>(),
            CliTools = cliTools ?? Array.Empty<CliToolInput>(),
            Defaults = defaults ?? new ProductDefaultsInput(),
        };

    public static ProductInput Product(
        string alias,
        IReadOnlyList<TargetGroupInput>? targetGroups = null,
        string? migrationFilesRootDirectory = "/tmp",
        string? targetGroupMigrationOrder = null,
        string? useCliToolAlias = null,
        string? effectiveErrorAction = "Terminate",
        string? effectiveRollbackErrorAction = null,
        string? effectiveMigrationFilesExtension = "sql",
        string? effectiveRollbackPreExtension = "rollback")
        => new()
        {
            Alias = alias,
            MigrationFilesRootDirectory = migrationFilesRootDirectory,
            TargetGroupMigrationOrder = targetGroupMigrationOrder,
            UseCliToolAlias = useCliToolAlias,
            EffectiveMigrationErrorAction = effectiveErrorAction,
            EffectiveRollbackErrorAction = effectiveRollbackErrorAction,
            EffectiveMigrationFilesExtension = effectiveMigrationFilesExtension,
            EffectiveRollbackPreExtension = effectiveRollbackPreExtension,
            TargetGroups = targetGroups ?? Array.Empty<TargetGroupInput>(),
        };

    public static TargetGroupInput TargetGroup(
        string alias,
        string? databaseType = "SqlServer",
        IReadOnlyList<TargetInput>? targets = null,
        string? useCliToolAlias = null,
        string? effectiveTargetMigrationOrder = "Simultaneously",
        string? effectiveHashValidationScope = "File")
        => new()
        {
            Alias = alias,
            DatabaseType = databaseType,
            UseCliToolAlias = useCliToolAlias,
            EffectiveTargetMigrationOrder = effectiveTargetMigrationOrder,
            EffectiveHashValidationScope = effectiveHashValidationScope,
            Targets = targets ?? Array.Empty<TargetInput>(),
        };

    public static TargetInput Target(
        string alias,
        string? connectionString = "Server=.;Database=db;",
        string? useCliToolAlias = null,
        string? effectiveUseCliToolAlias = null,
        IReadOnlyDictionary<string, string>? cliToolParameters = null,
        IReadOnlyDictionary<string, string>? effectiveCliToolParameters = null)
        => new()
        {
            Alias = alias,
            ConnectionString = connectionString,
            UseCliToolAlias = useCliToolAlias,
            EffectiveUseCliToolAlias = effectiveUseCliToolAlias ?? useCliToolAlias,
            CliToolParameters = cliToolParameters,
            EffectiveCliToolParameters = effectiveCliToolParameters ?? cliToolParameters,
        };

    public static CliToolInput CliTool(
        string alias,
        string? executablePath = "sqlcmd",
        string? argumentTemplate = "-S {Server} -d {Database} -i {FilePath}",
        string? inputMode = "File",
        IReadOnlyList<string>? successExitCodes = null,
        int? cliToolTimeoutInSeconds = 120)
        => new()
        {
            Alias = alias,
            ExecutablePath = executablePath,
            ArgumentTemplate = argumentTemplate,
            InputMode = inputMode,
            SuccessExitCodes = successExitCodes,
            CliToolTimeoutInSeconds = cliToolTimeoutInSeconds,
        };
}
