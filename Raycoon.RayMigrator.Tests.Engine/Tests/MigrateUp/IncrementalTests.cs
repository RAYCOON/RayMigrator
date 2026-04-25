
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "MigrateUp")]
public class IncrementalTests : PostgreSqlTestBase
{
    public IncrementalTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// #27 Phase 1: MigrateUp to Release_2.0. Phase 2: MigrateUp all remaining.
    /// All 12 files should be Migrated across 2 runs, both Ok.
    /// </summary>
    [Fact]
    public async Task TwoPhases_AllSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Migrate up to Release_2.0
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertRunCount(1);

        // Phase 2: Rebuild and migrate all remaining
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Both runs should be Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // All 12 files should be Migrated
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

        // All 8 tables should exist
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
    /// #28 Phase 1: MigrateUp to Release_2.0 (Ok).
    /// Phase 2: MigrateUp all with error in R3/F3 and Rollback.
    /// Rollback only touches Phase 2's records (R3/F1, R3/F2). R1+R2 untouched from Run 1.
    /// </summary>
    [Fact]
    public async Task R1R2First_R3Error_Rollback_OnlyCurrentRunRolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BuildAsync();

        // Phase 1: Migrate up to Release_2.0 (error file in R3 not reached)
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Rebuild and migrate all -- hits error in R3/F3
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Error
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // R1+R2: Migrated from Run 1 (untouched by rollback)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3: Rolled back (Run 2)
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4: Never reached (no records)
    }

    /// <summary>
    /// #29 Same as #28 but with RollbackRelease. Result identical because only R3
    /// was in Run 2, so RollbackRelease and Rollback produce the same outcome.
    /// </summary>
    [Fact]
    public async Task R1R2First_R3Error_RollbackRelease_SameAsRollback()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BuildAsync();

        // Phase 1: Migrate up to Release_2.0
        await ctx.MigrateUpAsync("Release_2.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Rebuild and migrate all -- hits error in R3/F3
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Error
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // R1+R2: Migrated from Run 1 (untouched)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3: Rolled back
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4: Never reached (no records)
    }

    /// <summary>
    /// #30 Phase 1: MigrateUp to Release_1.0 (Ok).
    /// Phase 2: MigrateUp all with error in R3/F3 and Rollback.
    /// Rollback touches all of Run 2's records: R2 and R3.
    /// </summary>
    [Fact]
    public async Task R1First_R2R3Error_Rollback_R2AndR3RolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BuildAsync();

        // Phase 1: Migrate up to Release_1.0
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Rebuild and migrate all -- hits error in R3/F3
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Error
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // R1: Migrated from Run 1 (untouched)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2: Rolled back (was in Run 2's successfullyMigratedRecords)
            ("01_CreateTableC.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3: Rolled back
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4: Never reached (no records)
    }

    /// <summary>
    /// #31 Phase 1: MigrateUp to Release_1.0 (Ok).
    /// Phase 2: MigrateUp all with error in R3/F3 and RollbackRelease.
    /// Only R3 is rolled back; R2 stays Migrated (different release from failure).
    /// </summary>
    [Fact]
    public async Task R1First_R2R3Error_RollbackRelease_OnlyR3RolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BuildAsync();

        // Phase 1: Migrate up to Release_1.0
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Rebuild and migrate all -- hits error in R3/F3
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Error
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // R1: Migrated from Run 1
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2: Migrated from Run 2 (NOT rolled back -- different release from error)
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3: Rolled back (same release as error)
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4: Never reached (no records)
    }
}
