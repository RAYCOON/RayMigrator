using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.Sqlite;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: A SQLite script that opens its own transaction (<c>BEGIN IMMEDIATE</c>, as the repository steps do) and fails
/// before <c>COMMIT</c> must not keep the file's write lock: Microsoft.Data.Sqlite pools the connection, and without a
/// rollback the next writer fails with "database is locked". The DAL rolls the open transaction back on failure.
/// </summary>
public class DalSqliteOpenTransactionTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"raydal_{Guid.NewGuid():N}.sqlite");
    private readonly string _connectionString;
    private static readonly DalSettings Settings = new() { UseTransaction = false, DbCommandTimeoutInSeconds = 1, MaxRetries = 0, RetryDelayMs = 0 };

    public DalSqliteOpenTransactionTests() => _connectionString = $"Data Source={_file}";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
    }

    private const string FailingScript = "BEGIN IMMEDIATE; ALTER TABLE t ADD COLUMN y INTEGER; ALTER TABLE t ADD COLUMN y INTEGER; COMMIT; SELECT 1;";

    [Fact]
    public async Task ExecuteScalarAsync_FailingScriptWithOwnTransaction_RollsBackAndReleasesTheWriteLock()
    {
        var dal = new DalSqlite(_connectionString);
        await dal.ExecuteNonQueryAsync("CREATE TABLE t (x INTEGER);", Settings, cancellationToken: TestContext.Current.CancellationToken);

        var act = () => dal.ExecuteScalarAsync(FailingScript, Settings);
        await act.Should().ThrowAsync<Exception>();

        // Another writer gets the lock at once (the busy timeout is one second) and the failed ALTER left no trace.
        var other = new DalSqlite(_connectionString);
        await other.ExecuteNonQueryAsync("BEGIN IMMEDIATE; INSERT INTO t (x) VALUES (1); COMMIT;", Settings, cancellationToken: TestContext.Current.CancellationToken);
        (await other.ExecuteScalarAsync("SELECT COUNT(*) FROM t;", Settings, cancellationToken: TestContext.Current.CancellationToken)).Should().Be(1L);
        (await other.ExecuteScalarAsync("SELECT COUNT(*) FROM pragma_table_info('t') WHERE name = 'y';", Settings, cancellationToken: TestContext.Current.CancellationToken)).Should().Be(0L,
            "the first ALTER TABLE of the failed script is rolled back with the script's transaction");
    }

    [Fact]
    public async Task ExecuteNonQuery_FailingScriptWithOwnTransaction_RollsBackAndReleasesTheWriteLock()
    {
        var dal = new DalSqlite(_connectionString);
        dal.ExecuteNonQuery("CREATE TABLE t (x INTEGER);", Settings);

        var act = () => dal.ExecuteNonQuery(FailingScript, Settings);
        act.Should().Throw<Exception>();

        var other = new DalSqlite(_connectionString);
        await other.ExecuteNonQueryAsync("BEGIN IMMEDIATE; INSERT INTO t (x) VALUES (1); COMMIT;", Settings, cancellationToken: TestContext.Current.CancellationToken);
        (await other.ExecuteScalarAsync("SELECT COUNT(*) FROM pragma_table_info('t') WHERE name = 'y';", Settings, cancellationToken: TestContext.Current.CancellationToken)).Should().Be(0L);
    }
}
