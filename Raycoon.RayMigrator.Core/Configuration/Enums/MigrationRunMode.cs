namespace Raycoon.RayMigrator.Core.Configuration.Enums;

public enum MigrationRunMode : byte
{
    /// <summary>
    /// Invalid RunMode value. RunMode has not been set properly.
    /// </summary>
    Undefined = 0,
    
    /// <summary>
    /// Validates configuration and all migration files. Does NOT connect to target databases or repository database.
    /// </summary>
    Validate = 10,

    /// <summary>
    /// Validates, checks DB connectivity, reads repository state. Does NOT execute SQL against target databases or write to the repository.
    /// </summary>
    Simulate = 20,
    
    /// <summary>
    /// Validates configuration and all migration files. Performs actual migrations against target databases.
    /// </summary>
    Migrate = 100
}
