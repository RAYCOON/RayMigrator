using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The shared-connection calls of a DAL (the atomic path runs a file and its repository updates on the caller's connection
/// and transaction) hand the caller's token to the command. The unit tests cannot show this for every provider: Npgsql and
/// MySqlConnector check the connection before the token, and a unit test has no open connection to give them. Here the
/// connection is open, so a DAL that dropped the token would run the statement instead of reporting the cancellation.
/// </summary>
internal static class DalSharedConnectionCancellationContract
{
    /// <summary>The two shared-connection calls of <see cref="IDal"/>.</summary>
    public static TheoryData<string> SharedConnectionCalls => new() { "ExecuteNonQueryAsync", "ExecuteScalarAsync" };

    public static async Task VerifyTokenCancelledBeforeTheCallIsReportedByTheProviderAsync(EngineConfig config, string call)
    {
        // Arrange: the caller's open connection and transaction, a token that is cancelled before the call
        DalFactory.TryGetDal(config.DatabaseType, config.ConnectionString, out var dal).Should().BeTrue();
        await using var connection = dal!.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        const string sql = "SELECT 1";

        // Act
        Func<Task> act = call switch
        {
            "ExecuteNonQueryAsync" => () => dal.ExecuteNonQueryAsync(sql, connection, transaction, 5, null, cts.Token),
            "ExecuteScalarAsync" => () => dal.ExecuteScalarAsync(sql, connection, transaction, 5, null, cts.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(call), call, null)
        };

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
            because: "the DAL hands the cancelled token to the command, which does not start");
        thrown.Which.CancellationToken.Should().Be(cts.Token);
        thrown.Which.InnerException.Should().Match(inner => inner == null || inner is OperationCanceledException,
            because: "the provider reports the cancellation itself and no failure of the statement");
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }
}
