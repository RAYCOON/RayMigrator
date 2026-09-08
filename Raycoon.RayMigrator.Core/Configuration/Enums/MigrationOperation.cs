namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// What a MigrationRun / MigrationRecord did. Stamped into <c>MigrationOperationId</c> and shown as
/// <b>Operation</b> in the <c>info</c> run history. Not to be confused with <see cref="MigrationCommand"/>
/// (the CLI verb) or <see cref="MigrationRunMode"/> (migrate / simulate / validate).
/// </summary>
public enum MigrationOperation : byte
{
    /// <summary>
    /// Invalid value. RunOperation has not been set properly.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Performing Rollback.
    /// </summary>
    Rollback = 5,

    /// <summary>
    /// Performing Down-Migration.
    /// </summary>
    MigrateDown = 50,

    /// <summary>
    /// Performing Up-Migration.
    /// </summary>
    MigrateUp = 100,

    /// <summary>
    /// Marking migration files as migrated without executing them (baseline command).
    /// </summary>
    Baseline = 110,
}
