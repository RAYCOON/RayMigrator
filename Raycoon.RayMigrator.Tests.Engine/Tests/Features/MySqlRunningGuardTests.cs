using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlRunningGuardTests : MySqlTestBase
{
    public MySqlRunningGuardTests(MySqlFixture fixture) : base(fixture) { }

    [Fact] public async Task MigrateUp_WithRunning_ShouldFail() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync("Release_2.0"); ctx.AssertSuccess(true); ctx.InsertRunningMigrationRun(); await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate); var result = await ctx.MigrateUpAsync(); result.Success.Should().BeFalse("MigrateUp should fail when a running MigrationRun exists"); result.ErrorCode.Should().Be(TemplateResultCode.MigrationAlreadyRunning); }

    [Fact] public async Task MigrateDown_WithRunning_ShouldFail() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync("Release_2.0"); ctx.AssertSuccess(true); ctx.InsertRunningMigrationRun(); await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0"); var result = await ctx.MigrateDownAsync("Release_1.0"); result.Success.Should().BeFalse("MigrateDown should fail when a running MigrationRun exists"); result.ErrorCode.Should().Be(TemplateResultCode.MigrationAlreadyRunning); }

    [Fact] public async Task Baseline_WithRunning_ShouldFail() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync("Release_2.0"); ctx.AssertSuccess(true); ctx.InsertRunningMigrationRun(); await ctx.RebuildForAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate); var result = await ctx.BaselineAsync(); result.Success.Should().BeFalse("Baseline should fail when a running MigrationRun exists"); result.ErrorCode.Should().Be(TemplateResultCode.MigrationAlreadyRunning); }

    [Fact] public async Task ValidateHash_WithRunning_ShouldSucceed() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync("Release_2.0"); ctx.AssertSuccess(true); ctx.InsertRunningMigrationRun(); await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate); var result = await ctx.ValidateHashAsync(); result.Success.Should().BeTrue($"ValidateHash should succeed even with running MigrationRun: {result.ErrorMessage}"); }

    [Fact] public async Task UpdateHash_WithRunning_ShouldSucceed() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync("Release_2.0"); ctx.AssertSuccess(true); ctx.InsertRunningMigrationRun(); await ctx.RebuildForAsync(MigrationCommand.UpdateHash, MigrationRunMode.Migrate); var result = await ctx.UpdateHashAsync(); result.Success.Should().BeTrue($"UpdateHash should succeed even with running MigrationRun: {result.ErrorMessage}"); }

    [Fact] public async Task MigrateUp_WithoutRunning_ShouldSucceed() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); var result = await ctx.MigrateUpAsync(); result.Success.Should().BeTrue($"MigrateUp failed: {result.ErrorMessage}"); result.TotalMigrations.Should().BeGreaterThan(0); result.Result.Should().Be(MigrationRunResult.Ok); }
}
