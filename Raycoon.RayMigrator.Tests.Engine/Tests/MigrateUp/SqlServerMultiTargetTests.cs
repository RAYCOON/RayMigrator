using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "MigrateUp")]
public class SqlServerMultiTargetTests : SqlServerTestBase
{
    public SqlServerMultiTargetTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// #32 Simultaneously mode with Ignore. Error in R2/F2 fails on T1, T2 skipped for that file.
    /// T1: R2/F2=Failed, all others Migrated. T2: R2/F2=NoRecord (skipped), all others Migrated.
    /// </summary>
    [Fact]
    public async Task Simultaneously_Ignore_SkipsSecondTarget()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithMultiTarget(Fixture.EngineConfig.ConnectionString2!)
            .WithTargetMigrationOrder(TargetMigrationOrder.Simultaneously)
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // T1 (MainDB): R2/F2 Failed, all others Migrated
        ctx.AssertFileStatusForTarget("01_CreateTableA.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableB.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataA.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableC.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "MainDB", MigrationStatus.Failed);
        ctx.AssertFileStatusForTarget("03_SeedDataB.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableE.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableF.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataC.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableG.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableH.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataD.sql", "MainDB", MigrationStatus.Migrated);

        // T2 (SecondDB): R2/F2 has no record (skipped when T1 failed in Simultaneously), all others Migrated
        ctx.AssertFileStatusForTarget("01_CreateTableA.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableB.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataA.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableC.sql", "SecondDB", MigrationStatus.Migrated);
        // R2/F2 on SecondDB: no record (skipped)
        ctx.AssertFileStatusForTarget("03_SeedDataB.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableE.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableF.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataC.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableG.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableH.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataD.sql", "SecondDB", MigrationStatus.Migrated);
    }

    /// <summary>
    /// #33 Simultaneously mode with Rollback. Error in R2/F2 triggers rollback of both targets.
    /// All files on both targets end up as NotMigrated.
    /// </summary>
    [Fact]
    public async Task Simultaneously_Rollback_BothTargets()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithMultiTarget(Fixture.EngineConfig.ConnectionString2!)
            .WithTargetMigrationOrder(TargetMigrationOrder.Simultaneously)
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // Both targets: R1 + R2/F1 files rolled back to NotMigrated
        string[] commonFiles =
        [
            "01_CreateTableA.sql", "02_CreateTableB.sql", "03_SeedDataA.sql",
            "01_CreateTableC.sql"
        ];

        foreach (string target in new[] { "MainDB", "SecondDB" })
        {
            foreach (string file in commonFiles)
            {
                ctx.AssertFileStatusForTarget(file, target, MigrationStatus.NotMigrated);
            }
        }

