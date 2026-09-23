# 1. Introduction and Goals

This chapter answers what RayMigrator is, which problem it solves, which
functional features are essential, which quality goals drive its architecture,
and who the stakeholders are and what they expect from the system. It is the
entry point for everybody who evaluates, integrates or extends RayMigrator.

## 1.1 Requirements Overview

RayMigrator is a cross platform database migration framework for versioned,
release based schema migrations. It is written in C# for .NET 8, 9 and 10,
published as a command line tool (`Raycoon.RayMigrator.Console`) and as NuGet
packages, and developed by RAYCOON.com GmbH under the Business Source License
1.1 with an Additional Use Grant. The current release line is 0.14.x, a
pre-1.0 version that is explicitly not yet proven across a broad range of
production workloads.

The core problem it addresses is schema drift: application code is versioned
and deployed in a controlled way, while the SQL that shapes the database is
often applied by hand, lives on individual machines and cannot be traced
afterwards. RayMigrator replaces this with plain SQL files organized in release
directories, discovered and ordered deterministically, executed against every
configured database, and recorded in a dedicated migration repository (the
RayMigrator bookkeeping database) together with a SHA-256 hash of each file.
The result is a reproducible, auditable path from one schema version to the
next on SQL Server, PostgreSQL, MariaDB, MySQL and SQLite.

A configuration (`appsettings.json`, node `RayMigrator`) describes one or more
products; each product owns target groups (one database engine each, node
`TargetGroups`), and each target group owns targets (one connection each, node
`Targets`). One `migrate-up` run applies all pending migration files of a
product to every target of every target group. Migration files carry an
optional TOML metadata header that restricts them to environments or targets,
controls transaction use and overrides error handling per file.

### Essential functional features

| Feature | Description | Authoritative documentation |
|---------|-------------|-----------------------------|
| Versioned, file based migrations | Migration files are SQL files under `Release`/`TargetGroup` directories, ordered alphabetically by release and file name. No code generation, no DSL. | [Directory structure](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/directory-structure.md), [File naming](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/file-naming.md) |
| Five database engines | DAL plugins `Raycoon.RayMigrator.Database.SqlServer`, `.PostgreSQL`, `.MariaDb`, `.MySql`, `.Sqlite`; all five can host the migration repository as well as targets. | [SQL dialects](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/sql-dialects.md), [DAL architecture](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/dal-architecture.md) |
| Product / target group / target hierarchy | One configuration migrates several products, each with several engines and several databases per engine. | [Product options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/product-options.md), [Settings inheritance](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/settings-inheritance-overview.md) |
| Migration repository and audit trail | Tables such as `MigrationRun`, `MigrationRecord` and `MigrationRecordHistory` record every run, every file per target, its status, hash and timestamps. Created automatically on first use. | [Repository schema](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/repository-schema.md) |
| Rollback files and error actions | Companion `*.rollback.sql` files; `MigrationErrorAction` (`Terminate`, `Rollback`, `RollbackErrorOnly`, `RollbackRelease`, `Ignore`) and `RollbackErrorAction` (`Terminate`, `Ignore`) decide what happens on failure. | [Rollback files](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/rollback-files.md), [Error handling](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/error-handling.md) |
| Hash validation | SHA-256 of each executed file (and optionally of its SQL blocks) is stored and checked; `HashValidationScope` is `File`, `SqlBlocks` or `Disabled`. Commands `validate-hash` and `update-hash`. | [Hash validation](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/hash-validation.md) |
| TOML metadata and migsettings | Per file header (`Description`, `Environments`, `Targets`, `UseTransaction`, `RunAlways`, error actions) plus directory wide `migsettings.txt` files. | [TOML metadata](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/toml-metadata.md), [migsettings files](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/migsettings-files.md) |
| Environment specific execution | `--environment` selects the environment; files can be restricted by TOML `Environments` or by a file name suffix such as `.Production.sql`. Configuration values support `{ENV:NAME}` placeholders. | [Environment specific files](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/environment-specific.md), [Environment variables](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/environment-variables.md) |
| Execution modes | Run modes `MigrationRunMode` = `Validate`, `Simulate`, `Migrate`; target order `TargetMigrationOrder` = `FileByFile` or `TargetByTarget`; optional `TargetGroupMigrationOrder`. | [Execution modes](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/execution-modes.md) |
| Transaction control | Per file `UseTransaction`, respecting engine capabilities (DDL is not transactional on MariaDB and MySQL). | [Block execution](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/04-service-layer/block-execution.md) |
| Resilience and concurrency control | Retry on transient errors, block level resume, detection of interrupted and orphaned runs (`fix` command), exclusive run per product and environment enforced in the repository. | [Resilience](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/resilience.md), [Concurrency control](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/concurrency-control.md) |
| CLI | Commands `migrate-up`, `migrate-down`, `baseline`, `validate-hash`, `update-hash`, `info`, `fix`. | [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md) |
| External CLI tool execution | `CliTools` lets an engine's own client (`sqlcmd`, `psql`, `mysql`, ...) execute migration files instead of the built in DAL, selected via `UseCliToolAlias`. | [CLI tools options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/cli-tools-options.md) |
| Database logging | Optional `DatabaseLogging` writes migration events to `MigrationLog` tables for central monitoring. | [Logging schema](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/logging-schema.md) |
| Extensibility by DAL plugins | Additional engines are added as external assemblies implementing `IDal`, discovered by `DalFactory`; `Raycoon.RayMigrator.Database.Example` is the MIT licensed skeleton. | [External DAL development](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/09-extending/external-dal-development.md) |
| Config Wizard | The web application `Raycoon.RayMigrator.ConfigWizard.Web` generates and validates configuration files. | [Config Wizard overview](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/12-config-wizard/overview.md) |

