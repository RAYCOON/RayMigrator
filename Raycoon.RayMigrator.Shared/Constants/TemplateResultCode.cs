namespace Raycoon.RayMigrator.Shared.Constants;

/// <summary>
/// Defines all negative ResultCodes returned by SQL templates and positive ErrorCodes for C# errors.
/// Convention: negative = SQL template error, positive = C# backend error, 0 = no error.
/// </summary>
public static class TemplateResultCode
{
    // ===== SQL Template ResultCodes (negative, returned by SQL templates) =====

    /// <summary>General/unclassified template error (legacy fallback).</summary>
    public const int GeneralError = -1;

    /// <summary>Migration already running — parallel run prevention (MigrationRun_Insert).</summary>
    public const int MigrationAlreadyRunning = -2;

    // --- Repository_CheckCreate, Repository_Upgrade_{n}, Repository_MigratorMeta_CheckInsert (-10 to -19) ---
    // -10 and -11 (table count checks of the former Repository_CheckCreate) are gone since the schema is versioned
    // (ADR-024): a repository without a schema version or with a newer one is refused by TemplateExecutor in C#.

    /// <summary>Multiple MigratorMeta entries found for the same RayMigratorVersion and RepositoryDatabaseType.</summary>
    public const int RepositoryMultipleVersionEntries = -12;

    // --- Repository_Product_CheckInsert (-20 to -29) ---

    /// <summary>Product name is NULL or empty.</summary>
    public const int ProductNameEmpty = -20;

    // --- Repository_MigrationRun (-30 to -39) ---

    /// <summary>MigrationRun with given Id does not exist.</summary>
    public const int MigrationRunNotFound = -30;

    /// <summary>MigrationRun not found or not in Running state (FixOrphaned).</summary>
    public const int MigrationRunNotInRunningState = -31;

    /// <summary>
    /// MariaDB and MySQL schema templates: the repository schema lock (<c>GET_LOCK()</c>) was not acquired within
    /// 60 seconds; nothing was written (Repository_CheckCreate, Repository_Upgrade_{n}, Repository_MigratorMeta_CheckInsert).
    /// </summary>
    public const int RepositorySchemaLockNotAcquired = -32;

    // --- Repository_Migration (-40 to -49) ---

    /// <summary>Migration with given Id does not exist.</summary>
    public const int MigrationNotFound = -40;

    // --- Repository_Environment_CheckInsert (-50 to -59) ---

    /// <summary>Environment name is NULL or empty.</summary>
    public const int EnvironmentNameEmpty = -50;

    // ===== C# Backend ErrorCodes (positive, assigned by backend logic) =====

    /// <summary>RequireRollbackFile validation failed — missing rollback files.</summary>
    public const int RequireRollbackFileValidationFailed = 1001;

    /// <summary>Migration file parsing error.</summary>
    public const int MigrationFileParsingFailed = 1002;

    /// <summary>Configuration validation error.</summary>
    public const int ConfigurationValidationFailed = 1003;

    /// <summary>
    /// The host cancelled the run and the engine stopped it: the run of a migrate-up, migrate-down or baseline is
    /// <c>MigrationRunResult.Cancelled</c>, its <c>MigrationCancelledAction</c> completed. Also the <c>ErrorCode</c> of an
    /// update-hash or fix that stopped early. The read commands (validate-hash, status, history) throw an
    /// <c>OperationCanceledException</c> instead.
    /// </summary>
    public const int Cancelled = 1004;

    /// <summary>
    /// The host cancelled a migrate-up and the rollback its <c>MigrationCancelledAction</c> started did not complete
    /// cleanly: it reported a failure or a warning (a failed rollback block, a missing rollback file, rollback errors that
    /// RollbackErrorAction=Ignore skipped). The run is <c>MigrationRunResult.Error</c>.
    /// </summary>
    public const int CancelledRollbackIncomplete = 1005;

    // ===== Known Code Registry =====

    private static readonly HashSet<int> KnownNegativeCodes =
    [
        GeneralError, MigrationAlreadyRunning,
        RepositoryMultipleVersionEntries,
        ProductNameEmpty, MigrationRunNotFound, MigrationRunNotInRunningState, RepositorySchemaLockNotAcquired, MigrationNotFound,
        EnvironmentNameEmpty
    ];

    /// <summary>
    /// Returns true if the negative ResultCode is part of the known catalog.
    /// Unknown negative codes (e.g. from user-customized templates) return false.
    /// </summary>
    public static bool IsKnown(int resultCode) => KnownNegativeCodes.Contains(resultCode);
}
