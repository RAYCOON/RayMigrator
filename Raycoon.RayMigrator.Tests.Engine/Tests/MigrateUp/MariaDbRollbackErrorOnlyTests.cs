using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "MigrateUp")]
public class MariaDbRollbackErrorOnlyTests : MariaDbTestBase
{
    public MariaDbRollbackErrorOnlyTests(MariaDbFixture fixture) : base(fixture) { }

    /// <summary>
    /// #21 Error in R2/F2 with RollbackErrorOnly. Only the failed file is rolled back.
    /// R1=Migrated, R2/F1=Migrated, R2/F2=NotMigrated (rolled back), R2/F3-R4=NoRecord.
    /// </summary>
    [Fact]
    public async Task OnlyErrorFileRolledBack()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackErrorOnly)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1 Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1 Migrated
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            // R2/F2 NotMigrated (only this file was rolled back)
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated)
        );
        // R2/F3-R4 have no records
    }

    /// <summary>
    /// #22 Error in R2/F2 with RollbackErrorOnly and broken rollback for the same file.
    /// The rollback fails, so R2/F2 stays Failed.
    /// R1=Migrated, R2/F1=Migrated, R2/F2=Failed, rest=NoRecord.
    /// </summary>
    [Fact]
    public async Task BrokenRollback()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .BreakRollback("Release_2.0", "02_CreateTableD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackErrorOnly)
            .WithRollbackErrorAction(RollbackErrorAction.Terminate)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1 Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1 Migrated
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            // R2/F2 Failed (rollback also failed)
            ("02_CreateTableD.sql", MigrationStatus.Failed)
        );
        // R2/F3-R4 have no records
    }

    /// <summary>
    /// #23 Error in R2/F2 with RollbackErrorOnly, missing rollback file, RequireRollbackFile=false.
    /// The rollback is skipped, so R2/F2 stays Failed.
    /// R1=Migrated, R2/F1=Migrated, R2/F2=Failed, rest=NoRecord.
    /// </summary>
    [Fact]
    public async Task MissingRollback_RequireFalse()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .RemoveRollback("Release_2.0", "02_CreateTableD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.RollbackErrorOnly)
            .WithRequireRollbackFile(false)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1 Migrated
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1 Migrated
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            // R2/F2 Failed (no rollback file available)
            ("02_CreateTableD.sql", MigrationStatus.Failed)
        );
        // R2/F3-R4 have no records
    }
}
