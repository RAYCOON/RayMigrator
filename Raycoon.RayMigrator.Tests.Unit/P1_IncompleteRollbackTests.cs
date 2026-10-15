using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: migrate-up refuses to run a file whose rollback stopped in the middle, a record Failed, or Executing in a run that
/// has ended, with some but not all of its rollback blocks (<see cref="MigrationService.FindIncompleteRollback"/>):
/// migrate-down continues that rollback, and running the file again from block 1 would act on a target that kept part of
/// it. The message names each such file.
/// </summary>
public class IncompleteRollbackTests
{
    private static MigrationRecord Record(MigrationStatus status, int? rollbackBlocksDone, int? rollbackBlocksTotal, string target = "MainDB", int id = 1)
        => TestFactories.CreateMigrationRecord(id: id, status: status, targetAlias: target, fileUpBlocksMigrated: 3, fileUpBlocksTotal: 3,
            fileDownHash: rollbackBlocksTotal == null ? null : "rollback-hash",
            fileDownBlocksMigrated: rollbackBlocksDone, fileDownBlocksTotal: rollbackBlocksTotal);

    [Theory]
    [InlineData(MigrationStatus.Failed)]
    [InlineData(MigrationStatus.Executing)]
    public void FindIncompleteRollback_PartOfItsRollbackBlocks_ReturnsTheRecord(MigrationStatus status)
    {
        // Arrange: one of three rollback blocks done, as a cancelled migrate-down leaves it (Failed), or a run that ended
        // without the final write of the record: a failed write, or an end between the two steps of the orphan repair,
        // which closed the run and left its records open (Executing)
        var record = Record(status, 1, 3);

        // Act
        var found = MigrationService.FindIncompleteRollback(TestFactories.CreateMigrationFile(), "MainDB", [record]);

        // Assert
        found.Should().BeSameAs(record, because: "migrate-down continues the rollback with block 2, whatever status its last write left");
    }

    [Theory]
    [InlineData(MigrationStatus.Failed, null, null)]
    [InlineData(MigrationStatus.Failed, 0, 3)]
    [InlineData(MigrationStatus.Failed, 3, 3)]
    [InlineData(MigrationStatus.Executing, 0, 3)]
    [InlineData(MigrationStatus.NotMigrated, 3, 3)]
    [InlineData(MigrationStatus.Migrated, null, null)]
    public void FindIncompleteRollback_NoRollbackInTheMiddle_ReturnsNull(MigrationStatus status, int? rollbackBlocksDone, int? rollbackBlocksTotal)
    {
        // Arrange: no rollback, a failed first rollback block (nothing continues it), a rollback with every block counted
        // (RollbackErrorAction=Ignore), a rollback that ended before its first rollback block, a completed rollback, a migration
        var record = Record(status, rollbackBlocksDone, rollbackBlocksTotal);

        // Act
        var found = MigrationService.FindIncompleteRollback(TestFactories.CreateMigrationFile(), "MainDB", [record]);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public void FindIncompleteRollback_RecordOfAnotherTarget_ReturnsNull()
    {
        // Act
        var found = MigrationService.FindIncompleteRollback(TestFactories.CreateMigrationFile(), "MainDB", [Record(MigrationStatus.Failed, 1, 3, target: "SecondDB")]);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public void FindIncompleteRollbacks_ChecksTheTargetsTheFileIsPendingOn()
    {
        // Arrange: the file is pending on SecondDB only; both targets have a rollback in the middle
        var product = new ProductOptions
        {
            Alias = "P",
            TargetGroups = [new TargetGroupOptions { Alias = "Backend", Targets = [new TargetOptions { Alias = "MainDB" }, new TargetOptions { Alias = "SecondDB" }] }]
        };
        var file = TestFactories.CreateMigrationFile();
        file.PendingTargetAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SecondDB" };
        var onMain = Record(MigrationStatus.Failed, 1, 3, target: "MainDB", id: 1);
        var onSecond = Record(MigrationStatus.Failed, 2, 3, target: "SecondDB", id: 2);

        // Act
        var found = MigrationService.FindIncompleteRollbacks([file], [onMain, onSecond], product);

        // Assert
        found.Should().Equal([onSecond], because: "migrate-up runs the file on SecondDB only");
    }

    [Fact]
    public void IncompleteRollbackMessage_NamesReleaseFileTargetProgressAndTheNextBlock()
    {
        // Arrange
        var record = Record(MigrationStatus.Failed, 1, 3);

        // Act
        string message = MigrationService.IncompleteRollbackMessage([record]);

        // Assert
        message.Should().Be(
            "Incomplete rollback: 1 file(s) stopped in the middle of their rollback, and migrate-up runs no file again before its rollback " +
            "has completed: [Release 1.0/10_Create.sql on target MainDB: 1 of 3 rollback blocks, migrate-down continues at block 2]. " +
            "Complete the rollback with migrate-down to a release before theirs, then run migrate-up.");
    }
}
