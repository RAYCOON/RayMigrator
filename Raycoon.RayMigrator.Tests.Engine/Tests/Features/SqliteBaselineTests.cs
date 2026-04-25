
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteBaselineTests : SqliteTestBase
{
    public SqliteBaselineTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Baseline_ToR2_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var result = await ctx.BaselineAsync("Release_2.0");
        result.Success.Should().BeTrue($"Baseline should succeed: {result.ErrorMessage}");
        result.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files as baselined");
    }

    [Fact]
    public async Task Baseline_ShouldNotCreateUserTables()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
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

    [Fact]
    public async Task Baseline_ShouldCreateRepositoryRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        await ctx.BaselineAsync("Release_2.0");
        ctx.AssertProductExists(true);
        ctx.CountMigrations().Should().BeGreaterThan(0, "Migration records should be created by baseline");
        ctx.AssertRunCount(1);
    }

    [Fact]
    public async Task BaselineThenMigrateUp_NothingNew()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var baselineResult = await ctx.BaselineAsync();
        baselineResult.Success.Should().BeTrue($"Baseline failed: {baselineResult.ErrorMessage}");
        baselineResult.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files");
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var migrateResult = await ctx.MigrateUpAsync();
        migrateResult.Success.Should().BeTrue($"MigrateUp after baseline failed: {migrateResult.ErrorMessage}");
        migrateResult.TotalMigrations.Should().Be(0, "No migrations should be needed after full baseline");
    }

    [Fact]
    public async Task BaselineThenMigrateUp_MultipleMigrationRuns()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var baselineResult = await ctx.BaselineAsync("Release_2.0");
        baselineResult.Success.Should().BeTrue($"Baseline failed: {baselineResult.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var migrateResult = await ctx.MigrateUpAsync();
        migrateResult.Success.Should().BeTrue($"MigrateUp failed: {migrateResult.ErrorMessage}");
        ctx.AssertRunCount(2);
        ctx.AssertFileStatuses(
            ("01_CreateTableE.sql", MigrationStatus.Migrated),
            ("02_CreateTableF.sql", MigrationStatus.Migrated),
            ("03_SeedDataC.sql", MigrationStatus.Migrated),
            ("01_CreateTableG.sql", MigrationStatus.Migrated),
            ("02_CreateTableH.sql", MigrationStatus.Migrated),
            ("03_SeedDataD.sql", MigrationStatus.Migrated)
        );
        ctx.AssertTableExists("tablee", true);
        ctx.AssertTableExists("tablef", true);
        ctx.AssertTableExists("tableg", true);
        ctx.AssertTableExists("tableh", true);
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
    }

    [Fact]
    public async Task Baseline_IdempotentSecondRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var result1 = await ctx.BaselineAsync("Release_2.0");
        result1.Success.Should().BeTrue($"First baseline failed: {result1.ErrorMessage}");
        result1.BaselinedFiles.Should().BeGreaterThan(0, "First baseline should mark files");
        await ctx.RebuildForAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate, "Release_2.0");
        var result2 = await ctx.BaselineAsync("Release_2.0");
        result2.Success.Should().BeTrue($"Second baseline failed: {result2.ErrorMessage}");
        result2.BaselinedFiles.Should().Be(0, "Second baseline should find nothing new to baseline");
    }

    [Fact]
    public async Task Baseline_AllReleases()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var result = await ctx.BaselineAsync();
        result.Success.Should().BeTrue($"Baseline all releases should succeed: {result.ErrorMessage}");
        result.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files as baselined");
    }

    [Fact]
    public async Task Baseline_AllReleases_MoreThanPartial()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx1 = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var partialResult = await ctx1.BaselineAsync("Release_1.0");
        partialResult.Success.Should().BeTrue($"Partial baseline failed: {partialResult.ErrorMessage}");
        int partialCount = partialResult.BaselinedFiles;
        await using var ctx2 = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var allResult = await ctx2.BaselineAsync();
        allResult.Success.Should().BeTrue($"Full baseline failed: {allResult.ErrorMessage}");
        allResult.BaselinedFiles.Should().BeGreaterThan(partialCount,
            "Baseline of all releases should cover more files than Release_1.0 only");
    }

    [Fact]
    public async Task BaselineAll_ThenMigrateUp_NothingNew()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var baselineResult = await ctx.BaselineAsync();
        baselineResult.Success.Should().BeTrue($"Baseline failed: {baselineResult.ErrorMessage}");
        baselineResult.BaselinedFiles.Should().BeGreaterThan(0, "Baseline should mark files");
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var migrateResult = await ctx.MigrateUpAsync();
        migrateResult.Success.Should().BeTrue($"MigrateUp after full baseline failed: {migrateResult.ErrorMessage}");
        migrateResult.TotalMigrations.Should().Be(0, "No migrations should be needed after full baseline");
    }

    [Fact]
    public async Task Baseline_ThenMigrateDown_ShouldFailBecauseTablesNeverCreated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline);
        var baselineResult = await ctx.BaselineAsync();
        baselineResult.Success.Should().BeTrue($"Baseline failed: {baselineResult.ErrorMessage}");
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        var downResult = await ctx.MigrateDownAsync("Release_2.0");
        downResult.Success.Should().BeFalse("MigrateDown after Baseline should fail -- tables never created");
    }
}
