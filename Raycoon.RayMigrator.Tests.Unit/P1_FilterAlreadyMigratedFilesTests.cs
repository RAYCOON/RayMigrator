
using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-2: FilterAlreadyMigratedFiles tests.
/// Errors lead to migrations being executed twice or skipped.
/// Uses reflection to invoke the internal method without requiring full service construction.
/// </summary>
public class FilterAlreadyMigratedFilesTests
{
    private static ProductOptions CreateProductOptions(string hashValidationScope = "File", string targetGroupAlias = "Backend")
    {
        return new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new() { Alias = targetGroupAlias, HashValidationScope = hashValidationScope, DatabaseType = "SqlServer" }
            }
        };
    }

    private static List<MigrationFileInfo> InvokeFilter(
        List<MigrationFileInfo> files, List<MigrationRecord> records)
    {
        return InvokeFilterWithScope(files, records, "File");
    }

    private static List<MigrationFileInfo> InvokeFilterWithScope(
        List<MigrationFileInfo> files, List<MigrationRecord> records, string hashValidationScope)
    {
        var service = TestFactories.CreateUninitializedMigrationService();
        var productOptions = CreateProductOptions(hashValidationScope);

        var method = typeof(MigrationService).GetMethod("FilterAlreadyMigratedFiles",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        return (List<MigrationFileInfo>)method!.Invoke(service, new object[] { files, records, productOptions })!;
    }

    // === Existing tests (unchanged assertions) ===

    [Fact]
    public void AlreadyMigratedFile_IsFiltered()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile() };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord() };

        var result = InvokeFilter(files, records);

        result.Should().BeEmpty();
    }

    [Fact]
    public void NotMigratedFile_IsKept()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile(filename: "20_New.sql") };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord() };

        var result = InvokeFilter(files, records);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void RunAlways_NeverFiltered()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile(runAlways: true) };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord() };

        var result = InvokeFilter(files, records);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void HashChanged_FileIsReExecuted()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile(hash: "newhash") };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord(hash: "oldhash") };

        var result = InvokeFilter(files, records);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void MatchesByFilenameReleaseAndTargetGroup()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "10_Create.sql", release: "Release 1.0", targetGroup: "Backend")
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "10_Create.sql", release: "Release 2.0", targetGroup: "Backend")
        };

        var result = InvokeFilter(files, records);

        // Different release => no match => file is kept
        result.Should().HaveCount(1);
    }

    [Fact]
    public void RecordWithUnclearState_FileIsKept()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile() };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(status: Raycoon.RayMigrator.Core.Configuration.Enums.MigrationStatus.Failed)
        };

        var result = InvokeFilter(files, records);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void RecordWithErrorResult_FileIsKept()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile() };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(status: Raycoon.RayMigrator.Core.Configuration.Enums.MigrationStatus.Pending)
        };

        var result = InvokeFilter(files, records);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void MultipleRecords_ThreeMigrated_TwoRemaining()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "01_A.sql", hash: "h1"),
            TestFactories.CreateMigrationFile(filename: "02_B.sql", hash: "h2"),
            TestFactories.CreateMigrationFile(filename: "03_C.sql", hash: "h3"),
            TestFactories.CreateMigrationFile(filename: "04_D.sql", hash: "h4"),
            TestFactories.CreateMigrationFile(filename: "05_E.sql", hash: "h5"),
        };
        var records = new List<MigrationRecord>
        {
            TestFactories.CreateMigrationRecord(filename: "01_A.sql", hash: "h1"),
            TestFactories.CreateMigrationRecord(filename: "02_B.sql", hash: "h2"),
            TestFactories.CreateMigrationRecord(filename: "03_C.sql", hash: "h3"),
        };

        var result = InvokeFilter(files, records);

        result.Should().HaveCount(2);
        result.Select(f => f.Filename).Should().Contain("04_D.sql");
        result.Select(f => f.Filename).Should().Contain("05_E.sql");
    }

    [Fact]
    public void EmptyRecordList_AllFilesKept()
    {
        var files = new List<MigrationFileInfo>
        {
            TestFactories.CreateMigrationFile(filename: "01_A.sql"),
            TestFactories.CreateMigrationFile(filename: "02_B.sql"),
        };

        var result = InvokeFilter(files, new List<MigrationRecord>());

        result.Should().HaveCount(2);
    }

    [Fact]
    public void EmptyFileList_ReturnsEmptyList()
    {
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord() };

        var result = InvokeFilter(new List<MigrationFileInfo>(), records);

        result.Should().BeEmpty();
    }

    // === New scope-aware tests ===

    [Fact]
    public void SqlBlocksScope_BlocksHashMatch_FileHashMismatch_IsFiltered()
    {
        // CLI Tool case: TOML changed (FileUpHash differs) but SQL unchanged (BlocksHash matches)
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile(hash: "newhash", blocksHash: "sameblockhash") };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord(hash: "oldhash", blocksHash: "sameblockhash") };

        var result = InvokeFilterWithScope(files, records, "SqlBlocks");

        result.Should().BeEmpty("SqlBlocks scope compares FileUpBlocksHash, which matches");
    }

    [Fact]
    public void SqlBlocksScope_BlocksHashMismatch_IsReExecuted()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile(hash: "newhash", blocksHash: "newblocks") };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord(hash: "oldhash", blocksHash: "oldblocks") };

        var result = InvokeFilterWithScope(files, records, "SqlBlocks");

        result.Should().HaveCount(1, "SqlBlocks scope detects changed SQL content");
    }

    [Fact]
    public void DisabledScope_HashMismatch_IsFiltered()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile(hash: "newhash") };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord(hash: "oldhash") };

        var result = InvokeFilterWithScope(files, records, "Disabled");

        result.Should().BeEmpty("Disabled scope always treats hash as matching");
    }

    [Fact]
    public void DisabledScope_RunAlways_IsNotFiltered()
    {
        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile(runAlways: true) };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord() };

        var result = InvokeFilterWithScope(files, records, "Disabled");

        result.Should().HaveCount(1, "RunAlways overrides Disabled scope");
    }

    [Fact]
    public void UndefinedScope_FallsBackToFile_HashMismatch_IsReExecuted()
    {
        var service = TestFactories.CreateUninitializedMigrationService();
        // ProductOptions with no HashValidationScope set → Undefined → fallback to File
        var productOptions = new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new() { Alias = "Backend", DatabaseType = "SqlServer" } // No HashValidationScope set
            }
        };

        var files = new List<MigrationFileInfo> { TestFactories.CreateMigrationFile(hash: "newhash") };
        var records = new List<MigrationRecord> { TestFactories.CreateMigrationRecord(hash: "oldhash") };

        var method = typeof(MigrationService).GetMethod("FilterAlreadyMigratedFiles",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        var result = (List<MigrationFileInfo>)method!.Invoke(service, new object[] { files, records, productOptions })!;

        result.Should().HaveCount(1, "Undefined scope falls back to File → hash mismatch detected");
    }
}
