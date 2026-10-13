namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Where a MigrationRun was started from. Stamped into <c>MigrationRun.MigrationRunSourceId</c> (schema version 2)
/// and seeded into the <c>MigrationRunSource</c> lookup table of every DAL. The CLI reports <see cref="Cli"/> or,
/// when a CI/CD pipeline variable is set, <see cref="Ci"/>; RayMigrator Studio reports <see cref="Studio"/>.
/// </summary>
public enum MigrationRunSource : byte
{
    /// <summary>
    /// Invalid value. The source has not been set.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Started by the RayMigrator command line on a workstation or server.
    /// </summary>
    Cli = 10,

    /// <summary>
    /// Started by the RayMigrator command line in a CI/CD pipeline.
    /// </summary>
    Ci = 20,

    /// <summary>
    /// Started by RayMigrator Studio.
    /// </summary>
    Studio = 30,
}
