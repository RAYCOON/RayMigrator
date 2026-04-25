namespace Raycoon.RayMigrator.Core.Configuration.Enums;

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
}