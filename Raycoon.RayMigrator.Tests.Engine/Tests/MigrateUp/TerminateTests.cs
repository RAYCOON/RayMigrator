using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "MigrateUp")]
public class TerminateTests : PostgreSqlTestBase
{
    public TerminateTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// #3 Error injected into the very first file. Only R1/F1 gets a Failed record,
    /// all other files have no records. No tables exist.
    /// </summary>
    [Fact]
    public async Task ErrorInFirstFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_1.0", "01_CreateTableA.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // Only R1/F1 has a record (Failed)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Failed)
        );

        // No tables should exist
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
    /// #4 Error in the middle of Release_2.0. R1 fully migrated, R2/F1 migrated,
    /// R2/F2 failed, rest have no records.
    /// </summary>
    [Fact]
    public async Task ErrorInMiddleRelease()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // R1 fully migrated, R2/F1 migrated, R2/F2 failed
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Failed)
        );

        // R1 tables exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", true);

        // R2/F2 table does NOT exist (error before creation)
        ctx.AssertTableExists("tabled", false);

        // R3+R4 tables do NOT exist
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
        ctx.AssertTableExists("tableg", false);
        ctx.AssertTableExists("tableh", false);
    }

    /// <summary>
    /// #5 Error in the very last file (R4/F3 seed data). R1-R3 fully migrated,
    /// R4/F1+F2 migrated, R4/F3 failed. All tables exist but seed data for tableg is missing.
    /// </summary>
    [Fact]
    public async Task ErrorInLastFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_4.0", "03_SeedDataD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // R1-R3 fully migrated, R4/F1+F2 migrated, R4/F3 failed
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
            ("03_SeedDataD.sql", MigrationStatus.Failed)
        );

        // All 8 tables exist (CREATE TABLE succeeded, only seed data failed)
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", true);
        ctx.AssertTableExists("tabled", true);
        ctx.AssertTableExists("tablee", true);
        ctx.AssertTableExists("tablef", true);
        ctx.AssertTableExists("tableg", true);
        ctx.AssertTableExists("tableh", true);

        // Seed data for tableg is missing (INSERT rolled back by transaction on PostgreSQL)
        ctx.AssertRowCount("tableg", 0);
    }
}
