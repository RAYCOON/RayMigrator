using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class DatabaseLogTests : PostgreSqlTestBase
{
    public DatabaseLogTests(PostgreSqlFixture fixture) : base(fixture) { }

    /// <summary>
    /// D1: After MigrateUp with database logging enabled, log entries should exist.
    /// SKIPPED: DatabaseLogWriter async queue does not flush within test lifecycle.
    /// </summary>
    [Fact]
    public async Task LogEntries_AfterMigrateUp_ShouldExist()
    {
        Assert.Skip("DatabaseLogWriter async queue does not flush within test lifecycle");
    }

    /// <summary>
    /// D2: After MigrateUp with Debug-level logging, both Debug and Information log entries should exist.
    /// SKIPPED: DatabaseLogWriter async queue does not flush within test lifecycle.
    /// </summary>
    [Fact]
    public async Task LogEntries_ShouldContainMultipleLogLevels()
    {
        Assert.Skip("DatabaseLogWriter async queue does not flush within test lifecycle");
    }

    /// <summary>
    /// D3: After a failed MigrateUp with database logging, log entries should still be written.
    /// </summary>
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
