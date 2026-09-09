using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "MigrateUp")]
public class SqlServerBlockLevelTests : SqlServerTestBase
{
    public SqlServerBlockLevelTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// #50 Error at block index 2 (third GO block) of 03_SeedDataA.sql with Terminate.
    /// Blocks 0 and 1 succeed, block 2 fails. Repository and target share one database, so the file runs in one
    /// transaction that is rolled back: nothing is committed, FileUpBlocksMigrated=0 (#11), FileUpBlocksTotal=3, Status=Failed.
    /// </summary>
    [Fact]
    public async Task ErrorAtBlock2Of3_Terminate()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectErrorAtBlock("Release_1.0", "03_SeedDataA.sql", 2)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 0,
            FileUpBlocksTotal = 3
        });
    }

    /// <summary>
    /// #51 Error at block index 1 (second GO block) of 03_SeedDataA.sql with Terminate.
    /// Block 0 succeeds, block 1 fails. Atomic execution rolls block 0 back: FileUpBlocksMigrated=0 (#11), FileUpBlocksTotal=3, Status=Failed.
    /// </summary>
    [Fact]
    public async Task ErrorAtBlock1Of3_Terminate()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectErrorAtBlock("Release_1.0", "03_SeedDataA.sql", 1)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 0,
            FileUpBlocksTotal = 3
        });
    }

    /// <summary>
    /// #52 Error at block index 0 (first GO block) of 03_SeedDataA.sql with Terminate.
    /// No blocks succeed.
    /// FileUpBlocksMigrated=0 (committed blocks, #11), FileUpBlocksTotal=3, Status=Failed.
    /// </summary>
    [Fact]
    public async Task ErrorAtBlock0Of3_Terminate()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectErrorAtBlock("Release_1.0", "03_SeedDataA.sql", 0)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 0,
            FileUpBlocksTotal = 3
        });
    }

    /// <summary>
    /// #53 Error at block index 2 of 03_SeedDataA.sql with Rollback.
    /// After rollback: R1/F3 UpDone=0 (atomic execution committed nothing, #11), UpTotal=3, Status=NotMigrated, DownDone=DownTotal (complete rollback).
    /// R1/F1 and R1/F2 also rolled back to NotMigrated.
    /// </summary>
    [Fact]
    public async Task ErrorAtBlock2Of3_Rollback_CompleteRollbackExecuted()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectErrorAtBlock("Release_1.0", "03_SeedDataA.sql", 2)
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Recovered);
        ctx.AssertRunCount(1);

        // R1/F3: partially migrated then rolled back
        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            FileUpBlocksMigrated = 0,
            FileUpBlocksTotal = 3
        });

        // R1/F1 and R1/F2: rolled back
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated)
        );

        // All tables dropped by rollback
        ctx.AssertTableExists("TableA", false);
        ctx.AssertTableExists("TableB", false);
    }

    /// <summary>
    /// #54 Error at block index 1 of 03_SeedDataA.sql with Rollback.
    /// R1/F3: UpDone=0 (atomic execution committed nothing, #11), UpTotal=3, Status=NotMigrated.
    /// R1/F1 and R1/F2: also NotMigrated (rolled back).
    /// </summary>
    [Fact]
    public async Task ErrorAtBlock1Of3_Rollback_PrecedingFilesAlsoRolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectErrorAtBlock("Release_1.0", "03_SeedDataA.sql", 1)
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Recovered);
        ctx.AssertRunCount(1);

        // R1/F3: partially migrated then rolled back
        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.NotMigrated,
            FileUpBlocksMigrated = 0,
            FileUpBlocksTotal = 3
        });

        // R1/F1 and R1/F2: rolled back
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated)
        );
    }

    /// <summary>
    /// #55 No error injected. All 3 blocks of 03_SeedDataA.sql succeed.
    /// FileUpBlocksMigrated=3, FileUpBlocksTotal=3, Status=Migrated.
    /// </summary>
    [Fact]
    public async Task MultiBlock_AllSucceed_BlockCountCorrect()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);
        ctx.AssertRunCount(1);

        // R1/F3: all 3 blocks migrated
        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Migrated,
            FileUpBlocksMigrated = 3,
            FileUpBlocksTotal = 3
        });

        // TableA should have 3 rows (one per GO block)
        ctx.AssertRowCount("TableA", 3);
    }
}
