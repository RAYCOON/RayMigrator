// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Compound;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Compound")]
public class SqliteRoundTripTests : SqliteTestBase
{
    public SqliteRoundTripTests(SqliteFixture fixture) : base(fixture) { }

    /// <summary>
    /// #46 Up all (Ok) -> Down to R1 (Ok) -> Up all (Ok).
    /// Final state: all 12 files Migrated, all tables exist. 3 runs (Ok, Ok, Ok).
    /// </summary>
    [Fact]
    public async Task UpDownUp_AllMigratedAgain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .BuildAsync();

        // Phase 1: Migrate up all
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(1);

        // Phase 2: Migrate down to Release_1.0
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Phase 3: Migrate up all again
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(3);

        // 3 runs all Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });
        ctx.AssertMigrationRun(3, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // All 12 files Migrated
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

        // All 8 tables exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", true);
        ctx.AssertTableExists("tabled", true);
        ctx.AssertTableExists("tablee", true);
        ctx.AssertTableExists("tablef", true);
        ctx.AssertTableExists("tableg", true);
        ctx.AssertTableExists("tableh", true);
    }

    /// <summary>
    /// #47 Phase 1: MigrateUp all with error in R3/F3 and RollbackRelease.
    /// R1+R2=Migrated, R3=NotMigrated.
    /// Phase 2: MigrateDown to Release_1.0 (rolls back R2).
    /// Final: R1=Migrated, R2+R3=NotMigrated, R4=NoRecord. Runs: Err, Ok.
    /// </summary>
    [Fact]
    public async Task UpErrorRollbackRelease_ThenMigrateDown()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
            .InjectError("Release_3.0", "03_SeedDataC.sql")
            .BuildAsync();

        // Phase 1: MigrateUp all -- error at R3/F3, RollbackRelease rolls back R3
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);
        ctx.AssertRunCount(1);

        // Phase 2: MigrateDown to Release_1.0 (rolls back R2)
        await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_1.0");
        await ctx.MigrateDownAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRunCount(2);

        // Run 1 Error, Run 2 Ok
        ctx.AssertMigrationRun(1, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Error });
        ctx.AssertMigrationRun(2, new MigrationRunExpectation { MigrationRunResultId = (int)MigrationRunResult.Ok });

        // R1: Migrated (untouched)
        ctx.AssertFileStatuses(
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Migrated),
            // R2: NotMigrated (rolled back in Phase 2)
            ("01_CreateTableC.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableD.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3: NotMigrated (rolled back in Phase 1 by RollbackRelease)
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataC.sql", MigrationStatus.NotMigrated)
        );
        // R4: Never reached (no records)

        // Only R1 tables exist
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tableb", true);
        ctx.AssertTableExists("tablec", false);
        ctx.AssertTableExists("tabled", false);
        ctx.AssertTableExists("tablee", false);
        ctx.AssertTableExists("tablef", false);
    }
}
