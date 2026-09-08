using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class InfoTests : PostgreSqlTestBase
{
    public InfoTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// I1: Info on a fresh repository should show zero executed and pending > 0.
    /// </summary>
    [Fact]
    public async Task Info_OnFreshRepository_ReturnsZeroCounts()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        var result = await ctx.InfoAsync();

        result.TotalMigrationsExecuted.Should().Be(0);
        result.PendingMigrations.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// I2: After a full migration, Info should show all migrated and none pending.
    /// </summary>
    [Fact]
    public async Task Info_AfterFullMigration_ShowsAllMigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.InfoAsync();

        result.PendingMigrations.Should().Be(0);
        result.TotalMigrationsExecuted.Should().BeGreaterThan(0);
        result.LastRunResult.Should().Be(MigrationRunResult.Ok);
    }

    /// <summary>
    /// I3: After a partial migration, Info should show both executed and pending counts.
    /// </summary>
    [Fact]
    public async Task Info_AfterPartialMigration_ShowsPendingCount()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.InfoAsync();

        result.PendingMigrations.Should().BeGreaterThan(0);
        result.TotalMigrationsExecuted.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// I4: After a baseline, Info should show all files as migrated with none pending.
    /// </summary>
    [Fact]
    public async Task Info_AfterBaseline_ShowsBaselinedAsMigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate);

        await ctx.BaselineAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.InfoAsync();

        result.PendingMigrations.Should().Be(0);
        result.TotalMigrationsExecuted.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// I5: TargetGroup status should be populated correctly after a full migration.
    /// </summary>
    [Fact]
    public async Task Info_TargetGroupStatus_PopulatesCorrectly()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.InfoAsync();

        result.TargetGroups.Should().ContainKey("Backend");
        var tg = result.TargetGroups["Backend"];
        tg.ExecutedMigrations.Should().BeGreaterThan(0);
        tg.DatabaseType.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// I6: After a failed migration, Info should reflect the error state.
    /// </summary>
    [Fact]
    public async Task Info_AfterFailedMigration_ReflectsErrorState()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .InjectError("Release_1.0", "01_CreateTableA.sql")
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.InfoAsync();

        result.LastRunResult.Should().Be(MigrationRunResult.Error);
    }

    /// <summary>
    /// I7: After multiple migration runs, GetHistory should return all runs.
    /// </summary>
    [Fact]
    public async Task GetHistory_AfterMultipleRuns_ReturnsAllRuns()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.GetHistoryAsync();

        result.Runs.Count.Should().Be(2);
    }

    /// <summary>
    /// I8: GetHistory should return run details including result and migration count.
    /// </summary>
    [Fact]
    public async Task GetHistory_ShowsRunDetails()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.GetHistoryAsync();

        result.Runs.Count.Should().BeGreaterThanOrEqualTo(1);
        result.Runs[0].Result.Should().Be(MigrationRunResult.Ok);
        result.Runs[0].TotalMigrations.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// #8: pending is evaluated like migrate-up does, so a migrated file whose hash no longer matches
    /// (it would be re-executed) counts as pending.
    /// </summary>
    [Fact]
    public async Task Info_AfterFileModification_CountsModifiedFileAsPending()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        string filePath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql");
        File.WriteAllText(filePath, File.ReadAllText(filePath) + Environment.NewLine + "-- hash-breaking modification");

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.InfoAsync();

        result.PendingMigrations.Should().Be(1, "the modified file would be re-executed by migrate-up (#8)");
    }

    /// <summary>
    /// I9: A migrate-down run must show up in the history as MigrateDown with the records it rolled back, and the
    /// migrate-up run that created those records keeps its own count. Up all (12 files), down to Release_1.0
    /// (9 files rolled back): newest run MigrateDown 9/9, oldest run MigrateUp 12/12 (#13).
    /// </summary>
    [Fact]
    public async Task GetHistory_AfterMigrateDown_ShowsDownRunWithItsRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // the rolled-back record is stamped with the operation that rolled it back
        ctx.AssertMigrationRecord("02_CreateTableD.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            MigrationOperationId = (int)MigrationOperation.MigrateDown
        });
        ctx.AssertMigrationRecord("01_CreateTableA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Migrated,
            MigrationOperationId = (int)MigrationOperation.MigrateUp
        });

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var history = await ctx.GetHistoryAsync();

        history.Runs.Count.Should().Be(2);
        var downRun = history.Runs.OrderByDescending(r => r.MigrationRunId).First();
        downRun.Operation.Should().Be(MigrationOperation.MigrateDown, "the newest run is the migrate-down");
        downRun.Result.Should().Be(MigrationRunResult.Ok);
        downRun.TotalMigrations.Should().Be(9, "Release_2.0 to Release_4.0 hold 9 files that were rolled back");
        downRun.SuccessfulMigrations.Should().Be(9, "every rollback ended NotMigrated");
        downRun.FailedMigrations.Should().Be(0);

        var upRun = history.Runs.OrderBy(r => r.MigrationRunId).First();
        upRun.Operation.Should().Be(MigrationOperation.MigrateUp);
        upRun.TotalMigrations.Should().Be(12, "the migrate-up keeps the records it migrated, even after they were rolled back");
        upRun.SuccessfulMigrations.Should().Be(12);
    }

    /// <summary>
    /// I10: Info.LastRunResult reflects the persisted result of the newest MigrationRun, not the status of an
    /// arbitrary record. Up all (Ok), then migrate-down with a broken rollback and Terminate (Error): Info must report Error (#14).
    /// </summary>
    [Fact]
    public async Task Info_AfterFailedMigrateDown_LastRunResultIsError()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRollbackErrorAction(RollbackErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        string rollbackPath = Path.Combine(ctx.WorkDirectory, "Release_2.0", "Backend", "01_CreateTableC.rollback.sql");
        File.WriteAllText(rollbackPath,
            """
            /*
            [RayMigrator]
            Description = "Broken rollback"
            UseTransaction = true
            */

            """ + SqlDialect.GetErrorSql(Fixture.EngineConfig.DatabaseType) + Environment.NewLine);

        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var result = await ctx.InfoAsync();

        result.LastRunResult.Should().Be(MigrationRunResult.Error, "the newest MigrationRun row is the failed migrate-down");
    }

    /// <summary>
    /// I11: An error-recovery rollback inside a migrate-up stamps the rolled-back records with Rollback, and the
    /// history still lists the run as MigrateUp with every record it touched. Error in R2/F2 with Rollback:
    /// R1 (3) + R2/F1 + the failed R2/F2 = 5 records, all NotMigrated afterwards (#13).
    /// </summary>
    [Fact]
    public async Task GetHistory_AfterMigrateUpWithRollback_ShowsUpRunWithRolledBackRecords()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);

        ctx.AssertMigrationRecord("01_CreateTableC.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            MigrationOperationId = (int)MigrationOperation.Rollback
        });

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var history = await ctx.GetHistoryAsync();

        history.Runs.Count.Should().Be(1);
        history.Runs[0].Operation.Should().Be(MigrationOperation.MigrateUp, "an error-recovery rollback happens inside the migrate-up run");
        history.Runs[0].Result.Should().Be(MigrationRunResult.Error);
        history.Runs[0].TotalMigrations.Should().Be(5, "R1 (3 files), R2/F1 and the failed R2/F2 were touched");
        history.Runs[0].SuccessfulMigrations.Should().Be(0, "every record was rolled back again");
        history.Runs[0].FailedMigrations.Should().Be(0, "the failed file was rolled back too");
    }
}
