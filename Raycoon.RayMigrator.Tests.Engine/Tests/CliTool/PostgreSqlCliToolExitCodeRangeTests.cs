using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.CliTool;

/// <summary>
/// Tests that SuccessExitCodes range notation is correctly evaluated through the full engine path:
/// Config → MigrationService → CliToolExecutor → ExitCodeMatcher.
/// </summary>
[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "CliTool")]
public class PostgreSqlCliToolExitCodeRangeTests : PostgreSqlTestBase
{
    public PostgreSqlCliToolExitCodeRangeTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RangeIncludesZero_ToolReturnsZero_MigrationSucceeds()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetStdinConfig("PostgreSQL", Fixture.EngineConfig.ConnectionString);

        // "0..5" is a closed range that includes 0 — psql returns 0 on success, so this should work
        await using var ctx = await CreateScenario()
            .WithCliTool(cfg.Alias, cfg.ExecutablePath, cfg.ArgumentTemplate, cfg.InputMode,
                cfg.TimeoutInSeconds, successExitCodes: new[] { "0..5" })
            .WithUseCliToolAlias(cfg.Alias)
            .WithCliToolParameters(cfg.Parameters)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertTableExists("tablea", true);
    }

    [Fact]
    public async Task RangeExcludesZero_ToolReturnsZero_MigrationFails()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetStdinConfig("PostgreSQL", Fixture.EngineConfig.ConnectionString);

        // "1..5" does NOT include 0 — psql returns 0, but the whitelist rejects it
        await using var ctx = await CreateScenario()
            .WithCliTool(cfg.Alias, cfg.ExecutablePath, cfg.ArgumentTemplate, cfg.InputMode,
                cfg.TimeoutInSeconds, successExitCodes: new[] { "1..5" })
            .WithUseCliToolAlias(cfg.Alias)
            .WithCliToolParameters(cfg.Parameters)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
    }
}
