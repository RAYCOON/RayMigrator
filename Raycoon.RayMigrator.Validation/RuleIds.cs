
namespace Raycoon.RayMigrator.Validation;

/// <summary>
/// String constants for all rule identifiers. See <c>Docs/appendix/validation-rules.md</c>
/// for the authoritative per-rule description, severity, and example messages.
/// </summary>
public static class RuleIds
{
    public const string RULE_1_1 = "RULE_1_1";   // DUPLICATE_TARGETGROUP_ALIAS
    public const string RULE_1_2 = "RULE_1_2";   // DUPLICATE_TARGET_ALIAS
    public const string RULE_1_8 = "RULE_1_8";   // DUPLICATE_PRODUCT_ALIAS
    public const string RULE_1_9 = "RULE_1_9";   // DUPLICATE_CLITOOL_ALIAS
    public const string RULE_1_10 = "RULE_1_10"; // TG_MIGRATION_ORDER_INVALID_ALIAS
    public const string RULE_1_11 = "RULE_1_11"; // TG_MIGRATION_ORDER_MISSING_ALIAS
    public const string RULE_1_12 = "RULE_1_12"; // TG_MIGRATION_ORDER_DUPLICATE_ALIAS
    public const string RULE_1_13 = "RULE_1_13"; // TG_MIGRATION_ORDER_IRRELEVANT_FOR_SINGLE_TG
    public const string RULE_2_11 = "RULE_2_11"; // ROLLBACK_WITHOUT_ROLLBACK_ERROR_ACTION
    public const string RULE_2_13 = "RULE_2_13"; // EXTENSION_EQUALS_PRE_EXTENSION
    public const string RULE_3_1 = "RULE_3_1";   // FILE_MODE_MISSING_FILEPATH
    public const string RULE_3_2 = "RULE_3_2";   // STDIN_MODE_WITH_FILEPATH
    public const string RULE_3_3 = "RULE_3_3";   // USE_CLI_TOOL_ALIAS_INVALID
    public const string RULE_3_4 = "RULE_3_4";   // CLI_PARAMS_WITHOUT_CLI_ALIAS
    public const string RULE_3_7 = "RULE_3_7";   // EXIT_CODE_EXPRESSION_INVALID
    public const string RULE_3_8 = "RULE_3_8";   // CLI_PARAMS_MISSING_REQUIRED_KEYS (Error)
    public const string RULE_3_9 = "RULE_3_9";   // CLI_PARAMS_RESERVED_KEY_COLLISION
    public const string RULE_3_10 = "RULE_3_10"; // CLI_PARAMS_UNUSED_KEYS
    public const string RULE_4_1 = "RULE_4_1";   // SCHEMA_ON_SCHEMALESS_DB
    public const string RULE_4_2 = "RULE_4_2";   // SCHEMA_MISSING_FOR_SCHEMA_DB
    public const string RULE_4_3 = "RULE_4_3";   // LOWERCASE_TABLEBASENAME_REQUIRED
    public const string RULE_7_1 = "RULE_7_1";   // REPO_AND_TARGET_SAME_DB
    public const string RULE_7_2 = "RULE_7_2";   // DUPLICATE_TARGET_CONNECTION
    public const string RULE_7_3 = "RULE_7_3";   // HARDCODED_CREDENTIALS
    public const string RULE_8_1 = "RULE_8_1";   // MISSING_EFFECTIVE_MIGRATION_ERROR_ACTION
    public const string RULE_8_2 = "RULE_8_2";   // MISSING_EFFECTIVE_MIGRATION_ORDER
    public const string RULE_8_3 = "RULE_8_3";   // MISSING_EFFECTIVE_HASH_VALIDATION_SCOPE
}
