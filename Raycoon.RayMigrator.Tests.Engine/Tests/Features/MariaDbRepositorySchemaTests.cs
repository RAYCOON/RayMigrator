using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The versioned repository schema on MariaDb (ADR-024): a fresh repository gets every schema step and the engine
/// version, a second run changes nothing, a repository that is newer than the engine or has tables without a schema
/// version is refused before anything is written, and a parallel start on an empty repository creates it once.
/// </summary>
[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbRepositorySchemaTests : MariaDbTestBase
{
    public MariaDbRepositorySchemaTests(MariaDbFixture fixture) : base(fixture) { }

    private string InsertFutureVersionSql => $"INSERT INTO repository_schema_version (version, applied_at, raymigrator_version) VALUES (99, CURRENT_TIMESTAMP, 'future')";

    private string DropVersionTableSql => $"DROP TABLE repository_schema_version";

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

    [Fact]
    public async Task CheckCreate_HalfCreatedRepository_IsCompletedNotRefused()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        await ctx.InfoAsync();
        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);

        // DDL commits implicitly on this engine: a step 1 that lost its connection after creating migrator_meta leaves
        // the table behind, empty, and no version row. That is not a pre-0.17.0 repository; the next command completes it.
        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString, "DELETE FROM migrator_meta");
        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString, "DELETE FROM repository_schema_version");

        await ctx.InfoAsync();

        ctx.CountRepoRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion, "every step ran again");
        ctx.CountRepoRows("MigratorMeta").Should().Be(1, "the engine version is registered again");
    }

    [Fact]
    public async Task CheckCreate_RepositoryWithEngineRowsButNoVersionRow_IsRefusedAsPre017()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        await ctx.InfoAsync();

        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString, "DELETE FROM repository_schema_version");

        var act = () => ctx.InfoAsync();

        await act.Should().ThrowAsync<RepositorySchemaException>("migrator_meta has rows and no version row exists");
    }
}
