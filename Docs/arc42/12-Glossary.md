# 12. Glossary

This chapter collects the terms that the preceding eleven chapters use
without defining them in place: the domain vocabulary of RayMigrator, the
enumerations and code families whose numeric values end up in the migration
repository (the RayMigrator bookkeeping database) or in the process exit
code, the configuration nodes, files and options an operator meets, and the
architecture and technology terms of the arc42 pages themselves. Definitions
are deliberately short and point to the chapter or `Docs/` page that explains
the mechanism. The
[appendix glossary](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/glossary.md)
in the repository remains the reference for class level entries (exception
types, model classes, event ids); this page is the architecture level subset
and every value on it was verified against the source tree of the 0.14.x
line. Spellings follow the terminology table of the arc42 authoring guide, so
a term is written here exactly as it appears in chapters 01 to 11.

## Domain terms

| Term | Definition | See also |
|------|------------|----------|
| alias | Name of a product, target group, target or CLI tool in configuration; matches `^(?=.{1,50}$)[\p{L}\p{N}_]+$` (CLI tool aliases also allow hyphens) and is unique case insensitively within its parent node. | [8.1 Domain Model](08-Crosscutting-Concepts.md#products-target-groups-targets-and-environments) |
| atomic shared connection | Execution path taken when repository and target share `DatabaseType` and a byte identical connection string, `UseTransaction` is on and the error action is not `Ignore`: all blocks of a file and the repository updates run in one transaction (`ExecuteSqlBlocksAtomic`). | [ADR-012](09-Architecture-Decisions.md#adr-012-atomic-shared-connection) |
| baseline | The `baseline` command: marks the migration files of an existing database as `Migrated` in the repository without executing SQL, for all releases or up to `--to-release`; the run is stamped `MigrationOperation.Baseline`. | [3.1 CLI commands](03-Context-and-Scope.md#cli-commands-as-the-business-interface) |
| block | A unit of SQL inside a migration file executed as one statement batch (`IDal.ExecuteNonQueryAsync`), in its own transaction when `UseTransaction` is `true`; the committed block count is persisted after every block. | [Block-Level Execution and Transactions](08-Crosscutting-Concepts.md#block-level-execution-and-transactions) |
| block delimiter | The engine specific token that `SplitSqlIntoBlocks` expects alone on a line: `GO` on SQL Server, `;` on PostgreSQL, MariaDB, MySQL and SQLite (`DalSpecificProperties.SqlBlockDelimiter`). Files routed to a CLI tool are not split. | [Block-Level Execution and Transactions](08-Crosscutting-Concepts.md#block-level-execution-and-transactions) |
| block level resume | Continuation of a `Failed` file from the first uncommitted block on the next run (`FindResumableBlock`), possible only while `FileUpBlocksHash` is unchanged. | [Runtime View 6.3](06-Runtime-View.md#63-migrate-up-the-primary-scenario) |
| CLI tool (external) | A vendor client such as `sqlcmd`, `psql`, `mysql`, `mariadb` or `sqlite3` (or a `docker exec` wrapper) configured under `CliTools` and selected by `UseCliToolAlias`; it executes whole migration files instead of the DAL, and only its exit code is evaluated. | [Runtime View 6.8](06-Runtime-View.md#68-external-cli-tool-execution) |
| Config Wizard | The web application `Raycoon.RayMigrator.ConfigWizard.Web` (Blazor WebAssembly) that generates and validates the `appsettings*.json` hierarchy in the browser; it never contacts the engine or a database. | [Building Block View 5.2.8](05-Building-Block-View.md#528-config-wizard-raycoonraymigratorconfigwizardcore-raycoonraymigratorconfigwizardweb) |
| DAL / DAL plugin | Database access layer implementation for one engine (`Raycoon.RayMigrator.Database.*`): a public class deriving from `DalBase`, annotated `[DatabaseType("...")]`, discovered by `DalFactory` together with its SQL templates. External plugins live under `DataAccessLayers/{Type}/`. | [5.2.6 Database layer](05-Building-Block-View.md#526-database-layer) |
| database logging | Optional writing of enriched log events into the `MigrationLog` table (event catalog `MigrationEvent`) through the Serilog sink `RayMigratorDatabaseSink`; active when the `DatabaseLogging` node exists and the command profile writes the log. | [Structured Logging](08-Crosscutting-Concepts.md#structured-logging-to-console-file-and-database) |
| engine | A database engine: SQL Server, PostgreSQL, MariaDB, MySQL, SQLite. Each ships as one DAL plugin and can host targets, the migration repository and the logging tables. | [Architecture Constraints](02-Architecture-Constraints.md#database-engines-and-adonet-providers) |
| environment | The deployment context name (for example `Production`) given by the required `--environment` option (checked against `DOTNET_ENVIRONMENT`); it selects `appsettings.{Environment}.json`, `migsettings.{Environment}.txt` and environment specific files and is registered in the repository table `Environment`. It is not a configuration node. | [Environments and profiles](08-Crosscutting-Concepts.md#environments-and-profiles) |
| environment specific file | A migration file named `{BaseName}.{Environment}.{Extension}`, included only when the suffix matches the environment; the TOML key `Environments` is the alternative inside a generic file. | [Migration files, releases and metadata](08-Crosscutting-Concepts.md#migration-files-releases-and-metadata) |
| exclusive run lock | The open `MigrationRun` row (`FinishedAt IS NULL`) per product and environment. `Repository_MigrationRun_Insert` refuses a second one with `ResultCode` `-2` inside the engine's own serialization; the run mode is not part of the predicate, and `Validate` and `Simulate` never hold it. | [Exclusive Run Lock and Orphaned Run Detection](08-Crosscutting-Concepts.md#exclusive-run-lock-and-orphaned-run-detection) |
| hash validation | Comparison of the SHA-256 hashes stored per migration record (`FileUpHash`, `FileUpConfigHash`, `FileUpBlocksHash`) with the file on disk, governed by `HashValidationScope`; performed by `validate-hash`, `update-hash` and during `migrate-up`. | [Hash Validation](08-Crosscutting-Concepts.md#hash-validation) |
| interrupted run | A run whose process died while a record was `Executing`; `RepositoryMigrationGetInterrupted` reports it as a warning at the next start and block level resume continues the file. | [Runtime View 6.2](06-Runtime-View.md#62-repository-bootstrap-first-contact-with-a-database) |
| migration file | A versioned SQL file (`*.{MigrationFilesExtension}`, default `sql`) with optional TOML metadata header, discovered from `MigrationFilesRootDirectory/{Release}/{TargetGroupAlias}/`, or flat under the release directory for a product with one target group. | [ADR-004](09-Architecture-Decisions.md#adr-004-plain-sql-migration-files-with-toml-header) |
| migration record | One `MigrationRecord` row per migration file and target: `MigrationStatus`, `FileOrderId`, up and down hashes, block counters and timestamps; terminal transitions are copied into `MigrationRecordHistory`. | [Migration repository schema](08-Crosscutting-Concepts.md#migration-repository-schema) |
| migration repository | The RayMigrator bookkeeping database: 11 tables created by `Repository_CheckCreate` on any of the five engines, configured under `Repository`; stores runs, records, hashes and the run lock. Not a Git repository. | [ADR-007](09-Architecture-Decisions.md#adr-007-repository-separate-from-targets-recreated-instead-of-upgraded) |
| migration run | One invocation of a state changing command for a product and environment, recorded as a `MigrationRun` row with `MigrationOperation`, `MigrationRunMode`, `MigrationRunResult`, timestamps and the masked settings snapshot in `MigrationRunMeta`. | [Runtime View 6.3](06-Runtime-View.md#63-migrate-up-the-primary-scenario) |
| `migsettings.txt` | Directory level defaults for migration files (`migsettings.txt` and `migsettings.{Environment}.txt`) at product, release and target group level, read as UTF-8; the TOML header of a file overrides them. | [migsettings files](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/migsettings-files.md) |
| orphaned run | A `MigrationRun` left in `Running` with `FinishedAt IS NULL` by a crashed process; closed as `Error` automatically when older than 10 minutes (`AutoFixOrphanedRunsThresholdMinutes`) or manually by `fix`. | [Runtime View 6.7](06-Runtime-View.md#67-concurrency-exclusive-run-lock-and-orphaned-run-recovery-fix) |
| out of order file | A pending file whose release is lower than the highest release already migrated on a target; `DetectOutOfOrderFiles` aborts the run unless `--allow-out-of-order` is given. | [Deterministic File Ordering](08-Crosscutting-Concepts.md#deterministic-file-ordering) |
| product | A configured software product whose databases are migrated; owns target groups and a `MigrationFilesRootDirectory`. Configuration node `Products`. | [8.1 Domain Model](08-Crosscutting-Concepts.md#products-target-groups-targets-and-environments) |
| product alias | The `Alias` of a product, named by `--product` (compared case sensitively) and registered by lowercase name in the repository table `Product`. | [8.1 Domain Model](08-Crosscutting-Concepts.md#products-target-groups-targets-and-environments) |
| RayMigrator Studio | A separate RAYCOON product that hosts the engine services behind an API; the `OperatingMode` values `ManagedLocal` and `ManagedRemote` and `RayMigratorHostMode.Api` exist as its contract and are not executed by the CLI. | [What RayMigrator deliberately is not](01-Introduction-and-Goals.md#what-raymigrator-deliberately-is-not) |
| release / release directory | A directory directly under `MigrationFilesRootDirectory` whose name is the release version; releases are ordered as strings (`StringComparer.OrdinalIgnoreCase`) and `--to-release` bounds a run. | [Deterministic File Ordering](08-Crosscutting-Concepts.md#deterministic-file-ordering) |
| rollback file | The rollback counterpart of a migration file, `{BaseName}.{MigrationRollbackFilesPreExtension}.{Extension}` (default `rollback`), executed by `migrate-down` and by the error recovery chain of `migrate-up`. | [Rollback Strategies](08-Crosscutting-Concepts.md#rollback-strategies) |
| run mode | The `MigrationRunMode` of a run (`Validate`, `Simulate`, `Migrate`), chosen with `--run-mode`; together with the command it yields the `CommandProfile` that decides whether targets are connected and repository or log are written. | [Execution modes](08-Crosscutting-Concepts.md#execution-modes) |
| target | One concrete database connection inside a target group (`Alias`, `ConnectionString`, timeout and retry settings, optional `CliToolParameters`). Configuration node `Targets`. | [8.1 Domain Model](08-Crosscutting-Concepts.md#products-target-groups-targets-and-environments) |
| target alias | The `Alias` of a target; referenced by the TOML key `Targets` and stored with every migration record. | [8.1 Domain Model](08-Crosscutting-Concepts.md#products-target-groups-targets-and-environments) |
| target group | A named group of targets inside a product that share one `DatabaseType` and one migration file directory per release. Configuration node `TargetGroups`. | [8.1 Domain Model](08-Crosscutting-Concepts.md#products-target-groups-targets-and-environments) |
| target group alias | The `Alias` of a target group; must match the directory name under the release directory, including case. | [ADR-009](09-Architecture-Decisions.md#adr-009-flat-directory-layout-auto-detection) |
| target group migration order | Ordered list of target group aliases per release, resolved from `--target-group-migration-order`, the release level `migsettings.txt`, `ProductOptions.TargetGroupMigrationOrder` or, by default, configuration order. | [Execution modes](08-Crosscutting-Concepts.md#execution-modes) |
| target migration order | `TargetMigrationOrder` of a target group: `FileByFile` (file, then target) or `TargetByTarget` (target, then file). Only the loop nesting changes; nothing runs in parallel. | [ADR-010](09-Architecture-Decisions.md#adr-010-no-parallel-database-execution) |
| template | SQL template used to create and maintain the repository and logging schema per engine: one `*.sql` file per `TemplateType` under `DataAccessLayers/{Type}/`, returning one scalar `ResultCode,ResultMessage`. | [Template-Driven Repository Schema](08-Crosscutting-Concepts.md#template-driven-repository-schema) |
| TOML metadata header | The optional `/* [RayMigrator] ... */` block at the top of a migration file with keys such as `Description`, `Environments`, `Targets`, `UseTransaction`, `RunAlways`, `RequireRollbackFile`, `MigrationErrorAction`, `RollbackErrorAction` and `UseCliToolAlias`; parsed by `MigrationService` without a TOML library. | [ADR-004](09-Architecture-Decisions.md#adr-004-plain-sql-migration-files-with-toml-header) |
| transient error | A provider error classified as temporary by the DAL's `IsTransient` override (timeouts, connection loss, deadlocks); `RetryHelper` repeats the operation with linear backoff up to `DbCommandMaxRetries`. | [Retry on Transient Errors](08-Crosscutting-Concepts.md#retry-on-transient-errors) |
| Validate / Simulate / Migrate | The three run modes: `Validate` parses and hashes without any database connection, `Simulate` connects and reads the repository but writes nothing and holds no lock, `Migrate` executes SQL and records the outcome. | [Validate and Simulate Run Modes](08-Crosscutting-Concepts.md#validate-and-simulate-run-modes) |

## Enumerations and codes

Numeric values are the ones stored in the repository lookup tables and
asserted by the tests; the CLI accepts the names case insensitively.

| Enum / code family | Values | Meaning | Defined in (project) |
|--------------------|--------|---------|----------------------|
| `MigrationCommand` | `None` 0, `MigrateUp` 1, `MigrateDown` 2, `ValidateHash` 3, `UpdateHash` 4, `Info` 5, `Baseline` 6, `FixIssues` 7 | The CLI subcommand (`migrate-up`, `migrate-down`, `validate-hash`, `update-hash`, `info`, `baseline`, `fix`) | `Raycoon.RayMigrator.Core` |
| `MigrationRunMode` | `Undefined` 0, `Validate` 10, `Simulate` 20, `Migrate` 100 | Run mode of a command (`--run-mode`) | `Raycoon.RayMigrator.Core` |
| `MigrationOperation` | `Undefined` 0, `Rollback` 5, `MigrateDown` 50, `MigrateUp` 100, `Baseline` 110 | Operation stamped on a run and its log events; `Rollback` marks the error recovery chain | `Raycoon.RayMigrator.Core` |
| `MigrationRunResult` | `Undefined` 0, `Running` 10, `PartialSuccess` 50, `Recovered` 80, `Error` 90, `Ok` 100 | Outcome of a `MigrationRun`; `Running` is the lock, `Recovered` a clean rollback chain, `PartialSuccess` an `Ignore` run with `Failed` files | `Raycoon.RayMigrator.Core` |
| `MigrationStatus` | `Undefined` 0, `Pending` 10, `Executing` 20, `Failed` 30, `NotMigrated` 50, `Migrated` 100 | State of one migration record (file and target) | `Raycoon.RayMigrator.Core` |
| `MigrationErrorAction` | `Undefined` 0, `Terminate` 10, `Rollback` 20, `RollbackErrorOnly` 21, `RollbackRelease` 22, `Ignore` 30 | What `migrate-up` does after a failed block: nothing, roll back all files of the run, the failed file only, the run's files of that release, or skip and continue | `Raycoon.RayMigrator.Core` |
| `RollbackErrorAction` | `Undefined` 0, `Terminate` 10, `Ignore` 30 | Whether a failing rollback block aborts the chain or is skipped with a warning | `Raycoon.RayMigrator.Core` |
| `HashValidationScope` | `Undefined` 0, `File` 1, `SqlBlocks` 2, `Disabled` 3 | Which stored hash is compared (`FileUpHash`, `FileUpBlocksHash`, none) per target group or `--scope` | `Raycoon.RayMigrator.Core` |
| `TargetMigrationOrder` | `Undefined` 0, `FileByFile` 1, `TargetByTarget` 2 | Loop nesting inside a target group; `Undefined` behaves as `TargetByTarget` | `Raycoon.RayMigrator.Core` |
| `FixScope` | `Undefined` 0, `All` 1, `OrphanedRuns` 2 | Scope of the `fix` command (`--scope all` or `orphanedruns`); both values run the orphaned run repair | `Raycoon.RayMigrator.Core` |
| `CliToolInputMode` | `Undefined` 0, `File` 1, `Stdin` 2 | How a migration file reaches an external CLI tool; required per `CliTools` entry (`RULE_3_11`) | `Raycoon.RayMigrator.Core` |
| `OperatingMode` | `Standalone`, `ManagedLocal`, `ManagedRemote` (no explicit numbers) | Bootstrap contract; the engine only executes `Standalone`, the managed modes belong to RayMigrator Studio | `Raycoon.RayMigrator.Core` |
| `RayMigratorHostMode` | `Cli`, `Api` (no explicit numbers) | Selects the `IMigrationContextAccessor` registration in `AddRayMigratorServices`: singleton for `Cli`, `AsyncLocal` per request for `Api` | `Raycoon.RayMigrator.Core` |
| `TemplateType` | `Undefined` plus 21 members: 19 `Repository_*` (`Repository_CheckCreate`, `Repository_Drop`, and `Repository_<Table>_<Operation>` for `Product`, `Environment`, `MigrationRun`, `MigrationRecord`, `MigrationRecordHistory`) plus `DatabaseLogging_CheckCreate` and `DatabaseLogging_Insert` | One SQL template file per member and engine; `TemplateCache` requires the full set per `DatabaseType` | `Raycoon.RayMigrator.Core` |
| Template `ResultCode` (`TemplateResultCode`) | `-1` `GeneralError`, `-2` `MigrationAlreadyRunning`, `-10` `RepositoryIncomplete`, `-11` `RepositoryPartialWithoutVersionTable`, `-12` `RepositoryMultipleVersionEntries`, `-20` `ProductNameEmpty`, `-30` `MigrationRunNotFound`, `-31` `MigrationRunNotInRunningState`, `-40` `MigrationNotFound`, `-50` `EnvironmentNameEmpty`; `1001` `RequireRollbackFileValidationFailed`, `1002` `MigrationFileParsingFailed`, `1003` `ConfigurationValidationFailed` | Negative codes are returned by templates and raise `TemplateResultException` (`-2` is the exclusive run lock refusal); other non-negative results are success values such as an inserted id; `1001` to `1003` are raised by the engine itself and surface as `OperationResult.ErrorCode` | `Raycoon.RayMigrator.Shared` |
| Process exit codes | `0` success, help or version; `1` command failure or `ApplicationStartupException`; `2` conflicting `--environment` and `DOTNET_ENVIRONMENT`; `3` no environment; `4` no `Serilog` node; `5` exception while parsing the command line (ordinary parse errors return System.CommandLine's own code); `100` any other exception, including `ConfigurationValidationException` | The only machine readable result of a run | `Raycoon.RayMigrator.Console` (`Program`), `Raycoon.RayMigrator.Core` (`EnvironmentResolver`), `Raycoon.RayMigrator.Pipeline` (`DirectModePipeline`, `RayMigratorService`) |
| Validation rule ids | `RULE_{group}_{n}`, `RULE_1_1` to `RULE_8_3`: group 1 alias uniqueness and target group order, 2 semantic contradictions, 3 CLI tools, 4 schema names, 7 connection string hygiene, 8 defaults cascade | Identify configuration errors and warnings at startup and in the Config Wizard | `Raycoon.RayMigrator.Validation` (`RuleIds`) |
| `MigrationEvent` event ids | `10` `CommandLineParsing` to `1000` `RayMigratorServiceShutdown`; `0` `UnspecifiedEvent` | `EventId` constants seeded into the `MigrationEvent` table and stored per `MigrationLog` row | `Raycoon.RayMigrator.Core` |

## Configuration nodes and files

| Name | Kind | Purpose | Reference |
|------|------|---------|-----------|
| `RayMigrator` | node (root) | The section bound to `RayMigratorOptions`; everything RayMigrator reads from JSON lives below it | [Configuration hierarchy](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/appsettings-hierarchy.md) |
| `Repository` | node | `DatabaseType`, `ConnectionString`, `SchemaName`, `TableBaseName`, timeout and retry settings of the migration repository | [Repository options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/repository-options.md) |
| `DatabaseLogging` | node (optional) | `DatabaseType`, `ConnectionString`, `MinimumLevel` of the logging tables; its presence switches database logging on | [Logging options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/logging-options.md) |
| `Serilog` | node (required) | Serilog configuration read by `SerilogFactory`; a missing node ends the start with exit code `4` | [Logging options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/logging-options.md) |
| `ProductDefaults` | node | Defaults inherited by every product (error actions, file extensions, encoding, `RequireRollbackFile`, `UseCliToolAlias`) | [Settings inheritance](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/settings-inheritance-overview.md) |
| `ProductDefaults.TargetGroupDefaults` | node | Defaults inherited by every target group (`TargetMigrationOrder`, `HashValidationScope`, `StopRollbackOnMissingRollbackFile`) | [Settings inheritance](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/settings-inheritance-overview.md) |
| `ProductDefaults.TargetGroupDefaults.TargetDefaults` | node | Defaults inherited by every target (`DbCommandTimeoutInSeconds`, `DbCommandMaxRetries`, `DbCommandWaitTimeInMsBeforeRetry`) | [Target options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/target-options.md) |
| `Products` | node (array) | One `ProductOptions` per product: `Alias`, `MigrationFilesRootDirectory`, `TargetGroupMigrationOrder`, overrides of the defaults, `TargetGroups` | [Product options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/product-options.md) |
| `TargetGroups` | node (array, inside a product) | One `TargetGroupOptions` per engine: `Alias`, `DatabaseType`, `TargetMigrationOrder`, `HashValidationScope`, `UseCliToolAlias`, `Targets` | [Target group options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/target-group-options.md) |
| `Targets` | node (array, inside a target group) | One `TargetOptions` per connection: `Alias`, `ConnectionString`, timeouts, retries, `UseCliToolAlias`, `CliToolParameters` | [Target options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/target-options.md) |
| `CliTools` | node (array) | External tool profiles: `Alias`, `ExecutablePath`, `ArgumentTemplate`, `InputMode`, `SuccessExitCodes`, `CliToolTimeoutInSeconds` | [CLI tools options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/cli-tools-options.md) |
| `UseCliToolAlias` | key (product, target group, target, `migsettings`, TOML) | Routes migration files to the `CliTools` entry with that alias instead of the DAL; empty means DAL | [CLI tools options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/cli-tools-options.md) |
| `MigrationFilesRootDirectory` | key (product) | Root of the release directories, absolute or relative to the working directory; must exist at startup | [Directory structure](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/directory-structure.md) |
| `appsettings.json`, `appsettings.{Environment}.json`, `appsettings.{Product}.json`, `appsettings.{Product}.{Environment}.json` | files | The four layer hierarchy merged by `JsonOptionsSource` from the working directory or `--config-dir`; later files override earlier ones, arrays are replaced | [Configuration hierarchy](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/appsettings-hierarchy.md) |
| `migsettings.txt`, `migsettings.{Environment}.txt` | files | Directory defaults at product, release and target group level, applied after the JSON layers and before the TOML header | [migsettings files](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/migsettings-files.md) |
| `DataAccessLayers/{Type}/` | directory (next to the binary) | Holds the SQL templates of every engine and the assemblies of external DAL plugins; scanned by `DalFactory` and `TemplateCache` | [Deployment View 7.2.1](07-Deployment-View.md#721-cli-installation-on-an-operator-machine-or-ci-runner) |
| `DOTNET_ENVIRONMENT` | env var | Cross checked against the required `--environment`; a conflicting value exits `2`, a blank `--environment` without it exits `3` | [Environment variables](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/environment-variables.md) |
| `{ENV:NAME}` | placeholder | Replaced by the environment variable `NAME` in configuration values, `--config-dir`, `--product`, `--environment`, `--to-release`, SQL templates and migration file content; unresolved in configuration or templates ends the run, in migration files it becomes empty with a warning | [Environment variables](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/environment-variables.md) |
| `{CFG:NAME}` | placeholder (templates only) | Replaced per call by `TemplateCache` with `SchemaName` or `TableBaseName` of the repository | [Template system](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/template-system.md) |
| `--product` / `-p`, `--environment` / `-env` | CLI options (required on every command) | Select the product alias and the environment name | [Global options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/global-options.md) |
| `--config-dir` / `-cd` | CLI option (global) | Directory searched for the `appsettings*.json` hierarchy instead of the working directory | [Global options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/global-options.md) |
| `--startup-info` / `-si`, `--reveal-sensitive-data` / `-rsd` | CLI options (global) | Show or suppress the startup banner; disable `SensitiveDataMasker` for one run | [Global options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/global-options.md) |
| `--run-mode` / `-rm` | CLI option (`migrate-up`, `migrate-down`, `fix`) | `validate`, `simulate` or `migrate` (default); `fix` accepts only `migrate` and `simulate`; other commands always run in `Migrate` mode | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) |
| `--to-release` / `-tr` | CLI option (`migrate-up`, `migrate-down`, `baseline`) | Upper bound of the releases to apply, or the release to roll back to | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) |
| `--target-group` / `-tg` | CLI option (`migrate-up`, `migrate-down`, `validate-hash`, `update-hash`, `baseline`) | Restricts the command to the named target group aliases | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) |
| `--allow-out-of-order` / `-ooo` | CLI option (`migrate-up`) | Permits files of a release below the highest migrated release for this run | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) |
| `--target-group-migration-order` / `-tgmo` | CLI option (`migrate-up`, `baseline`) | Comma separated target group aliases that override the configured order for this run | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) |
| `--stop-rollback-on-missing-rollback-file` / `-sromrf` | CLI option (`migrate-up`) | Overrides `StopRollbackOnMissingRollbackFile` for the error recovery chain of this run | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) |
| `--scope` / `-s` | CLI option (`validate-hash`, `fix`) | `file`, `sqlblocks` or `disabled` for `validate-hash`; `all` or `orphanedruns` for `fix` | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) |
| `--older-than` / `-ot`, `--last-migration-status` / `-lms` | CLI options (`fix`) | Minimum age in minutes of an orphaned run (default 60); status given to its open records, `not-migrated` (default) or `migrated` | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md#fix) |

## Architecture and technology terms

| Term | Definition | See also |
|------|------------|----------|
| arc42 | The template for architecture documentation whose twelve sections structure this wiki; each chapter opens with the question it answers. | [Home](Home.md) |
| building block | A unit of the static decomposition (project, layer, class group) shown in the Building Block View; RayMigrator's level 1 blocks are the 24 projects of `RayMigrator.sln`. | [Building Block View](05-Building-Block-View.md#51-whitebox-overall-system) |
| blackbox / whitebox | A blackbox states only the responsibility and interfaces of a building block; a whitebox opens it and shows the inner blocks and their dependencies. | [Building Block View](05-Building-Block-View.md) |
| crosscutting concept | A rule, model or mechanism that applies across many building blocks at once (configuration layering, hashing, logging, error model); chapter 08 lists them under the approach names of section 4.3. | [Crosscutting Concepts](08-Crosscutting-Concepts.md) |
| ADR | Architecture Decision Record: a decision documented with context, decision, consequences and alternatives; chapter 09 keeps twenty of them in the compact Nygard/MADR style. | [Decision log](09-Architecture-Decisions.md#decision-log) |
| quality goal, quality scenario | A quality goal is one of the five prioritized ISO 25010 characteristics of section 1.2; a quality scenario (`Q-{GOAL}-{n}`) makes it testable with stimulus, environment, response and response measure. | [Quality Requirements](10-Quality-Requirements.md#102-quality-scenarios) |
| risk, technical debt | A risk (`R-nn`) is an uncertain external event that can hurt a quality goal; technical debt (`TD-C-nn`, `TD-D-nn`, `DAL-nnn`) is a known deficiency inside the repository. | [Risks and Technical Debt](11-Risks-and-Technical-Debt.md#111-risk-assessment-method) |
| layer | A group of projects with one reason to change; project references point downward only (`Console`, `Pipeline`, `Services`, `Infrastructure`, `Core`, `Database`, leaf packages). | [Solution Strategy 4.2](04-Solution-Strategy.md#42-top-level-decomposition) |
| ADO.NET provider | The .NET data access library of an engine (`Microsoft.Data.SqlClient`, `Npgsql`, `MySqlConnector`, `Microsoft.Data.Sqlite`), isolated inside the corresponding DAL plugin; RayMigrator uses no ORM. | [Architecture Constraints](02-Architecture-Constraints.md#database-engines-and-adonet-providers) |
| System.CommandLine | The Microsoft command line parsing library (2.0.11) behind `CommandLineConfiguration`; it defines the seven subcommands, their options and the help and version handling. | [Context and Scope](03-Context-and-Scope.md#cli-commands-as-the-business-interface) |
| Options pattern | The `Microsoft.Extensions.Options` idiom of binding configuration to typed classes (`RayMigratorOptions`) with data annotation validation, post configuration (`ProductDefaultsPostConfigureOptions`) and `IValidateOptions` (`RayMigratorOptionsValidator`). | [Configuration Inheritance with Validation](08-Crosscutting-Concepts.md#configuration-inheritance-with-validation) |
| dependency injection, host | The `Microsoft.Extensions.Hosting` container built by `DirectModePipeline`; services resolve their collaborators from it, the DAL is the exception and comes from the static `DalFactory`. | [Dependency injection and the context pattern](08-Crosscutting-Concepts.md#dependency-injection-and-the-context-pattern) |
| Serilog sink, enricher | A sink receives log events (console, file, `Raycoon.Serilog.Sinks.SQLite`, `RayMigratorDatabaseSink`); an enricher adds properties to every event (`MigrationContextEnricher` adds run, target group, target, file and block ids). | [Structured Logging](08-Crosscutting-Concepts.md#structured-logging-to-console-file-and-database) |
| Blazor WebAssembly | The .NET framework that runs the Config Wizard as static files inside the browser (`Microsoft.NET.Sdk.BlazorWebAssembly`, MudBlazor components); no server side code. | [ADR-019](09-Architecture-Decisions.md#adr-019-blazor-webassembly-config-wizard-as-a-separate-database-free-tool) |
| RID | Runtime identifier of a .NET publish target; releases are built for `win-x64`, `osx-arm64` and `linux-x64`. | [Deployment View](07-Deployment-View.md#73-release-pipeline) |
| framework dependent single file publish | The publish mode of the release archives (`--no-self-contained`, `PublishSingleFile`): one executable per RID that needs the .NET 10 runtime on the host. | [Deployment View 7.1](07-Deployment-View.md#71-infrastructure-level-1-distribution-and-runtime-environments) |
| central package management | `ManagePackageVersionsCentrally` in `Directory.Packages.props`: one pinned version per NuGet package for all projects, restored only from nuget.org. | [Architecture Constraints](02-Architecture-Constraints.md#platform-and-toolchain) |
| multi-framework targeting | Every library, DAL plugin and the console compile for `net10.0;net9.0;net8.0`; test projects and the Config Wizard web project target `net10.0` only. | [ADR-015](09-Architecture-Decisions.md#adr-015-multi-framework-targeting) |
| OIDC trusted publishing | NuGet publishing without a stored API key: `Publish NuGet` obtains a short lived token through `NuGet/login@v1` using the GitHub Actions OIDC identity. | [Deployment View 7.3](07-Deployment-View.md#73-release-pipeline) |
| GitHub Actions workflow | The CI definitions under `.github/workflows/`: `Build & Test`, `Publish Release`, `Publish NuGet`, `Deploy ConfigWizard Web`, `Sync arc42 Wiki` and the two Claude workflows. | [Architecture Constraints 2.2](02-Architecture-Constraints.md#22-organizational-constraints) |
| Azure Static Web Apps | The static file host of the Config Wizard at `config.raymigrator.com`; deployed by `Azure/static-web-apps-deploy@v1` after the legal page check. | [Deployment View 7.2.4](07-Deployment-View.md#724-config-wizard-hosting) |
| BUSL-1.1 | Business Source License 1.1, the license of every RayMigrator version from 0.11.0; source available, with a Change Date and a Change License. | [ADR-017](09-Architecture-Decisions.md#adr-017-business-source-license-11-with-additional-use-grant) |
| Additional Use Grant | The BUSL clause under which RAYCOON permits production use free of charge for everyone, hosted and SaaS offerings included. | [Architecture Constraints 2.3](02-Architecture-Constraints.md#23-conventions) |
| Change Date, Change License | The date four years after first public distribution of a version, recorded per version in `Docs/license-change-dates.md`, on which that version converts to the Change License, Apache License 2.0. | [License change dates](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/license-change-dates.md) |
| MIT carve out | `Raycoon.RayMigrator.Database.Example` carries its own MIT license so that external DAL authors can copy the skeleton. | [Architecture Constraints 2.3](02-Architecture-Constraints.md#23-conventions) |
| CLA | Contributor License Agreement (`CLA.md`), confirmed in the pull request template; it lets RAYCOON relicense contributions under BUSL-1.1 and the Change License. | [Architecture Constraints 2.2](02-Architecture-Constraints.md#22-organizational-constraints) |
| Docker engine tests | `Raycoon.RayMigrator.Tests.Engine` against the four containers of `Testing/Docker/docker-compose.yml` (SQLite uses a temporary file); a local duty before a release, skipped with `Assert.SkipUnless` when a container is missing. | [Deployment View 7.2.5](07-Deployment-View.md#725-development-and-test-environment) |
| xUnit v3 | The test framework (`xunit.v3`) of all test projects, chosen for native dynamic skips; paired with `AwesomeAssertions` and `NSubstitute`. | [Test Pyramid](08-Crosscutting-Concepts.md#test-pyramid) |
| Semantic Versioning, Keep a Changelog | The versioning and changelog conventions of `CHANGELOG.md`; below 1.0 breaking changes are allowed between minor versions and marked explicitly. | [ADR-016](09-Architecture-Decisions.md#adr-016-pre-10-compatibility-policy-no-aliases-no-in-place-upgrades) |
| SHA-256 | The hash algorithm applied to the UTF-8 text of every migration file, its TOML section and its SQL blocks (`StringExtensions.GenerateSha256`). | [Hash Validation](08-Crosscutting-Concepts.md#hash-validation) |
| Mermaid | The text based diagram syntax used in these pages instead of binary images; GitHub renders it natively in the wiki. | [Home](Home.md) |

## Abbreviations

| Abbreviation | Expansion |
|--------------|-----------|
| ADO.NET | ActiveX Data Objects for .NET, the .NET database access API implemented by the providers |
| ADR | Architecture Decision Record |
| API | Application Programming Interface |
| BOM | Byte Order Mark |
| BUSL | Business Source License |
| CI | Continuous Integration |
| CLA | Contributor License Agreement |
| CLI | Command Line Interface |
| CVE | Common Vulnerabilities and Exposures |
| DAL | Data Access Layer |
| DBA | Database Administrator |
| DDL | Data Definition Language (`CREATE`, `ALTER`, `DROP`) |
| DI | Dependency Injection |
| DML | Data Manipulation Language (`INSERT`, `UPDATE`, `DELETE`, `SELECT`) |
| DSL | Domain Specific Language |
| DTO | Data Transfer Object |
| FK | Foreign Key |
| ISO 25010 | ISO/IEC 25010 software product quality model |
| JSON | JavaScript Object Notation |
| LTS | Long Term Support |
| MADR | Markdown Architectural Decision Records |
| MIT | MIT License |
| OIDC | OpenID Connect |
| ORM | Object Relational Mapper |
| OSI | Open Source Initiative |
| RID | Runtime Identifier |
| RMLA | RayMigrator Dual License Agreement (superseded by BUSL-1.1 in 0.11.0) |
| SaaS | Software as a Service |
| SDK | Software Development Kit |
| SHA-256 | Secure Hash Algorithm with a 256 bit digest |
| SQL | Structured Query Language |
| SQLSTATE | Five character SQL standard error code (PostgreSQL transient error classification) |
| TDS | Tabular Data Stream (SQL Server wire protocol) |
| TOML | Tom's Obvious, Minimal Language |
| UTF-8 | 8 bit Unicode Transformation Format |
| WAL | Write Ahead Log (SQLite journal mode) |
| WASM | WebAssembly |
| Q-, R-, TD-, DAL- | Identifier prefixes of quality scenarios, risks, technical debt items and DAL audit items in chapters 10 and 11 |

## Related documentation

- [Appendix glossary](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/glossary.md), the class level glossary of the implementation reference
- [Configuration hierarchy](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/appsettings-hierarchy.md) and [Settings inheritance](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/settings-inheritance-overview.md), the configuration reference
- [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) and [Global options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/global-options.md)
- [Validation rules](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/validation-rules.md), [Repository schema](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/repository-schema.md), [Execution modes](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/execution-modes.md)
- [Home](Home.md), [Introduction and Goals](01-Introduction-and-Goals.md), [Crosscutting Concepts](08-Crosscutting-Concepts.md)
