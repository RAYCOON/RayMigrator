using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The repository schema service on MariaDb: a host creates and upgrades a repository from the repository options alone,
/// without a product, an environment or a run (ADR-024).
/// </summary>
[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbRepositorySchemaServiceTests : MariaDbTestBase
{
    public MariaDbRepositorySchemaServiceTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Ensure_CreatesTheRepositoryOnceAndFindsItAgain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositorySchemaServiceContract.VerifyEnsureAsync(Fixture.EngineConfig);
    }
}
