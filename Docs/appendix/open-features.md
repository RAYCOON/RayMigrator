# Open Features

Central registry of open features in RayMigrator v0.10.x.

---

## Priority 1 — Core Migration Features

### F3: Recovery Orchestration

**Status:** IMPLEMENTED

Detection of orphaned runs and interrupted migrations works and logs warnings. Orphaned run cleanup is implemented via the `Fix` command (`FixIssuesAsync`), which marks orphaned `MigrationRun` entries as Error and fixes associated `MigrationRecord` entries. Resume-from-block recovery is implemented for both Migrate-Up and Migrate-Down.

- **What exists:** `RepositoryMigrationRunSelectOrphaned()`, `RepositoryMigrationGetInterrupted()`, warning logging, `FixIssuesAsync` command with `Repository_MigrationRun_FixOrphaned.sql` and `Repository_MigrationRecord_FixOrphaned.sql` templates (all 5 engines), `RepositoryMigrationRecordFixOrphaned` template-executor method, `InterruptedMigrationInfo` model, dry-run mode, `--older-than` filter, `--last-migration-status` option, `FindResumableBlock()` for automatic resume-from-block in Migrate-Up, rollback block resume in Migrate-Down via `FileDownBlocksMigrated`, auto-fix of orphaned runs older than 10 minutes (`AutoFixOrphanedRunsThresholdMinutes`) when a parallel-run conflict is detected, `StopRollbackOnMissingRollbackFile` configuration setting and `--stop-rollback-on-missing-rollback-file` / `-sromrf` CLI option (controls error-recovery rollback chain behavior when rollback file is missing and `RequireRollbackFile=false`)
- **What's missing:** `--force-restart` / `--skip` CLI options
- **Files:** `MigrationService.cs`, `TemplateExecutor.cs`, `RayMigratorService.cs`
- **Docs:** [Resilience](../02-core-concepts/resilience.md)

---

## Priority 3 — Infrastructure & Tooling

### F11: Planned Database Support (Oracle)

**Status:** OPEN

Oracle DAL is not yet implemented. The pluggable DAL architecture supports adding new database engines via the `Database.Example` skeleton project.

- **What exists:** Pluggable DAL architecture with 5 migration engines (SqlServer, PostgreSQL, MariaDb, MySql, Sqlite), `Database.Example` skeleton for external DAL development (19 placeholder templates including `Repository_MigrationRecordHistory_Archive.sql`)
- **What's missing:** Oracle DAL
- **Docs:** [Adding New Database](../03-database-layer/adding-new-database.md), [New Database Type](../09-extending/new-database-type.md), [External DAL Development](../09-extending/external-dal-development.md)

### F12: Managed Operating Modes (ManagedLocal, ManagedRemote)

**Status:** OPEN (in RayMigrator Studio)

The `OperatingMode` enum defines three modes: `Standalone` (current default, JSON-based config), `ManagedLocal` (Admin-DB for configuration), and `ManagedRemote` (Thin Client to API server). Only `Standalone` mode is active in Engine. The Admin-DB, API, and Client projects have been moved to RayMigrator Studio. The ConfigWizard.Core and ConfigWizard.Web projects remain in this repository.

- **What exists in Engine:** `OperatingMode` enum with all three values, `AdminDbOptions` and `RayMigratorBootstrapOptions` configuration classes, `DirectModePipeline` entry point designed to accept pre-built options from Studio, `IOptionsSource` / `OptionsSourceResult` abstraction for pluggable configuration sources
- **What's in RayMigrator Studio:** ManagedLocal mode (Admin-DB integration), ManagedRemote mode (Thin Client), Admin-DB schema and services, API server, and Client
- **Files:** `Core/Configuration/Enums/OperatingMode.cs`, `Core/Configuration/Options/RayMigratorBootstrapOptions.cs`, `Pipeline/DirectModePipeline.cs`, `Core/Configuration/Sources/OptionsSourceResult.cs`, `Core/Configuration/Sources/IOptionsSource.cs`

---

## Implemented Features

### CLI Tool Execution (External SQL Tools)

**Status:** IMPLEMENTED

