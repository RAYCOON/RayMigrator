
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "MigrateUp")]
public class SqlServerFlatLayoutTests : SqlServerTestBase
{
    public SqlServerFlatLayoutTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// Mixed layout: Release_1.0 and Release_3.0 use flat layout (files directly under release dir),
    /// Release_2.0 and Release_4.0 use traditional layout (Backend/ subdirectory).
    /// All files should migrate successfully regardless of layout.
    /// </summary>
    [Fact]
    public async Task MixedFlatAndTraditionalLayout_AllReleasesMigrateSuccessfully()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithFlatLayoutForRelease("Release_1.0")
            .WithFlatLayoutForRelease("Release_3.0")
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertRunCount(1);

        // All 12 files should be Migrated (regardless of layout)
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

        // All tables exist
        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableB", true);
        ctx.AssertTableExists("TableC", true);
        ctx.AssertTableExists("TableD", true);
        ctx.AssertTableExists("TableE", true);
        ctx.AssertTableExists("TableF", true);
        ctx.AssertTableExists("TableG", true);
        ctx.AssertTableExists("TableH", true);

        // Seed data
        ctx.AssertRowCount("TableA", 3);
        ctx.AssertRowCount("TableC", 3);
        ctx.AssertRowCount("TableE", 3);
        ctx.AssertRowCount("TableG", 3);
    }

    /// <summary>
    /// After MigrateUp with mixed layout, MigrateDown should correctly find
    /// rollback files in both flat and traditional layouts.
    /// </summary>
    [Fact]
    public async Task MixedLayout_MigrateDownToRelease2_RollsBackFlatAndTraditionalReleases()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithFlatLayoutForRelease("Release_1.0")
            .WithFlatLayoutForRelease("Release_3.0")
            .BuildAsync();

        // First: migrate up all releases
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        // Then: migrate down to Release_2.0 (should rollback Release_4.0 traditional + Release_3.0 flat)
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0");
        await ctx.MigrateDownAsync("Release_2.0");

        ctx.AssertSuccess(true);

        // R1+R2 tables should still exist
        ctx.AssertTableExists("TableA", true);
        ctx.AssertTableExists("TableB", true);
        ctx.AssertTableExists("TableC", true);
        ctx.AssertTableExists("TableD", true);

        // R3+R4 tables should be dropped by rollback
        ctx.AssertTableExists("TableE", false);
        ctx.AssertTableExists("TableF", false);
        ctx.AssertTableExists("TableG", false);
        ctx.AssertTableExists("TableH", false);
    }
}
