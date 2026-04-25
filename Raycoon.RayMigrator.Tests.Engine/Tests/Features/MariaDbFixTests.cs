using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbFixTests : MariaDbTestBase
{
    public MariaDbFixTests(MariaDbFixture fixture) : base(fixture) { }

    /// <summary>
    /// F1: Fix with no orphaned runs should succeed with zero found/fixed.
    /// </summary>
    [Fact]
    public async Task Fix_NoOrphanedRuns_SucceedsWithZeroFixed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        var result = await ctx.FixIssuesAsync();

        result.Success.Should().BeTrue();
        result.OrphanedRunsFound.Should().Be(0);
        result.OrphanedRunsFixed.Should().Be(0);
    }

    /// <summary>
    /// F2: Fix should detect and fix an orphaned MigrationRun.
    /// </summary>
    [Fact]
    public async Task Fix_WithOrphanedRun_FixesIt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        ctx.InsertOrphanedMigrationRun(120);

        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        var result = await ctx.FixIssuesAsync(olderThanMinutes: 0);

        result.Success.Should().BeTrue();
        result.OrphanedRunsFound.Should().Be(1);
        result.OrphanedRunsFixed.Should().Be(1);
    }

    /// <summary>
    /// F3: Fix with DryRun should detect but not modify the orphaned run.
    /// </summary>
    [Fact]
    public async Task Fix_DryRun_DoesNotModifyRepository()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        ctx.InsertOrphanedMigrationRun(120);

        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        var result = await ctx.FixIssuesAsync(dryRun: true, olderThanMinutes: 0);

        result.OrphanedRunsFound.Should().Be(1);
        result.OrphanedRunsFixed.Should().Be(0);
        result.WasDryRun.Should().BeTrue();

        // Verify orphan still exists: run count should be 2 (1 completed + 1 orphaned)
        ctx.CountMigrationRuns().Should().Be(2);
    }

    /// <summary>
    /// F4: OlderThanMinutes filter should exclude recent orphaned runs.
    /// </summary>
    [Fact]
    public async Task Fix_OlderThanFilter_RespectsThreshold()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        ctx.InsertOrphanedMigrationRun(30);

        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        var result = await ctx.FixIssuesAsync(olderThanMinutes: 60);

        result.OrphanedRunsFound.Should().Be(0);
    }

    /// <summary>
    /// F5: After fixing an orphaned run, MigrateUp should succeed again.
    /// </summary>
    [Fact]
    public async Task Fix_AfterFixing_MigrateUpSucceeds()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        ctx.InsertOrphanedMigrationRun(120);

        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        await ctx.FixIssuesAsync(olderThanMinutes: 0);
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
    }

    /// <summary>
    /// F6: Fix should handle multiple orphaned runs.
    /// </summary>
    [Fact]
    public async Task Fix_MultipleOrphans_FixesAll()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        ctx.InsertOrphanedMigrationRun(120);
        ctx.InsertOrphanedMigrationRun(120);

        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        var result = await ctx.FixIssuesAsync(olderThanMinutes: 0);

        result.OrphanedRunsFound.Should().Be(2);
        result.OrphanedRunsFixed.Should().Be(2);
    }

    /// <summary>
    /// F7: Orphaned run details should be populated correctly.
    /// </summary>
    [Fact]
    public async Task Fix_OrphanedRunDetails_PopulateCorrectly()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        ctx.InsertOrphanedMigrationRun(120);

        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        var result = await ctx.FixIssuesAsync(olderThanMinutes: 0);

        result.OrphanedRuns.Should().HaveCount(1);
        var orphan = result.OrphanedRuns[0];
        orphan.MigrationRunId.Should().BeGreaterThan(0);
        orphan.Environment.Should().Be("Docker");
        orphan.MinutesRunning.Should().BeGreaterThanOrEqualTo(100);
        orphan.WasFixed.Should().BeTrue();
    }

    /// <summary>
    /// F8: Fix with AssumedMigrationStatus=Migrated should succeed.
    /// </summary>
    [Fact]
    public async Task Fix_AssumedStatusMigrated_UsesSpecifiedStatus()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        ctx.InsertOrphanedMigrationRun(120);

        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        var result = await ctx.FixIssuesAsync(
            assumedMigrationStatus: MigrationStatus.Migrated,
            olderThanMinutes: 0);

        result.Success.Should().BeTrue();
        result.OrphanedRunsFixed.Should().Be(1);
    }
}