External CLI tools (sqlcmd, psql, mysql, mariadb, sqlite3) can be used as an alternative to the built-in DAL for executing migration SQL files. Tools are defined globally in `CliTools[]` configuration, and activation is controlled by the `UseCliToolAlias` setting which cascades through the full configuration hierarchy (ProductDefaults -> Product -> TargetGroup -> Target -> migsettings -> TOML header).

- **What exists:** `CliToolOptions` configuration class, `CliToolInputMode` enum (File/Stdin), `ICliToolExecutor` interface and `CliToolExecutor` implementation using `System.Diagnostics.Process`, `UseCliToolAlias` on `ProductDefaultOptions`, `ProductOptions`, `TargetGroupOptions`, `TargetOptions`, `MigrationFileInfo`, and migsettings entries, `CliToolParameters` dictionary on `TargetOptions` for placeholder substitution, `CliToolExecutionException` and `CliToolTimeoutException` exceptions, argument template placeholder resolution (`{FilePath}` + custom keys), configurable success/error exit codes and timeout, `ResolveUseCliToolAlias()` for file+target resolution, `GetCliToolByAlias()` for alias lookup with validation, unit tests in `P0_CliToolConfigTests`, `P1_CliToolExecutionHelpersTests`, `P1_CliToolExecutorTests`, `P1_CliToolValidationTests`, `P1_UseCliToolAliasInheritanceTests`, engine integration tests for File, Stdin, and Docker modes across all 4 supported engines (`SqlServerCliToolFileTests`, `SqlServerCliToolStdinTests`, `SqlServerCliToolDockerTests`, `PostgreSqlCliToolFileTests`, `PostgreSqlCliToolStdinTests`, `PostgreSqlCliToolDockerTests`, `MariaDbCliToolFileTests`, `MariaDbCliToolStdinTests`, `MariaDbCliToolDockerTests`, `MySqlCliToolFileTests`, `MySqlCliToolStdinTests`, `MySqlCliToolDockerTests`)
- **What's missing:** Nothing planned at this time
- **Files:** `CliToolExecutor.cs` (Services), `RayMigratorOptions.cs` (Core), `MigrationFileInfo.cs` (Core), `MigrationService.cs` (Services), `CustomExceptions.cs` (Shared), `CliToolInputMode.cs` (Core)
- **Docs:** [CLI Tools Options](../06-configuration-reference/cli-tools-options.md)

### Flat Directory Layout Auto-Detection for Single-TargetGroup Products

**Status:** IMPLEMENTED

For products with exactly one TargetGroup, migration files can be placed directly in the release directory (flat layout) instead of in a TargetGroup subdirectory. RayMigrator auto-detects this layout when scanning migration files and rollback files: if no files are found in the `{Release}/{TargetGroupAlias}/` subdirectory, it falls back to looking directly in `{Release}/`. Mixed flat and traditional layouts within the same release directory are detected and rejected with a `ConfigurationValidationException`.

- **What exists:** Flat layout fallback in `ScanMigrationFiles` (tries release directory directly when product has a single TargetGroup), `ValidateFlatLayoutAmbiguity()` method to reject ambiguous layouts with a `ConfigurationValidationException`, engine tests in `FlatLayoutTests`, `SqlServerFlatLayoutTests`, `MariaDbFlatLayoutTests`, `MySqlFlatLayoutTests`, `SqliteFlatLayoutTests`
- **What's missing:** Nothing planned at this time
- **Files:** `MigrationService.cs` (Services)

### TargetGroup Migration Order Override

**Status:** IMPLEMENTED

The `--TargetGroup-MigrationOrder` / `-tgmo` CLI option on `Migrate-Up` and `Baseline` commands allows specifying an explicit execution order for TargetGroups, overriding the default configuration order. All configured TargetGroup aliases must be listed exactly once.

- **What exists:** `--TargetGroup-MigrationOrder` (`-tgmo`) option on `Migrate-Up` and `Baseline` commands, `RayMigratorConsoleOptions.TargetGroupMigrationOrder` property, order validation in `MigrationService`, engine tests in `SqlServerTargetGroupMigrationOrderTests`, `SqliteTargetGroupMigrationOrderTests`, `TargetGroupMigrationOrderTests` (PostgreSQL), `MariaDbTargetGroupMigrationOrderTests`, `MySqlTargetGroupMigrationOrderTests`
- **What's missing:** Nothing planned at this time
- **Files:** `CommandLineConfiguration.cs` (Core), `RayMigratorConsoleOptions.cs` (Core), `MigrationService.cs` (Services)
- **Docs:** [CLI Reference](../08-cli-reference/command-reference.md)

