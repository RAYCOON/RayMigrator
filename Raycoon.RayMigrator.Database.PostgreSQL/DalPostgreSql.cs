
using System.Data;
using System.Data.Common;
using System.Reflection;
using Npgsql;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Database.PostgreSQL;

[DatabaseType("PostgreSQL")]
public class DalPostgreSql : DalBase, IDal
{
    private readonly string _connectionString;
    public override string DatabaseType { get; }
    public override DalSpecificProperties DalSpecificProperties { get; }

    public DalPostgreSql(string connectionString)
    {
        _connectionString = connectionString;
        DatabaseType = this.GetType().GetCustomAttribute<DatabaseTypeAttribute>()!.DatabaseType;
        DalSpecificProperties = new DalSpecificProperties
        {
            SqlBlockDelimiter = ";",
            SqlMultiLineCommentStart = "/*",
            SqlMultiLineCommentEnd = "*/",
            SupportsSchema = true,
            SupportsTransactionalDdl = true,
            IdentifierQuoteStart = "\"",
            IdentifierQuoteEnd = "\"",
            DefaultSchema = "public",
            FoldsUnquotedIdentifiersToLower = true,
        };
    }

    // Transient PostgreSQL SQLSTATE codes that trigger automatic retry.
    private static readonly string[] s_transientSqlStates =
    [
        "08000", // connection_exception - General connection error
        "08003", // connection_does_not_exist - Connection dropped
        "08006", // connection_failure - Connection lost during communication
        "08001", // sqlclient_unable_to_establish_sqlconnection - Cannot establish connection
        "08004", // sqlserver_rejected_establishment_of_sqlconnection - Connection rejected (e.g. max_connections exceeded)
        "57P01", // admin_shutdown - Server shutting down
        "57P02", // crash_shutdown - Server crashed
        "57P03", // cannot_connect_now - Server is starting up or recovering
        "40001", // serialization_failure - Concurrent transaction conflict
        "40P01", // deadlock_detected - Transaction deadlock
    ];

    public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
    {
        if (ex is PostgresException pgEx && pgEx.SqlState != null)
            return (s_transientSqlStates.Contains(pgEx.SqlState), pgEx.SqlState);
        return base.IsTransient(ex);
    }

    public override void CheckConnectionStringOrValidateConnection(bool validateConnection)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        if (validateConnection)
        {
            connection.Open();
            connection.Close();
        }
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        await ExecuteWithRetryAsync(
            async () => { await ExecuteNonQueryAsyncInternal(sqlCode, dalSettings, dalParameterList); }, dalSettings);
    }

    private async Task ExecuteNonQueryAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        if (dalSettings.UseTransaction)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                await command.ExecuteNonQueryAsync();
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
            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
            await command.ExecuteNonQueryAsync();
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

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        if (dalSettings.UseTransaction)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                using var command = new NpgsqlCommand(sql, connection, transaction);
                command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                command.ExecuteNonQuery();
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
            using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
            command.ExecuteNonQuery();
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

        await using var connection = new NpgsqlConnection(_connectionString);

        // Capture RAISE NOTICE output for DO blocks that communicate results via RAISE NOTICE
        string? lastNotice = null;
        connection.Notice += (_, args) => { lastNotice = args.Notice.MessageText; };

        await connection.OpenAsync();

        if (dalSettings.UseTransaction)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                var result = await command.ExecuteScalarAsync();
                await transaction.CommitAsync();
                return result ?? lastNotice;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
            var result = await command.ExecuteScalarAsync();
            return result ?? lastNotice;
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

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[reader.GetName(i)] = value;
            }
            results.Add(row);
        }

        return results;
    }

    public override async Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
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
        return new NpgsqlConnection(_connectionString);
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)connection, (NpgsqlTransaction)transaction);
        command.CommandTimeout = commandTimeoutInSeconds;
        await command.ExecuteNonQueryAsync();
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        // Capture RAISE NOTICE output for DO blocks that communicate results via RAISE NOTICE
        string? lastNotice = null;
        var npgsqlConnection = (NpgsqlConnection)connection;
        NoticeEventHandler handler = (_, args) => { lastNotice = args.Notice.MessageText; };
        npgsqlConnection.Notice += handler;

        try
        {
            await using var command = new NpgsqlCommand(sql, npgsqlConnection, (NpgsqlTransaction)transaction);
            command.CommandTimeout = commandTimeoutInSeconds;
            var result = await command.ExecuteScalarAsync();
            return result ?? lastNotice;
        }
        finally
        {
            npgsqlConnection.Notice -= handler;
        }
    }

    /// <summary>
    /// Substitutes @paramName placeholders with actual values in the SQL text.
    /// Required for PostgreSQL because DO $$ blocks treat @paramName as literal text,
    /// not as parameterized query placeholders.
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
        if (param.ParameterValue == null)
            return "NULL";

        if (param.ParameterType == typeof(string))
            return "'" + param.ParameterValue.ToString()!.Replace("\\", "\\\\").Replace("'", "''") + "'";

        if (param.ParameterType == typeof(bool))
            return (bool)param.ParameterValue ? "TRUE" : "FALSE";

        return param.ParameterValue.ToString()!;
    }
}
