// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.MigrateUp;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "MigrateUp")]
public class SqliteIgnoreTests : SqliteTestBase
{
    public SqliteIgnoreTests(SqliteFixture fixture) : base(fixture) { }

    /// <summary>
    /// #24 Single error in R2/F2 with Ignore. Execution continues past the error.
    /// All files get records: R2/F2=Failed, everything else=Migrated. RunResult=Error.
    /// </summary>
    [Fact]
    public async Task SingleError_ExecutionContinues()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
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
            // R2/F1 Migrated, R2/F2 Failed (ignored), R2/F3 Migrated
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Failed),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3 Migrated
            ("01_CreateTableE.sql", MigrationStatus.Migrated),
            ("02_CreateTableF.sql", MigrationStatus.Migrated),
            ("03_SeedDataC.sql", MigrationStatus.Migrated),
            // R4 Migrated
            ("01_CreateTableG.sql", MigrationStatus.Migrated),
            ("02_CreateTableH.sql", MigrationStatus.Migrated),
            ("03_SeedDataD.sql", MigrationStatus.Migrated)
        );
    }

    /// <summary>
    /// #25 Two errors in different releases with Ignore. Both are Failed, all others Migrated.
    /// R1/F3=Failed, R3/F2=Failed, everything else=Migrated. RunResult=Error.
    /// </summary>
    [Fact]
    public async Task MultipleErrors_AllIgnored()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_1.0", "03_SeedDataA.sql")
            .InjectError("Release_3.0", "02_CreateTableF.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Ignore)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1/F1+F2 Migrated, R1/F3 Failed
            ("01_CreateTableA.sql", MigrationStatus.Migrated),
            ("02_CreateTableB.sql", MigrationStatus.Migrated),
            ("03_SeedDataA.sql", MigrationStatus.Failed),
            // R2 Migrated
            ("01_CreateTableC.sql", MigrationStatus.Migrated),
            ("02_CreateTableD.sql", MigrationStatus.Migrated),
            ("03_SeedDataB.sql", MigrationStatus.Migrated),
            // R3/F1 Migrated, R3/F2 Failed, R3/F3 Migrated
            ("01_CreateTableE.sql", MigrationStatus.Migrated),
            ("02_CreateTableF.sql", MigrationStatus.Failed),
            ("03_SeedDataC.sql", MigrationStatus.Migrated),
            // R4 Migrated
            ("01_CreateTableG.sql", MigrationStatus.Migrated),
            ("02_CreateTableH.sql", MigrationStatus.Migrated),
            ("03_SeedDataD.sql", MigrationStatus.Migrated)
        );
    }

    /// <summary>
    /// #26 Per-file Ignore combined with product-level Rollback.
    /// R2/F2 has per-file MigrationErrorAction=Ignore, R3/F2 triggers product-level Rollback.
    /// R2/F2 is Ignored (Failed but not in rollback chain). R3/F2 error triggers rollback of all
    /// files EXCEPT R2/F2 (which was not in successfullyMigratedRecords).
    /// </summary>
    [Fact]
    public async Task IgnoredFile_NotInRollbackChain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .InjectError("Release_2.0", "02_CreateTableD.sql")
            .SetFileToml("Release_2.0", "02_CreateTableD.sql", "MigrationErrorAction", "\"Ignore\"")
            .InjectError("Release_3.0", "02_CreateTableF.sql")
            .WithMigrationErrorAction(MigrationErrorAction.Rollback)
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        ctx.AssertRunCount(1);

        ctx.AssertFileStatuses(
            // R1 rolled back to NotMigrated
            ("01_CreateTableA.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableB.sql", MigrationStatus.NotMigrated),
            ("03_SeedDataA.sql", MigrationStatus.NotMigrated),
            // R2/F1 rolled back, R2/F2 stays Failed (not in rollback chain), R2/F3 rolled back
            ("01_CreateTableC.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableD.sql", MigrationStatus.Failed),
            ("03_SeedDataB.sql", MigrationStatus.NotMigrated),
            // R3/F1 rolled back, R3/F2 rolled back (the error file)
            ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
            ("02_CreateTableF.sql", MigrationStatus.NotMigrated)
        );
        // R3/F3 and R4 have no records (never attempted after R3/F2 error)
    }
}
