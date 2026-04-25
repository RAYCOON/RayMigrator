namespace Raycoon.RayMigrator.Shared.Exceptions;


#region Pre-Migration Exceptions

/// <summary>
/// Exception that is based on RayMigrator configuration issues.
/// </summary>
public class ApplicationStartupException : Exception
{
    public const string AbortMessage = "RayMigrator aborted due to startup-problems. ";
    public ApplicationStartupException(string message) : base(AbortMessage + message) { }
    public ApplicationStartupException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }
}

/// <summary>
/// Exception that is based on RayMigrator configuration issues.
/// </summary>
public class ConfigurationValidationException : Exception
{
    public const string AbortMessage = "RayMigrator aborted due to problems processing the configuration. ";
    public ConfigurationValidationException(string message) : base(AbortMessage + message) { }
    public ConfigurationValidationException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }
}

/* Disabled in favour to TemplateExecutionException
/// <summary>
/// Exception that is based on a malformed (SELECT-)result received from a template execution.
/// </summary>
public class TemplateResultFormatException : Exception
{
    public const string AbortMessage = "RayMigrator aborted due to a malformed (SELECT-)result received from a template execution. ";
    public TemplateResultFormatException(string message) : base(AbortMessage + message) { }
}

/// <summary>
/// Exception that is based on a negative ResultCode received from a template execution.
/// </summary>
public class TemplateResultException : Exception
{
    public const string AbortMessage = "RayMigrator aborted due to a negative ResultCode received from a template execution. ";
    public TemplateResultException(string message) : base(AbortMessage + message) { }
}
*/
#endregion



/// <summary>
/// Exception that occurs when executing a SQL-template.
/// </summary>
public class TemplateExecutionException : Exception
{
    private const string AbortMessage = "RayMigrator aborted due to an error at template execution. ";
    public TemplateExecutionException(string message) : base(AbortMessage + message) { }
    public TemplateExecutionException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }
}

/// <summary>
/// Exception that is based on a negative SELECT-result of a database-specific SQL-template execution.
/// </summary>
public class TemplateResultException : Exception
{
    private const string AbortMessage = "RayMigrator aborted due to a negative result at a template execution. ";

    /// <summary>
    /// The negative ResultCode returned by the SQL template.
    /// See <see cref="Constants.TemplateResultCode"/> for the full catalog.
    /// </summary>
    public int ResultCode { get; }

    public TemplateResultException(string message, int resultCode = -1) : base(AbortMessage + message)
    {
        ResultCode = resultCode;
    }
    public TemplateResultException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }
}

/// <summary>
/// Exception thrown when a SQL template returns a negative ResultCode that is NOT in the known catalog.
/// This can happen when users customize templates and add their own error codes.
/// Always causes migration abort.
/// </summary>
public class UndefinedTemplateResultException : TemplateResultException
{
    public UndefinedTemplateResultException(string message, int resultCode)
        : base(message, resultCode) { }
}


/// <summary>
/// Exceptions that occur during migration execution.
/// </summary>
#region Migration-Run Exceptions

public class MigrationHashValidationException : Exception
{
    public MigrationHashValidationException(string message) : base(message) { }
    public MigrationHashValidationException(string message, Exception innerException) : base(message, innerException) { }
}

public class MigrationFileParsingException : Exception
{
    public const string AbortMessage = "RayMigrator aborted due to problems parsing the migration script. ";

    /// <summary>
    /// Optional error code for classification. See <see cref="Constants.TemplateResultCode"/> for values.
    /// </summary>
    public int? ErrorCode { get; }

    public MigrationFileParsingException(string message) : base(AbortMessage + message) { }

    public MigrationFileParsingException(string message, int errorCode) : base(AbortMessage + message)
    {
        ErrorCode = errorCode;
    }

    public MigrationFileParsingException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }
}

public class MigrationExecutionException : Exception
{
    public MigrationExecutionException(string message) : base(message) { }
    public MigrationExecutionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when an external CLI tool execution fails (process start failure, unexpected exit code).
/// </summary>
public class CliToolExecutionException : MigrationExecutionException
{
    public const string AbortMessage = "RayMigrator aborted due to a CLI tool execution error. ";

    /// <summary>
    /// The path of the CLI tool executable that was invoked.
    /// </summary>
    public string ExecutablePath { get; }

    /// <summary>
    /// The exit code returned by the CLI tool, if the process started successfully.
    /// </summary>
    public int? ExitCode { get; }

    public CliToolExecutionException(string message, string executablePath)
        : base(AbortMessage + message)
    {
        ExecutablePath = executablePath;
    }

    public CliToolExecutionException(string message, string executablePath, int exitCode)
        : base(AbortMessage + message)
    {
        ExecutablePath = executablePath;
        ExitCode = exitCode;
    }

    public CliToolExecutionException(string message, string executablePath, Exception innerException)
        : base(AbortMessage + message, innerException)
    {
        ExecutablePath = executablePath;
    }
}

/// <summary>
/// Exception thrown when a CLI tool execution exceeds the configured timeout.
/// </summary>
public class CliToolTimeoutException : CliToolExecutionException
{
    /// <summary>
    /// The configured timeout in seconds that was exceeded.
    /// </summary>
    public int TimeoutSeconds { get; }

