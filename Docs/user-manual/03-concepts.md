# Chapter 3 — Core Concepts

This chapter explains the foundational ideas behind RayMigrator. Understanding these concepts will make every subsequent chapter — configuration, migration authoring, error handling, rollbacks — straightforward.

## The Product / TargetGroup / Target Hierarchy

RayMigrator organizes databases into a three-level hierarchy:

```
Product (BookStore)
├── TargetGroup (Backend) — DatabaseType: SqlServer
│   ├── Target (MainDB)      — Connection: server1
│   └── Target (ReplicaDB)   — Connection: server2
└── TargetGroup (Analytics) — DatabaseType: PostgreSQL
    └── Target (WarehouseDB) — Connection: server3
```

### Product

A **Product** represents your application or service. It has an alias (e.g., `BookStore`), a root directory for migration files, and one or more target groups. Most teams have one product per repository, but RayMigrator supports multiple products in a single configuration.

### TargetGroup

A **TargetGroup** is a collection of databases that share the same database engine and receive the same set of migration files. All targets within a group must use the same `DatabaseType` (SqlServer, PostgreSQL, MariaDb, MySql, or Sqlite).

The TargetGroup alias must match a subdirectory name in your migration files directory structure:

```
Migrations/
└── Release 1.0/
    ├── Backend/          ← matches TargetGroup alias "Backend"
    │   └── 001_CreateBooks.sql
    └── Analytics/        ← matches TargetGroup alias "Analytics"
        └── 001_CreateEvents.sql
```

### Target

A **Target** is an individual database connection. Each target has its own alias and connection string. When RayMigrator processes a TargetGroup, it applies every pending migration to every target in the group.

This is what makes multi-target deployment possible: define two targets in the same group, and one `migrate-up` command applies the same migrations to both databases.

For complete configuration of each level, see [Product Options](../06-configuration-reference/product-options.md).

## Releases

A **Release** is a versioned directory of migration files. Releases are the top-level directories inside your migration root:

```
Migrations/
├── Release 1.0/
│   └── Backend/
│       ├── 001_CreateBooks.sql
│       └── 002_CreateAuthors.sql
├── Release 1.1/
│   └── Backend/
│       └── 001_AddBookCategory.sql
└── Release 2.0/
    └── Backend/
        └── 001_CreateOrders.sql
```

Releases are processed in **alphabetical order**. Within each release, migration files are processed in alphabetical order by filename. This is why the naming convention matters:

| Convention | Example | Notes |
|------------|---------|-------|
| Release naming | `Release 1.0`, `Release 1.1` | Alphabetical sort determines order |
| File naming | `001_CreateBooks.sql`, `002_CreateAuthors.sql` | Numeric prefix controls sequence |

> **Warning:** Because ordering is alphabetical, `Release 10.0` sorts *before* `Release 2.0` (the character `1` comes before `2`). Use zero-padded names or consistent formatting to avoid surprises. For example: `Release 01.0`, `Release 02.0`, `Release 10.0`.

### Targeted Releases

The `--to-release` CLI flag lets you migrate up to (and including) a specific release, stopping before later ones. This is useful for staged deployments.

## The Repository

The **Repository** is a database (or schema within a database) where RayMigrator stores all tracking information. It can live in the same database as your application or in a separate database. The schema name is configurable via `Repository.SchemaName` in `appsettings.json` (e.g., `"migrations"` or `"ray"`). See [Repository Options](../06-configuration-reference/repository-options.md) for configuration details.

The repository contains these tables:

| Table | Purpose |
|-------|---------|
| `MigratorMeta` | Tracks repository schema version, database type, and the RayMigrator application version that created it. |
| `Product` | One record per product alias. Links migrations to the product they belong to. |
| `Environment` | One record per environment value (e.g., Development, Production). Referenced by `MigrationRun`, `MigrationRecord`, and `MigrationRecordHistory`. |
| `MigrationRun` | One record per execution of `migrate-up` or `migrate-down`. Records start time, end time, and overall result. |
| `MigrationRunMeta` | One-to-one with `MigrationRun`. Stores the serialized settings JSON and an optional description. |
| `MigrationRecord` | One record per migration file per target. Records file path, hash, status, execution timestamps. |
| `MigrationRecordHistory` | Audit log for MigrationRecord entries. Each time a migration reaches a terminal state (Migrated, Failed, or NotMigrated), the current state of that `MigrationRecord` row is copied here. The original `MigrationRecord` is retained and may be reused (reset) on future runs. See [Repository Schema](../03-database-layer/repository-schema.md). |
| `MigrationRunMode` | Lookup table for run modes: Validate (10), Simulate (20), Migrate (100). |
| `MigrationOperation` | Lookup table for operation types: Rollback (5), MigrateDown (50), MigrateUp (100). |
| `MigrationRunResult` | Lookup table for run outcomes: Running (10), Error (90), Ok (100). |
| `MigrationStatus` | Lookup table for per-file statuses (see next section). |

The repository is created automatically on the first migration run. RayMigrator checks for the configured schema and creates it along with all required tables if they do not exist.

> **Tip:** The repository provides the audit trail. To see the history of all migration runs, query the `MigrationRun` table in your configured repository schema. To see which files have been applied to which targets, query the `MigrationRecord` table.

