using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Compound;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Compound")]
public class RecoveryTests : PostgreSqlTestBase
{
    public RecoveryTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// #48 Phase 1: MigrateUp with error in R2/F2 and Terminate.
    /// R1=Migrated, R2/F1=Migrated, R2/F2=Failed.
    /// Between phases: restore original R2/F2 file from base path.
    /// Phase 2: MigrateUp all -- engine re-processes Failed record and continues.
    /// Final: all 12 files Migrated. Runs: Err, Ok.
    /// </summary>
    [Fact]
    public async Task AfterTerminate_IncrementalRerun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .BuildAsync();

        // Phase 1: MigrateUp -- error at R2/F2
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(1);
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // Verify Phase 1 state
        ctx.AssertFileStatus("01_CreateTableA.sql", MigrationStatus.Migrated);
        ctx.AssertFileStatus("02_CreateTableB.sql", MigrationStatus.Migrated);
        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.Migrated);
        ctx.AssertFileStatus("01_CreateTableC.sql", MigrationStatus.Migrated);
        ctx.AssertFileStatus("02_CreateTableD.sql", MigrationStatus.Failed);

        // Between phases: restore original R2/F2 from base files
        string srcPath = Path.Combine(Fixture.EngineConfig.BaseFilesPath, "Release_2.0", "Backend", "02_CreateTableD.sql");
        string dstPath = Path.Combine(ctx.WorkDirectory, "Release_2.0", "Backend", "02_CreateTableD.sql");
        File.Copy(srcPath, dstPath, overwrite: true);

        // Phase 2: MigrateUp all -- Failed record re-processed, succeeds
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Run 1 Error, Run 2 Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // All 12 files Migrated
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

        // All 8 tables exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", true);
        ctx.AssertTableExists("tabled", true);
        ctx.AssertTableExists("tablee", true);
        ctx.AssertTableExists("tablef", true);
        ctx.AssertTableExists("tableg", true);
        ctx.AssertTableExists("tableh", true);
    }

    /// <summary>
    /// #49 Phase 1: MigrateUp all (Ok). Phase 2: MigrateUp all again (nothing to do, Ok).
    /// All files still Migrated from Run 1. 2 runs (Ok, Ok).
    /// </summary>
    [Fact]
    public async Task NothingToMigrate_SecondRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: MigrateUp all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: MigrateUp all again (nothing new to migrate)
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Both runs Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // All 12 files still Migrated from Run 1
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
    }
}
