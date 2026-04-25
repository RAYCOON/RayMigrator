using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "MigrateUp")]
public class SqlServerRunAlwaysTests : SqlServerTestBase
{
    public SqlServerRunAlwaysTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// #56 R1/F3 has RunAlways=true. Phase 1: MigrateUp all (Ok).
    /// Phase 2: MigrateUp all -- R1/F3 re-executed (Ok).
    /// 2 runs (Ok, Ok). R1/F3 has records from both runs (latest is Migrated).
    /// </summary>
    [Fact]
    public async Task RunAlways_ReExecutedOnSecondRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetFileToml("Release_1.0", "03_SeedDataA.sql", "RunAlways", "true")
            .BuildAsync();

        // Phase 1: MigrateUp all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: MigrateUp all -- R1/F3 re-executed because RunAlways=true
        // AllowOutOfOrder needed because RunAlways file from R1 must re-execute while R4 is already applied
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync(allowOutOfOrder: true);
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Both runs Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // All files Migrated (R1/F3 re-executed in Run 2, latest status is Migrated)
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

        // Seed data re-inserted: TableA should have 6 rows (3 from Run 1 + 3 from Run 2)
        ctx.AssertRowCount("TableA", 6);
    }

    /// <summary>
    /// #57 R1/F3 has RunAlways=true and uses explicit PK that duplicates on rerun.
    /// Phase 1: MigrateUp all (Ok). Phase 2: MigrateUp all -- R1/F3 fails (duplicate key).
    /// R1/F3 = Failed from Run 2. Other files = Migrated from Run 1. Run 2 = Error.
    /// </summary>
    [Fact]
    public async Task RunAlways_FailsOnRerun_Terminate()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetFileToml("Release_1.0", "03_SeedDataA.sql", "RunAlways", "true")
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        // Replace R1/F3 with explicit PK insert that will duplicate on second run.
        // All statements in a single batch (no GO) so IDENTITY_INSERT stays ON for the INSERT.
        string seedPath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "03_SeedDataA.sql");
        File.WriteAllText(seedPath,
            """
            /*
            [RayMigrator]
            Description = "Seed with explicit PK - fails on rerun"
            Environments = ["*"]
            Targets = ["*"]
            UseTransaction = true
            RunAlways = true
            */

            SET IDENTITY_INSERT [dbo].[TableA] ON;
            INSERT INTO [dbo].[TableA] ([Id], [Name], [Value]) VALUES (999, 'runalways_test', 10);
            SET IDENTITY_INSERT [dbo].[TableA] OFF;
            """);

        // Phase 1: MigrateUp all -- explicit PK insert succeeds (id 999 unique)
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: MigrateUp all -- R1/F3 re-executed, fails (duplicate key 999)
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync(allowOutOfOrder: true);
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Error
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // R1/F3: Failed from Run 2 (duplicate key)
        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.Failed);

        // Other files: still Migrated from Run 1 (unchanged)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
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

    /// <summary>
    /// #58 Same as #57 but with Rollback. Run 2 only has R1/F3 in its
    /// successfullyMigratedRecords (nothing else new), so rollback scope is empty/R1/F3 only.
    /// R1/F3 = NotMigrated. Other files = Migrated from Run 1. Run 2 = Error.
    /// </summary>
    [Fact]
    public async Task RunAlways_FailsOnRerun_Rollback()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .SetFileToml("Release_1.0", "03_SeedDataA.sql", "RunAlways", "true")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();

        // Replace R1/F3 with explicit PK insert that will duplicate on second run.
        // All statements in a single batch (no GO) so IDENTITY_INSERT stays ON for the INSERT.
        string seedPath = Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "03_SeedDataA.sql");
        File.WriteAllText(seedPath,
            """
            /*
            [RayMigrator]
            Description = "Seed with explicit PK - fails on rerun"
            Environments = ["*"]
            Targets = ["*"]
            UseTransaction = true
            RunAlways = true
            */

            SET IDENTITY_INSERT [dbo].[TableA] ON;
            INSERT INTO [dbo].[TableA] ([Id], [Name], [Value]) VALUES (999, 'runalways_test', 10);
            SET IDENTITY_INSERT [dbo].[TableA] OFF;
            """);

        // Phase 1: MigrateUp all -- explicit PK insert succeeds
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: MigrateUp all -- R1/F3 re-executed, fails (duplicate key), Rollback
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync(allowOutOfOrder: true);
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Error
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // R1/F3: NotMigrated (rolled back after failure)
        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.NotMigrated);

        // Other files: still Migrated from Run 1 (unchanged by Run 2's rollback)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
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

    /// <summary>
    /// #59 All files have RunAlways=false (default). Phase 1: MigrateUp all (Ok).
    /// Phase 2: MigrateUp all -- nothing to do (Ok). All files still Migrated from Run 1.
    /// 2 runs (Ok, Ok). No new Migration records in Run 2.
    /// </summary>
    [Fact]
    public async Task RunAlwaysFalse_NotReExecuted()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: MigrateUp all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: MigrateUp all again -- RunAlways=false, nothing to do
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

        // Seed tables should still have exactly 3 rows each (not re-inserted)
        ctx.AssertRowCount("TableA", 3);
        ctx.AssertRowCount("TableC", 3);
        ctx.AssertRowCount("TableE", 3);
        ctx.AssertRowCount("TableG", 3);
    }
}
