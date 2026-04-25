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

    // --- Repository_CheckCreate (-10 to -19) ---

    /// <summary>Repository incomplete: wrong table count (MigratorMeta table exists).</summary>
    public const int RepositoryIncomplete = -10;

    /// <summary>Repository incomplete: partial tables found but no MigratorMeta table.</summary>
    public const int RepositoryPartialWithoutVersionTable = -11;

    /// <summary>Multiple MigratorMeta entries found for same combination.</summary>
    public const int RepositoryMultipleVersionEntries = -12;

    // --- Repository_Product_CheckInsert (-20 to -29) ---

    /// <summary>Product name is NULL or empty.</summary>
    public const int ProductNameEmpty = -20;

    // --- Repository_MigrationRun (-30 to -39) ---

    /// <summary>MigrationRun with given Id does not exist.</summary>
    public const int MigrationRunNotFound = -30;

    /// <summary>MigrationRun not found or not in Running state (FixOrphaned).</summary>
    public const int MigrationRunNotInRunningState = -31;

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

    // ===== Known Code Registry =====

    private static readonly HashSet<int> KnownNegativeCodes =
    [
        GeneralError, MigrationAlreadyRunning,
        RepositoryIncomplete, RepositoryPartialWithoutVersionTable, RepositoryMultipleVersionEntries,
        ProductNameEmpty, MigrationRunNotFound, MigrationRunNotInRunningState, MigrationNotFound,
        EnvironmentNameEmpty
    ];

    /// <summary>
    /// Returns true if the negative ResultCode is part of the known catalog.
    /// Unknown negative codes (e.g. from user-customized templates) return false.
    /// </summary>
    public static bool IsKnown(int resultCode) => KnownNegativeCodes.Contains(resultCode);
}
