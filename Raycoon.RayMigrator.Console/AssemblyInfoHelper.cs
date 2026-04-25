namespace Raycoon.RayMigrator;

/// <summary>
/// Provides assembly information to be displayed at program startup.
/// Delegates to the shared helper for logo and version.
/// </summary>
public static class AssemblyInfoHelper
{
    /// <summary>
    /// Returns the ASCII-art startup header with version, description, and copyright.
    /// </summary>
    public static string GetAssemblyInfo()
    {
        return Raycoon.RayMigrator.Shared.AssemblyInfoHelper.GetAsciiHeader();
    }

    /// <summary>
    /// Gets the RayMigrator version. Delegates to the shared helper.
    /// </summary>
    public static string GetRayMigratorVersion()
    {
        return Raycoon.RayMigrator.Shared.AssemblyInfoHelper.GetRayMigratorVersion();
    }
}
