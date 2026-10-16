using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Repository.ReadModel;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// A scenario with a table prefix leaves no table of its prefix behind (<see cref="PrefixTableCleanup"/>): once it is
/// disposed, the catalog of the repository database, and of a log database of its own, lists no table whose name starts
/// with the prefix. The catalog is searched by the prefix and not by the table list of <see cref="DatabaseCleanupHelper"/>,
/// so the contract also fails when the engine creates a table of the prefix that the cleanup does not know: MySQL, MariaDB
/// and SQLite drop the repository tables by name and would keep it, SQL Server and PostgreSQL drop the test schema. The
/// scenario uses the first prefix of <see cref="RepositoryPrefixContract"/>, so that the engine tests keep to the prefixes
/// <c>a_</c>, <c>b_</c> and <see cref="DatabaseLogIndexContract.LongestPrefix"/>.
/// </summary>
internal static class PrefixTableCleanupContract
{
    private static readonly DalSettings QuerySettings = new() { UseTransaction = false, DbCommandTimeoutInSeconds = 30 };

    /// <summary>
    /// A run creates the repository of the prefix and logs into the repository database or, with
    /// <paramref name="logInADatabaseOfItsOwn"/>, into the database of the fixture's second connection string; once the
    /// scenario is disposed, neither database holds a table of the prefix.
    /// </summary>
    public static async Task VerifyDisposeLeavesNoTableOfThePrefixAsync(
        ScenarioBuilder builder,
        EngineConfig config,
        bool logInADatabaseOfItsOwn)
    {
        // Arrange
        const string prefix = RepositoryPrefixContract.FirstPrefix;
        string logDatabase = logInADatabaseOfItsOwn ? config.ConnectionString2! : config.ConnectionString;
        builder.WithTableBaseName(prefix);
        var ctx = await (logInADatabaseOfItsOwn ? builder.WithDatabaseLoggingInto(logDatabase) : builder.WithDatabaseLogging())
            .BuildAsync();
        var naming = new RepositoryNaming(config.DatabaseType, string.IsNullOrEmpty(config.SchemaName) ? null : config.SchemaName, prefix);
        try
        {
            var result = await ctx.MigrateUpAsync();
            result.Success.Should().BeTrue(because: $"the run creates the repository of the prefix: {result.ErrorMessage}");
            (await TablesOfThePrefixAsync(config, config.ConnectionString, prefix)).Should()
                .Contain(naming.TableName(RepositoryTable.MigrationRun), because: "the run created the repository");
            (await TablesOfThePrefixAsync(config, logDatabase, prefix)).Should()
                .Contain(naming.TableName(RepositoryTable.MigrationLog), because: "the host created the log tables in the log database");
        }
        catch
        {
            await ctx.DisposeAsync();
            throw;
        }

        // Act
        await ctx.DisposeAsync();

        // Assert
        (await TablesOfThePrefixAsync(config, config.ConnectionString, prefix)).Should().BeEmpty(
            because: "the scenario drops the repository and log tables of its prefix when it ends");
        (await TablesOfThePrefixAsync(config, logDatabase, prefix)).Should().BeEmpty(
            because: "the scenario drops the log tables of its prefix also in a log database of their own");
    }

    /// <summary>
    /// The tables whose name starts with <paramref name="prefix"/>, from the catalog of the database of
    /// <paramref name="connectionString"/>: in the test schema on SQL Server and PostgreSQL, in the database of the
    /// connection on MySQL and MariaDB, in the file on SQLite.
    /// </summary>
    private static async Task<IReadOnlyList<string>> TablesOfThePrefixAsync(EngineConfig config, string connectionString, string prefix)
    {
        int length = prefix.Length;
        string sql = config.DatabaseType switch
        {
            "SqlServer" => "SELECT STRING_AGG(t.name, '|') FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
                $"WHERE s.name = '{config.SchemaName}' AND LEFT(t.name, {length}) = '{prefix}'",
            "PostgreSQL" => "SELECT string_agg(table_name::text, '|') FROM information_schema.tables " +
                $"WHERE table_schema = '{config.SchemaName}' AND left(table_name::text, {length}) = '{prefix}'",
            "MariaDb" or "MySql" => "SELECT GROUP_CONCAT(table_name SEPARATOR '|') FROM information_schema.tables " +
                $"WHERE table_schema = DATABASE() AND LEFT(table_name, {length}) = '{prefix}'",
            "Sqlite" => "SELECT group_concat(name, '|') FROM sqlite_master " +
                $"WHERE type = 'table' AND substr(name, 1, {length}) = '{prefix}'",
            _ => throw new NotSupportedException($"Unsupported database type: {config.DatabaseType}")
        };

        DalFactory.TryGetDal(config.DatabaseType, connectionString, out IDal? dal).Should().BeTrue();
        object? names = await dal!.ExecuteScalarAsync(sql, QuerySettings, null, TestContext.Current.CancellationToken);
        return names is string { Length: > 0 } text ? text.Split('|') : [];
    }
}
