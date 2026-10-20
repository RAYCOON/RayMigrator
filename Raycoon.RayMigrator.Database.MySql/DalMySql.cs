using System.Data;
using System.Data.Common;
using System.Globalization;
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

    // Built-in transient MySQL error codes; a TransientErrorCodes.txt in DataAccessLayers/MySql/ replaces them (ADR-022).
    private static readonly string[] TransientCodes =
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

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> DefaultTransientErrorCodes => TransientCodes;

    /// <summary>The command timeout of the statement that releases the locks of a session after a failed call.</summary>
    private const int UserLockReleaseTimeoutInSeconds = 5;

    public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
    {
        if (ex is MySqlException mysqlEx)
            return IsTransientCode(mysqlEx.Number.ToString(CultureInfo.InvariantCulture));
        return base.IsTransient(ex);
    }

    /// <inheritdoc />
    /// <remarks>MySqlConnector reports a command timeout as a <see cref="MySqlException"/> with <c>CommandTimeoutExpired</c>.</remarks>
    public override bool IsTimeout(Exception ex) => ex is MySqlException { ErrorCode: MySqlErrorCode.CommandTimeoutExpired } || base.IsTimeout(ex);

    public override void CheckConnectionStringOrValidateConnection(bool validateConnection)
    {
        using var connection = new MySqlConnection(_connectionString);
        if (validateConnection)
        {
            connection.Open();
            connection.Close();
        }
    }

    protected override async Task ExecuteNonQueryCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
    {
        await ExecuteWithRetryAsync(
            async () => { await ExecuteNonQueryAsyncInternal(sqlCode, dalSettings, dalParameterList, cancellationToken); }, dalSettings,
            cancellationToken: cancellationToken);
    }

    private async Task ExecuteNonQueryAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        try
        {
            await SetSessionTimezoneAsync(connection, cancellationToken);

            if (dalSettings.UseTransaction)
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await using var command = new MySqlCommand(sql, connection, transaction);
                    command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    // No token: a commit that has started must complete, or the caller cannot tell whether it happened.
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
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch
        {
            await ReleaseUserLocksAsync(connection);
            throw;
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
        try
        {
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
        catch
        {
            ReleaseUserLocks(connection);
            throw;
        }
    }

    protected override async Task<object?> ExecuteScalarCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryAsync(
            async () => await ExecuteScalarAsyncInternal(sqlCode, dalSettings, dalParameterList, cancellationToken), dalSettings,
            cancellationToken: cancellationToken);
    }

    private async Task<object?> ExecuteScalarAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        try
        {
            await SetSessionTimezoneAsync(connection, cancellationToken);

            if (dalSettings.UseTransaction)
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await using var command = new MySqlCommand(sql, connection, transaction);
                    command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                    var result = await command.ExecuteScalarAsync(cancellationToken);
                    // No token: a commit that has started must complete, or the caller cannot tell whether it happened.
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
                return await command.ExecuteScalarAsync(cancellationToken);
            }
        }
        catch
        {
            await ReleaseUserLocksAsync(connection);
            throw;
        }
    }

    protected override async Task<List<Dictionary<string, object?>>> ExecuteReaderCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryAsync(
            async () => await ExecuteReaderAsyncInternal(sqlCode, dalSettings, dalParameterList, cancellationToken), dalSettings,
            cancellationToken: cancellationToken);
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteReaderAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;
        var results = new List<Dictionary<string, object?>>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        try
        {
            await SetSessionTimezoneAsync(connection, cancellationToken);

            await using var command = new MySqlCommand(sql, connection);
            command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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
        catch
        {
            // The reader is closed here: the using of the try block ended before the catch
            await ReleaseUserLocksAsync(connection);
            throw;
        }
    }

    protected override async Task<bool> IsConnectionValidCoreAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new MySqlConnection(EnsureConnectionStringOptions(connectionString));
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public override DbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    protected override async Task ExecuteNonQueryCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var command = new MySqlCommand(sql, (MySqlConnection)connection, (MySqlTransaction)transaction);
        command.CommandTimeout = commandTimeoutInSeconds;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected override async Task<object?> ExecuteScalarCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken)
    {
        string sql = dalParameterList != null ? SubstituteParameters(sqlCode, dalParameterList) : sqlCode;

        await using var command = new MySqlCommand(sql, (MySqlConnection)connection, (MySqlTransaction)transaction);
        command.CommandTimeout = commandTimeoutInSeconds;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>
    /// Pins the session time zone to <c>+00:00</c> (UTC) on an already-open
    /// connection. Called immediately after every successful <c>OpenAsync()</c>
    /// in the internal execution methods so that <c>TIMESTAMP</c> columns
    /// round-trip as UTC regardless of the server's local time zone or the
    /// pool-lease state. Executed as a separate round-trip (not prepended to
    /// user SQL) to keep ADO.NET's reader-state model simple.
    /// </summary>
    private static async Task SetSessionTimezoneAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = new MySqlCommand("SET time_zone = '+00:00';", connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
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
    /// Releases the locks the session holds through <c>GET_LOCK</c> after a call on a connection of its own failed. A
    /// template that takes such a lock releases it itself, but one that failed, timed out or was cancelled between the
    /// two leaves the lock on the session, and the pool keeps the session with its lock until it hands the connection out
    /// again or prunes it as idle (<c>ConnectionIdleTimeout</c>, 180 seconds by default): every other host waits for the
    /// lock until then. Best effort and without the token of the call; a session that is gone has lost its locks anyway.
    /// The shared-connection calls leave this to the caller, whose connection it is.
    /// </summary>
    private static async Task ReleaseUserLocksAsync(MySqlConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            return;

        try
        {
            await using var command = new MySqlCommand("DO RELEASE_ALL_LOCKS();", connection);
            command.CommandTimeout = UserLockReleaseTimeoutInSeconds;
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // ignored: best effort, the caller sees the failure of its call
        }
    }

    /// <summary>
    /// Synchronous counterpart to <see cref="ReleaseUserLocksAsync"/>.
    /// </summary>
    private static void ReleaseUserLocks(MySqlConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            return;

        try
        {
            using var command = new MySqlCommand("DO RELEASE_ALL_LOCKS();", connection);
            command.CommandTimeout = UserLockReleaseTimeoutInSeconds;
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // ignored: best effort, the caller sees the failure of its call
        }
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
    /// Substitutes @paramName placeholders with literal values. The MySQL templates use session variables
    /// (@v_count, @dummy) next to command parameters (@ProductId, @Name), so the parameters cannot be bound
    /// through the provider; the substitution leaves every identifier that is not a parameter alone.
    /// See <see cref="DalParameterSubstitution"/> for the matching rules.
    /// </summary>
    internal static string SubstituteParameters(string sqlCode, DalParameterList dalParameterList)
    {
        return DalParameterSubstitution.Substitute(sqlCode, dalParameterList, FormatParameterValue);
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
