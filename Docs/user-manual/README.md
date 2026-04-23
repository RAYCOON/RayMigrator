# RayMigrator User Manual

This manual guides new users from zero to productive with RayMigrator. It complements the technical reference documentation in the [`Docs/`](../) folder by providing a structured, tutorial-driven path through installation, configuration, migration authoring, and production deployment. A running "BookStore" example is built progressively across chapters so you can follow along hands-on.

## Target Audience

- **Developers** writing and testing database migrations during application development
- **DevOps engineers and DBAs** managing migration deployments across environments and database engines

## Chapter Overview

| Chapter | Title | You'll Learn |
|---------|-------|--------------|
| [01](01-introduction.md) | What is RayMigrator? | The problem it solves, key features, supported databases, system requirements |
| [02](02-quick-start.md) | Quick Start | Create and run your first migration in 10 minutes (BookStore tutorial begins) |
| [03](03-concepts.md) | Core Concepts | Product/TargetGroup/Target hierarchy, releases, repository, migration lifecycle |
| [04](04-configuration.md) | Configuration Guide | appsettings.json structure, environment variables, inheritance chain |
| [05](05-migration-files.md) | Writing Migration Files | TOML metadata, SQL conventions, rollback files, naming and ordering |
| [06](06-cli-commands.md) | CLI Command Reference | Migrate-Up, Migrate-Down, Validate-Hash, Update-Hash, Info, Baseline, Fix |
| [07](07-execution-modes.md) | Execution Modes | Validate, Simulate, Migrate workflow and CI/CD integration |
| [08](08-error-handling.md) | Error Handling | MigrationErrorAction strategies (Terminate, Rollback, RollbackErrorOnly, RollbackRelease, Ignore), RollbackErrorAction (Terminate, Ignore) |
| [09](09-rollback-guide.md) | Rolling Back Migrations | Migrate-Down, writing rollback files, non-reversible operations |
| [10](10-advanced-features.md) | Advanced Features | TargetMigrationOrder, hash validation, baseline, out-of-order, multi-target, target group execution order, CLI tool execution |
| [11](11-operations-guide.md) | Production Operations | Deployment checklists, CI/CD pipelines, monitoring, security |
| [12](12-reference.md) | Quick Reference | All config options, CLI options, enums, exit codes, file patterns |

## "I want to..."

| Task | Go to |
|------|-------|
| Get started quickly | [Chapter 02 — Quick Start](02-quick-start.md) |
| Understand the concepts | [Chapter 03 — Core Concepts](03-concepts.md) |
| Configure my project | [Chapter 04 — Configuration Guide](04-configuration.md) |
| Write migration files | [Chapter 05 — Writing Migration Files](05-migration-files.md) |
| Learn CLI commands | [Chapter 06 — CLI Command Reference](06-cli-commands.md) |
| Set up CI/CD | [Chapter 07 — Execution Modes](07-execution-modes.md), [Chapter 11 — Production Operations](11-operations-guide.md) |
| Handle errors | [Chapter 08 — Error Handling](08-error-handling.md) |
| Recover from a failed migration | [Error Scenarios and Recovery](../02-core-concepts/error-scenarios-and-recovery.md) |
| Roll back migrations | [Chapter 09 — Rolling Back Migrations](09-rollback-guide.md) |
| Use advanced features | [Chapter 10 — Advanced Features](10-advanced-features.md) |
| Deploy to production | [Chapter 11 — Production Operations](11-operations-guide.md) |
| Look up a reference | [Chapter 12 — Quick Reference](12-reference.md) |

## About the BookStore Tutorial

Chapters 02 through 10 build a single running example — a **BookStore** application — that grows in complexity as you progress. Each chapter adds new migration files, configuration changes, or operational techniques on top of what came before. You can follow along step-by-step or jump to the chapter that addresses your immediate need.

| Chapter | Tutorial Step |
|---------|--------------|
| 02 | Create minimal config + first migration, run Migrate-Up |
| 04 | Split config into Dev/Prod environments |
| 05 | Add more migrations + rollback files |
| 06 | Run Validate-Hash, Info commands |
| 07 | Demonstrate Validate, Simulate, Migrate workflow |
| 08 | Introduce intentional error, observe error handling |
| 09 | Migrate-Down to previous release |
| 10 | Add second target, demonstrate TargetMigrationOrder |

> **Tip:** The technical reference docs in [`Docs/`](../) cover every option and edge case in detail. This manual focuses on *when* and *why* you would use each feature.
