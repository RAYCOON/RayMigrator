using Raycoon.RayMigrator.Database.MySql;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// A call of the MySql DAL that fails after a <c>GET_LOCK</c> leaves no lock behind (see <see cref="DalUserLockContract"/>).
/// </summary>
[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlDalUserLockTests : MySqlTestBase
{
    public MySqlDalUserLockTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteScalar_FailsAfterGetLock_ReleasesTheLock()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await DalUserLockContract.VerifyFailedCallReleasesItsLockAsync(
            new DalMySql(Fixture.EngineConfig.ConnectionString), Fixture.EngineConfig.ConnectionString);
    }
}
