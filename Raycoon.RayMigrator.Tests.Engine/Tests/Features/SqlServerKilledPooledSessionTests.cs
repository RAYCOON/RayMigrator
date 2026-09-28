using Microsoft.Data.SqlClient;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.SqlServer;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// A migration file that alters the login RayMigrator itself uses (for example
/// <c>ALTER LOGIN [sa] WITH DEFAULT_LANGUAGE = German</c>) invalidates every pooled session opened before the change:
/// SQL Server fails the reset on reuse with error 4021, then kills the session (596). SqlClient discards only the one
/// connection that failed, so the repository update after such a file used to fail once per poisoned pooled connection.
/// The DAL now retries these errors and clears the pool, so the next attempt logs in afresh.
/// </summary>
[Collection("SqlServer")]
[Trait("Engine", "SqlServer")]
[Trait("Category", "Features")]
public class SqlServerKilledPooledSessionTests : SqlServerTestBase
{
    // A private application name gives this test its own connection pool, so poisoning it cannot touch the pools
    // of the other SQL Server tests, and clearing it afterwards is exact.
    private const string ApplicationName = "RayMigrator.Tests.KilledPooledSession";
    private const int PooledConnections = 3;

    public SqlServerKilledPooledSessionTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteScalarAsync_LoginAlteredAfterPooledSessionsOpened_RetrySucceedsOnFreshConnection()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

        var pooled = BuildConnectionString(pooling: true);
        var unpooled = BuildConnectionString(pooling: false);
        var dal = new DalSqlServer(pooled);
        var loginName = await ScalarAsync<string>(unpooled, "SELECT SUSER_SNAME()");
        var originalLanguage = await ScalarAsync<string>(unpooled,
            "SELECT default_language_name FROM sys.server_principals WHERE name = SUSER_SNAME()");
        var otherLanguage = originalLanguage.Equals("us_english", StringComparison.OrdinalIgnoreCase) ? "German" : "us_english";

        try
        {
            // Without retries the first call still fails, but it clears the pool: the second call must succeed
            // although more than one poisoned connection was pooled.
            await WarmPoolAsync(pooled);
            await AlterDefaultLanguageAsync(unpooled, loginName, otherLanguage);

            var noRetry = new DalSettings { MaxRetries = 0, DbCommandTimeoutInSeconds = 10 };
            var firstFailure = await Assert.ThrowsAsync<SqlException>(() => dal.ExecuteScalarAsync("SELECT 1", noRetry));
            firstFailure.Number.Should().Be(4021);
            firstFailure.Errors.Cast<SqlError>().Select(e => e.Number).Should().Contain(596);

            (await dal.ExecuteScalarAsync("SELECT 1", noRetry)).Should().Be(1);

            // With one retry the caller never sees the killed session. The successful call above opened a session
            // while the other language was active, so restoring the language poisons that one: clear the pool first.
            await AlterDefaultLanguageAsync(unpooled, loginName, originalLanguage);
            SqlConnection.ClearPool(new SqlConnection(pooled));
            await WarmPoolAsync(pooled);
            await AlterDefaultLanguageAsync(unpooled, loginName, otherLanguage);

            var oneRetry = new DalSettings { MaxRetries = 1, RetryDelayMs = 10, DbCommandTimeoutInSeconds = 10 };
            (await dal.ExecuteScalarAsync("SELECT 1", oneRetry)).Should().Be(1);
        }
        finally
        {
            await AlterDefaultLanguageAsync(unpooled, loginName, originalLanguage);
            SqlConnection.ClearPool(new SqlConnection(pooled));
        }
    }

    private string BuildConnectionString(bool pooling)
    {
        var builder = new SqlConnectionStringBuilder(Fixture.EngineConfig.ConnectionString)
        {
            InitialCatalog = "master",
            ApplicationName = ApplicationName,
            Pooling = pooling
        };
        return builder.ConnectionString;
    }

    /// <summary>Opens several pooled connections at once so the pool holds that many physical sessions.</summary>
    private static async Task WarmPoolAsync(string connectionString)
    {
        var connections = new List<SqlConnection>();
        try
        {
            for (int i = 0; i < PooledConnections; i++)
            {
                var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await using var command = new SqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();
                connections.Add(connection);
            }
        }
        finally
        {
            foreach (var connection in connections)
                await connection.DisposeAsync();
        }
    }

    private static async Task AlterDefaultLanguageAsync(string connectionString, string loginName, string language)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"ALTER LOGIN [{loginName}] WITH DEFAULT_LANGUAGE = [{language}]", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
