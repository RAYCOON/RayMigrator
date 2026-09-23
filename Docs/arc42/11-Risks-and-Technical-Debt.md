# 11. Risks and Technical Debt

This chapter answers which known risks threaten the quality goals of
[Introduction and Goals](01-Introduction-and-Goals.md#12-quality-goals), which
mitigations already exist for them, and which technical debt in code,
database templates, documentation and test coverage a maintainer or adopter
should know about. RayMigrator is a pre-1.0 product (release line 0.14.x). Its
`README.md` maturity notice states that the behavior has not yet been proven
across a broad range of production workloads and asks for a verified backup
before every run; everything below is to be read against that background.

## 11.1 Risk Assessment Method

A *risk* is an uncertain event outside the control of the code that can hurt a
quality goal at run time; a *technical debt* item is a known deficiency inside
the repository (code, templates, documentation, tests) that costs effort later
but is not uncertain. Both are rated on a three step scale:

| Scale | Probability | Impact |
|-------|-------------|--------|
| Low | unlikely in a typical installation | inconvenience, recoverable by re-running |
| Medium | plausible in some installations | manual repair of the migration repository or a target, no data loss |
| High | expected to happen to some adopters | data loss, schema corruption or a silently wrong audit trail |

Risks are reviewed with each minor release. Lists are maintained in the public
[GitHub issue tracker](https://github.com/RAYCOON/RayMigrator/issues), in the
`Docs/todo/` folder (the [DAL best practices audit](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-best-practices-audit.md)
and its plan files), in the `Unreleased` section of
[CHANGELOG.md](https://github.com/RAYCOON/RayMigrator/blob/main/CHANGELOG.md)
when one exists, and in
[Open features](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/open-features.md).
Security relevant risks follow the process in
[SECURITY.md](https://github.com/RAYCOON/RayMigrator/blob/main/SECURITY.md).

An item enters this chapter in one of three ways:

- a reviewer verifies a discrepancy between code and documentation while
  writing or revising a chapter (the source of most entries in 11.3);
- an engine test, a support case or a GitHub issue shows a behavior that the
  architecture cannot prevent (the source of most entries in 11.2);
- a planned change is deferred with a written plan file under `Docs/todo/`.

An item leaves when the fix is released and `CHANGELOG.md` names it, or when
the maintainers accept it explicitly (see "Accepted risks" below).

## 11.2 Risks

Quality goal names are those of section 1.2. Mitigations link to the concept in
[Crosscutting Concepts](08-Crosscutting-Concepts.md) that implements them.

| ID | Risk | Affected quality goal(s) | Probability | Impact | Mitigation in place | Open action |
|----|------|--------------------------|-------------|--------|---------------------|-------------|
| R-01 | A failing block on MariaDB or MySQL leaves DDL committed because these engines have no transactional DDL (`SupportsTransactionalDdl = false`); `UseTransaction` cannot undo it. | Reliability | Medium | High | [Block-level execution and transactions](08-Crosscutting-Concepts.md#block-level-execution-and-transactions): committed block counter, `LogMigrationSafetyWarnings` before the first block; [Rollback strategies](08-Crosscutting-Concepts.md#rollback-strategies) with rollback files; maturity notice asks for a backup | Authors keep DDL and DML in separate blocks or files on these engines; no automatic protection is possible |
| R-02 | Connection loss or process kill in the middle of a run leaves a file partially applied and the repository behind the target. | Reliability, Functional correctness and integrity | Medium | High | `FindResumableBlock`, `RepositoryMigrationGetInterrupted`, `fix` ([Exclusive run lock and orphaned run detection](08-Crosscutting-Concepts.md#exclusive-run-lock-and-orphaned-run-detection)); `ExecuteSqlBlocksAtomic` when repository and target share a connection; [Retry on transient errors](08-Crosscutting-Concepts.md#retry-on-transient-errors) | Atomic shared connection has engine tests for SQL Server only (`open-features.md`); the block-by-block path keeps a window between target commit and repository update |
| R-03 | An operator runs against the wrong environment, for example a production configuration directory with `DOTNET_ENVIRONMENT` still set from a test shell. | Functional correctness and integrity | Medium | High | [Environments and profiles](08-Crosscutting-Concepts.md#environments-and-profiles): conflict between `--environment` and `DOTNET_ENVIRONMENT` exits `2`, a blank `--environment` without `DOTNET_ENVIRONMENT` exits `3`, no default; `Simulate` and `Validate` modes ([Validate and simulate run modes](08-Crosscutting-Concepts.md#validate-and-simulate-run-modes)); every run records product and environment in `MigrationRun` | The environment name is not cross checked against the connection string or the host; separate `--config-dir` trees per environment are an operator convention |
| R-04 | Credentials are written literally into `appsettings*.json` and end up in version control or in the `MigrationRunSettingsJson` snapshot. | Functional correctness and integrity (audit trail); security expectation of the reviewers in 1.3 | Medium | Medium | [Credentials and secrets](08-Crosscutting-Concepts.md#credentials-and-secrets): `{ENV:NAME}` placeholders, `RULE_7_3` warning for `Password=` and `Pwd=` literals, `SensitiveDataMasker` for connection strings in logs and snapshot | `RULE_7_3` is a warning, not an error; the Config Wizard export contains `example.env` but cannot enforce its use |
| R-05 | Two products that share one target database run at the same time; the exclusive run lock is per product and environment, so both runs proceed and interleave on the target. | Functional correctness and integrity | Low | High | [Exclusive run lock](08-Crosscutting-Concepts.md#exclusive-run-lock-and-orphaned-run-detection) serializes runs of one product and environment inside the repository engine | Model shared databases as one product, or schedule runs sequentially outside RayMigrator; no cross product lock exists |
| R-06 | An external CLI tool (`sqlcmd`, `psql`, `mysql`, ...) is missing on the host, has a different version, or receives placeholder values that need quoting. | Reliability, Portability across database engines | Medium | Medium | [Runtime View 6.8](06-Runtime-View.md#68-external-cli-tool-execution): `Process.Start` failures become `CliToolExecutionException`, timeout kills the process tree, `SuccessExitCodes` matching; validation rules of group 3 | Only the exit code is evaluated, `ArgumentTemplate` values are not quoted by RayMigrator, and no pre-flight check verifies `ExecutablePath` before the first file |
| R-07 | Breaking changes between minor versions (enum names, CLI options, repository schema) break RayMigrator Studio, NuGet consumers and existing repositories; 0.13.0 and 0.14.0 both required dropping and recreating repositories. | Modifiability and extensibility, Operability | High | Medium | `CHANGELOG.md` marks every breaking change; `MigratorMeta.RayMigratorVersion` records the creating version; `SECURITY.md` limits support to the latest 0.14.x | No in-place repository upgrade exists by decision (`Repository_CheckCreate` seeds only at creation); adopters need a drop and recreate procedure per environment until 1.0 |
| R-08 | A small team at RAYCOON maintains five engines, the Config Wizard and the documentation; review capacity and bus factor are limited. | all five goals | High | Medium | [Architecture Constraints 2.2](02-Architecture-Constraints.md#22-organizational-constraints): automated release chain, `Claude Code Review` workflow, CLA for outside contributions, this documentation set | Contributor onboarding depends on `CONTRIBUTING.md`, which does not yet describe the branching model (TD-D-12) |
| R-09 | A vulnerability in a pinned ADO.NET provider or transitive package (as with `SQLitePCLRaw` 2.1.11, CVE-2025-6965) ships in a release. | Reliability; security expectation of the reviewers in 1.3 | Medium | Medium | Central pins with CVE comments in `Directory.Packages.props`, `NuGet.config` restricted to nuget.org, `THIRD-PARTY-NOTICES.md`, private vulnerability reporting per `SECURITY.md` | No automated dependency scanning: `.github/` contains no `dependabot.yml` and no audit step in `build-test.yml` |
| R-10 | A regression on a real engine (template, dialect, provider behavior) reaches a release because CI runs only `Raycoon.RayMigrator.Tests.Unit`. | Reliability, Portability across database engines | Medium | High | [Test pyramid](08-Crosscutting-Concepts.md#test-pyramid): engine suite with `Testing/Docker/docker-compose.yml` as a local duty before a release; `Assert.SkipUnless` keeps it runnable everywhere | Engine job in CI (Docker services on `ubuntu-latest`); see [Quality Requirements](10-Quality-Requirements.md#103-verification-status) |
| R-11 | A SQLite repository is shared between hosts or written by two processes; SQLite is a single writer, file based engine. | Portability across database engines, Reliability | Low | Medium | `DalSqlite` enables `Foreign Keys=true` unless the connection string sets it, and `PRAGMA journal_mode=WAL`; the run lock uses a write transaction | Documented as a limitation only; no detection of a network file system |
| R-12 | Very large migration files (bulk data loads) exhaust memory: `MigrationService` reads each file with `File.ReadAllBytes`, keeps every block in memory and hashes the whole content. | Reliability | Low | Medium | None beyond the operating system limits; CLI tools can stream a file through the vendor client instead (`UseCliToolAlias`) | Size guard or streaming for the ADO.NET path; document a recommended maximum file size |
| R-13 | `HashValidationScope = Disabled` on a target group hides modified migration files; a changed file is neither re-executed nor reported. | Functional correctness and integrity | Low | High | [Hash validation](08-Crosscutting-Concepts.md#hash-validation): default scope `File`, `validate-hash --scope` overrides the configuration, block level resume still requires an unchanged `FileUpBlocksHash` | Startup warning when a target group disables hash validation |

### Risk matrix

| Probability \ Impact | Low | Medium | High |
|----------------------|-----|--------|------|
| High | | R-07, R-08 | |
| Medium | | R-04, R-06, R-09 | R-01, R-02, R-03, R-10 |
| Low | | R-11, R-12 | R-05, R-13 |

The upper right cell is empty: the failure modes that would belong there (a
repository left inconsistent after a failed rollback chain, a run lock
without a repair path) are covered by `MigrationRunResult.Recovered`
(0.14.0, #18), the `fix` command and the auto-fix of orphaned runs (see
[Runtime View 6.7](06-Runtime-View.md#67-concurrency-exclusive-run-lock-and-orphaned-run-recovery-fix)),
which is why R-02 is rated medium probability rather than high.

### Accepted risks

The following limitations are documented in
[Introduction and Goals 1.1](01-Introduction-and-Goals.md#11-requirements-overview)
as deliberate scope boundaries and are not tracked as open risks:

- No parallel execution of targets or target groups; long runs against many
  targets take proportionally longer.
- No schema diffing: a target that was changed outside RayMigrator is not
  detected until a migration file fails on it.
- Rollback depends on the rollback files the author wrote and on the engine's
  transaction capabilities; there is no automatic inverse of a migration.
- Only `OperatingMode.Standalone` is executed by the engine; managed modes are
  the domain of RayMigrator Studio.

## 11.3 Technical Debt

| Category | Items | Highest priority |
|----------|-------|------------------|
| Code | TD-C-01 to TD-C-09 | TD-C-01 (exit code contract for configuration errors) |
| Database access layer audit | DAL-001 to DAL-025 | reconcile plan file status (TD-D-14); all template changes are marked done in the master list |
| Documentation | TD-D-01 to TD-D-12, TD-D-14, TD-D-15 | TD-D-03 to TD-D-05 (user facing pages that contradict 0.14.0 behavior) |
| Test coverage | five gaps below | engine suite in CI (R-10) |

### Debt retired in 0.13.0 and 0.14.0

`CHANGELOG.md` records the cleanup that preceded this chapter; the entries are
listed so that readers of older documentation know which findings are closed.

| Closed item | Release | Reference |
|-------------|---------|-----------|
| `TargetMigrationOrder` names `Simultaneously` / `Successively` replaced by `FileByFile` / `TargetByTarget`; alias mechanism (`EnumAliasAttribute`) removed | 0.13.0, 0.14.0 | #19 |
| `fix --dry-run` replaced by `fix --run-mode simulate`; `FixDryRun`, `DryRun` and `WasDryRun` plumbing removed | 0.14.0 | #22 |
| `MigrationRunResult.PartialSuccess` (50) and `Recovered` (80) added; result of the error recovery chain was previously discarded by `HandleMigrationError` | 0.13.0, 0.14.0 | #18 |
| In-place upgrade blocks for lookup tables and the `MigrationEvent` catalog removed; lookup seeds only at creation on all five engines | 0.14.0 | #6, #12, #18 |
| `MigratorMeta.CreatedByRayMigratorVersion` and the hand maintained `RepositoryVersion` constant removed; `RayMigratorVersion` identifies the creating version | 0.14.0 | follow-up to #6, #12, #18 |
| Config hash of a file without TOML stored as NULL instead of an empty string; `NormalizeConfigHash` and `ConfigHashesEqual` removed | 0.14.0 | #9 |
| `CliTools[].InputMode` required; silent `File` default removed (`RULE_3_11`) | 0.14.0 | #19 |
| Parallel run guard no longer filters on `MigrationRunModeId` | 0.14.0 | #19 |

### Code

| ID | Item | Location (file/class) | Consequence | Suggested resolution |
|----|------|-----------------------|-------------|----------------------|
| TD-C-01 | `ConfigurationValidationException` derives from `Exception`, not from `ApplicationStartupException`. | `Raycoon.RayMigrator.Shared/Exceptions/CustomExceptions.cs`; catch blocks in `Raycoon.RayMigrator.Console/Program.cs` and `Raycoon.RayMigrator.Pipeline/DirectModePipeline.cs` | Validation failures, an unknown product alias and `TemplateCache` errors before the command runs exit with `100` (documented as unhandled exception) instead of `1`; with `DatabaseLogging` configured the same `TemplateCache` error exits `1` because `InitDatabaseLogger` wraps it in `ApplicationStartupException`. Schedulers cannot tell a configuration error from a crash. | Derive `ConfigurationValidationException` from `ApplicationStartupException` or add a dedicated exit code, then update `Docs/08-cli-reference/global-options.md` |
| TD-C-02 | Unreachable fallbacks `?? 3` and `?? 500` in `GetDalSettings()`; the effective defaults are `100` retries and `250` ms from `[RayRangeInt]` on `RepositoryOptions`. | `Raycoon.RayMigrator.Infrastructure/RepositoryExtensions.cs`; similar `?? 0` and `?? 250` in `Raycoon.RayMigrator.Services/MigrationService.cs` | Readers of the code infer wrong defaults; a future change to validation could silently activate the fallbacks | Remove the fallbacks and make the properties non-nullable after validation, or align them with the annotation defaults |
| TD-C-03 | `MigrateUpAsync`, `MigrateDownAsync` and `BaselineAsync` catch every exception and return a failed `OperationResult` (`ExtractErrorCode` maps `MigrationAlreadyRunningException` to `-2`), so the `catch (MigrationAlreadyRunningException)` block in `RayMigratorService.DoWorkAsync` is dead code. | `Raycoon.RayMigrator.Services/MigrationService.cs`; `Raycoon.RayMigrator.Pipeline/RayMigratorService.cs` | The operator hint that names the `fix` command in that block is never printed from there; the hint printed by `RepositoryMigrationRunInsertWithAutoFix` is the only one | Remove the dead block or move the hint into the result mapping of `DoWorkAsync` |
| TD-C-04 | `CultureDependentSorting` (file name `CultureDependendSorting.cs`, typo) has no callers and contains `Console.ReadKey()`; real ordering uses `StringComparer.OrdinalIgnoreCase` in `MigrationService`. | `Raycoon.RayMigrator.Core/CultureDependendSorting.cs` | Dead code in a NuGet package suggests culture dependent ordering that does not exist | Delete the file |
| TD-C-05 | Console project metadata carries `RayMigrator Pro Database Migration Framework` and `Development`, `Partner` and `Professional Edition` build stages with `DefineConstants`. | `Raycoon.RayMigrator.Console/Raycoon.RayMigrator.Console.csproj` | Assembly metadata and banner text do not match the BUSL positioning (one edition, free production use) | Reduce to the single product name and remove the edition constants |
| TD-C-06 | The release archive ships no `appsettings*.json` (`CopyToPublishDirectory=Never`). | `Raycoon.RayMigrator.Console/Raycoon.RayMigrator.Console.csproj` | First contact with the binary yields exit code `4` (no `Serilog` node) until the user creates files from the Config Wizard or the documentation examples | Ship an `appsettings.example.json` next to the binary or print the wizard URL in the exit `4` message |
| TD-C-07 | The run lock predicate is product, environment and open run; the engine level serialization differs: `pg_advisory_xact_lock` keyed by product only on PostgreSQL, `GET_LOCK` named by product and environment on MariaDB and MySQL. | `Repository_MigrationRun_Insert.sql` in `Raycoon.RayMigrator.Database.PostgreSQL/Templates` and `.MariaDb/Templates`, `.MySql/Templates` | Behavior is correct (the row check runs inside the lock) but runs of different environments of one product serialize on PostgreSQL; the asymmetry is undocumented and easy to break during template edits | Key the PostgreSQL advisory lock by product and environment; add a unit test that compares the lock names across templates |
| TD-C-08 | `MigrationRecoveryException` and `DatabaseTransientException` are declared and documented in `Docs/02-core-concepts/resilience.md` but never thrown; `DatabaseParameterException` is thrown by `DalSqlServer` only. | `Raycoon.RayMigrator.Shared/Exceptions/CustomExceptions.cs`; `Raycoon.RayMigrator.Database.SqlServer/DalSqlServer.cs` | Consumers may catch types that cannot occur; the other four DALs report parameter errors differently | Remove the unused types or throw them consistently in `DalBase` |
| TD-C-09 | `Raycoon.RayMigrator.Database.Example/Templates` contains 22 files including `Repository_MigrationRecordHistory_Archive.sql`, which has no `TemplateType` member; the five shipped DALs carry 21. | `Raycoon.RayMigrator.Database.Example/Templates` | Plugin developers copy a template that `TemplateCache` never loads | Delete the file or add the archive feature to `TemplateType` and all five DALs |

### Database access layer audit

`Docs/todo/dal-best-practices-audit.md` is the master list of the template audit against the engine vendors' best practice
documents. The table consolidates its 25 plan files; contents are not repeated here. Two status columns are shown because they
disagree: the master list marks every item `done` (progress log entries dated 2026-04-17), while 17 plan files still carry
`Status: open`, although the code confirms the master list for the items that were checked (for example unquoted `snake_case`
identifiers on PostgreSQL, MariaDB and MySQL, DAL-017 and DAL-018). Reconciling the plan files is TD-D-14.

| ID | Engine(s) | Topic | Status (plan file) | Status (master list) | Plan file |
|----|-----------|-------|--------------------|----------------------|-----------|
| DAL-001 | SQLite | Enable `PRAGMA foreign_keys` | open | done | [DAL-001](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-001-sqlite-foreign-keys.md) |
| DAL-002 | MariaDB, MySQL | Explicit `ENGINE=InnoDB` on all tables | open | done | [DAL-002](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-002-mysql-mariadb-engine-innodb.md) |
| DAL-003 | MariaDB, MySQL | `TINYINT(1)` to `BOOLEAN` | open | done | [DAL-003](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-003-mysql-mariadb-tinyint-to-boolean.md) |
| DAL-004 | all five | `NOT NULL` on lookup table `Name` columns | done | done | [DAL-004](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-004-lookup-name-not-null.md) |
| DAL-005 | PostgreSQL | Indexes on all foreign key columns | open | done | [DAL-005](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-005-postgres-fk-indexes.md) |
| DAL-006 | PostgreSQL | Explicit `ON DELETE` / `ON UPDATE` on foreign keys | open | done | [DAL-006](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-006-postgres-fk-on-delete-explicit.md) |
| DAL-007 | SQL Server | Explicit `ON DELETE NO ACTION` on foreign keys | open | done | [DAL-007](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-007-sqlserver-fk-on-delete-explicit.md) |
| DAL-008 | SQL Server | Explicit `DATETIME2(n)` precision | open | done | [DAL-008](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-008-sqlserver-datetime2-precision.md) |
| DAL-009 | SQL Server | `ISNULL()` to `COALESCE()` | open | done | [DAL-009](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-009-sqlserver-isnull-to-coalesce.md) |
| DAL-010 | SQL Server | Qualify `sp_addextendedproperty` with `sys.` | done | done | [DAL-010](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-010-sqlserver-sp-addextendedproperty-sys.md) |
| DAL-011 | SQLite | Consistent quoting in the `_rc_state` temp table | open | done | [DAL-011](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-011-sqlite-state-table-quoting.md) |
| DAL-012 | PostgreSQL | `TIMESTAMP` to `TIMESTAMPTZ` on audit columns | open | done | [DAL-012](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-012-postgres-timestamp-to-timestamptz.md) |
| DAL-013 | PostgreSQL | `VARCHAR(n)` to `TEXT` with optional `CHECK` | open | done | [DAL-013](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-013-postgres-varchar-to-text-check.md) |
| DAL-014 | MariaDB, MySQL | `DATETIME` to `TIMESTAMP` for audit columns | done | done | [DAL-014](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-014-mysql-mariadb-datetime-to-timestamp.md) |
| DAL-015 | MariaDB, MySQL | Explicit `CHARSET=utf8mb4` and engine specific `COLLATE` | done | done | [DAL-015](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-015-mysql-mariadb-charset-collate.md) |
| DAL-016 | SQL Server | Unify `VARCHAR` / `NVARCHAR` usage | done | done | [DAL-016](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-016-sqlserver-varchar-nvarchar-consistency.md) |
| DAL-017 | PostgreSQL | Identifier casing to `snake_case` | open | done | [DAL-017](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-017-postgres-identifier-casing.md) |
| DAL-018 | MariaDB, MySQL | Identifier casing to `snake_case` | open | done | [DAL-018](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-018-mysql-mariadb-identifier-casing.md) |
| DAL-019 | MariaDB, MySQL | Policy for intentional template divergence | done | done | [DAL-019](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-019-mysql-vs-mariadb-divergence.md) |
| DAL-020 | SQLite | Drop `AUTOINCREMENT` unless gap free sequences are required | open | done | [DAL-020](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-020-sqlite-autoincrement-review.md) |
| DAL-021 | SQLite | Enforce ISO-8601 datetime format via `CHECK` | done | done | [DAL-021](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-021-sqlite-datetime-format-enforcement.md) |
| DAL-022 | SQLite | Evaluate `STRICT` tables (SQLite 3.37+) | open | done | [DAL-022](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-022-sqlite-strict-tables.md) |
| DAL-023 | SQL Server | Check for remaining legacy `DATETIME` declarations | done | done | [DAL-023](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-023-sqlserver-datetime-to-datetime2-check.md) |
| DAL-024 | SQLite | Clarify transaction boundaries in templates | open | done | [DAL-024](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-024-sqlite-template-transaction-clarity.md) |
| DAL-025 | all (documentation) | Naming conventions per engine in `sql-dialects.md` | open | done | [DAL-025](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-audit/DAL-025-docs-cross-engine-casing-policy.md) |

### Documentation

Paths are relative to `Docs/` unless stated otherwise.

| ID | Item | Pages affected | Suggested resolution |
|----|------|----------------|----------------------|
| TD-D-01 | Template count given as 20; `TemplateType` has 21 members besides `Undefined` and every shipped DAL has 21 files. | `03-database-layer/template-system.md`, `03-database-layer/dal-architecture.md`, `01-architecture/overview.md`, `09-extending/external-dal-development.md`, `01-architecture/patterns.md`, `01-architecture/component-responsibilities.md`, `03-database-layer/adding-new-database.md`, `09-extending/new-database-type.md`, `09-extending/template-customization.md` | Replace the literal with 21 and reference the [Template-driven repository schema](08-Crosscutting-Concepts.md#template-driven-repository-schema) list |
| TD-D-02 | Stale version numbers: package references and `RayMigratorVersion` cited as `0.10.3`; open features header says `v0.10.x`; `Serilog.Settings.Configuration` listed as 10.0.0 while `Directory.Packages.props` pins 10.0.1. | `09-extending/external-dal-development.md`, `appendix/open-features.md`, `06-configuration-reference/logging-options.md` | Reference `Directory.Build.props` and `Directory.Packages.props` instead of literals |
| TD-D-03 | Feature bullets use the pre-0.14 execution mode wording (`simultaneously or successively`, removed as aliases in 0.14.0) and list only three rollback strategies (`RollbackErrorOnly` and `Ignore` missing). | `README.md` (repository root) | Use `FileByFile` / `TargetByTarget` and the five `MigrationErrorAction` values |
| TD-D-04 | Exclusive run described as per product, environment and run mode; since 0.14.0 the predicate is product and environment only. | `02-core-concepts/concurrency-control.md` | Align with [Runtime View 6.7](06-Runtime-View.md#67-concurrency-exclusive-run-lock-and-orphaned-run-recovery-fix) |
| TD-D-05 | Transaction section states "All blocks in file are atomic" and "Consistent state guaranteed"; a transaction spans a block, file level atomicity exists only on the atomic shared connection path. | `02-core-concepts/error-handling.md` | Rewrite the section per [Block-level execution and transactions](08-Crosscutting-Concepts.md#block-level-execution-and-transactions) |
| TD-D-06 | Baseline activity diagram and note say the run records `MigrationOperation = MigrateUp`; `BaselineAsync` stamps `MigrationOperation.Baseline`. | `04-service-layer/activity-diagrams.md` | Correct diagram and note |
| TD-D-07 | The context state diagram uses states (`Initialized`, `ValidatingRepository`, `RepositoryReady`, `DiscoveringFiles`, ...) that exist in no enum or class. | `01-architecture/data-flow.md` | Replace with the `MigrationStatus` / `MigrationRunResult` state machine of [Runtime View 6.3](06-Runtime-View.md#63-migrate-up-the-primary-scenario) |
| TD-D-08 | Entry points named `InfoAsync` and `FixAsync`; the interface has `GetStatusAsync`, `GetHistoryAsync` and `FixIssuesAsync`. | `03-database-layer/template-execution-order.md` | Use the `IMigrationService` member names |
| TD-D-09 | `MigrationRunResult` table lacks `PartialSuccess` (50) and `Recovered` (80); the `IMigrationContextFactory.Create` signature omits the `MigrationCommand command` parameter. | `02-core-concepts/migration-context.md` | Regenerate the enum table and the signature from `Raycoon.RayMigrator.Core` |
| TD-D-10 | MariaDB healthcheck shown as `mariadb-admin ping`; `Testing/Docker/docker-compose.yml` uses `healthcheck.sh --connect --innodb_initialized`. | `10-testing/test-infrastructure.md` | Quote the compose file instead of copying it |
| TD-D-11 | Linux installation extracts the whole archive (binary, `DataAccessLayers/`, license files) into `/usr/local/bin`. | `user-manual/11-operations-guide.md` | Extract into `/opt/raymigrator` and symlink the binary, as the archive layout requires `DataAccessLayers/` next to the executable |
| TD-D-12 | The branching model (`develop` as working branch, fast forward to `main`, tag `v<version>`) exists only as a comment in `.github/workflows/build-test.yml`; `CONTRIBUTING.md` mentions a feature branch but no base branch. | `CONTRIBUTING.md` (repository root) | Add a "Branches and releases" section |
| TD-D-14 | Audit plan files carry `Status: open` for 17 items that the master list and the templates show as done; `open-features.md` states "19 placeholder templates" for the Example DAL (22 files). | `todo/dal-audit/DAL-*.md`, `appendix/open-features.md` | Update the status lines from the master list; correct the template count |
| TD-D-15 | "Arrays are completely replaced, not merged" in the configuration hierarchy page; `JsonOptionsSource` uses plain `AddJsonFile`, so `Microsoft.Extensions.Configuration` merges arrays by index and a shorter array in a later file keeps the surplus elements of an earlier one. | `06-configuration-reference/appsettings-hierarchy.md` | Describe the index based merge and advise complete arrays in every file that overrides one |

### Test coverage

The verification status per quality scenario is kept in
[Quality Requirements 10.3](10-Quality-Requirements.md#103-verification-status). Known gaps that are independent of individual scenarios:

- `Build & Test` runs `Raycoon.RayMigrator.Tests.Unit` on `net10.0` only; `Tests.Unit.Validation`, `Tests.Unit.ConfigWizard.Core`, `Tests.Unit.ConfigWizard.Web` and the engine suite are a local duty before a release (R-10), and `net8.0` and `net9.0` are built but not tested in CI.
- Ten engine tests are hard coded `Assert.Skip("DatabaseLogWriter async queue does not flush within test lifecycle")`: two in each of `DatabaseLogTests`, `SqlServerDatabaseLogTests`, `MariaDbDatabaseLogTests`, `MySqlDatabaseLogTests` and `SqliteDatabaseLogTests` under `Raycoon.RayMigrator.Tests.Engine/Tests/Features`. The database logging path therefore has no end to end assertion on any engine until the queue flush becomes awaitable from tests.
- The atomic shared connection path (`ExecuteSqlBlocksAtomic`, `ExecuteRollbackBlocksAtomic`) has engine tests for SQL Server only (`SqlServerAtomicSharedConnectionTests`).
- All CI runners are `ubuntu-latest`; Windows and macOS behavior (paths, console encodings, `win-x64` and `osx-arm64` binaries) is covered only by local runs.
- Engine tests skip silently when a Docker container is missing (`Assert.SkipUnless(Fixture.IsDatabaseAvailable, ...)`); a local run with a stopped container reports success for that engine.

## 11.4 Open Features and Roadmap Pointers

These are pointers to where open work is recorded, not commitments.

- [Open features](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/open-features.md) lists three items that are not implemented: `--force-restart` / `--skip` options for recovery orchestration (F3, otherwise implemented), an Oracle DAL (F11, to be built as an external plugin on `Raycoon.RayMigrator.Database.Example`), and the managed operating modes `ManagedLocal` / `ManagedRemote` (F12), which live in the separate RayMigrator Studio product; the engine reads only `Standalone`.
- The public [issue tracker](https://github.com/RAYCOON/RayMigrator/issues) is the live list. The issues referenced in `CHANGELOG.md` for 0.13.0 and 0.14.0 (#6, #9, #11 to #19, #21, #22) closed the enum, schema and compatibility cleanup; the tracker held no open issues when this chapter was last revised.
- `CHANGELOG.md` has no `Unreleased` section at present; `RayMigratorVersion` in `Directory.Build.props` names the version being worked towards (see [Architecture Constraints 2.2](02-Architecture-Constraints.md#22-organizational-constraints)).
- The DAL audit master list keeps a dated progress log and is the place where template changes that require a repository recreate are announced before they reach `CHANGELOG.md`.

## Related documentation

- [Quality Requirements](10-Quality-Requirements.md), [Architecture Decisions](09-Architecture-Decisions.md), [Crosscutting Concepts](08-Crosscutting-Concepts.md)
- [Introduction and Goals](01-Introduction-and-Goals.md), [Architecture Constraints](02-Architecture-Constraints.md), [Runtime View](06-Runtime-View.md)
- [README.md](https://github.com/RAYCOON/RayMigrator/blob/main/README.md) maturity notice, [SECURITY.md](https://github.com/RAYCOON/RayMigrator/blob/main/SECURITY.md), [CHANGELOG.md](https://github.com/RAYCOON/RayMigrator/blob/main/CHANGELOG.md)
- [Open features](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/appendix/open-features.md), [DAL best practices audit](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/todo/dal-best-practices-audit.md)
- [Error scenarios and recovery](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/error-scenarios-and-recovery.md), [Resilience](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/02-core-concepts/resilience.md), [SQL dialects](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/03-database-layer/sql-dialects.md), [Engine tests](https://github.com/RAYCOON/RayMigrator/blob/main/Docs/10-testing/engine-tests.md)
- [Directory.Packages.props](https://github.com/RAYCOON/RayMigrator/blob/main/Directory.Packages.props) (CVE pins)
