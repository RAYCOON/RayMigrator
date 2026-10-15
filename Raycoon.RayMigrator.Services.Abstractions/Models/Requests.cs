using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// Request for forward migration
/// </summary>
public class MigrateUpRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? TargetReleaseVersion { get; set; }
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public bool AllowOutOfOrder { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
    public string[]? TargetGroupMigrationOrder { get; set; }
    /// <summary>
    /// The handle the host cancels this run with, or null. The run stops at the next block boundary and takes the
    /// action the first <see cref="RunCancellation.Cancel"/> chose; the token of the call cancels with
    /// <see cref="MigrationCancelledAction.Terminate"/>.
    /// </summary>
    public RunCancellation? Cancellation { get; set; }
}

/// <summary>
/// Request for rollback migration
/// </summary>
public class MigrateDownRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string TargetReleaseVersion { get; set; } = string.Empty;
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
    /// <summary>
    /// The handle the host cancels this run with, or null. A migrate-down stops at the next rollback block and
    /// ignores the action the handle carries: the record in progress stays Failed with its rollback blocks, and the
    /// next migrate-down continues it.
    /// </summary>
    public RunCancellation? Cancellation { get; set; }
}

/// <summary>
/// Request for hash validation
/// </summary>
public class ValidateHashRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public HashValidationScope? HashValidationScope { get; set; }
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
}

/// <summary>
/// Request for hash update
/// </summary>
public class UpdateHashRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
}

/// <summary>
/// Request for baseline operation (mark migrations as executed without running SQL)
/// </summary>
public class BaselineRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? TargetReleaseVersion { get; set; }
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
    public string[]? TargetGroupMigrationOrder { get; set; }
    /// <summary>
    /// The handle the host cancels this run with, or null. A baseline stops before the next file and ignores the
    /// action the handle carries; the records it wrote stay Migrated.
    /// </summary>
    public RunCancellation? Cancellation { get; set; }
}

/// <summary>
/// Request for fixing repository inconsistencies (orphaned runs, unclear migrations)
/// </summary>
public class FixIssuesRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public FixScope Scope { get; set; } = FixScope.OrphanedRuns;
    /// <summary>
    /// Minimum age of the last heartbeat (or of the start, for a run without one) in minutes for an open run to count
    /// as orphaned; 0 repairs every open run. Default 2, the engine's <c>Repository.OrphanedRunThresholdInMinutes</c>.
    /// </summary>
    public int OlderThanMinutes { get; set; } = 2;
    /// <summary>
    /// Migrate repairs the orphaned runs, Simulate only lists them and writes nothing. Must equal the run mode the
    /// <c>MigrationContext</c> was built with (#17, #22).
    /// </summary>
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate;
    public MigrationStatus AssumedMigrationStatus { get; set; } = MigrationStatus.NotMigrated;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
}
