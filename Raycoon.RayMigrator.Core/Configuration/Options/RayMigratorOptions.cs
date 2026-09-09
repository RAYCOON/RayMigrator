using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Configuration.Options;

/// <summary>
/// Converts the validated string representation of an enum-typed option into the enum member.
/// Matching is case-insensitive and mirrors the contract of <see cref="RayEnumAttribute"/>: the
/// allowed names are the enum members without the first (<c>Undefined</c>) sentinel. A non-empty
/// value that does not match any allowed name fails fast instead of silently degrading to
/// <c>Undefined</c>, which downstream code treats like a real member.
/// </summary>
internal static class OptionsEnumParser
{
    /// <summary>
    /// Tries to convert <paramref name="raw"/> into an allowed member of <typeparamref name="TEnum"/>.
    /// Only the member names after the <c>Undefined</c> sentinel are accepted, compared with
    /// <see cref="StringComparison.OrdinalIgnoreCase"/> and without trimming — exactly what
    /// <see cref="RayEnumAttribute"/> accepts. Numeric strings, comma-separated lists and the literal
    /// <c>Undefined</c> are rejected even though <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>
    /// would accept them.
    /// </summary>
    /// <returns><c>true</c> when <paramref name="value"/> holds the member; otherwise <c>false</c> and
    /// <paramref name="error"/> carries the message in the wording of <see cref="RayEnumAttribute"/>.</returns>
    internal static bool TryParse<TEnum>(string? raw, string propertyName, out TEnum value, out string? error)
        where TEnum : struct, Enum
    {
        var allowedValues = typeof(TEnum).AllowedValues();
        // member names after the Undefined sentinel, case-insensitive
        var match = typeof(TEnum).ResolveMemberName(raw);

        if (match is not null)
        {
            value = Enum.Parse<TEnum>(match);
            error = null;
            return true;
        }

        value = default;
        error = $"Invalid value [{raw}] for property [{propertyName}]. Allowed values: [{string.Join(", ", allowedValues)}].";
        return false;
    }

    /// <summary>
    /// Same as <see cref="TryParse{TEnum}"/>, but throws <see cref="ConfigurationValidationException"/> for a value
    /// that is not an allowed member.
    /// </summary>
    internal static TEnum ParseOrThrow<TEnum>(string raw, string propertyName) where TEnum : struct, Enum
    {
        if (TryParse<TEnum>(raw, propertyName, out var value, out var error))
        {
            return value;
        }

        throw new ConfigurationValidationException(error!);
    }
}

/// <summary>
/// Holds the enum member behind a string-typed option (<c>MigrationErrorAction</c>, <c>HashValidationScope</c>, ...).
/// The options classes bind the JSON value as a string so that <see cref="RayEnumAttribute"/> can validate it; the
/// <c>*Enum</c> property of each option resolves it through this helper. The parse result is cached per string
/// value: a changed string (defaults merged by <c>ProductDefaultsPostConfigureOptions</c>) is parsed again, an
/// unset string yields <paramref name="missing"/> without caching. Replaces eight hand-written getters (#19).
/// </summary>
internal sealed class ParsedEnumOption<TEnum> where TEnum : struct, Enum
{
    private string? _parsedFrom;
    private TEnum _value;

    /// <summary>Resolves <paramref name="raw"/> to its member, or <paramref name="missing"/> when it is unset.</summary>
    /// <exception cref="ConfigurationValidationException">The value is not a member name.</exception>
    public TEnum Resolve(string? raw, string propertyName, TEnum missing = default)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return missing;
        }

        if (!string.Equals(_parsedFrom, raw, StringComparison.Ordinal))
        {
            _value = OptionsEnumParser.ParseOrThrow<TEnum>(raw, propertyName);
            _parsedFrom = raw;
        }

        return _value;
    }
}

/// <summary>
/// 
/// </summary>
public class RayMigratorOptions
{
/*
    /// <summary>
    /// The (relative) root directory of the DAL .sql template-files.
    /// </summary>
    [Required]
    [RayDirectoryExists]
    public string? DatabaseAccessLayersRootDirectory { get; set; }
*/

