using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// Raised when a file is about to run on a target.
/// </summary>
/// <param name="Operation">MigrateUp, MigrateDown, Rollback (error recovery) or Baseline.</param>
/// <param name="ReleaseVersion">The release the file belongs to.</param>
/// <param name="TargetGroupAlias">The target group.</param>
/// <param name="TargetAlias">The target the file runs on.</param>
/// <param name="FileName">The file name without path.</param>
/// <param name="FileOrderId">The position of the file in the execution order.</param>
/// <param name="MigrationRecordId">The repository record of the file on the target, or 0 when the run mode writes no records.</param>
/// <param name="BlocksTotal">The number of SQL blocks of the file; 1 for a file or a rollback file that runs through a CLI
/// tool. For a rollback that ends before its rollback file is parsed, the count the repository recorded for the record.</param>
/// <param name="ResumeFromBlock">The number of leading blocks a previous run already committed and this run skips; for a
/// rollback, the rollback blocks an earlier attempt ran, after which it continues. Always 0 when the file or the rollback
/// file runs through a CLI tool, which runs it whole, and when migrate-up starts a RunAlways file.</param>
/// <param name="StartedAtUtc">When the file started.</param>
public sealed record FileStartedEvent(
    MigrationOperation Operation,
    string ReleaseVersion,
    string TargetGroupAlias,
    string TargetAlias,
    string FileName,
    int FileOrderId,
    int MigrationRecordId,
    int BlocksTotal,
    int ResumeFromBlock,
    DateTime StartedAtUtc);
