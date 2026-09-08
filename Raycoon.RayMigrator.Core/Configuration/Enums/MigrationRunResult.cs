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
    /// Migration(s) stopped due to error(s).
    /// </summary>
    Error = 90,

    /// <summary>
    /// Migration(s) successfully executed and finished.
    /// </summary>
    Ok = 100,
}
