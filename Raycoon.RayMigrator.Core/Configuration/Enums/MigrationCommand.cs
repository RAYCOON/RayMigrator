
namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Represents the migration command to execute
/// </summary>
public enum MigrationCommand : byte
{
    /// <summary>
    /// No command specified
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Apply pending migrations forward (Migrate-Up command)
    /// </summary>
    MigrateUp = 1,
    
    /// <summary>
    /// Rollback to previous version (Migrate-Down command)
    /// </summary>
    MigrateDown = 2,
    
    /// <summary>
    /// Verify migration file integrity (Validate-Hash command)
    /// </summary>
    ValidateHash = 3,
    
    /// <summary>
    /// Update repository hashes after approved changes (Update-Hash command)
    /// </summary>
    UpdateHash = 4,

    /// <summary>
    /// Display migration status information (Info command)
    /// </summary>
    Info = 5,

    /// <summary>
    /// Mark existing database as migrated (Baseline command)
    /// </summary>
    Baseline = 6,

    /// <summary>
    /// Fix repository inconsistencies (Fix-Issues command)
    /// </summary>
    FixIssues = 7
}