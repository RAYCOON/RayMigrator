
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "MigrateUp")]
public class MultiTargetTests : PostgreSqlTestBase
{
    public MultiTargetTests(PostgreSqlFixture fixture) : base(fixture) { }

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
}
