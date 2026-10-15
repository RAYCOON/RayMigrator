using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The product scope of a run on MySql (ADR-026): a run of the main product succeeds beside a product that
/// cannot be validated, and a host for that product fails on its own findings alone.
/// </summary>
[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlProductIsolationTests : MySqlTestBase
{
    public MySqlProductIsolationTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MigrateUp_RunsTheProductBesideABrokenOne()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await using var ctx = await ProductIsolationContract.WithBrokenProduct(CreateScenario()).BuildAsync();

        await ProductIsolationContract.VerifyRunBesideBrokenProduct(ctx);
    }

    [Fact]
    public async Task Build_ForTheBrokenProduct_FailsOnItsOwnFindings()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await ProductIsolationContract.VerifyBrokenProductFailsOnItsOwn(ProductIsolationContract.WithBrokenProduct(CreateScenario()));
    }
}
