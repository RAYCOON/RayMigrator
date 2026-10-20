using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The PostgreSQL DAL reports a command timeout as a timeout (see <see cref="DalTimeoutContract"/>).
/// </summary>
[Collection("PostgreSQL")]
[Trait("Engine", "PostgreSQL")]
[Trait("Category", "Features")]
public class PostgreSqlDalTimeoutTests : PostgreSqlTestBase
{
    public PostgreSqlDalTimeoutTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteNonQuery_RunsIntoTheCommandTimeout_IsATimeout()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await DalTimeoutContract.VerifyCommandTimeoutIsATimeoutAsync(Fixture.EngineConfig);
    }
}