## Migration Status Lifecycle

Every migration file tracked in the repository has a **status** that moves through a defined lifecycle:

```
Pending ──────► Executing ──────► Migrated
                    │
                    ├──────► Failed
                    │         (manual fix needed)
                    │
                    └──────► NotMigrated
                              (after rollback)
```

The status values and their numeric IDs:

| Status | ID | Meaning |
|--------|----|---------|
| Undefined | 0 | Initial state, should not appear in normal operation |
| Pending | 10 | File discovered but not yet executed in the current run |
| Executing | 20 | SQL is currently being executed against the target |
| Failed | 30 | Execution failed; database state is unclear |
| NotMigrated | 50 | File is not deployed on target database (rolled back or never executed) |
| Migrated | 100 | SQL executed successfully; the migration is applied |

Understanding this lifecycle is essential for troubleshooting. A `Failed` status means the SQL did not complete — you need to examine the error, fix the issue (either in the database or the migration file), and determine the correct recovery action. Chapter 8 covers error handling in detail.

> **Note:** A migration with status `Migrated` has its SHA-256 hash stored. If the file is later modified, `validate-hash` will detect the discrepancy. Never modify a migration file that has already been executed — create a new file instead.

## TOML Metadata

A migration file is just SQL. The TOML header is entirely optional.

The simplest valid migration file has no header:

```sql
CREATE TABLE ...
```

Add a header only when you want to provide a description or override a default:

```sql
/*
[RayMigrator]
Description = "Create Books table"
*/

CREATE TABLE ...
```

The TOML header controls how RayMigrator processes the file:

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `Description` | string | `""` | Human-readable description, stored in repository and shown in logs |
| `Environments` | string array | all environments (omit parameter) | Which environments this file applies to. Omit to run everywhere, or specify `["Production"]` to restrict. |
| `Targets` | string array | all targets (omit parameter) | Intended targets for this file (metadata only; stored in repository but not used for runtime filtering) |
| `UseTransaction` | boolean | `true` | Wrap execution in a database transaction |
| `RunAlways` | boolean | `false` | Execute on every run, even if previously migrated |

Without a header, all defaults apply: the file runs in all environments, applies to all targets, wraps execution in a transaction, and executes only once.

Additional TOML parameters (`RequireRollbackFile`, `StopRollbackOnMissingRollbackFile`, `MigrationErrorAction`, `RollbackErrorAction`, `UseCliToolAlias`, `TargetGroupMigrationOrder`) are available for per-file overrides. Chapter 5 covers all TOML metadata in full detail, including advanced filtering scenarios. For the complete specification, see [TOML Metadata Reference](../07-migration-files/toml-metadata.md).

## Execution Flow

When you run `migrate-up`, RayMigrator follows this sequence:

```
1. Load configuration
   └── Read appsettings.json
   └── Replace {ENV:...} placeholders with environment variable values
   └── Validate configuration (required fields, connection strings)

2. Discover migration files
   └── Scan MigrationFilesRootDirectory
   └── Match subdirectories to TargetGroup aliases
   └── Sort releases alphabetically, files alphabetically within each release

3. Filter by environment
   └── Read TOML metadata from each file
   └── Exclude files that don't match the current --environment
   └── Exclude environment-specific files (e.g., .Development.sql) that don't match

4. Compare with repository
   └── Query ray.MigrationRecord for previously executed files
   └── Compare file hashes (if HashValidationScope is enabled)
   └── Identify pending migrations (not yet executed)

5. Execute SQL on target databases
   └── For each release, for each TargetGroup:
       └── For each pending file and each target (order depends on TargetMigrationOrder):
           └── Open connection to target database
           └── Begin transaction (if UseTransaction = true)
           └── Split SQL into statement blocks (GO for SqlServer, ; for others)
           └── Execute each block
           └── Commit or rollback transaction

6. Record results in repository
   └── Write MigrationRecord row (status, hash, timestamps)
   └── Update MigrationRun record (end time, overall result)
```

The order in which targets are processed within a TargetGroup depends on the `TargetMigrationOrder` setting:

- **Simultaneously**: For each migration file, apply it to all targets in the group before moving to the next file (file-first loop: file -> target).
- **Successively**: For each target, apply all migration files before moving to the next target (target-first loop: target -> file).

Chapter 10 covers TargetMigrationOrder in detail.

## Tying It All Together: The BookStore Example

In the Quick Start (Chapter 2), you created:

- **Product**: `BookStore` (alias in configuration)
- **TargetGroup**: `Backend` (SqlServer, one target)
- **Target**: `MainDB` (single database connection)
- **Release**: `Release 1.0` (one migration file)
- **Migration file**: `001_CreateBooks.sql` with TOML metadata

As we continue building the BookStore example in later chapters, we will add:

- A second target (ReplicaDB) to see multi-target execution
- A second target group (Analytics, PostgreSQL) to see multi-engine support
- Rollback files and error handling strategies
- Environment-specific migrations for Development vs Production

## What's Next

With these concepts understood, you are ready to explore the full configuration system — how to define multiple products, customize defaults, and use environment variables effectively.

**Next:** [Chapter 04 — Configuration Guide](04-configuration.md)
