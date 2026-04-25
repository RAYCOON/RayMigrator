
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class MigrationHistoryTrackingTests : PostgreSqlTestBase
{
    public MigrationHistoryTrackingTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// A1: Simulate mode is side-effect-free — no repository records or history entries.
    /// </summary>
    [Fact]
    public async Task Simulate_DoesNotWriteRepositoryOrHistory()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);

        // Run 1: Simulate mode — should succeed without writing anything
        var result1 = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);
        result1.Success.Should().BeTrue($"Simulate Run 1 failed: {result1.ErrorMessage}");

        // Repository tables should NOT exist (Simulate is side-effect-free)
        ctx.AssertRepositoryTableExists("MigrationRecord", false);
        ctx.AssertRepositoryTableExists("MigrationRun", false);
        ctx.AssertRepositoryTableExists("MigrationRecordHistory", false);
        ctx.AssertRepositoryTableExists("Product", false);
    }

    /// <summary>
    /// A2: MigrateUp produces inline MigrationHistory entries from the first run.
    /// Run 1: MigrateUp R1+R2 (history created immediately).
    /// Run 2: MigrateUp R3+R4 (additional history entries).
    /// </summary>
    [Fact]
    public async Task FirstRun_CreatesInlineHistory()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Run 1: MigrateUp R1+R2
        var result1 = await ctx.MigrateUpAsync("Release_2.0");
        result1.Success.Should().BeTrue($"Run 1 failed: {result1.ErrorMessage}");
        ctx.CountMigrations().Should().BeGreaterThan(0, "Run 1 should create Migration records");
        ctx.CountMigrationHistory().Should().BeGreaterThan(0,
            "Inline historization creates history entries from first run");

        var historyAfterRun1 = ctx.CountMigrationHistory();

        // Run 2: MigrateUp all remaining (R3+R4)
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();

        ctx.CountMigrationHistory().Should().BeGreaterThan(historyAfterRun1,
            "Second run should add more history entries");
        ctx.CountMigrationRuns().Should().BeGreaterThanOrEqualTo(2,
            "Should have at least 2 MigrationRuns");
    }

    /// <summary>
    /// A3: After a failed MigrateUp and a successful retry, there should be no duplicate
    /// Migration records per file (the one-record-per-file invariant).
    /// History should contain Failed + Migrated entries for the retried file.
    /// </summary>
    [Fact]
    public async Task FailedRetry_NoDuplicateRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        // Phase 1: MigrateUp R1+R2 (error file in R3 not reached yet)
        await using var ctx = await CreateScenario()
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BuildAsync();

        var result1 = await ctx.MigrateUpAsync("Release_2.0");
        result1.Success.Should().BeTrue($"Phase 1 failed: {result1.ErrorMessage}");

        // Phase 2: MigrateUp all -- hits error in R3/F3, terminates
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var result2 = await ctx.MigrateUpAsync();
        result2.Success.Should().BeFalse("Phase 2 should fail due to injected error in R3");

        // Phase 3: Restore original R3/F3 file and retry
        string srcPath = Path.Combine(Fixture.EngineConfig.BaseFilesPath,
            "Release_3.0", "Backend", "03_SeedDataC.sql");
        string dstPath = Path.Combine(ctx.WorkDirectory,
            "Release_3.0", "Backend", "03_SeedDataC.sql");
        File.Copy(srcPath, dstPath, overwrite: true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var result3 = await ctx.MigrateUpAsync();
        result3.Success.Should().BeTrue($"Phase 3 retry failed: {result3.ErrorMessage}");

        // One record per file invariant: no duplicates in Migration table
        ctx.CountMigrationsByFilename("01_CreateTableA.sql").Should().Be(1,
            "Should have exactly 1 record per file, no duplicates");
        ctx.CountMigrationsByFilename("03_SeedDataC.sql").Should().Be(1,
            "Failed-then-retried file should have exactly 1 record, no duplicates");

        // History should contain multiple entries for the failed+retried file
        ctx.CountMigrationHistory().Should().BeGreaterThan(0,
            "History should contain entries for all terminal state changes");
    }

    /// <summary>
    /// A4: After MigrateUp-all, MigrateDown, then MigrateUp-all again, there should be
    /// no duplicate Migration records per file. History captures full lifecycle.
    /// </summary>
    [Fact]
    public async Task DownThenUp_NoDuplicateRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Run 1: MigrateUp all
        var upResult = await ctx.MigrateUpAsync();
        upResult.Success.Should().BeTrue($"MigrateUp failed: {upResult.ErrorMessage}");

        var historyAfterUp = ctx.CountMigrationHistory();
        historyAfterUp.Should().BeGreaterThan(0,
            "MigrateUp should create history entries (Migrated per file)");

        // Run 2: MigrateDown to R2
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        var downResult = await ctx.MigrateDownAsync("Release_2.0");
        downResult.Success.Should().BeTrue($"MigrateDown failed: {downResult.ErrorMessage}");

        ctx.CountMigrationsWithStatus((int)MigrationStatus.NotMigrated).Should().BeGreaterThan(0,
            "R3+R4 should be NotMigrated after rollback");
        ctx.CountMigrationsWithStatus((int)MigrationStatus.Migrated).Should().BeGreaterThan(0,
            "R1+R2 should still be Migrated");
        ctx.CountMigrationHistory().Should().BeGreaterThan(historyAfterUp,
            "MigrateDown should add NotMigrated history entries");

        // Run 3: Re-MigrateUp all
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var reUpResult = await ctx.MigrateUpAsync();
        reUpResult.Success.Should().BeTrue($"Re-MigrateUp failed: {reUpResult.ErrorMessage}");

        // One record per file invariant: no duplicates after re-execution
        ctx.CountMigrationsByFilename("01_CreateTableA.sql").Should().Be(1,
            "No duplicate records after MigrateDown+MigrateUp");
        ctx.CountMigrationsByFilename("01_CreateTableE.sql").Should().Be(1,
            "No duplicate records for re-migrated file after MigrateDown+MigrateUp");
        ctx.CountMigrationsByFilename("03_SeedDataC.sql").Should().Be(1,
            "No duplicate records for re-migrated seed file after MigrateDown+MigrateUp");
    }

    /// <summary>
    /// A5: Baseline produces inline MigrationHistory entries (1 per file: Migrated).
    /// Second baseline is idempotent and adds no new history.
    /// </summary>
    [Fact]
    public async Task Baseline_ProducesInlineHistory()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        // Run 1: Baseline all
        var result1 = await ctx.BaselineAsync();
        result1.Success.Should().BeTrue($"First baseline failed: {result1.ErrorMessage}");
        result1.BaselinedFiles.Should().BeGreaterThan(0, "First baseline should mark files");
        ctx.CountMigrationHistory().Should().BeGreaterThan(0,
            "Baseline should create history entries (Migrated per file)");

        var historyAfterBaseline = ctx.CountMigrationHistory();

        // Run 2: Baseline again (idempotent)
        await ctx.RebuildForAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate);
        var result2 = await ctx.BaselineAsync();
        result2.Success.Should().BeTrue($"Second baseline failed: {result2.ErrorMessage}");
        result2.BaselinedFiles.Should().Be(0, "Second baseline should find nothing new");

        ctx.CountMigrationHistory().Should().Be(historyAfterBaseline,
            "Idempotent second baseline should not add new history entries");
    }
}
