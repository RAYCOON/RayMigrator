using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// The shared-connection calls of the SqlServer DAL hand the caller's token to the command on an open connection
/// (see <see cref="DalSharedConnectionCancellationContract"/>).
/// </summary>
[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerDalCancellationTests : SqlServerTestBase
{
    public SqlServerDalCancellationTests(SqlServerFixture fixture) : base(fixture) { }

    public static TheoryData<string> SharedConnectionCalls => DalSharedConnectionCancellationContract.SharedConnectionCalls;

    [Theory]
    [MemberData(nameof(SharedConnectionCalls))]
    public async Task SharedConnectionCall_TokenCancelledBeforeTheCall_TheProviderReportsTheCancellation(string call)
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        await DalSharedConnectionCancellationContract.VerifyTokenCancelledBeforeTheCallIsReportedByTheProviderAsync(Fixture.EngineConfig, call);
    }
}