### What RayMigrator deliberately is not

- **Not an ORM and not a query builder.** RayMigrator executes the SQL you
  wrote, split into blocks; it never generates schema statements from a model.
- **Not a schema diffing or comparison tool.** It does not inspect the target
  schema to derive migrations. State is tracked in the migration repository by
  file name, release and hash, not by comparing database catalogs.
- **Not a parallel executor.** Targets and target groups are processed one
  after another. `FileByFile` and `TargetByTarget` only change the loop order;
  nothing runs concurrently, and a second run for the same product and
  environment is rejected while one is unfinished.
- **Not an undo for arbitrary failures.** Rollback relies on the rollback
  files you provide and on the engine's transaction capabilities. On MariaDB
  and MySQL, DDL commits implicitly.
- **Not a managed service.** The engine runs standalone with JSON
  configuration. The `OperatingMode` enum (`Standalone`, `ManagedLocal`,
  `ManagedRemote`) exists only as a contract for the separate RayMigrator
  Studio product and is not read by the engine.
- **Not yet an Oracle (or other engine) migrator.** Only the five engines
  above ship in the repository; further engines are the domain of DAL plugins.

## 1.2 Quality Goals

The goals are listed in priority order and use ISO 25010 characteristic
names. Chapter [Quality Requirements](10-Quality-Requirements.md) elaborates
them as concrete scenarios.

| Priority | Quality goal | Motivation |
|----------|--------------|------------|
| 1 | Reliability (fault tolerance, recoverability) | A migration touches production data and is not naturally reversible. Every executed block is persisted, failed runs can be resumed, interrupted and orphaned runs are detected, and configurable error actions roll back what has already been applied. A wrong outcome must never be silent. |
| 2 | Functional correctness and integrity | The repository must reflect exactly what was applied where: one record per file and target, hash validation against modified files, exclusive runs per product and environment, deterministic ordering by release and file name. |
| 3 | Portability across database engines | The same migration workflow, repository schema and CLI behave identically on SQL Server, PostgreSQL, MariaDB, MySQL and SQLite; engine specifics are confined to DAL plugins and their SQL templates. |
| 4 | Operability (analysability, transparency) | Operators need to see what will happen before it happens (`Validate`, `Simulate`), what happened afterwards (`info`, repository tables, optional database logging) and how to repair (`fix`, documented recovery procedures). |
| 5 | Modifiability and extensibility | New engines, commands and configuration sources must be addable without touching the core: plugin discovery for DALs, a layered project structure with abstractions, and a template based SQL layer. |

