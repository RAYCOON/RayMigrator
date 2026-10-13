using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The versioned repository schema on Sqlite (ADR-024): a fresh repository gets every schema step and the engine
/// version, a second run changes nothing, a repository that is newer than the engine or has tables without a schema
/// version is refused before anything is written, and a parallel start on an empty repository creates it once.
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteRepositorySchemaTests : SqliteTestBase
{
    public SqliteRepositorySchemaTests(SqliteFixture fixture) : base(fixture) { }

    private string InsertFutureVersionSql => $"INSERT INTO \"RepositorySchemaVersion\" (\"Version\", \"AppliedAt\", \"RayMigratorVersion\") VALUES (99, datetime('now'), 'future')";

    private string DropVersionTableSql => $"DROP TABLE \"RepositorySchemaVersion\"";

    [Fact]
    public async Task CheckCreate_FreshRepository_AppliesSchemaAndRegistersEngineVersion()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        ctx.CountRepoRows("MigratorMeta").Should().Be(1);
        ctx.AssertRepositoryTableExists("MigrationRecordHistory", true);
    }

    [Fact]
    public async Task CheckCreate_SecondRun_LeavesSchemaVersionAndEngineVersionUnchanged()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        await ctx.InfoAsync();

        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        ctx.CountRepoRows("MigratorMeta").Should().Be(1);
    }

    [Fact]
    public async Task CheckCreate_RepositoryNewerThanEngine_RefusesTheRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString, InsertFutureVersionSql);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("schema version [99]").And.Contain($"version [{RepositorySchema.CurrentVersion}] at most");
        ctx.CountMigrationRuns().Should().Be(1, because: "the refused run writes no run row");
    }

    [Fact]
    public async Task CheckCreate_TablesWithoutSchemaVersion_RefusesTheRun()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync();
        await ctx.MigrateUpAsync();
        ctx.AssertSuccess(true);
        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString, DropVersionTableSql);

        await ctx.RebuildForAsync(MigrationCommand.MigrateUp, MigrationRunMode.Migrate);
        var result = await ctx.MigrateUpAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no schema version").And.Contain("before 0.17.0");
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(0, because: "the check recreates the empty version table and the engine then refuses the repository");
        ctx.CountMigrationRuns().Should().Be(1);
    }

    [Fact]
    public async Task CheckCreate_ParallelStartOnEmptyRepository_CreatesTheSchemaOnce()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var first = await CreateScenario().BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        await using var second = await CreateScenario().BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);

        await Task.WhenAll(first.InfoAsync(), second.InfoAsync());

        first.CountRepoRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        first.CountRepoRows("MigratorMeta").Should().Be(1);
    }
}
