using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The shared-connection calls of the Sqlite DAL hand the caller's token to the command on an open connection
/// (see <see cref="DalSharedConnectionCancellationContract"/>).
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteDalCancellationTests : SqliteTestBase
{
    public SqliteDalCancellationTests(SqliteFixture fixture) : base(fixture) { }

    public static TheoryData<string> SharedConnectionCalls => DalSharedConnectionCancellationContract.SharedConnectionCalls;

    [Theory]
    [MemberData(nameof(SharedConnectionCalls))]
    public async Task SharedConnectionCall_TokenCancelledBeforeTheCall_TheProviderReportsTheCancellation(string call)
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await DalSharedConnectionCancellationContract.VerifyTokenCancelledBeforeTheCallIsReportedByTheProviderAsync(Fixture.EngineConfig, call);
    }
}
