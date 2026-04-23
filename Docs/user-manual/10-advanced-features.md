# 10 — Power User Features

This chapter covers advanced RayMigrator features that give you fine-grained control over migration execution, validation, and multi-target deployments.

---

## TargetMigrationOrder: Simultaneously vs Successively

When a TargetGroup has multiple targets, the `TargetMigrationOrder` setting controls the order in which migrations are applied across those targets.

### Successively (Default)

All migrations run on one target before moving to the next.

```
Target: MainDB               Target: ReplicaDB
├── 001_CreateBooks.sql
├── 002_CreateAuthors.sql
│                            ├── 001_CreateBooks.sql
│                            └── 002_CreateAuthors.sql
```

- **Use when:** Targets are independent databases
- **Benefit:** If an error occurs, only one target is partially migrated

### Simultaneously

Each migration runs on ALL targets before the next migration starts.

```
001_CreateBooks.sql → MainDB
001_CreateBooks.sql → ReplicaDB
002_CreateAuthors.sql → MainDB
002_CreateAuthors.sql → ReplicaDB
```

- **Use when:** Targets are shards of the same logical database, and you want them in sync
- **Benefit:** All targets stay at the same migration level

### Configuration

```json
{
  "TargetGroups": [{
    "Alias": "Backend",
    "DatabaseType": "SqlServer",
    "TargetMigrationOrder": "Simultaneously",
    "Targets": [
      { "Alias": "Shard1", "ConnectionString": "..." },
      { "Alias": "Shard2", "ConnectionString": "..." }
    ]
  }]
}
```

