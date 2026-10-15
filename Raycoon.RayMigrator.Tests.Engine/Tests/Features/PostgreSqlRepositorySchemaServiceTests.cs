using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The repository schema service on PostgreSql: a host creates and upgrades a repository from the repository options alone,
/// without a product, an environment or a run (ADR-024).
/// </summary>
[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class PostgreSqlRepositorySchemaServiceTests : PostgreSqlTestBase
{
    public PostgreSqlRepositorySchemaServiceTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public void Ensure_CreatesTheRepositoryOnceAndFindsItAgain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        RepositorySchemaServiceContract.VerifyEnsure(Fixture.EngineConfig);
    }
}
