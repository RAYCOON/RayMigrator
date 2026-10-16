namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// One file on one target that the next migrate-up runs, and why: an entry of <see cref="MigrationStatusInfo.PendingFiles"/>.
/// </summary>
/// <param name="ReleaseVersion">The release the file belongs to.</param>
/// <param name="TargetGroupAlias">The target group of the file.</param>
/// <param name="TargetAlias">
/// The target the file runs on. Null when neither the configuration of the target group, the <c>Targets</c> filter of
/// the file nor a record names a target (options built in memory without targets); the entry then stands for the file.
/// </param>
/// <param name="Filename">The file name without path.</param>
/// <param name="FileOrderId">The position of the file in the discovery order, as <see cref="Core.Progress.FileStartedEvent"/> reports it.</param>
/// <param name="Reason">Why the next migrate-up runs the file on the target.</param>
/// <param name="ResumeFromBlock">
/// Only for <see cref="PendingMigrationReason.Resumable"/>, null otherwise: the number of leading blocks that committed
/// and that migrate-up skips, counted like <see cref="Core.Progress.FileStartedEvent.ResumeFromBlock"/> of that run.
/// Migrate-up continues with block <c>ResumeFromBlock + 1</c>; when the value equals the block count of the file, it
/// only finalizes the record.
/// </param>
/// <param name="RollbackBlocksMigrated">
/// Only for <see cref="PendingMigrationReason.RollbackIncomplete"/>, null otherwise: the rollback blocks that ran;
/// migrate-down continues with block <c>RollbackBlocksMigrated + 1</c>.
/// </param>
/// <param name="RollbackBlocksTotal">
/// Only for <see cref="PendingMigrationReason.RollbackIncomplete"/>, null otherwise: the blocks of the rollback file.
/// </param>
public sealed record PendingMigrationInfo(
    string ReleaseVersion,
    string TargetGroupAlias,
    string? TargetAlias,
    string Filename,
    int FileOrderId,
    PendingMigrationReason Reason,
    int? ResumeFromBlock,
    int? RollbackBlocksMigrated = null,
    int? RollbackBlocksTotal = null);
