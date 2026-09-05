# Design Decisions

This document explains the rationale behind key architectural decisions in RayMigrator.

## Layered Architecture

**Decision**: 7-layer service-oriented architecture with clear separation of concerns.

**Rationale**:
- **Testability**: Each layer can be unit tested independently
- **Maintainability**: Changes in one layer don't cascade to others
- **Extensibility**: New database types or commands can be added without modifying core logic
- **Dependency Management**: Clear dependency flow prevents circular references

**Trade-offs**:
- More projects to manage
- Additional abstraction overhead
- Requires careful interface design

## Pipeline Extraction

**Decision**: Extract the execution pipeline (`DirectModePipeline`, `JsonOptionsSource`, `RayMigratorService`, `SerilogFactory`) into a separate `Raycoon.RayMigrator.Pipeline` project.

**Rationale**:
- **Separation of concerns**: The Console project focuses on command-line parsing and environment resolution; the Pipeline project owns DI setup, host building, and service execution
- **Reusability**: The Pipeline project can be referenced by other entry points without pulling in Console-specific dependencies (System.CommandLine)
- **Cleaner dependencies**: Pipeline references Services, Core, Infrastructure, Database, Shared — but not Console

## DAL Plugin Architecture

**Decision**: Use DependencyContext-based discovery for built-in DAL assemblies and filesystem-based discovery for external DAL plugins in `DataAccessLayers/` subdirectories.

**Rationale**:
- **Extensibility**: Third-party developers can add database support without modifying core code — drop a DLL into `DataAccessLayers/{DatabaseType}/` and it is discovered automatically
- **Isolation**: Each DAL is a separate assembly (e.g., `Database.SqlServer`, `Database.PostgreSQL`, `Database.MariaDb`, `Database.MySql`, `Database.Sqlite`) with its own NuGet dependencies
- **Discovery mechanism**: `DalFactory` static constructor uses two modes: (1) DependencyContext-based scanning of deps.json for runtime libraries starting with `Raycoon.RayMigrator.` to discover built-in DALs (works with single-file publish), and (2) filesystem scanning of `DataAccessLayers/` subdirectories for external DAL plugins implementing `IDal` with a `[DatabaseType("...")]` attribute.
- **Instance caching**: DAL instances are cached in a `ConcurrentDictionary` keyed by `"{databaseType}_{connectionString}"`, avoiding redundant allocations
- **External DAL development**: The `Database.Example` project provides a skeleton template for external developers to build custom DAL plugins
- **Build integration**: Post-build MSBuild targets in the Console project copy built DAL DLLs into `DataAccessLayers/{Type}/` subdirectories. SQL templates are delivered as `<Content>` items that propagate transitively through ProjectReference.
- **Transient error detection per DAL**: Each DAL overrides `DalBase.IsTransient(Exception ex)` to check database-specific exception types and error codes. `DalBase` provides a base implementation that handles `TimeoutException` and protected `ExecuteWithRetryAsync`/`ExecuteWithRetry` helper methods that pass `IsTransient` to `RetryHelper`. This keeps transient detection co-located with each database driver.

**Trade-offs**:
- Requires `Activator.CreateInstance` (reflection) for DAL instantiation — DAL classes must be `public`
- File-based discovery adds startup I/O, but this is a one-time cost
- Duplicate `[DatabaseType]` attributes across assemblies are silently ignored (`TryAdd` keeps the first-discovered type)

## Configuration System

**Decision**: Use Options Pattern with hierarchical JSON configuration files.

**Rationale**:
- **Strongly-typed**: Compile-time checking of configuration properties
- **Flexible override**: Environment-specific settings without code changes
- **Environment variables**: `{ENV:VAR}` syntax for runtime flexibility
- **Standardized**: Follows .NET Core configuration best practices

**Hierarchy** (later overrides earlier):
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. `appsettings.{Product}.json`
4. `appsettings.{Product}.{Environment}.json`

All four files are searched in the directory specified by `--config-dir` (or the current working directory when `--config-dir` is not set).

> **Note**: Unlike the typical .NET pattern, `AddCommandLine(args)` and `AddEnvironmentVariables()` are NOT used in the configuration builder. Command-line arguments are handled separately via `System.CommandLine` and mapped to `RayMigratorConsoleOptions`. Environment variables are resolved via the custom `{ENV:VAR}` placeholder syntax in `EnvironmentVariableReplacer`.

