# Configuration and Migration Examples

This directory contains ready-to-use configuration files and migration file examples for RayMigrator.

## Complete Example Products

The `Examples/` directory at the repository root contains two fully-runnable example products with Docker infrastructure. Use these to explore RayMigrator with real databases before building your own product.

See [Examples/README.md](../../Examples/README.md) for setup instructions and the complete file inventory.

### MySimpleApplication

Located in `Examples/MySimpleApplication/`. Demonstrates a **flat layout**: a single `ApplicationTargetGroup` targeting one SQL Server database (`SimpleApplicationDB`). Migration files live directly inside each release folder — no TargetGroup subdirectories.

Configuration: `Examples/MySimpleApplication/appsettings.MySimpleApplication.Docker.json`

Three releases with full rollback coverage:
- `Release 1.0` — Customer and Order tables
- `Release 1.1` — AddCustomerEmail column, sample data inserts
- `Release 2.0` — Product table, OrderDetails table

Port: SQL Server on `localhost:14330` (Docker-mapped).

### MyComplexApplication

Located in `Examples/MyComplexApplication/`. Demonstrates a **nested layout**: two TargetGroups across two different database engines.

- **FrontendTargetGroup** (PostgreSQL, `localhost:54320`, database `FrontendDB`) — user profiles, sessions, preferences, notifications, audit logs, dashboard widgets. Each release directory contains a `migsettings.txt` that sets `RequireRollbackFile = false`.
- **BackendTargetGroup** (SQL Server, `localhost:14330`, database `BackendDB`) — financial accounts, transactions, reporting views, indexes. Full rollback files for every migration.

Configuration: `Examples/MyComplexApplication/appsettings.MyComplexApplication.Docker.json`

Three releases:
- `Release 1.0` — foundational tables for both TargetGroups
- `Release 1.1` — account status columns, account types lookup data, user preferences, notifications
- `Release 2.0` — reporting views, transaction indexes, audit log, dashboard settings

The shared migration repository (`RayMigratorRepository`) is tracked in SQL Server and uses schema `ray`.

### Docker Infrastructure

Located in `Examples/Docker/`. Provides Docker Compose configuration for SQL Server and PostgreSQL containers. The SQL Server container pre-creates three databases on startup: `RayMigratorRepository`, `SimpleApplicationDB`, and `BackendDB`. The PostgreSQL container creates `FrontendDB`.

Start all containers:

```bash
# Windows (PowerShell)
.\Examples\Docker\RunDocker.default.all.ps1

# macOS / Linux
./Examples/Docker/run-docker-all.sh
```

Required environment variables before running migrations:

```bash
export SA_PASSWORD="P@ssw0rd!"
export POSTGRES_EXAMPLE_PASSWORD="raypass123"
```

See [10-testing/test-infrastructure.md](../10-testing/test-infrastructure.md) for the equivalent test Docker setup.

## Configuration Examples

### [appsettings.minimal.json](appsettings.minimal.json)

The smallest valid configuration for a single SQL Server product with one target group and one target. Use this as a starting point for new projects.

Key characteristics:
- Single product (`MyProduct`) with one target group (`Backend`) pointing to one SQL Server database
- `MigrationErrorAction`: `Terminate` — stops on first error, no automatic rollback
- `TargetMigrationOrder`: `Successively` — runs migrations file-by-file across targets in sequence
- `HashValidationScope`: `File` — validates the entire file hash on each run
- All connection strings and paths use `{ENV:...}` placeholders
- Configured timeouts: 30 s command timeout, no retries (system defaults are 20 s and 0 retries; see [Target Options](../06-configuration-reference/target-options.md))

See [06-configuration-reference/appsettings-hierarchy.md](../06-configuration-reference/appsettings-hierarchy.md) for the full file loading order.

### [appsettings.complete.json](appsettings.complete.json)

A comprehensive configuration demonstrating all major options: repository with retry settings, database logging, CLI tool definitions, product-level overrides, multiple target groups with different database types, and a full Serilog configuration with console and file sinks.

Key characteristics:
- Repository configured with retry (`DbCommandMaxRetries: 3`, `DbCommandWaitTimeInMsBeforeRetry: 500 ms`)
- `DatabaseLogging` section routes log entries to a SQL Server `logs` schema
- `CliTools` array defines two external CLI tools (`sqlcmd-tool` for SQL Server, `psql-tool` for PostgreSQL) with `ArgumentTemplate` placeholders, `InputMode`, exit code mapping, and timeout
- `ProductDefaults` sets `MigrationErrorAction: Terminate`, `RollbackErrorAction: Terminate`, `StopRollbackOnMissingRollbackFile: true`, and `UseCliToolAlias: null` (uses built-in DAL by default) as the baseline
- Product `MyApplication` explicitly sets `MigrationErrorAction` to `Rollback`, `RollbackErrorAction` to `Terminate` (overriding `ProductDefaults`), and `TargetGroupMigrationOrder` to `"Backend, Analytics"` to pin the execution order
- Two target groups:
  - `Backend` (SQL Server, `Successively`, two targets with retry, `UseCliToolAlias: sqlcmd-tool` with per-target `CliToolParameters`)
  - `Analytics` (PostgreSQL, `Simultaneously`, `HashValidationScope: SqlBlocks`, `UseCliToolAlias: psql-tool` with per-target `CliToolParameters`)
