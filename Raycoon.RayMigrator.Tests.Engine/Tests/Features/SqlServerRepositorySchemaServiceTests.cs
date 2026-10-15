using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The repository schema service on SqlServer: a host creates and upgrades a repository from the repository options alone,
/// without a product, an environment or a run (ADR-024).
/// </summary>
[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerRepositorySchemaServiceTests : SqlServerTestBase
{
    public SqlServerRepositorySchemaServiceTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Ensure_CreatesTheRepositoryOnceAndFindsItAgain()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await RepositorySchemaServiceContract.VerifyEnsureAsync(Fixture.EngineConfig);
    }
}
