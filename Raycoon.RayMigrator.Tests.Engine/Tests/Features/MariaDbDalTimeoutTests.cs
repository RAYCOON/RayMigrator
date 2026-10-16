using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The MariaDb DAL reports a command timeout as a timeout (see <see cref="DalTimeoutContract"/>).
/// </summary>
[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbDalTimeoutTests : MariaDbTestBase
{
    public MariaDbDalTimeoutTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteNonQuery_RunsIntoTheCommandTimeout_IsATimeout()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await DalTimeoutContract.VerifyCommandTimeoutIsATimeoutAsync(Fixture.EngineConfig);
    }
}
