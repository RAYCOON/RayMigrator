# Component Responsibilities

Detailed breakdown of each component's responsibilities and key classes.

## Console Layer Components

### `Program.cs`
**Location**: `Raycoon.RayMigrator.Console/Program.cs`

**Responsibilities**:
- Parse and validate command-line arguments via `CommandLineConfiguration` (System.CommandLine)
- Initialize `SensitiveDataMasker` with the `RevealSensitiveData` flag from the parsed options
- Resolve environment from CLI arguments or `DOTNET_ENVIRONMENT` variable via `EnvironmentResolver`
- Dispatch to `RunDirectMode()` with `JsonOptionsSource` (constructed from `rayMigratorConsoleOptions.ConfigDir`)
- `RunDirectMode()` loads the configuration via `IOptionsSource.LoadAsync()` and then delegates to `DirectModePipeline.ExecuteAsync()` for unified host build and service execution
- Converts exceptions into exit codes (parser failure = 5, `ApplicationStartupException` = 1, other exceptions = 100)

### `AssemblyInfoHelper` (Console)
**Location**: `Raycoon.RayMigrator.Console/AssemblyInfoHelper.cs`

**Purpose**: Provides `GetAssemblyInfo()` (delegates to `Shared.AssemblyInfoHelper.GetAsciiHeader()`) and `GetRayMigratorVersion()` (delegates to `Shared.AssemblyInfoHelper.GetRayMigratorVersion()`).

### Launch Profiles
**Location**: `Raycoon.RayMigrator.Console/Properties/launchSettings.json`

**Purpose**: IDE integration for development/debugging. Defines profiles per engine (SQL Server, PostgreSQL, MariaDB, MySQL, SQLite) and platform (Mac/Win) with the CLI arguments and `ConnectionString_*` environment variables required by the referenced appsettings files.

**Required Environment Variables**:
- `DOTNET_ENVIRONMENT` - Target environment (default `Docker`)
- `ConnectionString_*` - Database connection strings consumed via `{ENV:ConnectionString_*}` placeholders in appsettings

## Pipeline Layer Components

### `DirectModePipeline`
**Location**: `Raycoon.RayMigrator.Pipeline/DirectModePipeline.cs`

**Purpose**: Unified execution pipeline for Standalone mode. Handles the complete lifecycle after options have been loaded: Serilog creation, DI host build, DatabaseLogWriter init, DAL properties, connection validation, `RayMigratorService` execution, and shutdown.

**Key Steps**:
1. Early configuration validation (check for Serilog section)
2. Create Serilog logger via `SerilogFactory.Create()`
3. Log environment variable replacements
4. Build DI host with `Host.CreateDefaultBuilder(args)`
5. Resolve `DatabaseLogWriter` (triggers options validation in JSON mode)
6. Register sensitive data for masking
7. Validate product alias against configured products (with case-sensitivity hints)
8. Resolve `MigrationContext`, set `MigrationLoggingContext.Current`
9. Initialize `DatabaseLogWriter` with template infrastructure
10. Initialize `DalSpecificPropertiesDictionary` for all configured database types
11. Validate schema names via `SchemaNameValidator`
12. Validate connections via `ConnectionValidator`
13. Execute `RayMigratorService.DoWorkAsync(host)`
14. Shutdown (flush log queue, stop host)

### `JsonOptionsSource`
**Location**: `Raycoon.RayMigrator.Pipeline/JsonOptionsSource.cs`

**Purpose**: `IOptionsSource` implementation that loads configuration from JSON files (appsettings.json hierarchy), performs `{ENV:...}` replacement, and returns `OptionsSourceResult` with `HostConfiguration` for DI binding. Accepts an optional `configDir` parameter (from the `--config-dir` global CLI option) that overrides the base path for locating configuration files; defaults to the current working directory when null or empty.

### `RayMigratorService`
**Location**: `Raycoon.RayMigrator.Pipeline/RayMigratorService.cs`

**Responsibilities**:
- Bridge between CLI commands and `IMigrationService`
- Create request objects from CLI parameters
- Execute service methods asynchronously
- Convert service results to exit codes (0 = success, 1+ = error)
- Handle top-level exceptions (including `MigrationAlreadyRunningException`)

**Key Methods**:
```csharp
Task<int> DoWorkAsync(IHost host)              // Primary entry point, dispatches by command
// All Execute* methods are private:
private Task<int> ExecuteMigrateUpAsync()
private Task<int> ExecuteMigrateDownAsync()
private Task<int> ExecuteValidateHashAsync()
private Task<int> ExecuteUpdateHashAsync()
private Task<int> ExecuteBaselineAsync()
private Task<int> ExecuteInfoAsync()
private Task<int> ExecuteFixIssuesAsync()
```

### `SerilogFactory`
**Location**: `Raycoon.RayMigrator.Pipeline/SerilogFactory.cs`

**Purpose**: Creates and configures the Serilog logger from the RayMigrator configuration section. Handles the database sink creation when DatabaseLogging is configured. Also maps `Microsoft.Extensions.Logging.LogLevel` to `Serilog.Events.LogEventLevel`.

## Service Layer Components

### `IMigrationService`
**Location**: `Raycoon.RayMigrator.Services.Abstractions/IMigrationService.cs`

**Interface Definition**:
```csharp
public interface IMigrationService
{
    Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request);
    Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request);
    Task<ValidationResult> ValidateHashAsync(ValidateHashRequest request);
    Task<HashUpdateResult> UpdateHashAsync(UpdateHashRequest request);
    Task<BaselineResult> BaselineAsync(BaselineRequest request);
    Task<MigrationStatusInfo> GetStatusAsync(string productAlias);
    Task<MigrationHistory> GetHistoryAsync(string productAlias, int limit = 100);
    Task<FixIssuesResult> FixIssuesAsync(FixIssuesRequest request);
}
```

