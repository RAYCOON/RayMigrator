using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// Database logging goes into the repository by default on Sqlite: without a DatabaseLogging section a migrate-up
/// writes MigrationLog rows at Information into the repository database, a section with only MinimumLevel keeps
/// the repository and changes the level, and Enabled: false writes nothing and creates no log tables.
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteDatabaseLogDefaultTests : SqliteTestBase
{
    public SqliteDatabaseLogDefaultTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_WithoutSection_LogsIntoTheRepositoryAtInformation()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        // Serilog passes Debug events on, so that the sink's own level (Information by default) is what filters them.
        await using var ctx = await CreateScenario()
            .WithSerilogMinimumLevel("Debug")
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.FlushDatabaseLog();

        ctx.AssertSuccess(true);
        ctx.AssertRepositoryTableExists("MigrationLog", true);
        ctx.CountLogEntries().Should().BeGreaterThan(0, "the log lands in the repository without any DatabaseLogging section");
        ctx.CountLogEntriesAtLevel((int)LogLevel.Information).Should().BeGreaterThan(0);
        ctx.CountLogEntriesAtLevel((int)LogLevel.Debug).Should().Be(0, "the default level is Information");
    }

    [Fact]
    public async Task MigrateUp_WithOnlyMinimumLevel_KeepsTheRepositoryAndChangesTheLevel()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithSerilogMinimumLevel("Debug")
            .WithDatabaseLoggingMinimumLevelOnly("Debug")
            .BuildAsync();

        await ctx.MigrateUpAsync();
        ctx.FlushDatabaseLog();

        ctx.AssertSuccess(true);
        ctx.AssertRepositoryTableExists("MigrationLog", true);
        ctx.CountLogEntriesAtLevel((int)LogLevel.Debug).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MigrateUp_Disabled_WritesNothingAndCreatesNoLogTables()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario()
            .WithDatabaseLoggingDisabled()
            .BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.AssertRepositoryTableExists("MigrationLog", false);
        ctx.AssertRepositoryTableExists("MigrationEvent", false);
    }
}
