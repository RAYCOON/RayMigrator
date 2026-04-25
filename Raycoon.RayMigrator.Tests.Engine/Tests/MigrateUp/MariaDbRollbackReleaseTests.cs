using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "MigrateUp")]
public class MariaDbRollbackReleaseTests : MariaDbTestBase
{
    public MariaDbRollbackReleaseTests(MariaDbFixture fixture) : base(fixture) { }

    /// <summary>
    /// #14 Error in R3/F3 with RollbackRelease. Only R3 is rolled back.
    /// R1+R2 stay Migrated, R3=NotMigrated, R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task ErrorInR3_OnlyR3RolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1+R2 stay Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3 rolled back to NotMigrated
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4 has no records

        // R1+R2 tables still exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", true);
        ctx.AssertTableExists("tabled", true);

        // R3 tables dropped by rollback
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
    }

    /// <summary>
    /// #15 Error in R2/F2 with RollbackRelease. Only R2 is rolled back.
    /// R1 stays Migrated, R2/F1+F2=NotMigrated, R3+R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task ErrorInR2_OnlyR2RolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1 stays Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1+F2 rolled back to NotMigrated
            ("01_CreateTableC.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated)
        );
        // R2/F3 and R3+R4 have no records

        // R1 tables still exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);

        // R2 tables dropped by rollback
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
    }

    /// <summary>
    /// #16 Error in R1/F3 with RollbackRelease. All of R1 is rolled back.
    /// R1/F1+F2=NotMigrated (rolled back), R1/F3=NotMigrated (error, rolled back), R2-R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task ErrorInR1_AllR1RolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_1.0", "03_SeedDataA.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1/F1+F2 rolled back, R1/F3 error then rolled back
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataA.sql", MigrationStatus.NotMigrated)
        );
        // R2-R4 have no records

        // R1 tables dropped by rollback
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
    }

    /// <summary>
    /// #17 Error at release boundary (first file of R2) with RollbackRelease.
    /// R1 stays Migrated, R2/F1=NotMigrated (only file in R2 that was attempted), rest=NoRecord.
    /// </summary>
    [Fact]
    public async Task ErrorAtReleaseBoundary()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "01_CreateTableC.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1 stays Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1 rolled back
            ("01_CreateTableC.sql", MigrationStatus.NotMigrated)
        );
        // R2/F2-R4 have no records

        // R1 tables still exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);

        // R2 table dropped by rollback
        ctx.AssertTableExists("tablec", false);
    }

    /// <summary>
    /// #18 Error in R3/F3 with broken rollback at R3/F1 and RollbackErrorAction=Terminate.
    /// R1+R2 stay Migrated, R3/F1=Failed, R3/F2+F3=NotMigrated, R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task BrokenRollback_Terminate()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BreakRollback("Release_3.0", "01_CreateTableE.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .WithRollbackErrorAction(RollbackErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1+R2 stay Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3/F1 Failed (broken rollback, chain aborted)
            ("01_CreateTableE.sql", MigrationStatus.Failed),
            // R3/F2+F3 NotMigrated (successfully rolled back before chain abort)
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4 has no records
    }

    /// <summary>
    /// #19 Same as #18 but RollbackErrorAction=Ignore. Rollback continues past broken R3/F1.
    /// R1+R2 stay Migrated, R3/F1=Failed, R3/F2+F3=NotMigrated, R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task BrokenRollback_Ignore()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BreakRollback("Release_3.0", "01_CreateTableE.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .WithRollbackErrorAction(RollbackErrorAction.Ignore)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1+R2 stay Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3/F1 Failed (rollback error ignored, blocks attempted)
            ("01_CreateTableE.sql", MigrationStatus.Failed),
            // R3/F2+F3 NotMigrated
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4 has no records
    }

    /// <summary>
    /// #20 Error in R3/F3, missing rollback for R3/F1, RequireRollbackFile=false.
    /// R1+R2 stay Migrated, R3/F1=Migrated (missing rollback, data remains in database), R3/F2+F3=NotMigrated, R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task MissingRollback_RequireFalse()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .RemoveRollback("Release_3.0", "01_CreateTableE.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .WithRequireRollbackFile(false)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1+R2 stay Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3/F1 Migrated (missing rollback file, data remains in database)
            ("01_CreateTableE.sql", MigrationStatus.Migrated),
            // R3/F2+F3 NotMigrated
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4 has no records
    }
}
