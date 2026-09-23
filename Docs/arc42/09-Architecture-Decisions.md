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
| ADR-007 | Repository separate from targets, recreated instead of upgraded | Accepted (refined 0.14.0) | <= 0.10.3 | [Migration repository schema](08-Crosscutting-Concepts.md#migration-repository-schema), [Migration history and info](08-Crosscutting-Concepts.md#migration-history-and-info) |
| ADR-008 | Block-level execution with per-block transactions | Accepted (refined 0.13.0) | <= 0.10.3 | [Block-Level Execution and Transactions](08-Crosscutting-Concepts.md#block-level-execution-and-transactions) |
| ADR-009 | Flat directory layout auto-detection | Accepted | <= 0.10.3 | [Migration files, releases and metadata](08-Crosscutting-Concepts.md#migration-files-releases-and-metadata), [Deterministic File Ordering](08-Crosscutting-Concepts.md#deterministic-file-ordering) |
| ADR-010 | No parallel database execution | Accepted | <= 0.10.3 | [Execution modes](08-Crosscutting-Concepts.md#execution-modes), [Exclusive Run Lock and Orphaned Run Detection](08-Crosscutting-Concepts.md#exclusive-run-lock-and-orphaned-run-detection) |
| ADR-011 | External CLI tool execution as an alternative execution path | Accepted | <= 0.10.3 | [Credentials and secrets](08-Crosscutting-Concepts.md#credentials-and-secrets), [Runtime View 6.8](06-Runtime-View.md#68-external-cli-tool-execution) |
| ADR-012 | Atomic shared connection | Accepted | <= 0.10.3 | [Block-Level Execution and Transactions](08-Crosscutting-Concepts.md#block-level-execution-and-transactions), [Retry on Transient Errors](08-Crosscutting-Concepts.md#retry-on-transient-errors) |
| ADR-013 | Hash validation scopes | Accepted | <= 0.10.3 | [Hash Validation](08-Crosscutting-Concepts.md#hash-validation) |
| ADR-014 | Configuration inheritance with rule-based validation | Accepted | <= 0.10.3 | [Configuration Inheritance with Validation](08-Crosscutting-Concepts.md#configuration-inheritance-with-validation) |
| ADR-015 | Multi-framework targeting | Accepted | <= 0.10.3 | [Architecture Constraints](02-Architecture-Constraints.md#platform-and-toolchain) |
| ADR-016 | Pre-1.0 compatibility policy: no aliases, no in-place upgrades | Accepted | 0.14.0 | [Error model and exit codes](08-Crosscutting-Concepts.md#error-model-and-exit-codes) |
| ADR-017 | Business Source License 1.1 with Additional Use Grant | Accepted (supersedes RMLA v1.0) | 0.11.0 | [Architecture Constraints](02-Architecture-Constraints.md#23-conventions) |
| ADR-018 | Structured logging with a database sink | Accepted (refined 0.12.0, 0.13.0) | <= 0.10.3 | [Structured Logging to Console, File and Database](08-Crosscutting-Concepts.md#structured-logging-to-console-file-and-database) |
| ADR-019 | Blazor WebAssembly Config Wizard as a separate, database-free tool | Accepted | <= 0.10.3 | [Configuration Inheritance with Validation](08-Crosscutting-Concepts.md#configuration-inheritance-with-validation), [Runtime View 6.9](06-Runtime-View.md#69-config-wizard-session-short) |
| ADR-020 | Error handling strategies with rollback files and run results | Accepted (refined 0.13.0, 0.14.0) | <= 0.10.3 | [Rollback Strategies](08-Crosscutting-Concepts.md#rollback-strategies) |

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
per `{databaseType}_{connectionString}`. Engine facts are data in `DalSpecificProperties`; transient error codes live in each
plugin's `IsTransient` override. `Raycoon.RayMigrator.Database.Example` is the MIT licensed skeleton.

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
`TemplateType` (21 besides `Undefined`), loaded by `TemplateCache` at startup and executed by `TemplateExecutor`. Every template
returns one scalar `ResultCode,ResultMessage`; negative codes map to the `TemplateResultCode` catalog and raise
`TemplateResultException`. Placeholders are `{ENV:*}` (once, at load), `{CFG:*}` (per call, `SchemaName` and `TableBaseName` only)
and `@Parameter` bindings; `TemplateCache` checks completeness at startup; `validateConfiguration = false` only skips the check that every configured `DatabaseType` has a template folder.

**Consequences.** Positive: no ORM, engine specific locking primitives (`UPDLOCK, HOLDLOCK`, `pg_advisory_xact_lock`, `GET_LOCK`)
live in SQL where they belong, and a DBA can customize a template. Negative: 21 templates times five engines to keep in sync, a
result contract that every external DAL must reproduce exactly (0.12.0 and 0.13.0 each added templates that external DALs had to
ship), and `TemplateExecutor` is synchronous over async DAL calls (`GetAwaiter().GetResult()`).

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

**References.** [Repository Separate from Targets](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#repository-separate-from-targets), [Repository schema](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/repository-schema.md), CHANGELOG 0.14.0 (#6, #12, #18 and the `MigratorMeta` follow-up).

### ADR-008: Block-level execution with per-block transactions

**Context.** Migration files can be long, SQL Server requires `GO` separated batches, and an interrupted run should not force a
manual repair of half applied files.

**Decision.** `SplitSqlIntoBlocks` splits a file at the engine's `SqlBlockDelimiter` (`GO` on SQL Server, `;` elsewhere); each block
is one `IDal.ExecuteNonQueryAsync` call in its own transaction when `UseTransaction` is `true`, and `RepositoryMigrationUpdate`
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
`MigrationContextEnricher` from `MigrationLoggingContext.Current`; an optional `DatabaseLogging` node adds `RayMigratorDatabaseSink`,
which enqueues into the asynchronous `DatabaseLoggerQueue` and writes through the `DatabaseLogging_Insert` template into
`MigrationLog`, gated by `DbLogEnabled` from the command's `CommandProfile`. `SensitiveDataMasker` hides secrets unless
`--reveal-sensitive-data` is given.

**Consequences.** Positive: the same enriched event reaches every sink, and the log database may live on a different server than
the repository. Negative: the sink needs its own DAL and template set, rows are lost if the process dies before `DirectModePipeline`
flushes the queue, and two defects surfaced late: `info` filled the audit log while `validate-hash` left no trace until the profile
gate (0.12.0, #6), and every row carried `EventId` 0 until 0.13.0 (#12).

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
rollback chain, `Error` (90) otherwise; the exit code stays `1` for all three.

**Consequences.** Positive: one setting per environment expresses the policy, and `info` distinguishes a recovered run from a
broken one. Negative: `StopRollbackOnMissingRollbackFile` is parsed in `migsettings` and TOML but consulted only from the CLI,
target group and product levels; a rollback chain on an engine without transactional DDL is best effort; and the semantics were
corrected twice after release (#15, #18), a sign of how many combinations the five by two matrix produces.

**References.** [Error Handling Strategies](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md#error-handling-strategies), [Error handling](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/error-handling.md), [Rollback files](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/07-migration-files/rollback-files.md), CHANGELOG 0.13.0 (#15, #18), 0.14.0 (#18).

## Related documentation

- [Solution Strategy](04-Solution-Strategy.md), [Crosscutting Concepts](08-Crosscutting-Concepts.md), [Risks and Technical Debt](11-Risks-and-Technical-Debt.md)
- [Design decisions](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/design-decisions.md), [Architectural patterns](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/01-architecture/patterns.md), [Changelog](https://github.com/RAYCOON/RayMigrator/blob/main/CHANGELOG.md), [License change dates](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/license-change-dates.md)