### `MigrationService`
**Location**: `Raycoon.RayMigrator.Services/MigrationService.cs`

**Responsibilities**:
- Implement `IMigrationService`
- Validate requests against `MigrationContext`
- Coordinate with `TemplateExecutor` for repository operations
- Orchestrate migration execution flow
- Return strongly-typed results

**Dependencies**:
- `ILogger<MigrationService>` (injected)
- `IOptions<RayMigratorOptions>` (injected)
- `TemplateExecutor` (injected)
- `IMigrationContextAccessor` (injected) — accesses the current `MigrationContext` via `_ctxAccessor.Current`
- `IServiceProvider` (injected)
- `ICliToolExecutor` (injected) — executes external CLI tools when `UseCliToolAlias` is configured

**Key internal methods related to file discovery**:
- `DiscoverAndPrepareMigrationFiles` — entry point for file scanning, calls the validators below
- `ValidateTargetGroupAliasCasing` (internal static) — checks that subdirectory names in release directories match configured TargetGroup aliases exactly (case-sensitive); throws `ConfigurationValidationException` on mismatch
- `ValidateFlatLayoutAmbiguity` (internal static) — for single-TargetGroup products, ensures no release directory mixes flat-layout files (directly under the release dir) and traditional-layout files (under a TargetGroup subdirectory); throws `ConfigurationValidationException` on ambiguity
- `ResolveTargetGroupMigrationOrder` (internal) — resolves TargetGroup execution order from CLI > migsettings > appsettings > config array order
- `LoadMigSettingsDefaults` (internal) — loads and merges `migsettings.txt` and `migsettings.{Environment}.txt` files

**Key internal methods related to execution**:
- `CanUseSharedConnection` (internal static) — returns `true` when the target and repository share the same `DatabaseType` and `ConnectionString`, `UseTransaction=true` is set, and block errors are not ignored (`MigrationErrorAction != Ignore`). When `true`, `ExecuteSqlBlocks` delegates to `ExecuteSqlBlocksAtomic`, which executes all SQL blocks and repository status updates in a single atomic transaction on a shared connection. Retries are supported: when `DbCommandMaxRetries > 0`, transient errors trigger a file-level retry (full transaction rollback and re-execution from block 1).

### Request/Response DTOs

**Location**: `Raycoon.RayMigrator.Services.Abstractions/Models/`

All request types are in `Models/Requests.cs`, all result types are in `Models/Results.cs`.

| Request DTO | Purpose |
|-----|---------|
| `MigrateUpRequest` | Parameters for forward migration |
| `MigrateDownRequest` | Parameters for rollback migration |
| `ValidateHashRequest` | Parameters for hash validation |
| `UpdateHashRequest` | Parameters for hash update |
| `BaselineRequest` | Parameters for baseline operation |
| `FixIssuesRequest` | Parameters for repository fix operations |

| Result DTO | Purpose |
|-----|---------|
| `OperationResult` | Abstract base class (Success, ErrorMessage, ErrorCode, Messages, ExecutedAt, Duration) |
| `MigrationOperationResult` | Result of migration up/down (extends `OperationResult`) |
| `ValidationResult` | Result of hash validation |
| `HashUpdateResult` | Result of hash update |
| `BaselineResult` | Result of baseline operation |
| `MigrationStatusInfo` | Current migration status for a product |
| `FixIssuesResult` | Result of repository fix operations |
| `MigrationHistory` | Migration run history |

### CLI Tool Execution

**Location**: `Raycoon.RayMigrator.Services/CliToolExecutor.cs`

| Class | Purpose |
|-------|---------|
| `ICliToolExecutor` | Interface for executing external SQL CLI tools (sqlcmd, psql, mysql, mariadb, sqlite3) as an alternative to DAL-based SQL execution |
| `CliToolExecutor` | Implementation using `System.Diagnostics.Process`. Handles stdin/file input modes, timeout enforcement, exit code evaluation, and structured logging |
| `CliToolExecutionRequest` | Request model: `ExecutablePath`, `Arguments`, `InputMode`, `FileContent`, `FilePath`, `Filename`, `TimeoutInSeconds`, `ExitCodeMatcher` |
| `CliToolExecutionResult` | Result model: `Success`, `ExitCode`, `StandardOutput`, `StandardError`, `Duration`, `ErrorMessage` |

## Core Layer Components

### `MigrationContext`
**Location**: `Raycoon.RayMigrator.Core/MigrationContext.cs`

**Purpose**: Central state object carrying configuration and runtime state through execution pipeline.

**Key Properties**:
```csharp
public RayMigratorOptions RayMigratorOptions { get; set; }
public RayMigratorConsoleOptions RayMigratorConsoleOptions { get; set; }
public string RayMigratorVersion { get; set; }
public MigrationState MigrationState { get; set; }
public IEnumerable<TargetGroupOptions>? ProductTargetGroupOptionsEnumerable { get; init; }
public ConcurrentDictionary<string, DalSpecificProperties> DalSpecificPropertiesDictionary { get; set; }
public MigrationContext Clone { get; }  // Deep copy of MigrationState for logging
```

**Constructor**:
```csharp
public MigrationContext(
    RayMigratorOptions rayMigratorOptions,
    RayMigratorConsoleOptions rayMigratorConsoleOptions,
    string rayMigratorVersion,
    MigrationState? migrationState = null)
```

