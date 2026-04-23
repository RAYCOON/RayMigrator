// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlInfoTests : MySqlTestBase
{
    public MySqlInfoTests(MySqlFixture fixture) : base(fixture) { }

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
}
