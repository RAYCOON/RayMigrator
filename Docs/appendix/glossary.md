# Glossary

Definitions of terms and concepts used in RayMigrator.

## A

### Alias
A unique identifier string used to reference entities in configuration. Products, Target Groups, and Targets all have aliases that must match `^(?=.{1,50}$)[\p{L}\p{N}_]+$`: Unicode letters (`\p{L}`), Unicode numeric characters (`\p{N}`), and underscores only, 1–50 characters maximum. CLI Tools use a slightly broader alias pattern that additionally allows hyphens: `^(?=.{1,50}$)[\p{L}\p{N}_\-]+$`.

### AllowOutOfOrder
A CLI option (`--allow-out-of-order` / `-ooo`) for the `migrate-up` command that permits execution of migration files that were added after previously executed files. When enabled, RayMigrator will not skip files that sort before the latest executed migration. Available as `RayMigratorConsoleOptions.AllowOutOfOrder`.

### ApplicationStartupException
An exception thrown when RayMigrator encounters a fatal error during startup, before migration execution begins. Common causes include failure to create the host application, inability to initialize the DatabaseLogWriter, or DAL instance creation failures. Defined in `CustomExceptions.cs` in the Shared project.

### Auto-Fix Orphaned Runs
An automatic recovery mechanism in `MigrationService` that detects and fixes orphaned migration runs when a new migration encounters a parallel-run conflict. Orphaned runs older than 10 minutes (`AutoFixOrphanedRunsThresholdMinutes`) are automatically marked as Error, allowing the new migration to proceed. For manual control, use the `fix` CLI command with the `--older-than` option.

