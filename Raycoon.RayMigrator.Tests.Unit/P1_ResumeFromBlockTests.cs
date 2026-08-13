using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Resume-from-block tests.
/// Validates that FindResumableBlock correctly identifies whether a file+target
/// combination can resume from a previous partial execution, and returns the correct
/// 0-based block index to start from.
/// </summary>
public class ResumeFromBlockTests
{
    private static int InvokeFindResumableBlock(
        MigrationFileInfo file, string targetAlias, List<MigrationRecord> existingRecords)
    {
        var service = TestFactories.CreateUninitializedMigrationService();

        var method = typeof(MigrationService).GetMethod("FindResumableBlock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        return (int)method!.Invoke(service, new object[] { file, targetAlias, existingRecords })!;
    }

    private static (int Result, CapturingLogger<MigrationService> Logger) InvokeFindResumableBlockWithLogger(
        MigrationFileInfo file, string targetAlias, List<MigrationRecord> existingRecords)
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var method = typeof(MigrationService).GetMethod("FindResumableBlock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        var result = (int)method!.Invoke(service, new object[] { file, targetAlias, existingRecords })!;
        return (result, logger);
    }

    [Fact]
    public void NoExistingRecord_ReturnsZero()
    {
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>();

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void MigratedRecord_ReturnsZero()
    {
        // A fully migrated record should not trigger resume
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                status: MigrationStatus.Migrated,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void FailedWithPartialBlocks_ReturnsBlockCount()
    {
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(2); // Resume from block index 2 (0-based), i.e. block 3 of 5
    }

    [Fact]
    public void ExecutingWithPartialBlocks_ReturnsBlockCount()
    {
        // Executing status (process crash) should also trigger resume
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 3,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(3); // Resume from block index 3 (0-based), i.e. block 4 of 5
    }

    [Fact]
    public void FailedWithHashMismatch_ReturnsZero()
    {
        // File has been modified since partial execution - no resume
        var file = TestFactories.CreateMigrationFile(blocksHash: "new_hash");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Failed,
                blocksHash: "old_hash",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5)
        };

        var (result, logger) = InvokeFindResumableBlockWithLogger(file, "MainDB", records);

        result.Should().Be(0);
        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("hash mismatch"));
    }

    [Fact]
    public void FailedWithRollbackAttempted_ReturnsZero()
    {
        // FileDownHash is set => rollback was attempted => DB state is unclear => no resume
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5,
                fileDownHash: "rollback_hash_xyz")
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void FailedWithAllBlocksComplete_ReturnsZero()
    {
        // All blocks migrated (BlocksMigrated == BlocksTotal) => not a partial execution
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void FailedWithZeroBlocksMigrated_ReturnsZero()
    {
        // No blocks migrated => nothing to skip
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 0,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void MultipleFailedRecords_UsesNewest()
    {
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 1,
                fileUpBlocksTotal: 5),
            TestFactories.CreateMigrationRecord(
                id: 5,
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 3,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(3); // Uses record with Id=5 (newest), which has 3 blocks migrated
    }

    [Fact]
    public void DifferentTargetAlias_ReturnsZero()
    {
        // Record is for a different target alias => no match
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Failed,
                targetAlias: "OtherDB",
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void DifferentFilename_ReturnsZero()
    {
        var file = TestFactories.CreateMigrationFile(filename: "20_NewFile.sql", blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                filename: "10_Create.sql",
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void DifferentRelease_ReturnsZero()
    {
        var file = TestFactories.CreateMigrationFile(release: "Release 2.0", blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                release: "Release 1.0",
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void ResumeLogsInformationMessage()
    {
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5)
        };

        var (result, logger) = InvokeFindResumableBlockWithLogger(file, "MainDB", records);

        result.Should().Be(2);
        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Information &&
            e.Message.Contains("Resuming") &&
            e.Message.Contains("block 3/5"));
    }

    [Fact]
    public void NotMigratedStatus_ReturnsZero()
    {
        // NotMigrated (rollback was successful) => no resume needed
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.NotMigrated,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }

    [Fact]
    public void PendingStatus_ReturnsZero()
    {
        // Pending status is not Failed or Executing => no resume
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 1,
                status: MigrationStatus.Pending,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 2,
                fileUpBlocksTotal: 5)
        };

        var result = InvokeFindResumableBlock(file, "MainDB", records);

        result.Should().Be(0);
    }
}
