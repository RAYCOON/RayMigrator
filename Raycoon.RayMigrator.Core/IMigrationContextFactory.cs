
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Factory for creating MigrationContext instances.
/// CLI creates one at startup; API creates one per request.
/// </summary>
public interface IMigrationContextFactory
{
    MigrationContext Create(
        RayMigratorOptions options,
        string product,
        string environment,
        MigrationRunMode runMode,
        string version,
        string? targetReleaseVersion = null,
        bool revealSensitiveData = false);
}

/// <summary>
/// Default factory implementation that creates MigrationContext instances from parameters.
/// </summary>
public class MigrationContextFactory : IMigrationContextFactory
{
    public MigrationContext Create(
        RayMigratorOptions options,
        string product,
        string environment,
        MigrationRunMode runMode,
        string version,
        string? targetReleaseVersion = null,
        bool revealSensitiveData = false)
    {
        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.MigrateUp, // Will be overridden per operation
            Product = product,
            Environment = environment,
            RunMode = runMode,
            TargetReleaseVersion = targetReleaseVersion,
            ShowStartupInfo = false,
            RevealSensitiveData = revealSensitiveData,
        };

        return new MigrationContext(options, consoleOptions, version);
    }
}
