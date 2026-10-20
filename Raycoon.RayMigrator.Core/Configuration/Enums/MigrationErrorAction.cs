namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// What a migrate-up does after a migration file failed (ProductDefaults / Product / migsettings / file header).
/// </summary>
public enum MigrationErrorAction : byte
{
    /// <summary>
    /// Invalid value.
    /// </summary>
    Undefined = 0,
    
    /// <summary>
    /// Instantly terminate all migrations and do not perform any further actions.
    /// </summary>
    Terminate = 10,
    
    /// <summary>
    /// Migrate down the file that failed and every file the current MigrationRun ran before it, newest first: the files it
    /// migrated and the files it left Failed under <see cref="Ignore"/>, each from its first rollback block.
    /// </summary>
    Rollback = 20,
    
    /// <summary>
    /// Migrate down only using the .down-file associated with the file that caused the error(s).
    /// In FileByFile order only the record of the target on which the error occurred is rolled back; targets
    /// that already ran the file keep it (#20).
    /// </summary>
    RollbackErrorOnly = 21,

    /// <summary>
    /// Migrate down the file that failed and the files of its release the current MigrationRun ran before it, newest first:
    /// the files it migrated and the files it left Failed under <see cref="Ignore"/>.
    /// Migrations of that release applied by earlier runs, and migrations from earlier releases, remain intact (#20).
    /// </summary>
    RollbackRelease = 22,

    /// <summary>
    /// Ignore the error and continue execution.
    /// Failed SQL blocks are skipped, and the migration file is marked as Failed.
    /// The migration run continues with the next file, is persisted as MigrationRunResult.PartialSuccess and
    /// the CLI exit code is 1 (#18, #20). A later <see cref="Rollback"/> or <see cref="RollbackRelease"/> of the same run,
    /// and the rollback of a <see cref="MigrationCancelledAction"/>, roll the file back from its first rollback block with the
    /// files the run migrated, so its rollback file must tolerate a partially applied file.
    /// </summary>
    Ignore = 30
}