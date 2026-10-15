using Raycoon.RayMigrator.Database.MariaDb;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// A call of the MariaDb DAL that fails after a <c>GET_LOCK</c> leaves no lock behind (see <see cref="DalUserLockContract"/>).
/// </summary>
[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbDalUserLockTests : MariaDbTestBase
{
    public MariaDbDalUserLockTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteScalar_FailsAfterGetLock_ReleasesTheLock()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await DalUserLockContract.VerifyFailedCallReleasesItsLockAsync(
            new DalMariaDb(Fixture.EngineConfig.ConnectionString), Fixture.EngineConfig.ConnectionString);
    }
}
