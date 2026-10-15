namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// What a cancelled migrate-up does after it stopped at the next block boundary. The host chooses it when it
/// cancels one run (<c>RunCancellation.Cancel</c>); it is a value of that run only, not a configuration option, and
/// it never overrides <see cref="MigrationErrorAction"/>. The values mirror <see cref="MigrationErrorAction"/>
/// without <see cref="MigrationErrorAction.Ignore"/> and carry its numbers; a rollback action runs the same rollback
/// chain the error action of the same number runs, with the same rules for missing rollback files and
/// <see cref="RollbackErrorAction"/>. A cancel during migrate-down, baseline or a running error recovery stops at
/// the next block or file boundary and ignores the action.
/// </summary>
public enum MigrationCancelledAction : byte
{
    /// <summary>
    /// Invalid value.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Stop and do nothing else; the default. The interrupted file keeps its committed blocks and its record is
    /// <see cref="MigrationStatus.Failed"/>, so the next migrate-up resumes it with the first block that did not commit.
    /// </summary>
    Terminate = 10,

    /// <summary>
    /// Roll back the interrupted file and every file the run migrated before it, newest first, like
    /// <see cref="MigrationErrorAction.Rollback"/>.
    /// </summary>
    RollbackRun = 20,

    /// <summary>
    /// Roll back the interrupted file only, like <see cref="MigrationErrorAction.RollbackErrorOnly"/>. A run cancelled
    /// between two files has no interrupted file and rolls back nothing.
    /// </summary>
    RollbackFile = 21,

    /// <summary>
    /// Roll back the interrupted file and the files of its release the run migrated, like
    /// <see cref="MigrationErrorAction.RollbackRelease"/>. Without an interrupted file the release is the one of the
    /// file the run migrated last.
    /// </summary>
    RollbackRelease = 22
}