    /// <summary>
    /// The Migration Repository.
    /// </summary>
    /// <remarks>Validation see https://stackoverflow.com/questions/51692665/validation-of-asp-net-core-options-during-startup.</remarks>
    [ValidateObjectMembers]
    public RepositoryOptions? Repository { get; set; }
    
    /// <summary>
    /// Class for template-dependent logging into a database table
    /// </summary>
    /// <remarks>Validation see https://stackoverflow.com/questions/51692665/validation-of-asp-net-core-options-during-startup.</remarks>
    // [Required] does not make a difference, therefore existence is validated in RayMigratorOptionsValidator 
    [ValidateObjectMembers]
    public DatabaseLoggingOptions? DatabaseLogging { get; set; }

    public SerilogOptions? Serilog { get; set; }
    
    [Required]
    [ValidateObjectMembers]
    public ProductDefaultOptions? ProductDefaults { get; set; }
    
    [Required]
    [ValidateEnumeratedItems]
    public List<ProductOptions>? Products { get; set; }

    /// <summary>
    /// Global CLI tool definitions. Referenced by UseCliToolAlias in Products, TargetGroups, Targets, migsettings, and TOML headers.
    /// </summary>
    [ValidateEnumeratedItems]
    public List<CliToolOptions>? CliTools { get; set; }
}


/// <summary>
/// 
/// </summary>
public class RepositoryOptions
{
    [Required]
    public string? DatabaseType { get; set; }

    [RayConnectionString(false)]
    public string? ConnectionString { get; set; }
    
    public string? SchemaName { get; set; }

    public string? TableBaseName { get; set; }

    [RayRangeInt(0, int.MaxValue, 60)]
    public int? DbCommandTimeoutInSeconds { get; set; }

    // ToDo: implement retry
    [RayRangeInt(0, int.MaxValue, 100)]
    public int? DbCommandMaxRetries { get; set; }

    [RayRangeInt(0, int.MaxValue, 250)]
    public int? DbCommandWaitTimeInMsBeforeRetry { get; set; }
}


/// <summary>
/// 
/// </summary>
public class DatabaseLoggingOptions
{
    public string? DatabaseType { get; set; }
    
    [RayEnum(typeof(LogLevel), false, false)]
    public string? MinimumLevel { get; set; }
    
    [RayConnectionString(false)]
    public string? ConnectionString { get; set; }
    
    public string? SchemaName { get; set; }
    
    public string? TableBaseName { get; set; }
    
    [RayRangeInt(0, int.MaxValue, 20)]
    public int? DbCommandTimeoutInSeconds { get; set; }
}


/// <summary>
/// Class for checking if Serilog-section exists (used in RayMigratorOptionsValidator) 
/// </summary>
public class SerilogOptions { }


/// <summary>
/// 
/// </summary>
public class ProductDefaultOptions
{
    #region MigrationErrorAction

    [ConfigurationKeyName("MigrationErrorAction")]
    [RayEnum(typeof(Enums.MigrationErrorAction), isRequired: false)]
    public string? MigrationErrorAction { get; set; }

    public ProductDefaultOptions() { }

    public ProductDefaultOptions(string? migrationFilesEncoding)
    {
        MigrationFilesEncoding = migrationFilesEncoding;
    }

    private readonly ParsedEnumOption<Enums.MigrationErrorAction> _migrationErrorAction = new();

    /// <summary>
    /// <see cref="MigrationErrorAction"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.MigrationErrorAction MigrationErrorActionEnum => _migrationErrorAction.Resolve(MigrationErrorAction, nameof(MigrationErrorAction));
    
    #endregion MigrationErrorAction

    #region RollbackErrorAction

    [ConfigurationKeyName("RollbackErrorAction")]
    [RayEnum(typeof(Enums.RollbackErrorAction), isRequired: false)]
    public string? RollbackErrorAction { get; set; }

