using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Models;

/// <summary>
/// Represents a migration record from the repository database.
/// Maps to the result of Repository_MigrationRecord_Select query.
/// Used for comparing already-executed migrations with files on disk.
/// </summary>
public class MigrationRecord
{
    /// <summary>
    /// The Migration record ID in the repository.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The Product ID.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// The parent MigrationRun ID.
    /// </summary>
    public int MigrationRunId { get; set; }

    /// <summary>
    /// The migration operation (MigrateUp=100, MigrateDown=50, Rollback=5).
    /// </summary>
    public MigrationOperation MigrationOperationId { get; set; }

    /// <summary>
    /// The migration status (Pending=10, Executing=20, Failed=30, NotMigrated=50, Migrated=100).
    /// </summary>
    public MigrationStatus MigrationStatusId { get; set; }

    /// <summary>
    /// The release version (e.g., "Release 1.0").
    /// </summary>
    public string ReleaseVersion { get; set; } = string.Empty;

    /// <summary>
    /// The target group alias (e.g., "Backend").
    /// </summary>
    public string TargetGroupAlias { get; set; } = string.Empty;

    /// <summary>
    /// The target alias (e.g., "MainDB").
    /// </summary>
    public string TargetAlias { get; set; } = string.Empty;

    /// <summary>
    /// The migration filename without path.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// The file execution order.
    /// </summary>
    public int FileOrderId { get; set; }

    /// <summary>
    /// SHA256 hash of the entire up-migration file.
    /// </summary>
    public string FileUpHash { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the TOML config section of the up-migration file.
    /// </summary>
    public string? FileUpConfigHash { get; set; }

    /// <summary>
    /// SHA256 hash of the SQL blocks of the up-migration file.
    /// </summary>
    public string FileUpBlocksHash { get; set; } = string.Empty;

    /// <summary>
    /// Number of blocks successfully migrated (up).
    /// </summary>
    public int FileUpBlocksMigrated { get; set; }

    /// <summary>
    /// Total number of blocks in the up-migration file.
    /// </summary>
    public int FileUpBlocksTotal { get; set; }

    /// <summary>
    /// Whether a corresponding rollback file exists.
    /// </summary>
    public bool MigrateDownFileExists { get; set; }

    /// <summary>
    /// SHA256 hash of the entire rollback file.
    /// </summary>
    public string? FileDownHash { get; set; }

    /// <summary>
    /// SHA256 hash of the TOML config section of the rollback file.
    /// </summary>
    public string? FileDownConfigHash { get; set; }

    /// <summary>
    /// SHA256 hash of the SQL blocks of the rollback file.
    /// </summary>
    public string? FileDownBlocksHash { get; set; }

    /// <summary>
    /// Number of rollback blocks successfully executed.
    /// </summary>
    public int? FileDownBlocksMigrated { get; set; }

    /// <summary>
    /// Total number of blocks in the rollback file.
    /// </summary>
    public int? FileDownBlocksTotal { get; set; }
}
