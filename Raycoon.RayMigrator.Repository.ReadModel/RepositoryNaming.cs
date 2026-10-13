using System.Text;

namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>
/// The names a repository has on one database engine. The engine templates write the tables in PascalCase on SQL
/// Server and SQLite and in snake_case on PostgreSQL, MySQL and MariaDB (DAL-017, DAL-018); SQL Server and
/// PostgreSQL put them into the configured schema, the others into the connection's database; every table name
/// carries the configured <c>TableBaseName</c> prefix. Column names follow the same case rule and are the record
/// property names of <see cref="RepositoryRows"/> in PascalCase.
/// </summary>
public sealed class RepositoryNaming
{
    private const string SqlServer = "SqlServer";
    private const string PostgreSql = "PostgreSQL";
    private const string MariaDb = "MariaDb";
    private const string MySql = "MySql";
    private const string Sqlite = "Sqlite";

    /// <summary>
    /// Creates the naming of one repository.
    /// </summary>
    /// <param name="databaseType">SqlServer, PostgreSQL, MariaDb, MySql or Sqlite (case-insensitive).</param>
    /// <param name="schemaName">The configured <c>Repository.SchemaName</c>; used by SQL Server and PostgreSQL only.</param>
    /// <param name="tableBaseName">The configured <c>Repository.TableBaseName</c>; null means no prefix.</param>
    /// <exception cref="ArgumentException"><paramref name="databaseType"/> is not one of the five engines.</exception>
    public RepositoryNaming(string databaseType, string? schemaName, string? tableBaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseType);
        DatabaseType = Canonical(databaseType);
        TableBaseName = tableBaseName ?? string.Empty;
        SupportsSchema = DatabaseType is SqlServer or PostgreSql;
        UsesSnakeCase = DatabaseType is PostgreSql or MariaDb or MySql;
        SchemaName = SupportsSchema ? schemaName : null;

        if (SupportsSchema && string.IsNullOrWhiteSpace(schemaName))
            throw new ArgumentException($"A {DatabaseType} repository needs a schema name.", nameof(schemaName));
    }

    /// <summary>The canonical engine name: SqlServer, PostgreSQL, MariaDb, MySql or Sqlite.</summary>
    public string DatabaseType { get; }

    /// <summary>The schema of the repository tables, null for engines without schema support.</summary>
    public string? SchemaName { get; }

    /// <summary>The prefix of every table name, empty when none is configured.</summary>
    public string TableBaseName { get; }

    /// <summary>True for PostgreSQL, MySQL and MariaDB, whose table and column names are snake_case.</summary>
    public bool UsesSnakeCase { get; }

    /// <summary>True for SQL Server and PostgreSQL, whose tables live in <see cref="SchemaName"/>.</summary>
    public bool SupportsSchema { get; }

    /// <summary>The unquoted, unqualified name of a table with its prefix, in the engine's case.</summary>
    public string TableName(RepositoryTable table) => TableBaseName + Identifier(table.ToString());

    /// <summary>The unquoted name of a column, in the engine's case.</summary>
    /// <param name="column">The column in PascalCase, as the record properties name it.</param>
    public string ColumnName(string column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(column);
        return Identifier(column);
    }

    /// <summary>
    /// The name a SQL statement uses for a table: quoted as the engine quotes it and qualified with the schema where
    /// the engine has one, e.g. <c>[ray].[MigrationRun]</c>, <c>ray.migration_run</c>, <c>`migration_run`</c> or
    /// <c>"MigrationRun"</c>.
    /// </summary>
    public string QualifiedTableName(RepositoryTable table)
    {
        string name = Quote(TableName(table));
        return SchemaName == null ? name : $"{Quote(SchemaName)}.{name}";
    }

    /// <summary>The name a SQL statement uses for a column: quoted as the engine quotes it.</summary>
    public string QualifiedColumnName(string column) => Quote(ColumnName(column));

    /// <summary>
    /// A <c>SELECT</c> of every column of <see cref="RepositoryRows.ColumnsOf"/> in record order from the qualified
    /// table, without a <c>WHERE</c> clause; the rows of its result map with <see cref="RepositoryRowMapper"/>.
    /// </summary>
    public string SelectStatement(RepositoryTable table)
    {
        var columns = RepositoryRows.ColumnsOf(table).Select(QualifiedColumnName);
        return $"SELECT {string.Join(", ", columns)} FROM {QualifiedTableName(table)}";
    }

    /// <summary>
    /// Quotes an identifier as the engine's templates do: brackets on SQL Server, backticks on MySQL and MariaDB,
    /// double quotes on SQLite, nothing on PostgreSQL, whose snake_case identifiers are written unquoted (DAL-017).
    /// </summary>
    public string Quote(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return DatabaseType switch
        {
            SqlServer => $"[{identifier}]",
            MariaDb or MySql => $"`{identifier}`",
            Sqlite => $"\"{identifier}\"",
            _ => identifier,
        };
    }

    /// <summary>
    /// The snake_case form of a PascalCase identifier as the PostgreSQL, MySQL and MariaDB templates write it: an
    /// underscore before every upper-case letter that follows a lower-case one, then lower case; the product name
    /// <c>RayMigrator</c> stays one word (<c>raymigrator_version</c>).
    /// </summary>
    public static string ToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        string normalized = pascalCase.Replace("RayMigrator", "Raymigrator", StringComparison.Ordinal);
        var sb = new StringBuilder(normalized.Length + 8);
        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(normalized[i - 1]))
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private string Identifier(string pascalCase) => UsesSnakeCase ? ToSnakeCase(pascalCase) : pascalCase;

    private static string Canonical(string databaseType) => databaseType.ToLowerInvariant() switch
    {
        "sqlserver" => SqlServer,
        "postgresql" => PostgreSql,
        "mariadb" => MariaDb,
        "mysql" => MySql,
        "sqlite" => Sqlite,
        _ => throw new ArgumentException($"Unknown database type [{databaseType}]; the repository read model knows SqlServer, PostgreSQL, MariaDb, MySql and Sqlite.", nameof(databaseType)),
    };
}