The constructor also eagerly initializes `ProductTargetGroupOptionsEnumerable` from the product matching `RayMigratorConsoleOptions.Product`.

**Lifecycle**: See [MigrationContext](../02-core-concepts/migration-context.md) for the full lifecycle description.

### Configuration Options

**Location**: `Raycoon.RayMigrator.Core/Configuration/Options/`

| Class | Purpose |
|-------|---------|
| `RayMigratorBootstrapOptions` | Bootstrap configuration for mode detection (AdminDb, Serilog) |
| `AdminDbOptions` | Admin database connection settings (Provider, ConnectionString, SchemaName). Part of Engine's Core NuGet contract; consumed by RayMigrator Studio. Not active in standalone CLI mode. |
| `RayMigratorOptions` | Root configuration from appsettings.json |
| `RayMigratorConsoleOptions` | CLI parameters (command, product, environment, run mode, target release, target group aliases, target group execution order, hash validation scope, fix options, allow out of order, stop rollback on missing rollback file, config dir, etc.) |
| `CommandLineConfiguration` | System.CommandLine root command definition and argument parsing |
| `RepositoryOptions` | Repository database settings |
| `ProductDefaultOptions` | Product-level defaults (inherited by all products), includes `UseCliToolAlias`, `StopRollbackOnMissingRollbackFile` |
| `TargetGroupDefaultOptions` | TargetGroup-level defaults (TargetMigrationOrder, HashValidationScope, StopRollbackOnMissingRollbackFile) |
| `TargetDefaultsOptions` | Target-level defaults (timeouts, retries) |
| `ProductOptions` | Product-level settings, includes `UseCliToolAlias`, `StopRollbackOnMissingRollbackFile`, and `TargetGroupMigrationOrder` |
| `TargetGroupOptions` | Target group settings, includes `UseCliToolAlias`, `StopRollbackOnMissingRollbackFile` |
| `TargetOptions` | Individual target settings, includes `UseCliToolAlias` and `CliToolParameters` |
| `DatabaseLoggingOptions` | Logging database settings |
| `SerilogOptions` | Serilog configuration section marker |
| `CliToolOptions` | External CLI tool definition (Alias, ExecutablePath, ArgumentTemplate, InputMode, SuccessExitCodes, CliToolTimeoutInSeconds). `ExitCodeMatcherInstance` provides the parsed `ExitCodeMatcher`. |
| `ExitCodeMatcher` | Parses `SuccessExitCodes` expressions (single values `"0"`, closed ranges `"1..5"`, open ranges `"10.."`, `"..-1"`) and matches process exit codes against them. |

### Migration State

**Location**: `Raycoon.RayMigrator.Core/MigrationState.cs`

**Properties**:
```csharp
// Migration Process
public MigrationEvent? MigrationEvent { get; set; }

// RunId's
public int MigratorMetaId { get; set; }  // Set from RepositoryCheckCreate result
public int ProductId { get; set; }
public int EnvironmentId { get; set; }   // Set from RepositoryEnvironmentCheckInsert result
public int MigrationRunId { get; set; }
public int MigrationId { get; set; }

// File metadata
public string ReleaseVersionFromFileNameWithPath { get; set; }
public string FilenameWithRelativePath { get; set; }
public int FileOrderId { get; set; }
public int FileBlockId { get; set; }

// Step / Result
public MigrationRunResult MigrationRunResult { get; set; }
public MigrationOperation MigrationOperation { get; set; }
public MigrationStatus MigrationStatus { get; set; }

// TargetGroup- / Target-settings
public string TargetGroupAlias { get; set; }
public HashValidationScope? HashValidationScope { get; set; }
public string TargetAlias { get; set; }
```

### `MigrationStateSnapshot`
**Location**: `Raycoon.RayMigrator.Core/MigrationStateSnapshot.cs`

**Purpose**: Immutable snapshot of `MigrationState` properties used for structured logging. The snapshot captures all state properties (`ProductId`, `MigrationRunId`, `MigrationId`, file metadata, `MigrationRunResult`, `MigrationOperation`, `MigrationStatus`, target group/target aliases, `HashValidationScope`) as `init`-only properties.

> **Note**: `MigrationContext.Clone` creates a new `MigrationContext` with a deep-copied `MigrationState`, not a `MigrationStateSnapshot`. The `MigrationStateSnapshot` class is a separate lightweight type for log enrichment.

### `IMigrationContextAccessor`
**Location**: `Raycoon.RayMigrator.Core/IMigrationContextAccessor.cs`

**Purpose**: Provides access to the current `MigrationContext` for the execution scope. Two implementations support different hosting models:

| Class | Purpose |
|-------|---------|
| `SingletonMigrationContextAccessor` | CLI mode: wraps a single `MigrationContext` instance (registered as Singleton) |
| `AsyncLocalMigrationContextAccessor` | Uses `AsyncLocal<T>` for per-request `MigrationContext` isolation (registered as Scoped) |

### `IMigrationContextFactory`
**Location**: `Raycoon.RayMigrator.Core/IMigrationContextFactory.cs`

**Purpose**: Factory for creating `MigrationContext` instances.

**Implementation**: `MigrationContextFactory` — creates a `MigrationContext` from `RayMigratorOptions`, product, environment, run mode, version, and optional parameters.

### `RayMigratorHostMode`
**Location**: `Raycoon.RayMigrator.Core/RayMigratorHostMode.cs`

**Purpose**: Enum distinguishing CLI mode (`Cli`) from API mode (`Api`). Used by `ServiceCollectionExtensions.AddRayMigratorServices()` to register the appropriate `IMigrationContextAccessor` implementation.

