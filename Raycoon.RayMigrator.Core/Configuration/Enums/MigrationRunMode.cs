namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Execution mode of <c>migrate-up</c> and <c>migrate-down</c>, chosen by the user via <c>--run-mode</c>.
/// Every other command runs in <see cref="Migrate"/> mode and decides its own side effects through its
/// <see cref="CommandProfile"/> (see <c>MigrationCommandExtensions.GetProfile</c>); the run mode of those
/// commands is only stamped into the rows they write, never evaluated.
/// </summary>
public enum MigrationRunMode : byte
{
    /// <summary>
    /// Invalid RunMode value. RunMode has not been set properly. Rejected when a <see cref="MigrationContext"/> is built.
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
    /// Also the mode every non-migrate command (validate-hash, update-hash, info, baseline, fix) runs in.
    /// </summary>
    Migrate = 100
}
