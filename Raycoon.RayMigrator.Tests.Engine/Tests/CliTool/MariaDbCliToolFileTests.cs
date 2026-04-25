
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.CliTool;

[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "CliTool")]
public class MariaDbCliToolFileTests : MariaDbTestBase
{
    public MariaDbCliToolFileTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task HappyPath_FileMode_AllReleasesMigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetFileConfig("MariaDb", Fixture.EngineConfig.ConnectionString);

        await using var ctx = await CreateScenario()
            .WithCliTool(cfg.Alias, cfg.ExecutablePath, cfg.ArgumentTemplate, cfg.InputMode, cfg.TimeoutInSeconds)
            .WithUseCliToolAlias(cfg.Alias)
            .WithCliToolParameters(cfg.Parameters)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            ("01_CreateTableE.sql", MigrationStatus.Migrated),
            ("02_CreateTableF.sql", MigrationStatus.Migrated),
            ("03_SeedDataC.sql", MigrationStatus.Migrated),
            ("01_CreateTableG.sql", MigrationStatus.Migrated),
            ("02_CreateTableH.sql", MigrationStatus.Migrated),
            ("03_SeedDataD.sql", MigrationStatus.Migrated)
        );

        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableh", true);
        ctx.AssertRowCount("tablea", 3);
    }

    [Fact]
    public async Task FileMode_TwoReleases_PartialMigration()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetFileConfig("MariaDb", Fixture.EngineConfig.ConnectionString);

        await using var ctx = await CreateScenario()
            .WithCliTool(cfg.Alias, cfg.ExecutablePath, cfg.ArgumentTemplate, cfg.InputMode, cfg.TimeoutInSeconds)
            .WithUseCliToolAlias(cfg.Alias)
            .WithCliToolParameters(cfg.Parameters)
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tabled", true);
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tableh", false);
    }

    [Fact]
    public async Task FileMode_SimulateMode_NoTablesCreated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetFileConfig("MariaDb", Fixture.EngineConfig.ConnectionString);

        await using var ctx = await CreateScenario()
            .WithCliTool(cfg.Alias, cfg.ExecutablePath, cfg.ArgumentTemplate, cfg.InputMode, cfg.TimeoutInSeconds)
            .WithUseCliToolAlias(cfg.Alias)
            .WithCliToolParameters(cfg.Parameters)
            .BuildAsync();

        await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        ctx.AssertSuccess(true);
        ctx.AssertTableExists("tablea", false);
    }
}
