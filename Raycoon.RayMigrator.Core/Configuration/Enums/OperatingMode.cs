
namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// The operating mode of RayMigrator, determined by bootstrap configuration.
/// </summary>
public enum OperatingMode
{
    /// <summary>
    /// Standalone mode: All configuration loaded from JSON files (appsettings.json hierarchy).
    /// No Admin-DB, no API server. This is the default mode.
    /// </summary>
    Standalone,

    /// <summary>
    /// Managed mode (local): Configuration loaded from a local Admin-DB.
    /// Products, Environments, Targets, and Repository config come from the Admin-DB.
    /// Serilog configuration still read from appsettings.json.
    /// </summary>
    ManagedLocal,

    /// <summary>
    /// Managed mode (remote): CLI operates as a Thin Client, sending HTTP requests
    /// to a remote RayMigrator API server instead of accessing databases directly.
    /// </summary>
    ManagedRemote
}
