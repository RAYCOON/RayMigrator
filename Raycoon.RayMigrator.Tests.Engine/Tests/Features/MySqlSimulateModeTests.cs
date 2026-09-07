using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlSimulateModeTests : MySqlTestBase
{
    public MySqlSimulateModeTests(MySqlFixture fixture) : base(fixture) { }

    [Fact] public async Task Simulate_ShouldSucceed() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate); var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate); result.Success.Should().BeTrue($"Simulate mode should succeed: {result.ErrorMessage}"); }

    [Fact] public async Task Simulate_ShouldNotCreateUserTables() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate); await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate); ctx.AssertTableExists("tablea", false); ctx.AssertTableExists("tableb", false); ctx.AssertTableExists("tablec", false); ctx.AssertTableExists("tabled", false); ctx.AssertTableExists("tablee", false); ctx.AssertTableExists("tablef", false); ctx.AssertTableExists("tableg", false); ctx.AssertTableExists("tableh", false); }

    /// <summary>
    /// S3: Simulate mode should NOT write repository records (side-effect-free).
    /// </summary>
    [Fact] public async Task Simulate_ShouldNotWriteRepositoryRecords() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate); var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate); result.Success.Should().BeTrue($"Simulate mode should succeed: {result.ErrorMessage}"); ctx.AssertRepositoryTableExists("MigrationRecord", false); ctx.AssertRepositoryTableExists("MigrationRun", false); ctx.AssertRepositoryTableExists("Product", false); }

    [Fact] public async Task Validate_ShouldNotWriteAnything() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Validate); var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Validate); result.Success.Should().BeTrue($"Validate mode should succeed: {result.ErrorMessage}"); ctx.AssertRepositoryTableExists("MigrationRecord", false); ctx.AssertRepositoryTableExists("MigrationRun", false); ctx.AssertRepositoryTableExists("Product", false); ctx.AssertTableExists("tablea", false); ctx.AssertTableExists("tableb", false); ctx.AssertTableExists("tablec", false); ctx.AssertTableExists("tabled", false); }

    [Fact] public async Task SimulateMigrateDown_ShouldNotRollback() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); ctx.AssertTableExists("tablea", true); ctx.AssertTableExists("tablec", true); ctx.AssertRowCount("tablea", 3); ctx.AssertRowCount("tablec", 3); await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Simulate, "Release_1.0"); var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Simulate); downResult.Success.Should().BeTrue($"Simulate MigrateDown should succeed: {downResult.ErrorMessage}"); ctx.AssertTableExists("tablea", true); ctx.AssertTableExists("tableb", true); ctx.AssertTableExists("tablec", true); ctx.AssertTableExists("tabled", true); ctx.AssertTableExists("tablee", true); ctx.AssertTableExists("tablef", true); ctx.AssertTableExists("tableg", true); ctx.AssertTableExists("tableh", true); ctx.AssertRowCount("tablea", 3); ctx.AssertRowCount("tablec", 3); ctx.AssertRowCount("tablee", 3); ctx.AssertRowCount("tableg", 3); }

    [Fact] public async Task ValidateMigrateDown_ShouldNotRollback() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); ctx.AssertTableExists("tablea", true); ctx.AssertTableExists("tablec", true); await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Validate, "Release_1.0"); var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Validate); downResult.Success.Should().BeTrue($"Validate MigrateDown should succeed: {downResult.ErrorMessage}"); ctx.AssertTableExists("tablea", true); ctx.AssertTableExists("tableb", true); ctx.AssertTableExists("tablec", true); ctx.AssertTableExists("tabled", true); ctx.AssertTableExists("tablee", true); ctx.AssertTableExists("tablef", true); ctx.AssertTableExists("tableg", true); ctx.AssertTableExists("tableh", true); ctx.AssertRowCount("tablea", 3); ctx.AssertRunCount(1); }

    [Fact] public async Task ValidateMigrateDown_MissingRollback_ShouldFail() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); string rollbackPath = Path.Combine(ctx.WorkDirectory, "Release_4.0", "Backend", "01_CreateTableG.rollback.sql"); File.Delete(rollbackPath); await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Validate, "Release_1.0"); var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Validate); downResult.Success.Should().BeFalse("Validate MigrateDown should fail when rollback file is missing"); ctx.AssertTableExists("tablea", true); ctx.AssertTableExists("tablec", true); ctx.AssertTableExists("tableg", true); }

    #region Simulate reads the same records as Migrate (#7)

    /// <summary>
    /// #7: Simulate skipped the CheckInsert templates that resolve ProductId/EnvironmentId and queried the
    /// repository with id 0, so it previewed every file as pending and never found rollback candidates.
    /// These tests migrate first and then simulate against the populated repository.
    /// </summary>
    [Fact]
    public async Task Simulate_AfterMigrate_PreviewsOnlyPendingFiles()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        int recordsBefore = ctx.CountMigrations();
        int runsBefore = ctx.CountMigrationRuns();

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);
        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        result.Success.Should().BeTrue($"Simulate should succeed: {result.ErrorMessage}");
        result.MigrationResults.Should().NotBeEmpty("Release_2.0 and later are still pending");
        result.MigrationResults.Should().NotContain(r => r.ReleaseVersion == "Release_1.0",
            "files that are already migrated must not be previewed as pending (#7)");
        result.MigrationResults.Should().Contain(r => r.ReleaseVersion == "Release_2.0");
        ctx.CountMigrations().Should().Be(recordsBefore, "Simulate must not write migration records");
        ctx.CountMigrationRuns().Should().Be(runsBefore, "Simulate must not create a MigrationRun");
    }

    [Fact]
    public async Task Simulate_AfterFullMigrate_PreviewsNothing()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);
        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        result.Success.Should().BeTrue($"Simulate should succeed: {result.ErrorMessage}");
        result.TotalMigrations.Should().Be(0, "everything is applied, so Simulate has nothing to preview (#7)");
        result.MigrationResults.Should().BeEmpty();
    }

    [Fact]
    public async Task Simulate_MigrateDown_ListsExactlyTheRecordsAboveTargetRelease()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        int release1Records = ctx.CountMigrations();
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        int recordsBefore = ctx.CountMigrations();
        int runsBefore = ctx.CountMigrationRuns();

        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Simulate, "Release_1.0");
        var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Simulate);

        downResult.Success.Should().BeTrue($"Simulate MigrateDown should succeed: {downResult.ErrorMessage}");
        downResult.TotalMigrations.Should().Be(recordsBefore - release1Records,
            "exactly the migrated records above Release_1.0 are rollback candidates (#7)");
        ctx.CountMigrations().Should().Be(recordsBefore, "Simulate must not touch migration records");
        ctx.CountMigrationRuns().Should().Be(runsBefore, "Simulate must not create a MigrationRun");
    }

    [Fact]
    public async Task Simulate_WithoutRepositoryTables_TreatsAllAsPending()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);

        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        result.Success.Should().BeTrue($"Simulate without a repository should succeed: {result.ErrorMessage}");
        result.MigrationResults.Should().Contain(r => r.ReleaseVersion == "Release_1.0",
            "without any repository record every file is pending");
        result.TotalMigrations.Should().Be(result.MigrationResults.Count).And.BeGreaterThan(0);
        ctx.AssertRepositoryTableExists("Product", false);
    }

    [Fact]
    public async Task Simulate_MigrateDown_WithoutRepositoryTables_ReportsNothingToRollBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.MigrateDown, MigrationRunMode.Simulate, "Release_1.0");

        var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Simulate);

        downResult.Success.Should().BeTrue($"Simulate MigrateDown without a repository should succeed: {downResult.ErrorMessage}");
        downResult.TotalMigrations.Should().Be(0);
        ctx.AssertRepositoryTableExists("MigrationRun", false);
    }

    /// <summary>
    /// The product is registered (a real migrate ran), but the simulated environment is not.
    /// The read-only lookup must report "not registered", treat everything as pending, and must NOT
    /// insert the environment row the way the CheckInsert template would.
    /// </summary>
    [Fact]
    public async Task Simulate_UnregisteredEnvironment_TreatsAllAsPendingWithoutInserting()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        int recordsBefore = ctx.CountMigrations();
        int runsBefore = ctx.CountMigrationRuns();

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate, environment: "SimulateOnly");
        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        result.Success.Should().BeTrue($"Simulate should succeed: {result.ErrorMessage}");
        result.MigrationResults.Should().Contain(r => r.ReleaseVersion == "Release_1.0",
            "nothing is migrated in an environment the repository has never seen, so every file is pending");
        ctx.AssertProductExists(true);
        ctx.AssertEnvironmentExists("SimulateOnly", false);
        ctx.CountMigrations().Should().Be(recordsBefore);
        ctx.CountMigrationRuns().Should().Be(runsBefore);
    }

    [Fact]
    public async Task Simulate_MigrateDown_UnregisteredEnvironment_ReportsNothingToRollBackWithoutInserting()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        int runsBefore = ctx.CountMigrationRuns();

        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Simulate, "Release_1.0", environment: "SimulateOnly");
        var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Simulate);

        downResult.Success.Should().BeTrue($"Simulate MigrateDown should succeed: {downResult.ErrorMessage}");
        downResult.TotalMigrations.Should().Be(0, "the unregistered environment has no records to roll back");
        ctx.AssertEnvironmentExists("SimulateOnly", false);
        ctx.CountMigrationRuns().Should().Be(runsBefore);
    }

    #endregion
}
