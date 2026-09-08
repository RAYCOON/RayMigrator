using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.CliTool;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "CliTool")]
public class SqlServerCliToolFileTests : SqlServerTestBase
{
    public SqlServerCliToolFileTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task HappyPath_FileMode_AllReleasesMigrated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetFileConfig("SqlServer", Fixture.EngineConfig.ConnectionString);

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

        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableH", true);
        ctx.AssertRowCount("TableA", 3);
    }

    [Fact]
    public async Task FileMode_TwoReleases_PartialMigration()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetFileConfig("SqlServer", Fixture.EngineConfig.ConnectionString);

        await using var ctx = await CreateScenario()
            .WithCliTool(cfg.Alias, cfg.ExecutablePath, cfg.ArgumentTemplate, cfg.InputMode, cfg.TimeoutInSeconds)
            .WithUseCliToolAlias(cfg.Alias)
            .WithCliToolParameters(cfg.Parameters)
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableD", true);
        ctx.AssertTableExists("TableE", false);
        ctx.AssertTableExists("TableH", false);
    }

    [Fact]
    public async Task FileMode_SimulateMode_NoTablesCreated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetFileConfig("SqlServer", Fixture.EngineConfig.ConnectionString);

        await using var ctx = await CreateScenario()
            .WithCliTool(cfg.Alias, cfg.ExecutablePath, cfg.ArgumentTemplate, cfg.InputMode, cfg.TimeoutInSeconds)
            .WithUseCliToolAlias(cfg.Alias)
            .WithCliToolParameters(cfg.Parameters)
            .BuildAsync();

        await ctx.MigrateUpAsync(runMode: MigrationRunMode.Simulate);

        ctx.AssertSuccess(true);
        ctx.AssertTableExists("TableA", false);
    }

    /// <summary>
    /// RollbackErrorAction=Ignore must be honoured when rollback files run through a CLI tool, exactly like
    /// the DAL path (see MigrateDown ErrorTests #42): the broken R2/F1 rollback is marked Failed, the chain
    /// continues and the run ends Ok (#15).
    /// </summary>
    [Fact]
    public async Task FileMode_BrokenRollback_Ignore_ChainContinues()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var cfg = CliToolConfigHelper.GetFileConfig("SqlServer", Fixture.EngineConfig.ConnectionString);

        await using var ctx = await CreateScenario()
            .WithCliTool(cfg.Alias, cfg.ExecutablePath, cfg.ArgumentTemplate, cfg.InputMode, cfg.TimeoutInSeconds)
            .WithUseCliToolAlias(cfg.Alias)
            .WithCliToolParameters(cfg.Parameters)
            .WithRollbackErrorAction(RollbackErrorAction.Ignore)
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

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

        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1: rollback failed, error ignored
            ("01_CreateTableC.sql", MigrationStatus.Failed),
            // everything above R2/F1 was rolled back before, everything below is unaffected
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableG.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableH.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataD.sql", MigrationStatus.NotMigrated)
        );
    }
}
