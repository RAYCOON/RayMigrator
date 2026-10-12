namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// How a file ended on a target, as reported by <see cref="FileCompletedEvent"/>.
/// </summary>
public enum FileOutcome
{
    Undefined = 0,

    /// <summary>Every block ran and the record is Migrated.</summary>
    Migrated = 10,

    /// <summary>A previous run had executed every block but not finalized the record; this run finalized it without executing SQL.</summary>
    Recovered = 20,

    /// <summary>Simulate or Validate run mode: the blocks were walked but no SQL ran.</summary>
    Simulated = 30,

    /// <summary>The file failed and the run handles the error (abort or rollback, depending on the error action).</summary>
    Failed = 40,

    /// <summary>The file failed and the run continues because the error action is Ignore.</summary>
    FailedIgnored = 50,

    /// <summary>The rollback file ran and the record is NotMigrated.</summary>
    RolledBack = 60,

    /// <summary>The rollback file failed.</summary>
    RollbackFailed = 70,

    /// <summary>Baseline recorded the file as Migrated without executing it.</summary>
    Baselined = 80,
}
