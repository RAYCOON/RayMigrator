using System.Globalization;

namespace Raycoon.RayMigrator.Validation.Messages;

/// <summary>
/// Centralised English-only format strings for validation issues.
/// Each constant is a <see cref="string.Format(string, object[])"/> template — see usage in rule classes.
/// </summary>
internal static class ValidationMessages
{
    private static readonly IFormatProvider Culture = CultureInfo.InvariantCulture;

    public static string Format(string template, params object?[] args)
        => string.Format(Culture, template, args);

    // -- Alias uniqueness --------------------------------------------------
    public const string DuplicateTargetGroupAlias =
        "Product '{0}': Duplicate TargetGroup alias '{1}'. Each TargetGroup within a Product must have a unique Alias.";
    public const string DuplicateTargetAlias =
        "Product '{0}', TargetGroup '{1}': Duplicate Target alias '{2}'. Each Target within a TargetGroup must have a unique Alias.";
    public const string DuplicateProductAlias =
        "Duplicate Product alias '{0}'. Each Product must have a unique Alias.";
    public const string DuplicateCliToolAlias =
        "Duplicate CLI tool alias '{0}'. Each CliTools entry must have a unique Alias.";

    // -- TargetGroupMigrationOrder ----------------------------------------
    public const string TgOrderInvalidAlias =
        "TargetGroupMigrationOrder for Product '{0}' references alias '{1}' that does not match any TargetGroup in this product.";
    public const string TgOrderMissingAlias =
        "TargetGroupMigrationOrder for Product '{0}' is missing TargetGroup '{1}' from the execution order.";
    public const string TgOrderDuplicateAlias =
        "TargetGroupMigrationOrder for Product '{0}': TargetGroup '{1}' appears more than once.";
    public const string TgOrderIrrelevantForSingleTg =
        "TargetGroupMigrationOrder is irrelevant when the product has only one TargetGroup.";

    // -- Semantic contradictions ------------------------------------------
    public const string RollbackWithoutRollbackErrorAction =
        "MigrationErrorAction is '{0}' but no RollbackErrorAction is defined at product or defaults level.";
    public const string ExtensionEqualsPreExtension =
        "MigrationFilesExtension and MigrationRollbackFilesPreExtension must differ, but both resolve to '{0}'.";

    // -- CLI tool definitions ---------------------------------------------
    public const string FileModeMissingFilePath =
        "CLI tool '{0}': InputMode is 'File' but ArgumentTemplate does not contain '{{FilePath}}'.";
    public const string StdinModeWithFilePath =
        "CLI tool '{0}': InputMode is 'Stdin' but ArgumentTemplate contains '{{FilePath}}'. The placeholder will not be resolved.";
    public const string ExitCodeExpressionInvalid =
        "CLI tool '{0}': Invalid SuccessExitCodes expression '{1}': {2}";

    // -- CLI tool references -----------------------------------------------
    public const string UseCliToolAliasInvalid =
        "{0}: UseCliToolAlias '{1}' does not match any defined CliTools alias. Available aliases: [{2}].";

    // -- CLI tool parameters -----------------------------------------------
    public const string CliParamsWithoutAlias =
        "CliToolParameters are defined but no UseCliToolAlias is configured at any level.";
    public const string CliParamsMissingRequiredKeys =
        "CLI tool '{0}' expects parameters [{1}] but they are missing or empty: [{2}].";
    public const string CliParamsReservedKeyCollision =
        "CliToolParameters contains reserved key '{0}'. Reserved keys are substituted internally and must not be set by the user.";
    public const string CliParamsUnusedKeys =
        "CliToolParameters contains key(s) [{0}] that are not used by CLI tool '{1}' ArgumentTemplate.";

    // -- Schema rules ------------------------------------------------------
    public const string SchemaOnSchemalessDb =
        "{0} does not support schemas. SchemaName will be ignored.";
    public const string SchemaMissingForSchemaDb =
        "SchemaName is required for database type '{0}'.";
    public const string LowercaseTableBaseNameRequired =
        "TableBaseName '{0}' must be lowercase for database type '{1}'. Unquoted PostgreSQL identifiers fold to lowercase, and MariaDB/MySQL repository identifiers are stored as lowercase.";

    // -- Connection string rules ------------------------------------------
    public const string RepoAndTargetSameDb =
        "Repository ConnectionString is identical to Target '{0}' in Product '{1}', TargetGroup '{2}'. Repository and migration target share the same database (Single Point of Failure).";
    public const string DuplicateTargetConnection =
        "Product '{0}', TargetGroup '{1}': Targets '{2}' and '{3}' have identical ConnectionStrings. Migrations would execute twice on the same database.";
    public const string HardcodedCredentials =
        "Connection string appears to contain a hardcoded credential. Consider using {{ENV:VARIABLE}} placeholders.";

    // -- Default cascade rules --------------------------------------------
    public const string MissingEffectiveMigrationErrorAction =
        "No effective MigrationErrorAction. Set it on the product or in ProductDefaults.";
    public const string MissingEffectiveMigrationOrder =
        "No effective TargetMigrationOrder. Set it on the TargetGroup or in ProductDefaults.TargetGroupDefaults.";
    public const string MissingEffectiveHashValidationScope =
        "No effective HashValidationScope. Set it on the TargetGroup or in ProductDefaults.TargetGroupDefaults.";
}
