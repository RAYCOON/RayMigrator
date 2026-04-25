
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "MigrateUp")]
public class SqliteRollbackTests : SqliteTestBase
{
    public SqliteRollbackTests(SqliteFixture fixture) : base(fixture) { }

    /// <summary>
    /// #6 Error in R4/F3 with Rollback. All 12 files rolled back to NotMigrated.
    /// All tables dropped by rollback chain.
    /// </summary>
    [Fact]
    public async Task ErrorInR4_AllRolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_4.0", "03_SeedDataD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // All 12 files should be NotMigrated (rolled back)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataA.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableC.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableG.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableH.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataD.sql", MigrationStatus.NotMigrated)
        );

        // All tables dropped by rollback
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }

    /// <summary>
    /// #7 Error in R2/F2 with Rollback. R1+R2 files rolled back to NotMigrated,
    /// R3+R4 have no records. All tables dropped.
    /// </summary>
    [Fact]
    public async Task ErrorInR2_AllRolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // R1 + R2 (attempted files) are NotMigrated, R3+R4 have no records
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataA.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableC.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated)
        );

        // All tables dropped
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }

    /// <summary>
    /// #8 Error in the very first file R1/F1 with Rollback. Only R1/F1 gets rolled back.
    /// Rest have no records.
    /// </summary>
    [Fact]
    public async Task ErrorInR1_SingleFileRolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_1.0", "01_CreateTableA.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // Only R1/F1 has a record (NotMigrated after rollback)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated)
        );

        // No tables exist
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
    }

    /// <summary>
    /// #9 Error in R3/F3 with broken rollback at R2/F1 and RollbackErrorAction=Terminate.
    /// Rollback chain aborts at R2/F1. R1 stays Migrated, R2/F1=Failed, R2/F2+F3=NotMigrated,
    /// R3=NotMigrated, R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task BrokenRollback_Terminate_ChainAborted()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BreakRollback("Release_2.0", "01_CreateTableC.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .WithRollbackErrorAction(RollbackErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // R1 stays Migrated (rollback chain aborted before reaching R1)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1 Failed (rollback SQL failed)
            ("01_CreateTableC.sql", MigrationStatus.Failed),
            // R2/F2+F3 NotMigrated (successfully rolled back)
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3 NotMigrated (successfully rolled back)
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4 has no records (never attempted)
    }

    /// <summary>
    /// #10 Same as #9 but RollbackErrorAction=Ignore. Rollback continues past the broken R2/F1.
    /// R1=NotMigrated (chain reached R1), R2/F1=Failed, R2/F2+F3=NotMigrated, R3=NotMigrated, R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task BrokenRollback_Ignore_ChainContinues()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BreakRollback("Release_2.0", "01_CreateTableC.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .WithRollbackErrorAction(RollbackErrorAction.Ignore)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // R1 NotMigrated (chain continued past broken rollback and reached R1)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataA.sql", MigrationStatus.NotMigrated),
            // R2/F1 Failed (rollback block failed, Ignored)
            ("01_CreateTableC.sql", MigrationStatus.Failed),
            // R2/F2+F3 NotMigrated
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3 NotMigrated
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4 has no records
    }

    /// <summary>
    /// #11 Missing rollback file with RequireRollbackFile=true.
    /// Pre-validation fails before any SQL executes. MigrationRun created with Error,
    /// but no Migration records exist. No tables created.
    /// </summary>
    [Fact]
    public async Task MissingRollback_RequireTrue_PreValidationFails()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .RemoveRollback("Release_2.0", "01_CreateTableC.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .WithRequireRollbackFile(true)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // No Migration records created (exception during file discovery phase)
        // All files have no records -- nothing to assert via AssertFileStatuses

        // No tables exist (no SQL executed)
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }

    /// <summary>
    /// #12 Error in R3/F3, missing rollback for R2/F1, RequireRollbackFile=false.
    /// Rollback chain skips the missing rollback and continues.
    /// R1=NotMigrated, R2/F1=Migrated (missing rollback, data remains in database), R2/F2+F3=NotMigrated, R3=NotMigrated, R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task MissingRollback_RequireFalse_ChainSkipsAndContinues()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .RemoveRollback("Release_2.0", "01_CreateTableC.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .WithRequireRollbackFile(false)
            .WithStopRollbackOnMissingRollbackFile(false)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1 NotMigrated (chain continued past missing rollback)
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataA.sql", MigrationStatus.NotMigrated),
            // R2/F1 Migrated (rollback file missing, data remains in database)
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            // R2/F2+F3 NotMigrated
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3 NotMigrated
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4 has no records
    }

    /// <summary>
    /// #13 Error in R4/F3, two missing rollbacks (R2/F1, R1/F1), RequireRollbackFile=false.
    /// Chain skips both missing rollbacks. R1/F1=Migrated, R1/F2+F3=NotMigrated,
    /// R2/F1=Migrated, R2/F2+F3=NotMigrated, R3=NotMigrated, R4=NotMigrated.
    /// </summary>
    [Fact]
    public async Task MissingMultipleRollbacks_RequireFalse()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_4.0", "03_SeedDataD.sql")
            .RemoveRollback("Release_2.0", "01_CreateTableC.sql")
            .RemoveRollback("Release_1.0", "01_CreateTableA.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .WithRequireRollbackFile(false)
            .WithStopRollbackOnMissingRollbackFile(false)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1/F1 Migrated (missing rollback, data remains in database)
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            // R1/F2+F3 NotMigrated
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataA.sql", MigrationStatus.NotMigrated),
            // R2/F1 Migrated (missing rollback, data remains in database)
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            // R2/F2+F3 NotMigrated
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3 NotMigrated
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated),
            // R4 NotMigrated
            ("01_CreateTableG.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableH.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataD.sql", MigrationStatus.NotMigrated)
        );
    }
}