- Serilog writes to both Console (ANSI theme) and a rolling File sink

See [06-configuration-reference/product-options.md](../06-configuration-reference/product-options.md), [06-configuration-reference/target-group-options.md](../06-configuration-reference/target-group-options.md), and [06-configuration-reference/cli-tools-options.md](../06-configuration-reference/cli-tools-options.md) for all available keys.

### [appsettings.docker.json](appsettings.docker.json)

A development/CI configuration for running against Docker containers. The `{ENV:SA_PASSWORD}` placeholder keeps credentials out of source control while using literal host/port values for the container addresses.

Key characteristics:
- Repository and database logging both point to `localhost:1433` (Docker-mapped port)
- Product `RayMigratorTests` with two SQL Server target groups: `Backend` (two targets) and `Frontend` (one target)
- `MinimumLevel: Debug` for detailed output during testing
- Migration files served from `Testing/MigrationFiles/RayMigratorTests` (relative path)

See [10-testing/test-infrastructure.md](../10-testing/test-infrastructure.md) for Docker container setup.

### [appsettings.docker-cli.json](appsettings.docker-cli.json)

A Docker CLI tool configuration demonstrating how to execute migration files through database CLI tools running inside Docker containers instead of using the built-in DAL. Shows both input modes in one config across four database engines (PostgreSQL, MariaDB, SQL Server, and MySQL).

Key characteristics:
- `CliTools` array with 4 entries: PostgreSQL (Stdin), MariaDB (Stdin), SQL Server (Stdin), and MySQL (File mode via bash wrapper)
- **Stdin mode** (`InputMode: Stdin`): uses `docker` as `ExecutablePath` with `docker exec -i <container>` to pipe migration file content into the container's CLI tool
- **File mode** (`InputMode: File`): uses `/bin/bash` as `ExecutablePath` with a `cat '{FilePath}' | docker exec -i ...` wrapper to bridge host files into the container
- Product `DockerCliDemo` with 4 target groups, each pointing to a different database engine with `UseCliToolAlias` and per-target `CliToolParameters`
- psql uses `--set ON_ERROR_STOP=1` (required — without it psql exits 0 on SQL errors)
- sqlcmd uses `-b` (abort on error) and `-C` (trust server certificate)
- All secrets via `{ENV:...}` placeholders

See [06-configuration-reference/cli-tools-options.md](../06-configuration-reference/cli-tools-options.md) for all CLI tool options and the Docker execution guide.

## Migration File Examples

All migration file examples are in the [migration-examples/](migration-examples/) subdirectory.

### [migration-examples/001_CreateTable.sql](migration-examples/001_CreateTable.sql)

Standard DDL migration: creates a `Users` table with indexes. The TOML header contains only a `Description`; all other settings use their defaults (`UseTransaction = true`, all environments, all targets). The directory-level `migsettings.txt` sets `Targets = ["*"]`, `Environments = ["*"]`, and `UseTransaction = true` explicitly, so those defaults are also inherited by this file.

### [migration-examples/001_CreateTable.rollback.sql](migration-examples/001_CreateTable.rollback.sql)

Rollback file for `001_CreateTable.sql`. Uses `DROP INDEX IF EXISTS` / `DROP TABLE IF EXISTS` so the rollback is idempotent. The `.rollback.` pre-extension matches the default `MigrationRollbackFilesPreExtension` setting.

### [migration-examples/002_InsertMasterData.sql](migration-examples/002_InsertMasterData.sql)

DML + DDL migration that adds a `UserRoles` table and inserts lookup data, then adds a foreign key to `Users`. Demonstrates file-level overrides: `MigrationErrorAction = "Rollback"`, `RollbackErrorAction = "Terminate"`, and `RequireRollbackFile = true` set directly in the TOML header.

### [migration-examples/003_EnvironmentSpecific.Production.sql](migration-examples/003_EnvironmentSpecific.Production.sql)

Environment-scoped migration that only executes in the `Production` environment. The `.Production.` segment in the filename is the environment filter; the TOML header confirms `Environments = ["Production"]`.

See [07-migration-files/environment-specific.md](../07-migration-files/environment-specific.md) for the naming convention.

### [migration-examples/004_MultiBlockMigration.sql](migration-examples/004_MultiBlockMigration.sql)

SQL Server multi-block migration using `GO` as a block separator. Contains four blocks: a stored procedure, a scalar function, a view, and a `GRANT` statement. Uses `UseTransaction = false` because some DDL operations (e.g. `CREATE FUNCTION`) cannot run inside a transaction.

See [04-service-layer/block-execution.md](../04-service-layer/block-execution.md) for how SQL blocks are parsed and executed.

### [migration-examples/migsettings.txt](migration-examples/migsettings.txt)

Directory-level default settings file. Settings defined here apply to every migration file in the same directory and can be overridden by individual file headers. The `[RayMigrator]` section follows the same TOML format as individual migration file headers. Includes commented-out examples for `MigrationErrorAction`, `RollbackErrorAction`, `StopRollbackOnMissingRollbackFile`, `UseCliToolAlias`, and `TargetGroupMigrationOrder` (the last one is only effective when placed in a release-level directory).

See [07-migration-files/migsettings-files.md](../07-migration-files/migsettings-files.md) for the full specification.
