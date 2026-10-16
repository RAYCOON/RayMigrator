using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the records migrate-down rolls back when their release is after the target release
/// (<see cref="MigrationService.IsRolledBackByMigrateDown"/>): a Migrated record, a file that stopped after committed
/// blocks without a rollback attempt (Failed, or Executing in a run that has ended), which the next migrate-up would
/// resume after those blocks, and a rollback that stopped in the middle, which it continues. A file that stopped without a
/// committed block and without a rollback attempt is not rolled back, and migrate-down names it in a warning
/// (<see cref="MigrationService.IsStoppedWithoutCommittedBlock"/>).
/// </summary>
public class MigrateDownSelectionTests
{
    /// <summary>A record of a file with three blocks; a record with a rollback total has a rollback attempt (<c>FileDownHash</c>).</summary>
    private static MigrationRecord Record(MigrationStatus status, int blocksMigrated, int? rollbackBlocksMigrated = null, int? rollbackBlocksTotal = null)
        => TestFactories.CreateMigrationRecord(status: status, fileUpBlocksMigrated: blocksMigrated, fileUpBlocksTotal: 3,
            fileDownHash: rollbackBlocksTotal == null ? null : "rollback-hash",
            fileDownBlocksMigrated: rollbackBlocksMigrated, fileDownBlocksTotal: rollbackBlocksTotal);

    [Theory]
    [InlineData(MigrationStatus.Migrated, 3, null, null)]
    [InlineData(MigrationStatus.Failed, 1, null, null)]
    [InlineData(MigrationStatus.Failed, 3, null, null)]
    [InlineData(MigrationStatus.Executing, 1, null, null)]
    [InlineData(MigrationStatus.Executing, 3, null, null)]
    [InlineData(MigrationStatus.Failed, 3, 1, 3)]
    [InlineData(MigrationStatus.Executing, 1, 2, 3)]
    public void IsRolledBackByMigrateDown_CommittedBlocksOrARollbackInTheMiddle_IsTrue(
        MigrationStatus status, int blocksMigrated, int? rollbackBlocksMigrated, int? rollbackBlocksTotal)
    {
        // Arrange: a migrated file; a file that stopped after 1 or every block (a failure, a cancel, ignored failed blocks,
        // the orphan repair, a failed final write), Failed, or Executing in a run that has ended; a rollback that stopped
        // after 1 or 2 of 3 rollback blocks
        var record = Record(status, blocksMigrated, rollbackBlocksMigrated, rollbackBlocksTotal);

        // Act
        bool rolledBack = MigrationService.IsRolledBackByMigrateDown(record);

        // Assert
        rolledBack.Should().BeTrue(because: "the target holds what committed blocks of the file wrote, and the next migrate-up would resume after them");
    }

    [Theory]
    [InlineData(MigrationStatus.Failed, 0, null, null)]
    [InlineData(MigrationStatus.Executing, 0, null, null)]
    [InlineData(MigrationStatus.Failed, 3, 0, 3)]
    [InlineData(MigrationStatus.Failed, 1, 0, 3)]
    [InlineData(MigrationStatus.Failed, 3, 3, 3)]
    [InlineData(MigrationStatus.NotMigrated, 3, 3, 3)]
    [InlineData(MigrationStatus.NotMigrated, 1, null, null)]
    [InlineData(MigrationStatus.NotMigrated, 0, null, null)]
    [InlineData(MigrationStatus.Pending, 0, null, null)]
    public void IsRolledBackByMigrateDown_NoCommittedBlockOrARollbackThatRan_IsFalse(
        MigrationStatus status, int blocksMigrated, int? rollbackBlocksMigrated, int? rollbackBlocksTotal)
    {
        // Arrange: a file that stopped without a committed block, Failed or Executing; a rollback whose first rollback block
        // failed, of a migrated file and of a file that stopped after a committed block; a rollback that ran to its end with
        // ignored failed blocks; a completed rollback; a record that fix set to NotMigrated with its counters; a record
        // without a run of its file; a record that was inserted and nothing more
        var record = Record(status, blocksMigrated, rollbackBlocksMigrated, rollbackBlocksTotal);

        // Act
        bool rolledBack = MigrationService.IsRolledBackByMigrateDown(record);

        // Assert
        rolledBack.Should().BeFalse(because: "the record counts no committed block to roll back, or its rollback ran without a rollback block to continue after");
    }

