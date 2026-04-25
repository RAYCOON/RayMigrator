using System.Data;
using System.Data.Common;
using System.Reflection;
using MySqlConnector;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Database.MySql;

[DatabaseType("MySql")]
public class DalMySql : DalBase, IDal
{
    private readonly string _connectionString;
    public override string DatabaseType { get; }
    public override DalSpecificProperties DalSpecificProperties { get; }

    public DalMySql(string connectionString)
    {
        _connectionString = EnsureConnectionStringOptions(connectionString);
        DatabaseType = this.GetType().GetCustomAttribute<DatabaseTypeAttribute>()!.DatabaseType;
        DalSpecificProperties = new DalSpecificProperties
        {
            SqlBlockDelimiter = ";",
            SqlMultiLineCommentStart = "/*",
            SqlMultiLineCommentEnd = "*/",
            SupportsSchema = false,
            SupportsTransactionalDdl = false,
            IdentifierQuoteStart = "`",
            IdentifierQuoteEnd = "`",
        };
    }

    // Transient MySQL error codes that trigger automatic retry.
    private static readonly string[] s_transientCodes =
    [
        "1040", // ER_CON_COUNT_ERROR - Too many connections
        "1205", // ER_LOCK_WAIT_TIMEOUT - Lock wait timeout exceeded
        "1213", // ER_LOCK_DEADLOCK - Deadlock found when trying to get lock
        "1614", // ER_TRANSACTION_RESOLUTION_UNKNOWN - Transaction rolled back (semi-sync replication uncertainty)
        "2002", // CR_CONNECTION_ERROR - Can't connect through socket
        "2003", // CR_CONN_HOST_ERROR - Can't connect to server
        "2006", // CR_SERVER_GONE_ERROR - Server has gone away
        "2013", // CR_SERVER_LOST - Lost connection during query
        "2055", // CR_SERVER_LOST_EXTENDED - Lost connection at reading authorization packet
    ];

    public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
    {
        if (ex is MySqlException mysqlEx)
        {
            var code = mysqlEx.Number.ToString();
            return (s_transientCodes.Contains(code), code);
        }
        return base.IsTransient(ex);
    }

    public override void CheckConnectionStringOrValidateConnection(bool validateConnection)
    {
        using var connection = new MySqlConnection(_connectionString);
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

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await SetSessionTimezoneAsync(connection);

        if (dalSettings.UseTransaction)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
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
            await using var command = new MySqlCommand(sql, connection);
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

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();
        SetSessionTimezone(connection);

        if (dalSettings.UseTransaction)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                using var command = new MySqlCommand(sql, connection, transaction);
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
            using var command = new MySqlCommand(sql, connection);
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

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await SetSessionTimezoneAsync(connection);

        if (dalSettings.UseTransaction)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                var result = await command.ExecuteScalarAsync();
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
            await using var command = new MySqlCommand(sql, connection);
            command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
            return await command.ExecuteScalarAsync();
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

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await SetSessionTimezoneAsync(connection);

        await using var command = new MySqlCommand(sql, connection);
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
            await using var connection = new MySqlConnection(EnsureConnectionStringOptions(connectionString));
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
        return new MySqlConnection(_connectionString);
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var command = new MySqlCommand(sql, (MySqlConnection)connection, (MySqlTransaction)transaction);
        command.CommandTimeout = commandTimeoutInSeconds;
        await command.ExecuteNonQueryAsync();
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var command = new MySqlCommand(sql, (MySqlConnection)connection, (MySqlTransaction)transaction);
        command.CommandTimeout = commandTimeoutInSeconds;
        return await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// Pins the session time zone to <c>+00:00</c> (UTC) on an already-open
    /// connection. Called immediately after every successful <c>OpenAsync()</c>
    /// in the internal execution methods so that <c>TIMESTAMP</c> columns
    /// round-trip as UTC regardless of the server's local time zone or the
    /// pool-lease state. Executed as a separate round-trip (not prepended to
    /// user SQL) to keep ADO.NET's reader-state model simple.
    /// </summary>
    private static async Task SetSessionTimezoneAsync(MySqlConnection connection)
    {
        await using var cmd = new MySqlCommand("SET time_zone = '+00:00';", connection);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Synchronous counterpart to <see cref="SetSessionTimezoneAsync"/>.
    /// </summary>
    private static void SetSessionTimezone(MySqlConnection connection)
    {
        using var cmd = new MySqlCommand("SET time_zone = '+00:00';", connection);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Ensures required MySqlConnector options are present in the connection string.
    /// AllowUserVariables: Required because MySQL templates use session variables (@v_count, @dummy, etc.).
    /// </summary>
    internal static string EnsureConnectionStringOptions(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Substitutes @paramName placeholders with actual values in the SQL text.
    /// Required because MySQL templates use session variables (@v_count, @dummy) alongside
    /// command parameters (@ProductId, @Name). Manual substitution of command parameters
    /// ensures they don't conflict with session variables.
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
            return "'" + param.ParameterValue.ToString()!.Replace("'", "''").Replace("\\", "\\\\") + "'";

        if (param.ParameterType == typeof(bool))
            return (bool)param.ParameterValue ? "1" : "0";

        return param.ParameterValue.ToString()!;
    }
}
