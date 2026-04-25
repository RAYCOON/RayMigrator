using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class RunningGuardTests : PostgreSqlTestBase
{
    public RunningGuardTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// G1: MigrateUp should fail when a running MigrationRun exists.
    /// Setup: MigrateUp R1+R2, insert running run, rebuild, attempt MigrateUp.
    /// </summary>
    [Fact]
    public async Task MigrateUp_WithRunning_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Setup: MigrateUp R1+R2 to create the Product record
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Insert a fake running MigrationRun
        ctx.InsertRunningMigrationRun();

        // Rebuild for a new MigrateUp attempt
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeFalse("MigrateUp should fail when a running MigrationRun exists");
        result.ErrorCode.Should().Be(TemplateResultCode.MigrationAlreadyRunning);
    }

    /// <summary>
    /// G2: MigrateDown should fail when a running MigrationRun exists.
    /// </summary>
    [Fact]
    public async Task MigrateDown_WithRunning_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Setup: MigrateUp R1+R2
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Insert a fake running MigrationRun
        ctx.InsertRunningMigrationRun();

        // Rebuild for MigrateDown
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        var result = await ctx.MigrateDownAsync("Release_1.0");

        result.Success.Should().BeFalse("MigrateDown should fail when a running MigrationRun exists");
        result.ErrorCode.Should().Be(TemplateResultCode.MigrationAlreadyRunning);
    }

    /// <summary>
    /// G3: Baseline should fail when a running MigrationRun exists.
    /// </summary>
    [Fact]
    public async Task Baseline_WithRunning_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Setup: MigrateUp R1+R2 to create the Product record
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Insert a fake running MigrationRun
        ctx.InsertRunningMigrationRun();

        // Rebuild for Baseline
        await ctx.RebuildForAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate);
        var result = await ctx.BaselineAsync();

        result.Success.Should().BeFalse("Baseline should fail when a running MigrationRun exists");
        result.ErrorCode.Should().Be(TemplateResultCode.MigrationAlreadyRunning);
    }

    /// <summary>
    /// G4: ValidateHash should succeed even when a running MigrationRun exists (read-only operation).
    /// </summary>
    [Fact]
    public async Task ValidateHash_WithRunning_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Setup: MigrateUp R1+R2
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Insert a fake running MigrationRun
        ctx.InsertRunningMigrationRun();

        // Rebuild for ValidateHash
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var result = await ctx.ValidateHashAsync();

        result.Success.Should().BeTrue(
            $"ValidateHash should succeed even with running MigrationRun: {result.ErrorMessage}");
    }

    /// <summary>
    /// G5: UpdateHash should succeed even when a running MigrationRun exists (read-only operation).
    /// Note: GetStatusAsync is not exposed on ScenarioContext, so we test UpdateHash as
    /// the alternative read-only operation instead of Info.
    /// </summary>
    [Fact]
    public async Task UpdateHash_WithRunning_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Setup: MigrateUp R1+R2
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);

        // Insert a fake running MigrationRun
        ctx.InsertRunningMigrationRun();

        // Rebuild for UpdateHash
        await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate);
        var result = await ctx.UpdateHashAsync();

        result.Success.Should().BeTrue(
            $"UpdateHash should succeed even with running MigrationRun: {result.ErrorMessage}");
    }

    /// <summary>
    /// G6: Normal MigrateUp without a running guard should succeed.
    /// </summary>
    [Fact]
    public async Task MigrateUp_WithoutRunning_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeTrue($"MigrateUp failed: {result.ErrorMessage}");
        result.TotalMigrations.Should().BeGreaterThan(0);
        result.Result.Should().Be(MigrationRunResult.Ok);
    }
}
