using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Testing;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

/// <summary>
/// Block-level resume after a failed migration. A Failed record stores the number of blocks that were
/// SUCCESSFULLY migrated in <c>FileUpBlocksMigrated</c>; the next run skips exactly that many blocks and
/// re-executes the block that failed. Both the atomic path (repository and target share one connection, the
/// whole file is rolled back) and the block-by-block path (target on another database) are covered.
/// </summary>
[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "MigrateUp")]
public class SqlServerResumeTests : SqlServerTestBase
{
    public SqlServerResumeTests(SqlServerFixture fixture) : base(fixture) { }

    // Block 1 (0-based) of 03_SeedDataA.sql is replaced by an INSERT into a table that does not exist during
    // the first run. The file hash stays the same between the runs, so the resume path applies once the
    // table has been created between the runs.
    private const string BlockIntoMissingTable = "INSERT INTO [dbo].[ResumeProbe] ([Name]) VALUES ('from block 2')";
    private const string CreateResumeProbe = "CREATE TABLE [dbo].[ResumeProbe] ([Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY, [Name] VARCHAR(100) NOT NULL)";

    /// <summary>
    /// Atomic path (repository == target, UseTransaction=true): the failed run commits nothing, so the record
    /// must store 0 migrated blocks and the second run must execute all three blocks.
    /// </summary>
    [Fact]
    public async Task FailedBlock_AtomicPath_SecondRunExecutesAllBlocks()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .ReplaceBlock("Release_1.0", "03_SeedDataA.sql", 1, BlockIntoMissingTable)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        // Run 1: block 2 fails, the transaction rolls blocks 1 and 2 back
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRowCount("TableA", 0);
        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 0,
            FileUpBlocksTotal = 3
        });

        // Between the runs: make block 2 executable without touching the file
        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString, CreateResumeProbe);

        // Run 2: nothing was committed, so every block has to run again
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);

        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.Migrated);
        ctx.AssertRowCount("ResumeProbe", 1);
        ctx.AssertRowCount("TableA", 2);
        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Migrated,
            FileUpBlocksMigrated = 3,
            FileUpBlocksTotal = 3
        });
    }

    /// <summary>
    /// Block-by-block path (target on Backend_2, repository on Backend_1): block 1 is committed, block 2 fails,
    /// so the record must store 1 migrated block and the second run must resume with block 2.
    /// </summary>
    [Fact]
    public async Task FailedBlock_BlockByBlockPath_SecondRunResumesWithFailedBlock()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        string target = Fixture.EngineConfig.ConnectionString2!;
        DatabaseCleanupHelper.CleanDatabase(Fixture.EngineConfig.DatabaseType, target, Fixture.EngineConfig.SchemaName);

        await using var ctx = await CreateScenario()
            .WithTargetConnectionString(target)
            .ReplaceBlock("Release_1.0", "03_SeedDataA.sql", 1, BlockIntoMissingTable)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        // Run 1: block 1 committed, block 2 fails
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRowCountOnConnection(target, "TableA", 1);
        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Failed,
            FileUpBlocksMigrated = 1,
            FileUpBlocksTotal = 3
        });

        // Between the runs: make block 2 executable without touching the file
        ctx.ExecuteOnConnection(target, CreateResumeProbe);

        // Run 2: resume with block 2, then block 3
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);

        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.Migrated);
        ctx.AssertRowCountOnConnection(target, "ResumeProbe", 1);
        ctx.AssertRowCountOnConnection(target, "TableA", 2);
        ctx.AssertMigrationRecord("03_SeedDataA.sql", new MigrationRecordExpectation
        {
            MigrationStatusId = (int)MigrationStatus.Migrated,
            FileUpBlocksMigrated = 3,
            FileUpBlocksTotal = 3
        });
    }

    /// <summary>
    /// Guard: with MigrationErrorAction=Ignore every block is attempted and the failed ones are skipped, so the
    /// successfully migrated blocks are not a contiguous prefix. Such a record must never be resumed: the
    /// second run re-executes the whole file. (Passes today; must keep passing after the resume fix.)
    /// </summary>
    [Fact]
    public async Task FailedBlock_IgnoreMode_SecondRunReExecutesWholeFile()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .ReplaceBlock("Release_1.0", "03_SeedDataA.sql", 1, BlockIntoMissingTable)
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();

        // Run 1: blocks 1 and 3 succeed, block 2 is skipped, file is Failed, run continues
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.PartialSuccess);
        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.Failed);
        ctx.AssertRowCount("TableA", 2);

        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString, CreateResumeProbe);

        // Run 2: no resume, the whole file runs again
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunResult(MigrationRunResult.Ok);

        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.Migrated);
        ctx.AssertRowCount("ResumeProbe", 1);
        ctx.AssertRowCount("TableA", 4);
    }
}
