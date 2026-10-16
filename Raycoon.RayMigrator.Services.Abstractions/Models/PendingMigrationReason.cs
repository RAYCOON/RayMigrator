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
    /// a run that is still open. Migrate-up refuses to start while such a run is alive and closes it as orphaned once
    /// its heartbeat is older than the threshold, which marks the record NotMigrated. Migrate-up runs the file from its
    /// first block.
    /// </summary>
    NotMigrated = 10,

    /// <summary>
    /// The last attempt on the target failed, or stopped without a final status in a run that has ended, and left
    /// nothing to resume: no block committed (with MigrationErrorAction=Ignore: the first block failed), every block is
    /// counted on the Failed record, the blocks of the file changed since, a rollback ran, or a CLI tool runs the file on
    /// the target and runs it whole. Migrate-up runs the file again from its first block.
    /// </summary>
    Failed = 20,

    /// <summary>
    /// A previous run committed the leading blocks of the file on the target and stopped: a failure, a cancel with
    /// Terminate, a run that ended after the last block without finalizing the record, or a failed block that
    /// MigrationErrorAction=Ignore skipped, after which the record counts the blocks before it. Migrate-up skips the
    /// committed blocks (<see cref="PendingMigrationInfo.ResumeFromBlock"/>) and continues with the next one, or only
    /// finalizes the record when every block committed. A RunAlways file is resumed like any other, so this reason
    /// takes precedence over <see cref="RunAlways"/>.
    /// </summary>
    Resumable = 30,

    /// <summary>
    /// The file runs on every migrate-up (<c>RunAlways = true</c>), whatever its record says.
    /// </summary>
    RunAlways = 40,

    /// <summary>
    /// The file is migrated on the target, but its hash under the HashValidationScope of the target group no longer
    /// matches the file on disk; migrate-up logs a warning and runs the file again from its first block.
    /// </summary>
    HashMismatch = 50,

    /// <summary>
    /// The rollback of the file on the target stopped in the middle: the record is Failed with some but not all of its
    /// rollback blocks (<see cref="PendingMigrationInfo.RollbackBlocksMigrated"/> of
    /// <see cref="PendingMigrationInfo.RollbackBlocksTotal"/>), as a cancelled migrate-down or error recovery or a failed
    /// rollback block after the first leaves it. migrate-up refuses to run while there is such a record, because running
    /// the file again from block 1 would act on a target that kept part of what the rollback did not take back yet;
    /// migrate-down continues the rollback with the next rollback block. This reason takes precedence over every other.
    /// </summary>
    RollbackIncomplete = 60
}
