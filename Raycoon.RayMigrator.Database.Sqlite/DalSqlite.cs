
using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Database.Sqlite;

[DatabaseType("Sqlite")]
public class DalSqlite : DalBase, IDal
{
    private readonly string _connectionString;
    public override string DatabaseType { get; }
    public override DalSpecificProperties DalSpecificProperties { get; }

    public DalSqlite(string connectionString)
    {
        _connectionString = EnsureForeignKeysEnabled(connectionString);
        DatabaseType = this.GetType().GetCustomAttribute<DatabaseTypeAttribute>()!.DatabaseType;
        DalSpecificProperties = new DalSpecificProperties
        {
            SqlBlockDelimiter = ";",
            SqlMultiLineCommentStart = "/*",
            SqlMultiLineCommentEnd = "*/",
            SupportsSchema = false,
            SupportsTransactionalDdl = true,
            IdentifierQuoteStart = "\"",
            IdentifierQuoteEnd = "\"",
        };
    }

    // Transient SQLite error codes that trigger automatic retry.
    private static readonly string[] s_transientCodes =
    [
        "5", // SQLITE_BUSY - Database file is locked by another process
        "6", // SQLITE_LOCKED - Table in the database is locked
    ];

    public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
    {
        if (ex is SqliteException sqliteEx)
        {
            var code = sqliteEx.SqliteErrorCode.ToString();
            return (s_transientCodes.Contains(code), code);
        }
        return base.IsTransient(ex);
    }

    public override void CheckConnectionStringOrValidateConnection(bool validateConnection)
    {
        using var connection = new SqliteConnection(_connectionString);
        if (validateConnection)
        {
            connection.Open();
            using var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = "PRAGMA journal_mode=WAL;";
            pragmaCommand.ExecuteNonQuery();
            connection.Close();
        }
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        await ExecuteWithRetryAsync(
            async () => { await ExecuteNonQueryAsyncInternal(sqlCode, dalSettings, dalParameterList); }, dalSettings);
    }

    /// <summary>
    /// Executes all statements in a multi-statement SQL batch via ExecuteReader + NextResult.
    /// Microsoft.Data.Sqlite's documented batching mechanism — ensures all statements execute
    /// and errors in later statements are thrown in the caller's context (not during Dispose).
    /// See: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/batching
    /// </summary>
    private static async Task ExecuteAllStatementsAsync(SqliteCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.NextResultAsync()) { }
    }

    /// <summary>Synchronous version of ExecuteAllStatementsAsync.</summary>
    private static void ExecuteAllStatements(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        while (reader.NextResult()) { }
    }

    /// <summary>
    /// Executes all statements and returns the scalar value from the last result set with rows.
    /// RayMigrator templates follow: setup DDL/DML (no result sets) → final SELECT with result
    /// code → optional cleanup DROP. Only the SELECT produces HasRows=true.
    /// </summary>
    private static async Task<object?> ExecuteScalarAllStatementsAsync(SqliteCommand command)
    {
        object? result = null;
        await using var reader = await command.ExecuteReaderAsync();
        do
        {
            if (reader.HasRows && await reader.ReadAsync())
            {
                result = reader.IsDBNull(0) ? null : reader.GetValue(0);
            }
        } while (await reader.NextResultAsync());
        return result;
    }

    private async Task ExecuteNonQueryAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        if (dalSettings.UseTransaction)
        {
            await using var transaction = connection.BeginTransaction();
            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                await ExecuteAllStatementsAsync(command);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
            await ExecuteAllStatementsAsync(command);
        }
    }

    public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        ExecuteWithRetry(
            () => { ExecuteNonQueryInternal(sqlCode, dalSettings, dalParameterList); }, dalSettings);
    }

    private void ExecuteNonQueryInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        if (dalSettings.UseTransaction)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                ExecuteAllStatements(command);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        else
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
            ExecuteAllStatements(command);
        }
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        return await ExecuteWithRetryAsync(
            async () => await ExecuteScalarAsyncInternal(sqlCode, dalSettings, dalParameterList), dalSettings);
    }

    private async Task<object?> ExecuteScalarAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        if (dalSettings.UseTransaction)
        {
            await using var transaction = connection.BeginTransaction();
            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                var result = await ExecuteScalarAllStatementsAsync(command);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
            return await ExecuteScalarAllStatementsAsync(command);
        }
    }

    public override async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        return await ExecuteWithRetryAsync(
            async () => await ExecuteReaderAsyncInternal(sqlCode, dalSettings, dalParameterList), dalSettings);
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteReaderAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;
        var results = new List<Dictionary<string, object?>>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;

        await using var reader = await command.ExecuteReaderAsync();
        do
        {
            if (reader.FieldCount > 0)
            {
                var currentBatch = new List<Dictionary<string, object?>>();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    currentBatch.Add(row);
                }
                if (currentBatch.Count > 0)
                    results = currentBatch;
            }
        } while (await reader.NextResultAsync());

        return results;
    }

    public override async Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings)
    {
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override DbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var command = ((SqliteConnection)connection).CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        command.CommandTimeout = commandTimeoutInSeconds;
        await ExecuteAllStatementsAsync(command);
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var command = ((SqliteConnection)connection).CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        command.CommandTimeout = commandTimeoutInSeconds;
        return await ExecuteScalarAllStatementsAsync(command);
    }

    /// <summary>
    /// Substitutes @paramName placeholders with actual values in the SQL text.
    /// SQLite templates use multi-statement scripts where standard parameterized queries
    /// would not work across statement boundaries.
    /// </summary>
    internal static string SubstituteParameters(string sqlCode, DalParameterList dalParameterList)
    {
        string result = sqlCode;

        // Sort by name length descending to avoid partial matches (e.g., @ProductId before @Product)
        var sortedParams = dalParameterList.GetAllParameters()
            .OrderByDescending(p => p.Value.ParameterName.Length)
            .ToList();

        foreach (var param in sortedParams)
        {
            string placeholder = $"@{param.Value.ParameterName}";
            string replacement = FormatParameterValue(param.Value);
            result = result.Replace(placeholder, replacement);
        }

        return result;
    }

    internal static string FormatParameterValue(DalParameter param)
    {
        if (param.ParameterValue == null || param.ParameterValue is DBNull)
            return "NULL";

        if (param.ParameterType == typeof(string))
            return "'" + param.ParameterValue.ToString()!.Replace("'", "''") + "'";

        if (param.ParameterType == typeof(bool))
            return (bool)param.ParameterValue ? "1" : "0";

        return param.ParameterValue.ToString()!;
    }

    // SQLite does not enforce foreign key constraints unless PRAGMA foreign_keys = ON
    // is set per connection. Microsoft.Data.Sqlite issues the pragma automatically when
    // the connection string contains Foreign Keys=True. Respect an explicit user setting.
    // Ref: https://www.sqlite.org/pragma.html#pragma_foreign_keys
    internal static string EnsureForeignKeysEnabled(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.ForeignKeys == null)
            builder.ForeignKeys = true;
        return builder.ToString();
    }
}
