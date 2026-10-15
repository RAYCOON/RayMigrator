using MySqlConnector;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The MySQL and MariaDB templates take their locks with <c>GET_LOCK</c> and release them in the same template. A call
/// that fails between the two must not leave its lock on the session: the connection goes back to the pool, which keeps
/// the session and the lock until it hands the connection out again or prunes it as idle, and every other host waits for
/// the lock until then. The lock is checked from a session of its own, outside the pool of the DAL.
/// </summary>
internal static class DalUserLockContract
{
    private static readonly DalSettings Settings = new() { UseTransaction = true, DbCommandTimeoutInSeconds = 20, MaxRetries = 0, RetryDelayMs = 0 };

    public static async Task VerifyFailedCallReleasesItsLockAsync(IDal dal, string connectionString)
    {
        // Arrange
        string lockName = $"rm_test_{Guid.NewGuid():N}";
        string sql = $"DO GET_LOCK('{lockName}', 0); SELECT missing_column FROM rm_table_that_does_not_exist;";

        // Act
        var act = () => dal.ExecuteScalarAsync(sql, Settings, null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<MySqlException>(because: "the statement after the lock fails");
        (await IsFreeLockAsync(connectionString, lockName)).Should().Be(1,
            because: "the DAL releases the locks of a session whose call failed before the connection goes back to the pool");
    }

    private static async Task<long> IsFreeLockAsync(string connectionString, string lockName)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString) { Pooling = false };
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new MySqlCommand($"SELECT IS_FREE_LOCK('{lockName}')", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }
}
