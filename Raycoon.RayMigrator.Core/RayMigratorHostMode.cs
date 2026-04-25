namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Defines the hosting mode for RayMigrator's DI registration.
/// </summary>
public enum RayMigratorHostMode
{
    /// <summary>
    /// CLI mode: short-lived process, singleton MigrationContext.
    /// </summary>
    Cli,

    /// <summary>
    /// API mode: long-lived server, per-request MigrationContext via AsyncLocal.
    /// </summary>
    Api
}
