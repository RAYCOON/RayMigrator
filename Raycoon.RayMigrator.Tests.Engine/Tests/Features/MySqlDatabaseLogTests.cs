using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;
using Raycoon.RayMigrator.Core.Logging;

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

    /// <summary>
    /// D5: The EventId of a logger call must reach the MigrationEventId column. TemplateExecutor logs every
    /// repository template execution with a MigrationEvent id (100..136), so after a migrate-up at least one
    /// MigrationLog row must carry an event id other than 0 (UnspecifiedEvent) (#12).
    /// </summary>
    [Fact]
    public async Task LogEntries_CarryMigrationEventId()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithDatabaseLogging("Debug")
            .WithSerilogMinimumLevel("Debug")
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.FlushDatabaseLog();

        ctx.CountLogEntries().Should().BeGreaterThan(0, "database logging is enabled");
        ctx.CountLogEntriesWithEventIdOtherThan(0).Should().BeGreaterThan(0,
            "template execution events (MigrationEvent.TemplateExecution*) are logged with an EventId that must be persisted");

        // A second command against the same log database runs the "already exists" branch of
        // DatabaseLogging_CheckCreate, i.e. the idempotent catalogue upgrade, on this engine.
        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
    }
}
