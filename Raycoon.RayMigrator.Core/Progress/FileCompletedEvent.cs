using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// Raised when a file finished on a target.
/// </summary>
/// <param name="Operation">MigrateUp, MigrateDown, Rollback (error recovery) or Baseline.</param>
/// <param name="ReleaseVersion">The release the file belongs to.</param>
/// <param name="TargetGroupAlias">The target group.</param>
/// <param name="TargetAlias">The target.</param>
/// <param name="FileName">The file name without path.</param>
/// <param name="MigrationRecordId">The repository record, or 0 when the run mode writes no records.</param>
/// <param name="Outcome">How the file ended.</param>
/// <param name="Duration">How long the file took on the target.</param>
/// <param name="ErrorMessage">The error of a failed file.</param>
public sealed record FileCompletedEvent(
    MigrationOperation Operation,
    string ReleaseVersion,
    string TargetGroupAlias,
    string TargetAlias,
    string FileName,
    int MigrationRecordId,
    FileOutcome Outcome,
    TimeSpan Duration,
    string? ErrorMessage);
