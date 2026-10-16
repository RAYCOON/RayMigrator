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
/// do not need the asynchronous writer.
/// </summary>
internal static class DatabaseLogIndexContract
{
    /// <summary>
    /// The longest table prefix RULE_4_4 allows (16 bytes), in lower case for PostgreSQL, MySQL and MariaDB: with it the
    /// longer index name of the log has 49 bytes, within the 63 PostgreSQL keeps and the 64 MySQL accepts.
    /// </summary>
    public const string LongestPrefix = "rm_sixteen_bytes";

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

    /// <summary>A <c>DROP INDEX</c> of the log table in the syntax of the engine.</summary>
    private static string DropIndexSql(RepositoryNaming naming, string indexName) => naming.DatabaseType switch
    {
        "SqlServer" or "MariaDb" or "MySql" => $"DROP INDEX {naming.Quote(indexName)} ON {naming.QualifiedTableName(RepositoryTable.MigrationLog)}",
        "PostgreSQL" => $"DROP INDEX {naming.Quote(naming.SchemaName!)}.{naming.Quote(indexName)}",
        _ => $"DROP INDEX {naming.Quote(indexName)}"
    };
}
