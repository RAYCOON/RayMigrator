using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

namespace Raycoon.RayMigrator.Core.Configuration.Options;

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

    private bool _isMigrationErrorActionInitialized;
    private MigrationErrorAction _migrationErrorAction;

    public ProductDefaultOptions() { }

    public ProductDefaultOptions(string? migrationFilesEncoding)
    {
        MigrationFilesEncoding = migrationFilesEncoding;
    }

    public MigrationErrorAction MigrationErrorActionEnum
    {
        get
        {
            if (_isMigrationErrorActionInitialized)
            {
                return _migrationErrorAction;
            }

            if (Enum.TryParse(MigrationErrorAction!, out _migrationErrorAction))
            {
                _isMigrationErrorActionInitialized = true;
                return _migrationErrorAction;
            }

            return Enums.MigrationErrorAction.Undefined;
        }
    }
    
    #endregion MigrationErrorAction

    #region RollbackErrorAction

    [ConfigurationKeyName("RollbackErrorAction")]
    [RayEnum(typeof(Enums.RollbackErrorAction), isRequired: false)]
    public string? RollbackErrorAction { get; set; }

    private bool _isRollbackErrorActionInitialized;
    private RollbackErrorAction _rollbackErrorAction;

    public RollbackErrorAction RollbackErrorActionEnum
    {
        get
        {
            if (_isRollbackErrorActionInitialized)
            {
                return _rollbackErrorAction;
            }

            if (Enum.TryParse(RollbackErrorAction!, out _rollbackErrorAction))
            {
                _isRollbackErrorActionInitialized = true;
                return _rollbackErrorAction;
            }

            return Enums.RollbackErrorAction.Undefined;
        }
    }

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
    /// Only applies to error-recovery rollback (MigrationErrorAction=Rollback/RollbackRelease),
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

    private bool _isTargetMigrationOrderInitialized;
    private TargetMigrationOrder _targetMigrationOrder;
    public TargetMigrationOrder TargetMigrationOrderEnum
    {
        get
        {
            if (_isTargetMigrationOrderInitialized)
            {
                return _targetMigrationOrder;
            }

            if (Enum.TryParse(TargetMigrationOrder!, out _targetMigrationOrder))
            {
                _isTargetMigrationOrderInitialized = true;
                return _targetMigrationOrder;
            }

            return Enums.TargetMigrationOrder.Undefined;
        }
    }

    #endregion TargetMigrationOrder
    
    #region HashValidationScope
    
    [RayEnum(typeof(Enums.HashValidationScope), isRequired: false)]
    public string? HashValidationScope { get; set; }

    private bool _isHashValidationScopeInitialized;
    private HashValidationScope _HashValidationScope;
    public HashValidationScope HashValidationScopeEnum
    {
        get
        {
            if (_isHashValidationScopeInitialized)
            {
                return _HashValidationScope;
            }

            if (Enum.TryParse(HashValidationScope!, out _HashValidationScope))
            {
                _isHashValidationScopeInitialized = true;
                return _HashValidationScope;
            }

            return Enums.HashValidationScope.Undefined;
        }
    }

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

    private bool _isMigrationErrorActionInitialized;
    private MigrationErrorAction _migrationErrorAction;

    public ProductOptions() { }

    public ProductOptions(string? migrationRollbackFilesPreExtension)
    {
        MigrationRollbackFilesPreExtension = migrationRollbackFilesPreExtension;
    }

    public MigrationErrorAction MigrationErrorActionEnum
    {
        get
        {
            if (_isMigrationErrorActionInitialized)
            {
                return _migrationErrorAction;
            }

            if (Enum.TryParse(MigrationErrorAction!, out _migrationErrorAction))
            {
                _isMigrationErrorActionInitialized = true;
                return _migrationErrorAction;
            }

            return Enums.MigrationErrorAction.Undefined;
        }
    }
    
    #endregion MigrationErrorAction

    #region RollbackErrorAction

    [ConfigurationKeyName("RollbackErrorAction")]
    [RayEnum(typeof(Enums.RollbackErrorAction), isRequired: false)]
    public string? RollbackErrorAction { get; set; }

    private bool _isRollbackErrorActionInitialized;
    private RollbackErrorAction _rollbackErrorAction;

    public RollbackErrorAction RollbackErrorActionEnum
    {
        get
        {
            if (_isRollbackErrorActionInitialized)
            {
                return _rollbackErrorAction;
            }

            if (Enum.TryParse(RollbackErrorAction!, out _rollbackErrorAction))
            {
                _isRollbackErrorActionInitialized = true;
                return _rollbackErrorAction;
            }

            return Enums.RollbackErrorAction.Undefined;
        }
    }

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
    /// Only applies to error-recovery rollback (MigrationErrorAction=Rollback/RollbackRelease),
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

    private bool _isTargetMigrationOrderInitialized;
    private TargetMigrationOrder _targetMigrationOrder;
    public TargetMigrationOrder TargetMigrationOrderEnum
    {
        get
        {
            if (_isTargetMigrationOrderInitialized)
            {
                return _targetMigrationOrder;
            }

            if (Enum.TryParse(TargetMigrationOrder!, out _targetMigrationOrder))
            {
                _isTargetMigrationOrderInitialized = true;
                return _targetMigrationOrder;
            }

            return Enums.TargetMigrationOrder.Undefined;
        }
    }

    #endregion TargetMigrationOrder
    
    #region HashValidationScope
    
    [RayEnum(typeof(Enums.HashValidationScope), isRequired: true)] // isRequired=true because value is being evaluated AFTER it was copied from its defaults by class 'ProductDefaultsPostConfigureOptions'
    public string? HashValidationScope { get; set; }

    private bool _isHashValidationScopeInitialized;
    private HashValidationScope _HashValidationScope;
    public HashValidationScope HashValidationScopeEnum
    {
        get
        {
            if (_isHashValidationScopeInitialized)
            {
                return _HashValidationScope;
            }

            if (Enum.TryParse(HashValidationScope!, out _HashValidationScope))
            {
                _isHashValidationScopeInitialized = true;
                return _HashValidationScope;
            }

            return Enums.HashValidationScope.Undefined;
        }
    }

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
    /// Default: File.
    /// </summary>
    [RayEnum(typeof(Enums.CliToolInputMode), isRequired: false)]
    public string? InputMode { get; set; }

    private bool _isInputModeInitialized;
    private CliToolInputMode _inputMode;

    public CliToolInputMode InputModeEnum
    {
        get
        {
            if (_isInputModeInitialized)
                return _inputMode;

            if (Enum.TryParse(InputMode!, out _inputMode))
            {
                _isInputModeInitialized = true;
                return _inputMode;
            }

            return Enums.CliToolInputMode.File; // Default to File
        }
    }

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
