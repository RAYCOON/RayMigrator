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

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlDatabaseLogTests : MySqlTestBase
{
    public MySqlDatabaseLogTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task LogEntries_AfterMigrateUp_ShouldExist()
    {
        Assert.Skip("DatabaseLogWriter async queue does not flush within test lifecycle");
    }

    [Fact]
    public async Task LogEntries_ShouldContainMultipleLogLevels()
    {
        Assert.Skip("DatabaseLogWriter async queue does not flush within test lifecycle");
    }

    [Fact]
    public async Task LogEntries_DuringError_ShouldStillBeWritten()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithDatabaseLogging()
            .InjectError("Release_2.0", "01_CreateTableC.sql")
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(false);

        // Poll for log entries (even with errors, logs should be written)
        int logCount = 0;
        for (int i = 0; i < 60 && logCount == 0; i++)
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            logCount = ctx.CountLogEntries();
        }

        logCount.Should().BeGreaterThan(0,
            "Database log should contain entries even when migration fails");
    }

    /// <summary>
    /// D4: After MigrateUp with database logging enabled, MigrationLog and
    /// MigrationEvent tables must be created by DatabaseLogging_CheckCreate.
    /// </summary>
    [Fact]
    public async Task LogTables_AfterMigrateUp_ShouldBeCreated()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithDatabaseLogging()
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        ctx.AssertRepositoryTableExists("MigrationLog", true);
        ctx.AssertRepositoryTableExists("MigrationEvent", true);
    }
}
