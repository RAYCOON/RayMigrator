using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// Base result class for all operations
/// </summary>
public abstract class OperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Categorized error code. Negative = SQL template ResultCode, positive = C# backend ErrorCode, null = unclassified.
    /// See <see cref="Raycoon.RayMigrator.Shared.Constants.TemplateResultCode"/> for the catalog.
    /// </summary>
    public int? ErrorCode { get; set; }

    public List<string> Messages { get; set; } = new List<string>();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of a migration operation (up or down)
/// </summary>
public class MigrationOperationResult : OperationResult
{
    /// <summary>
    /// The repository id of the run row (<c>MigrationRun.Id</c>), or 0 when the run mode writes no run row
    /// (Simulate, Validate) or the run failed before the row was inserted.
    /// </summary>
    public int MigrationRunId { get; set; }
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public MigrationOperation Operation { get; set; }
    public MigrationRunResult Result { get; set; }
    public int TotalMigrations { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public string? CurrentRelease { get; set; }
    public List<MigrationFileResult> MigrationResults { get; set; } = new List<MigrationFileResult>();
}

/// <summary>
/// Result of a single migration file execution
/// </summary>
public class MigrationFileResult
{
    public string FileName { get; set; } = string.Empty;
    public string ReleaseVersion { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;

    /// <summary>
    /// The target the entry belongs to. Null when the entry summarizes all targets of the group, which is how
    /// FileByFile order reports a file that succeeded (one entry per file); TargetByTarget order, failures and
    /// rollbacks report the target.
    /// </summary>
    public string? TargetAlias { get; set; }

    /// <summary>The repository record of the file on the target, or 0 when none exists or applies.</summary>
    public int MigrationRecordId { get; set; }

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of hash validation
/// </summary>
public class ValidationResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int ValidFiles { get; set; }
    public int InvalidFiles { get; set; }
    public int MissingFiles { get; set; }
    public List<HashValidationIssue> Issues { get; set; } = new List<HashValidationIssue>();
}

/// <summary>
/// Individual hash validation issue
/// </summary>
public class HashValidationIssue
{
    public string FileName { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty; // "Modified", "Missing", "New"
    public string? ExpectedHash { get; set; }
    public string? ActualHash { get; set; }
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Result of hash update operation
/// </summary>
public class HashUpdateResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    /// <summary>Distinct files whose stored hashes were updated on at least one target.</summary>
    public int UpdatedFiles { get; set; }
    /// <summary>Repository records (one per file and target) whose hashes were updated (#9).</summary>
    public int UpdatedRecords { get; set; }
    public int NewFiles { get; set; }
    public int RemovedFiles { get; set; }
    public List<string> UpdatedFileNames { get; set; } = new List<string>();
}

/// <summary>
/// Result of a baseline operation
/// </summary>
public class BaselineResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;

    /// <summary>The repository id of the run row (<c>MigrationRun.Id</c>), or 0 when the run failed before it was inserted.</summary>
    public int MigrationRunId { get; set; }
    public string? TargetReleaseVersion { get; set; }
    /// <summary>Files recorded per release and target group, whether or not every target still needed them.</summary>
    public int BaselinedFiles { get; set; }

    /// <summary>Records written: one per file and target that was pending. Also set when the baseline failed midway.</summary>
    public int BaselinedRecords { get; set; }

    /// <summary>
    /// The result the run row records: <see cref="MigrationRunResult.Ok"/>, <see cref="MigrationRunResult.Error"/>, or
    /// <see cref="MigrationRunResult.Cancelled"/> when the host cancelled the baseline before the next file.
    /// </summary>
    public MigrationRunResult Result { get; set; }
}

/// <summary>
/// Current migration status overview for the Info command.
/// </summary>
public class MigrationStatusInfo
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string CurrentRelease { get; set; } = string.Empty;
    public DateTime? LastMigrationDate { get; set; }
    public int TotalMigrationsExecuted { get; set; }

    /// <summary>The number of files the next migrate-up runs, counted per file and target: always <c>PendingFiles.Count</c>.</summary>
    public int PendingMigrations { get; set; }

