
using System.Data;
using System.Data.Common;
using System.Reflection;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Database.Example;

/// <summary>
/// Example/template DAL implementation for a custom database provider.
/// Fork this project and replace "Example" with your database type name throughout.
///
/// Steps to create your own DAL:
/// 1. Copy this project and rename it (e.g., Raycoon.RayMigrator.Database.Oracle)
/// 2. Update the [DatabaseType] attribute value
/// 3. Add your ADO.NET driver NuGet package
/// 4. Override IsTransient() with your database's transient error codes
/// 5. Implement all methods using your database's connection/command classes
/// 6. Implement all 19 SQL templates in the Templates/ directory
/// 7. Build and copy the output to DataAccessLayers/{YourDatabaseType}/
/// </summary>
[DatabaseType("Example")]
public class DalExample : DalBase, IDal
{
    private readonly string _connectionString;
    public override string DatabaseType { get; }
    public override DalSpecificProperties DalSpecificProperties { get; }

    public DalExample(string connectionString)
    {
        _connectionString = connectionString;
        DatabaseType = this.GetType().GetCustomAttribute<DatabaseTypeAttribute>()!.DatabaseType;
        DalSpecificProperties = new DalSpecificProperties
        {
            // TODO: Set the SQL block delimiter for your database
            // "GO" for SQL Server, ";" for PostgreSQL/MariaDB/MySQL
            SqlBlockDelimiter = ";",
            SqlMultiLineCommentStart = "/*",
            SqlMultiLineCommentEnd = "*/",
            // TODO: Set to true if your database supports schemas (e.g., SQL Server, PostgreSQL)
            SupportsSchema = false,
            // TODO: Set to false if your database does not support transactional DDL
            // (e.g., MariaDB/MySQL where DDL causes implicit COMMIT)
            SupportsTransactionalDdl = true,
            // TODO: Set identifier quoting characters for your database
            // SQL Server: "[" / "]", PostgreSQL/SQLite: "\"" / "\"", MariaDB/MySQL: "`" / "`"
            IdentifierQuoteStart = "\"",
            IdentifierQuoteEnd = "\"",
            // TODO: Set the default schema name (e.g., "dbo" for SQL Server, "public" for PostgreSQL)
            DefaultSchema = "",
            // TODO: Set to true if your database folds unquoted identifiers to lowercase (PostgreSQL)
            FoldsUnquotedIdentifiersToLower = false,
        };
    }

    // TODO: Define transient error codes for your database engine.
    // These are error codes that indicate temporary conditions safe to retry.
    // Example: timeouts, deadlocks, connection drops, too many connections.
    // private static readonly string[] s_transientCodes = ["1205", "1213", "2006"];

    // TODO: Override IsTransient to detect transient errors from your database driver.
    // Check your ADO.NET driver's exception type and compare the error code
    // against known transient error codes for your database engine.
    // The base implementation already handles TimeoutException
    // and recursively checks InnerException.
    //
    // public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
    // {
    //     if (ex is YourDbException dbEx)
    //     {
    //         var code = dbEx.ErrorNumber.ToString();
    //         return (s_transientCodes.Contains(code), code);
    //     }
    //     return base.IsTransient(ex);
    // }

    public override void CheckConnectionStringOrValidateConnection(bool validateConnection)
    {
        // TODO: Create a connection using your ADO.NET driver and optionally validate it
        // Example:
        // using var connection = new YourDbConnection(_connectionString);
        // if (validateConnection)
        // {
        //     connection.Open();
        //     connection.Close();
        // }
        throw new NotImplementedException("Implement connection validation for your database.");
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        // TODO: Implement async non-query execution
        // Use the DalBase retry helpers which automatically route through your IsTransient override:
        // await ExecuteWithRetryAsync(async () => { await YourInternalMethod(...); }, dalSettings);
        throw new NotImplementedException("Implement async non-query execution for your database.");
    }

    public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        // TODO: Implement synchronous non-query execution
        throw new NotImplementedException("Implement sync non-query execution for your database.");
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        // TODO: Implement async scalar execution
        throw new NotImplementedException("Implement async scalar execution for your database.");
    }

    public override async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
    {
        // TODO: Implement async reader execution
        throw new NotImplementedException("Implement async reader execution for your database.");
    }

    public override async Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings)
    {
        // TODO: Implement connection validation
        // Return true if the connection can be established, false otherwise
        throw new NotImplementedException("Implement connection validation for your database.");
    }

    public override DbConnection CreateConnection()
    {
        // TODO: Return a new unopened connection using your ADO.NET driver
        // Example: return new YourDbConnection(_connectionString);
        throw new NotImplementedException("Implement CreateConnection for your database.");
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        // TODO: Execute a non-query command on the caller-provided connection and transaction
        // Do NOT create connections, manage transactions, or add retry logic here — the caller controls the lifecycle.
        throw new NotImplementedException("Implement shared-connection ExecuteNonQueryAsync for your database.");
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        // TODO: Execute a scalar command on the caller-provided connection and transaction
        // Do NOT create connections, manage transactions, or add retry logic here — the caller controls the lifecycle.
        throw new NotImplementedException("Implement shared-connection ExecuteScalarAsync for your database.");
    }
}
