using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The repository schema service on MySql: a host creates and upgrades a repository from the repository options alone,
/// without a product, an environment or a run (ADR-024).
/// </summary>
[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlRepositorySchemaServiceTests : MySqlTestBase
{
    public MySqlRepositorySchemaServiceTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public void Ensure_CreatesTheRepositoryOnceAndFindsItAgain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        RepositorySchemaServiceContract.VerifyEnsure(Fixture.EngineConfig);
    }
}
