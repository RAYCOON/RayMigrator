using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The MySql DAL reports a command timeout as a timeout (see <see cref="DalTimeoutContract"/>).
/// </summary>
[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlDalTimeoutTests : MySqlTestBase
{
    public MySqlDalTimeoutTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteNonQuery_RunsIntoTheCommandTimeout_IsATimeout()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await DalTimeoutContract.VerifyCommandTimeoutIsATimeoutAsync(Fixture.EngineConfig);
    }
}
