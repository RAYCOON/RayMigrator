# Chapter 1 — What is RayMigrator?

## The Problem: Schema Drift and Manual Scripts

Every application backed by a relational database faces the same challenge: the database schema must evolve alongside the application code. Without a disciplined process, teams run into familiar pain points:

- **Schema drift** — development, staging, and production databases diverge because changes are applied manually and inconsistently.
- **Lost scripts** — ad-hoc SQL files live on individual machines, in email threads, or in chat messages. Nobody knows which scripts have been executed where.
- **Team coordination failures** — two developers modify the same table in conflicting ways, and the conflict is only discovered during deployment.
- **No rollback path** — when a migration fails in production, there is no automated way to undo the damage.
- **No audit trail** — compliance and debugging require knowing *who* ran *what* on *which database* and *when*. Manual processes cannot provide this.

These problems compound as teams grow, as the number of databases increases, and as deployment frequency rises.

## What RayMigrator Does

RayMigrator is a professional database migration framework that brings order to this chaos. It manages **versioned, tracked, multi-database migrations** with built-in rollback support.

You organize your SQL scripts into numbered files inside release directories. RayMigrator scans those directories, determines which migrations have not yet been applied, executes them against your target databases in the correct order, and records every action in a dedicated repository database. If something goes wrong, configurable error strategies control whether to terminate, skip, or roll back.

## Key Differentiators

### Multi-Target Execution

A single migration file can be applied to multiple database instances simultaneously. Deploy the same schema change to your primary database and all read replicas in one command.

### Multi-Engine Support

RayMigrator supports five database engines from a single configuration. You can manage SQL Server, PostgreSQL, MariaDB, MySQL, and SQLite databases within the same product, each receiving engine-appropriate SQL.

### Rollback Support

Every migration can have a companion `.rollback.sql` file. When a migration fails, RayMigrator can automatically execute rollback scripts for all previously successful migrations in the current run — restoring databases to their pre-migration state.

### Configurable Error Strategies

Choose what happens when a migration fails: terminate immediately, roll back the entire run, roll back only the failed file, roll back only the current release, or skip the failed file and continue. Different products can use different strategies, and individual migration files can override the product-level setting via TOML metadata. A separate `RollbackErrorAction` controls behavior when a rollback itself fails.

### Hash Validation

RayMigrator computes a SHA-256 hash of every migration file and stores it in the repository. Before each run, it can validate that previously executed files have not been modified — catching accidental or unauthorized changes before they cause inconsistencies.

### Environment-Specific Migrations

TOML metadata in each migration file can restrict execution to specific environments (e.g., only Development, only Production) or specific targets. One set of migration files serves all environments with fine-grained control.

### Repository Tracking

A dedicated repository schema records every migration run, every file executed, its status, its hash, and timestamps. This provides a complete audit trail for compliance, debugging, and operational visibility.

## Supported Databases

| Database   | Statement Separator | DDL Transaction Support | Role                         |
|------------|---------------------|-------------------------|------------------------------|
| SQL Server | `GO`                | Full                    | Migration target, Repository |
| PostgreSQL | `;`                 | Full                    | Migration target, Repository |
| MariaDB    | `;`                 | Limited                 | Migration target, Repository |
| MySQL      | `;`                 | Limited                 | Migration target, Repository |
| SQLite     | `;`                 | Full                    | Migration target, Repository |

> **Note:** "Limited" DDL transaction support means that DDL statements (CREATE TABLE, ALTER TABLE, etc.) cause an implicit commit in MariaDB and MySQL. DML statements (INSERT, UPDATE, DELETE) are fully transactional on all engines.

For detailed dialect information, see [SQL Dialects](../03-database-layer/sql-dialects.md).

## System Requirements

| Requirement       | Minimum                                |
|-------------------|----------------------------------------|
| Runtime           | .NET 8 or later                        |
| RAM               | 4 GB                                   |
| Disk space        | 4 GB                                   |
| Operating system  | Windows 10+, Ubuntu 24.04+, macOS 10.15+ |

Pre-built binaries for all supported platforms are available from the [GitHub Releases](https://github.com/RAYCOON/RayMigrator/releases) page. See [Chapter 2 — Quick Start](02-quick-start.md) for installation instructions.

## Architecture at a Glance

```
                    appsettings.json
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│         CLI (Console Layer)                             │
│                                                         │
│    Migrate-Up · Migrate-Down · Baseline                 │
│   Validate-Hash · Update-Hash · Info · Fix              │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────┐
│            Service Layer                    │
│   File discovery · TOML parsing · Ordering  │
│   Hash validation · Error handling          │
└────────┬─────────────────────────┬──────────┘
         │                         │
         ▼                         ▼
┌──────────────────┐    ┌──────────────────────┐
│  Target Database │    │  Repository Database │
│  (your app DB)   │    │  (configured schema) │
│                  │    │  MigrationRun        │
│  Books, Users,   │    │  MigrationRecord     │
│  Orders, ...     │    │  MigrationRunResult  │
│                  │    │  MigrationStatus     │
└──────────────────┘    └──────────────────────┘
```

Configuration flows in through `appsettings.json`. The CLI parses commands and delegates to the service layer, which discovers migration files, filters and orders them, and executes SQL against your target databases. Every action is recorded in the repository database under the configured schema (set via `Repository.SchemaName` in `appsettings.json`).

## What's Next

Ready to see it in action? The next chapter walks you through creating and running your first migration in under 10 minutes.

**Next:** [Chapter 02 — Quick Start](02-quick-start.md)
