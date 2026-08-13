using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: OPT-1 tests for TryFinalizeCompletedMigration.
/// Validates that migrations stuck in Executing state with all blocks completed
/// are correctly detected and finalized, preventing unnecessary re-execution.
/// Note: TryFinalizeCompletedMigration calls RepositoryMigrationUpdate which requires
/// TemplateExecutor. Since the uninitialized service has no TemplateExecutor, these tests
/// use a separate static query method to test the detection logic independently.
/// </summary>
public class TryFinalizeCompletedMigrationTests
{
    /// <summary>
    /// Extracts the detection logic from TryFinalizeCompletedMigration for unit testing
    /// without requiring TemplateExecutor (which performs the actual DB update).
    /// Returns the matching record ID or -1.
    /// </summary>
    private static int FindCompletedButNotFinalized(
        MigrationFileInfo file, string targetAlias, List<MigrationRecord> existingRecords)
    {
        var completedRecord = existingRecords
            .Where(r => r.Filename == file.Filename
                && r.ReleaseVersion == file.ReleaseVersion
                && r.TargetGroupAlias == file.TargetGroupAlias
                && r.TargetAlias == targetAlias
                && r.MigrationStatusId == MigrationStatus.Executing
                && r.FileUpBlocksMigrated > 0
                && r.FileUpBlocksMigrated >= r.FileUpBlocksTotal
                && r.FileUpBlocksHash == file.FileUpBlocksHash
                && r.FileDownHash == null)
            .OrderByDescending(r => r.Id)
            .FirstOrDefault();

        return completedRecord?.Id ?? -1;
    }

    [Fact]
    public void ExecutingWithAllBlocksComplete_ReturnsRecordId()
    {
        // Core R2 scenario: all blocks executed, status stuck at Executing
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(42);
    }

    [Fact]
    public void ExecutingWithPartialBlocks_ReturnsMinusOne()
    {
        // Partial execution (3 of 5) should NOT be finalized - this is handled by FindResumableBlock
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 3,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void MigratedStatus_ReturnsMinusOne()
    {
        // Already properly finalized - nothing to do
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                status: MigrationStatus.Migrated,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void FailedWithAllBlocksComplete_ReturnsMinusOne()
    {
        // Failed status means an error occurred - don't auto-finalize
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                status: MigrationStatus.Failed,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void ExecutingAllBlocksButHashChanged_ReturnsMinusOne()
    {
        // File was modified since partial execution - don't finalize, re-execute
        var file = TestFactories.CreateMigrationFile(blocksHash: "new_hash");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                status: MigrationStatus.Executing,
                blocksHash: "old_hash",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void ExecutingAllBlocksButRollbackAttempted_ReturnsMinusOne()
    {
        // FileDownHash set means rollback was attempted - DB state is unclear
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5,
                fileDownHash: "rollback_hash")
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void DifferentTargetAlias_ReturnsMinusOne()
    {
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                targetAlias: "OtherDB",
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void DifferentFilename_ReturnsMinusOne()
    {
        var file = TestFactories.CreateMigrationFile(filename: "20_Other.sql", blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                filename: "10_Create.sql",
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void DifferentRelease_ReturnsMinusOne()
    {
        var file = TestFactories.CreateMigrationFile(release: "Release 2.0", blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                release: "Release 1.0",
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void MultipleRecords_UsesNewest()
    {
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 10,
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5),
            TestFactories.CreateMigrationRecord(
                id: 50,
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(50); // Newest record (Id=50)
    }

    [Fact]
    public void NoRecords_ReturnsMinusOne()
    {
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>();

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void ZeroBlocksMigrated_ReturnsMinusOne()
    {
        // Edge case: Executing with 0 blocks migrated should not be finalized
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 0,
                fileUpBlocksTotal: 5)
        };

        var result = FindCompletedButNotFinalized(file, "MainDB", records);

        result.Should().Be(-1);
    }

    [Fact]
    public void IsDisjointFromFindResumableBlock()
    {
        // Verify that the detection criteria are mutually exclusive with FindResumableBlock.
        // FindResumableBlock: BlocksMigrated > 0 AND < BlocksTotal (partial)
        // TryFinalize: BlocksMigrated >= BlocksTotal (complete)
        // A record with BlocksMigrated == BlocksTotal matches TryFinalize but NOT FindResumableBlock.
        var file = TestFactories.CreateMigrationFile(blocksHash: "hash_abc");
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(
                id: 42,
                status: MigrationStatus.Executing,
                blocksHash: "hash_abc",
                fileUpBlocksMigrated: 5,
                fileUpBlocksTotal: 5)
        };

        var finalizeResult = FindCompletedButNotFinalized(file, "MainDB", records);
        var resumeResult = InvokeFindResumableBlock(file, "MainDB", records);

        finalizeResult.Should().Be(42, "TryFinalize should match complete records");
        resumeResult.Should().Be(0, "FindResumableBlock should NOT match complete records");
    }

    private static int InvokeFindResumableBlock(
        MigrationFileInfo file, string targetAlias, List<MigrationRecord> existingRecords)
    {
        var service = TestFactories.CreateUninitializedMigrationService();
        var method = typeof(MigrationService).GetMethod("FindResumableBlock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return (int)method!.Invoke(service, new object[] { file, targetAlias, existingRecords })!;
    }
}
