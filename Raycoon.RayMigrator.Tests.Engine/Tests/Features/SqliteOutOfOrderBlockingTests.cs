// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteOutOfOrderBlockingTests : SqliteTestBase
{
    public SqliteOutOfOrderBlockingTests(SqliteFixture fixture) : base(fixture) { }

    private void AddOutOfOrderFile(string workDir)
    {
        string dbType = Fixture.EngineConfig.DatabaseType;
        string dir = Path.Combine(workDir, "Release_1.0", "Backend");

        // Write migration file
        string createSql = SqlDialect.GetCreateSimpleTableSql(dbType, "TableOOO");
        File.WriteAllText(Path.Combine(dir, "99_CreateTableOOO.sql"), createSql);

        // Write rollback file
        string dropSql = SqlDialect.GetDropSimpleTableSql(dbType, "TableOOO");
        File.WriteAllText(Path.Combine(dir, "99_CreateTableOOO.rollback.sql"), dropSql);
    }

    /// <summary>
    /// O1: MigrateUp all, add a new file to Release_1.0, MigrateUp with allowOutOfOrder=false should fail.
    /// </summary>
    [Fact]
    public async Task OutOfOrder_Blocked_ShouldFail()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();

        // Phase 1: MigrateUp all releases
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        // Phase 2: Add a new file to Release_1.0 (older than currently migrated R4)
        AddOutOfOrderFile(ctx.WorkDirectory);

        // Phase 3: MigrateUp again with allowOutOfOrder: false (default)
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var result = await ctx.MigrateUpAsync(allowOutOfOrder: false);

        result.Success.Should().BeFalse("Out-of-order should be blocked when allowOutOfOrder is false");
    }

    /// <summary>
    /// O2: MigrateUp all, add a new file to Release_1.0, MigrateUp with allowOutOfOrder=true should succeed.
    /// </summary>
    [Fact]
    public async Task OutOfOrder_Allowed_ShouldSucceed()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        AddOutOfOrderFile(ctx.WorkDirectory);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var result = await ctx.MigrateUpAsync(allowOutOfOrder: true);

        result.Success.Should().BeTrue($"Out-of-order should succeed: {result.ErrorMessage}");
    }

    /// <summary>
    /// O3: After a blocked out-of-order attempt, original data should remain intact.
    /// </summary>
    [Fact]
    public async Task OutOfOrder_Blocked_OriginalDataIntact()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        AddOutOfOrderFile(ctx.WorkDirectory);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync(allowOutOfOrder: false);

        // Original tables should still exist with data
        ctx.AssertTableExists("tablea", true);
        ctx.AssertTableExists("tablec", true);
        ctx.AssertRowCount("tablea", 3);
    }
}
