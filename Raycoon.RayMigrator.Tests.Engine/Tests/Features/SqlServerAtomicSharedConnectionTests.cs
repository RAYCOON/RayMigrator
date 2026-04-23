// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Database", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerAtomicSharedConnectionTests : SqlServerTestBase
{
    private static readonly DalSettings SetupSettings = new()
    {
        UseTransaction = false,
        DbCommandTimeoutInSeconds = 30
    };

    public SqlServerAtomicSharedConnectionTests(SqlServerFixture fixture) : base(fixture) { }

    /// <summary>
    /// ASC1: Transient error (timeout) in block 2 triggers transaction rollback followed by
    /// file-level retry. On the second attempt all 3 blocks succeed, yielding 2 rows.
    /// Verifies that the atomic shared-connection path retries the entire file as a unit.
    /// </summary>
    [Fact]
    public async Task AtomicRetry_TransientErrorInBlock2_RollsBackAndRetries()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        string connectionString = Fixture.EngineConfig.ConnectionString;
        string filename = "01_AtomicRetry.sql";

        // Setup: create the SEQUENCE used to distinguish attempt 1 from attempt 2
        ExecuteSetupSql(connectionString,
            "IF OBJECT_ID('dbo.AtomicRetrySeq', 'SO') IS NOT NULL DROP SEQUENCE dbo.AtomicRetrySeq;" +
            "CREATE SEQUENCE dbo.AtomicRetrySeq AS INT START WITH 1 INCREMENT BY 1;");

        try
        {
            await using var ctx = await CreateScenario()
                .WithTargetMaxRetries(1, 100)
                .WithTargetCommandTimeout(2)
                .WithRequireRollbackFile(false)
                .BuildAsync();

            // Write the migration file directly into the scenario work directory
            string releaseDir = Path.Combine(ctx.WorkDirectory, "Release_Atomic");
            string backendDir = Path.Combine(releaseDir, "Backend");
            Directory.CreateDirectory(backendDir);

            string migrationContent =
                "CREATE TABLE dbo.AtomicRetryTest (Id INT PRIMARY KEY, Name NVARCHAR(50));" +
                Environment.NewLine + "GO" + Environment.NewLine +
                "DECLARE @attempt INT = NEXT VALUE FOR dbo.AtomicRetrySeq;" + Environment.NewLine +
                "IF @attempt = 1" + Environment.NewLine +
                "    WAITFOR DELAY '00:00:05';" + Environment.NewLine +
                "INSERT INTO dbo.AtomicRetryTest (Id, Name) VALUES (1, 'FromBlock2');" +
                Environment.NewLine + "GO" + Environment.NewLine +
                "INSERT INTO dbo.AtomicRetryTest (Id, Name) VALUES (2, 'FromBlock3');" +
                Environment.NewLine;

            File.WriteAllText(Path.Combine(backendDir, filename), migrationContent);

            await ctx.MigrateUpAsync();

            ctx.AssertSuccess(true);
            ctx.AssertRunResult(MigrationRunResult.Ok);
            ctx.AssertFileStatus(filename, MigrationStatus.Migrated);
            ctx.AssertTableExists("AtomicRetryTest", true);
            ctx.AssertRowCount("AtomicRetryTest", 2);
        }
        finally
        {
            ExecuteSetupSql(connectionString,
                "IF OBJECT_ID('dbo.AtomicRetryTest', 'U') IS NOT NULL DROP TABLE dbo.AtomicRetryTest;" +
                "IF OBJECT_ID('dbo.AtomicRetrySeq', 'SO') IS NOT NULL DROP SEQUENCE dbo.AtomicRetrySeq;");
        }
    }

    /// <summary>
    /// ASC2: Permanent error in block 2 causes the entire shared-connection transaction to
    /// roll back, including the CREATE TABLE from block 1. No table must exist after the run.
    /// Verifies that the atomic shared-connection path treats the whole file as one unit.
    /// </summary>
    [Fact]
    public async Task AtomicRollback_PermanentErrorInBlock2_RollsBackAllBlocks()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        string filename = "01_AtomicRollback.sql";

        await using var ctx = await CreateScenario()
            .WithRequireRollbackFile(false)
            .BuildAsync();

        // Write the migration file directly into the scenario work directory
        string releaseDir = Path.Combine(ctx.WorkDirectory, "Release_Atomic");
        string backendDir = Path.Combine(releaseDir, "Backend");
        Directory.CreateDirectory(backendDir);

        string migrationContent =
            "CREATE TABLE dbo.AtomicRollbackTest (Id INT PRIMARY KEY);" +
            Environment.NewLine + "GO" + Environment.NewLine +
            "INSERT INTO dbo.NonExistentTable_Error (Id) VALUES (1);" +
            Environment.NewLine + "GO" + Environment.NewLine +
            "INSERT INTO dbo.AtomicRollbackTest (Id) VALUES (1);" +
            Environment.NewLine;

        File.WriteAllText(Path.Combine(backendDir, filename), migrationContent);

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(false);
        ctx.AssertRunResult(MigrationRunResult.Error);
        // Block 1's CREATE TABLE must be rolled back along with block 2's error
        ctx.AssertTableExists("AtomicRollbackTest", false);
    }

    /// <summary>
    /// Executes a DDL or setup SQL statement directly against the target database.
    /// Used for test fixture setup and teardown that runs outside the migration pipeline.
    /// </summary>
    private static void ExecuteSetupSql(string connectionString, string sql)
    {
        if (!DalFactory.TryGetDal("SqlServer", connectionString, out IDal? dal))
            throw new InvalidOperationException("Could not create SqlServer DAL for setup SQL");

        dal!.ExecuteNonQuery(sql, SetupSettings, null);
    }
}