    public CliToolTimeoutException(string message, string executablePath, int timeoutSeconds)
        : base(message, executablePath)
    {
        TimeoutSeconds = timeoutSeconds;
    }
}

public class RayMigratorInternalException : Exception
{
    public const string AbortMessage = "RayMigrator aborted due to an internal error. ";
    public RayMigratorInternalException(string message) : base(AbortMessage + message) { }
    public RayMigratorInternalException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }
}

#endregion


#region Not Yet Implemented

/// <summary>
/// Exception thrown when a feature is planned but not yet implemented.
/// </summary>
public class NotYetImplementedException : Exception
{
    public const string AbortMessage = "This feature is not yet implemented. ";
    public NotYetImplementedException(string feature) : base(AbortMessage + $"'{feature}' is planned for a future release.") { }
}

#endregion


#region Database and Recovery Exceptions

/// <summary>
/// Exception thrown when database parameter conversion fails.
/// </summary>
public class DatabaseParameterException : Exception
{
    public const string AbortMessage = "RayMigrator aborted due to a database parameter conversion error. ";

    /// <summary>
    /// The number of parameters that were being converted when the error occurred.
    /// </summary>
    public int ParameterCount { get; }

    public DatabaseParameterException(string message) : base(AbortMessage + message) { }

    public DatabaseParameterException(string message, int parameterCount) : base(AbortMessage + message)
    {
        ParameterCount = parameterCount;
    }

    public DatabaseParameterException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }

    public DatabaseParameterException(string message, int parameterCount, Exception innerException) : base(AbortMessage + message, innerException)
    {
        ParameterCount = parameterCount;
    }
}

/// <summary>
/// Exception thrown when attempting to start a migration while another migration is already running for the same product.
/// </summary>
public class MigrationAlreadyRunningException : Exception
{
    public const string AbortMessage = "RayMigrator aborted because another migration is already running. ";

    /// <summary>
    /// The ID of the product for which a migration is already running.
    /// </summary>
    public int ProductId { get; }

    /// <summary>
    /// The ID of the existing running migration run.
    /// </summary>
    public int? ExistingMigrationRunId { get; }

    public MigrationAlreadyRunningException(string message) : base(AbortMessage + message) { }

    public MigrationAlreadyRunningException(string message, int productId) : base(AbortMessage + message)
    {
        ProductId = productId;
    }

    public MigrationAlreadyRunningException(string message, int productId, int existingMigrationRunId) : base(AbortMessage + message)
    {
        ProductId = productId;
        ExistingMigrationRunId = existingMigrationRunId;
    }

    public MigrationAlreadyRunningException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }
}

/// <summary>
/// Exception thrown during migration recovery operations.
/// </summary>
public class MigrationRecoveryException : Exception
{
    public const string AbortMessage = "RayMigrator encountered an error during migration recovery. ";

    /// <summary>
    /// The ID of the migration run being recovered.
    /// </summary>
    public int? MigrationRunId { get; }

    /// <summary>
    /// The ID of the specific migration record being recovered.
    /// </summary>
    public int? MigrationRecordId { get; }

    public MigrationRecoveryException(string message) : base(AbortMessage + message) { }

    public MigrationRecoveryException(string message, int? migrationRunId) : base(AbortMessage + message)
    {
        MigrationRunId = migrationRunId;
    }

    public MigrationRecoveryException(string message, int? migrationRunId, int? migrationRecordId) : base(AbortMessage + message)
    {
        MigrationRunId = migrationRunId;
        MigrationRecordId = migrationRecordId;
    }

    public MigrationRecoveryException(string message, Exception innerException) : base(AbortMessage + message, innerException) { }

    public MigrationRecoveryException(string message, int? migrationRunId, Exception innerException) : base(AbortMessage + message, innerException)
    {
        MigrationRunId = migrationRunId;
    }
}

/// <summary>
/// Exception thrown when a transient database error occurs and all retry attempts have been exhausted.
/// </summary>
public class DatabaseTransientException : Exception
{
    public const string AbortMessage = "RayMigrator aborted after exhausting retry attempts for transient database error. ";

    /// <summary>
    /// The number of retry attempts made before giving up.
    /// </summary>
    public int AttemptsMade { get; }

    /// <summary>
    /// The database-specific error code of the last failure.
    /// </summary>
    public string? LastErrorCode { get; }

    public DatabaseTransientException(string message) : base(AbortMessage + message) { }

    public DatabaseTransientException(string message, int attemptsMade) : base(AbortMessage + message)
    {
        AttemptsMade = attemptsMade;
    }

    public DatabaseTransientException(string message, int attemptsMade, string? lastErrorCode) : base(AbortMessage + message)
    {
        AttemptsMade = attemptsMade;
        LastErrorCode = lastErrorCode;
    }

    public DatabaseTransientException(string message, int attemptsMade, Exception innerException) : base(AbortMessage + message, innerException)
    {
        AttemptsMade = attemptsMade;
    }

    public DatabaseTransientException(string message, int attemptsMade, string? lastErrorCode, Exception innerException) : base(AbortMessage + message, innerException)
    {
        AttemptsMade = attemptsMade;
        LastErrorCode = lastErrorCode;
    }
}

#endregion
