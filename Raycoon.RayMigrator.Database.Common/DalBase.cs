using System.Data;
using System.Data.Common;

namespace Raycoon.RayMigrator.Database.Common;

/// <summary>
/// Base class of the DAL plugins. It implements the asynchronous <see cref="IDal"/> members on top of the
/// <c>…CoreAsync</c> members a DAL provides, so that a cancelled call ends the same way in every DAL (see
/// <see cref="IDal"/>), and holds the transient error classification and the retry helpers.
/// </summary>
public abstract class DalBase : IDal
{
    public abstract string DatabaseType { get; }
    public abstract DalSpecificProperties DalSpecificProperties { get; }

    /// <inheritdoc />
    public Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default)
        => ObserveCancellationAsync(() => ExecuteNonQueryCoreAsync(sqlCode, dalSettings, dalParameterList, cancellationToken), cancellationToken);

    /// <summary>
    /// The work of <see cref="ExecuteNonQueryAsync(string, IDalSettings, DalParameterList, CancellationToken)"/>: passes
    /// <paramref name="cancellationToken"/> to opening the connection, to the command and to
    /// <see cref="ExecuteWithRetryAsync(Func{Task}, IDalSettings, string, CancellationToken)"/>, not to commit and rollback.
    /// </summary>
    protected abstract Task ExecuteNonQueryCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken);

    public abstract void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);

    /// <inheritdoc />
    public Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default)
        => ObserveCancellationAsync(() => ExecuteScalarCoreAsync(sqlCode, dalSettings, dalParameterList, cancellationToken), cancellationToken);

    /// <summary>
    /// The work of <see cref="ExecuteScalarAsync(string, IDalSettings, DalParameterList, CancellationToken)"/>: passes
    /// <paramref name="cancellationToken"/> to opening the connection, to the command and to
    /// <see cref="ExecuteWithRetryAsync{T}(Func{Task{T}}, IDalSettings, string, CancellationToken)"/>, not to commit and rollback.
    /// </summary>
    protected abstract Task<object?> ExecuteScalarCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default)
        => ObserveCancellationAsync(() => ExecuteReaderCoreAsync(sqlCode, dalSettings, dalParameterList, cancellationToken), cancellationToken);

    /// <summary>
    /// The work of <see cref="ExecuteReaderAsync(string, IDalSettings, DalParameterList, CancellationToken)"/>: passes
    /// <paramref name="cancellationToken"/> to opening the connection, to the query, to every read and to
    /// <see cref="ExecuteWithRetryAsync{T}(Func{Task{T}}, IDalSettings, string, CancellationToken)"/>.
    /// </summary>
    protected abstract Task<List<Dictionary<string, object?>>> ExecuteReaderCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsConnectionValidAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken = default)
        => ObserveCancellationAsync(() => IsConnectionValidCoreAsync(connectionString, dalSettings, cancellationToken), cancellationToken);

    /// <summary>
    /// The work of <see cref="IsConnectionValidAsync"/>: opens a connection with <paramref name="cancellationToken"/> and
    /// answers false for every failure while the token is not cancelled.
    /// </summary>
    protected abstract Task<bool> IsConnectionValidCoreAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken);

    public abstract void CheckConnectionStringOrValidateConnection(bool validateConnection);

    /// <inheritdoc />
    public abstract DbConnection CreateConnection();

    /// <inheritdoc />
    public Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default)
        => ObserveCancellationAsync(() => ExecuteNonQueryCoreAsync(sqlCode, connection, transaction, commandTimeoutInSeconds, dalParameterList, cancellationToken), cancellationToken);

    /// <summary>
    /// The work of the shared-connection <c>ExecuteNonQueryAsync</c>: passes <paramref name="cancellationToken"/> to the
    /// command on the caller's connection and transaction.
    /// </summary>
    protected abstract Task ExecuteNonQueryCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null, CancellationToken cancellationToken = default)
        => ObserveCancellationAsync(() => ExecuteScalarCoreAsync(sqlCode, connection, transaction, commandTimeoutInSeconds, dalParameterList, cancellationToken), cancellationToken);

    /// <summary>
    /// The work of the shared-connection <c>ExecuteScalarAsync</c>: passes <paramref name="cancellationToken"/> to the
    /// command on the caller's connection and transaction.
    /// </summary>
    protected abstract Task<object?> ExecuteScalarCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken);

    /// <summary>
    /// Runs one asynchronous <see cref="IDal"/> call. Once <paramref name="cancellationToken"/> is cancelled, whatever the
    /// provider throws becomes an <see cref="OperationCanceledException"/> carrying that token, with the provider's
    /// exception as inner exception: providers report a cancelled command in their own way (an exception of their own,
    /// or an <see cref="OperationCanceledException"/> with a token of their own), and a failure that coincides with the
    /// cancellation is reported as the cancellation. The message names the provider's message unless the provider
    /// reported a cancellation itself, so that a database error that met the cancel stays readable where the cancel is
    /// reported.
    /// </summary>
    private async Task<T> ObserveCancellationAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && !IsCancellationOf(ex, cancellationToken))
        {
            throw CancelledBy(ex, cancellationToken);
        }
    }

    /// <summary>The counterpart of <see cref="ObserveCancellationAsync{T}"/> for calls without a result.</summary>
    private async Task ObserveCancellationAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        try
        {
            await operation();
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && !IsCancellationOf(ex, cancellationToken))
        {
            throw CancelledBy(ex, cancellationToken);
        }
    }

    private static bool IsCancellationOf(Exception ex, CancellationToken cancellationToken) =>
        ex is OperationCanceledException canceled && canceled.CancellationToken == cancellationToken;

    private OperationCanceledException CancelledBy(Exception ex, CancellationToken cancellationToken)
    {
        string message = ex is OperationCanceledException
            ? $"The operation on the [{DatabaseType}] database was cancelled."
            : $"The operation on the [{DatabaseType}] database was cancelled: {ex.Message}";
        return new OperationCanceledException(message, ex, cancellationToken);
    }

    /// <summary>
    /// Determines if an exception represents a transient database error that can be retried.
    /// Override in each DAL to check database-specific exception types and error codes.
    /// The base implementation handles TimeoutException as a common transient error
    /// and recursively checks InnerException via virtual dispatch. A cancellation (an
    /// <see cref="OperationCanceledException"/> whose token is cancelled) is never transient, whatever its inner
    /// exception reports: a cancelled call must not be retried.
    /// </summary>
    public virtual (bool isTransient, string? errorCode) IsTransient(Exception ex)
    {
        if (ex is OperationCanceledException { CancellationToken.IsCancellationRequested: true })
            return (false, null);

        if (ex is TimeoutException)
            return (true, null);

        return ex.InnerException != null ? IsTransient(ex.InnerException) : (false, null);
    }

    private IReadOnlySet<string>? _transientErrorCodes;

    /// <summary>
    /// The built-in transient error codes of this DAL, effective until a <c>TransientErrorCodes.txt</c> replaces them
    /// through <see cref="SetTransientErrorCodes"/>. Override in each DAL; the base has none. Compared case-insensitively.
    /// </summary>
    protected virtual IReadOnlyCollection<string> DefaultTransientErrorCodes => [];

    /// <summary>
    /// The effective transient error codes: the list set through <see cref="SetTransientErrorCodes"/>, otherwise
    /// <see cref="DefaultTransientErrorCodes"/>. A code not contained is not retried. Case-insensitive.
    /// </summary>
    public IReadOnlySet<string> TransientErrorCodes =>
        _transientErrorCodes ??= new HashSet<string>(DefaultTransientErrorCodes, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Where the effective codes come from: the path of the loaded <c>TransientErrorCodes.txt</c>, or null for the
    /// built-in list.
    /// </summary>
    public string? TransientErrorCodesSource { get; private set; }

    /// <summary>
    /// Replaces the effective transient error codes, for example with the content of a shipped
    /// <c>TransientErrorCodes.txt</c> (ADR-022). The list is complete: it does not merge with the built-in codes, and an
    /// empty list means that no database error is retried.
    /// </summary>
    /// <param name="codes">The complete list of codes to retry.</param>
    /// <param name="source">Where the codes come from, typically the file path; null when unknown.</param>
    public void SetTransientErrorCodes(IEnumerable<string> codes, string? source)
    {
        ArgumentNullException.ThrowIfNull(codes);
        _transientErrorCodes = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        TransientErrorCodesSource = source;
    }

    /// <summary>
    /// Classifies a provider error code against <see cref="TransientErrorCodes"/>: the tuple an
    /// <see cref="IsTransient"/> override returns for a provider exception that carries <paramref name="code"/>.
    /// </summary>
    protected (bool isTransient, string? errorCode) IsTransientCode(string? code)
    {
        return (code != null && TransientErrorCodes.Contains(code), code);
    }

    /// <summary>
    /// Executes an async operation with retry logic for transient errors.
    /// Uses this DAL's IsTransient method for transient error detection. Once <paramref name="cancellationToken"/> is
    /// cancelled, no further attempt starts and the wait before a retry ends.
    /// </summary>
    protected async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation, IDalSettings dalSettings, string? operationDescription = null, CancellationToken cancellationToken = default)
    {
        if (dalSettings.MaxRetries > 0)
            return await RetryHelper.ExecuteWithRetryAsync(
                operation, dalSettings.MaxRetries, dalSettings.RetryDelayMs,
                IsTransient, operationDescription: operationDescription, cancellationToken: cancellationToken);
        return await operation();
    }

    /// <summary>
    /// Executes an async void operation with retry logic for transient errors. Once <paramref name="cancellationToken"/>
    /// is cancelled, no further attempt starts and the wait before a retry ends.
    /// </summary>
    protected async Task ExecuteWithRetryAsync(
        Func<Task> operation, IDalSettings dalSettings, string? operationDescription = null, CancellationToken cancellationToken = default)
    {
        if (dalSettings.MaxRetries > 0)
            await RetryHelper.ExecuteWithRetryAsync(
                operation, dalSettings.MaxRetries, dalSettings.RetryDelayMs,
                IsTransient, operationDescription: operationDescription, cancellationToken: cancellationToken);
        else
            await operation();
    }

    /// <summary>
    /// Executes a synchronous operation with retry logic for transient errors. Only for the synchronous
    /// <see cref="ExecuteNonQuery"/>, which the background thread of the database log writer calls.
    /// </summary>
    protected void ExecuteWithRetry(
        Action operation, IDalSettings dalSettings, string? operationDescription = null)
    {
        if (dalSettings.MaxRetries > 0)
            RetryHelper.ExecuteWithRetry(
                () => { operation(); return true; },
                dalSettings.MaxRetries, dalSettings.RetryDelayMs,
                IsTransient, operationDescription: operationDescription);
        else
            operation();
    }

    public virtual bool TryGetDbSpecificSqlParameter<T>(DalParameterList dalParameterList, out List<T>? sqlParameterList) where T : class, IDbDataParameter, new()
    {
        try
        {
            List<T> sqlParameters = new List<T>();

            foreach (var dalParameter in dalParameterList.GetAllParameters())
            {
                Type dalParameterType = dalParameter.Value.ParameterType;

                if (!TryGetDbTypeForType(dalParameterType, out DbType dbType))
                {
                    throw new ApplicationException($"Error converting application parameter of type [{dalParameter.Value.ParameterType}] into a DAL-specific parameter for Type [{typeof(T).Name}].");
                }

                T parameter = CreateParameter<T>(dbType, dalParameter.Value.ParameterName, dalParameter.Value.ParameterValue);
                sqlParameters.Add(parameter);
            }

            sqlParameterList = sqlParameters;
            return true;
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Error converting application ParameterList into a DAL-specific parameters for Type [{typeof(T).Name}].", ex);
        }
    }

    protected virtual T CreateParameter<T>(DbType dbType, string parameterName, object? parameterValue) where T : class, IDbDataParameter, new()
    {
        return new T
        {
            DbType = dbType,
            ParameterName = parameterName,
            Value = ConvertToDbValue(parameterValue)
        };
    }

    protected virtual object ConvertToDbValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    public bool TryGetDbTypeForType(Type type, out DbType dbType)
    {
        Type? underlyingType = Nullable.GetUnderlyingType(type);
        Type effectiveType = underlyingType ?? type;

        switch (effectiveType)
        {
            case Type t when t == typeof(byte) || t == typeof(byte?):
                dbType = DbType.Byte;
                return true;
            case Type t when t == typeof(sbyte) || t == typeof(sbyte?):
                dbType = DbType.SByte;
                return true;
            case Type t when t == typeof(short) || t == typeof(short?):
                dbType = DbType.Int16;
                return true;
            case Type t when t == typeof(ushort) || t == typeof(ushort?):
                dbType = DbType.UInt16;
                return true;
            case Type t when t == typeof(int) || t == typeof(int?):
                dbType = DbType.Int32;
                return true;
            case Type t when t == typeof(uint) || t == typeof(uint?):
                dbType = DbType.UInt32;
                return true;
            case Type t when t == typeof(long) || t == typeof(long?):
                dbType = DbType.Int64;
                return true;
            case Type t when t == typeof(ulong) || t == typeof(ulong?):
                dbType = DbType.UInt64;
                return true;
            case Type t when t == typeof(float) || t == typeof(float?):
                dbType = DbType.Single;
                return true;
            case Type t when t == typeof(double) || t == typeof(double?):
                dbType = DbType.Double;
                return true;
            case Type t when t == typeof(decimal) || t == typeof(decimal?):
                dbType = DbType.Decimal;
                return true;
            case Type t when t == typeof(bool) || t == typeof(bool?):
                dbType = DbType.Boolean;
                return true;
            case Type t when t == typeof(string):
                dbType = DbType.String;
                return true;
            case Type t when t == typeof(char) || t == typeof(char?):
                dbType = DbType.StringFixedLength;
                return true;
            case Type t when t == typeof(Guid) || t == typeof(Guid?):
                dbType = DbType.Guid;
                return true;
            case Type t when t == typeof(DateTime) || t == typeof(DateTime?):
                dbType = DbType.DateTime;
                return true;
            case Type t when t == typeof(DateTimeOffset) || t == typeof(DateTimeOffset?):
                dbType = DbType.DateTimeOffset;
                return true;
            case Type t when t == typeof(byte[]):
                dbType = DbType.Binary;
                return true;
            case Type t when t == typeof(System.Xml.Linq.XElement) || t == typeof(System.Xml.XmlDocument):
                dbType = DbType.Xml;
                return true;
            default:
                dbType = default;
                return false;
        }
    }
}