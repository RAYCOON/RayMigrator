using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: what a cancelled run reports: the result Cancelled (70) and the file outcome Cancelled (100), the positive engine
/// codes of a cancel, the code an <see cref="OperationCanceledException"/> maps to, the error message that names the
/// action, its outcome and the reason, and that of a failed run whose error recovery a cancel stopped, which names the
/// failure first; the outcome of a cancelled migrate-down says where the cancel stopped its chain, that of a cancel action
/// whose rollback did not complete why.
/// </summary>
public class CancellationResultTests
{
    [Fact]
    public void ResultAndOutcome_Cancelled_HaveTheirSchemaValues()
    {
        // Act
        byte result = (byte)MigrationRunResult.Cancelled;
        int outcome = (int)FileOutcome.Cancelled;

        // Assert
        result.Should().Be(70, because: "schema step 3 adds the lookup row 70");
        outcome.Should().Be(100);
    }

    [Fact]
    public void TemplateResultCode_CancelCodes_ArePositiveEngineCodes()
    {
        // Act
        int cancelled = TemplateResultCode.Cancelled;
        int incomplete = TemplateResultCode.CancelledRollbackIncomplete;

        // Assert
        cancelled.Should().Be(1004, because: "the engine codes are positive and follow ConfigurationValidationFailed");
        incomplete.Should().Be(1005);
        TemplateResultCode.IsKnown(cancelled).Should().BeFalse(because: "the catalog of known codes lists the negative template codes only");
    }

    [Fact]
    public void ExtractErrorCode_OperationCanceled_IsCancelled()
    {
        // Act
        int? code = MigrationService.ExtractErrorCode(new OperationCanceledException());

        // Assert
        code.Should().Be(TemplateResultCode.Cancelled);
    }

    [Fact]
    public void ExtractErrorCode_TaskCanceled_IsCancelled()
    {
        // Act
        int? code = MigrationService.ExtractErrorCode(new TaskCanceledException());

        // Assert
        code.Should().Be(TemplateResultCode.Cancelled, because: "TaskCanceledException is an OperationCanceledException");
    }

    [Fact]
    public void ExtractErrorCode_OtherExceptions_KeepTheirCodes()
    {
        // Act
        int? unclassified = MigrationService.ExtractErrorCode(new InvalidOperationException("boom"));
        int? running = MigrationService.ExtractErrorCode(new MigrationAlreadyRunningException("busy"));

        // Assert
        unclassified.Should().BeNull();
        running.Should().Be(TemplateResultCode.MigrationAlreadyRunning);
    }

    [Fact]
    public void CancelledMessage_WithAnOutcome_NamesTheActionAndTheOutcome()
    {
        // Act
        string message = MigrationService.CancelledMessage(MigrationCancelledAction.RollbackFile, null, "1 file(s) rolled back", null);

        // Assert
        message.Should().Be("Cancelled on request (RollbackFile): 1 file(s) rolled back");
    }

    [Fact]
    public void CancelledMessage_WithAReason_AppendsIt()
    {
        // Act
        string message = MigrationService.CancelledMessage(MigrationCancelledAction.Terminate, null, "stopped before the next file", "deployment window closed");

        // Assert
        message.Should().Be("Cancelled on request (Terminate): stopped before the next file. Reason: deployment window closed");
    }

    [Fact]
    public void CancelledMessage_IgnoredAction_SaysWhy()
    {
        // Act
        string ignored = MigrationService.CancelledMessage(MigrationCancelledAction.RollbackRun, "a rollback was already running", "migrate-down stopped", null);
        string terminate = MigrationService.CancelledMessage(MigrationCancelledAction.Terminate, "a rollback was already running", "migrate-down stopped", null);

        // Assert
        ignored.Should().Be("Cancelled on request (RollbackRun ignored: a rollback was already running): migrate-down stopped");
        terminate.Should().Be("Cancelled on request (Terminate): migrate-down stopped", because: "Terminate is what happens anyway");
    }

    [Theory]
    [InlineData("no such table: T.", 0, null, 0, null,
        "no such table: T. The error recovery was cancelled on request before its first rollback: nothing was rolled back, " +
        "03_Seed.sql stays Failed with the blocks it committed, and the files this run migrated stay Migrated")]
    [InlineData("relation \"t\" does not exist", 1, null, 0, "Ctrl+C",
        "relation \"t\" does not exist. The error recovery was cancelled on request after 1 of 3 rollback(s): " +
        "the files it did not reach keep their state. Reason: Ctrl+C")]
    [InlineData("no such table: T", 0, "03_Seed.sql", 1, null,
        "no such table: T. The error recovery was cancelled on request in the rollback of 03_Seed.sql on target MainDB after 1 of 3 " +
        "rollback block(s); migrate-down continues that rollback, and migrate-up refuses 03_Seed.sql until it has completed")]
    [InlineData("no such table: T", 1, "02_Create.sql", 0, null,
        "no such table: T. The error recovery was cancelled on request in the rollback of 02_Create.sql on target MainDB, whose first " +
        "rollback block failed under RollbackErrorAction=Ignore: 02_Create.sql is Failed without rollback progress, which " +
        "migrate-down does not continue")]
    public void CancelledErrorRecoveryMessage_CancelAtEachPointOfTheRecovery_SaysWhereItStoppedAndWhatThatLeaves(
        string failure, int recordsDone, string? stoppedFile, int rollbackBlocksDone, string? reason, string expected)
    {
        // Arrange: before the first record, before a later record, between two rollback blocks of a record, and between
        // two rollback blocks of a record whose first rollback block failed under RollbackErrorAction=Ignore
        var stop = stoppedFile == null
            ? new MigrationService.RollbackStop(recordsDone)
            : new MigrationService.RollbackStop(recordsDone, stoppedFile, "MainDB", rollbackBlocksDone, 3);

        // Act
        string message = MigrationService.CancelledErrorRecoveryMessage(failure, "03_Seed.sql", stop, 3, reason);

        // Assert
        message.Should().Be(expected, because: "the failure decides the run, and where the cancel stopped its recovery follows as a sentence of its own");
    }