    /// <summary>
    /// The files the next migrate-up runs, one entry per file and target, with the reason and, for a resumed file, the
    /// committed blocks it skips. A file is pending on every target its <c>Targets</c> filter selects: a RunAlways file
    /// always, any other file unless the target has a Migrated record whose hash matches under the HashValidationScope
    /// of the target group.
    /// </summary>
    /// <remarks>
    /// The entries are in the order migrate-up runs them: releases in order; the target groups of a release in their
    /// TargetGroupMigrationOrder (the migsettings of the release, else the product), otherwise in configuration order;
    /// within a target group file by file or target by target as its TargetMigrationOrder says, the targets in
    /// configuration order. This is the order of a migrate-up without <c>--target-group-migration-order</c>; a run with
    /// <c>--to-release</c> or <c>--target-group</c> runs a part of the list. Files of a release older than what a
    /// target has migrated are listed as well, although migrate-up refuses them without <c>--allow-out-of-order</c>.
    /// </remarks>
    public List<PendingMigrationInfo> PendingFiles { get; set; } = [];
    public MigrationRunResult? LastRunResult { get; set; }
    public Dictionary<string, TargetGroupStatus> TargetGroups { get; set; } = new Dictionary<string, TargetGroupStatus>();
}

/// <summary>
/// Status of a target group
/// </summary>
public class TargetGroupStatus
{
    public string Alias { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public string CurrentRelease { get; set; } = string.Empty;
    public int ExecutedMigrations { get; set; }

    /// <summary>The entries of <see cref="MigrationStatusInfo.PendingFiles"/> that belong to this target group.</summary>
    public int PendingMigrations { get; set; }

    /// <summary>
    /// When the Migrated record of this target group that finished last finished (<c>MigrationRecord.FinishedAt</c>), in
    /// UTC (<see cref="DateTimeKind.Utc"/> on every engine); for a record that the orphan repair settled as Migrated, the
    /// time of the repair. Null when no Migrated record has an end, for example with an external DAL whose record select
    /// lacks the column.
    /// </summary>
    public DateTime? LastMigrationDate { get; set; }
    public List<string> Targets { get; set; } = new List<string>();
}

/// <summary>
/// Migration history
/// </summary>
public class MigrationHistory
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public List<MigrationRunInfo> Runs { get; set; } = new List<MigrationRunInfo>();
}

/// <summary>
/// Information about a migration run
/// </summary>
public class MigrationRunInfo
{
    /// <summary>The repository id of the run row (<c>MigrationRun.Id</c>).</summary>
    public int MigrationRunId { get; set; }
    public string Environment { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MigrationOperation Operation { get; set; }
    public MigrationRunResult Result { get; set; }
    public MigrationRunMode RunMode { get; set; }
    public int TotalMigrations { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }

    /// <summary>The highest Migrated release when the run started; null for a first run.</summary>
    public string? FromRelease { get; set; }
    public string? ToRelease { get; set; }

    /// <summary>Where the run was started from (<c>MigrationRun.MigrationRunSourceId</c>); null when the host did not say.</summary>
    public MigrationRunSource? Source { get; set; }

    /// <summary>The display name of the person or service account that started the run; null when unknown.</summary>
    public string? InitiatedBy { get; set; }

    /// <summary>A stable identifier of the initiator, for example the subject claim of an identity provider; null when unknown.</summary>
    public string? InitiatedBySubject { get; set; }

    /// <summary>The machine the run executed on.</summary>
    public string? Host { get; set; }

    /// <summary>The engine error code that decided a result other than Ok, when there was one.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>The message of the error that decided a result other than Ok, cut to 4000 characters; null for Ok.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>When the run last reported that it was alive (<c>MigrationRun.LastHeartbeatAt</c>).</summary>
    public DateTime? LastHeartbeatAt { get; set; }
}

/// <summary>
/// Result of Fix command execution
/// </summary>
public class FixIssuesResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    /// <summary>True when the fix ran in Simulate mode: the orphaned runs were listed but not repaired (#22).</summary>
    public bool WasSimulated { get; set; }

    /// <summary>
    /// The repairs the requested scope expanded to, in execution order (<c>All</c> lists every known repair) (#16).
    /// </summary>
    public List<FixScope> Repairs { get; set; } = new();
    public int OrphanedRunsFound { get; set; }
    public int OrphanedRunsFixed { get; set; }
    public List<OrphanedRunInfo> OrphanedRuns { get; set; } = new();
}

/// <summary>
/// Information about an orphaned MigrationRun entry
/// </summary>
public class OrphanedRunInfo
{
    public int MigrationRunId { get; set; }
    public string Environment { get; set; } = string.Empty;
    public int EnvironmentId { get; set; }
    public DateTime StartedAt { get; set; }
    public double MinutesRunning { get; set; }
    public int MigrationRunModeId { get; set; }
    public bool WasFixed { get; set; }

    /// <summary>When the run last reported that it was alive: its start until the first heartbeat; null only for a run row written without it.</summary>
    public DateTime? LastHeartbeatAt { get; set; }

    /// <summary>Minutes since the last heartbeat, or since the start for a run without one; the age <c>--older-than</c> compares.</summary>
    public double MinutesSinceHeartbeat { get; set; }

    /// <summary>Who started the run, when the run row says.</summary>
    public string? InitiatedBy { get; set; }

    /// <summary>The machine the run executed on, when the run row says.</summary>
    public string? Host { get; set; }
}
