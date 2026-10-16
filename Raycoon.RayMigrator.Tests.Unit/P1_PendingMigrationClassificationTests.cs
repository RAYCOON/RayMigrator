using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The reason the status gives a pending (file, target) pair follows what the next migrate-up does with its record
/// (<see cref="MigrationService.ClassifyPendingMigration"/>): the orphan repair before the first file turns the open
/// records of an open run NotMigrated, a record that migrate-up finalizes or resumes is Resumable with the committed
/// blocks it skips (also for a RunAlways file), then RunAlways, HashMismatch for a Migrated record whose hash no longer
/// matches, Failed for a failed attempt without a resume point, NotMigrated for the rest.
/// </summary>
public class PendingMigrationClassificationTests
{
    private const int OpenRunId = 7;

    private static MigrationFileInfo ThreeBlockFile(bool runAlways = false, string blocksHash = "blocks") => TestFactories.CreateMigrationFile(
        runAlways: runAlways, blocksHash: blocksHash, sqlBlocks: ["SELECT 1", "SELECT 2", "SELECT 3"]);

    private static MigrationRecord Record(
        MigrationStatus status, int blocksMigrated = 0, string blocksHash = "blocks", int runId = 3, string? fileDownHash = null)
    {
        var record = TestFactories.CreateMigrationRecord(id: 1, status: status, blocksHash: blocksHash,
            fileUpBlocksMigrated: blocksMigrated, fileUpBlocksTotal: 3, fileDownHash: fileDownHash);
        record.MigrationRunId = runId;
        return record;
    }

    private static (PendingMigrationReason Reason, int? ResumeFromBlock) Classify(
        MigrationFileInfo file, MigrationRecord? record, MigrationRecord? migratedRecord = null, int? openRunId = null)
        => MigrationService.ClassifyPendingMigration(file, "MainDB", record == null ? [] : [record], migratedRecord, openRunId);

    [Fact]
    public void Classify_WithoutRecord_IsNotMigrated()
    {
        // Act
        var result = Classify(ThreeBlockFile(), null);

        // Assert
        result.Should().Be((PendingMigrationReason.NotMigrated, (int?)null));
    }

