namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// What the <c>fix</c> command repairs (<c>--scope</c>). <c>MigrationService.RepairsFor</c> expands a scope into
/// the repairs to run; a new repair has to be added there, otherwise <see cref="All"/> would silently skip it (#16).
/// Formerly named <c>FixIssues</c>, which collided with the command, the request and the service method.
/// </summary>
public enum FixScope : byte
{
    /// <summary>
    /// Invalid FixScope value. FixScope has not been set properly.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Runs every repair RayMigrator knows, in a fixed order. Today that is <see cref="OrphanedRuns"/>.
    /// </summary>
    All = 1,

    /// <summary>
    /// Fixes orphaned MigrationRun entries (process crashed while Running).
    /// </summary>
    OrphanedRuns = 2,
}