### Domain Models

**Location**: `Raycoon.RayMigrator.Core/Models/`

| Class | Purpose |
|-------|---------|
| `MigrationFileInfo` | Represents a parsed migration file ready for execution. Contains file metadata, TOML configuration, SQL blocks, computed hashes, and optional `UseCliToolAlias` override. |
| `MigrationRecord` | Represents a migration record from the repository database. Maps to the result of `Repository_Migration_Select` query. |

### File Sorting

**Location**: `Raycoon.RayMigrator.Core/CultureDependendSorting.cs`

| Class | Purpose |
|-------|---------|
| `CultureDependentSorting` | Culture-aware file sorting utility. Provides `GetAndSortFilesRecursive()` for recursively enumerating and sorting files by relative directory path and filename using a specified `CultureInfo`. Note: the main migration file discovery in `MigrationService.DiscoverAndPrepareMigrationFiles` sorts files directly via `StringComparer.OrdinalIgnoreCase` rather than through this class. |

### Recovery

**Location**: `Raycoon.RayMigrator.Core/Recovery/`

| Class | Purpose |
|-------|---------|
| `InterruptedMigrationInfo` | Contains information about an interrupted migration that can be resumed. Includes block progress tracking (`BlocksMigrated`, `BlocksTotal`, `NextBlockToResume`, `ProgressPercent`). |

### Template Types

**Location**: `Raycoon.RayMigrator.Core/Templates/`

| Class | Purpose |
|-------|---------|
| `Template` | Represents a loaded SQL template with `TemplateType`, `DatabaseType`, `Filename`, and `Content` properties. |
| `TemplateResponse` | Result of a template execution, containing `ResultCode` (int) and `ResultMessage` (string?). |
| `TemplateType` | Enum defining all SQL template types (18 values + Undefined). |

### `MigrationLoggingContext`
**Location**: `Raycoon.RayMigrator.Core/MigrationLoggingContext.cs`

**Purpose**: Static ambient context using `AsyncLocal<MigrationContext?>` for carrying `MigrationContext` through the async call chain. Used by the Serilog `MigrationContextEnricher` to add migration-specific properties to every log event without explicit parameter passing.

### Configuration Sources

**Location**: `Raycoon.RayMigrator.Core/Configuration/Sources/`

| Class | Purpose |
|-------|---------|
| `IOptionsSource` | Interface for loading `RayMigratorOptions` from different sources (JSON files or alternative stores). Defines `LoadAsync(product, environment)` returning `OptionsSourceResult`. |
| `OptionsSourceResult` | Result of configuration loading. Contains `RayMigratorConfigSection`, `PreBuiltOptions` (pre-built mode), `ReplacedEnvironmentVariables`, `HostConfiguration` (JSON mode), `ModeName`, and `ConfigFileDiagnostics`. |

### `EnvironmentResolver`
**Location**: `Raycoon.RayMigrator.Core/Configuration/EnvironmentResolver.cs`

**Purpose**: Static class that resolves the target environment from `RayMigratorConsoleOptions.Environment` and the `DOTNET_ENVIRONMENT` variable. Returns a tuple of `(environment, environmentOrigin, errorCode)`. Detects conflicts between CLI argument and environment variable (returns error code 2), and missing environment (returns error code 3).

### `SensitiveDataMasker`
**Location**: `Raycoon.RayMigrator.Core/Configuration/SensitiveDataMasker.cs`

**Purpose**: Centralized static class for masking sensitive data (connection strings, passwords) in log output. Maintains a `HashSet<string>` of registered sensitive values and replaces them with a mask string. Thread-safe via lock-based writes and snapshot-based reads. CLI mode uses global state via `Initialize()`. API mode supports per-request scoping via `BeginScope()` using `AsyncLocal`.

### Configuration Validation

Validation is split across two projects. The **shared rule catalog** lives in `Raycoon.RayMigrator.Validation` (WASM-safe, zero dependencies — consumed by both the engine and the Blazor ConfigWizard). The **engine-side glue** lives in `Raycoon.RayMigrator.Core/Configuration/Validation/`.

**Shared catalog** (`Raycoon.RayMigrator.Validation`):

| Class | Purpose |
|-------|---------|
| `RuleCatalog` | Static entry point. `RuleCatalog.RunAll(ValidationInput)` executes all rules and returns a `ValidationReport`. Fixed rule list — no reflection. |
| `Rules/*` (9 rule classes) | `AliasUniquenessRule`, `TargetGroupMigrationOrderRule`, `SemanticContradictionsRule`, `CliToolDefinitionsRule`, `CliToolReferencesRule`, `CliToolParametersRule`, `SchemaRule`, `ConnectionStringRule`, `DefaultCascadeRule`. Each owns one rule-ID category. All `internal sealed`. |
| `RuleIds` | String constants (`RULE_1_1` … `RULE_8_3`). See [Appendix: Validation Rules](../appendix/validation-rules.md) for the full catalog. |
| `Models/ValidationIssue`, `ValidationReport`, `ValidationSeverity`, `ValidationInput` (+ sub-types) | Immutable POCOs passed between the adapters and the rules. |
| `Helpers/CliToolPlaceholderExtractor` | Extracts user-facing placeholder keys from a CLI tool's `ArgumentTemplate` (excludes reserved `{FilePath}`). |
| `Helpers/ExitCodeExpressionValidator` | Sole source of truth for parsing `SuccessExitCodes` expressions. `ExitCodeMatcher.TryParse` in `Core` delegates here. |

**Engine glue** (`Raycoon.RayMigrator.Core/Configuration/Validation/`):

