
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerSimulateModeTests : SqlServerTestBase
{
    public SqlServerSimulateModeTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// S1: MigrateUp in Simulate mode should succeed.
    /// </summary>
    [Fact]
    public async Task Simulate_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);

        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        result.Success.Should().BeTrue($"Simulate mode should succeed: {result.ErrorMessage}");
    }

    /// <summary>
    /// S2: Simulate mode should not create user tables.
    /// </summary>
    [Fact]
    public async Task Simulate_ShouldNotCreateUserTables()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);

        await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        ctx.AssertTableExists("TableA", false);
        ctx.AssertTableExists("TableB", false);
        ctx.AssertTableExists("TableC", false);
        ctx.AssertTableExists("TableD", false);
        ctx.AssertTableExists("TableE", false);
        ctx.AssertTableExists("TableF", false);
        ctx.AssertTableExists("TableG", false);
        ctx.AssertTableExists("TableH", false);
    }

    /// <summary>
    /// S3: Simulate mode should NOT write repository records (side-effect-free).
    /// </summary>
    [Fact]
    public async Task Simulate_ShouldNotWriteRepositoryRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);

        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);
        result.Success.Should().BeTrue($"Simulate mode should succeed: {result.ErrorMessage}");

        // Repository tables should NOT exist (Simulate is side-effect-free)
        ctx.AssertRepositoryTableExists("MigrationRecord", false);
        ctx.AssertRepositoryTableExists("MigrationRun", false);
        ctx.AssertRepositoryTableExists("Product", false);
    }

    /// <summary>
    /// S4: Validate mode should not write anything to the repository or create user tables.
    /// </summary>
    [Fact]
    public async Task Validate_ShouldNotWriteAnything()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.MigrateUp, MigrationRunMode.Validate);

        var result = await ctx.MigrateUpAsync(runMode: MigrationRunMode.Validate);
        result.Success.Should().BeTrue($"Validate mode should succeed: {result.ErrorMessage}");

        // Repository tables should NOT exist (RepositoryCheckCreate is skipped in Validate mode)
        ctx.AssertRepositoryTableExists("MigrationRecord", false);
        ctx.AssertRepositoryTableExists("MigrationRun", false);
        ctx.AssertRepositoryTableExists("Product", false);

        // User tables should NOT exist
        ctx.AssertTableExists("TableA", false);
        ctx.AssertTableExists("TableB", false);
        ctx.AssertTableExists("TableC", false);
        ctx.AssertTableExists("TableD", false);
    }

    /// <summary>
    /// S5: MigrateUp then Simulate MigrateDown should not actually roll back data.
    /// </summary>
    [Fact]
    public async Task SimulateMigrateDown_ShouldNotRollback()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Real MigrateUp all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        // Verify tables and data exist
        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableC", true);
        ctx.AssertRowCount("TableA", 3);
        ctx.AssertRowCount("TableC", 3);

        // Phase 2: Simulate MigrateDown to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Simulate, "Release_1.0");
        var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Simulate);
        downResult.Success.Should().BeTrue($"Simulate MigrateDown should succeed: {downResult.ErrorMessage}");

        // Data should still be intact (simulate does not execute rollback SQL)
        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableB", true);
        ctx.AssertTableExists("TableC", true);
        ctx.AssertTableExists("TableD", true);
        ctx.AssertTableExists("TableE", true);
        ctx.AssertTableExists("TableF", true);
        ctx.AssertTableExists("TableG", true);
        ctx.AssertTableExists("TableH", true);

        ctx.AssertRowCount("TableA", 3);
        ctx.AssertRowCount("TableC", 3);
        ctx.AssertRowCount("TableE", 3);
        ctx.AssertRowCount("TableG", 3);
    }

    /// <summary>
    /// S6: Validate MigrateDown should succeed but not actually roll back any data.
    /// </summary>
    [Fact]
    public async Task ValidateMigrateDown_ShouldNotRollback()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Real MigrateUp all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        // Verify tables and data exist
        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableC", true);

        // Phase 2: Validate MigrateDown to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Validate, "Release_1.0");
        var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Validate);
        downResult.Success.Should().BeTrue($"Validate MigrateDown should succeed: {downResult.ErrorMessage}");

        // All tables should still exist (Validate does not execute rollback SQL)
        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableB", true);
        ctx.AssertTableExists("TableC", true);
        ctx.AssertTableExists("TableD", true);
        ctx.AssertTableExists("TableE", true);
        ctx.AssertTableExists("TableF", true);
        ctx.AssertTableExists("TableG", true);
        ctx.AssertTableExists("TableH", true);

        // Data should be intact
        ctx.AssertRowCount("TableA", 3);

        // Validate does not create a new MigrationRun
        ctx.AssertRunCount(1);
    }

    /// <summary>
    /// S7: Validate MigrateDown should fail when rollback file is missing, but tables remain intact.
    /// </summary>
    [Fact]
    public async Task ValidateMigrateDown_MissingRollback_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Real MigrateUp all (rollback file must exist during MigrateUp due to RequireRollbackFile=true)
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        // Phase 2: Remove rollback file AFTER successful MigrateUp
        string rollbackPath = Path.Combine(ctx.WorkDirectory, "Release_4.0", "Backend", "01_CreateTableG.rollback.sql");
        File.Delete(rollbackPath);

        // Phase 3: Validate MigrateDown — should fail due to missing rollback file
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Validate, "Release_1.0");
        var downResult = await ctx.MigrateDownAsync("Release_1.0", runMode: MigrationRunMode.Validate);
        downResult.Success.Should().BeFalse("Validate MigrateDown should fail when rollback file is missing");

        // Tables should still exist (Validate is non-destructive)
        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableC", true);
        ctx.AssertTableExists("TableG", true);
    }
}
