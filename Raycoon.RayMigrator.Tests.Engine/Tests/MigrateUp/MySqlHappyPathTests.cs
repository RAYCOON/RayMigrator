using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "MigrateUp")]
public class MySqlHappyPathTests : MySqlTestBase
{
    public MySqlHappyPathTests(MySqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// #1 All four releases with no errors. All 12 files should be Migrated,
    /// all 8 tables should exist, and seed tables should have 3 rows each.
    /// </summary>
    [Fact]
    public async Task AllFourReleases_AllSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertRunCount(1);

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

        // Seed tables should have 3 rows each
        ctx.AssertRowCount("tablea", 3);
        ctx.AssertRowCount("tablec", 3);
        ctx.AssertRowCount("tablee", 3);
        ctx.AssertRowCount("tableg", 3);
    }

    /// <summary>
    /// #2 Only first two releases are migrated via toRelease parameter.
    /// R1+R2 files should be Migrated, R3+R4 should have no records.
    /// </summary>
    [Fact]
    public async Task TwoReleases_PartialMigration()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_2.0");

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertRunCount(1);

        // R1 + R2 files should be Migrated
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated)
        );

        // R1+R2 tables should exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", true);
        ctx.AssertTableExists("tabled", true);

        // R3+R4 tables should NOT exist
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }
}
