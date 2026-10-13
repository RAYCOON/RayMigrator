using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The run heartbeat on PostgreSQL (schema version 2): a running migration refreshes LastHeartbeatAt, an open run with a
/// fresh heartbeat is alive however old its start, a run whose heartbeat went stale is auto-fixed by the next start,
/// and the fix command reports and filters by the heartbeat age.
/// </summary>
[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class HeartbeatTests : PostgreSqlTestBase
{
    public HeartbeatTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_LongRunningBlock_RefreshesTheHeartbeat()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRepositoryHeartbeat(intervalSeconds: 1)
            // The long block sits in Release_3.0, so the run goes on after it and the heartbeat has time to land.
            .ReplaceBlock("Release_3.0", "03_SeedDataC.sql", 0, SqlDialect.GetSleepSql(Fixture.EngineConfig.DatabaseType, 3))
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.HeartbeatAdvanced(1).Should().BeTrue($"a heartbeat landed while the long block ran (StartedAt|LastHeartbeatAt|FinishedAt = {ctx.GetMigrationRunTimestamps(1)})");
    }

    [Fact]
    public async Task OpenRun_WithFreshHeartbeat_BlocksANewRunHoweverOldItsStart()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.InsertOrphanedMigrationRun(minutesOld: 120, heartbeatMinutesOld: 0);

        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeFalse("the open run is alive: its heartbeat is younger than the threshold");
        result.ErrorCode.Should().Be(-2);
        ctx.CountMigrationRuns().Should().Be(2, "the inserted run stays open");
    }

    [Fact]
    public async Task OpenRun_WithStaleHeartbeat_IsAutoFixedByTheNextRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.InsertOrphanedMigrationRun(minutesOld: 3, heartbeatMinutesOld: 3);

        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeTrue("a heartbeat older than the threshold of 2 minutes marks the run as orphaned");
        ctx.CountMigrationRuns().Should().Be(3);
        ctx.GetLatestRunResultId().Should().Be((int)MigrationRunResult.Ok);
    }

    [Fact]
    public async Task Fix_ReportsAndFiltersByTheHeartbeatAge()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.FixIssues, MigrationRunMode.Migrate);
        ctx.InsertOrphanedMigrationRun(minutesOld: 120, heartbeatMinutesOld: 50);

        var tooYoung = await ctx.FixIssuesAsync(olderThanMinutes: 60);
        tooYoung.OrphanedRunsFixed.Should().Be(0, "the heartbeat is 50 minutes old, the start 120: the heartbeat age counts");

        var fixedRuns = await ctx.FixIssuesAsync(olderThanMinutes: 40);
        fixedRuns.OrphanedRunsFixed.Should().Be(1);
        var run = fixedRuns.OrphanedRuns.Should().ContainSingle().Which;
        run.MinutesSinceHeartbeat.Should().BeInRange(49, 51);
        run.MinutesRunning.Should().BeInRange(119, 121);
        run.LastHeartbeatAt.Should().NotBeNull();
        run.WasFixed.Should().BeTrue();
    }
}
