# Quick Reference

Central reference page with links to the canonical documentation sources. Use this page to quickly find where each topic is documented.

---

## Configuration Options

All configuration settings are documented in the [Configuration Reference](../06-configuration-reference/) section:

- **[Settings & Inheritance Overview](../06-configuration-reference/settings-inheritance-overview.md)** — Master table of every setting across all 4 layers, with complete inheritance chains
- [Repository Options](../06-configuration-reference/repository-options.md) — Repository database settings
- [Product Options](../06-configuration-reference/product-options.md) — Product-level settings
- [Target Group Options](../06-configuration-reference/target-group-options.md) — Target group settings
- [Target Options](../06-configuration-reference/target-options.md) — Target settings
- [Logging Options](../06-configuration-reference/logging-options.md) — Database logging settings
- [CLI Tools Options](../06-configuration-reference/cli-tools-options.md) — External CLI tool execution settings

---

## CLI Commands & Options

- **[Command Reference](../08-cli-reference/command-reference.md)** — Complete command matrix, per-command options, property mappings, and enum reference
- [Global Options](../08-cli-reference/global-options.md) — Options available across all commands (`--startup-info`, `--reveal-sensitive-data`, `--config-dir`)
- Per-command details: [Migrate-Up](../08-cli-reference/migrate-up.md) | [Migrate-Down](../08-cli-reference/migrate-down.md) | [Validate-Hash](../08-cli-reference/validate-hash.md) | [Update-Hash](../08-cli-reference/update-hash.md)
- Additional commands: [Info](../08-cli-reference/command-reference.md#info) | [Baseline](../08-cli-reference/command-reference.md#baseline) | [Fix](../08-cli-reference/command-reference.md#fix)

---

## Enums

All CLI-relevant enums are documented in the [Enum Reference](../08-cli-reference/command-reference.md#enum-reference) section of the Command Reference:

- **MigrationCommand** — CLI commands (`None` = 0, `MigrateUp` = 1, `MigrateDown` = 2, `ValidateHash` = 3, `UpdateHash` = 4, `Info` = 5, `Baseline` = 6, `FixIssues` = 7)
- **MigrationRunMode** — Execution modes (`Undefined` = 0, `Validate` = 10, `Simulate` = 20, `Migrate` = 100)
- **MigrationErrorAction** — Error handling (`Undefined` = 0, `Terminate` = 10, `Rollback` = 20, `RollbackErrorOnly` = 21, `RollbackRelease` = 22, `Ignore` = 30)
- **RollbackErrorAction** — Rollback error handling (`Undefined` = 0, `Terminate` = 10, `Ignore` = 30). See [Error Handling](../02-core-concepts/error-handling.md#rollback-error-handling)
- **TargetMigrationOrder** — Target iteration order (`Undefined` = 0, `Simultaneously` = 1, `Successively` = 2)
- **HashValidationScope** — Hash granularity (`Undefined` = 0, `File` = 1, `SqlBlocks` = 2, `Disabled` = 3)
- **FixIssues** — Fix scope (`Undefined` = 0, `All` = 1, `OrphanedRuns` = 2)
- **MigrationStatus** — Per-migration record status (`Undefined` = 0, `Pending` = 10, `Executing` = 20, `Failed` = 30, `NotMigrated` = 50, `Migrated` = 100)
- **MigrationRunResult** — Per-run result status (`Undefined` = 0, `Running` = 10, `Error` = 90, `Ok` = 100)
- **MigrationOperation** — Repository operation types (`Undefined` = 0, `Rollback` = 5, `MigrateDown` = 50, `MigrateUp` = 100). Used in the `MigrationOperation` lookup table in the repository.
- **CliToolInputMode** — How SQL files are passed to external CLI tools (`Undefined` = 0, `File` = 1, `Stdin` = 2). Default: `File`. See [CLI Tools Options](../06-configuration-reference/cli-tools-options.md)

---

## Exit Codes

Exit codes are documented in [Global Options — Exit Codes](../08-cli-reference/global-options.md#exit-codes).

---

## File Naming & TOML Metadata

- [File Naming Conventions](../07-migration-files/file-naming.md) — Sequence numbers, naming patterns, environment-specific files
- [TOML Metadata](../07-migration-files/toml-metadata.md) — `[RayMigrator]` header parameters (`Description`, `Environments`, `Targets`, `UseTransaction`, `RunAlways`, `RequireRollbackFile`, `StopRollbackOnMissingRollbackFile`, `MigrationErrorAction`, `RollbackErrorAction`, `UseCliToolAlias`, `TargetGroupMigrationOrder`)
- [Rollback Files](../07-migration-files/rollback-files.md) — `.rollback.sql` naming and structure
- [migsettings Files](../07-migration-files/migsettings-files.md) — Directory-level TOML overrides
- [Directory Structure](../07-migration-files/directory-structure.md) — Release/TargetGroup/file hierarchy
- [Environment-Specific Files](../07-migration-files/environment-specific.md) — Per-environment filename suffixes and discovery rules

---

## Database Support

Supported `DatabaseType` values: `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite`

| DatabaseType | Block Separator (`SqlBlockDelimiter`) | DDL Transactions | Driver Package |
|--------------|---------------------------------------|------------------|----------------|
| `SqlServer` | `GO` | Full | Microsoft.Data.SqlClient |
| `PostgreSQL` | `;` | Full | Npgsql |
| `MariaDb` | `;` | Limited (implicit commit on DDL) | MySqlConnector |
| `MySql` | `;` | Limited (implicit commit on DDL) | MySqlConnector |
| `Sqlite` | `;` | Full | Microsoft.Data.Sqlite |

Database-specific details (statement separators, DDL transaction support, schema conventions, drivers) are documented in [SQL Dialects](../03-database-layer/sql-dialects.md).

---

## Configuration File Loading Order

The 4-level file merge hierarchy is documented in [Configuration Hierarchy](../06-configuration-reference/appsettings-hierarchy.md).

---

## Settings Inheritance Chain

The defaults inheritance flow (`ProductDefaults` → `Product` → `TargetGroupDefaults` → etc.) is documented in [Settings & Inheritance Overview](../06-configuration-reference/settings-inheritance-overview.md#appsettings-defaults-inheritance).

---

## Environment Variables & Connection Strings

Environment variable placeholder syntax (`{ENV:VARIABLE_NAME}`) and connection string examples are documented in [Environment Variables](../06-configuration-reference/environment-variables.md).

---

## Error Handling Strategies

The five `MigrationErrorAction` strategies (`Terminate`, `Rollback`, `RollbackErrorOnly`, `RollbackRelease`, `Ignore`) and the `RollbackErrorAction` enum (`Terminate`, `Ignore`) are documented in [Error Handling](../02-core-concepts/error-handling.md).

---

## Execution Modes

The three run modes (Validate, Simulate, Migrate) and migration order (Simultaneously, Successively) are documented in [Execution Modes](../02-core-concepts/execution-modes.md).