    [Theory]
    [InlineData(null, "Migration failed at 03_Seed.sql. The error recovery")]
    [InlineData("", "Migration failed at 03_Seed.sql. The error recovery")]
    [InlineData("  ", "Migration failed at 03_Seed.sql. The error recovery")]
    [InlineData("no such table: T. ", "no such table: T. The error recovery")]
    [InlineData(" no such table: T..", "no such table: T. The error recovery")]
    public void CancelledErrorRecoveryMessage_FailureWithoutTextOrWithTrailingDots_StartsWithOneSentence(string? failure, string expectedStart)
    {
        // Act
        string message = MigrationService.CancelledErrorRecoveryMessage(failure, "03_Seed.sql", new MigrationService.RollbackStop(1), 3, null);

        // Assert
        message.Should().StartWith(expectedStart,
            because: "a failure without text names the file it happened at, and the failure ends with exactly one period");
    }

    [Theory]
    [InlineData(0, "03_SeedDataB.sql", 1,
        "migrate-down stopped in the rollback of 03_SeedDataB.sql on target MainDB after 1 of 3 rollback block(s); the next " +
        "migrate-down continues that rollback, and migrate-up refuses 03_SeedDataB.sql until it has completed")]
    [InlineData(1, "02_CreateTableD.sql", 0,
        "migrate-down stopped in the rollback of 02_CreateTableD.sql on target MainDB, whose first rollback block failed under " +
        "RollbackErrorAction=Ignore: 02_CreateTableD.sql is Failed without rollback progress, which migrate-down does not continue")]
    [InlineData(0, null, 0, "migrate-down stopped before the first rollback: nothing was rolled back")]
    [InlineData(2, null, 0,
        "migrate-down stopped after 2 of 3 rollback(s): the files it did not reach keep their state, and the next migrate-down " +
        "rolls them back")]
    public void CancelledMigrateDownOutcome_CancelAtEachPointOfTheChain_SaysWhereItStoppedAndWhatThatLeaves(
        int recordsDone, string? stoppedFile, int rollbackBlocksDone, string expected)
    {
        // Arrange: between two rollback blocks of the first record, between two rollback blocks of the second record whose
        // first rollback block failed under RollbackErrorAction=Ignore, before the first record, and before the third record
        var stop = stoppedFile == null
            ? new MigrationService.RollbackStop(recordsDone)
            : new MigrationService.RollbackStop(recordsDone, stoppedFile, "MainDB", rollbackBlocksDone, 3);

        // Act
        string outcome = MigrationService.CancelledMigrateDownOutcome(stop, 3);

        // Assert
        outcome.Should().Be(expected,
            because: "the next migrate-down continues a rollback only when rollback blocks of it ran, and rolls back the records the cancel kept from it");
    }

    [Fact]
    public void IncompleteCancelActionOutcome_FailureAbortedTheChain_CountsTheRolledBackFilesAndNamesTheFailure()
    {
        // Act
        string outcome = MigrationService.IncompleteCancelActionOutcome(
            "Rollback chain aborted: block 2 of 02_CreateTableB.sql failed.", [], 1, 3);

        // Assert
        outcome.Should().Be("the rollback did not complete: 1 of 3 file(s) rolled back; Rollback chain aborted: block 2 of 02_CreateTableB.sql failed");
    }

    [Fact]
    public void IncompleteCancelActionOutcome_ChainWithWarningsOnly_NamesEveryWarning()
    {
        // Arrange: the chain went on past a missing rollback file and a rollback with ignored errors; it has no failure, and
        // its closing line counts both files as rolled back
        string[] warnings =
        [
            "02_CreateTableB.sql: Rollback file not found: 02_CreateTableB.rollback.sql",
            "01_CreateTableA.sql: Rollback completed with errors (some blocks failed, RollbackErrorAction=Ignore)"
        ];

        // Act
        string outcome = MigrationService.IncompleteCancelActionOutcome(null, warnings, 1, 3);

        // Assert
        outcome.Should().Be(
            "the rollback did not complete: 1 of 3 file(s) rolled back; 02_CreateTableB.sql: Rollback file not found: 02_CreateTableB.rollback.sql; " +
            "01_CreateTableA.sql: Rollback completed with errors (some blocks failed, RollbackErrorAction=Ignore)",
            because: "the warnings say why the rollback did not complete");
    }
}
