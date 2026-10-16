using AwesomeAssertions;
using Raycoon.RayMigrator.Repository.ReadModel;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The indexes of the log table every engine keeps: <c>DatabaseLogging_CheckCreate</c> gives <c>MigrationLog</c> an
/// index on <c>MigrationRunId</c> and one on <c>CreatedAt</c>, the access paths of the readers of the log and of its
/// cleanup by age. The indexes stand outside the creation branch, so a log table that an older engine created without
/// them gets them at the next initialization, which still reports that the log existed. The tests run the
/// initialization of the host's database log writer and read the indexes from the catalog of the log database; they
/// do not need the asynchronous writer. Hosts that start at the same moment against a log database without log tables
/// all initialize the log, and the event catalogue holds each event once.
/// </summary>
internal static class DatabaseLogIndexContract
{
    /// <summary>
    /// The longest table prefix RULE_4_4 allows (16 bytes), in lower case for PostgreSQL, MySQL and MariaDB: with it the
    /// longer index name of the log has 49 bytes, within the 63 PostgreSQL keeps and the 64 MySQL accepts.
    /// </summary>
    public const string LongestPrefix = "rm_sixteen_bytes";

    /// <summary>The table prefix of the parallel first starts: a prefix of their own, whose log tables the rounds drop.</summary>
    public const string ParallelStartsPrefix = "rm_lograce_";

    /// <summary>How many hosts start at the same moment in each round of the parallel first starts.</summary>
    private const int ParallelStarts = 8;

    /// <summary>How many rounds of parallel first starts run: the race of two first starts is narrow.</summary>
    private const int Rounds = 10;

    /// <summary>
    /// Hosts that start at the same moment against a log database without log tables all initialize the log, at least
    /// one of them reports that it created the log, and the event catalogue holds each event once. SQL Server,
    /// PostgreSQL and MySQL look for the log tables under the lock of the log initialization, so one start creates them
    /// and inserts the catalogue; MariaDB and SQLite take no lock, so several starts can find no log table and insert
    /// the catalogue, which they must tolerate. In each round the test drops both log tables of its prefix and starts
    /// <see cref="ParallelStarts"/> writers of their own, each on a connection of its own, at once.
    /// </summary>
    public static async Task VerifyParallelFirstStartsInsertTheCatalogueOnceAsync(ScenarioBuilder builder)
    {
        // Arrange: the host created the log tables and the catalogue while it started; its writer is flushed and closed,
        // so that it writes nothing into the tables the rounds drop
        await using var ctx = await builder.WithTableBaseName(ParallelStartsPrefix).WithDatabaseLogging().BuildAsync();
        ctx.FlushDatabaseLog();
        var (naming, log) = LogOf(ctx);
        string connectionString = ctx.DatabaseLoggingOptions.ConnectionString!;
        int catalogue = log.CountRows("MigrationEvent");
        catalogue.Should().BePositive(because: "the start of the host inserted the event catalogue");
        var failedStarts = new List<string>();
        var findings = new List<string>();

        // Act
        for (int round = 1; round <= Rounds; round++)
        {
            log.ExecuteOnConnection(connectionString, DropTableSql(naming, RepositoryTable.MigrationLog));
            log.ExecuteOnConnection(connectionString, DropTableSql(naming, RepositoryTable.MigrationEvent));
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var starts = Enumerable.Range(0, ParallelStarts).Select(_ => ctx.InitDatabaseLoggerWithNewWriterAsync(start.Task)).ToList();
            start.SetResult();

            int created = 0;
            foreach (var task in starts)
            {
                try
                {
                    if (await task)
                        created++;
                }
                catch (Exception ex)
                {
                    var cause = ex.GetBaseException();
                    failedStarts.Add($"round {round}: {cause.GetType().Name}: {cause.Message}");
                }
            }

            int rows = log.CountRows("MigrationEvent");
            if (rows != catalogue)
                findings.Add($"round {round}: {rows} catalogue rows instead of {catalogue}");
            if (created == 0)
                findings.Add($"round {round}: no start reported that it created the log");
        }

        // Assert
        failedStarts.Should().BeEmpty(because: "every start initializes the log, but {0} of {1} parallel first starts failed",
            failedStarts.Count, Rounds * ParallelStarts);
        findings.Should().BeEmpty(because: "the catalogue holds each event once after every round, and a start created the log");
    }

    /// <summary>
    /// A fresh log table carries both indexes under the names and on the columns of the engine, and the next
    /// initialization finds the tables and both indexes and creates nothing.
    /// </summary>
    public static async Task VerifyFreshLogTableCarriesTheIndexesAsync(ScenarioBuilder builder)
    {
        // Arrange: the scenario drops the log tables, then the host creates them while it starts
        await using var ctx = await builder.WithDatabaseLogging().BuildAsync();
        var (naming, log) = LogOf(ctx);
        var (runIdIndex, createdAtIndex) = IndexNames(naming);

        // Act
        bool createdAgain = await ctx.InitDatabaseLoggerAsync();

        // Assert
        log.GetIndexColumns(runIdIndex).Should().Equal([naming.ColumnName("MigrationRunId")], because: "the readers of the log filter by run");
        log.GetIndexColumns(createdAtIndex).Should().Equal([naming.ColumnName("CreatedAt")], because: "the cleanup of the log deletes by age");
        createdAgain.Should().BeFalse(because: "the next initialization finds the log tables and reports 0");
    }

