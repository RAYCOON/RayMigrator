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
    /// Migrate down all migrations performed by the current MigrationRun.
    /// </summary>
    Rollback = 20,
    
    /// <summary>
    /// Migrate down only using the .down-file associated with the file that caused the error(s).
    /// In FileByFile order only the record of the target on which the error occurred is rolled back; targets
    /// that already ran the file keep it (#20).
    /// </summary>
    RollbackErrorOnly = 21,

    /// <summary>
    /// Migrate down the migrations of the release that caused the error which the current MigrationRun applied.
    /// Migrations of that release applied by earlier runs, and migrations from earlier releases, remain intact (#20).
    /// </summary>
    RollbackRelease = 22,

    /// <summary>
    /// Ignore the error and continue execution.
    /// Failed SQL blocks are skipped, and the migration file is marked as Failed.
    /// The migration run continues with the next file, is persisted as MigrationRunResult.PartialSuccess and
    /// the CLI exit code is 1 (#18, #20).
    /// </summary>
    Ignore = 30
}