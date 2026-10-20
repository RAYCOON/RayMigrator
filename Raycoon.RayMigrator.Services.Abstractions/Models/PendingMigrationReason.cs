namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// Why the next migrate-up runs a file on a target, as <see cref="PendingMigrationInfo.Reason"/> reports it. The reason
/// follows the repository record of the file on the target and what migrate-up does with that record.
/// </summary>
public enum PendingMigrationReason
{
    /// <summary>
    /// Invalid value.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// The file has not run on the target: it has no record there, its record was rolled back, or its record belongs to
    /// a run that is still open and the orphan repair will settle it as NotMigrated: no block of the file committed, or
    /// its rollback had run every rollback block. Migrate-up refuses to start while such a run is alive and closes it as
    /// orphaned once its heartbeat is older than the threshold; the orphan repair settles the records of that run by the
    /// progress of their files, and the pending list classifies them as the repair leaves them. Migrate-up runs the file
    /// from its first block.
    /// </summary>
    NotMigrated = 10,

    /// <summary>
    /// The last attempt on the target failed, or stopped without a final status in a run that has ended, and left
    /// nothing to resume: no block committed (with MigrationErrorAction=Ignore: the first block failed), every block is
    /// counted on the Failed record, the blocks of the file changed since, a rollback ran, or a CLI tool runs the file on
    /// the target and runs it whole. Migrate-up runs the file again from its first block. A migrate-down that rolls back
    /// the release of the file rolls it back when its record counts committed blocks and no rollback ran.
    /// </summary>
    Failed = 20,

    /// <summary>
    /// A previous run committed the leading blocks of the file on the target and stopped: a failure, a cancel with
    /// Terminate, a run that ended after the last block without finalizing the record, a run that ended abruptly, whose
    /// open record the orphan repair settles with its committed blocks, or a failed block that MigrationErrorAction=Ignore
    /// skipped, after which the record counts the blocks before it. Migrate-up skips the
    /// committed blocks (<see cref="PendingMigrationInfo.ResumeFromBlock"/>) and continues with the next one, or only
    /// finalizes the record when every block committed. A RunAlways file is never resumed or finalized, so it is
    /// <see cref="RunAlways"/> instead; neither is a file that a CLI tool runs on the target, because the tool runs the
    /// whole file, so it is <see cref="Failed"/> instead. A migrate-down that rolls back the release of the file rolls it
    /// back from its first rollback block instead, and migrate-up then runs it from its first block.
    /// </summary>
    Resumable = 30,

    /// <summary>
    /// The file runs on every migrate-up (<c>RunAlways = true</c>), whatever its record says, and always in full from its
    /// first block: migrate-up skips no block that an earlier attempt committed and finalizes no record of an earlier
    /// attempt, so this reason takes precedence over <see cref="Resumable"/> and every other reason except
    /// <see cref="RollbackIncomplete"/>.
    /// </summary>
    RunAlways = 40,

    /// <summary>
    /// The file is migrated on the target, but its hash under the HashValidationScope of the target group no longer
    /// matches the file on disk; migrate-up logs a warning and runs the file again from its first block.
    /// </summary>
    HashMismatch = 50,

    /// <summary>
    /// The rollback of the file on the target stopped in the middle: the record has some but not all of its rollback blocks
    /// (<see cref="PendingMigrationInfo.RollbackBlocksMigrated"/> of <see cref="PendingMigrationInfo.RollbackBlocksTotal"/>)
    /// and is Failed, as a cancelled migrate-down or error recovery, a failed rollback block after the first, or the orphan
    /// repair of a run that ended abruptly in the middle of a rollback leaves it, or still Executing in a run that ended
    /// without its final write. migrate-up refuses to run while there is such a record, because running
    /// the file again from block 1 would act on a target that kept part of what the rollback did not take back yet;
    /// migrate-down continues the rollback with the next rollback block. This reason takes precedence over every other.
    /// </summary>
    RollbackIncomplete = 60
}
