using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateDown;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "MigrateDown")]
public class HappyPathTests : PostgreSqlTestBase
{
    public HappyPathTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// #36 Phase 1: MigrateUp all (Ok). Phase 2: MigrateDown to Release_2.0.
    /// R1+R2 stay Migrated, R3+R4 become NotMigrated. 2 runs (Ok, Ok).
    /// </summary>
    [Fact]
    public async Task ToRelease2_R3R4RolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Migrate up all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Migrate down to Release_2.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        await ctx.MigrateDownAsync("Release_2.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Both runs should be Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // R1+R2: Still Migrated
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3+R4: Rolled back to NotMigrated
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableG.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableH.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataD.sql", MigrationStatus.NotMigrated)
        );

        // R1+R2 tables still exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", true);
        ctx.AssertTableExists("tabled", true);

        // R3+R4 tables dropped by rollback
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }

    /// <summary>
    /// #37 Phase 1: MigrateUp all (Ok). Phase 2: MigrateDown to Release_1.0.
    /// Only R1 stays Migrated, R2+R3+R4 become NotMigrated.
    /// </summary>
    [Fact]
    public async Task ToRelease1_OnlyR1Stays()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Migrate up all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Migrate down to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Both runs Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // R1: Still Migrated
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2+R3+R4: Rolled back to NotMigrated
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

        // Only R1 tables exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }

    /// <summary>
    /// #38 Phase 1: MigrateUp all (Ok). Phase 2: MigrateDown to empty string (full rollback).
    /// All 12 files become NotMigrated. No tables remain.
    /// </summary>
    [Fact]
    public async Task FullRollback_AllReleases()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Migrate up all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Migrate down to empty string (before all releases)
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "");
        await ctx.MigrateDownAsync("");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Both runs Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // All 12 files NotMigrated
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

        // No tables remain
        ctx.AssertTableExists("tablea", false);
        ctx.AssertTableExists("tableb", false);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }
}