| Class | Purpose |
|-------|---------|
| `RayMigratorOptionsValidator` | `IValidateOptions<RayMigratorOptions>` — wires the engine into the shared catalog. Delegates to `OptionsValidationInputAdapter` to flatten `RayMigratorOptions` into `ValidationInput`, runs `RuleCatalog.RunAll`, emits warnings via static `Serilog.Log`, and fails `ValidateOptionsResult` on any error-severity issues. Registered in DI and triggered via `.ValidateOnStart()` in `DirectModePipeline`. |
| `OptionsValidationInputAdapter` | Static mapper `RayMigratorOptions` → `ValidationInput`. Assumes defaults have already been merged by `ProductDefaultsPostConfigureOptions`. |
| `ProductDefaultsPostConfigureOptions` | `IPostConfigureOptions<RayMigratorOptions>` — merges `ProductDefaults` into each `Product`, `TargetGroup`, and `Target`. Runs before `RayMigratorOptionsValidator`. Delegates to the public static `MergeDefaults(RayMigratorOptions)` method. |
| `SchemaNameValidator` | Static pipeline-level check that validates `SchemaName` against `DalSpecificProperties.SupportsSchema` after DAL discovery. Complements (does not overlap) the catalog's structural `RULE_4_1` / `RULE_4_2` by bringing in DAL-specific knowledge. |
| `ValidationContextPropertySetter` | Static helper for setting property values via `ValidationContext` during DataAnnotations validation. Used by custom attributes (e.g., `RayRangeIntAttribute`, `RayDirectoryExistsAttribute`) to apply default values. |

**Custom Validation Attributes** (`Raycoon.RayMigrator.Core/Configuration/Validation/RayAttributes/`):

| Attribute | Purpose |
|-----------|---------|
| `RayDirectoryExistsAttribute` | Validates that a directory path exists on disk |
| `RayConnectionStringAttribute` | Validates connection string format |
| `RayEncodingAttribute` | Validates that an encoding name is recognized by .NET |
| `RayRangeIntAttribute` | Validates that an integer value falls within a specified range |
| `RayEnumAttribute` | Validates that a string value matches a valid enum member |

### Environment Variable Replacement

**Location**: `Raycoon.RayMigrator.Core/Configuration/Replacer/`

| Class | Purpose |
|-------|---------|
| `EnvironmentVariableReplacer` | Static class that scans `IConfigurationSection` values for `{ENV:VariableName}` placeholders and replaces them with the corresponding environment variable values. Returns a list of `EnvironmentVariableWithMetadata` for logging and validation. |
| `EnvironmentVariableWithMetadata` | Tracks a single environment variable replacement: `Path`, `ConfigurationKey`, `ConfigurationValue`, `ConfigurationValueReplaced`, `EnvironmentVariableName`, and `EnvironmentVariableValue`. |

### Configuration Utilities

| Class | Location | Purpose |
|-------|----------|---------|
| `ConfigurationConstants` | `Core/Configuration/ConfigurationConstants.cs` | Static constants and compiled regexes for `{ENV:...}` and `{CFG:...}` placeholder syntax, allowed template variable names, sensitive data masking string, `DotNetEnvironmentVariableName`, and `DatabaseAccessLayersRootDirectory`. |
| `ConfigurationHelper` | `Core/Configuration/ConfigurationHelper.cs` | Static utility returning all `TemplateType` enum values (excluding `Undefined`). |
| `StringExtensions` | `Core/Extensions/StringExtensions.cs` | Extension methods for SHA-256 hashing, path parsing, and connection string utilities. |
| `RayMigratorOptionsExtensions` | `Core/Extensions/RayMigratorOptionsExtensions.cs` | Extension method (`ToDetailString`) on `IConfigurationSection` for formatted configuration output with optional sensitive data masking. |
| `MigrationRunModeExtensions` | `Core/Extensions/MigrationRunModeExtensions.cs` | Extension methods for the `MigrationRunMode` enum. |
| `EnumTypeExtensions` | `Core/Extensions/EnumTypeExtensions.cs` | Enum helper extensions. |
| `ExceptionExtensions` | `Core/Extensions/ExceptionExtensions.cs` | Exception formatting extensions (e.g., `GetExceptionDetails()`). |

## Infrastructure Layer Components

### `DatabaseLogWriter`
**Location**: `Raycoon.RayMigrator.Infrastructure/Logging/DatabaseLogWriter.cs`

**Purpose**: Writes log entries to a database table. Registered as Singleton in DI via factory.

### `DatabaseLoggerQueue`
**Location**: `Raycoon.RayMigrator.Infrastructure/Logging/DatabaseLoggerQueue.cs`

**Purpose**: Background queue for asynchronous database log writing using `BlockingCollection<Action>`. Supports deterministic shutdown via `Flush()` which signals completion and waits for all pending entries to be processed.

### `RayMigratorDatabaseSink`
**Location**: `Raycoon.RayMigrator.Infrastructure/Logging/RayMigratorDatabaseSink.cs`

**Purpose**: Custom Serilog sink that buffers log events for database writing.

### `MigrationContextEnricher`
**Location**: `Raycoon.RayMigrator.Infrastructure/Logging/MigrationContextEnricher.cs` (namespace: `Raycoon.RayMigrator.Infrastructure.Logging`)

**Purpose**: Serilog enricher that reads `MigrationLoggingContext.Current` and adds migration-specific properties to every log event. Console/file properties: `Environment`, `MigrationRunId`, `TargetGroupAlias`, `TargetAlias`, `MigrationFilename`, `MigrationFileId`, `MigrationBlockId`. Database sink properties: `RunModeId`, `ProductId`, `MigrationId`, `ReleaseVersion`, `FileName`, `FileOrderId`, `FileBlockId`.

