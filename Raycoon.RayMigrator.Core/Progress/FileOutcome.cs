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

    /// <summary>Nothing ran on the target: a rollback without a rollback file, or a rollback chain that stopped at one.</summary>
    Skipped = 90,

    /// <summary>
    /// The run was cancelled while the file ran: the record is Failed with the blocks that committed before the run
    /// stopped (on the atomic path, whose transaction is rolled back, only those of an earlier attempt). A block the
    /// provider interrupts does not commit; SQLite, and MySQL after KILL QUERY, may run the block in progress to its end. A
    /// rollback that follows reports its own start and completion.
    /// </summary>
    Cancelled = 100,
}