For all enum values and detailed behavior, see [Execution Modes — Target Migration Order](../02-core-concepts/execution-modes.md#target-migration-order).

> **Tip:** If you are unsure which mode to use, start with `Successively`. It is the safer default because a failure only affects a single target.

---

## Hash Validation

RayMigrator stores a SHA-256 hash of each migration file when it is first executed. On subsequent runs, it compares the current file hash against the stored hash to detect unintended modifications.

### HashValidationScope Options

For all enum values, see [Hash Validation](../02-core-concepts/hash-validation.md). In brief: `File` (entire content, strictest), `SqlBlocks` (SQL only, allows TOML changes), `Disabled` (no validation).

### Configuration

```json
{
  "TargetGroups": [{
    "HashValidationScope": "File"
  }]
}
```

### When Files Change: Update-Hash Workflow

If you intentionally modify an already-executed migration file:

1. Run `Validate-Hash` to see which files have changed
2. Review the changes to ensure they are intentional
3. Run `Update-Hash` to update stored hashes

```bash
RayMigrator Validate-Hash -p BookStore -env Production
# Shows: "Hash mismatch for 001_CreateBooks.sql"

RayMigrator Update-Hash -p BookStore -env Production
# Updates stored hashes to match current files
```

> **Warning:** Only update hashes for intentional changes. Hash validation protects against accidental modifications to already-executed migrations.

### Validate-Hash in Practice

A typical pre-deployment validation step:

```bash
# Validate that no executed migration files have been tampered with
RayMigrator Validate-Hash -p BookStore -env Production

# Exit code 0 = all hashes match
# Non-zero = one or more files have changed
```

Integrate this into your CI/CD pipeline to catch unintended changes before they reach production.

---

## Baseline: Onboarding Existing Databases

When you introduce RayMigrator to a database that already has its schema in place, running Migrate-Up would fail — the tables, columns, and constraints already exist. Baseline solves this by recording migration files as "already applied" in the repository **without executing any SQL on the target database**. This lets you start tracking migrations going forward, using Migrate-Up only for genuinely new releases.

```
Before Baseline:
  Target DB has R1.0, R1.1, R1.2 applied manually
  Repository is empty → Migrate-Up would try to re-create everything

After Baseline (--to-release "Release 1.2"):
  Repository shows R1.0–R1.2 as Migrated
  Target DB unchanged → Migrate-Up now starts from R2.0
```

### CLI Syntax

```bash
RayMigrator Baseline --product <alias> --environment <env> [--to-release <release>] [--target-group <group>] [--TargetGroup-MigrationOrder <order>]
```

| Option | Alias | Required | Default | Description |
|---|---|---|---|---|
| `--product` | `-p` | Yes | — | Product alias from configuration |
| `--environment` | `-env` | Yes | — | Target environment |
| `--to-release` | `-tr` | No | all | Baseline up to this release (inclusive) |
| `--target-group` | `-tg` | No | all | Filter to specific target groups |
| `--TargetGroup-MigrationOrder` | `-tgmo` | No | — | Override target group processing order (comma-separated aliases, e.g. `"Frontend,Backend"`) |

For the full option reference, see [CLI Reference — Baseline](../08-cli-reference/command-reference.md#baseline).

### Full Baseline vs Partial Baseline

**Full Baseline** — omit `--to-release` to mark every migration file as migrated:

```bash
RayMigrator Baseline -p BookStore -env Production
```

Use this when the target database is completely up-to-date with all releases and you simply want to start tracking from this point.

**Partial Baseline** — use `--to-release` to mark only releases up to (and including) the specified version:

```bash
RayMigrator Baseline -p BookStore -env Production -tr "Release 1.2"
```

Use this when the target database is at a specific release level and you want Migrate-Up to apply everything after that release. This is the most common scenario when onboarding an existing database.

### How Baseline Works

Baseline follows a five-phase process, identical in structure to Migrate-Up but without executing any SQL on the target databases:

1. **Initialization** — RayMigrator ensures the repository tables exist (creating them if this is a first run), registers the product if needed, creates a new MigrationRun record with status `Running`, and cleans up any orphaned runs from prior interrupted executions.

2. **File Discovery** — All migration files are read from the configured `MigrationFilesRootDirectory`. TOML metadata blocks are parsed to extract release versions, environment filters, target filters, and other properties.

3. **Filtering** — The discovered files are narrowed down in three steps:
   - **Release filter**: If `--to-release` is set, only files up to and including that release are kept.
   - **Target group filter**: If `--target-group` is set, only matching target groups are kept.
   - **Already-migrated filter**: Files that already have a `Migrated` record in the repository are excluded. This is what makes Baseline idempotent.

4. **Recording** — For each remaining file+target combination, RayMigrator archives any prior migration history for that file, then writes a new repository record and immediately sets its status to `Migrated`. The recording respects the configured `TargetMigrationOrder`:
   - **Successively** (default): iterates target → file (each target receives all its files before moving to the next target).
   - **Simultaneously**: iterates file → target (each file is recorded against all its targets before moving to the next file).

   > **Important:** No SQL is executed on the target databases during this phase. Only the migration repository is written to.

5. **Finalization** — The MigrationRun record is updated to `Ok` and a summary is logged.

If any error occurs during these phases, the MigrationRun is marked as `Error` and the error is logged.

### Key Properties

#### No SQL on Target Databases

Baseline writes exclusively to the migration repository. Target databases are never contacted — no connections are opened, no tables are created, no data is modified. This is the fundamental difference between Baseline and Migrate-Up.

#### Idempotent

Running Baseline twice with the same parameters is safe. The second run detects that all files are already recorded as `Migrated` in the repository, filters them out, and completes with zero files processed. Both runs finish successfully.

#### Respects TargetMigrationOrder

Baseline uses the same execution order as Migrate-Up. If your target group is configured with `TargetMigrationOrder: Successively` (the default), Baseline records files in target → file order. If configured with `Simultaneously`, it uses file → target order. See [Execution Modes](07-execution-modes.md) for details.

### Repository State After Baseline

| Table | Content |
|---|---|
| Migration | One record per file+target combination, `MigrationStatusId = Migrated` |
| MigrationRun | One record, `MigrationRunResultId = Ok` |
| Target database | **Unchanged** — no tables created, no data inserted |

### Tutorial: Onboarding BookStore

Suppose the BookStore database already has all tables from Release 1.0 through Release 1.2, created manually or by another tool. You want RayMigrator to manage it from Release 2.0 onward.

**Step 1** — Prepare your migration files directory. It must contain the historical releases (1.0, 1.1, 1.2) as well as the new Release 2.0. RayMigrator needs to see the historical files to create repository records for them.

**Step 2** — Run Baseline for the existing releases:

```bash
RayMigrator Baseline -p BookStore -env Production -tr "Release 1.2"
```

**Step 3** — Verify the repository state with the Info command:

```bash
RayMigrator Info -p BookStore -env Production
```

This shows the current release as `Release 1.2`, the total migrations executed as the count of Releases 1.0–1.2 files, and Release 2.0 files counted as pending migrations.

**Step 4** — Apply the new release:

```bash
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate
```

RayMigrator skips Releases 1.0–1.2 (already baselined) and executes only Release 2.0.

**Step 5** — Confirm integrity:

```bash
RayMigrator Validate-Hash -p BookStore -env Production
```

> **Tip:** Always run Baseline in a staging environment first. Use the Info command to verify the repository records look correct before applying to production.

> **Caution:** Baseline does not verify that the target database actually matches the migration files. It trusts that the files accurately describe the current state of the database. If the database schema diverges from the migration files, subsequent Migrate-Up runs may fail.

### Target Group Filtering

Use `--target-group` to baseline only a specific target group:

```bash
RayMigrator Baseline -p BookStore -env Production -tg Backend
```

This is useful for incremental onboarding — for example, when the backend databases are ready for tracking but the reporting databases are still being set up. You can baseline each target group independently.

---

## Out-of-Order Execution

By default, RayMigrator requires migrations to be executed in strict sequential order. If an older migration file is discovered that was skipped, it is flagged as an error.

Use `--allow-out-of-order` / `-ooo` to allow executing older migrations that were previously skipped:

```bash
RayMigrator Migrate-Up -p BookStore -env Development -rm Migrate --allow-out-of-order
```

### When to Use

- **Parallel branch development** where teams add migrations independently
- **Hotfix branches** that create migrations with earlier sequence numbers
- **Feature branches** merged out of order

### Example Scenario

Team A creates `003_CreateReviews.sql` and merges it. Team B, working from an older branch, creates `002_CreatePublishers.sql` and merges later. Without `--allow-out-of-order`, RayMigrator would reject `002_CreatePublishers.sql` because `003` has already been applied.

```bash
# Allow the out-of-order migration
RayMigrator Migrate-Up -p BookStore -env Development -rm Migrate --allow-out-of-order
```

> **Caution:** Use out-of-order execution carefully. Ensure the skipped migration does not depend on later migrations that have already been applied.

---

## Multi-Target Setups

A single TargetGroup can have multiple targets. All targets in a group receive the same set of migrations:

```json
{
  "TargetGroups": [{
    "Alias": "Backend",
    "DatabaseType": "SqlServer",
    "TargetMigrationOrder": "Simultaneously",
    "Targets": [
      { "Alias": "Primary", "ConnectionString": "{ENV:PRIMARY_DB}" },
      { "Alias": "Reporting", "ConnectionString": "{ENV:REPORTING_DB}" },
      { "Alias": "Analytics", "ConnectionString": "{ENV:ANALYTICS_DB}" }
    ]
  }]
}
```

### Tutorial: Add a Second Target to BookStore

Add a reporting database to the BookStore configuration:

```json
{
  "TargetGroups": [{
    "Alias": "Backend",
    "DatabaseType": "SqlServer",
    "TargetMigrationOrder": "Simultaneously",
    "Targets": [
      { "Alias": "MainDB", "ConnectionString": "{ENV:BOOKSTORE_CONNECTION}" },
      { "Alias": "ReportingDB", "ConnectionString": "{ENV:BOOKSTORE_REPORTING_CONNECTION}" }
    ]
  }]
}
```

Now every migration runs on both MainDB and ReportingDB. With `TargetMigrationOrder` set to `Simultaneously`, both databases stay at the same migration level after each file.

### When to Use Multiple Targets

| Scenario | TargetMigrationOrder | Rationale |
|----------|---------------|-----------|
| Database shards | `Simultaneously` | Keep all shards in sync |
| Primary + read replicas | `Successively` | Replicas are independent |
| Multi-region databases | `Simultaneously` | Schema consistency across regions |
| Dev + test databases | `Successively` | Independent, failure isolation |

---

## Environment-Specific Migrations

Create migration files that only run in specific environments.

### Via TOML Header

```sql
/*
[RayMigrator]
Description = "Insert production seed data"
Environments = ["Production"]
*/

INSERT INTO [dbo].[Categories] VALUES ('Fiction'), ('Non-Fiction'), ('Technical');
```

The `Environments` array controls which environments this migration applies to:

| Value | Behavior |
|-------|----------|
| Key omitted | Runs in all environments (default) |
| `["*"]` | Explicit wildcard; runs in all environments |
| `["Production"]` | Runs only in Production |
| `["Development", "Staging"]` | Runs in Development and Staging |

### Via Filename Convention

You can also use the filename to indicate environment specificity:

- `005_InsertConfig.Production.sql` — only runs in Production
- `005_InsertConfig.sql` — runs in all environments

### Tutorial: BookStore Environment-Specific Seed Data

Create a development-only seed data migration:

```sql
/*
[RayMigrator]
Description = "Insert sample books for development"
Environments = ["Development"]
UseTransaction = true
*/

INSERT INTO [dbo].[Books] (Title, Author, ISBN, Price)
VALUES
    ('Sample Book 1', 'Dev Author', '000-0-00-000001-0', 0.00),
    ('Sample Book 2', 'Dev Author', '000-0-00-000002-0', 0.00);
```

This migration will be skipped when running against Production or Staging, but applied in Development.

---

## Target-Specific Migrations

The TOML `Targets` parameter records which targets a migration is intended for, but it is **metadata only** — it does not restrict which targets actually execute the migration at runtime. Every target in a target group receives every migration file regardless of the `Targets` value.

### Via TOML Header (Metadata Only)

```sql
/*
[RayMigrator]
Description = "Create reporting-only materialized view"
Targets = ["ReportingDB"]
*/

CREATE VIEW [dbo].[vw_BookSales] AS
SELECT b.Title, COUNT(*) AS SalesCount
FROM [dbo].[Sales] s
JOIN [dbo].[Books] b ON s.BookId = b.Id
GROUP BY b.Title;
```

The `Targets` value is stored in the repository as documentation for which target this migration was designed for, but all targets in the group will still execute it.

> **Note:** The `Targets` parameter is reserved for future runtime filtering. For actual target group filtering today, use the `--target-group` (`-tg`) CLI option. See [TOML Metadata](../07-migration-files/toml-metadata.md#target-filtering) for full details.

---

## TargetGroup Filtering

Use `--target-group` / `-tg` to run migrations only on specific target groups:

```bash
# Only migrate the Backend target group
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate -tg Backend

# Migrate multiple target groups
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate -tg Backend -tg Reporting
```

### When to Use

- **Different teams** manage different target groups
- **Staged rollouts** where you deploy to one target group before another
- **Troubleshooting** issues with a specific database group
- **Selective rollback** of a single target group

### Example: Staged Deployment

```bash
# Step 1: Deploy to non-critical databases first
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate -tg Reporting

# Step 2: Verify reporting database is healthy
# ... run smoke tests ...

# Step 3: Deploy to the primary backend
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate -tg Backend
```

---

## TargetGroup Execution Order

By default, target groups are processed in the order they appear in the `TargetGroups` array in configuration. Use the `--TargetGroup-MigrationOrder` (`-tgmo`) CLI option to override this order per command invocation.

```bash
# Process Frontend before Backend, regardless of configuration order
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate -tgmo "Frontend,Backend"
```

The execution order can also be set at a more permanent level:

- **Per product in `appsettings.json`** — set `TargetGroupMigrationOrder` on the product:
  ```json
  {
    "Products": [{
      "Alias": "BookStore",
      "TargetGroupMigrationOrder": "Frontend,Backend",
      "TargetGroups": [...]
    }]
  }
  ```
- **Per release in `migsettings.txt`** — set `TargetGroupMigrationOrder` in a release-level migsettings file:
  ```toml
  [RayMigrator]
  TargetGroupMigrationOrder = ["Frontend", "Backend"]
  ```

**Precedence (highest to lowest):** CLI `--TargetGroup-MigrationOrder` > release-level migsettings > `appsettings.json` product property > configuration array order.

**Rules:**
- All TargetGroup aliases for the product must be listed exactly once.
- Only applicable when the product has more than one target group.
- Applies to `Migrate-Up` and `Baseline` commands only.

### When to Use

- **Dependency ordering** — when one target group's schema must exist before another group can run its migrations
- **Priority ordering** — apply critical groups first, then less critical ones
- **Team conventions** — enforce a consistent deployment order regardless of how groups appear in configuration

---

## Target Release Filtering

Use `--to-release` / `-tr` to limit migration execution to a specific release:

```bash
# Only migrate up to Release 2.0 (skip Release 2.1 and beyond)
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate -tr "Release 2.0"
```

This is useful for:
- **Incremental deployments** where you roll out one release at a time
- **Controlled rollouts** where Release 2.1 is not yet approved for production
- **Testing** a specific release in isolation

---

## RunAlways Migrations

By default, a migration file is executed only once. If it has already been applied (status `Migrated` in the repository), it is skipped on subsequent runs. The `RunAlways` TOML property overrides this behavior: when set to `true`, the migration is re-executed on every run, even if it was previously applied.

### Via TOML Header

```sql
/*
[RayMigrator]
Description = "Refresh materialized views"
RunAlways = true
UseTransaction = false
*/

EXEC sp_refreshview 'dbo.vw_BookSales';
```

### Use Cases

- **Refreshing views or stored procedures** that must stay current
- **Re-seeding lookup data** that may change between releases
- **Running maintenance scripts** (index rebuilds, statistics updates)

### Via migsettings

You can also set `RunAlways = true` at the directory level using a `migsettings.txt` file. All migration files in that directory (and its subdirectories) inherit the setting unless overridden by a more specific migsettings file or by the file's own TOML header.

**`Release 9.0/Backend/migsettings.txt`**:
```toml
[RayMigrator]
RunAlways = true
```

> **Warning:** RunAlways files are re-executed on every migration run. Ensure they are idempotent -- they must produce the same result regardless of how many times they run.

---

## migsettings File Overrides

`migsettings.txt` files provide directory-wide TOML defaults for all migration files beneath them. They follow a hierarchical inheritance model where more specific (deeper) directories override less specific ones.

### File Names

| File | Scope |
|------|-------|
| `migsettings.txt` | Base settings for directory |
| `migsettings.{Environment}.txt` | Environment-specific overrides |

### Inheritance Hierarchy

Settings are merged from least specific to most specific. Each level can override properties from the level above:

1. Product-level `migsettings.txt` (lowest priority)
2. Product-level `migsettings.{Environment}.txt`
3. Release-level `migsettings.txt`
4. Release-level `migsettings.{Environment}.txt`
5. Target group `migsettings.txt`
6. Target group `migsettings.{Environment}.txt`
7. Migration file TOML header (highest priority)

### Supported Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `UseTransaction` | bool | `true` | Wrap migration in a database transaction |
| `RunAlways` | bool | `false` | Re-execute every migration run |
| `RequireRollbackFile` | bool? | inherits | Require a rollback file |
| `StopRollbackOnMissingRollbackFile` | bool? | inherits | Stop error-recovery rollback chain when rollback file is missing (only applies when `RequireRollbackFile=false`) |
| `Environments` | array | all | Allowed environments (omit key or use `["*"]` for all environments) |
| `Targets` | array | all | Intended target databases (metadata only; stored in repository but not used for runtime filtering) |
| `MigrationErrorAction` | string? | inherits | Error handling strategy |
| `RollbackErrorAction` | string? | inherits | Rollback error handling strategy |
| `UseCliToolAlias` | string? | inherits | CLI tool alias for executing migrations instead of the built-in DAL. References a `CliTools[].Alias` in `appsettings.json`. |
| `TargetGroupMigrationOrder` | array | inherits | Explicit TargetGroup execution order for this release (e.g., `["Frontend", "Backend"]`). Only meaningful when the product has more than one target group. Applies to `Migrate-Up` and `Baseline` commands. |

Note: `Description` is accepted in migsettings files but has no effect; it is only meaningful in migration file TOML headers.

### Example: Directory Structure

```
BookStore/
├── migsettings.txt                    # UseTransaction = true, RunAlways = false
├── migsettings.Production.txt         # MigrationErrorAction = Terminate
├── Release 1.0/
│   ├── Backend/
│   │   ├── migsettings.txt            # Targets = ["*"]
│   │   ├── migsettings.Docker.txt     # Environments = ["Docker"]
│   │   └── 001_CreateTable.sql
│   └── Frontend/
│       ├── migsettings.txt            # UseTransaction = false
│       └── 001_CreateViews.sql
└── Release 2.0/
    └── Backend/
        └── 001_AddColumn.sql
```

### Merge Behavior

Only properties that are **explicitly set** in a migsettings file override parent values; omitted properties are inherited unchanged. Arrays (`Environments`, `Targets`) are replaced entirely, not merged.

**Product `migsettings.txt`**:
```toml
[RayMigrator]
UseTransaction = true
RunAlways = false
Targets = ["*"]
```

**Target group `migsettings.txt`**:
```toml
[RayMigrator]
Targets = ["Primary", "Secondary"]
```

**Final merged settings**: `UseTransaction = true` (from product), `RunAlways = false` (from product), `Targets = ["Primary", "Secondary"]` (from target group -- replaces, not merges).

For full details, see [migsettings Files](../07-migration-files/migsettings-files.md).

---

## Database Logging

RayMigrator can optionally log migration events to a database table for centralized log storage and monitoring. Database logging is **in addition to** console and file logging (Serilog). It is activated by the presence of the `DatabaseLogging` section in configuration.

### Configuration

```json
{
  "RayMigrator": {
    "DatabaseLogging": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:LOG_CONNECTION}",
      "SchemaName": "logs",
      "TableBaseName": "",
      "MinimumLevel": "Information",
      "DbCommandTimeoutInSeconds": 20
    }
  }
}
```

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `DatabaseType` | string | Yes | - | Database type (SqlServer, PostgreSQL, MariaDb, MySql, Sqlite) |
| `ConnectionString` | string | Yes | - | Connection string (supports `{ENV:}` placeholders) |
| `SchemaName` | string | Conditional | - | Schema for logging tables (required for SqlServer, PostgreSQL; ignored for MariaDb, MySql, SQLite) |
| `TableBaseName` | string | No | - | Table name prefix |
| `MinimumLevel` | string | No | `Information` | Minimum log level (Trace, Debug, Information, Warning, Error, Critical, None) |
| `DbCommandTimeoutInSeconds` | int | No | `20` | Command timeout in seconds |

### How It Works

Database logging uses an asynchronous background queue (`DatabaseLoggerQueue`) to avoid blocking migration execution. Each log entry captures the current migration context: environment, release version, target group, target alias, filename, block ID, and more.

On first use, RayMigrator automatically creates the logging schema and tables (`MigrationEvent` and `MigrationLog`) if they do not exist.

### Same Database vs Separate Database

You can log to the same database as the repository (using a different schema) or to an entirely separate database:

```json
{
  "RayMigrator": {
    "Repository": {
      "ConnectionString": "{ENV:REPO_CONNECTION}",
      "SchemaName": "migrations"
    },
    "DatabaseLogging": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:REPO_CONNECTION}",
      "SchemaName": "logs"
    }
  }
}
```

### Querying Logs

```sql
-- Recent errors
SELECT CreatedAt, Environment, Filename, Message
FROM logs.MigrationLog
WHERE LogLevelId >= 4
ORDER BY CreatedAt DESC;

-- Logs for a specific migration run
SELECT e.Name AS Event, l.Message, l.Filename, l.CreatedAt
FROM logs.MigrationLog l
LEFT JOIN logs.MigrationEvent e ON l.MigrationEventId = e.Id
WHERE l.MigrationRunId = @RunId
ORDER BY l.CreatedAt;
```

> **Note:** `DatabaseLogging.MinimumLevel` uses `Microsoft.Extensions.Logging.LogLevel` values (Trace, Debug, Information, Warning, Error, Critical, None). The `Serilog` section uses Serilog's own level names (Verbose, Debug, Information, Warning, Error, Fatal). Do not mix them.

For full schema details, see [Logging Schema](../03-database-layer/logging-schema.md) and [Logging Options](../06-configuration-reference/logging-options.md).

---

## Retry and Resilience

RayMigrator includes built-in retry logic for transient database errors such as timeouts, connection losses, and deadlocks.

### Configuration

Retry settings are configured per target via `TargetDefaults` or individual `Targets`:

```json
{
  "RayMigrator": {
    "ProductDefaults": {
      "TargetGroupDefaults": {
        "TargetDefaults": {
          "DbCommandTimeoutInSeconds": 20,
          "DbCommandMaxRetries": 3,
          "DbCommandWaitTimeInMsBeforeRetry": 250
        }
      }
    }
  }
}
```

| Setting | Default (TargetDefaults) | Default (Repository) | Description |
|---------|--------------------------|----------------------|-------------|
| `DbCommandTimeoutInSeconds` | 20 | 60 | SQL command timeout |
| `DbCommandMaxRetries` | 0 (disabled) | 100 | Maximum retry attempts |
| `DbCommandWaitTimeInMsBeforeRetry` | 250 | 250 | Base delay in ms (linear backoff) |

Retries use **linear backoff**: the delay increases by the base value on each attempt (e.g., with `DbCommandWaitTimeInMsBeforeRetry=500`: 500ms, 1000ms, 1500ms, ...).

### Recognized Transient Errors

RayMigrator automatically detects transient errors for all supported database engines:

- **SQL Server**: Timeout, connection lost, Azure SQL throttling errors
- **PostgreSQL**: Connection exceptions, server shutdown, serialization failures, deadlocks
- **MariaDB / MySQL**: Too many connections, lock/deadlock errors, connection errors
- **SQLite**: SQLITE_BUSY, SQLITE_LOCKED

`TimeoutException` is recognized as transient regardless of the database provider.

### Block-Level Recovery

If a migration file with multiple SQL blocks fails partway through, RayMigrator records which blocks were completed. On re-run, it automatically resumes from the last failed block rather than re-executing the entire file.

> **Tip:** Target databases default to 0 retries (disabled) to avoid masking persistent errors in migration files. Repository operations default to 100 retries because repository availability is critical for state tracking.

For full details, see [Resilience and Recovery](../02-core-concepts/resilience.md).

---

## Fix: Repository Repair

The `Fix` command resolves repository inconsistencies such as orphaned migration runs (runs left in "Running" status due to a crash or interrupted process).

### Basic Usage

```bash
# Fix orphaned runs older than 60 minutes (default)
RayMigrator Fix -p BookStore -env Production

# Fix all orphaned runs immediately (no age threshold)
RayMigrator Fix -p BookStore -env Production --older-than 0

# Preview what would be fixed without applying changes
RayMigrator Fix -p BookStore -env Production --dry-run
```

### Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--scope` | `-s` | `OrphanedRuns` | Fix scope: `OrphanedRuns` or `All` |
| `--older-than` | `-ot` | `60` | Only fix runs older than N minutes (0 = immediate) |
| `--dry-run` | - | `false` | Preview mode -- show what would be fixed without applying |
| `--last-migration-status` | `-lms` | `not-migrated` | Status for orphaned migrations: `not-migrated` (re-execute next time) or `migrated` (skip next time) |

### When to Use

- **Process crash**: The migration process was killed or crashed, leaving a run in "Running" status
- **Stuck migrations**: A migration run appears hung and the process is no longer running
- **CI/CD recovery**: A pipeline step was cancelled mid-migration

> **Tip:** Always use `--dry-run` first to review what will be changed before applying the fix.

---

## CLI Tool Execution Mode

By default, RayMigrator executes migration SQL using its built-in Data Access Layer (DAL). CLI tool execution mode lets you delegate SQL execution to an external command-line tool such as `sqlcmd`, `psql`, `mysql`, `mariadb`, or `sqlite3`. This is useful when migrations require features that the built-in DAL does not support (e.g., `sqlcmd` variables, `psql` metacommands) or when organizational policy requires using a specific client tool.

### How It Works

1. Define one or more CLI tools in the `CliTools` array at the `RayMigrator` root level in configuration.
2. Reference a tool by setting `UseCliToolAlias` at any level: `ProductDefaults`, `Product`, `TargetGroup`, `Target`, migsettings file, or TOML header.
3. At execution time, RayMigrator calls the external tool for each migration file instead of using the built-in DAL.

### Configuration Example

```json
{
  "RayMigrator": {
    "CliTools": [{
      "Alias": "sqlcmd-tool",
      "ExecutablePath": "sqlcmd",
      "ArgumentTemplate": "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath} -b",
      "InputMode": "File",
      "SuccessExitCodes": ["0"],
      "CliToolTimeoutInSeconds": 120
    }],
    "ProductDefaults": {
      "UseCliToolAlias": "sqlcmd-tool"
    },
    "Products": [{
      "Alias": "BookStore",
      "MigrationFilesRootDirectory": "./Migrations",
      "TargetGroups": [{
        "Alias": "Backend",
        "DatabaseType": "SqlServer",
        "Targets": [{
          "Alias": "MainDB",
          "ConnectionString": "{ENV:BOOKSTORE_CONNECTION}",
          "CliToolParameters": {
            "Server": "localhost",
            "User": "sa",
            "Password": "{ENV:SA_PASSWORD}",
            "Database": "BookStore"
          }
        }]
      }]
    }]
  }
}
```

### UseCliToolAlias Inheritance

`UseCliToolAlias` follows the same cascading inheritance as other settings:

```
ProductDefaults.UseCliToolAlias
  → Product.UseCliToolAlias
    → TargetGroup.UseCliToolAlias
      → Target.UseCliToolAlias
        → migsettings UseCliToolAlias
          → TOML header UseCliToolAlias (highest priority)
```

### InputMode

| Mode | Description |
|------|-------------|
| `File` (default) | The migration file path is passed as a command-line argument via the `{FilePath}` placeholder in `ArgumentTemplate`. |
| `Stdin` | The migration file content is piped to the tool via standard input. Used by tools like `mysql` and `mariadb`. |

### Per-File Override via TOML

A specific migration file can use a different CLI tool (or revert to the built-in DAL) by setting `UseCliToolAlias` in its TOML header:

```sql
/*
[RayMigrator]
Description = "Execute with psql for COPY command support"
UseCliToolAlias = "psql-tool"
*/

COPY books FROM '/data/books.csv' CSV HEADER;
```

For the full property reference, see [CLI Tools Options](../06-configuration-reference/cli-tools-options.md).

---

[Next: Production Operations](11-operations-guide.md)
