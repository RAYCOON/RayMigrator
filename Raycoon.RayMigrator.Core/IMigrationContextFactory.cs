using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Factory for creating MigrationContext instances.
/// CLI creates one at startup; API creates one per request.
/// </summary>
public interface IMigrationContextFactory
{
    /// <summary>
    /// Creates a context for one command. The command is stamped into the MigrationRun settings snapshot and
    /// drives the command's side effects (see <see cref="Configuration.CommandProfile"/>), so it must be the
    /// command that is actually executed (#6). <paramref name="runMode"/> is only a choice for
    /// <see cref="MigrationCommand.MigrateUp"/> / <see cref="MigrationCommand.MigrateDown"/>; pass
    /// <see cref="MigrationRunMode.Migrate"/> for every other command.
    /// </summary>
    MigrationContext Create(
        RayMigratorOptions options,
        string product,
        string environment,
        MigrationCommand command,
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
        MigrationCommand command,
        MigrationRunMode runMode,
        string version,
        string? targetReleaseVersion = null,
        bool revealSensitiveData = false)
    {
        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = command,
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
