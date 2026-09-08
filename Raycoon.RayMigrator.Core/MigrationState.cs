using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Class containing dynamic data of the entire migration process.
/// </summary>
public class MigrationState
{
    // Migration Process: RunId's
    public int MigratorMetaId { get; set; }
    public int ProductId { get; set; }
    public int EnvironmentId { get; set; }
    public int MigrationRunId { get; set; }
    public int MigrationRecordId { get; set; }

    // Migration Process: File metadata
    public string ReleaseVersionFromFileNameWithPath { get; set; } = string.Empty;
    public string FilenameWithRelativePath { get; set; } = string.Empty;
    public int FileOrderId { get; set; }

    /// <summary>
    /// 1-based number of the block that is currently being executed (or was being executed when an error occurred).
    /// Diagnostic only: log messages report it, nothing derives a repository value from it.
    /// </summary>
    public int FileBlockId { get; set; }

    /// <summary>
    /// Number of leading blocks of the current file that are committed on the current target. This is what a
    /// <c>Failed</c> record stores in <c>FileUpBlocksMigrated</c>, and what the next run skips when it resumes the
    /// file (#11). Block-by-block execution advances it after every committed block; atomic execution leaves it at
    /// the resume offset until the transaction commits, because a rolled-back transaction leaves nothing behind.
    /// </summary>
    public int FileBlocksCommitted { get; set; }

    // Migration Process: Step / Result
    public MigrationRunResult MigrationRunResult { get; set; }
    public MigrationOperation MigrationOperation { get; set; }
    public MigrationStatus MigrationStatus { get; set; }
    
    // Migration Process: TargetGroup- / Target-settings
    public string TargetGroupAlias { get; set; } = string.Empty;
    public HashValidationScope? HashValidationScope { get; set; } // In TargetGroup-Optionen enthalten
    public string TargetAlias { get; set; } = string.Empty;
}