    private readonly ParsedEnumOption<Enums.RollbackErrorAction> _rollbackErrorAction = new();

    /// <summary>
    /// <see cref="RollbackErrorAction"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.RollbackErrorAction RollbackErrorActionEnum => _rollbackErrorAction.Resolve(RollbackErrorAction, nameof(RollbackErrorAction));

    #endregion RollbackErrorAction

    [RegularExpression("^[a-zA-Z_]+$", ErrorMessage = "Only lowercase and uppercase letters, as well as underscores, are allowed.")]
    public string? MigrationFilesExtension { get; set; }

    [RegularExpression("^[a-zA-Z_]+$", ErrorMessage = "Only lowercase and uppercase letters, as well as underscores, are allowed.")]
    public string? MigrationRollbackFilesPreExtension { get; set; }

    [RayEncoding]
    public string? MigrationFilesEncoding { get; set; }

    public bool? RequireRollbackFile { get; set; }

    /// <summary>
    /// When true (default), an error-recovery rollback chain stops when a rollback file is missing
    /// (RequireRollbackFile=false). When false, the chain continues and skips the missing file.
    /// Only applies to error-recovery rollback (MigrationErrorAction=Rollback/RollbackErrorOnly/RollbackRelease),
    /// not to explicit Migrate-Down.
    /// </summary>
    public bool? StopRollbackOnMissingRollbackFile { get; set; }

    /// <summary>
    /// CLI tool alias to use for migration execution instead of the DAL.
    /// References a CliTools[].Alias defined at the RayMigrator root level.
    /// Null or empty means use the DAL (default behavior).
    /// </summary>
    public string? UseCliToolAlias { get; set; }

    [ValidateObjectMembers]
    public TargetGroupDefaultOptions? TargetGroupDefaults { get; set; }
}


/// <summary>
///
/// </summary>
public class TargetGroupDefaultOptions
{
    #region TargetMigrationOrder

    [RayEnum(typeof(Enums.TargetMigrationOrder), isRequired: false)]
    public string? TargetMigrationOrder { get; set; }

    private readonly ParsedEnumOption<Enums.TargetMigrationOrder> _targetMigrationOrder = new();

    /// <summary>
    /// <see cref="TargetMigrationOrder"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.TargetMigrationOrder TargetMigrationOrderEnum => _targetMigrationOrder.Resolve(TargetMigrationOrder, nameof(TargetMigrationOrder));

    #endregion TargetMigrationOrder
    
    #region HashValidationScope
    
    [RayEnum(typeof(Enums.HashValidationScope), isRequired: false)]
    public string? HashValidationScope { get; set; }

    private readonly ParsedEnumOption<Enums.HashValidationScope> _hashValidationScope = new();

    /// <summary>
    /// <see cref="HashValidationScope"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.HashValidationScope HashValidationScopeEnum => _hashValidationScope.Resolve(HashValidationScope, nameof(HashValidationScope));

    #endregion HashValidationScope

    /// <summary>
    /// When true (default), an error-recovery rollback chain stops when a rollback file is missing
    /// (RequireRollbackFile=false). When false, the chain continues and skips the missing file.
    /// </summary>
    public bool? StopRollbackOnMissingRollbackFile { get; set; }

    [Required] // Annotation [ValidateObjectMembers] also ensures that TargetDefaultsOptions is not null - only IF TargetDefaults is NOT nullable!
    [ValidateObjectMembers]
    public TargetDefaultsOptions? TargetDefaults { get; set; }
}


/// <summary>
///
/// </summary>
public class TargetDefaultsOptions
{
    // The values are defined as string here because the JSON may also contain environment variables (e.g. "{ENV:DbConnectionTimeoutInSeconds}").
    [RayRangeInt(0, int.MaxValue, 20)]
    public int? DbCommandTimeoutInSeconds { get; set; } // int must be nullable, otherwise defaultValue is not applied!

    [RayRangeInt(0, int.MaxValue, 0)]
    public int? DbCommandMaxRetries { get; set; } // int must be nullable, otherwise defaultValue is not applied!

