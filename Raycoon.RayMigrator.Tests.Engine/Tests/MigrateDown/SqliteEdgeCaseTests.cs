
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateDown;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "MigrateDown")]
public class SqliteEdgeCaseTests : SqliteTestBase
{
    public SqliteEdgeCaseTests(SqliteFixture fixture) : base(fixture) { }

    /// <summary>
    /// #43 Phase 1: MigrateUp to Release_1.0. Phase 2: MigrateDown to Release_1.0.
    /// No files to roll back (already at target). R1 stays Migrated. Run 2=Ok.
    /// </summary>
    [Fact]
    public async Task NoOp_AlreadyAtTarget()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Migrate up to Release_1.0
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Migrate down to Release_1.0 (no-op)
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Both runs Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // R1 stays Migrated
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated)
        );

        // R1 tables still exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
    }

    /// <summary>
    /// #44 Phase 1: MigrateUp to Release_1.0. Phase 2: MigrateDown to Release_2.0.
    /// No files qualify (R1 is below R2 target). R1 stays Migrated. Run 2=Ok.
    /// </summary>
    [Fact]
    public async Task NoOp_BelowTarget()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Migrate up to Release_1.0
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Migrate down to Release_2.0 (R1 < R2, so nothing to do)
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        await ctx.MigrateDownAsync("Release_2.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Both runs Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // R1 stays Migrated
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated)
        );

        // R1 tables still exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
    }

    /// <summary>
    /// #45 Phase 1: MigrateUp all. Phase 2: MigrateDown to Release_2.0 (R3+R4 NM).
    /// Phase 3: MigrateDown to Release_1.0 (R2 NM). 3 runs (Ok, Ok, Ok).
    /// </summary>
    [Fact]
    public async Task PartialDown_ToR2_ThenDown_ToR1()
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

        // Phase 3: Migrate down to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(3);

        // 3 runs all Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(3, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // R1: Still Migrated
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2: NotMigrated (rolled back in Phase 3)
            ("01_CreateTableC.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3+R4: NotMigrated (rolled back in Phase 2)
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
}
