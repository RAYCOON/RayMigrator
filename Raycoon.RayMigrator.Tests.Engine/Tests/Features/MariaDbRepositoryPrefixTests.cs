using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// Table prefixes on MariaDb: a run against a repository with a prefix succeeds, and a second repository with another
/// prefix is created beside it in the same database, because every index and constraint name carries the prefix. A
/// scenario with a prefix leaves no table of it behind when it ends.
/// </summary>
[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbRepositoryPrefixTests : MariaDbTestBase
{
    public MariaDbRepositoryPrefixTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_WithPrefix_AndASecondPrefixBesideIt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await CreateScenario().WithTableBaseName(RepositoryPrefixContract.FirstPrefix).BuildAsync();

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.AssertRepositoryTableExists("MigrationRecordHistory", true);
        await RepositoryPrefixContract.VerifySecondPrefixBesideTheFirstAsync(ctx, Fixture.EngineConfig);
    }

    /// <summary>
    /// The scenario drops the repository and log tables of its prefix when it ends, also in a log database of their own
    /// (<see cref="PrefixTableCleanupContract"/>).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_WithPrefix_LeavesNoTableOfThePrefix(bool logInADatabaseOfItsOwn)
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await PrefixTableCleanupContract.VerifyDisposeLeavesNoTableOfThePrefixAsync(
            CreateScenario(), Fixture.EngineConfig, logInADatabaseOfItsOwn);
    }
}
