
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class BaselineTests : PostgreSqlTestBase
{
    public BaselineTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// B1: Baseline to Release_2.0 should succeed and mark files as baselined.
    /// </summary>
    [Fact]
    public async Task Baseline_ToR2_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        var result = await ctx.BaselineAsync("Release_2.0");

        result.Success.Should().BeTrue($"Baseline should succeed: {result.ErrorMessage}");
        result.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files as baselined");
    }

    /// <summary>
    /// B2: After baseline, no user tables should exist (SQL is not executed).
    /// </summary>
    [Fact]
    public async Task Baseline_ShouldNotCreateUserTables()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        await ctx.BaselineAsync("Release_2.0");

        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }

    /// <summary>
    /// B3: Baseline should create repository records (Product, Migration, MigrationRun).
    /// </summary>
    [Fact]
    public async Task Baseline_ShouldCreateRepositoryRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        await ctx.BaselineAsync("Release_2.0");

        ctx.AssertProductExists(true);
        ctx.CountMigrations().Should().BeGreaterThan(0, "Migration records should be created by baseline");
        ctx.AssertRunCount(1);
    }

    /// <summary>
    /// B4: Baseline all then MigrateUp should find nothing new to migrate.
    /// </summary>
    [Fact]
    public async Task BaselineThenMigrateUp_NothingNew()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        var baselineResult = await ctx.BaselineAsync();
        baselineResult.Success.Should().BeTrue($"Baseline failed: {baselineResult.ErrorMessage}");
        baselineResult.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files");

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var migrateResult = await ctx.MigrateUpAsync();

        migrateResult.Success.Should().BeTrue($"MigrateUp after baseline failed: {migrateResult.ErrorMessage}");
        migrateResult.TotalMigrations.Should().Be(0, "No migrations should be needed after full baseline");
    }

    /// <summary>
    /// B5: Baseline R1+R2 then MigrateUp all should produce 2 runs with R3+R4 newly migrated.
    /// </summary>
    [Fact]
    public async Task BaselineThenMigrateUp_MultipleMigrationRuns()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        // Baseline R1+R2
        var baselineResult = await ctx.BaselineAsync("Release_2.0");
        baselineResult.Success.Should().BeTrue($"Baseline failed: {baselineResult.ErrorMessage}");

        // MigrateUp all (should only migrate R3+R4)
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var migrateResult = await ctx.MigrateUpAsync();
        migrateResult.Success.Should().BeTrue($"MigrateUp failed: {migrateResult.ErrorMessage}");

        ctx.AssertRunCount(2);

        // R3+R4 files should be newly migrated
        ctx.AssertFileStatuses(
            ("01_CreateTableE.sql", MigrationStatus.Migrated),
            ("02_CreateTableF.sql", MigrationStatus.Migrated),
            ("03_SeedDataC.sql", MigrationStatus.Migrated),
            ("01_CreateTableG.sql", MigrationStatus.Migrated),
            ("02_CreateTableH.sql", MigrationStatus.Migrated),
            ("03_SeedDataD.sql", MigrationStatus.Migrated)
        );

        // R3+R4 tables should exist (actually executed)
        ctx.AssertTableExists("tablee", true);
        ctx.AssertTableExists("tablef", true);
        ctx.AssertTableExists("tableg", true);
        ctx.AssertTableExists("tableh", true);

        // R1+R2 tables should NOT exist (were baselined, not executed)
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
    }

    /// <summary>
    /// B6: Baseline is idempotent - second run should baseline 0 files.
    /// </summary>
    [Fact]
    public async Task Baseline_IdempotentSecondRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        // First baseline
        var result1 = await ctx.BaselineAsync("Release_2.0");
        result1.Success.Should().BeTrue($"First baseline failed: {result1.ErrorMessage}");
        result1.BaselinedFiles.Should().BeGreaterThan(0, "First baseline should mark files");

        // Second baseline (same scope)
        await ctx.RebuildForAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate, "Release_2.0");
        var result2 = await ctx.BaselineAsync("Release_2.0");

        result2.Success.Should().BeTrue($"Second baseline failed: {result2.ErrorMessage}");
        result2.BaselinedFiles.Should().Be(0, "Second baseline should find nothing new to baseline");
    }

    /// <summary>
    /// B7: Baseline without toRelease should baseline all releases successfully.
    /// </summary>
    [Fact]
    public async Task Baseline_AllReleases()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        var result = await ctx.BaselineAsync();

        result.Success.Should().BeTrue($"Baseline all releases should succeed: {result.ErrorMessage}");
        result.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files as baselined");
    }

    /// <summary>
    /// B8: Baseline all releases should cover more files than a partial baseline of R1 only.
    /// </summary>
    [Fact]
    public async Task Baseline_AllReleases_MoreThanPartial()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        // Baseline only R1
        await using var ctx1 = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        var partialResult = await ctx1.BaselineAsync("Release_1.0");
        partialResult.Success.Should().BeTrue($"Partial baseline failed: {partialResult.ErrorMessage}");
        int partialCount = partialResult.BaselinedFiles;

        // Baseline all releases (clean scenario)
        await using var ctx2 = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        var allResult = await ctx2.BaselineAsync();
        allResult.Success.Should().BeTrue($"Full baseline failed: {allResult.ErrorMessage}");

        allResult.BaselinedFiles.Should().BeGreaterThan(partialCount,
            "Baseline of all releases should cover more files than Release_1.0 only");
    }

    /// <summary>
    /// B9: Baseline all releases then MigrateUp should find nothing new.
    /// </summary>
    [Fact]
    public async Task BaselineAll_ThenMigrateUp_NothingNew()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        var baselineResult = await ctx.BaselineAsync();
        baselineResult.Success.Should().BeTrue($"Baseline failed: {baselineResult.ErrorMessage}");
        baselineResult.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files");

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var migrateResult = await ctx.MigrateUpAsync();

        migrateResult.Success.Should().BeTrue($"MigrateUp after full baseline failed: {migrateResult.ErrorMessage}");
        migrateResult.TotalMigrations.Should().Be(0, "No migrations should be needed after full baseline");
    }

    /// <summary>
    /// B10: MigrateDown after Baseline FAILS because Baseline marks files as Migrated
    /// without executing SQL — the rollback files (DELETE/DROP) fail because the
    /// database objects were never created.
    /// This documents the expected behavior: Baseline + MigrateDown is not a valid workflow.
    /// </summary>
    [Fact]
    public async Task Baseline_ThenMigrateDown_ShouldFailBecauseTablesNeverCreated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline);

        // Baseline all — marks files as Migrated without executing SQL
        var baselineResult = await ctx.BaselineAsync();
        baselineResult.Success.Should().BeTrue($"Baseline failed: {baselineResult.ErrorMessage}");

        // MigrateDown to Release_2.0 — should FAIL because rollback files
        // try to DROP/DELETE tables that were never created
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        var downResult = await ctx.MigrateDownAsync("Release_2.0");
        downResult.Success.Should().BeFalse("MigrateDown after Baseline should fail — tables never created");
    }
}
