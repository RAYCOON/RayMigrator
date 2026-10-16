# 9. Architecture Decisions

This chapter answers why RayMigrator is built the way the [Solution Strategy](04-Solution-Strategy.md) summarizes and the
[Crosscutting Concepts](08-Crosscutting-Concepts.md) describe: what was chosen, against which alternatives, and which downsides
were accepted knowingly. Each decision is an Architecture Decision Record in the compact Nygard/MADR style with **Context**,
**Decision**, **Consequences** (positive and negative), **Alternatives considered** where the sources name any, and **References**.
Titles reuse the wording of sections 4.1 and 4.5 of the Solution Strategy. The Git history begins with the initial commit of 2026-04-23,
followed the same day by the commit tagged `v0.10.3`; a decision already present there is dated `<= 0.10.3`, later ones from `CHANGELOG.md` with their issue.

## Decision log

| ID | Title | Status | Since | Related concepts |
|----|-------|--------|-------|------------------|
| ADR-001 | Layered architecture with one-directional dependencies | Accepted | <= 0.10.3 | [Dependency injection and the context pattern](08-Crosscutting-Concepts.md#dependency-injection-and-the-context-pattern) |
| ADR-002 | Pipeline layer extraction | Accepted | <= 0.10.3 | [Dependency injection and the context pattern](08-Crosscutting-Concepts.md#dependency-injection-and-the-context-pattern) |
| ADR-003 | DAL plugin architecture with `[DatabaseType]` discovery and `Database.Common` contract package | Accepted | <= 0.10.3 | [DAL Abstraction and plugin discovery](08-Crosscutting-Concepts.md#dal-abstraction-and-plugin-discovery), [Plugin and Command Registration](08-Crosscutting-Concepts.md#plugin-and-command-registration) |
| ADR-004 | Plain SQL migration files with TOML header | Accepted | <= 0.10.3 | [Migration files, releases and metadata](08-Crosscutting-Concepts.md#migration-files-releases-and-metadata) |
| ADR-005 | SQL template system for the repository schema | Accepted | <= 0.10.3 | [Template-Driven Repository Schema](08-Crosscutting-Concepts.md#template-driven-repository-schema) |
| ADR-006 | `MigrationContext` pattern with `IMigrationContextAccessor` | Accepted | <= 0.10.3 | [Dependency injection and the context pattern](08-Crosscutting-Concepts.md#dependency-injection-and-the-context-pattern) |
| ADR-007 | Repository separate from targets, recreated instead of upgraded | Accepted (refined 0.14.0; recreate part superseded by ADR-024) | <= 0.10.3 | [Migration repository schema](08-Crosscutting-Concepts.md#migration-repository-schema), [Migration history and info](08-Crosscutting-Concepts.md#migration-history-and-info) |
| ADR-008 | Block-level execution with per-block transactions | Accepted (refined 0.13.0) | <= 0.10.3 | [Block-Level Execution and Transactions](08-Crosscutting-Concepts.md#block-level-execution-and-transactions) |
| ADR-009 | Flat directory layout auto-detection | Accepted | <= 0.10.3 | [Migration files, releases and metadata](08-Crosscutting-Concepts.md#migration-files-releases-and-metadata), [Deterministic File Ordering](08-Crosscutting-Concepts.md#deterministic-file-ordering) |
| ADR-010 | No parallel database execution | Accepted | <= 0.10.3 | [Execution modes](08-Crosscutting-Concepts.md#execution-modes), [Exclusive Run Lock and Orphaned Run Detection](08-Crosscutting-Concepts.md#exclusive-run-lock-and-orphaned-run-detection) |
| ADR-011 | External CLI tool execution as an alternative execution path | Accepted | <= 0.10.3 | [Credentials and secrets](08-Crosscutting-Concepts.md#credentials-and-secrets), [Runtime View 6.8](06-Runtime-View.md#68-external-cli-tool-execution) |
| ADR-012 | Atomic shared connection | Accepted | <= 0.10.3 | [Block-Level Execution and Transactions](08-Crosscutting-Concepts.md#block-level-execution-and-transactions), [Retry on Transient Errors](08-Crosscutting-Concepts.md#retry-on-transient-errors) |
| ADR-013 | Hash validation scopes | Accepted | <= 0.10.3 | [Hash Validation](08-Crosscutting-Concepts.md#hash-validation) |
| ADR-014 | Configuration inheritance with rule-based validation | Accepted | <= 0.10.3 | [Configuration Inheritance with Validation](08-Crosscutting-Concepts.md#configuration-inheritance-with-validation) |
| ADR-015 | Multi-framework targeting | Accepted | <= 0.10.3 | [Architecture Constraints](02-Architecture-Constraints.md#platform-and-toolchain) |
| ADR-016 | Pre-1.0 compatibility policy: no aliases, no in-place upgrades | Accepted (repository part superseded by ADR-024) | 0.14.0 | [Error model and exit codes](08-Crosscutting-Concepts.md#error-model-and-exit-codes) |
| ADR-017 | Business Source License 1.1 with Additional Use Grant | Accepted (supersedes RMLA v1.0) | 0.11.0 | [Architecture Constraints](02-Architecture-Constraints.md#23-conventions) |
| ADR-018 | Structured logging with a database sink | Accepted (refined 0.12.0, 0.13.0) | <= 0.10.3 | [Structured Logging to Console, File and Database](08-Crosscutting-Concepts.md#structured-logging-to-console-file-and-database) |
| ADR-019 | Blazor WebAssembly Config Wizard as a separate, database-free tool | Accepted | <= 0.10.3 | [Configuration Inheritance with Validation](08-Crosscutting-Concepts.md#configuration-inheritance-with-validation), [Runtime View 6.9](06-Runtime-View.md#69-config-wizard-session-short) |
| ADR-020 | Error handling strategies with rollback files and run results | Accepted (refined 0.13.0, 0.14.0) | <= 0.10.3 | [Rollback Strategies](08-Crosscutting-Concepts.md#rollback-strategies) |
| ADR-021 | Configuration arrays merge by alias in every application | Accepted (0.15.0, #23) | 2026-09-23 | [Configuration Inheritance with Validation](08-Crosscutting-Concepts.md#configuration-inheritance-with-validation) |
| ADR-022 | Transient error codes ship as an editable file per DAL folder | Accepted (0.16.0, #24) | 2026-10-10 | [Retry on Transient Errors](08-Crosscutting-Concepts.md#retry-on-transient-errors), [DAL Abstraction and plugin discovery](08-Crosscutting-Concepts.md#dal-abstraction-and-plugin-discovery) |
| ADR-023 | The context factory is the hosting seam: one prepared, activated context per run, and typed progress events | Accepted (0.17.0) | 2026-10-12 | [Dependency injection and the context pattern](08-Crosscutting-Concepts.md#dependency-injection-and-the-context-pattern) |
| ADR-024 | The repository schema is versioned and upgraded in numbered steps | Accepted (0.17.0) | 2026-10-13 | [Migration repository schema](08-Crosscutting-Concepts.md#migration-repository-schema), [Template-Driven Repository Schema](08-Crosscutting-Concepts.md#template-driven-repository-schema) |
| ADR-025 | Technology-neutral read model of the repository | Accepted (0.17.0) | 2026-10-13 | [Migration repository schema](08-Crosscutting-Concepts.md#migration-repository-schema) |
| ADR-026 | A run binds and validates only its own product | Accepted (0.19.0) | 2026-10-15 | [Configuration Inheritance with Validation](08-Crosscutting-Concepts.md#configuration-inheritance-with-validation) |
| ADR-027 | The engine deletes log and history rows on a host's request and never reads them for a decision | Accepted (0.20.0) | 2026-10-16 | [Retention of history and log](08-Crosscutting-Concepts.md#retention-of-history-and-log), [Migration repository schema](08-Crosscutting-Concepts.md#migration-repository-schema), [Migration history and `info`](08-Crosscutting-Concepts.md#migration-history-and-info) |
| ADR-028 | A run cancels at the next block boundary with a `MigrationCancelledAction` and records `Cancelled` | Accepted (0.20.0) | 2026-10-16 | [Cancellation at the next block boundary](08-Crosscutting-Concepts.md#cancellation-at-the-next-block-boundary), [Rollback Strategies](08-Crosscutting-Concepts.md#rollback-strategies), [Error model and exit codes](08-Crosscutting-Concepts.md#error-model-and-exit-codes), [Runtime View 6.10](06-Runtime-View.md#610-cancellation-of-a-run) |

## ADRs

### ADR-001: Layered architecture with one-directional dependencies

**Context.** A migration engine that must serve a CLI today and a hosted product (RayMigrator Studio) tomorrow needs a structure
in which parsing, orchestration, migration logic, repository access and engine access can be tested and replaced separately.

**Decision.** Class libraries with project references that only point downward: `Raycoon.RayMigrator.Console` to `Pipeline` to
`Services` (behind `Services.Abstractions`) to `Infrastructure` to `Core` to `Database.Common`, with `Shared` and
`Validation` as leaf packages that reference nothing; `Database` (the `DalFactory`) is referenced by `Infrastructure`,
`Services`, `Pipeline` and `Testing`, not by `Core`. Only `Tests.Engine` references `Console`; `Core` never references
`Services` or `Infrastructure`.

**Consequences.** Positive: each layer has a unit test project or is reachable through `InternalsVisibleTo`; a change in a DAL or
the console never cascades into `Services`; the NuGet packages cut along the same lines. Negative: 24 projects to maintain, every
service method needs request and result DTOs, and the layering is not enforced by tooling; one known irregularity is that
`TemplateCache` and `TemplateExecutor` live in the Infrastructure project but declare `Raycoon.RayMigrator.Core` namespaces.

**References.** [Layered Architecture](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#layered-architecture), [Component responsibilities](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/component-responsibilities.md), [Building Block View](05-Building-Block-View.md).

### ADR-002: Pipeline layer extraction

**Context.** The console project originally owned DI setup, configuration loading, logger creation and command dispatch next to
`System.CommandLine` parsing, so a second host would have had to reference the console executable.

**Decision.** Move `DirectModePipeline`, `JsonOptionsSource`, `RayMigratorService` and `SerilogFactory` into
`Raycoon.RayMigrator.Pipeline`. The console keeps only `Program.Main`, `AssemblyInfoHelper` and the exit code mapping;
`CommandLineConfiguration` (System.CommandLine) and `EnvironmentResolver` live in `Core`, and the pipeline references
`Services`, `Infrastructure`, `Core`, `Database` and `Shared` without a direct `System.CommandLine` reference.

**Consequences.** Positive: another entry point (`EngineTestHost` in the engine tests, the Studio host) reuses the same host
construction and the `IOptionsSource` seam without the console. Negative: the console still references every DAL project and the
Serilog sink packages directly, because the post-build copy into `DataAccessLayers/{Type}/` and Serilog's configuration based
sink discovery need those assemblies in the executable's output, so the console is not as thin as the split suggests.

**References.** [Pipeline Extraction](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#pipeline-extraction), [Building Block View 5.2.2](05-Building-Block-View.md#522-pipeline-layer-raycoonraymigratorpipeline).

### ADR-003: DAL plugin architecture with `[DatabaseType]` discovery and `Database.Common` contract package

**Context.** Five engines with five ADO.NET providers must coexist without one provider update touching another, and third
parties should be able to add an engine without a fork.

**Decision.** One assembly per engine (`Raycoon.RayMigrator.Database.{Engine}`) that implements `IDal` by deriving from `DalBase`,
carries `[DatabaseType("...")]` and references only `Raycoon.RayMigrator.Database.Common` and `Shared`. The static `DalFactory`
discovers built in plugins from `DependencyContext.Default` (runtime libraries named `Raycoon.RayMigrator.*`) and external plugins
from `DataAccessLayers/*/*.dll`, instantiates them with `Activator.CreateInstance(type, connectionString)` and caches one instance
per `{databaseType}_{connectionString}`. Engine facts are data in `DalSpecificProperties`; the built-in transient error codes live in each
plugin (`DefaultTransientErrorCodes`, classified in its `IsTransient` override) and are replaced by a shipped `TransientErrorCodes.txt` since ADR-022. `Raycoon.RayMigrator.Database.Example` is the MIT licensed skeleton.

**Consequences.** Positive: a new engine is a drop-in folder with assembly, provider and templates; the engine code never branches
on an engine name; discovery survives single file publish. Negative: reflection instead of compile time checks (a DAL must be a
non-abstract class with a public connection string constructor), duplicate `DatabaseType` values are silently ignored (`TryAdd`, first wins),
`Assembly.LoadFrom` of any DLL next to the binary is a trust decision left to the operator, and the instance cache is never evicted.

**Alternatives considered.** DI registration of the DALs (given up for drop-in plugins, see Solution Strategy 4.5) and an ORM
layer (rejected because template SQL must stay reviewable per engine).

**References.** [DAL Plugin Architecture](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#dal-plugin-architecture), [DAL architecture](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/dal-architecture.md), [External DAL development](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/09-extending/external-dal-development.md).

### ADR-004: Plain SQL migration files with TOML header

**Context.** DBAs want to review exactly what runs; engine specific features (partitioning, extensions, hints) must stay usable;
tamper detection needs a stable unit to hash.

**Decision.** A migration is a `*.sql` file in the target engine's dialect with an optional `/* [RayMigrator] ... */` header holding
a small TOML key set (`Description`, `Environments`, `Targets`, `UseTransaction`, `RunAlways`, `RequireRollbackFile`,
`MigrationErrorAction`, `RollbackErrorAction`, `UseCliToolAlias`); directory defaults come from `migsettings.txt`. The header is
parsed by `MigrationService.ExtractTomlAndSql` and `ParseTomlConfig` without a TOML library. No DSL, no code-first model, no schema
diffing.

**Consequences.** Positive: the SHA-256 of the file is a meaningful audit unit; rollback files are plain SQL as well; nothing to
learn beyond SQL and a handful of keys. Negative: a product with several engines needs one file set per target group; the hand
written parser accepts only the documented subset of TOML and rejects unknown keys with `MigrationFileParsingException`; two keys
(`StopRollbackOnMissingRollbackFile`, `TargetGroupMigrationOrder`) are parsed at file level but not applied there, which is a
documented trap.

**Alternatives considered.** Engine agnostic migration definitions (Solution Strategy 4.5, "Plain SQL per engine instead of engine
agnostic migrations").

**References.** [TOML for Migration Metadata](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#toml-for-migration-metadata), [TOML metadata](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/toml-metadata.md), [migsettings files](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/migsettings-files.md).

### ADR-005: SQL template system for the repository schema

**Context.** The repository must exist on any of the five engines, and its SQL must be reviewable and adjustable by DBAs without
a rebuild.

**Decision.** All repository and log access goes through per engine SQL files in `DataAccessLayers/{Type}/`, one per
`TemplateType` (21 besides `Undefined` at the time of the decision, 30 in 0.20.0), loaded by `TemplateCache` at startup and executed by `TemplateExecutor`. Every template
returns one scalar `ResultCode,ResultMessage`; negative codes map to the `TemplateResultCode` catalog and raise
`TemplateResultException`. Placeholders are `{ENV:*}` (once, at load), `{CFG:*}` (per call, `SchemaName` and `TableBaseName` only)
and `@Parameter` bindings; `TemplateCache` checks completeness at startup, and `ValidateConfigurationAgainstTemplateCache` checks per configuration that every configured `DatabaseType` has a template folder.

**Consequences.** Positive: no ORM, engine specific locking primitives (`UPDLOCK, HOLDLOCK`, `pg_advisory_xact_lock`, `GET_LOCK`)
live in SQL where they belong, and a DBA can customize a template. Negative: every template five times, once per engine, to keep in sync, a
result contract that every external DAL must reproduce exactly (0.12.0 and 0.13.0 each added templates that external DALs had to
ship, 0.17.0 the schema steps and 0.20.0 the retention templates of ADR-027), and up to 0.19.0 `TemplateExecutor` was synchronous
over async DAL calls (`GetAwaiter().GetResult()`); since 0.20.0 it awaits the DAL and hands it the token of its caller (the reads of a run take the token of the run, its writes none, ADR-028).

**Alternatives considered.** ORM or query builder (rejected: transparency and DDL capabilities).

**References.** [SQL Template System](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#sql-template-system), [Template system](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/template-system.md), [Template customization](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/09-extending/template-customization.md), CHANGELOG 0.12.0 (#7), 0.13.0 (#13).

### ADR-006: `MigrationContext` pattern with `IMigrationContextAccessor`

**Context.** Options, run identity, current file and block, and the resolved `DalSpecificProperties` are needed by services,
`TemplateExecutor` and the log enricher alike; the same services must later run per request inside a web host.

**Decision.** One `MigrationContext` (immutable options plus mutable `MigrationState`, `Clone` for snapshots) created by
`IMigrationContextFactory`, reached only through `IMigrationContextAccessor.Current`. `AddRayMigratorServices(RayMigratorHostMode)`
registers `SingletonMigrationContextAccessor` for `Cli` and the `AsyncLocal` backed `AsyncLocalMigrationContextAccessor` for
`Api`; `TemplateExecutor` resolves context and repository DAL lazily on first use.

**Consequences.** Positive: services stay host agnostic, logging gets a consistent snapshot, and Studio reuses the engine
unchanged. Negative: an ambient context is implicit state; a service that touches `Current` before the factory has run fails at
runtime (`InvalidOperationException` in the `AsyncLocal` accessor); and the `Api` mode has no consumer and no test inside this
repository, so only RayMigrator Studio exercises it.

**References.** [MigrationContext Pattern](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#migrationcontext-pattern), [Context Pattern](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/patterns.md#context-pattern), [MigrationContext](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/migration-context.md).

### ADR-007: Repository separate from targets, recreated instead of upgraded

**Context.** Migration state for many products on mixed engines must be queryable in one place, and the run lock must be
enforced by a database rather than by a file or a service.

**Decision.** The migration repository is its own configured database (`Repository.DatabaseType`, `ConnectionString`,
`SchemaName`, `TableBaseName`) on any of the five engines, created by `Repository_CheckCreate` before every command. Since 0.14.0
the schema is never upgraded in place: lookup tables are seeded only at creation, the in-place upgrade blocks of 0.12.0 and
0.13.0 are gone, and `MigratorMeta.RayMigratorVersion` records which versions used the repository (the first row identifies the
schema). A schema change means dropping and recreating the repository.

**Consequences.** Positive: cross engine tracking, an audit trail where DBAs already look, a lock implemented with the engine's
own primitives, and a version history in `MigratorMeta` that makes a mismatched repository detectable. Negative: no zero footprint
mode, a repository account that needs DDL on first contact, and every pre-1.0 schema change forces a recreate that discards the
`MigrationRecordHistory` audit trail unless it is exported first. The repository may share a database with a target (ADR-012).

**Alternatives considered.** Migration state inside each target (rejected: no cross target view, no central lock).

**Superseded in part.** ADR-024 (0.17.0) versions the schema and upgrades it in steps, and a step brings the lookup rows it needs (step 3 of 0.20.0 adds `Cancelled` to `MigrationRunResult`); the separate repository and the run lock stay as decided here, the drop-and-recreate rule applies one last time to repositories created before 0.17.0.

**References.** [Repository Separate from Targets](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#repository-separate-from-targets), [Repository schema](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/repository-schema.md), CHANGELOG 0.14.0 (#6, #12, #18 and the `MigratorMeta` follow-up).

### ADR-008: Block-level execution with per-block transactions

**Context.** Migration files can be long, SQL Server requires `GO` separated batches, and an interrupted run should not force a
manual repair of half applied files.

**Decision.** `SplitSqlIntoBlocks` splits a file at the engine's `SqlBlockDelimiter` (`GO` on SQL Server, `;` elsewhere); each block
is one `IDal.ExecuteNonQueryAsync` call in its own transaction when `UseTransaction` is `true`, and `RepositoryMigrationUpdateAsync`
persists the committed block count after every block so `FindResumableBlock` can continue an interrupted file. Files routed to a
CLI tool are not split. Since 0.13.0 a `Failed` record stores the number of committed blocks, not the index of the failing block.

**Consequences.** Positive: resume from the failing block, progress visible per block, and a hash over the SQL blocks that ignores
header changes. Negative: a transaction spans a block, not a file, so a failure in block three leaves blocks one and two committed
unless the atomic path of ADR-012 applies; on MariaDB and MySQL DDL commits implicitly and only a warning (Rule 2.8) tells the
author; the delimiter is a plain line match, so a `;` on its own line inside a PostgreSQL function body splits the block.

**References.** [Block-Level Execution](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#block-level-execution), [Block execution](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/04-service-layer/block-execution.md), CHANGELOG 0.13.0 (#11).

### ADR-009: Flat directory layout auto-detection

**Context.** The canonical layout is `{Release}/{TargetGroupAlias}/file.sql`; teams with one database per product found the
target group level redundant and wanted to onboard existing flat trees without moving files.

**Decision.** When a product has exactly one target group, `StringExtensions.GetReleaseVersionAndTargetGroupAlias` assigns that
alias to files placed directly under the release directory. `ValidateFlatLayoutAmbiguity` rejects a release that mixes flat and
nested files with `ConfigurationValidationException`, `ValidateTargetGroupAliasCasing` rejects directory names that differ from the
alias only in case, and rollback lookup applies the same fallback.

**Consequences.** Positive: simpler trees for the common single database case and gradual adoption. Negative: adding a second
target group to such a product changes the meaning of the existing files (they must be moved), and with one target group a
subdirectory whose name matches no alias is absorbed into the flat layout instead of being reported; only case mismatches are caught.

**References.** [Flat Directory Layout Auto-Detection](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#flat-directory-layout-auto-detection), [Directory structure](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/directory-structure.md), [File discovery](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/04-service-layer/file-discovery.md).

### ADR-010: No parallel database execution

**Context.** A product may have many targets; running them concurrently would shorten wall clock time but complicate ordering,
rollback chains and the shared `MigrationContext`.

**Decision.** One process executes one loop over releases, target groups and targets. `TargetMigrationOrder` (`FileByFile`,
`TargetByTarget`) only changes the nesting of the file and target loops; nothing runs concurrently. The repository lock refuses a
second unfinished run for the same product and environment (since 0.14.0 regardless of run mode).

**Consequences.** Positive: deterministic order and logs, a rollback chain that is a simple reverse list, no risk of overloading a
server, and no locking inside `MigrationContext`. Negative: throughput on many targets, and the enum names `Simultaneously` and
`Successively` used until 0.13.0 suggested concurrency that never existed, which is why they were renamed (#19).

**Alternatives considered.** Parallel targets (Solution Strategy 4.5, "Sequential execution instead of parallel targets").

**References.** [No Parallel Database Execution](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#no-parallel-database-execution), [Execution modes](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/execution-modes.md), CHANGELOG 0.13.0 (#19), 0.14.0 (#19).

### ADR-011: External CLI tool execution as an alternative execution path

**Context.** Some scripts depend on tool syntax the DAL cannot interpret (`sqlcmd` `:r` includes, `psql` meta commands), and some
organizations mandate the vendor client for audit reasons.

**Decision.** `CliTools[]` defines tool profiles (`Alias`, `ExecutablePath`, `ArgumentTemplate`, `InputMode` `File` or `Stdin`,
`SuccessExitCodes` with range notation, `CliToolTimeoutInSeconds`); `UseCliToolAlias` selects a tool at any inheritance level down to
the file header, and `CliToolParameters` on a target fill the template. `CliToolExecutor` runs the process with
`UseShellExecute = false`, pipes `Stdin` as UTF-8 without BOM, and never logs the rendered arguments.

**Consequences.** Positive: existing tool based workflows adopt RayMigrator without rewriting scripts. Negative: the only result is
the exit code (no `TemplateResponse`), transactions and retries are the tool's business, files are not split into blocks so resume
is per file, the shared connection path never applies, and placeholder values are not quoted by RayMigrator. Since 0.14.0
`InputMode` is required (`RULE_3_11`) because a silent `File` default had surprised users.

**References.** [External CLI Tool Execution](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#external-cli-tool-execution), [CLI tools options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/cli-tools-options.md), CHANGELOG 0.12.0 (#4), 0.13.0 (#15), 0.14.0 (#19).

### ADR-012: Atomic shared connection

**Context.** With separate connections for target and repository, a crash between the last block and the status update leaves a
migrated target with an `Executing` record.

**Decision.** `MigrationService.CanUseSharedConnection` returns true when the file has `UseTransaction`, block errors are not
ignored, and repository and target share `DatabaseType` (case insensitive) and a byte identical connection string. Then
`ExecuteSqlBlocksAtomic` runs every block and the repository updates in one transaction on one `DbConnection`; with
`DbCommandMaxRetries > 0` a transient error rolls back and re-executes the whole file.

**Consequences.** Positive: target change and bookkeeping commit or roll back together without a distributed transaction.
Negative: applies only to the single database deployment; the byte comparison of connection strings means a different parameter
order disables it silently; and per block resume is lost because a failure rolls back the whole file.

**Alternatives considered.** Distributed transactions or two-phase commit (rejected as unnecessary for the same database).

**References.** [Atomic Shared Connection](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#atomic-shared-connection), [Block execution](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/04-service-layer/block-execution.md).

### ADR-013: Hash validation scopes

**Context.** Hashing the whole file catches every edit, but teams also edit headers (descriptions, environment lists) after a file
has run and did not want that to count as tampering.

**Decision.** Three SHA-256 hashes per record (`FileUpHash`, `FileUpConfigHash`, `FileUpBlocksHash`) and a `HashValidationScope`
per target group: `File` (1) compares the whole file, `SqlBlocks` (2) only the SQL, `Disabled` (3) nothing. `validate-hash`
reports `Modified` and `Missing` and exits with `1`; `migrate-up` re-executes a changed `Migrated` file with a warning instead of
aborting.

**Consequences.** Positive: header edits do not invalidate history under `SqlBlocks`; `Disabled` allows legacy adoption. Negative:
re-execution instead of a hard stop puts the tamper gate into the pipeline (`validate-hash` must run first); the config hash of
header-less files changed representation to `NULL` in 0.14.0 (#9), so `update-hash` reports those once on older repositories;
block resume always requires an unchanged `FileUpBlocksHash`, even with `Disabled`.

**Alternatives considered.** Abort on any mismatch (Solution Strategy 4.5, "Hash mismatch re-executes instead of aborting").

**References.** [Hash Validation Scopes](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#hash-validation-scopes), [Hash validation](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/hash-validation.md), CHANGELOG 0.12.0 (#5, #8, #9), 0.14.0 (#9, #21).

### ADR-014: Configuration inheritance with rule-based validation

**Context.** Dozens of settings apply per product, target group and target; repeating them invites drift, and a contradiction
discovered in the middle of a run is expensive.

**Decision.** `RayMigratorOptions` is bound from the four `appsettings*.json` layers by `JsonOptionsSource` with `{ENV:NAME}`
placeholders resolved by `EnvironmentVariableReplacer`; `ProductDefaults`, `TargetGroupDefaults` and `TargetDefaults` flow into
products, target groups and targets through `ProductDefaultsPostConfigureOptions.MergeDefaults`, followed by `migsettings.txt` and
the TOML header. Validation runs at startup in three stages: data annotations, the shared `RuleCatalog` (`RULE_1_1` to
`RULE_8_3`) from the dependency free `Raycoon.RayMigrator.Validation` package, and engine aware checks. Deliberately not used:
`AddCommandLine` and `AddEnvironmentVariables`.

**Consequences.** Positive: strongly typed options, one rule catalog for engine and Config Wizard, and failures before any
connection is opened. Negative: several resolution chains with different scopes (some keys stop at the target group, some reach
the file), which is why the snapshot `MigrationRunSettingsJson` exists; enum parsing had to be hardened twice (0.11.1 #3, 0.13.0 #17).

**References.** [Configuration System](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#configuration-system), [Settings inheritance](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/settings-inheritance-overview.md), [Validation rules](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/validation-rules.md), CHANGELOG 0.11.1 (#3), 0.13.0 (#17).

### ADR-015: Multi-framework targeting

**Context.** Enterprise adopters run the LTS runtime, early adopters the current one, and the NuGet packages must fit into both.

**Decision.** Every packable library, DAL plugin and the console declare `<TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>`,
including `Validation`, `Testing`, `Database.Example` and `ConfigWizard.Core`; the test projects and the WebAssembly app
`ConfigWizard.Web` target `net10.0` only. Package versions are pinned centrally in `Directory.Packages.props`.

**Consequences.** Positive: consumers choose the runtime; the packages carry nullable annotations for all three. Negative: three
builds per package, a matrix that CI exercises only on `net10.0` (`Build & Test` runs the unit suite there), and provider and
`Microsoft.Extensions.*` packages that must support all three targets, so a servicing bump raises the floor for every consumer.

**References.** [Multi-Framework Targeting](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#multi-framework-targeting), [Architecture Constraints](02-Architecture-Constraints.md#platform-and-toolchain), CHANGELOG 0.11.0 (Dependencies).

### ADR-016: Pre-1.0 compatibility policy: no aliases, no in-place upgrades

**Context.** The enum review of 0.13.0 (#11 to #20) renamed the `TargetMigrationOrder` members and kept `Simultaneously` and
`Successively` as aliases through `EnumAliasAttribute`; such layers below 1.0 double the surface every document has to describe.

**Decision.** While `RayMigratorVersion` is below 1.0, breaking changes in CLI, configuration, enums and repository schema are
allowed between minor versions and marked `Breaking` in `CHANGELOG.md`; no aliases, no compatibility shims and no in-place
repository upgrades are kept. 0.14.0 removed the alias mechanism (#19), the second `--scope` spelling (#21), `fix --dry-run`
(#22), the upgrade blocks in `Repository_CheckCreate` and `DatabaseLogging_CheckCreate` (#6, #12, #18) and the hand maintained
`RepositoryVersion` constant.

**Consequences.** Positive: one spelling everywhere, smaller templates, and external DALs need no upgrade branches. Negative:
every 0.x upgrade may require a recreated repository and updated pipelines in the same deployment; adopters who migrated with
0.12.0 or 0.13.0 lose their run history on the recreate.

**Alternatives considered.** Keeping the aliases (implemented in 0.13.0, reversed one release later).

**Superseded in part.** ADR-024 (0.17.0) introduces versioned in-place repository upgrades; the rest of the policy (no aliases, no shims, breaking changes marked in `CHANGELOG.md`) stands. The log tables carry no schema version, and a step would not reach a log database of its own, so since 0.20.0 `DatabaseLogging_CheckCreate` adds the two indexes of `MigrationLog` to a log table that already exists, idempotently and outside its creation branch; its master data is still written only when the tables are created.

**References.** CHANGELOG 0.13.0 (#19), 0.14.0 (#6, #9, #12, #18, #19, #21, #22), [Solution Strategy 4.4](04-Solution-Strategy.md#44-organizational-decisions).

### ADR-017: Business Source License 1.1 with Additional Use Grant

**Context.** Versions up to 0.10.3 shipped under the RayMigrator Dual License Agreement (RMLA) v1.0 with eligibility thresholds.
The company wanted source availability, free production use for everyone, and an eventual open source conversion without giving
up the name.

**Decision.** From 0.11.0 the Licensed Work is under BUSL-1.1 with an unconditional Additional Use Grant (production use free of
charge, hosted and SaaS offerings included), a per version Change Date four years after first distribution tracked in
`Docs/license-change-dates.md`, Apache License 2.0 as Change License, German law Supplemental Terms, and a trademark reservation
for modified versions. `Raycoon.RayMigrator.Database.Example` is carved out under MIT.

**Consequences.** Positive: adopters have no fee or size threshold to check, plugin authors may copy the skeleton freely, and the
conversion date is predictable. Negative: BUSL is not an OSI approved license, so some procurement policies still flag it; the
Licensed Work line in `LICENSE.md` must be bumped with every version and is checked by the release workflow; and a modified fork
must be renamed.

**References.** [LICENSE.md](https://github.com/RAYCOON/RayMigrator/blob/main/LICENSE.md), [COMMERCIAL-LICENSE.md](https://github.com/RAYCOON/RayMigrator/blob/main/COMMERCIAL-LICENSE.md), CHANGELOG 0.11.0 (Licensing).

### ADR-018: Structured logging with a database sink

**Context.** Operators read the console, auditors read files, and central monitoring wants queryable rows with the run, target and
file each event belongs to.

**Decision.** One Serilog pipeline built by `SerilogFactory` from the `RayMigrator.Serilog` node, enriched by
`MigrationContextEnricher` from `MigrationLoggingContext.Current`; `RayMigratorDatabaseSink` is on by default and writes into the repository (refined by WP2: a `DatabaseLogging` node changes level or database, `Enabled: false` removes the sink),
which enqueues into the asynchronous `DatabaseLoggerQueue` and writes through the `DatabaseLogging_Insert` template into
`MigrationLog`, gated by `DbLogEnabled` from the command's `CommandProfile`. `SensitiveDataMasker` hides secrets unless
`--reveal-sensitive-data` is given. Since 0.20.0 `DatabaseLogging_CheckCreate` gives `MigrationLog` an index on `MigrationRunId`
and one on `CreatedAt` for the readers of the log and its retention (ADR-027).

**Consequences.** Positive: the same enriched event reaches every sink, and the log database may live on a different server than
the repository. Negative: the sink needs its own DAL and template set, rows are lost if the process dies before `DirectModePipeline`
flushes the queue (since 0.20.0 the first Ctrl+C or SIGTERM stops the command and lets the pipeline flush, a second signal still
ends the process at once, ADR-028), and two defects surfaced late: `info` filled the audit log while `validate-hash` left no trace
until the profile gate (0.12.0, #6), and every row carried `EventId` 0 until 0.13.0 (#12).

**References.** [Logging options](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/logging-options.md), [Logging schema](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/logging-schema.md), CHANGELOG 0.12.0 (#6), 0.13.0 (#12).

### ADR-019: Blazor WebAssembly Config Wizard as a separate, database-free tool

**Context.** The configuration tree is deep and its rules are numerous; a guided editor lowers the entry barrier, but a hosted
editor that receives connection strings would be a liability.

**Decision.** `Raycoon.RayMigrator.ConfigWizard.Web` (`Microsoft.NET.Sdk.BlazorWebAssembly`, MudBlazor) runs entirely in the
browser as static files on top of `Raycoon.RayMigrator.ConfigWizard.Core`, which references only `Raycoon.RayMigrator.Validation`
and performs no file or database access. The wizard runs the same `RuleCatalog` as the engine and exports a ZIP; deployment is a
static site (`Deploy ConfigWizard Web`).

**Consequences.** Positive: no server ever sees a secret, hosting is trivial, and engine and wizard reject the same configurations.
Negative: the wizard cannot test a connection or inspect an engine, it keeps its own model of the options outside `Core` (the
`Validation` package must stay dependency free to remain WASM safe), and MudBlazor upgrades carry UI breaking changes (0.11.0
notes two).

**References.** [Config Wizard architecture](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/12-config-wizard/architecture.md), [Config Wizard validation](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/12-config-wizard/validation.md), CHANGELOG 0.11.0 (Dependencies).

### ADR-020: Error handling strategies with rollback files and run results

**Context.** Production wants a failed migration to stop, development wants it undone automatically, and both want the repository
to state honestly what happened.

**Decision.** `MigrationErrorAction` (`Terminate`, `Rollback`, `RollbackErrorOnly`, `RollbackRelease`, `Ignore`) selects the
recovery, `RollbackErrorAction` (`Terminate`, `Ignore`) governs failures inside the chain, `RequireRollbackFile` and
`StopRollbackOnMissingRollbackFile` handle absent rollback files; rollback files are plain SQL next to the migration. The run
result records the outcome: `PartialSuccess` (50) since 0.13.0 for `Ignore` runs, `Recovered` (80) since 0.14.0 for a clean
rollback chain, `Error` (90) otherwise; the exit code stays `1` for all three. Since 0.20.0 a cancelled migrate-up reuses these
chains through `MigrationCancelledAction` and records `Cancelled` (70), or `Error` when its rollback did not complete cleanly (ADR-028).

**Consequences.** Positive: one setting per environment expresses the policy, and `info` distinguishes a recovered run from a
broken one. Negative: `StopRollbackOnMissingRollbackFile` is parsed in `migsettings` and TOML but consulted only from the CLI,
target group and product levels; a rollback chain on an engine without transactional DDL is best effort; and the semantics were
corrected twice after release (#15, #18), a sign of how many combinations the five by two matrix produces.

**References.** [Error Handling Strategies](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#error-handling-strategies), [Error handling](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/error-handling.md), [Rollback files](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/rollback-files.md), CHANGELOG 0.13.0 (#15, #18), 0.14.0 (#18).

### ADR-021: Configuration arrays merge by alias in every application

**Context.** The `appsettings*.json` hierarchy is merged by the plain `Microsoft.Extensions.Configuration` JSON provider,
which flattens arrays into indexed keys and therefore overrides `Products`, `TargetGroups`, `Targets` and `CliTools`
elements by position. Authors identify these elements by `Alias`. An environment file that lists products in another
order than the base file silently attaches its values, including the target groups, to the wrong product; reproduced on
2026-09-23, a `migrate-up` for one product created its table in the other product's database with exit code 0. The Config
Wizard already merged by alias, so its preview and the engine disagreed.

**Decision.** Every array whose elements are objects with a string `Alias` is merged by alias (case-insensitive) at every
nesting level, base order preserved, new aliases appended, omitted elements kept; every other array is replaced as a whole
by the later file. One shared implementation (`ConfigurationJsonMerger` in `Raycoon.RayMigrator.Shared`) is used by the
engine's `JsonOptionsSource`, and through it by RayMigrator Studio's standalone mode, and by the wizard's `ConfigFileMerger`.
The engine merges the JSON documents before they enter the configuration builder, so binding, validation and `{ENV:}`
replacement are untouched. Engine and wizard share the code, not only the rules: besides the merger, `Shared` owns the
alias comparer, the names and order of the four hierarchy files (`ConfigurationFileChain`) and the JSON reader options,
and golden merge cases under `Testing/ConfigMergeCases/` run through both test suites. Accepted and implemented on
2026-09-23 for 0.15.0 (#23, Part A); `P0_ConfigurationJsonMergerTests`, `P0_ConfigurationFileChainTests`,
`P1_JsonOptionsSourceAliasMergeTests` and `ConfigFileMergerSharedTests` pin the semantics. The wizard's export side
(#23, Part B) followed the same day: `ConfigurationJsonDiff` in `Shared` provides the inverse operations of the merge,
`HierarchyFactoring` in the wizard core places every value in the highest file in which it holds for every combination
that file applies to, product elements in the base or their product-environment file, and golden export cases under
`Testing/ConfigExportCases/` are read back by the engine's loader in `Tests.Unit`.

**Consequences.** Positive: overrides can name only the element they change, in any order; the wizard shows exactly what
the engine runs; the rule is generic, so a future alias-bearing array needs no code change. Negative: a breaking behaviour
change for hierarchies that relied on positional overrides of non-alias arrays (`Serilog.WriteTo` is replaced, not
merged); `reloadOnChange` is given up, which a CLI run never used.

**References.** [Configuration hierarchy](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/06-configuration-reference/appsettings-hierarchy.md#arrays), [Config Wizard file hierarchy](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/12-config-wizard/file-hierarchy.md), [issue #23](https://github.com/RAYCOON/RayMigrator/issues/23).

### ADR-022: Transient error codes ship as an editable file per DAL folder

**Context.** Every DAL hardcoded the provider error codes that trigger a retry (`DalSqlServer.s_transientCodes` and its
siblings). The 4021/596 incident of 2026-09-28 (a migration ran `ALTER LOGIN` on the migration login, SQL Server killed
every pooled session, and the repository update failed although `DbCommandMaxRetries` was set) showed that operators may
have to adjust that list without waiting for a release. An `appsettings` section (`RayMigrator:TransientErrorCodes`,
versioned, with per-environment overrides and Config Wizard support) was considered.

**Decision.** Each DAL folder `DataAccessLayers/{DatabaseType}/` ships a `TransientErrorCodes.txt` next to the DLL and the
SQL templates: one code per line, `#` comments, compared case-insensitively (PostgreSQL SQLSTATEs such as `57P01`). The
file is the complete list and replaces the built-in list; it does not merge with it. An existing but empty file means
"retry nothing" and is logged as a Warning. `DalFactory` loads the file when it creates the DAL instance; a missing file
keeps the built-in list (`DalBase.DefaultTransientErrorCodes`, overridden per plugin), a malformed file aborts the start
with a `ConfigurationValidationException` naming the file and the line. `DirectModePipeline` logs the effective source
once per DatabaseType. The pool-clearing list of the SQL Server DAL (4021, 596) stays hardcoded: it is a mechanism, not a
policy. The configuration section was rejected because the list belongs to the engine plugin rather than to a product or
an environment, and because the Config Wizard would have to know provider error codes. Accepted and implemented on
2026-10-10 for 0.16.0 (#24); `P1_TransientErrorCodesFileTests`, `P1_DalTransientErrorCodesTests` and
`P1_DalShippedTransientErrorCodesTests` pin the semantics, the last one guards the shipped files against drift from the
built-in lists.

**Consequences.** Positive: a retry policy change is a text edit next to the binary with the same lifecycle as an edited
SQL template; external DAL authors get the mechanism by overriding `DefaultTransientErrorCodes` and classifying through
`IsTransientCode`. Negative: every publish or upgrade overwrites the folder, so edits must be re-applied after an update
(the trade-off customised templates already have); the file is outside the configuration hierarchy and has no
per-environment variant; a malformed file for the DatabaseLogging DAL surfaces as `ApplicationStartupException` (exit
code 1) instead of 100, because that DAL is created before the pipeline logger exists.

**Alternatives considered.** The `RayMigrator:TransientErrorCodes` configuration section (rejected, see Decision); merging
the file with the built-in list (rejected, because a built-in code could then never be removed).

**References.** [Transient error codes](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/transient-error-codes.md), [Resilience](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/resilience.md), [issue #24](https://github.com/RAYCOON/RayMigrator/issues/24).

### ADR-023: The context factory is the hosting seam: one prepared, activated context per run, and typed progress events

**Context.** RayMigrator Studio hosts the engine in one long-running process and runs one migration per request with options it builds in memory. Until 0.16.0 the engine was hostable only on paper: `MigrationService` read the products and the CLI tools from `IOptions<RayMigratorOptions>` while the repository settings came from the `MigrationContext`, so a context built from other options failed with "Sequence contains no matching element"; `IMigrationContextFactory` existed but no production code used it, and the pipeline and the engine test host repeated the post-build steps (DAL properties, schema names, logging context) by hand; pre-built options skipped the defaults merge, the data annotations and the rule catalog, so null timeouts failed later with an `InvalidOperationException`; and the only progress signal was log text without event ids.

**Decision.** `IMigrationContextFactory` (implemented in `Services`, registered by `AddRayMigratorServices`) is the entry point of every host for one run. `Create` prepares in-memory options with `RayMigratorOptionsPreparer` (placeholders, defaults, enum probe, data annotations, rule catalog) and `CreateFromValidatedOptions` skips the preparation for the CLI; both fill the DAL properties, check the schema names, register the secrets with the masker and make the context current for the calling async flow. `MigrationService` reads everything from the context; `TemplateCache` takes no options and is validated per configuration. The run operations raise typed events through `MigrationContext.ProgressObserver` (`IMigrationProgressObserver`: run, file on target, block, with ids and outcomes), and refuse a request whose run mode or target release differs from the context. The result DTOs name the repository run (`MigrationRunId`) and the target; history and status are per environment. The accessor is a singleton in both host modes. Since 0.18.0 a host creates or upgrades a repository without a run through `IRepositorySchemaService` (`EnsureAsync(RepositoryOptions)` since 0.20.0), which runs the schema steps of ADR-024 from the repository options alone; before that, a host needed a synthetic product to reach `TemplateExecutor.RepositoryCheckCreate`. Since 0.20.0 `IRepositoryMaintenanceService` deletes old history and log rows the same way, from the repository options alone (ADR-027), and a host stops a run through the `CancellationToken` that every method of `IMigrationService` takes or through the `RunCancellation` of its request (ADR-028).

**Consequences.** Positive: a host runs the engine with one factory call per run and no options registration; the CLI and a host take the same path, so the engine test host lost its duplicated steps; progress is observable without parsing log text; a run row records the release it started from. Negative: breaking changes to the service constructor, the factory signature, `TemplateCache`, the result DTOs and the `Repository_MigrationRun_Select` template (allowed by ADR-016); `DirectModePipeline` remains CLI-shaped (static `Log.Logger`, console output, exit code) and is not the host path; the observer is called synchronously on the thread of the run, so a slow observer slows the run.

**Alternatives considered.** Keeping `IOptions` and registering one container per configuration in the host (rejected: every run would build a container, and the two option sources could still diverge). Reporting progress through a Serilog sink only (rejected: the per-file message has no event id and changes with the wording). Raising the run-started event before the run row exists (rejected: a host needs the run id to correlate the live run with the repository).

**References.** `Raycoon.RayMigrator.Core/IMigrationContextFactory.cs`, `Raycoon.RayMigrator.Services/MigrationContextFactory.cs`, `Raycoon.RayMigrator.Core/Configuration/Validation/RayMigratorOptionsPreparer.cs`, `Raycoon.RayMigrator.Core/Progress/`, [MigrationContext: Hosting the engine in-process](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/migration-context.md#hosting-the-engine-in-process), `CHANGELOG.md` (0.17.0).

### ADR-024: The repository schema is versioned and upgraded in numbered steps

**Context.** RayMigrator Studio makes the repository the database of truth for runs, logs and its own configuration and keeps it across engine releases, while ADR-007 and ADR-016 answered every schema change with drop and recreate, which discards the audit trail and that configuration with it. `Repository_CheckCreate` created all 11 tables in one script, counted them to detect a corrupt repository (`-10`, `-11`), and no engine knew which schema a repository had beyond the first `MigratorMeta` row.

**Decision.** Since 0.17.0 the repository carries a `RepositorySchemaVersion` table with one row per applied schema version. `RepositorySchema.CurrentVersion` in Core names the newest version (3 since 0.20.0, whose step adds the `Cancelled` lookup row of ADR-028 and four indexes for the readers of the repository); every DAL ships `Repository_Upgrade_{n}` for every version from 1 to `CurrentVersion`, each creating its version on top of the one before, idempotent per engine and serialised against a parallel start (`sp_getapplock`, `pg_advisory_xact_lock`, `GET_LOCK()`, the SQLite busy timeout); a step that fails because the other process applied it first is accepted when the version row is there. `Repository_CheckCreate` creates only the version table and reports the version found; `TemplateExecutor.RepositoryCheckCreateAsync` applies the missing steps in order and then registers the engine version through `Repository_MigratorMeta_CheckInsert`. A fresh repository runs every step from 1, so the steps are the single source of the schema. Before 1.0 a step may still be corrected in place when the change does not alter the data of a repository that already has the step (0.18.0 put the table prefix into the key names; a 0.17.0 repository keeps its names and keeps working); after 1.0 a step never changes. A repository with tables but no version row was created before 0.17.0 and is refused with `RepositorySchemaException` (recreated once, as ADR-016 allows); a repository newer than the engine is refused as well. ADR-007 keeps the separate repository; its "recreated instead of upgraded" part and the "no in-place repository upgrades" part of ADR-016 are superseded.

**Consequences.** Positive: a schema change is one new step template per engine and a bump of `CurrentVersion`, data survives engine updates, Studio reads the version before it touches a repository, and the table count codes are gone. Negative: the repository account keeps DDL rights for every later upgrade, because the step runs inside the first command of a newer engine; the log tables are outside the versioned schema, so `DatabaseLogging_CheckCreate` adds what a newer engine needs to an existing log table (the two indexes of 0.20.0) and the account of the log needs the right to create indexes once, at the first start after that update; MySQL and MariaDB cannot roll a failed step back, so their steps are idempotent per statement instead of transactional; external DALs must ship the step templates; and repositories of 0.16.0 and earlier are recreated one last time.

**Alternatives considered.** A version column in `MigratorMeta` (rejected: that table records engine versions, not schema versions, and a step history shows partial upgrades). Branching on the version inside `Repository_CheckCreate` in SQL (rejected: SQLite cannot branch, and the template would grow with every version). Dropping and recreating as before (rejected: Studio keeps configuration in the repository).

**References.** `Raycoon.RayMigrator.Core/Templates/RepositorySchema.cs`, `Raycoon.RayMigrator.Infrastructure/RepositorySchemaExecutor.cs`, [Repository schema: Schema versioning](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/repository-schema.md#schema-versioning), `CHANGELOG.md` (0.17.0).

### ADR-025: Technology-neutral read model of the repository

**Context.** RayMigrator Studio and other tools read the repository (runs, records, logs) on five engines whose names differ (PascalCase or snake_case, a schema or the connection's database, a configured prefix) and whose providers return different .NET types for the same column. Studio maps the tables with EF Core; the engine must not depend on EF Core (multi-target packages, no provider for every engine in this repository), and the names must not be copied by hand into every reader.

**Decision.** The package `Raycoon.RayMigrator.Repository.ReadModel` carries the contract: `RepositoryTable`; `RepositoryNaming`, which derives the table and column names of an engine from `DatabaseType`, `SchemaName` and `TableBaseName` and quotes as the templates do (`ToSnakeCase` with the `RayMigrator` exception); one positional record per table whose constructor parameters are the columns in PascalCase (`RepositoryRows.ColumnsOf` reads them from the record); `RepositoryRowMapper` for ADO.NET rows (integer widths, enums, UTC timestamps, SQLite text, `DBNull`); and `RepositoryReadModel.SchemaVersion`, which is `RepositorySchema.CurrentVersion`. The package references Core for the enums and the schema version and nothing else. `ReadModelContractTests` on all five engines read every table after a real migrate-up with the package's statements through the engine DAL and map every row.

**Consequences.** Positive: one source of names for every reader; a reader detects a schema mismatch by comparing the version; Studio generates its EF Core mappings from the records; the Testing package takes its snake_case rule from the package. Negative: a new column touches the template, the record and, through the record, the contract test; the package versions with the engine, and a reader must match the engine release of the repository it reads; on MySQL and MariaDB a reader's session must run in UTC (`SET time_zone = '+00:00'`), because the `TIMESTAMP` columns are converted to the session time zone on read.

**Alternatives considered.** EF Core entities in the engine (rejected: EF Core dependency and a MySQL provider question in the engine; Studio decided that separately). Reading through `IMigrationService` DTOs only (rejected: reports need rows, joins and paging, not operation results). Copying the names into Studio (rejected: drifts with every schema step).

**References.** `Raycoon.RayMigrator.Repository.ReadModel/`, [Repository schema: Read model package](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/repository-schema.md#read-model-package), `Raycoon.RayMigrator.Tests.Engine/Infrastructure/ReadModelContract.cs`, `CHANGELOG.md` (0.17.0).

### ADR-026: A run binds and validates only its own product

**Context.** A merged `appsettings*.json` hierarchy names every product of an installation: the base file lists them all, and the connection strings of a product differ per environment and per product. Up to 0.18.0 the engine bound and validated the whole merged document of a run: the data annotations descended into every product, the rule catalog, the template check and the masker saw every product, and every `{ENV:...}` placeholder had to resolve. A connection string that product B keeps in `appsettings.B.Prod.json`, a file the chain of product A never loads, therefore failed every run of A in Prod; the Config Wizard's export, which places a product's own values in that product's product-environment file (ADR-021, file-hierarchy placement), produced families the engine refused as soon as two products existed, and RayMigrator Studio had to put the connection strings of every product into the environment document to work around it.

**Decision.** A run sees the shared sections and its own product only. `ConfigurationProductScope.Isolate` (`Raycoon.RayMigrator.Shared`) reduces the merged document to the shared sections (`Repository`, `DatabaseLogging`, `ProductDefaults`, `Serilog`, `CliTools`) and the elements of `Products` whose alias equals the product of the run, compared like the merger compares aliases; the other products and elements without an alias are left out, elements that repeat the alias stay for RULE_1_8. `JsonOptionsSource` applies it after the merge and before the document is bound, so the bound options and the section as written, which the rule catalog pairs by index for RULE_7_3, describe one product; `IMigrationContextFactory.Create` applies the same rule to a copy of options a host built in memory (`RayMigratorOptionsPreparer.IsolateProduct`), and hosts such as RayMigrator Studio that bind a document of their own are to apply `ConfigurationProductScope` first. A product the files do not name fails while they are loaded, with the aliases they do name. Every `CliTools` entry stays in scope, because a migration file's TOML header or a `migsettings.txt` may name any of them.

**Consequences.** Positive: a fault in one product (a missing connection string, an unknown database type, an unset variable, an unparseable enum) cannot stop another product's run; the file hierarchy keeps its meaning (`appsettings.{Product}.{Environment}.json` holds what a product changes in an environment) and the wizard's export runs as exported; hosts such as RayMigrator Studio are to validate and run each product of a combination in this scope with the same helper. Negative: RULE_1_8 and RULE_3_3 judge only the product of a run, so a duplicate alias of another product or its dangling CLI tool reference surfaces when that product runs, or in a whole-document check, which stays where no product is selected (the wizard's base model, a host's documents without a product); elements of `Products` without an alias are no longer reported by a run; the configuration a verbose run prints shows one product.

**Alternatives considered.** Removing the other products from the bound options after binding (rejected: the section as written is paired with the bound lists by index for RULE_7_3, and the placeholders, the enum probe and the masker would still see every product). Validating every product but failing only on the run's product (rejected: a variable of another product that is not set on this machine would still abort, and the catalog would still report the other products). Keeping the whole-document validation and moving the connection strings to the environment files (rejected: it contradicts the placement rule of the hierarchy and the wizard, and it makes every environment file carry the secrets of every product).

**References.** `Raycoon.RayMigrator.Shared/Configuration/ConfigurationProductScope.cs`, `Raycoon.RayMigrator.Pipeline/JsonOptionsSource.cs`, `Raycoon.RayMigrator.Core/Configuration/Validation/RayMigratorOptionsPreparer.cs`, [Configuration System: Validation](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/configuration-system.md#validation), [Validation rules: Product scope of a run](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/validation-rules.md#product-scope-of-a-run), `Raycoon.RayMigrator.Tests.Engine/Infrastructure/ProductIsolationContract.cs`, `CHANGELOG.md` (0.19.0).

### ADR-027: The engine deletes log and history rows on a host's request and never reads them for a decision

**Context.** `MigrationLog` and `MigrationRecordHistory` grow with every run: the database log keeps the events of every command that changes the repository, and every terminal transition of a record adds a history row (#13). RayMigrator Studio is to apply a retention policy to the repositories it manages. The repository schema belongs to the engine (ADR-024, ADR-025): its names depend on the engine and the prefix, its steps change it, and a host reads it through the read model but runs no SQL of its own against it. Up to 0.19.0 no template deleted a row (`Repository_Drop` is commented out on every engine), and the Example DAL carried an archive placeholder without a `TemplateType` (TD-C-09). Before rows may go, it must be clear which decisions of the engine read them.

**Decision.** The engine reads neither table for a decision, and a host deletes their old rows through the engine. Besides the retention templates, which count the rows they delete, `MigrationRecordHistory` has one reader: `TemplateExecutor.RepositoryMigrationRecordHistorySelectAsync`, which `MigrationService.GetHistoryAsync` calls to count the migrations of every run (`SummarizeRun`) for the run history that `RayMigratorService.ExecuteInfoAsync` prints. `MigrationLog` has no other reader in the engine: `DatabaseLogging_Insert` writes it, and outside the engine hosts read it through the read model (`MigrationLogRow`) and the tests through `RepositoryQueryHelper`. Every decision of a command (the highest release, the pending files, hash validation, the out-of-order check, the resume point, the rollback selection, the reuse of a record) reads `MigrationRecord`, and the run lock and the orphan repair read the open rows of `MigrationRun`; an engine change that decides on either table supersedes this decision. `IRepositoryMaintenanceService.CleanUpAsync(RepositoryOptions, RetentionRequest, CancellationToken)` (implemented by `RepositoryMaintenanceService`, registered by `AddRayMigratorServices` as a singleton next to `IRepositorySchemaService`) deletes, from the repository options alone, the history rows historized before one cutoff and the log rows created before another. It validates the options like the schema service, which shares that code now, reads the schema version through `RepositorySchemaExecutor.CheckVersionAsync` (a database without a repository, a repository without a schema version and a newer one are refused, an older one is cleaned as it is), and runs four templates per DAL through `RepositoryMaintenanceExecutor`: `Repository_MigrationRecordHistory_Delete` and `_Count`, `DatabaseLogging_Delete` and `_Count`. The loop is the engine's: the history first, then the log, in batches of one DELETE that commit on their own, the oldest rows first, until a batch deletes fewer rows than `BatchSize` (2,000 by default) or the time budget (60 seconds by default, checked before every batch once a batch has deleted rows) is spent; a dry run counts the same rows. The templates and the check of the schema version run with the command timeout of the repository but without its retries: a statement that timed out, and on SQLite one that reports BUSY after its busy timeout, would wait the whole command timeout again on every retry, and the default of 100 retries turned a budget of a minute into hours; the next batch and the next call are the retry. An error after a batch deleted rows returns `Completed = false` with the rows deleted so far and the error in `Message`; an error before that throws. Before it deletes from the log, the cleanup asks `DatabaseLogging_Count` with `@ExistsOnly = 1` whether the log table is there, without a count; the dry run counts in full. Every batch spares the rows of a run that is still open (`MigrationRun.FinishedAt` null), and a log row without a run falls by its age. The log is cleaned only where it lives with the repository, in the same database under the schema and prefix of the repository, its default home since 0.17.0; elsewhere the result reports `LogSkipped`. `MigrationRecord`, `MigrationRun` and `MigrationRunMeta` are never deleted. The token is checked before every batch and reaches the batch in progress; a cancel while the cleanup deletes, or while it checks the log table, returns what it deleted so far.

**Consequences.** Positive: a host applies a retention policy without SQL of its own for five dialects, and the schema boundary of ADR-024 and ADR-025 holds; no deletion changes a status, a pending list or any other decision of the engine; every batch is short and its commit durable, so a cancel, a spent budget or a failure keeps the progress and a later call continues; a failed statement waits its command timeout once; 2,000 rows keep a batch on SQL Server below lock escalation (5,000 locks on one index), which would hold back the log writes of running hosts: measured with the indexes of schema version 3, history batches of 4,800 rows and log batches of 5,000 rows escalated. Negative: the run history of `info` reports no migrations for a run whose history rows are gone; a batch that failed is not retried within the call, the next call continues; every DAL ships four more templates, and `TemplateCache` refuses a DAL folder that lacks one of them, whatever database a command uses (breaking for external DALs); a log of its own elsewhere is left to its owner; the guard reads the `MigrationRun` table of this repository only, so when another repository writes into the same log table (same database, schema and prefix), the rows of its open runs fall by their age; serialising the cleanups of a repository is the host's business, two at once only delete rows that match; the version check runs `Repository_CheckCreate`, which in a database without a repository creates the empty version table (on SQL Server and PostgreSQL also the schema) before the call is refused; an older repository is cleaned without its upgrade, so below schema version 3 every history batch reads the whole table (logged as a warning); on SQL Server a batch deletes in the order of the clustered key with `OPTION (LOOP JOIN)`, because ordered by the time column the optimizer read every matching row for each batch, and 1,000,000 log rows took 170 seconds instead of 51.

**Alternatives considered.** The host deletes with SQL of its own, as first planned for Studio (rejected: it would copy the names and prefix rules of five engines and drift with every schema step, and the guard of open runs needs the engine's notion of an open run). An API that deletes one batch per call and leaves the loop to the host (rejected: every host would repeat the stop rule, the time budget, the order of the two tables and the handling of a cancel, and every call would validate the options and read the schema version again).

**References.** `Raycoon.RayMigrator.Services.Abstractions/IRepositoryMaintenanceService.cs`, `Raycoon.RayMigrator.Services/RepositoryMaintenanceService.cs`, `Raycoon.RayMigrator.Infrastructure/RepositoryMaintenanceExecutor.cs`, [Repository schema: Retention](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/repository-schema.md#retention), [Logging schema](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/logging-schema.md), `Raycoon.RayMigrator.Tests.Engine/Infrastructure/RepositoryMaintenanceContract.cs`, `CHANGELOG.md` (0.20.0).

### ADR-028: A run cancels at the next block boundary with a `MigrationCancelledAction` and records `Cancelled`

**Context.** A run could not be stopped. RayMigrator Studio needs a stop for the runs it hosts, and Ctrl+C or the SIGTERM of a CI runner or a container stop killed the CLI in the middle of a block: the run row stayed `Running`, the record of the file in progress `Executing` or `Pending`, and only the orphan repair of a later start or `fix` closed them. No token reached the database: `TemplateExecutor` blocked on the asynchronous DAL (`GetAwaiter().GetResult()`), `MigrationService` moved its 65 executor calls to the thread pool with `Task.Run`, a transient error was retried regardless, and `CliToolExecutor` killed a tool only on its timeout. A stop must leave target and repository in a state the next command understands, and it must not take longer than the user can wait.

**Decision.** A cancel stops a run at the next block boundary and then takes the action the host chose. Every method of `IMigrationService` takes a `CancellationToken`, and `MigrateUpRequest`, `MigrateDownRequest` and `BaselineRequest` carry a `RunCancellation` (`Services.Abstractions`) whose first `Cancel(action, reason)` decides the `MigrationCancelledAction`, a value of the run and not a configuration option: `Terminate` (10, the default and the action of a token without a handle) only stops; `RollbackRun` (20), `RollbackFile` (21) and `RollbackRelease` (22) run the rollback chain of the `MigrationErrorAction` with the same number (`Rollback`, `RollbackErrorOnly`, `RollbackRelease`) with its rules for missing rollback files and `RollbackErrorAction`, and that rollback cannot be cancelled again. There are checkpoints before every file, target and block; the token reaches the target DAL, which turns a cancelled call into an `OperationCanceledException` and never retries it, the atomic transaction of a file, and the CLI tool, which is killed with its child processes. The interrupted file reports `FileOutcome.Cancelled` (100), and its record is `Failed` with the blocks that committed (on the atomic path only those of an earlier attempt, normally none), so the next migrate-up resumes it like a failed file. Repository writes on a connection of their own never take the token, so a record and its history row change together; on the atomic path the record updates are part of the file's transaction, which the token may interrupt and which then rolls back as a whole. Three rollback units always complete: the first block of a rollback file, an atomic rollback and a rollback through a CLI tool, because stopped earlier the record would be `Failed` without rollback progress, which migrate-down does not continue and migrate-up runs again from its first block. A record whose rollback stopped later, `Failed` with k of its n rollback blocks, is continued by migrate-down at block k+1, and migrate-up refuses to run before its first file while a file it would run has such a record: the record insert would reset the rollback columns and run the file from block 1 on a target that kept part of it. The refused run is `Error`, its message names release, file, target and k of n, and the status lists the pair as `PendingMigrationReason.RollbackIncomplete` (60). The run is recorded as `MigrationRunResult.Cancelled` (70, a lookup row of schema step 3) with `TemplateResultCode.Cancelled` (1004) and the message `Cancelled on request (<action>): <outcome>`, or as `Error` with `TemplateResultCode.CancelledRollbackIncomplete` (1005) when its rollback reported a failure or a warning, the rule that decides `Recovered` against `Error` after a failure. Migrate-down, baseline and a running error recovery stop and ignore the action. A migrate-up that failed at a file and whose error recovery the cancel stopped stays `Error` with the code of that failure: the run failed before the cancel, and the cancel only stopped the handling of the failure. Its message names the failure first and then how many rollbacks the recovery completed (`<failure>. The error recovery was cancelled on request after <n> of <m> rollback(s); the rollback is incomplete, migrate-down continues it`); update-hash and fix return what they did so far with the error code 1004; validate-hash, status and history throw `OperationCanceledException`. The CLI turns the first Ctrl+C, and on Linux and macOS the first SIGTERM, into a cancel with the action of `--cancelled-action` (`-ca`) and exits with 130, or with 1 when the run ended as `Error`; a second signal ends the process at once.

**Consequences.** Positive: a host and the CLI stop a run without leaving it open, the repository records why it stopped, the default `Terminate` leaves nothing the next migrate-up cannot resume, and the error rates of a host do not count cancels, while a failure whose error recovery a cancel stopped still counts as a failure; the repository path awaits the DAL (`TemplateExecutor`, `IRepositorySchemaService.EnsureAsync`, `DatabaseLogWriter.InitDatabaseLoggerAsync`) instead of holding a pool thread. Negative: how fast a block stops depends on the provider: SQL Server interrupts the statement in progress within about 10 milliseconds, PostgreSQL and MariaDB at once (on PostgreSQL the transaction is unusable afterwards and rolls back), MySQL ends only the statement in progress through `KILL QUERY` and runs the further statements of the block, so only the next block boundary stops it, the `KILL QUERY` of MySQL and MariaDB ends a `SLEEP()` early without an error, so such a block runs to its end, and SQLite cannot interrupt a statement and checks before a block only; a rollback action makes a cancel last as long as its rollback; the signatures of `IDal`, `TemplateExecutor`, `IRepositorySchemaService`, the pipeline and every implementation of `IMigrationService` changed (allowed by ADR-016), and the lookup row needed schema step 3; a second signal leaves the run `Running` until the orphan repair after `Repository.OrphanedRunThresholdInMinutes` or `fix`; a rollback that stopped in the middle blocks every migrate-up that would run its file until migrate-down has completed it. Known gaps: a rollback whose first block fails leaves its record `Failed` without a rollback block, as before 0.20.0, and migrate-up runs that file again from its first block; a cancel under `MigrationErrorAction=Ignore` stores the block count of the file on the `Failed` record, as an ignored failure does, so the next migrate-up runs the whole file again, the committed blocks included.

**Alternatives considered.** A record status `Cancelled` (rejected: the resume point, the rollback selection, the run counters and every reader would have to learn it, while `Failed` with `FileOutcome.Cancelled` says the same). Running the configured `MigrationErrorAction` on a cancel (rejected: a cancel is no error, the action would make a stop as long as a rollback nobody asked for, and that rollback could not be stopped; a host that wants a rollback chooses it through `MigrationCancelledAction`). Recording a cancel as `Error` with an error code (rejected: error rates would count cancels as failures, and the history would show `Error`). Recording a failed migrate-up whose error recovery a cancel stopped as `Cancelled` with 1004, which the CLI would end with exit code 130 (rejected: the run failed at the file before the cancel, which only stopped the handling of that failure; the result would hide the failure from error rates, from a pipeline that decides by the exit code, and from the operator, who must see the failed file). Keeping `Task.Run` and only adding tokens (rejected: it keeps the detour through the thread pool and the synchronous repository updates inside the open file transaction). Letting the `ConsoleLifetime` of the generic host handle the signals (rejected: the pipeline never starts the host, and the change would rebuild the CLI pipeline without a gain for Studio).

**References.** `Raycoon.RayMigrator.Services.Abstractions/Models/RunCancellation.cs`, `Raycoon.RayMigrator.Core/Configuration/Enums/MigrationCancelledAction.cs`, `Raycoon.RayMigrator.Services/MigrationService.cs` (`CompleteCancelledMigrateUpAsync`, `HandleStopAsync`), `Raycoon.RayMigrator.Pipeline/ConsoleCancellation.cs`, [Error handling: Cancellation](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/error-handling.md#cancellation), `Raycoon.RayMigrator.Tests.Engine/Infrastructure/CancellationContract.cs`, `CHANGELOG.md` (0.20.0).

## Related documentation

- [Solution Strategy](04-Solution-Strategy.md), [Crosscutting Concepts](08-Crosscutting-Concepts.md), [Risks and Technical Debt](11-Risks-and-Technical-Debt.md)
- [Design decisions](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md), [Architectural patterns](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/patterns.md), [Changelog](https://github.com/RAYCOON/RayMigrator/blob/main/CHANGELOG.md), [License change dates](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/license-change-dates.md)
