using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// Raised after a SQL block of a file was executed on a target. On the atomic shared-connection path the blocks
/// of a file and the repository update commit together at the end of the file, so a block reported here is not yet
/// durable until <see cref="FileCompletedEvent"/> follows.
/// </summary>
/// <param name="Operation">MigrateUp, MigrateDown or Rollback.</param>
/// <param name="ReleaseVersion">The release the file belongs to.</param>
/// <param name="TargetGroupAlias">The target group.</param>
/// <param name="TargetAlias">The target.</param>
/// <param name="FileName">The file name without path.</param>
/// <param name="MigrationRecordId">The repository record, or 0 when the run mode writes no records.</param>
/// <param name="BlockNumber">The 1-based number of the block.</param>
/// <param name="BlocksTotal">The number of blocks of the file.</param>
/// <param name="Success">Whether the block ran without error. False only under the Ignore error action; otherwise a failing block ends the file.</param>
/// <param name="ErrorMessage">The error of a failed block.</param>
public sealed record BlockCompletedEvent(
    MigrationOperation Operation,
    string ReleaseVersion,
    string TargetGroupAlias,
    string TargetAlias,
    string FileName,
    int MigrationRecordId,
    int BlockNumber,
    int BlocksTotal,
    bool Success,
    string? ErrorMessage);
