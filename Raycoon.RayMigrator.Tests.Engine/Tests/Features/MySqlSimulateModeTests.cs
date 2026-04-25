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
}
