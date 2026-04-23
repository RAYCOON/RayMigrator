// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateDown;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "MigrateDown")]
public class SqlServerErrorTests : SqlServerTestBase
{
    public SqlServerErrorTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// #39 Phase 1: MigrateUp all (RequireRollbackFile=false). Between phases: delete R2/F1 rollback.
    /// Phase 2: MigrateDown to Release_1.0. Missing rollback skipped, chain continues.
    /// R1 stays Migrated, R2/F1=Migrated, R2/F2+F3=NotMigrated, R3+R4=NotMigrated.
    /// </summary>
    [Fact]
    public async Task MissingRollback_RequireFalse_SkipAndContinue()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRequireRollbackFile(false)
            .BuildAsync();

        // Phase 1: Migrate up all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Between phases: delete R2/F1 rollback file
        string rollbackPath = Path.Combine(ctx.WorkDirectory, "Release_2.0", "Backend", "01_CreateTableC.rollback.sql");
        File.Delete(rollbackPath);

        // Phase 2: Migrate down to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true); // RequireRollbackFile=false → AddWarning (not AddFailure) → Success=true
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Ok (missing rollback with RequireRB=false is a warning, not a failure)
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // R1: Still Migrated (not in rollback scope)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1: Migrated (missing rollback, data remains in database — overall run is Ok)
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            // R2/F2+F3: NotMigrated (successfully rolled back)
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3+R4: NotMigrated (successfully rolled back)
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableG.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableH.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataD.sql", MigrationStatus.NotMigrated)
        );
    }

    /// <summary>
    /// #40 Phase 1: MigrateUp all (RequireRollbackFile=false). Between phases: delete R2/F1 rollback,
    /// update config to RequireRollbackFile=true. Phase 2: MigrateDown to Release_1.0.
    /// Chain aborted at R2/F1 (missing and required). R2/F2-R4 already rolled back before R2/F1.
    /// R1=Migrated, R2/F1=Failed, R2/F2+F3-R4=NotMigrated.
    /// </summary>
    [Fact]
    public async Task MissingRollback_RequireTrue_ChainAborted()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRequireRollbackFile(false)
            .BuildAsync();

        // Phase 1: Migrate up all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Between phases: delete R2/F1 rollback file
        string rollbackPath = Path.Combine(ctx.WorkDirectory, "Release_2.0", "Backend", "01_CreateTableC.rollback.sql");
        File.Delete(rollbackPath);

        // Between phases: update config to RequireRollbackFile=true
        string configPath = Path.Combine(ctx.WorkDirectory, "appsettings.json");
        string config = File.ReadAllText(configPath);
        config = config.Replace("\"RequireRollbackFile\": false", "\"RequireRollbackFile\": true");
        File.WriteAllText(configPath, config);

        // Phase 2: Migrate down to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Error
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // R1: Migrated (never reached in rollback chain)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1: Failed (chain aborted due to missing required rollback)
            ("01_CreateTableC.sql", MigrationStatus.Failed),
            // R2/F2+F3: NotMigrated (rolled back before abort)
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3+R4: NotMigrated (rolled back before R2/F1 was reached)
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableG.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableH.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataD.sql", MigrationStatus.NotMigrated)
        );
    }

    /// <summary>
    /// #41 Phase 1: MigrateUp all. Between phases: break R2/F1 rollback.
    /// Phase 2: MigrateDown to Release_1.0 with RollbackErrorAction=Terminate.
    /// Chain aborted at R2/F1 (broken SQL). R1=Migrated, R2/F1=Failed, rest=NotMigrated.
    /// </summary>
    [Fact]
    public async Task BrokenRollback_Terminate_ChainAborted()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRollbackErrorAction(RollbackErrorAction.Terminate)
            .BuildAsync();

        // Phase 1: Migrate up all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Between phases: break R2/F1 rollback with invalid SQL
        string rollbackPath = Path.Combine(ctx.WorkDirectory, "Release_2.0", "Backend", "01_CreateTableC.rollback.sql");
        File.WriteAllText(rollbackPath,
            """
            /*
            [RayMigrator]
            Description = "Broken rollback"
            UseTransaction = true
            */

            DROP TABLE [dbo].[NonExistentTable_BrokenRollback];
            """);

        // Phase 2: Migrate down to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Error
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });

        // R1: Migrated (chain aborted before reaching R1)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1: Failed (broken rollback SQL)
            ("01_CreateTableC.sql", MigrationStatus.Failed),
            // R2/F2+F3: NotMigrated (rolled back before R2/F1)
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3+R4: NotMigrated (rolled back)
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableG.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableH.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataD.sql", MigrationStatus.NotMigrated)
        );
    }

    /// <summary>
    /// #42 Same as #41 but RollbackErrorAction=Ignore. Chain continues past broken R2/F1.
    /// R1 reached and rolled back. R2/F1=Failed, all others=NotMigrated.
    /// </summary>
    [Fact]
    public async Task BrokenRollback_Ignore_ChainContinues()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithRollbackErrorAction(RollbackErrorAction.Ignore)
            .BuildAsync();

        // Phase 1: Migrate up all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Between phases: break R2/F1 rollback with invalid SQL
        string rollbackPath = Path.Combine(ctx.WorkDirectory, "Release_2.0", "Backend", "01_CreateTableC.rollback.sql");
        File.WriteAllText(rollbackPath,
            """
            /*
            [RayMigrator]
            Description = "Broken rollback"
            UseTransaction = true
            */

            DROP TABLE [dbo].[NonExistentTable_BrokenRollback];
            """);

        // Phase 2: Migrate down to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true); // RollbackErrorAction=Ignore → AddWarning (not AddFailure) → Success=true
        ctx.AssertRunCount(2);

        // Run 1 Ok, Run 2 Ok (broken rollback with Ignore is a warning, not a failure)
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // R1: Migrated (R1 IS the target release — not included in rollback chain)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2/F1: Failed (rollback block failed, error ignored — but overall run is Ok)
            ("01_CreateTableC.sql", MigrationStatus.Failed),
            // R2/F2+F3: NotMigrated
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3+R4: NotMigrated
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated),
            ("01_CreateTableG.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableH.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataD.sql", MigrationStatus.NotMigrated)
        );
    }
}