    [RayRangeInt(0, int.MaxValue, 250)]
    public int? DbCommandWaitTimeInMsBeforeRetry { get; set; } // int must be nullable, otherwise defaultValue is not applied!
}


/// <summary>
/// 
/// </summary>
public class ProductOptions
{
    [Required]
    [RegularExpression(@"^(?=.{1,50}$)[\p{L}\p{N}_]+$", ErrorMessage = "Only letters, numbers and underscores with a maximum length of 50 characters are allowed.")]
    public string? Alias { get; set; }
    
    [Required]
    [RayDirectoryExists]    
    public string? MigrationFilesRootDirectory { get; set; }

    #region MigrationErrorAction

    [ConfigurationKeyName("MigrationErrorAction")]
    [RayEnum(typeof(Enums.MigrationErrorAction), isRequired: true)]
    public string? MigrationErrorAction { get; set; }

    public ProductOptions() { }

    public ProductOptions(string? migrationRollbackFilesPreExtension)
    {
        MigrationRollbackFilesPreExtension = migrationRollbackFilesPreExtension;
    }

    private readonly ParsedEnumOption<Enums.MigrationErrorAction> _migrationErrorAction = new();

    /// <summary>
    /// <see cref="MigrationErrorAction"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.MigrationErrorAction MigrationErrorActionEnum => _migrationErrorAction.Resolve(MigrationErrorAction, nameof(MigrationErrorAction));
    
    #endregion MigrationErrorAction

    #region RollbackErrorAction

    [ConfigurationKeyName("RollbackErrorAction")]
    [RayEnum(typeof(Enums.RollbackErrorAction), isRequired: false)]
    public string? RollbackErrorAction { get; set; }

    private readonly ParsedEnumOption<Enums.RollbackErrorAction> _rollbackErrorAction = new();

    /// <summary>
    /// <see cref="RollbackErrorAction"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.RollbackErrorAction RollbackErrorActionEnum => _rollbackErrorAction.Resolve(RollbackErrorAction, nameof(RollbackErrorAction));

    #endregion RollbackErrorAction

    [RegularExpression("^[a-zA-Z_]+$", ErrorMessage = "Only lowercase and uppercase letters, as well as underscores, are allowed.")]
    public string? MigrationFilesExtension { get; set; }

    [RegularExpression("^[a-zA-Z_]+$", ErrorMessage = "Only lowercase and uppercase letters, as well as underscores, are allowed.")]
    public string? MigrationRollbackFilesPreExtension { get; set; }

    [RayEncoding]
    public string? MigrationFilesEncoding { get; set; }

    public bool? RequireRollbackFile { get; set; }

    /// <summary>
    /// When true (default), an error-recovery rollback chain stops when a rollback file is missing
    /// (RequireRollbackFile=false). When false, the chain continues and skips the missing file.
    /// Only applies to error-recovery rollback (MigrationErrorAction=Rollback/RollbackErrorOnly/RollbackRelease),
    /// not to explicit Migrate-Down.
    /// </summary>
    public bool? StopRollbackOnMissingRollbackFile { get; set; }

    /// <summary>
    /// CLI tool alias to use for migration execution instead of the DAL.
    /// Inherits from ProductDefaults.UseCliToolAlias if not set. Can be overridden per TargetGroup or Target.
    /// </summary>
    public string? UseCliToolAlias { get; set; }

    /// <summary>
    /// Comma-separated list of TargetGroup aliases defining the execution order.
    /// When specified, all TargetGroup aliases must be listed exactly once.
    /// Only applicable when the product has more than one TargetGroup.
    /// Applies to MigrateUp and Baseline commands only.
    /// </summary>
    public string? TargetGroupMigrationOrder { get; set; }

    /// <summary>
    /// The List of Target Groups.
    /// </summary>
    /// <remarks>Validation see https://stackoverflow.com/questions/51692665/validation-of-asp-net-core-options-during-startup.</remarks>
    [Required]
    [ValidateEnumeratedItems]
    public List<TargetGroupOptions>? TargetGroups { get; set; }
}


