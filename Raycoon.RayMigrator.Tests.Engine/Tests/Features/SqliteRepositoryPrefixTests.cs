using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// Table prefixes on Sqlite: a run against a repository with a prefix succeeds, and a second repository with another
/// prefix is created beside it in the same database, because every index and constraint name carries the prefix.
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteRepositoryPrefixTests : SqliteTestBase
{
    public SqliteRepositoryPrefixTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_WithPrefix_AndASecondPrefixBesideIt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().WithTableBaseName(RepositoryPrefixContract.FirstPrefix).BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.AssertRepositoryTableExists("MigrationRecordHistory", true);
        RepositoryPrefixContract.VerifySecondPrefixBesideTheFirst(ctx, Fixture.EngineConfig);
    }
}