### Atomic Shared Connection
An automatic optimization that activates when the Repository and a migration Target share the same ConnectionString (same physical database). RayMigrator wraps all SQL blocks of a migration file and the corresponding repository status updates in a single database transaction, guaranteeing atomic commit or rollback. Guard conditions: `UseTransaction = true`, `MigrationErrorAction != Ignore`, same `DatabaseType`, and identical `ConnectionString`. When conditions are not met, the standard per-block-connection behavior applies. See [Atomic Shared Connection](../02-core-concepts/error-handling.md#atomic-shared-connection) and [Atomic Shared Connection Execution](../04-service-layer/migration-service.md#atomic-shared-connection-execution).

## B

### Block
A discrete unit of SQL statements within a migration file, separated by delimiters (e.g., `GO` in SQL Server). Each block executes as a separate command.

### Block-Level Tracking
The mechanism by which RayMigrator tracks progress within a migration file at the block level. Enables resume from partial execution after failures. See **Resilience**.

## C

### CLI Tool Executor
A service (`CliToolExecutor`, implementing `ICliToolExecutor`) that executes external SQL CLI tools (e.g., sqlcmd, psql, mysql, mariadb, sqlite3) as an alternative to DAL-based SQL execution. Uses `System.Diagnostics.Process` to launch the tool, supports both file-path and stdin input modes, evaluates exit codes against configurable success/error code lists, and enforces a configurable timeout. Defined in `CliToolExecutor.cs` in the Services project. See also **CliToolOptions**, **UseCliToolAlias**.

### CliToolExecutionException
An exception thrown when an external CLI tool execution fails (process start failure, unexpected exit code). Extends `MigrationExecutionException`. Properties include `ExecutablePath` (the CLI tool that was invoked) and `ExitCode` (the exit code, if the process started successfully). Carries a constant `AbortMessage` prefix. Defined in `CustomExceptions.cs` in the Shared project. See also **CliToolTimeoutException**.

### CliToolInputMode
An enum that determines how the migration SQL file is passed to the external CLI tool (`CliToolInputMode` enum):
- **Undefined** (0): Falls back to `File` behavior at runtime.
- **File** (1): The file path is passed as a command-line argument via the `{FilePath}` placeholder in `ArgumentTemplate`. Used by tools like sqlcmd (`-i`), psql (`-f`), sqlite3 (`-init`).
- **Stdin** (2): The file content is piped to the process via standard input. Used by tools like mysql and mariadb that read SQL from stdin.

Defined in `CliToolInputMode.cs` in the Core project.

### CliToolOptions
A configuration class defining an external CLI tool that can execute migration SQL files instead of the built-in DAL. Properties include `Alias` (unique name, letters/numbers/underscores/hyphens, max 50 chars), `ExecutablePath` (path to the executable), `ArgumentTemplate` (command-line template with `{FilePath}` and custom placeholders resolved from `CliToolParameters`), `InputMode` (`File` or `Stdin`, default `File`), `SuccessExitCodes` (string array with range notation support, default `["0"]`), and `CliToolTimeoutInSeconds` (default 120). Any exit code not matched by `SuccessExitCodes` is treated as failure. Defined at the `RayMigrator.CliTools[]` root level in configuration. Defined in `RayMigratorOptions.cs` in the Core project. See also **UseCliToolAlias**, **CliToolParameters**, **ExitCodeMatcher**.

### CliToolParameters
A `Dictionary<string, string>` on `TargetOptions` that provides key-value pairs for placeholder substitution in the CLI tool's `ArgumentTemplate`. Values support `{ENV:VAR}` replacement (resolved at configuration load time). Example: `{"Server": "localhost", "User": "sa", "Password": "{ENV:SA_PASSWORD}", "Database": "mydb"}`. Defined in `RayMigratorOptions.cs` in the Core project. See also **CliToolOptions**, **UseCliToolAlias**.

### CliToolTimeoutException
An exception thrown when a CLI tool execution exceeds the configured timeout. Extends `CliToolExecutionException`. Properties include `TimeoutSeconds` (the configured timeout that was exceeded). Defined in `CustomExceptions.cs` in the Shared project.

### ConfigDir
A global CLI option (`--config-dir` / `-cd`) that overrides the directory where RayMigrator searches for configuration files (`appsettings.json`, `appsettings.{Environment}.json`, `appsettings.{Product}.json`, and `appsettings.{Product}.{Environment}.json`). When omitted, the current working directory is used. The value is resolved to an absolute path at parse time and supports `{ENV:VARIABLE_NAME}` substitution. Available as `RayMigratorConsoleOptions.ConfigDir`. Defined in `CommandLineConfiguration.cs` in the Core project.

### Connection String
A formatted string containing all information needed to connect to a database, including server address, credentials, and options.

### ConfigurationValidationException
An exception thrown when configuration validation fails during startup. Common causes include unresolved `{ENV:}` placeholders, invalid `DatabaseType` values, missing required settings, or schema name validation failures. Defined in `CustomExceptions.cs` in the Shared project.

### Context
See **Migration Context**.

## D

### DAL (Data Access Layer)
A component that provides database-specific implementation for connecting to and executing SQL against a particular database system (SQL Server, PostgreSQL, MariaDB, MySQL, SQLite, etc.). Each DAL is a separate project/assembly that implements the `IDal` interface and is discovered at runtime by the **DAL Factory**.

### DAL Factory
A static class (`DalFactory`) that discovers and instantiates DAL implementations at runtime using **dual-mode discovery**: first, it uses `DependencyContext.Default` (reads from `deps.json`) to discover built-in DAL assemblies within the `Raycoon.RayMigrator.*` namespace; then it performs filesystem scanning of `DataAccessLayers/` subdirectories for external DAL plugin DLLs. Both modes scan for classes implementing `IDal` with a `[DatabaseType]` attribute. Uses reflection-based instantiation via `Activator.CreateInstance`. See [DAL Architecture](../03-database-layer/dal-architecture.md#factory-resolution-plugin-discovery) for details.

### DAL Plugin
A self-contained database provider assembly that implements the `IDal` interface. Plugins typically extend `DalBase` (in `Database.Common`), which provides retry orchestration via `ExecuteWithRetryAsync` helpers and a virtual `IsTransient(Exception)` method for database-specific transient error detection. Each plugin is deployed to `DataAccessLayers/{DatabaseType}/` and is auto-discovered by the **DAL Factory**. Built-in plugins: `Database.SqlServer`, `Database.PostgreSQL`, `Database.MariaDb`, `Database.MySql`, `Database.Sqlite`. External plugins can be developed using **Database.Example** as a template.

### Database.Example
A skeleton template project (`Raycoon.RayMigrator.Database.Example`) for developing external DAL plugins. Contains placeholder implementations for all required methods and 18 SQL templates. Developers fork this project, add their ADO.NET driver, implement the methods, and deploy the output to `DataAccessLayers/{DatabaseType}/`.

### Database Type
An identifier for a specific database system, used to select the appropriate DAL via the `[DatabaseType]` attribute. Current values: `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite`.

### DatabaseParameterException
An exception thrown when database parameter conversion fails (e.g., type mismatch when converting C# values to database parameters). Includes a `ParameterCount` property. Defined in `CustomExceptions.cs` in the Shared project.

### DatabaseTransientException
An exception thrown when a transient database error occurs and all retry attempts have been exhausted. Properties include `AttemptsMade` (number of attempts made) and `LastErrorCode` (database-specific error code of the last failure; a `string?` supporting both numeric codes and SQLSTATE codes). Defined in `CustomExceptions.cs` in the Shared project. See also **RetryExhaustedException** (the lower-level exception thrown by `RetryHelper`).

### DDL (Data Definition Language)
SQL statements that define database structure: `CREATE`, `ALTER`, `DROP`, `TRUNCATE`.

### Delimiter
A marker that separates SQL blocks within a migration file. SQL Server uses `GO`, other databases typically use `;`.

### DML (Data Manipulation Language)
SQL statements that modify data: `INSERT`, `UPDATE`, `DELETE`, `SELECT`.

## E

### Environment
A deployment context such as Development, Staging, or Production. Each environment name observed by a MigrationRun is stored in the `Environment` repository table (lookup via `NameLower` with a unique index; original casing preserved in the `Name` column). The table is populated idempotently by the `Repository_Environment_CheckInsert` template.

### ExitCodeMatcher
A predicate-based class in `Raycoon.RayMigrator.Core.Configuration.Options` that evaluates process exit codes against a whitelist of expressions parsed from `CliToolOptions.SuccessExitCodes`. Supported expression forms: `"0"` (single integer), `"1..5"` (closed range, inclusive), `"10.."` (open-ended up, >= 10), `"..-1"` (open-ended down, <= -1). `IsMatch(int exitCode)` returns true if any predicate matches. The default instance accepts only exit code 0. `TryParse(string[]?, out ExitCodeMatcher, out string?)` builds a matcher from a string array and returns false with an error message if any expression is invalid. `ToString()` returns a human-readable representation such as `[0, 1..5]`.

### Environment Resolver
A static utility class (`EnvironmentResolver`) that resolves the target environment from the `--environment` / `-env` CLI argument and the `DOTNET_ENVIRONMENT` environment variable. If both are set to different values, it reports a conflict and terminates. If neither is set, it reports an error and terminates. Shared between CLI modes. Defined in `EnvironmentResolver.cs` in the Core project.

### Exclusive Run
A migration run that holds exclusive access to migrate a product. Only one migration process can run for a given product at any time, enforced at the database level via status checks.

### Execution Mode
See **Target Migration Order**.

## F

### File Hash
A SHA-256 hash of the entire migration file content, used for integrity validation.

### Fix Issues Scope
The scope of repository cleanup performed by the `fix` CLI command (`FixIssues` enum):
- **Undefined** (0): Invalid value
- **All** (1): Fix all known problems in the repository
- **OrphanedRuns** (2): Fix orphaned MigrationRun entries (process crashed while Running)

## H

### Hash Validation
The process of comparing current file hashes against stored values to detect unauthorized modifications. Supports File-level and SqlBlocks-level validation. See [Hash Validation](../02-core-concepts/hash-validation.md).

### Hash Validation Scope
Configuration that determines what is validated (`HashValidationScope` enum):
- **Undefined** (0): Invalid value
- **File** (1): Validates entire file content including TOML metadata
- **SqlBlocks** (2): Validates only SQL block content, allowing TOML changes
- **Disabled** (3): No hash validation performed

## I

### Idempotent
A migration that produces the same result regardless of how many times it's executed. Achieved using `IF NOT EXISTS` checks.

### IMigrationContextAccessor
An interface (`IMigrationContextAccessor`) that provides access to the current `MigrationContext`. Implementations vary by hosting model:
- **`SingletonMigrationContextAccessor`**: Used in CLI mode; holds a single shared context.
- **`AsyncLocalMigrationContextAccessor`**: Used in API mode; provides per-request context isolation via `AsyncLocal<T>`.

Registered via `RayMigratorHostMode` during DI setup. Defined in `IMigrationContextAccessor.cs` in the Core project. See also **RayMigratorHostMode**.

### InterruptedMigrationInfo
A data class containing details about an interrupted migration that can be resumed. Properties include `MigrationRecordId`, `MigrationRunId`, `ReleaseVersion`, `Filename`, `BlocksMigrated`, `BlocksTotal`, `Environment`, `EnvironmentId`, `TargetGroupAlias`, `TargetAlias`. Computed properties: `NextBlockToResume` (1-based next block number) and `ProgressPercent` (completion percentage). Used by `RepositoryMigrationGetInterrupted()` for block-level recovery. Defined in `InterruptedMigrationInfo.cs` in the Core project (`Recovery/`).

## L

### Logging Database
A database structure that stores detailed log entries for migration operations, separate from the main repository.

## M

### Migration
The process of applying database changes, or a single file containing such changes.

### MigrationAlreadyRunningException
An exception thrown when attempting to start a migration while another migration is already running for the same product. Properties include `ProductId` and `ExistingMigrationRunId`. Defined in `CustomExceptions.cs` in the Shared project. See also **Exclusive Run**.

### Migration Command
The CLI operation to execute (`MigrationCommand` enum):
- **None** (0): No command specified
- **MigrateUp** (1): Apply pending migrations forward
- **MigrateDown** (2): Rollback to a previous version
- **ValidateHash** (3): Verify migration file integrity
- **UpdateHash** (4): Update repository hashes after approved changes
- **Info** (5): Display migration status information
- **Baseline** (6): Mark an existing database as migrated without executing SQL
- **FixIssues** (7): Fix repository inconsistencies such as orphaned runs

### Migration Context
A data structure containing all information about a migration operation in progress, including product, environment, targets, and state.

### MigrationExecutionException
An exception thrown when an error occurs during the execution of a migration SQL block. Defined in `CustomExceptions.cs` in the Shared project.

### MigrationFileParsingException
An exception thrown when a migration file cannot be parsed, for example due to invalid TOML metadata or encoding issues. Includes an optional `ErrorCode` property for classification. Defined in `CustomExceptions.cs` in the Shared project.

### Migration Event
A class (`MigrationEvent`) defining structured `EventId` constants for logging throughout the migration lifecycle. Events are organized into categories:
- **Unspecified** (0): UnspecifiedEvent
- **Application Startup** (10-80): CommandLineParsing (10), EnvironmentVariableReplacement (20), CreateDatabaseLogger (31), ValidateRayMigratorOptions (40), CreateApplicationHost (50), InitializeDalSpecificProperties (60), ValidateConnectionStrings (70), RayMigratorServiceStart (80)
- **Template Execution - Repository Operations** (100-121): TemplateExecutionRepositoryCheckCreate (100), TemplateExecutionRepositoryMigrationRunInsert (110), TemplateExecutionRepositoryMigrationRunUpdate (111), TemplateExecutionRepositoryMigrationRunSelectOrphaned (112), TemplateExecutionRepositoryMigrationRunFixOrphaned (113), TemplateExecutionRepositoryMigrationFixOrphaned (114), TemplateExecutionRepositoryProductCheckInsert (120), TemplateExecutionRepositoryEnvironmentCheckInsert (121)
- **Template Execution - Migration Operations** (130-136): TemplateExecutionRepositoryMigrationInsert (130), TemplateExecutionRepositoryMigrationUpdate (131), TemplateExecutionRepositoryMigrationGetInterrupted (132), TemplateExecutionRepositoryMigrationUpdateRollback (133), TemplateExecutionRepositoryMigrationSelect (134), TemplateExecutionRepositoryMigrationUpdateHash (135), TemplateExecutionRepositoryMigrationRunSelect (136)
- **Application Shutdown** (1000): RayMigratorServiceShutdown

### Migration Error Action
Configuration that determines behavior when a migration fails (`MigrationErrorAction` enum):
- **Undefined** (0): Invalid value
- **Terminate** (10): Stop immediately, no rollback
- **Rollback** (20): Undo all migrations in current run
- **RollbackErrorOnly** (21): Undo only the failed migration
- **RollbackRelease** (22): Undo all migrations from the release that caused the error
- **Ignore** (30): Ignore the error and continue execution with the next file

### Migration File
A SQL file containing database changes to be applied, optionally with TOML metadata.

### MigrationHashValidationException
An exception thrown when hash validation detects a mismatch between the stored hash and the current file hash. Defined in `CustomExceptions.cs` in the Shared project. See also **Hash Validation**.

### Migration Operation
The type of migration operation currently being performed (`MigrationOperation` enum):
- **Undefined** (0): Invalid value
- **Rollback** (5): Performing rollback of previously applied migrations
- **MigrateDown** (50): Performing down-migration
- **MigrateUp** (100): Performing up-migration

### Target Migration Order
How migrations execute across multiple targets (`TargetMigrationOrder` enum):
- **Undefined** (0): Invalid value
- **Simultaneously** (1): One migration at a time across all targets
- **Successively** (2): All migrations on one target before moving to next

### MigrationRecoveryException
An exception thrown during migration recovery operations (e.g., fixing orphaned runs or resuming interrupted migrations). Properties include `MigrationRunId` (nullable `int?`) and `MigrationRecordId` (nullable `int?`). Defined in `CustomExceptions.cs` in the Shared project.

### Migration Run
A single execution of the migration process for a product/environment combination.

### Migration Run Result
The outcome of a migration run (`MigrationRunResult` enum):
- **Undefined** (0): Value has not been set
- **Running** (10): Migration process is currently running
- **Error** (90): Migration stopped due to errors
- **Ok** (100): Migration successfully executed and finished

### Migration Status
The status of a migration per target (`MigrationStatus` enum):
- **Undefined** (0): Invalid value
- **Pending** (10): Migration record created, execution has not started yet
- **Executing** (20): SQL blocks are currently being executed
- **Failed** (30): Execution failed, database state is unclear
- **NotMigrated** (50): File is not deployed on target database (rolled back or never executed)
- **Migrated** (100): File is successfully deployed on target database

### migsettings File
A control file (`migsettings.txt` or `migsettings.{Environment}.txt`) that sets default TOML settings for all migrations in a directory and its subdirectories.

## N

### NotYetImplementedException
An exception thrown when a feature is planned but not yet implemented. Carries a constant `AbortMessage` prefix and includes the feature name in the message. Defined in `CustomExceptions.cs` in the Shared project.

## O

### Operating Mode
The mode in which RayMigrator runs, determined by bootstrap configuration (`OperatingMode` enum):
- **Standalone**: All configuration loaded from JSON files (appsettings.json hierarchy). No Admin-DB, no API server. This is the default mode used by RayMigrator Engine.
- **ManagedLocal**: Configuration loaded from a local Admin-DB. Products, Environments, Targets, and Repository config come from the Admin-DB. Serilog configuration still read from appsettings.json. Implemented in RayMigrator Studio.
- **ManagedRemote**: CLI operates as a Thin Client, sending HTTP requests to a remote RayMigrator API server instead of accessing databases directly. Implemented in RayMigrator Studio.

### Options Pattern
A design pattern for accessing strongly-typed configuration through dependency injection.

### Orphaned Run
A MigrationRun that remains in "Running" status due to a process crash or unexpected termination. Requires manual intervention or automatic cleanup after a configurable timeout period. See **Resilience**.

### Orphaned Run Detection
`RepositoryMigrationRunSelectOrphaned()` returns orphaned MigrationRun data as `List<Dictionary<string, object?>>` (not a typed class). Each dictionary contains column values from the query result, including run ID, product, start time, and duration. Used for detecting stuck runs that need manual intervention or automatic cleanup.

## P

### Pipeline
The execution pipeline project (`Raycoon.RayMigrator.Pipeline`) that orchestrates the complete lifecycle of a migration run. Contains `DirectModePipeline` (the unified entry point for Standalone and Managed modes), `RayMigratorService` (the top-level service that dispatches to the appropriate migration command), `JsonOptionsSource` (loads configuration from JSON files), and `SerilogFactory` (creates Serilog logger instances). Configuration source abstractions (`IOptionsSource`, `OptionsSourceResult`) are defined in the Core project (`Core/Configuration/Sources/`). Extracted from the Console project to enable reuse across different host contexts.

### Placeholder
A token replaced with actual values at runtime. Used in SQL templates and CLI tool argument templates:
- `{CFG:PropertyName}` - Configuration values (SQL templates)
- `{ENV:VariableName}` - Environment variables (configuration values and SQL migration files)
- `@Parameter` - SQL parameters (SQL templates)
- `{FilePath}` - Migration file path (CLI tool `ArgumentTemplate`, when `InputMode=File`)
- Custom placeholders (e.g., `{Server}`, `{Database}`) - Resolved from `CliToolParameters` on the Target (CLI tool `ArgumentTemplate`)

### Product
A logical grouping representing a software application with its databases. Contains Target Groups. Each Product name used by a MigrationRun is stored in the `Product` repository table (lookup via `NameLower` with a unique index; original casing preserved in the `Name` column). The table is populated idempotently by the `Repository_Product_CheckInsert` template.

### Product Alias
The unique identifier for a product, used in configuration and CLI commands.

## R

### Release Version
A directory name representing a version of migrations (e.g., "Release 1.0", "v2.0.0"). Migrations are organized by release.

### Repository
The database structure that tracks migration state, history, and metadata. Contains 11 tables (`MigratorMeta`, `Product`, `Environment`, `MigrationRun`, `MigrationRunMeta`, `MigrationRecord`, `MigrationRecordHistory`, plus four lookups). See [Repository Schema](../03-database-layer/repository-schema.md).

### Rollback
The process of reversing previously applied migrations using rollback files.

### Rollback Error Action
Configuration that determines behavior when a rollback operation encounters an error (`RollbackErrorAction` enum). Applies both during explicit `migrate-down` execution and during error recovery rollback triggered by `MigrationErrorAction` (Rollback, RollbackErrorOnly, or RollbackRelease) in `migrate-up`. See [Error Handling](../02-core-concepts/error-handling.md).
- **Undefined** (0): Invalid value
- **Terminate** (10): Stop the rollback chain immediately (default)
- **Ignore** (30): Skip the failed rollback file (mark it as Failed) and continue the rollback chain with the next file

### Rollback File
A file containing SQL to undo a migration, named with `.rollback.sql` extension (configurable).

### RunAlways
TOML setting that determines whether a migration file is executed on every migration run, even if it has been applied before. Files with `RunAlways = true` are re-executed each time. Default: `false`.

### Run Mode
How migrations execute (`MigrationRunMode` enum):
- **Undefined** (0): Invalid value
- **Validate** (10): Validate configuration and all migration files without connecting to databases
- **Simulate** (20): Validate, check DB connectivity, and read repository records without writing repository records or executing SQL against target databases
- **Migrate** (100): Validate configuration and perform actual migrations against target databases

### Resilience
The ability of RayMigrator to handle failures gracefully and recover from interruptions. Includes transient error retry, block-level tracking, and orphaned run detection. See [Resilience documentation](../02-core-concepts/resilience.md).

### Retry (Transient Error)
Automatic re-attempt of database operations that fail due to temporary conditions such as network timeouts, connection resets, or server throttling. Configurable per target via `DbCommandMaxRetries` and `DbCommandWaitTimeInMsBeforeRetry` settings. Uses linear backoff (delay multiplied by attempt number).

### RetryExhaustedException
An exception thrown when all retry attempts for a transient database error have been exhausted. Properties include `AttemptsMade` (number of attempts made) and `LastErrorCode` (database-specific error code of the last failure; a `string?` to support both numeric codes such as SQL Server error numbers and SQLSTATE codes such as PostgreSQL `08000`). Defined in `RetryHelper.cs` in the Database.Common project.

### RayMigratorHostMode
An enum (`RayMigratorHostMode`) defining the hosting model for RayMigrator's dependency injection registration:
- **Cli**: Short-lived CLI process; uses `SingletonMigrationContextAccessor` for a single shared `MigrationContext`.
- **Api**: Long-lived API server; uses `AsyncLocalMigrationContextAccessor` for per-request `MigrationContext` isolation.

Defined in `RayMigratorHostMode.cs` in the Core project. See also **IMigrationContextAccessor**.

### RayMigratorInternalException
An exception thrown when RayMigrator encounters an unexpected internal error that is not caused by user configuration or external factors. Carries a constant `AbortMessage` prefix. Defined in `CustomExceptions.cs` in the Shared project.

## S

### Schema
A logical grouping of database objects. RayMigrator can create its repository tables in a custom schema.

### Simulate Mode
See **Run Mode**.

### SQL Block
See **Block**.

### State Machine
The model governing valid transitions between migration statuses (Pending, Executing, Failed, NotMigrated, Migrated).

### StopRollbackOnMissingRollbackFile
A configuration setting and CLI option that controls whether an error-recovery rollback chain stops or continues when a rollback file is missing. Only relevant when `RequireRollbackFile = false`. Has no effect on explicit `migrate-down` execution.

- **CLI option**: `--stop-rollback-on-missing-rollback-file` / `-sromrf` (available on `migrate-up` command)
- **Configuration**: `bool?` property on `ProductDefaultOptions`, `ProductOptions`, `TargetGroupDefaultOptions`, `TargetGroupOptions`
- **Default**: `true` (stop the rollback chain when a rollback file is missing)
- **`true`**: Stop the rollback chain immediately when a rollback file is missing
- **`false`**: Skip the missing rollback file and continue the rollback chain with the next file

The runtime effective value is resolved as: CLI option (highest priority) → `TargetGroup.StopRollbackOnMissingRollbackFile` → `Product.StopRollbackOnMissingRollbackFile` → hardcoded default `true`. At startup, `ProductDefaultsPostConfigureOptions` pre-populates `Product` and `TargetGroup` values from `ProductDefaults` and `ProductDefaults.TargetGroupDefaults` respectively, so those defaults flow in via the appsettings cascade before runtime. While the setting can also appear in migsettings files and per-file TOML (and is parsed there), those values are not consulted at rollback execution time. Stored as `RayMigratorConsoleOptions.StopRollbackOnMissingRollbackFile` (CLI override). Defined in `RayMigratorOptions.cs` in the Core project. See also **RollbackErrorAction**, **RequireRollbackFile**.

## T

### Table Base Name
A prefix added to all repository table names (e.g., "Ray" produces "RayMigrationRecord" instead of "MigrationRecord").

### Target
A single database connection that receives migrations. Multiple targets can exist within a Target Group.

### Target Alias
The unique identifier for a target within its Target Group.

### Target Group
A collection of targets that receive the same migrations. All targets in a group must use the same database type.

### Target Group Alias
The unique identifier for a target group within a product. Must match directory names in the migration files structure.

### Target Group Migration Order
An override that defines the explicit processing order of TargetGroups within a release, available for `migrate-up` and `baseline` commands. When not set, TargetGroups are processed in the order they appear in configuration. Supports a four-level priority chain (highest first):
1. `--target-group-migration-order` / `-tgmo` CLI option (comma-separated aliases, e.g. `"Frontend,Backend"`)
2. `TargetGroupMigrationOrder` key in a `migsettings` file for the release directory
3. `TargetGroupMigrationOrder` property on `ProductOptions` in appsettings (comma-separated string)
4. Configuration array order (default)

Stored as `RayMigratorConsoleOptions.TargetGroupMigrationOrder` (CLI) and `MigSettingsEntry.TargetGroupMigrationOrder` (migsettings). Resolution is performed per-release by `ResolveTargetGroupMigrationOrder()` in `MigrationService`.

### Target Group Filter
The `--target-group` / `-tg` CLI option, available on `migrate-up`, `migrate-down`, `validate-hash`, `update-hash`, and `baseline` commands. Accepts one or more target group aliases to restrict execution to specific target groups. When omitted, all target groups are processed. Stored as `RayMigratorConsoleOptions.TargetGroupAliases`.

### Template
A SQL file with placeholders that gets processed and executed by RayMigrator. Used for repository and logging schema creation.

### Template Result Code
A static class (`TemplateResultCode`) defining all known `ResultCode` values returned by SQL templates and C# backend error codes. Convention: negative values are SQL template errors, positive values are C# backend errors, and 0 means no error. Reserved ranges:
- `-1`: General/unclassified template error (legacy fallback)
- `-2`: Migration already running (parallel run prevention, `Repository_MigrationRun_Insert`)
- `-10..-19`: Repository health (`Repository_CheckCreate`)
- `-20..-29`: Product table (`Repository_Product_CheckInsert`)
- `-30..-39`: MigrationRun table (`Repository_MigrationRun_*`)
- `-40..-49`: Migration record (`Repository_MigrationRecord_*`)
- `-50..-59`: Environment table (`Repository_Environment_CheckInsert`)

Currently assigned codes:
- **-1**: `GeneralError`
- **-2**: `MigrationAlreadyRunning`
- **-10**: `RepositoryIncomplete` (wrong table count)
- **-11**: `RepositoryPartialWithoutVersionTable`
- **-12**: `RepositoryMultipleVersionEntries`
- **-20**: `ProductNameEmpty` (Product name is NULL or empty)
- **-30**: `MigrationRunNotFound`
- **-31**: `MigrationRunNotInRunningState` (FixOrphaned)
- **-40**: `MigrationNotFound`
- **-50**: `EnvironmentNameEmpty` (Environment name is NULL or empty)
- **1001**: `RequireRollbackFileValidationFailed`
- **1002**: `MigrationFileParsingFailed`
- **1003**: `ConfigurationValidationFailed`

`IsKnown(int)` can be used to check whether a negative code belongs to the known catalog (unknown negative codes from user-customized templates cause an `UndefinedTemplateResultException`). Defined in `TemplateResultCode.cs` in the Shared project.

### TemplateExecutionException
An exception thrown when an error occurs during the execution of a SQL template (repository operations, logging schema creation, etc.). Wraps the underlying database exception. Defined in `CustomExceptions.cs` in the Shared project.

### TemplateResultException
An exception thrown when a SQL template returns a negative `ResultCode`, indicating a domain-level error (e.g., repository incomplete, product name empty, migration run not found). Includes a `ResultCode` property. The subclass `UndefinedTemplateResultException` is thrown when the `ResultCode` is not in the known catalog. Defined in `CustomExceptions.cs` in the Shared project.

### Template Cache
A component (`TemplateCache`) that loads and caches SQL templates from the `DataAccessLayers/{DatabaseType}/` directories at startup (flat layout, no `Templates/` subdirectory). Validates that all required templates exist for every configured database type. Provides `GetAvailableDatabaseTypes()` and `ValidateConfigurationAgainstTemplateCache()` for configuration validation.

### Template Executor
The component that processes templates, resolves placeholders, and executes the resulting SQL.

### TOML
"Tom's Obvious, Minimal Language" - A configuration format used for migration file metadata. Similar to INI format but with more features.

### Transaction
A database operation that executes atomically - either completely succeeds or completely fails. Controlled by `UseTransaction` setting.

### Transient Error
A temporary database error that may succeed on retry, such as connection timeouts, network issues, or server throttling. Identified by specific error codes and handled automatically by the retry mechanism.

## U

### UseCliToolAlias
A configuration setting that specifies which external CLI tool to use for migration execution instead of the built-in DAL. References a `CliTools[].Alias` defined at the `RayMigrator` root level. Supports a full inheritance chain: `ProductDefaults` -> `Product` -> `TargetGroup` -> `Target` -> `migsettings` -> TOML file header. At each level, a non-null value overrides the parent. Null or empty means use the DAL (default behavior). Resolution at runtime: file-level (`UseCliToolAlias` from TOML or migsettings) takes priority over target-level (from the PostConfigure cascade). Defined on `ProductDefaultOptions`, `ProductOptions`, `TargetGroupOptions`, `TargetOptions`, and `MigrationFileInfo` in `RayMigratorOptions.cs` / `MigrationFileInfo.cs` in the Core project. See also **CliToolOptions**, **CliToolParameters**.

### UseTransaction
TOML setting that determines whether a migration runs within a database transaction. Default: `true`.

## V

### Validator
A component that checks migrations before or after execution, enforcing rules like naming conventions or SQL syntax.

## See Also

- [Architecture Overview](../01-architecture/overview.md)
- [Core Concepts](../02-core-concepts/migration-context.md)
- [Resilience and Recovery](../02-core-concepts/resilience.md)
- [Concurrency Control](../02-core-concepts/concurrency-control.md)
- [Configuration Reference](../06-configuration-reference/product-options.md)
- [CLI Tools Options](../06-configuration-reference/cli-tools-options.md)
- [External DAL Development](../09-extending/external-dal-development.md)
