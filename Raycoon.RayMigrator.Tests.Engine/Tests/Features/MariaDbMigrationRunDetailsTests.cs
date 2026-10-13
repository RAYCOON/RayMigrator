using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The run details of schema version 2 on MariaDb: a run row records what it does, where and by whom it was started and
/// the error that decided its result; the history reports them; and a repository of schema version 1 is upgraded
/// to version 2 by the next command with its rows kept (ADR-024).
/// </summary>
[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbMigrationRunDetailsTests : MariaDbTestBase
{
    public MariaDbMigrationRunDetailsTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_WithInitiator_WritesOperationSourceInitiatorAndHost()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRunInitiator(MigrationRunSource.Studio, "Daniel", "oidc:123", "studio-host")
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        var run = ctx.GetMigrationRun(1)!;
        run.MigrationOperationId.Should().Be((int)MigrationOperation.MigrateUp);
        run.MigrationRunSourceId.Should().Be((int)MigrationRunSource.Studio);
        run.InitiatedBy.Should().Be("Daniel");
        run.InitiatedBySubject.Should().Be("oidc:123");
        run.Host.Should().Be("studio-host");
        run.ErrorCode.Should().BeNull();
        run.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task MigrateUp_WithoutInitiator_StoresNullSourceAndTheMachineName()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        var run = ctx.GetMigrationRun(1)!;
        run.MigrationRunSourceId.Should().BeNull();
        run.InitiatedBy.Should().BeNull();
        run.InitiatedBySubject.Should().BeNull();
        run.Host.Should().Be(Environment.MachineName);
    }

    [Fact]
    public async Task MigrateUp_WithFailingFile_WritesTheErrorMessage()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "01_CreateTableC.sql")
            .BuildAsync();

        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeFalse();
        var run = ctx.GetMigrationRun(1)!;
        run.MigrationRunResultId.Should().Be((int)MigrationRunResult.Error);
        run.ErrorMessage.Should().NotBeNullOrWhiteSpace("the run row keeps the message of the failure that decided its result");
    }

    [Fact]
    public async Task MigrateDownAndBaseline_WriteTheirOperation()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true);
        await ctx.RebuildForAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate);
        await ctx.BaselineAsync();

        ctx.GetMigrationRun(2)!.MigrationOperationId.Should().Be((int)MigrationOperation.MigrateDown);
        ctx.GetMigrationRun(3)!.MigrationOperationId.Should().Be((int)MigrationOperation.Baseline);
    }

    [Fact]
    public async Task History_ReportsTheRunDetails()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRunInitiator(MigrationRunSource.Ci, "pipeline", null, "runner-7")
            .BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var history = await ctx.GetHistoryAsync();

        var run = history.Runs.Should().ContainSingle().Which;
        run.Operation.Should().Be(MigrationOperation.MigrateUp);
        run.Source.Should().Be(MigrationRunSource.Ci);
        run.InitiatedBy.Should().Be("pipeline");
        run.InitiatedBySubject.Should().BeNull();
        run.Host.Should().Be("runner-7");
        run.ErrorMessage.Should().BeNull();
        run.LastHeartbeatAt.Should().NotBeNull("the run row carries the start as its first heartbeat");
        run.TotalMigrations.Should().BeGreaterThan(0);
        run.SuccessfulMigrations.Should().Be(run.TotalMigrations);
    }

    [Fact]
    public async Task SchemaVersionOne_IsUpgradedToVersionTwoByTheNextCommand()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        ctx.PinRepositorySchemaVersion(1);
        await ctx.InfoAsync();
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(1, "the pinned host creates schema version 1 only");
        ctx.AssertRepositoryTableExists("MigrationRunSource", false);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        ctx.CountRepoRows("MigratorMeta").Should().Be(1, "the engine version registered at version 1 is kept");
        ctx.AssertRepositoryTableExists("MigrationRunSource", true);
        ctx.CountRepoRows("MigrationRunSource").Should().Be(3);
        ctx.GetMigrationRun(1)!.MigrationOperationId.Should().Be((int)MigrationOperation.MigrateUp);
    }
}