## SQL Template System

**Decision**: Use SQL templates with placeholder substitution instead of ORM or query builders.

**Rationale**:
- **Database-specific optimization**: Each database gets optimized SQL
- **Transparency**: DBAs can review exact SQL being executed
- **Flexibility**: Complex DDL operations that ORMs struggle with
- **Versioning**: Templates can be versioned alongside schema changes

**Placeholder types**:
- `{CFG:PropertyName}` - Configuration values
- `{ENV:VariableName}` - Environment variables
- `@ParameterName` - SQL parameters (parameterized queries)

### TemplateCache with Deferred Validation

**Decision**: `TemplateCache` accepts a `validateConfiguration` parameter (default `true`). When set to `false`, template loading occurs without validating that configured `DatabaseType` values have matching templates.

**Rationale**:
- **Flexibility**: When Products and Repository configuration are loaded from an alternative source, they may not be available at `TemplateCache` construction time. The cache loads all available templates at startup but defers validation until `ValidateConfigurationAgainstTemplateCache(options)` is called explicitly after configuration is loaded.
- **Reusability**: The same `TemplateCache` class works for different configuration modes without branching logic.
- **Public validation method**: `ValidateConfigurationAgainstTemplateCache(options)` and `GetAvailableDatabaseTypes()` are public, allowing external callers to validate on demand.

## MigrationContext Pattern

**Decision**: Use a central context object that flows through the execution pipeline, accessed via `IMigrationContextAccessor`.

**Rationale**:
- **State management**: Single source of truth for migration state
- **Immutable snapshots**: `Clone` property for safe logging
- **Host-mode independence**: `IMigrationContextAccessor` abstracts the hosting model — `SingletonMigrationContextAccessor` for CLI (single execution), `AsyncLocalMigrationContextAccessor` for per-request isolation
- **Traceability**: Complete context available for debugging

## Repository Separate from Targets

**Decision**: Migration tracking database (repository) is separate from target databases.

**Rationale**:
- **Cross-database support**: Track migrations across different database types
- **Centralized tracking**: Single source of truth for all products
- **Isolation**: Repository failures don't affect target databases
- **Audit compliance**: Dedicated logging and history tables

## Block-Level Execution

**Decision**: Support block-level migration tracking (GO delimiter for SQL Server).

**Rationale**:
- **Partial recovery**: Resume from failed block, not entire file
- **Large migrations**: Track progress within large migration files
- **Hash validation**: Validate at file, config, or block level

## TOML for Migration Metadata

**Decision**: Use TOML format embedded in SQL comments for migration configuration.

**Rationale**:
- **Human-readable**: Easy to write and understand
- **Parseable**: Standard format with good tooling
- **Non-invasive**: Embedded in comments, doesn't affect SQL execution
- **Hierarchical**: Inheritance through `migsettings.txt` files

## Flat Directory Layout Auto-Detection

**Decision**: For products with exactly one TargetGroup, allow migration files to be placed directly under the release directory (flat layout) without a TargetGroup subdirectory. The TargetGroup alias is assigned automatically.

**Implementation**: In `StringExtensions.GetReleaseVersionAndTargetGroupAlias`, if the second path segment does not match any configured TargetGroup alias and exactly one TargetGroup is configured, the single TargetGroup alias is auto-assigned. In `MigrationService.DiscoverAndPrepareMigrationFiles`, `ValidateFlatLayoutAmbiguity` ensures no release uses a mix of flat and traditional layouts (throws `ConfigurationValidationException`). Rollback file lookup also applies a flat-layout fallback when the file is not found in the traditional `{Release}/{TargetGroupAlias}/` path.

**Rationale**:
- **Simplicity**: Simple products with one target database do not need an extra subdirectory level
- **Gradual adoption**: Existing projects with flat file structures can be onboarded without reorganizing directories
- **Safety**: Mixed flat/traditional layouts within a release are rejected to prevent ambiguity

**Constraints**:
- Applies only when the product has exactly one TargetGroup
- Mixing flat and traditional layouts within the same release directory is not allowed
- Case mismatches between directory names and configured TargetGroup aliases are detected and rejected by `ValidateTargetGroupAliasCasing`

## Execution Modes

