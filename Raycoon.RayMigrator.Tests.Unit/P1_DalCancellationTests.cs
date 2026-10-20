using System.Data.Common;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.MariaDb;
using Raycoon.RayMigrator.Database.MySql;
using Raycoon.RayMigrator.Database.PostgreSQL;
using Raycoon.RayMigrator.Database.Sqlite;
using Raycoon.RayMigrator.Database.SqlServer;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Cancellation of the asynchronous <see cref="IDal"/> calls. <see cref="DalBase"/> hands the token to the DAL and
/// turns whatever a cancelled call throws into an <see cref="OperationCanceledException"/> carrying the caller's token,
/// without a further retry; every shipped DAL passes the token to the provider before it opens a connection.
/// </summary>
public class DalCancellationTests : IDisposable
{
    private static readonly DalSettings NoRetry = new() { UseTransaction = true, DbCommandTimeoutInSeconds = 5, MaxRetries = 0, RetryDelayMs = 0 };

    private readonly string _sqliteFile = Path.Combine(Path.GetTempPath(), $"raydal_cancel_{Guid.NewGuid():N}.sqlite");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_sqliteFile)) File.Delete(_sqliteFile);
    }

    /// <summary>
    /// A DAL whose asynchronous calls run <see cref="Behaviour"/>: the calls on a connection of their own through the
    /// retry helper of <see cref="DalBase"/>, as the shipped DALs do, the others directly.
    /// </summary>
    private sealed class ScriptedDal : DalBase
    {
        public Func<CancellationToken, Task> Behaviour { get; set; } = _ => Task.CompletedTask;

        public List<CancellationToken> ReceivedTokens { get; } = [];

        public override string DatabaseType => "Scripted";
        public override DalSpecificProperties DalSpecificProperties => new();

        protected override Task ExecuteNonQueryCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken) =>
            ExecuteWithRetryAsync(() => Run(cancellationToken), dalSettings, cancellationToken: cancellationToken);

        protected override async Task<object?> ExecuteScalarCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(() => Run(cancellationToken), dalSettings, cancellationToken: cancellationToken);
            return null;
        }

        protected override async Task<List<Dictionary<string, object?>>> ExecuteReaderCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(() => Run(cancellationToken), dalSettings, cancellationToken: cancellationToken);
            return [];
        }

        protected override async Task<bool> IsConnectionValidCoreAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken)
        {
            await Run(cancellationToken);
            return true;
        }

        protected override Task ExecuteNonQueryCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken) =>
            Run(cancellationToken);

        protected override async Task<object?> ExecuteScalarCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken)
        {
            await Run(cancellationToken);
            return null;
        }

        public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override void CheckConnectionStringOrValidateConnection(bool validateConnection) => throw new NotImplementedException();
        public override DbConnection CreateConnection() => throw new NotImplementedException();

        private Task Run(CancellationToken cancellationToken)
        {
            ReceivedTokens.Add(cancellationToken);
            return Behaviour(cancellationToken);
        }
    }

    /// <summary>The asynchronous <see cref="IDal"/> calls, by name.</summary>
    public static TheoryData<string> AsyncCalls => new()
    {
        "ExecuteNonQueryAsync", "ExecuteScalarAsync", "ExecuteReaderAsync", "IsConnectionValidAsync",
        "ExecuteNonQueryAsync(shared)", "ExecuteScalarAsync(shared)",
    };

    private static Task Invoke(IDal dal, string call, string connectionString, IDalSettings settings, CancellationToken cancellationToken) => call switch
    {
        "ExecuteNonQueryAsync" => dal.ExecuteNonQueryAsync("SELECT 1", settings, null, cancellationToken),
        "ExecuteScalarAsync" => dal.ExecuteScalarAsync("SELECT 1", settings, null, cancellationToken),
        "ExecuteReaderAsync" => dal.ExecuteReaderAsync("SELECT 1", settings, null, cancellationToken),
        "IsConnectionValidAsync" => dal.IsConnectionValidAsync(connectionString, settings, cancellationToken),
        "ExecuteNonQueryAsync(shared)" => dal.ExecuteNonQueryAsync("SELECT 1", CreateConnection(dal), null!, 5, null, cancellationToken),
        "ExecuteScalarAsync(shared)" => dal.ExecuteScalarAsync("SELECT 1", CreateConnection(dal), null!, 5, null, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(call), call, null),
    };

    // The scripted DAL ignores the connection of the shared-connection calls; a real DAL gets one it never opened.
    private static DbConnection CreateConnection(IDal dal) => dal is ScriptedDal ? null! : dal.CreateConnection();

    #region DalBase

    [Theory]
    [MemberData(nameof(AsyncCalls))]
    public async Task AsyncCall_ProviderFailsAfterCancellation_ThrowsOperationCanceledExceptionWithTheCallersToken(string call)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var providerError = new InvalidOperationException("Operation cancelled by user.");
        var dal = new ScriptedDal
        {
            Behaviour = _ =>
            {
                cts.Cancel();
                throw providerError;
            }
        };

        // Act
        var act = () => Invoke(dal, call, "", NoRetry, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
            because: "a provider reports a cancelled command in its own way, and DalBase makes every such report the same");
        thrown.Which.CancellationToken.Should().Be(cts.Token, because: "the exception names the token that was cancelled");
        thrown.Which.InnerException.Should().BeSameAs(providerError, because: "the provider's report stays available");
    }

    [Theory]
    [MemberData(nameof(AsyncCalls))]
    public async Task AsyncCall_DatabaseErrorMeetsTheCancellation_NamesTheProviderMessage(string call)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var providerError = new InvalidOperationException("Transaction (Process ID 61) was deadlocked on lock resources.");
        var dal = new ScriptedDal
        {
            Behaviour = _ =>
            {
                cts.Cancel();
                throw providerError;
            }
        };

        // Act
        var act = () => Invoke(dal, call, "", NoRetry, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.Message.Should().Be($"The operation on the [Scripted] database was cancelled: {providerError.Message}",
            because: "a database error that meets the cancel is reported as the cancel, so its message must stay readable there");
        thrown.Which.InnerException.Should().BeSameAs(providerError);
    }

    [Theory]
    [MemberData(nameof(AsyncCalls))]
    public async Task AsyncCall_ProviderReportsCancellationWithoutTheToken_ThrowsOperationCanceledExceptionWithTheCallersToken(string call)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var dal = new ScriptedDal
        {
            Behaviour = _ =>
            {
                cts.Cancel();
                throw new TaskCanceledException();
            }
        };

        // Act
        var act = () => Invoke(dal, call, "", NoRetry, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.CancellationToken.Should().Be(cts.Token,
            because: "SqlClient cancels an open with a TaskCanceledException that carries no token");
        thrown.Which.Message.Should().Be("The operation on the [Scripted] database was cancelled.",
            because: "a cancellation the provider reports adds no message of its own");
    }

    [Theory]
    [MemberData(nameof(AsyncCalls))]
    public async Task AsyncCall_ProviderFailsWithoutCancellation_ThrowsTheProviderExceptionUnchanged(string call)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var providerError = new InvalidOperationException("Syntax error.");
        var dal = new ScriptedDal { Behaviour = _ => throw providerError };

        // Act
        var act = () => Invoke(dal, call, "", NoRetry, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(providerError, because: "only a cancelled call is reported as a cancellation");
    }

    [Theory]
    [MemberData(nameof(AsyncCalls))]
    public async Task AsyncCall_WithToken_HandsTheTokenToTheDal(string call)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var dal = new ScriptedDal();

        // Act
        await Invoke(dal, call, "", NoRetry, cts.Token);

        // Assert
        dal.ReceivedTokens.Should().ContainSingle()
            .Which.Should().Be(cts.Token, because: "the DAL passes the caller's token on to the provider");
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_TransientFailureAfterCancellation_IsNotRetried()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        int attempts = 0;
        var dal = new ScriptedDal
        {
            Behaviour = _ =>
            {
                attempts++;
                cts.Cancel();
                throw new TimeoutException("Timeout expired.");
            }
        };
        var retries = new DalSettings { UseTransaction = true, DbCommandTimeoutInSeconds = 5, MaxRetries = 3, RetryDelayMs = 1 };

        // Act
        var act = () => dal.ExecuteNonQueryAsync("SELECT 1", retries, null, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.InnerException.Should().BeOfType<TimeoutException>();
        attempts.Should().Be(1, because: "a failure after the cancellation is not retried, even when it looks transient");
    }

    [Fact(Timeout = 30_000)]
    public async Task ExecuteNonQueryAsync_CancelledWhileWaitingForARetry_StopsWaiting()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        int attempts = 0;
        var dal = new ScriptedDal
        {
            Behaviour = _ =>
            {
                attempts++;
                cts.CancelAfter(TimeSpan.FromMilliseconds(50));
                throw new TimeoutException("Timeout expired.");
            }
        };
        var slowRetries = new DalSettings { UseTransaction = true, DbCommandTimeoutInSeconds = 5, MaxRetries = 3, RetryDelayMs = 60_000 };

        // Act
        var act = () => dal.ExecuteNonQueryAsync("SELECT 1", slowRetries, null, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
            because: "the cancellation ends the minute-long wait before the second attempt");
        thrown.Which.CancellationToken.Should().Be(cts.Token);
        attempts.Should().Be(1, because: "no attempt starts after the cancellation");
    }

    #endregion DalBase

    #region Shipped DALs

    private const string UnreachableHost = "192.0.2.1"; // TEST-NET-1 (RFC 5737): never routed, a connect attempt would hang

    public static TheoryData<string, string> DatabaseTypesAndAsyncCalls()
    {
        var data = new TheoryData<string, string>();
        foreach (string databaseType in new[] { "SqlServer", "PostgreSQL", "MySql", "MariaDb", "Sqlite" })
        {
            foreach (string call in AsyncCalls)
            {
                data.Add(databaseType, call);
            }
        }

        return data;
    }

    private string ConnectionString(string databaseType) => databaseType switch
    {
        "SqlServer" => $"Server={UnreachableHost};Database=raydb;User Id=ray;Password=ray;Connect Timeout=30;TrustServerCertificate=True",
        "PostgreSQL" => $"Host={UnreachableHost};Database=raydb;Username=ray;Password=ray;Timeout=30",
        "MySql" or "MariaDb" => $"Server={UnreachableHost};Database=raydb;User ID=ray;Password=ray;Connection Timeout=30",
        "Sqlite" => $"Data Source={_sqliteFile}",
        _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, null),
    };

    private static IDal CreateDal(string databaseType, string connectionString) => databaseType switch
    {
        "SqlServer" => new DalSqlServer(connectionString),
        "PostgreSQL" => new DalPostgreSql(connectionString),
        "MySql" => new DalMySql(connectionString),
        "MariaDb" => new DalMariaDb(connectionString),
        "Sqlite" => new DalSqlite(connectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, null),
    };

    [Theory(Timeout = 20_000)]
    [MemberData(nameof(DatabaseTypesAndAsyncCalls))]
    public async Task AsyncCall_TokenCancelledBeforeTheCall_ThrowsOperationCanceledExceptionWithoutConnecting(string databaseType, string call)
    {
        // Arrange
        string connectionString = ConnectionString(databaseType);
        var dal = CreateDal(databaseType, connectionString);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => Invoke(dal, call, connectionString, NoRetry, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
            because: "the DAL hands the cancelled token to the provider, which gives up before it connects (a connect " +
                     "attempt to the unroutable host would outlast the test's timeout)");
        thrown.Which.CancellationToken.Should().Be(cts.Token);
    }

    /// <summary>
    /// The calls whose provider looks at the token before anything else, so that the cancellation it reports shows that the
    /// DAL passed the token on. Npgsql and MySqlConnector check the connection of a shared-connection call first, which
    /// this test never opens; the engine tests check those calls on an open connection (<c>DalSharedConnectionCancellationContract</c>).
    /// </summary>
    public static TheoryData<string, string> CallsWhoseProviderChecksTheTokenFirst()
    {
        var data = new TheoryData<string, string>();
        foreach (string databaseType in new[] { "SqlServer", "PostgreSQL", "MySql", "MariaDb", "Sqlite" })
        {
            foreach (string call in AsyncCalls)
            {
                bool connectionCheckedFirst = call.EndsWith("(shared)", StringComparison.Ordinal) && databaseType is "PostgreSQL" or "MySql" or "MariaDb";
                if (!connectionCheckedFirst)
                {
                    data.Add(databaseType, call);
                }
            }
        }

        return data;
    }

    [Theory(Timeout = 20_000)]
    [MemberData(nameof(CallsWhoseProviderChecksTheTokenFirst))]
    public async Task AsyncCall_TokenCancelledBeforeTheCall_TheProviderReportsTheCancellation(string databaseType, string call)
    {
        // Arrange
        string connectionString = ConnectionString(databaseType);
        var dal = CreateDal(databaseType, connectionString);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => Invoke(dal, call, connectionString, NoRetry, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.InnerException.Should().Match(inner => inner == null || inner is OperationCanceledException,
            because: "the provider gave up on the token itself; a DAL that dropped the token would fail on the connection, " +
                     "which a shared-connection call gets unopened, and DalBase would report that failure as the cancel");
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_SqliteTokenCancelledBeforeTheCall_ExecutesNothing()
    {
        // Arrange
        var dal = new DalSqlite($"Data Source={_sqliteFile}");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => dal.ExecuteNonQueryAsync("CREATE TABLE t (x INTEGER);", NoRetry, null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await dal.ExecuteScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 't';", NoRetry, null, TestContext.Current.CancellationToken))
            .Should().Be(0L, because: "a call that ends cancelled has executed nothing");
    }

    [Fact]
    public async Task SharedConnectionExecuteNonQueryAsync_SqliteTokenCancelledBeforeTheCall_ExecutesNothing()
    {
        // Arrange
        var dal = new DalSqlite($"Data Source={_sqliteFile}");
        await using var connection = dal.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => dal.ExecuteNonQueryAsync("CREATE TABLE t (x INTEGER);", connection, transaction, 5, null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        (await dal.ExecuteScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 't';", NoRetry, null, TestContext.Current.CancellationToken))
            .Should().Be(0L, because: "the token reaches the command, which does not start");
    }

    [Fact]
    public async Task SharedConnectionExecuteNonQueryAsync_SqliteCancelledWhileTheBatchRuns_RunsTheBatchToItsEnd()
    {
        // Arrange
        var dal = new DalSqlite($"Data Source={_sqliteFile}");
        using var cts = new CancellationTokenSource();
        await using var connection = (SqliteConnection)dal.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        // The first statement cancels the token while the batch runs.
        connection.CreateFunction("cancel_run", () =>
        {
            cts.Cancel();
            return 1;
        });
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        // Act
        await dal.ExecuteNonQueryAsync("SELECT cancel_run(); CREATE TABLE t (x INTEGER);", connection, transaction, 5, null, cts.Token);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        cts.IsCancellationRequested.Should().BeTrue();
        (await dal.ExecuteScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 't';", NoRetry, null, TestContext.Current.CancellationToken))
            .Should().Be(1L, because: "SQLite cannot stop a batch that has started, so the DAL lets it finish and reports " +
                                      "what happened instead of a cancellation after the fact");
    }

    #endregion Shipped DALs
}