/// <summary>
///
/// </summary>
public class TargetGroupOptions
{
    [Required]
    [RegularExpression(@"^(?=.{1,50}$)[\p{L}\p{N}_]+$", ErrorMessage = "Only letters, numbers and underscores with a maximum length of 50 characters are allowed.")]
    public string? Alias { get; set; }

    [Required]
    public string? DatabaseType { get; set; }

    #region TargetMigrationOrder

    [RayEnum(typeof(Enums.TargetMigrationOrder), isRequired: true)] // isRequired=true because value is being evaluated AFTER it was copied from its defaults by class 'ProductDefaultsPostConfigureOptions'
    public string? TargetMigrationOrder { get; set; }

    private readonly ParsedEnumOption<Enums.TargetMigrationOrder> _targetMigrationOrder = new();

    /// <summary>
    /// <see cref="TargetMigrationOrder"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.TargetMigrationOrder TargetMigrationOrderEnum => _targetMigrationOrder.Resolve(TargetMigrationOrder, nameof(TargetMigrationOrder));

    #endregion TargetMigrationOrder
    
    #region HashValidationScope
    
    [RayEnum(typeof(Enums.HashValidationScope), isRequired: true)] // isRequired=true because value is being evaluated AFTER it was copied from its defaults by class 'ProductDefaultsPostConfigureOptions'
    public string? HashValidationScope { get; set; }

    private readonly ParsedEnumOption<Enums.HashValidationScope> _hashValidationScope = new();

    /// <summary>
    /// <see cref="HashValidationScope"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.HashValidationScope HashValidationScopeEnum => _hashValidationScope.Resolve(HashValidationScope, nameof(HashValidationScope));

    #endregion HashValidationScope

    /// <summary>
    /// CLI tool alias to use for migration execution instead of the DAL.
    /// Inherits from Product.UseCliToolAlias if not set. Can be overridden per Target.
    /// </summary>
    public string? UseCliToolAlias { get; set; }

    /// <summary>
    /// When true (default), an error-recovery rollback chain stops when a rollback file is missing
    /// (RequireRollbackFile=false). When false, the chain continues and skips the missing file.
    /// </summary>
    public bool? StopRollbackOnMissingRollbackFile { get; set; }

    /// <summary>
    /// The List of Targets.
    /// </summary>
    /// <remarks>Validation see https://stackoverflow.com/questions/51692665/validation-of-asp-net-core-options-during-startup.</remarks>
    [Required]
    [ConfigurationKeyName("Targets")]
    [ValidateEnumeratedItems]
    public List<TargetOptions>? Targets { get; set; }
}


/// <summary>
/// 
/// </summary>
public class TargetOptions
{
    [Required]
    [RegularExpression(@"^(?=.{1,50}$)[\p{L}\p{N}_]+$", ErrorMessage = "Only letters, numbers and underscores with a maximum length of 50 characters are allowed.")]
    public string? Alias { get; set; }

    [Required]
    [RayConnectionString(false)]
    public string? ConnectionString { get; set; }
    
    //[Required] // Not set to Required since defaultValue cannot be applied
    [RayRangeInt(0, Int32.MaxValue, 20)]
    public int? DbCommandTimeoutInSeconds { get; set; } // int must be nullable, otherwise defaultValue is not applied!
    
    //[Required] // Not set to Required since defaultValue cannot be applied
    [RayRangeInt(0, Int32.MaxValue, 0)]
    public int? DbCommandMaxRetries { get; set; } // int must be nullable, otherwise defaultValue is not applied!
    
    //[Required] // Not set to Required since defaultValue cannot be applied
    [RayRangeInt(0, Int32.MaxValue, 500)]
    public int? DbCommandWaitTimeInMsBeforeRetry { get; set; } // int must be nullable, otherwise defaultValue is not applied!

