using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Database.SqlServer;

[DatabaseType("SqlServer")]
public class DalSqlServer : DalBase, IDal
{
    private readonly string _connectionString;
    public override string DatabaseType { get; }
    public override DalSpecificProperties DalSpecificProperties { get; }

    public DalSqlServer(string connectionString)
    {
        _connectionString = connectionString;
        DatabaseType = this.GetType().GetCustomAttribute<DatabaseTypeAttribute>()!.DatabaseType;
        DalSpecificProperties = new DalSpecificProperties
        {
            SqlBlockDelimiter = "GO",
            SqlMultiLineCommentStart = "/*",
            SqlMultiLineCommentEnd = "*/",
            SupportsSchema = true,
            SupportsTransactionalDdl = true,
            IdentifierQuoteStart = "[",
            IdentifierQuoteEnd = "]",
            DefaultSchema = "dbo",
        };
    }

    // Transient SQL Server error codes that trigger automatic retry.
    private static readonly string[] s_transientCodes =
    [
        "-2",    // Timeout expired (SQL Server specific timeout)
        "20",    // Instance connection error (broken TDS connection / encryption negotiation failure)
        "64",    // Connection established but lost (ERROR_NETNAME_DELETED)
        "233",   // Connection closed during initialization (connection pool exhaustion / server busy)
        "10053", // WSAECONNABORTED - Software caused connection abort
        "10054", // WSAECONNRESET - Connection forcibly closed by remote host
        "10060", // WSAETIMEDOUT - Connection attempt timed out
        "40197", // Azure SQL: Service error processing request
        "40501", // Azure SQL: Service is currently busy
        "40613", // Azure SQL: Database is not currently available (failover / scaling)
        "49918", // Azure SQL: Not enough resources to process request
        "49919", // Azure SQL: Too many create or update operations in progress
        "49920", // Azure SQL: Too many operations in progress
        "4021",  // Pooled session reset failed: the login's state (default language, default database, ...) changed
                 // since the session logged in. Raised only on reuse of a pooled connection; a fresh login either
                 // succeeds or fails with a different, non-transient code.
        "596",   // Session is in the kill state (follows 4021, or a KILL / failover mid-batch)
    ];

    // SQL Server kills a pooled session whose login state no longer matches the state at its initial login (4021,
    // followed by 596). SqlClient discards only that one physical connection, so every other pooled connection opened
    // before the change fails the same way, one per attempt. Clearing the pool lets the very next attempt log in afresh.
    private static readonly int[] s_poolInvalidatingCodes = [4021, 596];

    /// <summary>
    /// True when <paramref name="errorNumber"/> is one of the SQL Server error numbers this DAL retries.
    /// </summary>
    internal static bool IsTransientErrorNumber(int errorNumber) => s_transientCodes.Contains(errorNumber.ToString());

    /// <summary>
    /// True when one of the error numbers in an exception's error chain marks the pooled session as killed by the
    /// server (4021 login state changed, 596 session in kill state), so the connection pool must be cleared.
    /// </summary>
    internal static bool RequiresPoolClear(IEnumerable<int> errorNumbers) => errorNumbers.Any(s_poolInvalidatingCodes.Contains);

    private static bool RequiresPoolClear(SqlException ex) => RequiresPoolClear(ex.Errors.Cast<SqlError>().Select(e => e.Number));

    private void ClearPool()
    {
        // ClearPool resolves the pool by connection string; the SqlConnection does not need to be opened.
        SqlConnection.ClearPool(new SqlConnection(_connectionString));
    }

    private async Task<T> ClearPoolOnKilledSessionAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (SqlException ex) when (RequiresPoolClear(ex))
        {
            ClearPool();
            throw;
        }
    }

    private async Task ClearPoolOnKilledSessionAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (SqlException ex) when (RequiresPoolClear(ex))
        {
            ClearPool();
            throw;
        }
    }

    private void ClearPoolOnKilledSession(Action operation)
    {
        try
        {
            operation();
        }
        catch (SqlException ex) when (RequiresPoolClear(ex))
        {
            ClearPool();
            throw;
        }
    }

    public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
    {
        if (ex is SqlException sqlEx)
        {
            var code = sqlEx.Number.ToString();
            return (s_transientCodes.Contains(code), code);
        }
        return base.IsTransient(ex);
    }

    public override void CheckConnectionStringOrValidateConnection(bool validateConnection)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            if (validateConnection)
            {
                connection.Open();
                connection.Close();
            }
        }
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        await ExecuteWithRetryAsync(
            () => ClearPoolOnKilledSessionAsync(
                () => ExecuteNonQueryAsyncInternal(sqlCode, dalSettings, dalParameterList)), dalSettings);
    }

    private async Task ExecuteNonQueryAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        List<SqlParameter>? sqlParameterList = null;

        if (dalParameterList != null)
        {
            if (!TryGetDbSpecificSqlParameter(dalParameterList, out sqlParameterList))
            {
                var paramCount = dalParameterList.GetAllParameters().Count();
                throw new DatabaseParameterException(
                    $"Failed to convert {paramCount} parameter(s) to SQL Server-specific parameters.",
                    paramCount);
            }
        }

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            if (dalSettings.UseTransaction)
            {
                await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();
                try
                {
                    await using (var command = new SqlCommand(sqlCode, connection, transaction))
                    {
                        command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                        if (sqlParameterList != null)
                        {
                            command.Parameters.AddRange(sqlParameterList.ToArray());
                        }
                        await command.ExecuteNonQueryAsync();
                    }
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
                await using (var command = new SqlCommand(sqlCode, connection))
                {
                    command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                    if (sqlParameterList != null)
                    {
                        command.Parameters.AddRange(sqlParameterList.ToArray());
                    }
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }

    public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        ExecuteWithRetry(
            () => ClearPoolOnKilledSession(
                () => ExecuteNonQueryInternal(sqlCode, dalSettings, dalParameterList)), dalSettings);
    }

    private void ExecuteNonQueryInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        List<SqlParameter>? sqlParameterList = null;

        if (dalParameterList != null)
        {
            if (!TryGetDbSpecificSqlParameter(dalParameterList, out sqlParameterList))
            {
                var paramCount = dalParameterList.GetAllParameters().Count();
                throw new DatabaseParameterException(
                    $"Failed to convert {paramCount} parameter(s) to SQL Server-specific parameters.",
                    paramCount);
            }
        }

        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            if (dalSettings.UseTransaction)
            {
                using SqlTransaction transaction = connection.BeginTransaction();
                try
                {
                    using (var command = new SqlCommand(sqlCode, connection, transaction))
                    {
                        command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                        if (sqlParameterList != null)
                        {
                            command.Parameters.AddRange(sqlParameterList.ToArray());
                        }
                        command.ExecuteNonQuery();
                    }
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
                using (var command = new SqlCommand(sqlCode, connection))
                {
                    command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                    if (sqlParameterList != null)
                    {
                        command.Parameters.AddRange(sqlParameterList.ToArray());
                    }
                    command.ExecuteNonQuery();
                }
            }
        }
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        return await ExecuteWithRetryAsync(
            () => ClearPoolOnKilledSessionAsync(
                () => ExecuteScalarAsyncInternal(sqlCode, dalSettings, dalParameterList)), dalSettings);
    }

    private async Task<object?> ExecuteScalarAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        List<SqlParameter>? sqlParameterList = null;

        if (dalParameterList != null)
        {
            if (!TryGetDbSpecificSqlParameter(dalParameterList, out sqlParameterList))
            {
                var paramCount = dalParameterList.GetAllParameters().Count();
                throw new DatabaseParameterException(
                    $"Failed to convert {paramCount} parameter(s) to SQL Server-specific parameters.",
                    paramCount);
            }
        }

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            if (dalSettings.UseTransaction)
            {
                using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync();
                try
                {
                    await using (var command = new SqlCommand(sqlCode, connection, transaction))
                    {
                        command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                        if (sqlParameterList != null)
                        {
                            command.Parameters.AddRange(sqlParameterList.ToArray());
                        }
                        var result = await command.ExecuteScalarAsync();
                        await transaction.CommitAsync();
                        return result;
                    }
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            else
            {
                await using (var command = new SqlCommand(sqlCode, connection))
                {
                    command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                    if (sqlParameterList != null)
                    {
                        command.Parameters.AddRange(sqlParameterList.ToArray());
                    }
                    var result = await command.ExecuteScalarAsync();
                    return result;
                }
            }
        }
    }

    public override async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        return await ExecuteWithRetryAsync(
            () => ClearPoolOnKilledSessionAsync(
                () => ExecuteReaderAsyncInternal(sqlCode, dalSettings, dalParameterList)), dalSettings);
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteReaderAsyncInternal(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        List<SqlParameter>? sqlParameterList = null;

        if (dalParameterList != null)
        {
            if (!TryGetDbSpecificSqlParameter(dalParameterList, out sqlParameterList))
            {
                var paramCount = dalParameterList.GetAllParameters().Count();
                throw new DatabaseParameterException(
                    $"Failed to convert {paramCount} parameter(s) to SQL Server-specific parameters.",
                    paramCount);
            }
        }

        var results = new List<Dictionary<string, object?>>();

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            await using (var command = new SqlCommand(sqlCode, connection))
            {
                command.CommandTimeout = dalSettings.DbCommandTimeoutInSeconds;
                if (sqlParameterList != null)
                {
                    command.Parameters.AddRange(sqlParameterList.ToArray());
                }

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
            }
        }

        return results;
    }

    public override async Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings)
    {
        try
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    public override DbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        List<SqlParameter>? sqlParameterList = null;

        if (dalParameterList != null)
        {
            if (!TryGetDbSpecificSqlParameter(dalParameterList, out sqlParameterList))
            {
                var paramCount = dalParameterList.GetAllParameters().Count();
                throw new DatabaseParameterException(
                    $"Failed to convert {paramCount} parameter(s) to SQL Server-specific parameters.",
                    paramCount);
            }
        }

        await using var command = new SqlCommand(sqlCode, (SqlConnection)connection, (SqlTransaction)transaction);
        command.CommandTimeout = commandTimeoutInSeconds;
        if (sqlParameterList != null)
        {
            command.Parameters.AddRange(sqlParameterList.ToArray());
        }
        await command.ExecuteNonQueryAsync();
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        List<SqlParameter>? sqlParameterList = null;

        if (dalParameterList != null)
        {
            if (!TryGetDbSpecificSqlParameter(dalParameterList, out sqlParameterList))
            {
                var paramCount = dalParameterList.GetAllParameters().Count();
                throw new DatabaseParameterException(
                    $"Failed to convert {paramCount} parameter(s) to SQL Server-specific parameters.",
                    paramCount);
            }
        }

        await using var command = new SqlCommand(sqlCode, (SqlConnection)connection, (SqlTransaction)transaction);
        command.CommandTimeout = commandTimeoutInSeconds;
        if (sqlParameterList != null)
        {
            command.Parameters.AddRange(sqlParameterList.ToArray());
        }
        return await command.ExecuteScalarAsync();
    }

    // Override this method only if you need SQL Server-specific behavior
    protected override T CreateParameter<T>(DbType dbType, string parameterName, object? parameterValue)
    {
        var parameter = base.CreateParameter<T>(dbType, parameterName, parameterValue);

        // Apply SQL Server-specific parameter adjustments
        if (parameter is SqlParameter sqlParameter)
        {
            // SQL Server-specific settings
            if (dbType == DbType.String && parameterValue != null)
            {
                sqlParameter.Size = Math.Max(1, ((string)parameterValue).Length);
            }
        }

        return parameter;
    }

    // Override this method only if you need SQL Server-specific conversions
    protected override object ConvertToDbValue(object? value)
    {
        // SQL Server-specific value conversions
        if (value is DateTime dateTime)
        {
            // SQL Server does not support dates before 1753
            if (dateTime < new DateTime(1753, 1, 1))
            {
                return new DateTime(1753, 1, 1);
            }
        }

        return base.ConvertToDbValue(value);
    }
}