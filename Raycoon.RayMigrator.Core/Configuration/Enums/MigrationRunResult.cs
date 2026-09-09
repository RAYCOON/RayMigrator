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
    /// A migrate-up failed and the configured error recovery (<see cref="MigrationErrorAction.Rollback"/>,
    /// <see cref="MigrationErrorAction.RollbackRelease"/> or <see cref="MigrationErrorAction.RollbackErrorOnly"/>)
    /// completed without a failure or warning: every record the recovery touched is
    /// <see cref="MigrationStatus.NotMigrated"/> again, so repository and database are consistent. Files the error
    /// action leaves in place on purpose (earlier releases, other targets) stay <see cref="MigrationStatus.Migrated"/>.
    /// Terminate, Ignore, a rollback failure, a skipped or missing rollback file and a stopped chain are persisted as
    /// <see cref="Error"/>. The CLI exit code stays 1 (#18).
    /// </summary>
    Recovered = 80,

    /// <summary>
    /// Migration(s) stopped due to error(s): aborted without recovery, or the error recovery did not complete cleanly.
    /// </summary>
    Error = 90,

    /// <summary>
    /// Migration(s) successfully executed and finished.
    /// </summary>
    Ok = 100,
}