    /// <summary>
    /// CLI tool alias to use for migration execution instead of the DAL.
    /// Inherits from TargetGroup.UseCliToolAlias if not set. Can be overridden per migration file via TOML or migsettings.
    /// </summary>
    public string? UseCliToolAlias { get; set; }

    /// <summary>
    /// Key-value pairs for placeholder substitution in the CLI tool's ArgumentTemplate.
    /// Values support {ENV:VAR} replacement (resolved at configuration load time).
    /// Example: {"Server": "localhost", "User": "sa", "Password": "{ENV:SA_PASSWORD}", "Database": "mydb"}
    /// </summary>
    public Dictionary<string, string>? CliToolParameters { get; set; }
}


/// <summary>
/// Defines an external CLI tool that can execute migration SQL files
/// instead of the built-in DAL (e.g., sqlcmd, psql, mysql, mariadb, sqlite3).
/// </summary>
public class CliToolOptions
{
    [Required]
    [RegularExpression(@"^(?=.{1,50}$)[\p{L}\p{N}_\-]+$", ErrorMessage = "Only letters, numbers, underscores and hyphens with a maximum length of 50 characters are allowed.")]
    public string? Alias { get; set; }

    /// <summary>
    /// Path to the CLI tool executable (absolute or relative/in PATH).
    /// </summary>
    [Required]
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Command-line argument template with placeholders.
    /// {FilePath} is replaced with the migration file path (when InputMode=File).
    /// Custom placeholders (e.g., {Server}, {User}) are resolved from CliToolParameters on the Target.
    /// Example: "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath} -b"
    /// </summary>
    [Required]
    public string? ArgumentTemplate { get; set; }

    #region InputMode

    /// <summary>
    /// Determines how the SQL file is passed to the CLI tool: "File" (as argument) or "Stdin" (piped via stdin).
    /// Required; there is no default (RULE_3_11 in the Validation project, #19).
    /// </summary>
    [RayEnum(typeof(Enums.CliToolInputMode), isRequired: true)]
    public string? InputMode { get; set; }

    private readonly ParsedEnumOption<Enums.CliToolInputMode> _inputMode = new();

    /// <summary>
    /// <see cref="InputMode"/> as enum member. Undefined while the string is unset; a value that is not a member
    /// name throws (see <see cref="ParsedEnumOption{TEnum}"/>).
    /// </summary>
    public Enums.CliToolInputMode InputModeEnum => _inputMode.Resolve(InputMode, nameof(InputMode));

    #endregion InputMode

    #region SuccessExitCodes

    /// <summary>
    /// Exit code expressions that indicate successful execution.
    /// Supports single values ("0"), closed ranges ("1..5"), and open ranges ("10..", "..-1").
    /// Default: ["0"].
    /// </summary>
    public string[]? SuccessExitCodes { get; set; }

    private bool _isExitCodeMatcherInitialized;
    private ExitCodeMatcher _exitCodeMatcher = ExitCodeMatcher.Default;

    /// <summary>
    /// Parsed and cached <see cref="ExitCodeMatcher"/> instance built from <see cref="SuccessExitCodes"/>.
    /// Falls back to <see cref="ExitCodeMatcher.Default"/> if parsing fails or no expressions are configured.
    /// </summary>
    public ExitCodeMatcher ExitCodeMatcherInstance
    {
        get
        {
            if (_isExitCodeMatcherInitialized)
                return _exitCodeMatcher;

            if (ExitCodeMatcher.TryParse(SuccessExitCodes, out var matcher, out _))
            {
                _exitCodeMatcher = matcher;
                _isExitCodeMatcherInitialized = true;
                return _exitCodeMatcher;
            }

            return _exitCodeMatcher; // Default; not cached so validator has a chance to report the error
        }
    }

    #endregion SuccessExitCodes

    /// <summary>
    /// Maximum time in seconds to wait for the CLI tool to complete. Default: 120.
    /// </summary>
    [RayRangeInt(1, int.MaxValue, 120)]
    public int? CliToolTimeoutInSeconds { get; set; }
}