        // R2/F2 (error file): T1=NotMigrated (rolled back), T2=NoRecord (never attempted in Simultaneously mode)
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "MainDB", MigrationStatus.NotMigrated);
    }

    /// <summary>
    /// #34 Successively mode with Terminate. Error in R2/F2.
    /// T1: R1 Migrated, R2/F1 Migrated, R2/F2 Failed, rest no records.
    /// T2: R1 Migrated, R2 never started (no records for R2+).
    /// </summary>
    [Fact]
    public async Task Successively_Terminate_SecondTargetNeverStarts()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithMultiTarget(Fixture.EngineConfig.ConnectionString2!)
            .WithTargetMigrationOrder(TargetMigrationOrder.Successively)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // T1 (MainDB): R1 Migrated, R2/F1 Migrated, R2/F2 Failed
        ctx.AssertFileStatusForTarget("01_CreateTableA.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableB.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataA.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableC.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "MainDB", MigrationStatus.Failed);
        // Rest of T1: no records

        // T2 (SecondDB): R1 Migrated (completed before error), R2+ never started
        ctx.AssertFileStatusForTarget("01_CreateTableA.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableB.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataA.sql", "SecondDB", MigrationStatus.Migrated);
        // R2+ on SecondDB: no records (never started R2 on T2)
    }

    /// <summary>
    /// #35 Successively mode with Rollback. Error in R2/F2.
    /// Both targets: all attempted files end up as NotMigrated.
    /// </summary>
    [Fact]
    public async Task Successively_Rollback_BothTargets()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithMultiTarget(Fixture.EngineConfig.ConnectionString2!)
            .WithTargetMigrationOrder(TargetMigrationOrder.Successively)
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        // T1 (MainDB): R1+R2 files attempted, all NotMigrated after rollback
        ctx.AssertFileStatusForTarget("01_CreateTableA.sql", "MainDB", MigrationStatus.NotMigrated);
        ctx.AssertFileStatusForTarget("02_CreateTableB.sql", "MainDB", MigrationStatus.NotMigrated);
        ctx.AssertFileStatusForTarget("03_SeedDataA.sql", "MainDB", MigrationStatus.NotMigrated);
        ctx.AssertFileStatusForTarget("01_CreateTableC.sql", "MainDB", MigrationStatus.NotMigrated);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "MainDB", MigrationStatus.NotMigrated);

        // T2 (SecondDB): R1 files attempted, all NotMigrated after rollback
        ctx.AssertFileStatusForTarget("01_CreateTableA.sql", "SecondDB", MigrationStatus.NotMigrated);
        ctx.AssertFileStatusForTarget("02_CreateTableB.sql", "SecondDB", MigrationStatus.NotMigrated);
        ctx.AssertFileStatusForTarget("03_SeedDataA.sql", "SecondDB", MigrationStatus.NotMigrated);
    }

    #region A target that failed while another succeeded is retried (#8)

    /// <summary>
    /// #8: "already migrated" used to be decided per file (any Migrated record). With two targets, a file
    /// that succeeded on MainDB and failed on SecondDB was never retried on SecondDB. The failure is
    /// provoked on the second target only (a conflicting table), so the migration file itself and its hash
    /// stay unchanged between the two runs. Execution is release by release: after the failure MainDB has
    /// Release_1.0 + Release_2.0 (6 records), SecondDB has Release_1.0, R2/F1 and the failed R2/F2 (5 records).
    /// </summary>
    [Fact]
    public async Task Successively_Terminate_SecondTargetFails_RetryMigratesOnlyThePendingPairs()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");
        string secondDb = Fixture.EngineConfig.ConnectionString2!;
        string dbType = Fixture.EngineConfig.DatabaseType;

        await using var ctx = await CreateScenario()
            .WithMultiTarget(secondDb)
            .WithTargetMigrationOrder(TargetMigrationOrder.Successively)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();

        // A conflicting "tabled" on SecondDB makes Release_2.0/02_CreateTableD.sql fail there only.
        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetCreateSimpleTableSql(dbType, "tabled"));

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataB.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "SecondDB", MigrationStatus.Failed);
        ctx.CountMigrations().Should().Be(11, "MainDB: R1 + R2 (6), SecondDB: R1 + R2/F1 + failed R2/F2 (5)");

        // info must see the lagging target: R2/F2 + R2/F3 on SecondDB, R3 (3) + R4 (3) on both = 14 pairs
        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();
        status.PendingMigrations.Should().Be(14, "pending is counted per (file, target) pair (#8)");

        // Remove the cause and run again: MainDB's R2 files must not be re-executed.
        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetDropSimpleTableSql(dbType, "tabled"));
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var retry = await ctx.MigrateUpAsync();

        retry.Success.Should().BeTrue($"the retry must migrate the lagging target: {retry.ErrorMessage}");
        retry.TotalMigrations.Should().Be(14, "exactly the 14 pending (file, target) pairs were executed; MainDB's R2 files were skipped (#8)");
        retry.MigrationResults.Should().OnlyContain(r => r.Success);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataB.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableE.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("01_CreateTableE.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataD.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataD.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertTableExistsOnConnection(secondDb, "tabled", true);
        ctx.CountMigrations().Should().Be(24, "12 files x 2 targets, the failed record was reused");
        ctx.AssertRunCount(2);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        (await ctx.InfoAsync()).PendingMigrations.Should().Be(0);
    }

    /// <summary>
    /// Same failure in Simultaneously order (file by file, MainDB first): after the failure both targets
    /// hold Release_1.0 + R2/F1, MainDB also R2/F2, SecondDB the failed R2/F2 (10 records). Pending pairs:
    /// R2/F2 on SecondDB, R2/F3 on both, R3 + R4 on both = 1 + 2 + 12 = 15.
    /// </summary>
    [Fact]
    public async Task Simultaneously_Terminate_SecondTargetFails_RetryMigratesOnlyThePendingPairs()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");
        string secondDb = Fixture.EngineConfig.ConnectionString2!;
        string dbType = Fixture.EngineConfig.DatabaseType;

        await using var ctx = await CreateScenario()
            .WithMultiTarget(secondDb)
            .WithTargetMigrationOrder(TargetMigrationOrder.Simultaneously)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();
        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetCreateSimpleTableSql(dbType, "tabled"));

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "SecondDB", MigrationStatus.Failed);
        ctx.CountMigrations().Should().Be(10, "both targets: R1 (3) + R2/F1; MainDB: R2/F2 migrated; SecondDB: R2/F2 failed");

        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetDropSimpleTableSql(dbType, "tabled"));
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var retry = await ctx.MigrateUpAsync();

        retry.Success.Should().BeTrue($"the retry must migrate the lagging target: {retry.ErrorMessage}");
        // Simultaneously reports one result per file (Successively one per file and target): R2/F2, R2/F3, R3 (3), R4 (3)
        retry.TotalMigrations.Should().Be(8, "8 files still had at least one pending target; MainDB's R2/F2 was skipped (#8)");
        retry.MigrationResults.Should().OnlyContain(r => r.Success);
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataB.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataB.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataD.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataD.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.CountMigrations().Should().Be(24);
        ctx.AssertRunCount(2);
    }

    [Fact]
    public async Task Successively_Terminate_SecondTargetFails_ThirdRunHasNothingToDo()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");
        string secondDb = Fixture.EngineConfig.ConnectionString2!;
        string dbType = Fixture.EngineConfig.DatabaseType;

        await using var ctx = await CreateScenario()
            .WithMultiTarget(secondDb)
            .WithTargetMigrationOrder(TargetMigrationOrder.Successively)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();
        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetCreateSimpleTableSql(dbType, "tabled"));
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetDropSimpleTableSql(dbType, "tabled"));
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var third = await ctx.MigrateUpAsync();

        third.Success.Should().BeTrue();
        third.TotalMigrations.Should().Be(0, "both targets are complete, nothing may be re-executed");
        ctx.CountMigrations().Should().Be(24);
    }

    [Fact]
    public async Task Successively_Terminate_SecondTargetFails_BaselineMarksOnlyThePendingPairs()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");
        string secondDb = Fixture.EngineConfig.ConnectionString2!;
        string dbType = Fixture.EngineConfig.DatabaseType;

        await using var ctx = await CreateScenario()
            .WithMultiTarget(secondDb)
            .WithTargetMigrationOrder(TargetMigrationOrder.Successively)
            .WithMigrationErrorAction(MigrationErrorAction.Terminate)
            .BuildAsync();
        ctx.ExecuteOnConnection(secondDb, SqlDialect.GetCreateSimpleTableSql(dbType, "tabled"));
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.CountMigrations().Should().Be(11);

        await ctx.RebuildForAsync(MigrationCommand.Baseline, MigrationRunMode.Migrate);
        var baseline = await ctx.BaselineAsync();

        baseline.Success.Should().BeTrue($"baseline should succeed: {baseline.ErrorMessage}");
        baseline.BaselinedFiles.Should().Be(8, "R2/F2 + R2/F3 (pending on SecondDB) and the 6 files of R3 + R4 (pending on both)");
        ctx.AssertFileStatusForTarget("02_CreateTableD.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataD.sql", "MainDB", MigrationStatus.Migrated);
        ctx.AssertFileStatusForTarget("03_SeedDataD.sql", "SecondDB", MigrationStatus.Migrated);
        ctx.CountMigrations().Should().Be(24, "the 13 missing records were added and the failed one was reused");
        ctx.AssertTableExistsOnConnection(secondDb, "tablee", false); // baseline records only, it does not execute SQL
    }

    /// <summary>
    /// validate-hash reports a deleted file once, not once per target record (#8).
    /// </summary>
    [Fact]
    public async Task ValidateHash_TwoTargets_DeletedFile_IsReportedMissingOnce()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        Assert.SkipWhen(Fixture.EngineConfig.ConnectionString2 is null, "Second connection string not configured");

        await using var ctx = await CreateScenario()
            .WithMultiTarget(Fixture.EngineConfig.ConnectionString2!)
            .BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.CountMigrations().Should().Be(24);

        File.Delete(Path.Combine(ctx.WorkDirectory, "Release_1.0", "Backend", "01_CreateTableA.sql"));

        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Validate);
        var result = await ctx.ValidateHashAsync();

        result.MissingFiles.Should().Be(1, "one file was deleted, it has two records (one per target)");
        result.Issues.Should().ContainSingle(i => i.IssueType == "Missing" && i.FileName == "01_CreateTableA.sql");
        result.ValidFiles.Should().Be(11, "the remaining 11 files are valid on both targets and counted once each");
        result.InvalidFiles.Should().Be(0);
    }

    #endregion
}
