namespace Raycoon.RayMigrator.Core.Configuration.Enums;

public enum FixIssues : byte
{
    /// <summary>
    /// Invalid RunMode value. RunMode has not been set properly.
    /// </summary>
    Undefined = 0,
    
    /// <summary>
    /// Fixes all known problems in repository.
    /// </summary>
    All = 1,
    
    /// <summary>
    /// Fixes orphaned MigrationRun entries (process crashed while Running).
    /// </summary>
    OrphanedRuns = 2,
}