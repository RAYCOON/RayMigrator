using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The run heartbeat on SqlServer (schema version 2): a running migration refreshes LastHeartbeatAt, an open run with a
/// fresh heartbeat is alive however old its start, a run whose heartbeat went stale is auto-fixed by the next start,
/// and the fix command reports and filters by the heartbeat age.
/// </summary>
[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerHeartbeatTests : SqlServerTestBase
{
    public SqlServerHeartbeatTests(SqlServerFixture fixture) : base(fixture) { }

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

    [Fact]
    public async Task FixOrphaned_AliveRun_IsNotClosed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        // An open run whose heartbeat is fresh: the orphan select of a parallel start would not list it, but a run can
        // beat between that select and the fix. The fix must leave it open (-31 → false) instead of failing the start.
        ctx.InsertOrphanedMigrationRun(minutesOld: 120, heartbeatMinutesOld: 0);
        int runId = ctx.GetMigrationRun(2)!.Id;

        ctx.FixOrphanedRun(runId, thresholdMinutes: 2).Should().BeFalse("the heartbeat is younger than the threshold");
        ctx.GetLatestRunResultId().Should().Be((int)MigrationRunResult.Running, "an alive run stays open");

        ctx.FixOrphanedRun(runId, thresholdMinutes: 0).Should().BeTrue("with threshold 0 every open run counts as orphaned");
        ctx.GetLatestRunResultId().Should().Be((int)MigrationRunResult.Error);
    }

    [Fact]
    public async Task OpenRun_WithHeartbeatYoungerThanTheThreshold_IsAliveAcrossAMinuteBoundary()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRepositoryHeartbeat(intervalSeconds: 30, orphanedRunThresholdMinutes: 1)
            .BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        // The heartbeat lies one second before the current minute began: a minute boundary separates it from now, but
        // less than a minute has elapsed. Counting boundaries (SQL Server's DATEDIFF(MINUTE)) would call the run
        // orphaned; elapsed time must not. Wait out the end of a minute so that the probe stays inside the same one.
        if (DateTime.UtcNow.Second > 40)
            await Task.Delay(TimeSpan.FromSeconds(61 - DateTime.UtcNow.Second), TestContext.Current.CancellationToken);
        var now = DateTime.UtcNow;
        var minuteStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        ctx.InsertOpenMigrationRun(startedAtUtc: minuteStart.AddMinutes(-10), lastHeartbeatAtUtc: minuteStart.AddSeconds(-1));

        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeFalse("the heartbeat is younger than the threshold of one minute, whatever minute boundary lies in between");
        result.ErrorCode.Should().Be(-2);
        ctx.CountMigrationRuns().Should().Be(2, "the inserted run stays open");
    }
}
