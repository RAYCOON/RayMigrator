using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Extensions;

/// <summary>
/// Single source of truth for which side effects a command may have (#6).
/// </summary>
/// <remarks>
/// <para>
/// <c>--run-mode</c> exists on <c>migrate-up</c> and <c>migrate-down</c> only; every other command runs in
/// <see cref="MigrationRunMode.Migrate"/> mode and decides its side effects by command. The resulting matrix:
/// </para>
/// <code>
/// Command                  RunMode   Executes  Connects  Reads  Writes  DbLog
/// MigrateUp / MigrateDown  Migrate   yes       yes       yes    yes     yes
/// MigrateUp / MigrateDown  Simulate  yes       yes       yes    no      no
/// MigrateUp / MigrateDown  Validate  yes       no        no     no      no
/// Baseline                 Migrate   no        no        yes    yes     yes
/// UpdateHash               Migrate   no        no        yes    yes     yes
/// FixIssues                Migrate   no        no        yes    yes     yes
/// FixIssues --dry-run      Migrate   no        no        yes    no      no
/// Info                     Migrate   no        no        yes    no      no
/// ValidateHash             Migrate   no        no        yes    no      no
/// </code>
/// <para>
/// <c>Baseline</c> does not execute SQL on the targets, so it does not require them to be reachable.
/// </para>
/// </remarks>
public static class MigrationCommandExtensions
{
    /// <summary>
    /// Returns the <see cref="CommandProfile"/> for the command (and run mode / dry-run flag) in <paramref name="options"/>.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">The command has no profile (<see cref="MigrationCommand.None"/> or an unknown value).</exception>
    public static CommandProfile GetProfile(this RayMigratorConsoleOptions options)
        => GetProfile(options.Command, options.RunMode, options.FixDryRun ?? false);

    /// <summary>
    /// Returns the <see cref="CommandProfile"/> for a command, its run mode and (for <see cref="MigrationCommand.FixIssues"/>) the dry-run flag.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">The command has no profile (<see cref="MigrationCommand.None"/> or an unknown value).</exception>
    public static CommandProfile GetProfile(MigrationCommand command, MigrationRunMode runMode, bool fixDryRun = false) => command switch
    {
        // Undefined is the "not set" sentinel. Deriving a profile from it would yield a validate-like profile
        // (no connect, no read, no write) for a command the caller meant to run for real (#17).
        MigrationCommand.MigrateUp or MigrationCommand.MigrateDown when runMode == MigrationRunMode.Undefined =>
            throw new ConfigurationValidationException($"No command profile for [{command}] with run mode [{MigrationRunMode.Undefined}]. Choose migrate, simulate or validate."),

        MigrationCommand.MigrateUp or MigrationCommand.MigrateDown => new CommandProfile(
            ExecutesMigrations: true,
            ConnectsToTargets: runMode >= MigrationRunMode.Simulate,
            ReadsRepository: runMode.ShouldReadRepository(),
            WritesRepository: runMode.ShouldWriteRepository(),
            WritesDatabaseLog: runMode.ShouldWriteRepository()),

        MigrationCommand.Baseline => new CommandProfile(
            ExecutesMigrations: false, ConnectsToTargets: false, ReadsRepository: true, WritesRepository: true, WritesDatabaseLog: true),

        MigrationCommand.UpdateHash => new CommandProfile(
            ExecutesMigrations: false, ConnectsToTargets: false, ReadsRepository: true, WritesRepository: true, WritesDatabaseLog: true),

        MigrationCommand.FixIssues => new CommandProfile(
            ExecutesMigrations: false, ConnectsToTargets: false, ReadsRepository: true, WritesRepository: !fixDryRun, WritesDatabaseLog: !fixDryRun),

        MigrationCommand.Info => new CommandProfile(
            ExecutesMigrations: false, ConnectsToTargets: false, ReadsRepository: true, WritesRepository: false, WritesDatabaseLog: false),

        MigrationCommand.ValidateHash => new CommandProfile(
            ExecutesMigrations: false, ConnectsToTargets: false, ReadsRepository: true, WritesRepository: false, WritesDatabaseLog: false),

        _ => throw new ConfigurationValidationException($"No command profile for [{command}].")
    };
}
