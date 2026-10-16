using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Configuration.Options;

public class RayMigratorConsoleOptions
{
    /// <summary>
    /// The command to execute (Migrate-Up, Migrate-Down, Validate-Hash, Update-Hash)
    /// </summary>
    public required MigrationCommand Command { get; init; }
    
    /// <summary>
    /// Product alias from configuration
    /// </summary>
    public required string Product { get; init; }
    
    /// <summary>
    /// Target environment (required for all commands)
    /// </summary>
    public required string Environment { get; init; }
    
    /// <summary>
    /// Execution mode (Migrate or Simulate)
    /// </summary>
    public required MigrationRunMode RunMode { get; init; }
    
    /// <summary>
    /// Target release version for migration
    /// </summary>
    public string? TargetReleaseVersion { get; init; }

    /// <summary>
    /// Target group aliases to filter execution (optional, empty = all target groups)
    /// </summary>
    public string[]? TargetGroupAliases { get; init; }

    /// <summary>
    /// Explicit TargetGroup execution order (optional, parsed from comma-separated CLI input).
    /// When specified, all TargetGroup aliases must be listed exactly once.
    /// Applies to MigrateUp and Baseline commands only.
    /// </summary>
    public string[]? TargetGroupMigrationOrder { get; init; }
    
    /// <summary>
    /// Hash validation scope (for Validate-Hash command)
    /// </summary>
    public HashValidationScope? HashValidationScope { get; init; }
    
    /// <summary>
    /// Show startup information
    /// </summary>
    public required bool ShowStartupInfo { get; set; }
    
    /// <summary>
    /// Include sensitive data in logs (WARNING: includes passwords)
    /// </summary>
    public required bool RevealSensitiveData { get; init; }

    /// <summary>
    /// Fix command: Scope (All, OrphanedRuns)
    /// </summary>
    public FixScope? FixScope { get; init; }

    /// <summary>
    /// Allow out-of-order migration execution (Migrate-Up only)
    /// </summary>
    public bool? AllowOutOfOrder { get; init; }

    /// <summary>
    /// Fix command: Only fix runs whose last heartbeat (or start) is older than N minutes (default 2, 0 = immediate)
    /// </summary>
    public int? FixOlderThanMinutes { get; init; }

    /// <summary>
    /// Fix command: the status of <c>--last-migration-status</c> (Migrated or NotMigrated), set on every open record of an
    /// orphaned run; null when the option is not given, and every open record keeps the progress of its file.
    /// </summary>
    public MigrationStatus? FixAssumedMigrationStatus { get; init; }

    /// <summary>
    /// Stop error-recovery rollback chain when rollback file is missing (default: true).
    /// CLI override for StopRollbackOnMissingRollbackFile configuration.
    /// </summary>
    public bool? StopRollbackOnMissingRollbackFile { get; init; }

    /// <summary>
    /// What a run does when Ctrl+C or SIGTERM cancels the CLI process (<c>--cancelled-action</c> on migrate-up,
    /// migrate-down and baseline): the CLI passes it to the <c>RunCancellation.Cancel</c> of the first signal. Migrate-down
    /// and baseline accept it like migrate-up so that scripts can pass it to every run command, and ignore it: a
    /// cancelled migrate-down stops at the next rollback block, a cancelled baseline before the next file.
    /// Default <see cref="MigrationCancelledAction.Terminate"/>, also for every other command.
    /// </summary>
    public MigrationCancelledAction CancelledAction { get; init; } = MigrationCancelledAction.Terminate;

    /// <summary>
    /// Override directory where RayMigrator searches for appsettings.json files.
    /// When null, the current working directory is used.
    /// Always resolved to an absolute path at parse time.
    /// </summary>
    public string? ConfigDir { get; init; }

    /// <summary>
    /// Where the run is started from, stamped into <c>MigrationRun.MigrationRunSourceId</c>. The CLI sets
    /// <see cref="MigrationRunSource.Cli"/> or <see cref="MigrationRunSource.Ci"/> (see <see cref="RunInitiator"/>),
    /// RayMigrator Studio sets <see cref="MigrationRunSource.Studio"/>. Null is stored as NULL.
    /// </summary>
    public MigrationRunSource? Source { get; init; }

    /// <summary>
    /// The display name of the person or service account that started the run (<c>MigrationRun.InitiatedBy</c>):
    /// the OS user for the CLI, the signed-in user for Studio. Null is stored as NULL.
    /// </summary>
    public string? InitiatedBy { get; init; }

    /// <summary>
    /// A stable identifier of the initiator (<c>MigrationRun.InitiatedBySubject</c>), for example the subject claim of
    /// the identity provider in Studio. The CLI leaves it null.
    /// </summary>
    public string? InitiatedBySubject { get; init; }

    /// <summary>
    /// The machine the run executes on (<c>MigrationRun.Host</c>). The engine uses the machine name when null.
    /// </summary>
    public string? Host { get; init; }

}