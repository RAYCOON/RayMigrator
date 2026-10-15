using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The repository schema service on Sqlite: a host creates and upgrades a repository from the repository options alone,
/// without a product, an environment or a run (ADR-024).
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteRepositorySchemaServiceTests : SqliteTestBase
{
    public SqliteRepositorySchemaServiceTests(SqliteFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Ensure_CreatesTheRepositoryOnceAndFindsItAgain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositorySchemaServiceContract.VerifyEnsureAsync(Fixture.EngineConfig);
    }
}