### Atomic Shared Connection

**Status:** IMPLEMENTED

When the Repository and a migration Target share the same ConnectionString (same physical database), RayMigrator automatically executes all SQL blocks of a migration file and the corresponding repository status updates inside a single database transaction. This eliminates the atomicity gap that can leave the database and repository in an inconsistent state after a crash between a target SQL commit and the repository write.

- **Guard conditions**: `UseTransaction = true`, `MigrationErrorAction != Ignore`, same `DatabaseType`, and identical `ConnectionString` (ordinal comparison). When any condition is not met, the standard per-block-connection behavior applies.
- **File-level retry**: When `DbCommandMaxRetries > 0`, transient errors trigger a rollback of the entire transaction followed by a full file re-execution (not just the failed block), up to `DbCommandMaxRetries` attempts.
- **Rollback atomicity**: The same atomic pattern applies to rollback operations (`Migrate-Down`, error-triggered rollback via `MigrationErrorAction.Rollback`). All rollback blocks + final `NotMigrated` status are committed in a single transaction.
- **What exists:** `CanUseSharedConnection` guard in `MigrationService`, `ExecuteSqlBlocksAtomic` (forward path), `ExecuteRollbackBlocksAtomic` (rollback path), unit tests in `P1_CanUseSharedConnectionTests`, engine tests in `SqlServerAtomicSharedConnectionTests` (ASC1: transient retry, ASC2: permanent error rollback)
- **What's missing:** Engine tests for PostgreSQL, MariaDB, MySQL, SQLite atomic shared connection path
- **Files:** `MigrationService.cs` (Services)
- **Docs:** [Atomic Shared Connection](../02-core-concepts/error-handling.md#atomic-shared-connection), [Atomic Shared Connection Execution](../04-service-layer/migration-service.md#atomic-shared-connection-execution)

### SQLite as Migration Target DAL

**Status:** IMPLEMENTED

SQLite is a full DAL plugin usable as a migration target database for user migrations.

- **DAL class**: `DalSqlite` with `[DatabaseType("Sqlite")]` attribute, deployed to `DataAccessLayers/Sqlite/`
- **Templates**: SQL files for Repository and DatabaseLogging operations
- **Features**: Transaction support, retry logic, WAL journal mode, parameterized queries via text substitution
- **Engine tests**: Full suite of engine tests in `Tests.Engine/` covering MigrateUp (`SqliteHappyPathTests`, `SqliteTerminateTests`, `SqliteRollbackTests`, `SqliteRollbackErrorOnlyTests`, `SqliteRollbackReleaseTests`, `SqliteIgnoreTests`, `SqliteMultiTargetTests`, `SqliteRunAlwaysTests`, `SqliteIncrementalTests`, `SqliteFlatLayoutTests`), MigrateDown (`SqliteHappyPathTests`, `SqliteEdgeCaseTests`, `SqliteErrorTests`), Features (`SqliteSimulateModeTests`, `SqliteBaselineTests`, `SqliteValidateHashTests`, `SqliteUpdateHashTests`, `SqliteRunningGuardTests`, `SqliteTargetGroupFilterTests`, `SqliteTargetGroupMigrationOrderTests`, `SqliteMigrationRunMetaTests`, `SqliteMigSettingsInheritanceTests`, `SqliteRepositoryIntegrityTests`, `SqliteDatabaseLogTests`, `SqliteArchiveRetentionTests`, `SqliteFixTests`, `SqliteInfoTests`, `SqliteOutOfOrderBlockingTests`), and Compound (`SqliteRecoveryTests`, `SqliteRoundTripTests`). All tests use the standard Docker availability guard — they run when Docker is available and are skipped otherwise. The `DalSqlite.ExecuteScalarAsync` multi-statement handling issue (only executing the first statement) was fixed by implementing explicit `ExecuteReader` batching with `NextResult` traversal.

---

## Related Documentation

- [Architecture Overview](../01-architecture/overview.md) — System design
- [CLI Reference](../08-cli-reference/command-reference.md) — CLI command reference
- [README](../README.md) — Documentation index