## 1.3 Stakeholders

RayMigrator is an open source, pre-1.0 tool maintained by a small company.
The stakeholder list is therefore deliberately short and practical.

| Role | Expectations and concerns | Most relevant chapters and documents |
|------|---------------------------|--------------------------------------|
| Database administrators | Know exactly which SQL runs against which database and in which order; keep control over transactions; be able to audit and repair the repository; take backups before every run. | [Runtime View](06-Runtime-View.md), [Crosscutting Concepts](08-Crosscutting-Concepts.md), [Repository schema](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/repository-schema.md), [Error scenarios and recovery](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/error-scenarios-and-recovery.md) |
| Application developers | Write plain SQL migration and rollback files, target several environments and databases from one file set, get fast feedback from `Validate` and `Simulate`, avoid surprises from ordering and hashing rules. | [Context and Scope](03-Context-and-Scope.md), [User manual](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/user-manual/README.md), [TOML metadata](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/toml-metadata.md) |
| Release and DevOps engineers | Run migrations unattended in pipelines with stable exit codes, environment variables instead of secrets in files, exclusive-run protection and predictable behavior across engines and operating systems. | [Deployment View](07-Deployment-View.md), [Command reference](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/08-cli-reference/command-reference.md), [Operations guide](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/user-manual/11-operations-guide.md) |
| DAL plugin developers | A stable `IDal` contract, a complete template inventory, a copyable skeleton (`Raycoon.RayMigrator.Database.Example`, MIT) and clarity about how plugins are discovered and licensed. | [Building Block View](05-Building-Block-View.md), [Architecture Decisions](09-Architecture-Decisions.md), [External DAL development](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/09-extending/external-dal-development.md) |
| RayMigrator maintainers (RAYCOON) | Keep the five engines behaviorally identical, keep the repository schema and enum contracts consistent with RayMigrator Studio, keep the engine and unit test suites green, and reach a 1.0 that can be trusted in production. | [Solution Strategy](04-Solution-Strategy.md), [Risks and Technical Debt](11-Risks-and-Technical-Debt.md), [Engine tests](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/10-testing/engine-tests.md), [CONTRIBUTING.md](https://github.com/RAYCOON/RayMigrator/blob/main/CONTRIBUTING.md) |
| Security and compliance reviewers | Tamper detection for executed SQL, a complete audit trail, no secrets in configuration files, a clear license and vulnerability reporting process. | [Architecture Constraints](02-Architecture-Constraints.md), [Hash validation](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/hash-validation.md), [SECURITY.md](https://github.com/RAYCOON/RayMigrator/blob/main/SECURITY.md), [LICENSE.md](https://github.com/RAYCOON/RayMigrator/blob/main/LICENSE.md) |
| Evaluators and adopters | A quick, honest assessment of maturity, scope boundaries and licensing before committing to the tool. | This chapter, [README.md](https://github.com/RAYCOON/RayMigrator/blob/main/README.md), [Quick start](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/user-manual/02-quick-start.md) |

## Related documentation

- [README.md](https://github.com/RAYCOON/RayMigrator/blob/main/README.md) - features, maturity notice, license summary
- [User manual, chapter 1: What is RayMigrator?](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/user-manual/01-introduction.md)
- [User manual, chapter 3: Core concepts](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/user-manual/03-concepts.md)
- [Architecture overview](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/overview.md)
- [Open features](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/open-features.md)
- [CHANGELOG.md](https://github.com/RAYCOON/RayMigrator/blob/main/CHANGELOG.md)
- [Glossary](12-Glossary.md)