    [Theory]
    [InlineData(MigrationStatus.Failed)]
    [InlineData(MigrationStatus.Executing)]
    public void IsStoppedWithoutCommittedBlock_FailedOrExecutingWithoutABlockOrARollback_IsTrue(MigrationStatus status)
    {
        // Arrange: the atomic path, a CLI tool or a failed first block leave the record Failed without a committed block,
        // a failed final write Executing in a run that has ended
        var record = Record(status, 0);

        // Act
        bool stopped = MigrationService.IsStoppedWithoutCommittedBlock(record);

        // Assert
        stopped.Should().BeTrue(because: "migrate-down does not roll the file back and names it, because with MigrationErrorAction=Ignore or a CLI tool blocks of it may have run");
        MigrationService.IsRolledBackByMigrateDown(record).Should().BeFalse();
    }

    [Theory]
    [InlineData(MigrationStatus.Failed, 1, null, null)]
    [InlineData(MigrationStatus.Executing, 3, null, null)]
    [InlineData(MigrationStatus.Migrated, 3, null, null)]
    [InlineData(MigrationStatus.Failed, 0, 0, 3)]
    [InlineData(MigrationStatus.NotMigrated, 0, null, null)]
    [InlineData(MigrationStatus.Pending, 0, null, null)]
    public void IsStoppedWithoutCommittedBlock_CommittedBlocksARollbackOrNoAttempt_IsFalse(
        MigrationStatus status, int blocksMigrated, int? rollbackBlocksMigrated, int? rollbackBlocksTotal)
    {
        // Arrange: a file that stopped after committed blocks, which migrate-down rolls back; a migrated file; a file that
        // stopped without a committed block and whose rollback ran; a record without a run of its file; a record that was
        // inserted and nothing more
        var record = Record(status, blocksMigrated, rollbackBlocksMigrated, rollbackBlocksTotal);

        // Act
        bool stopped = MigrationService.IsStoppedWithoutCommittedBlock(record);

        // Assert
        stopped.Should().BeFalse();
    }

    [Theory]
    [InlineData(MigrationStatus.Migrated, 3, null, null,
        "10_Create.sql keeps its status, and migrate-down rolls it back once the file is there")]
    [InlineData(MigrationStatus.Failed, 3, 1, 3,
        "10_Create.sql keeps its status, and migrate-down rolls it back once the file is there")]
    [InlineData(MigrationStatus.Failed, 2, null, null,
        "10_Create.sql keeps its status with its 2 committed block(s), and migrate-down rolls it back once the file is there")]
    [InlineData(MigrationStatus.Failed, 0, null, null,
        "10_Create.sql keeps its status; its record counts no committed block, so migrate-down does not roll it back, " +
        "and the next migrate-up runs it from its first block")]
    public void KeptStatusWithoutRequiredRollbackFile_EachRecordInAChain_SaysWhatMigrateDownAndMigrateUpDoWithIt(
        MigrationStatus status, int blocksMigrated, int? rollbackBlocksMigrated, int? rollbackBlocksTotal, string expected)
    {
        // Arrange: a migrated file, a rollback that stopped in the middle, a file that stopped after 2 committed blocks and
        // one that stopped without a committed block, which only the rollback of a stopped migrate-up reaches
        var record = Record(status, blocksMigrated, rollbackBlocksMigrated, rollbackBlocksTotal);

        // Act
        string outcome = MigrationService.KeptStatusWithoutRequiredRollbackFile(record);

        // Assert
        outcome.Should().Be(expected, because: "the record keeps its status, and the message says what the next command does with it");
    }
}
