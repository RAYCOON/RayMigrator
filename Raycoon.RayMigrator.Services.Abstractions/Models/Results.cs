
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
    public Guid RunId { get; set; }
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
    public int UpdatedFiles { get; set; }
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
    public string? TargetReleaseVersion { get; set; }
    public int BaselinedFiles { get; set; }
}

/// <summary>
/// Current migration status overview for the Info command.
/// </summary>
public class MigrationStatusInfo
{
    public string ProductAlias { get; set; } = string.Empty;
    public string CurrentRelease { get; set; } = string.Empty;
    public DateTime? LastMigrationDate { get; set; }
    public int TotalMigrationsExecuted { get; set; }
    public int PendingMigrations { get; set; }
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
    public DateTime? LastMigrationDate { get; set; }
    public List<string> Targets { get; set; } = new List<string>();
}

/// <summary>
/// Migration history
/// </summary>
public class MigrationHistory
{
    public string ProductAlias { get; set; } = string.Empty;
    public List<MigrationRunInfo> Runs { get; set; } = new List<MigrationRunInfo>();
}

/// <summary>
/// Information about a migration run
/// </summary>
public class MigrationRunInfo
{
    public int MigrationRunId { get; set; }
    public Guid RunId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MigrationOperation Operation { get; set; }
    public MigrationRunResult Result { get; set; }
    public MigrationRunMode RunMode { get; set; }
    public string? InitiatedBy { get; set; }
    public int TotalMigrations { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public string? ToRelease { get; set; }
}

/// <summary>
/// Result of Fix command execution
/// </summary>
public class FixIssuesResult : OperationResult
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public bool WasDryRun { get; set; }
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
}