    /// <summary>
    /// A log table without the indexes, as an engine before 0.20.0 left it, gets them at the next initialization: the
    /// initialization reports that the log existed (result 0), and the rows of the log stay.
    /// </summary>
    public static async Task VerifyLogTableOfAnOlderEngineGetsTheIndexesAsync(ScenarioBuilder builder)
    {
        // Arrange: the writer is flushed and closed so that only the arranged rows change the count
        await using var ctx = await builder.WithDatabaseLogging().BuildAsync();
        ctx.FlushDatabaseLog();
        var (naming, log) = LogOf(ctx);
        var (runIdIndex, createdAtIndex) = IndexNames(naming);
        string connectionString = ctx.DatabaseLoggingOptions.ConnectionString!;
        log.ExecuteOnConnection(connectionString, InsertLogRowsSql(naming));
        log.ExecuteOnConnection(connectionString, DropIndexSql(naming, runIdIndex));
        log.ExecuteOnConnection(connectionString, DropIndexSql(naming, createdAtIndex));
        log.GetIndexColumns(runIdIndex).Should().BeEmpty(because: "the arrangement dropped the index");
        log.GetIndexColumns(createdAtIndex).Should().BeEmpty(because: "the arrangement dropped the index");
        int rows = log.CountLogEntries();
        rows.Should().BeGreaterThanOrEqualTo(3, because: "the arrangement wrote three rows");

        // Act
        bool created = await ctx.InitDatabaseLoggerAsync();

        // Assert
        created.Should().BeFalse(because: "the log tables existed, so the initialization reports 0 although it added the indexes");
        log.GetIndexColumns(runIdIndex).Should().Equal([naming.ColumnName("MigrationRunId")], because: "the indexes stand outside the creation branch");
        log.GetIndexColumns(createdAtIndex).Should().Equal([naming.ColumnName("CreatedAt")], because: "the indexes stand outside the creation branch");
        log.CountLogEntries().Should().Be(rows, because: "adding the indexes keeps the rows of the log");
    }

    /// <summary>The naming and a query helper of the log database the host writes to, with its own schema and prefix.</summary>
    private static (RepositoryNaming Naming, RepositoryQueryHelper Log) LogOf(ScenarioContext ctx)
    {
        var options = ctx.DatabaseLoggingOptions;
        string schemaName = options.SchemaName ?? string.Empty;
        string tableBaseName = options.TableBaseName ?? string.Empty;
        var naming = new RepositoryNaming(options.DatabaseType!, string.IsNullOrEmpty(schemaName) ? null : schemaName, tableBaseName);
        return (naming, new RepositoryQueryHelper(options.DatabaseType!, options.ConnectionString!, schemaName, tableBaseName));
    }

    /// <summary>The two index names of the log table as the templates write them on the engine, prefix included.</summary>
    private static (string RunId, string CreatedAt) IndexNames(RepositoryNaming naming) => naming.UsesSnakeCase
        ? ($"ix_{naming.TableBaseName}migration_log_migration_run_id", $"ix_{naming.TableBaseName}migration_log_created_at")
        : ($"ix_{naming.TableBaseName}MigrationLog_MigrationRunId", $"ix_{naming.TableBaseName}MigrationLog_CreatedAt");

    /// <summary>Three log rows of two runs; the other columns keep their defaults.</summary>
    private static string InsertLogRowsSql(RepositoryNaming naming)
    {
        string table = naming.QualifiedTableName(RepositoryTable.MigrationLog);
        string columns = string.Join(", ", new[] { "LogLevelId", "MigrationRunId", "Message" }.Select(naming.QualifiedColumnName));
        return $"INSERT INTO {table} ({columns}) VALUES (2, 7, 'kept'), (2, 7, 'kept'), (3, 8, 'kept')";
    }

    /// <summary>A <c>DROP TABLE IF EXISTS</c> of a log table, with the schema and prefix of the log.</summary>
    private static string DropTableSql(RepositoryNaming naming, RepositoryTable table)
        => $"DROP TABLE IF EXISTS {naming.QualifiedTableName(table)}";

    /// <summary>A <c>DROP INDEX</c> of the log table in the syntax of the engine.</summary>
    private static string DropIndexSql(RepositoryNaming naming, string indexName) => naming.DatabaseType switch
    {
        "SqlServer" or "MariaDb" or "MySql" => $"DROP INDEX {naming.Quote(indexName)} ON {naming.QualifiedTableName(RepositoryTable.MigrationLog)}",
        "PostgreSQL" => $"DROP INDEX {naming.Quote(naming.SchemaName!)}.{naming.Quote(indexName)}",
        _ => $"DROP INDEX {naming.Quote(indexName)}"
    };
}
