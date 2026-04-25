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
    ];

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
            async () => { await ExecuteNonQueryAsyncInternal(sqlCode, dalSettings, dalParameterList); }, dalSettings);
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
            () => { ExecuteNonQueryInternal(sqlCode, dalSettings, dalParameterList); }, dalSettings);
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
            async () => await ExecuteScalarAsyncInternal(sqlCode, dalSettings, dalParameterList), dalSettings);
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
            async () => await ExecuteReaderAsyncInternal(sqlCode, dalSettings, dalParameterList), dalSettings);
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