**Decision**: Support both "Simultaneously" and "Successively" execution orders.

**Simultaneously** (file → target loop):
```
Migration A → Target 1
Migration A → Target 2
Migration B → Target 1
Migration B → Target 2
```

**Successively** (target → file loop):
```
All migrations → Target 1
All migrations → Target 2
```

Configured per TargetGroup via `TargetMigrationOrder` option (default inherited from `ProductDefaults.TargetGroupDefaults.TargetMigrationOrder`).

**Rationale**:
- **Simultaneously**: Keeps all targets in sync, better for tightly coupled systems
- **Successively**: Safer, allows partial success, better for independent targets

### TargetGroup Execution Order

The order in which target groups are processed within a release is controlled by `TargetGroupMigrationOrder`. The resolution chain (highest priority first) is: CLI argument (`--target-group-migration-order`) > migsettings entry > `ProductOptions.TargetGroupMigrationOrder` (comma-separated) > configuration array order.

When specified, all target group aliases must be listed exactly once. Applies to `MigrateUp` and `baseline` commands only.

## Error Handling Strategies

**Decision**: Provide five migration error handling modes via `MigrationErrorAction` and two rollback error handling modes via `RollbackErrorAction`.

### MigrationErrorAction

Defines the behavior when a migration encounters an error. Values: `Terminate` (10), `Rollback` (20), `RollbackErrorOnly` (21), `RollbackRelease` (22), `Ignore` (30). See [Error Handling](../02-core-concepts/error-handling.md) for detailed behavior descriptions and [Enum Reference](../08-cli-reference/command-reference.md#migrationerroraction) for the complete enum table.

### RollbackErrorAction

Defines the behavior when a rollback operation itself encounters an error. Since a failed rollback cannot itself be rolled back, only `Terminate` (10) and `Ignore` (30) are meaningful. See [Error Handling — Rollback Error Handling](../02-core-concepts/error-handling.md#rollback-error-handling) for details.

### StopRollbackOnMissingRollbackFile

When `RequireRollbackFile = false`, some migration files may not have a corresponding rollback file. `StopRollbackOnMissingRollbackFile` (default: `true`) controls what happens when an error-recovery rollback chain encounters a migration file with no rollback file:

- `true` (default): The rollback chain stops at the missing file, leaving any migrations before it intact.
- `false`: The rollback chain continues past the missing file and skips it.

This setting only applies to error-recovery rollback (`MigrationErrorAction = Rollback`, `RollbackErrorOnly`, or `RollbackRelease`), not to explicit `migrate-down` commands. The effective value is resolved at runtime via: CLI `--stop-rollback-on-missing-rollback-file` → `TargetGroup.StopRollbackOnMissingRollbackFile` → `Product.StopRollbackOnMissingRollbackFile` → hardcoded default `true`. While migsettings files and per-file TOML can declare this setting (and it is parsed and merged in the migsettings hierarchy), the value stored on `MigrationFileInfo` is not consulted during the rollback chain; only the appsettings-level (Product/TargetGroup) and CLI values participate in the runtime resolution.

### Inheritance

Both `MigrationErrorAction` and `RollbackErrorAction` follow the full configuration inheritance chain: `ProductDefaults` -> `Product` -> migsettings hierarchy -> per-file TOML. `StopRollbackOnMissingRollbackFile` is parsed in the same migsettings/TOML hierarchy but its runtime resolution uses only the appsettings levels (`ProductDefaults`, `Product`, `TargetGroup`) and the CLI override. See [Error Handling — Priority Chain](../02-core-concepts/error-handling.md#priority-chain) for the complete priority order.

**Rationale**:
- **Flexibility**: Different scenarios require different strategies
- **Safety**: Production might prefer Terminate to prevent cascading issues
- **Recovery**: Development might prefer Rollback for quick iteration
- **Granularity**: `RollbackRelease` provides a middle ground — rolls back the failed release while preserving earlier, successfully migrated releases
- **Rollback safety**: Separating rollback error handling from migration error handling acknowledges that rollback failures require different strategies since they cannot be recursively rolled back
- **Missing rollback files**: `StopRollbackOnMissingRollbackFile` gives explicit control over partial rollback scenarios when `RequireRollbackFile = false`

## Multi-Framework Targeting

**Decision**: Target .NET 10, .NET 9, and .NET 8.

**Rationale**:
- **.NET 8 LTS**: Long-term support for enterprise environments
- **.NET 9**: Current standard-term support
- **.NET 10**: Latest features for early adopters
- **Compatibility**: Broadest deployment options

## No Parallel Database Execution

**Decision**: Execute migrations sequentially, not in parallel within a run.

**Rationale**:
- **Simplicity**: Easier to reason about execution order
- **Debugging**: Clear sequence for troubleshooting
- **Resource management**: Avoid overwhelming database servers
- **Transaction integrity**: Simpler rollback scenarios

## External CLI Tool Execution

**Decision**: Support executing migration SQL files via external CLI tools (sqlcmd, psql, mysql, mariadb, sqlite3) as an alternative to the built-in DAL.

**Rationale**:
- **Flexibility**: Some environments require vendor CLI tools for compliance, auditing, or feature parity
- **Complex scripts**: Certain SQL scripts use tool-specific syntax (e.g., `:r` includes in sqlcmd) that the DAL cannot handle
- **Existing workflows**: Teams already using CLI tools can adopt RayMigrator without changing execution behavior

**Configuration**:
- `CliTools[]` defines global CLI tool profiles at the `RayMigratorOptions` root level, each with an `Alias`, `ExecutablePath`, `ArgumentTemplate`, `InputMode`, `SuccessExitCodes` (string[] with range notation support), and `CliToolTimeoutInSeconds`
- `UseCliToolAlias` can be set at `ProductDefaults`, `Product`, `TargetGroup`, `Target`, migsettings, or per-file TOML level, following the standard configuration inheritance chain
- `CliToolParameters` on `TargetOptions` provides key-value pairs for placeholder substitution in the `ArgumentTemplate`
- Two input modes: `File` (pass file path as argument) and `Stdin` (pipe content via standard input)

**Trade-offs**:
- CLI tool output parsing is limited to exit codes (no structured result like DAL `TemplateResponse`)
- Transaction control is delegated to the CLI tool (no programmatic transaction wrapping)
- Arguments may contain sensitive data from `CliToolParameters`; arguments are not logged

## Atomic Shared Connection

**Decision**: When the repository and target share the same database (identical `DatabaseType` and `ConnectionString`) and transactions are enabled and block errors are not ignored, execute both the target SQL blocks and the repository status updates in a single atomic transaction on a shared connection.

**Implementation**: `MigrationService.CanUseSharedConnection(file, targetOptions, repository, targetGroupDatabaseType, ignoreBlockErrors)` returns `true` when all conditions are met. When `true`, `ExecuteSqlBlocks` delegates to `ExecuteSqlBlocksAtomic`, which opens a single connection/transaction for both the migration SQL and the repository status updates, so a partial failure rolls back both together. When `DbCommandMaxRetries > 0`, transient errors trigger a file-level retry: the entire transaction is rolled back and all blocks re-executed from scratch.

**Rationale**:
- **Atomicity**: Target SQL execution and repository tracking either both commit or both roll back, eliminating the risk of a partially recorded migration run
- **Simplicity**: No distributed transaction or two-phase commit needed when both operations target the same database
- **Opt-in**: Applies only when the preconditions are met; all other cases fall back to the standard two-connection path

**Constraints**:
- Requires `UseTransaction = true` on the migration file
- Block errors must not be ignored (`MigrationErrorAction != Ignore`)
- Repository and target must have the same `DatabaseType` and the same `ConnectionString`

## Hash Validation Scopes

**Decision**: Support three hash validation scopes: File, SqlBlocks, Disabled.

**Rationale**:
- **File**: Detect any modification to migration files
- **SqlBlocks**: Allow config changes without rehashing
- **Disabled**: For development scenarios or legacy systems

## Related Documentation

- [Patterns](patterns.md) - Implementation patterns
- [Data Flow](data-flow.md) - How decisions affect execution
- [Component Responsibilities](component-responsibilities.md) - Layer boundaries
- [Execution Modes](../02-core-concepts/execution-modes.md) - Operating modes, migration order, run modes
- [Error Handling](../02-core-concepts/error-handling.md) - MigrationErrorAction strategies
- [CLI Tools Options](../06-configuration-reference/cli-tools-options.md) - External CLI tool configuration reference
