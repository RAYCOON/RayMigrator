namespace Raycoon.RayMigrator.Core.Configuration.Enums;

public enum MigrationRunResult : byte
{
    /// <summary>
    /// Invalid ResultId value. ResultId has not been set properly.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Migration process is currently running.
    /// </summary>
    Running = 10,

    /// <summary>
    /// The run finished, but at least one file was skipped or left <see cref="MigrationStatus.Failed"/>:
    /// a migrate-up that continued past a failure with <see cref="MigrationErrorAction.Ignore"/>, or a
    /// migrate-down that skipped a missing rollback file or ignored a failed rollback block
    /// (<see cref="RollbackErrorAction.Ignore"/>). The CLI exit code is unchanged (1 for migrate-up, 0 for
    /// migrate-down); the value makes the outcome distinguishable from an aborted run in the repository (#18).
    /// </summary>
    PartialSuccess = 50,

    /// <summary>
    /// The host cancelled the run and the engine stopped it at the next block or file boundary: a migrate-up whose
    /// <see cref="MigrationCancelledAction"/> completed (<see cref="MigrationCancelledAction.Terminate"/>, or a rollback
    /// without a failure or warning), or a migrate-down or baseline that stopped. A cancelled migrate-up whose rollback
    /// reported a failure or a warning (a failed rollback block, a missing rollback file, rollback errors that
    /// RollbackErrorAction=Ignore skipped) is persisted as <see cref="Error"/>, and so is a migrate-up that
    /// failed at a file before the cancel stopped its error recovery, with the code of that failure. The interrupted
    /// file is <see cref="MigrationStatus.Failed"/> with the blocks that committed before the cancel.
    /// </summary>
    Cancelled = 70,

    /// <summary>
    /// A migrate-up failed and the configured error recovery (<see cref="MigrationErrorAction.Rollback"/>,
    /// <see cref="MigrationErrorAction.RollbackRelease"/> or <see cref="MigrationErrorAction.RollbackErrorOnly"/>)
    /// completed without a failure or warning: every record the recovery touched is
    /// <see cref="MigrationStatus.NotMigrated"/> again, so repository and database are consistent. Files the error
    /// action leaves in place on purpose (earlier releases, other targets) stay <see cref="MigrationStatus.Migrated"/>.
    /// Terminate, a rollback failure, a skipped or missing rollback file and a stopped chain are persisted as
    /// <see cref="Error"/>; Ignore ends the run as <see cref="PartialSuccess"/>. The CLI exit code stays 1 (#18).
    /// </summary>
    Recovered = 80,

    /// <summary>
    /// Migration(s) stopped due to error(s): aborted without recovery, or the error recovery did not complete cleanly.
    /// Also a cancelled migrate-up whose rollback of its <see cref="MigrationCancelledAction"/> did not complete cleanly
    /// (engine code 1005, <c>TemplateResultCode.CancelledRollbackIncomplete</c>), a migrate-up that failed at a file
    /// before a cancel stopped its error recovery (the code of that failure), and a run that ended abruptly, which the
    /// orphan repair of a later start or <c>fix</c> closes (engine code 1006, <c>TemplateResultCode.OrphanedRunClosed</c>).
    /// </summary>
    Error = 90,

    /// <summary>
    /// Migration(s) successfully executed and finished.
    /// </summary>
    Ok = 100,
}