    [Fact]
    public void Classify_RolledBackRecord_IsNotMigrated()
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.NotMigrated, blocksMigrated: 3));

        // Assert
        result.Should().Be((PendingMigrationReason.NotMigrated, (int?)null), because: "a rolled back file runs from its first block");
    }

    [Fact]
    public void Classify_FailedWithoutCommittedBlock_IsFailed()
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.Failed));

        // Assert
        result.Should().Be((PendingMigrationReason.Failed, (int?)null));
    }

    [Fact]
    public void Classify_FailedAfterCommittedBlocks_IsResumableAfterThem()
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.Failed, blocksMigrated: 2));

        // Assert
        result.Should().Be((PendingMigrationReason.Resumable, (int?)2), because: "migrate-up skips the two committed blocks like FindResumableBlock");
    }

    [Theory]
    [InlineData("changed", null)]
    [InlineData("blocks", "rollback-hash")]
    public void Classify_FailedAfterCommittedBlocksButChangedOrRolledBack_IsFailed(string recordBlocksHash, string? fileDownHash)
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.Failed, blocksMigrated: 2, blocksHash: recordBlocksHash, fileDownHash: fileDownHash));

        // Assert
        result.Should().Be((PendingMigrationReason.Failed, (int?)null),
            because: "migrate-up resumes neither a file whose blocks changed nor a record a rollback touched");
    }

    [Fact]
    public void Classify_FailedWithEveryBlockCounted_IsFailed()
    {
        // Act: every block committed, but the record ended Failed (the final status update failed); there is no block
        // left to resume at, and migrate-up finalizes only Executing records, so it runs the file again (#11)
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.Failed, blocksMigrated: 3));

        // Assert
        result.Should().Be((PendingMigrationReason.Failed, (int?)null));
    }

    [Fact]
    public void Classify_ExecutingWithEveryBlockOfAnEndedRun_IsResumableAfterTheLastBlock()
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.Executing, blocksMigrated: 3), openRunId: OpenRunId);

        // Assert
        result.Should().Be((PendingMigrationReason.Resumable, (int?)3),
            because: "migrate-up finalizes the record without executing a block, and its FileStartedEvent reports the block count");
    }

    [Fact]
    public void Classify_ExecutingAfterCommittedBlocksOfAnEndedRun_IsResumableAfterThem()
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.Executing, blocksMigrated: 1));

        // Assert
        result.Should().Be((PendingMigrationReason.Resumable, (int?)1));
    }

    [Fact]
    public void Classify_ExecutingWithoutCommittedBlockOfAnEndedRun_IsFailed()
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.Executing));

        // Assert
        result.Should().Be((PendingMigrationReason.Failed, (int?)null), because: "the attempt stopped without a final status and left nothing to resume");
    }

    [Theory]
    [InlineData(MigrationStatus.Executing, 1)]
    [InlineData(MigrationStatus.Executing, 3)]
    [InlineData(MigrationStatus.Pending, 0)]
    public void Classify_OpenRecordOfTheOpenRun_IsNotMigrated(MigrationStatus status, int blocksMigrated)
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(status, blocksMigrated: blocksMigrated, runId: OpenRunId), openRunId: OpenRunId);

        // Assert
        result.Should().Be((PendingMigrationReason.NotMigrated, (int?)null),
            because: "the orphan repair of the next migrate-up marks the open records of the run NotMigrated, and the file runs from its first block");
    }

    [Fact]
    public void Classify_FailedRecordOfTheOpenRun_IsResumable()
    {
        // Act
        var result = Classify(ThreeBlockFile(), Record(MigrationStatus.Failed, blocksMigrated: 1, runId: OpenRunId), openRunId: OpenRunId);

        // Assert
        result.Should().Be((PendingMigrationReason.Resumable, (int?)1), because: "the orphan repair changes only Pending and Executing records");
    }

    [Fact]
    public void Classify_RunAlwaysFileWithMigratedRecord_IsRunAlways()
    {
        // Arrange
        var migrated = Record(MigrationStatus.Migrated, blocksMigrated: 3);

        // Act
        var result = Classify(ThreeBlockFile(runAlways: true), migrated);

        // Assert
        result.Should().Be((PendingMigrationReason.RunAlways, (int?)null));
    }

    [Fact]
    public void Classify_RunAlwaysFileAfterCommittedBlocks_IsResumable()
    {
        // Act
        var result = Classify(ThreeBlockFile(runAlways: true), Record(MigrationStatus.Failed, blocksMigrated: 1));

        // Assert
        result.Should().Be((PendingMigrationReason.Resumable, (int?)1), because: "migrate-up resumes a RunAlways file like any other");
    }

    [Fact]
    public void Classify_MigratedRecordWithChangedHash_IsHashMismatch()
    {
        // Arrange
        var migrated = Record(MigrationStatus.Migrated, blocksMigrated: 3, blocksHash: "old");

        // Act
        var result = Classify(ThreeBlockFile(), migrated, migratedRecord: migrated);

        // Assert
        result.Should().Be((PendingMigrationReason.HashMismatch, (int?)null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Classify_FailedInTheMiddleOfItsRollback_IsRollbackIncomplete(bool runAlways)
    {
        // Arrange: a cancelled migrate-down ran one of three rollback blocks
        var record = Record(MigrationStatus.Failed, blocksMigrated: 3, fileDownHash: "rollback-hash");
        record.FileDownBlocksMigrated = 1;
        record.FileDownBlocksTotal = 3;

        // Act
        var result = Classify(ThreeBlockFile(runAlways: runAlways), record);

        // Assert
        result.Should().Be((PendingMigrationReason.RollbackIncomplete, (int?)null),
            because: "migrate-up refuses to run the file, also a RunAlways file, until migrate-down completes the rollback");
    }

    [Fact]
    public void Classify_FailedWithoutRollbackProgress_IsFailed()
    {
        // Arrange: the first rollback block failed; nothing continues such a rollback, so migrate-up runs the file again
        var record = Record(MigrationStatus.Failed, blocksMigrated: 3, fileDownHash: "rollback-hash");
        record.FileDownBlocksMigrated = 0;
        record.FileDownBlocksTotal = 3;

        // Act
        var result = Classify(ThreeBlockFile(), record);

        // Assert
        result.Should().Be((PendingMigrationReason.Failed, (int?)null));
    }
}
