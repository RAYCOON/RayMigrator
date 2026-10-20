using System.Data;
using System.Data.Common;

namespace Raycoon.RayMigrator.Database.Common;

/// <summary>
/// The data access layer of one database type (a DAL plugin): executes SQL against one connection string and
/// classifies the provider's errors.
/// </summary>
/// <remarks>
/// Every <see cref="Task"/>-returning member takes a <see cref="CancellationToken"/>. A call that fails once the token
/// is cancelled throws an <see cref="OperationCanceledException"/> carrying that token, whatever the provider threw, and
/// starts no further retry; <see cref="DalBase"/> guarantees this for every DAL derived from it. How far a running
/// command can be stopped depends on the provider (SQLite finishes a batch that has started); a command that completes
/// despite the cancellation returns its result. The synchronous <see cref="ExecuteNonQuery"/> has no token: it serves
/// the background thread of the database log writer.
/// </remarks>
public interface IDal
{
    string DatabaseType { get; }
    DalSpecificProperties DalSpecificProperties { get; }

    /// <summary>
    /// Executes a non-query SQL command on a connection of its own, in a transaction when
    /// <see cref="IDalSettings.UseTransaction"/> is set, and retries transient errors as the settings allow.
    /// </summary>
    /// <param name="sqlCode">The SQL to execute.</param>
    /// <param name="dalSettings">Transaction, command timeout and retries.</param>
    /// <param name="dalParameterList">The parameters of the command, or null.</param>
    /// <param name="cancellationToken">Cancels opening, executing and waiting for a retry; a started commit completes.</param>
    Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The synchronous counterpart of <see cref="ExecuteNonQueryAsync(string, IDalSettings, DalParameterList, CancellationToken)"/>
    /// for the background thread of the database log writer, which writes outside any async flow.
    /// </summary>
    void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);

    /// <summary>
    /// Executes a SQL command on a connection of its own and returns the first column of the first row, in a
    /// transaction when <see cref="IDalSettings.UseTransaction"/> is set, and retries transient errors as the settings allow.
    /// </summary>
    /// <param name="sqlCode">The SQL to execute.</param>
    /// <param name="dalSettings">Transaction, command timeout and retries.</param>
    /// <param name="dalParameterList">The parameters of the command, or null.</param>
    /// <param name="cancellationToken">Cancels opening, executing and waiting for a retry; a started commit completes.</param>
    Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query on a connection of its own and returns its rows as column-name-to-value dictionaries, and
    /// retries transient errors as the settings allow.
    /// </summary>
    /// <param name="sqlCode">The SQL to execute.</param>
    /// <param name="dalSettings">Command timeout and retries.</param>
    /// <param name="dalParameterList">The parameters of the query, or null.</param>
    /// <param name="cancellationToken">Cancels opening, executing, reading and waiting for a retry.</param>
    Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a connection to <paramref name="connectionString"/> can be opened.
    /// </summary>
    /// <returns>True when the connection opened; false when it failed for any reason other than cancellation.</returns>
    Task<bool> IsConnectionValidAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken = default);

    void CheckConnectionStringOrValidateConnection(bool validateConnection);
    bool TryGetDbTypeForType(Type type, out DbType dbType);
    bool TryGetDbSpecificSqlParameter<T>(DalParameterList dalParameterList, out List<T>? sqlParameterList) where T : class, IDbDataParameter, new();

    /// <summary>
    /// Creates a new unopened database connection using the DAL's connection string.
    /// Used for shared-connection scenarios where the caller controls the connection lifecycle.
    /// </summary>
    DbConnection CreateConnection();

    /// <summary>
    /// Executes a non-query SQL command on a caller-provided connection and transaction.
    /// No connection creation, transaction management, or retry logic — the caller controls the lifecycle.
    /// </summary>
    /// <param name="cancellationToken">Cancels the execution; the caller decides about its transaction.</param>
    Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a scalar SQL command on a caller-provided connection and transaction.
    /// No connection creation, transaction management, or retry logic — the caller controls the lifecycle.
    /// </summary>
    /// <param name="cancellationToken">Cancels the execution; the caller decides about its transaction.</param>
    Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default);
}