### `ConnectionValidator`
**Location**: `Raycoon.RayMigrator.Infrastructure/ConnectionValidator.cs` (namespace: `Raycoon.RayMigrator.Core.Configuration.Validation`)

**Purpose**: Static class that validates database connections (repository, target databases, database logger) during startup. Post-validation after `IValidateOptions<RayMigratorOptions>` has been performed.

### `RepositoryExtensions`
**Location**: `Raycoon.RayMigrator.Infrastructure/RepositoryExtensions.cs` (namespace: `Raycoon.RayMigrator.Core.Extensions`)

**Purpose**: Extension methods for `RepositoryOptions`, e.g. `GetDalSettings()` to create `DalSettings` from repository configuration.

### `TemplateExecutor`
**Location**: `Raycoon.RayMigrator.Infrastructure/TemplateExecutor.cs` (namespace: `Raycoon.RayMigrator.Core`)

**Purpose**: Execute SQL templates with placeholder substitution.

All methods are **synchronous** (they internally call async DAL methods with `.GetAwaiter().GetResult()`).

**Key Methods**:
```csharp
void RepositoryCheckCreate()
void RepositoryProductCheckInsert()
void RepositoryEnvironmentCheckInsert()
void RepositoryMigrationRunInsert(string migrationRunSettingsJson)
void RepositoryMigrationRunUpdate(MigrationRunResult runResult)
List<Dictionary<string, object?>> RepositoryMigrationRunSelectOrphaned(int productId, string environment)
void RepositoryMigrationRunFixOrphaned(int migrationRunId)
int RepositoryMigrationFixOrphaned(int migrationRunId, MigrationStatus status)
InterruptedMigrationInfo? RepositoryMigrationGetInterrupted()
int RepositoryMigrationInsert(int existingMigrationId, string filename, string releaseVersion,
    string targetGroupAlias, string targetAlias, int fileOrderId, string fileUpHash,
    string? fileUpConfigHash, string fileUpBlocksHash, int fileUpBlocksTotal,
    string? fileUpConfigJson, bool migrateDownFileExists)
void RepositoryMigrationUpdate(int migrationId, MigrationStatus migrationStatus, int fileUpBlocksMigrated)
// Shared-connection atomic overload (used by ExecuteSqlBlocksAtomic):
void RepositoryMigrationUpdate(int migrationId, MigrationStatus migrationStatus, int fileUpBlocksMigrated,
    DbConnection connection, DbTransaction transaction, int repoCommandTimeoutInSeconds)
void RepositoryMigrationUpdateRollback(int migrationId, MigrationStatus migrationStatus, string fileDownHash, string? fileDownConfigHash, string fileDownBlocksHash, int fileDownBlocksMigrated, int fileDownBlocksTotal, string? fileDownConfigJson)
// Shared-connection atomic overload (used by ExecuteSqlBlocksAtomic):
void RepositoryMigrationUpdateRollback(int migrationId, MigrationStatus migrationStatus, string fileDownHash, string? fileDownConfigHash, string fileDownBlocksHash, int fileDownBlocksMigrated, int fileDownBlocksTotal, string? fileDownConfigJson, DbConnection connection, DbTransaction transaction, int repoCommandTimeoutInSeconds)
void RepositoryMigrationUpdateHash(int migrationId, string fileUpHash, string? fileUpConfigHash, string fileUpBlocksHash)
List<MigrationRecord> RepositoryMigrationSelect(MigrationRunMode? overrideRunMode = null)
List<Dictionary<string, object?>> RepositoryMigrationRunSelect(int limit)
TemplateResponse ExecuteScalarWithNegativeResultCodeException(Template template, IDal dal, DalSettings dalSettings, DalParameterList? dalParameterList, ILogger? logger = null, EventId? eventId = null)
```

**Constructor**: Takes `TemplateCache`, `ILogger<TemplateExecutor>`, `IMigrationContextAccessor`. Context access is **deferred** to first use (not constructor time). The `_repository` and `_repositoryDal` fields are lazily initialized via `InitializeFromContext()` on first access.

**Placeholder Resolution**:
1. `{ENV:VariableName}` → Replaced during `TemplateCache.Initialize()` (at startup); missing variables throw `ConfigurationValidationException`
2. `{CFG:PropertyName}` → Replaced per `GetRepositoryTemplate<T>()` / `GetTemplate<T>()` call; unreplaced placeholders throw `ConfigurationValidationException`
3. `@Parameter` → SQL parameter via `DalParameterList`

### `TemplateCache`
**Location**: `Raycoon.RayMigrator.Infrastructure/TemplateCache.cs` (namespace: `Raycoon.RayMigrator.Core.Templates`)

**Purpose**: Cache loaded SQL templates for performance. Loads all templates from `DataAccessLayers/{Type}/` at startup, resolves `{ENV:*}` placeholders, and provides typed accessors that resolve `{CFG:*}` placeholders via reflection-based property matching.

**Template Location**:
```csharp
Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataAccessLayers")
```

**Public Methods**:
```csharp
// Called on-demand when Products/Repository configuration becomes available (deferred validation mode)
public void ValidateConfigurationAgainstTemplateCache(RayMigratorOptions options)

// Returns all loaded DAL type names (e.g. "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite")
public List<string> GetAvailableDatabaseTypes()

// Get a template with {CFG:*} placeholders replaced via reflection on propertyClass
public Template GetTemplate<T>(string databaseType, TemplateType templateType, T propertyClass)

// Get a repository template; derives databaseType from propertyClass.DatabaseType
public Template GetRepositoryTemplate<T>(TemplateType templateType, T propertyClass) where T : RepositoryOptions

// Get a template's content string with {CFG:*} placeholders replaced
public string GetTemplateContent<T>(string databaseType, TemplateType templateType, T propertyClass)
```

