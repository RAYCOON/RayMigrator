using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// A command that runs into its command timeout is a timeout for <see cref="DalBase.IsTimeout"/> on every server engine,
/// however the provider reports it: SqlClient with error -2, Npgsql with a <see cref="TimeoutException"/>, MySqlConnector
/// with <c>CommandTimeoutExpired</c>. The initialization of the database log does not retry such a failure. SQLite has
/// no command timeout for a running statement.
/// </summary>
internal static class DalTimeoutContract
{
    public static async Task VerifyCommandTimeoutIsATimeoutAsync(EngineConfig config)
    {
        // Arrange: KILL QUERY ends a SLEEP() early without an error, so MySQL and MariaDB run a long query instead, which it
        // interrupts with an error, as it does an index build
        DalFactory.TryGetDal(config.DatabaseType, config.ConnectionString, out var dal).Should().BeTrue();
        var settings = new DalSettings { UseTransaction = false, DbCommandTimeoutInSeconds = 1, MaxRetries = 0, RetryDelayMs = 0 };
        string sql = config.DatabaseType is "MySql" or "MariaDb"
            ? "SELECT COUNT(*) FROM information_schema.COLUMNS a CROSS JOIN information_schema.COLUMNS b CROSS JOIN information_schema.COLUMNS c;"
            : SqlDialect.GetSleepSql(config.DatabaseType, 5);

        // Act
        var act = () => dal!.ExecuteNonQueryAsync(sql, settings, null, TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<Exception>(because: "the sleep outlasts the command timeout of one second");
        ((DalBase)dal!).IsTimeout(thrown.Which).Should().BeTrue(because: $"the provider reported a timeout: {thrown.Which.GetType().Name}: {thrown.Which.Message}");
        ((DalBase)dal!).IsTimeout(new InvalidOperationException("Syntax error.")).Should().BeFalse();
    }
}
