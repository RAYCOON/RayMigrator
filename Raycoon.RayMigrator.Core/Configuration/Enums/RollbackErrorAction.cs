
namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Defines the behavior when a rollback operation encounters an error.
/// Counterpart to <see cref="MigrationErrorAction"/> for rollback scenarios.
/// Since a failed rollback cannot itself be rolled back, only Terminate and Ignore are meaningful.
/// </summary>
public enum RollbackErrorAction : byte
{
    /// <summary>
    /// Invalid value.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Instantly terminate the rollback chain and do not perform any further rollbacks.
    /// This is the default behavior.
    /// </summary>
    Terminate = 10,

    /// <summary>
    /// Ignore the error and continue execution.
    /// Failed SQL blocks are skipped, and the rollback file is marked as Failed.
    /// The rollback chain continues with the next file.
    /// </summary>
    Ignore = 30
}