## Database Layer Components

### Database.Common (NuGet Package)

**Location**: `Raycoon.RayMigrator.Database.Common/`

Public API classes shipped as a NuGet package for external DAL development:

| Class | Purpose |
|-------|---------|
| `IDal` | Interface defining all database operations (execute, reader, scalar, connection validation, parameter conversion) |
| `IDalSettings` | Interface for database-specific settings (`UseTransaction`, `DbCommandTimeoutInSeconds`, `MaxRetries`, `RetryDelayMs`) |
| `DalBase` | Abstract base class implementing `IDal` with virtual `IsTransient(Exception ex)` for DAL-specific transient error detection, protected `ExecuteWithRetryAsync`/`ExecuteWithRetry` helpers that invoke `IsTransient` via `RetryHelper`, virtual `TryGetDbSpecificSqlParameter<T>()`, and non-virtual `TryGetDbTypeForType()` |
| `DalSettings` | Default `IDalSettings` implementation |
| `DalParameter` | Represents a single SQL parameter with name, value, and type |
| `DalParameterList` | Collection of `DalParameter` instances with `AddParameter()` and `GetAllParameters()` methods |
| `DalSpecificProperties` | Database-specific properties (fields: `SqlBlockDelimiter`, `SqlMultiLineCommentStart`, `SqlMultiLineCommentEnd`, `SupportsSchema`, `SupportsTransactionalDdl`, `IdentifierQuoteStart`, `IdentifierQuoteEnd`, `DefaultSchema`, `FoldsUnquotedIdentifiersToLower`) |
| `DatabaseTypeAttribute` | `[DatabaseType("...")]` attribute for reflection-based DAL discovery by `DalFactory` |
| `RetryHelper` | Static retry logic that accepts a transient error predicate (`Func<Exception, (bool isTransient, string? errorCode)>`). Provides `ExecuteWithRetryAsync<T>`, `ExecuteWithRetryAsync` (void), and `ExecuteWithRetry<T>` (synchronous) overloads with linear backoff. Transient error detection is delegated to each DAL via `DalBase.IsTransient`. |
| `RetryExhaustedException` | Exception thrown when all retry attempts are exhausted. Contains `AttemptsMade` and `LastErrorCode` properties |

### `IDal` Interface
**Location**: `Raycoon.RayMigrator.Database.Common/IDal.cs`

**Interface Definition**:
```csharp
public interface IDal
{
    string DatabaseType { get; }
    DalSpecificProperties DalSpecificProperties { get; }

    Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings);
    void CheckConnectionStringOrValidateConnection(bool validateConnection);
    bool TryGetDbTypeForType(Type type, out DbType dbType);
    bool TryGetDbSpecificSqlParameter<T>(DalParameterList dalParameterList, out List<T>? sqlParameterList) where T : class, IDbDataParameter, new();
    // Shared-connection methods for atomic operations (repository + target in one transaction):
    DbConnection CreateConnection();
    Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null);
    Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null);
}
```

### `DalFactory`
**Location**: `Raycoon.RayMigrator.Database/DalFactory.cs`

Static factory with dual-mode auto-discovery. Not registered in DI — called directly via `DalFactory.TryGetDal(databaseType, connectionString, out IDal? dal)`.

**Discovery modes**:
1. **DependencyContext**: Scans `DependencyContext.Default` (from deps.json) for runtime libraries whose names start with `Raycoon.RayMigrator.`, loads them via `Assembly.Load`, and discovers classes implementing `IDal` with `[DatabaseType]` attributes. Works with single-file publish.
2. **Filesystem**: Scans `DataAccessLayers/` subdirectories for DLLs containing `IDal` implementations with `[DatabaseType]` attributes (for external DAL plugins)

### Database Implementations

Each DAL is a separate project (not a subdirectory of the Database project):

| Database | Project | Implementation | Attribute |
|----------|---------|---------------|-----------|
| SQL Server | `Raycoon.RayMigrator.Database.SqlServer` | `DalSqlServer.cs` | `[DatabaseType("SqlServer")]` |
| PostgreSQL | `Raycoon.RayMigrator.Database.PostgreSQL` | `DalPostgreSql.cs` | `[DatabaseType("PostgreSQL")]` |
| MariaDB | `Raycoon.RayMigrator.Database.MariaDb` | `DalMariaDb.cs` | `[DatabaseType("MariaDb")]` |
| MySQL | `Raycoon.RayMigrator.Database.MySql` | `DalMySql.cs` | `[DatabaseType("MySql")]` |
| SQLite | `Raycoon.RayMigrator.Database.Sqlite` | `DalSqlite.cs` | `[DatabaseType("Sqlite")]` |

### Database Example Project

**Location**: `Raycoon.RayMigrator.Database.Example/`

**Purpose**: Skeleton template project for external DAL development. Contains `DalExample.cs` and placeholder templates as a starting point for implementing new database providers. Transient error detection is provided via an `IsTransient()` override in `DalExample.cs`.

### SQL Templates

**Source Location**: Each DAL project's `Templates/` directory (e.g., `Raycoon.RayMigrator.Database.SqlServer/Templates/`). At build time, templates are copied to the output directory under `DataAccessLayers/{DatabaseType}/` (flat layout, no `Templates/` subdirectory at runtime).

18 templates per DAL, matching all non-Undefined `TemplateType` enum values. See [Template System](../03-database-layer/template-system.md) for the complete list.

## Core Layer: Enumerations and Constants

**Location**: `Raycoon.RayMigrator.Core/Configuration/Enums/`

| Enum | Values |
|------|--------|
| `MigrationCommand` | None (0), MigrateUp (1), MigrateDown (2), ValidateHash (3), UpdateHash (4), Info (5), Baseline (6), FixIssues (7) |
| `OperatingMode` | Standalone, ManagedLocal, ManagedRemote |
| `MigrationRunMode` | Undefined (0), Validate (10), Simulate (20), Migrate (100) |
| `MigrationOperation` | Undefined (0), Rollback (5), MigrateDown (50), MigrateUp (100) |
| `MigrationRunResult` | Undefined (0), Running (10), Error (90), Ok (100) |
| `MigrationStatus` | Undefined (0), Pending (10), Executing (20), Failed (30), NotMigrated (50), Migrated (100) |
| `MigrationErrorAction` | Undefined (0), Terminate (10), Rollback (20), RollbackErrorOnly (21), RollbackRelease (22), Ignore (30) |
| `RollbackErrorAction` | Undefined (0), Terminate (10), Ignore (30) |
| `TargetMigrationOrder` | Undefined (0), Simultaneously (1), Successively (2) |
| `HashValidationScope` | Undefined (0), File (1), SqlBlocks (2), Disabled (3) |
| `FixIssues` | Undefined (0), All (1), OrphanedRuns (2) |
| `CliToolInputMode` | Undefined (0), File (1), Stdin (2) |

> **Note**: `MigrationEvent` resides in the same `Configuration/Enums/` directory but is a **class** (not an enum). It defines static `EventId` constants used for structured logging (e.g., `CommandLineParsing` = 10, `TemplateExecutionRepositoryCheckCreate` = 100, `RayMigratorServiceShutdown` = 1000).

**Location**: `Raycoon.RayMigrator.Core/Templates/`

| Enum | Values |
|------|--------|
| `TemplateType` | Undefined (0), DatabaseLogging_CheckCreate, DatabaseLogging_Insert, Repository_CheckCreate, Repository_Drop, Repository_MigrationRun_Insert, Repository_MigrationRun_Update, Repository_MigrationRun_SelectOrphaned, Repository_MigrationRun_FixOrphaned, Repository_Migration_FixOrphaned, Repository_Product_CheckInsert, Repository_Environment_CheckInsert, Repository_Migration_Insert, Repository_Migration_Update, Repository_Migration_UpdateHash, Repository_Migration_UpdateRollback, Repository_Migration_Select, Repository_Migration_GetInterrupted, Repository_MigrationRun_Select |

## Shared Layer Components

### `AssemblyInfoHelper`
**Location**: `Raycoon.RayMigrator.Shared/AssemblyInfoHelper.cs`

**Purpose**: Provides assembly version information shared across projects. Key methods: `GetRayMigratorVersion()`, `GetAsciiLogoLines(string version)`, `GetAsciiHeader()`.

> **Note**: The Console project has its own local `AssemblyInfoHelper` class (`Raycoon.RayMigrator.Console/AssemblyInfoHelper.cs`) with `GetAssemblyInfo()` and `GetRayMigratorVersion()` methods that delegate to `Shared.AssemblyInfoHelper`. This is used for startup banner display.

### Constants
**Location**: `Raycoon.RayMigrator.Shared/Constants/`

| Class | Purpose |
|-------|---------|
| `InternalConstants` | Internal string constants (e.g., `RayMigratorSectionName`) |
| `TemplateResultCode` | Catalog of negative result codes returned by SQL templates |

### Exceptions
**Location**: `Raycoon.RayMigrator.Shared/Exceptions/CustomExceptions.cs`

| Exception | Category | Purpose |
|-----------|----------|---------|
| `ApplicationStartupException` | Pre-Migration | Startup/initialization failures |
| `ConfigurationValidationException` | Pre-Migration | Configuration parsing/validation errors |
| `TemplateExecutionException` | Template | SQL template execution errors |
| `TemplateResultException` | Template | Negative result codes from template execution |
| `UndefinedTemplateResultException` | Template | Unknown negative result codes from customized templates (extends `TemplateResultException`) |
| `MigrationHashValidationException` | Migration-Run | Hash mismatch on migration files |
| `MigrationFileParsingException` | Migration-Run | TOML metadata or file parsing errors |
| `MigrationExecutionException` | Migration-Run | SQL execution errors during migration |
| `CliToolExecutionException` | Migration-Run | External CLI tool execution errors (extends `MigrationExecutionException`). Properties: `ExecutablePath`, `ExitCode` |
| `CliToolTimeoutException` | Migration-Run | CLI tool timeout (extends `CliToolExecutionException`). Property: `TimeoutSeconds` |
| `RayMigratorInternalException` | Migration-Run | Unexpected internal errors |
| `NotYetImplementedException` | Planned | Features not yet implemented |
| `DatabaseParameterException` | Database | Database parameter conversion failures |
| `MigrationAlreadyRunningException` | Database | Concurrent migration lock conflict |
| `MigrationRecoveryException` | Database | Errors during recovery operations |
| `DatabaseTransientException` | Database | Transient DB errors after retry exhaustion |

## Related Documentation

- [Overview](overview.md) - High-level architecture
- [Data Flow](data-flow.md) - How components interact
- [Patterns](patterns.md) - Implementation patterns
