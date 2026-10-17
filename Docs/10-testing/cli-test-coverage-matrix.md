# CLI Command Test Coverage Matrix

This document provides a comprehensive overview of unit and engine test coverage for every CLI command and option combination in RayMigrator.

**Last updated**: 2026-10-16 (`--cancelled-action`, Ctrl+C and SIGTERM with exit code 130 and the process test of SIGINT, the pending list of `info` with `RollbackIncomplete`); the statistics in section 5 are those of 2026-04-10 (after P1-P4 implementation)  
**Source of truth**: `Core/Configuration/Options/CommandLineConfiguration.cs`

---

## 1. CLI Option Inventory

### Quick Reference Matrix

| Option | migrate-up | migrate-down | validate-hash | update-hash | info | baseline | fix |
|--------|:----------:|:------------:|:-------------:|:-----------:|:----:|:--------:|:---:|
| `--product` `-p` | **R** | **R** | **R** | **R** | **R** | **R** | **R** |
| `--environment` `-env` | **R** | **R** | **R** | **R** | **R** | **R** | **R** |
| `--run-mode` `-rm` | O | O | — | — | — | — | O |
| `--to-release` `-tr` | O | **R** | — | — | — | O | — |
| `--target-group` `-tg` | O | O | O | O | — | O | — |
| `--allow-out-of-order` `-ooo` | O | — | — | — | — | — | — |
| `--stop-rollback-on-missing-rollback-file` `-sromrf` | O | — | — | — | — | — | — |
| `--target-group-migration-order` `-tgmo` | O | — | — | — | — | O | — |
| `--cancelled-action` `-ca` | O | O | — | — | — | O | — |
| `--scope` `-s` | — | — | O | — | — | — | O |
| `--older-than` `-ot` | — | — | — | — | — | — | O |
| `--last-migration-status` `-lms` | — | — | — | — | — | — | O |
| `--startup-info` `-si` | O | O | O | O | O | O | O |
| `--reveal-sensitive-data` `-rsd` | O | O | O | O | O | O | O |
| `--config-dir` `-cd` | O | O | O | O | O | O | O |

**Legend**: **R** = Required, O = Optional, — = Not applicable

### Option Details

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--product` | string | — | Product alias (required for all commands) |
| `--environment` | string | — | Environment name (required for all commands) |
| `--run-mode` | string | `"Migrate"` | `Migrate`, `Simulate`, or `Validate` |
| `--to-release` | string? | null | Target release version |
| `--target-group` | string[] | null | Filter by target group alias(es), multi-value |
| `--allow-out-of-order` | bool | false | Allow files from older releases to execute |
| `--stop-rollback-on-missing-rollback-file` | bool? | null (from config) | Stop rollback chain when rollback file missing |
| `--target-group-migration-order` | string? | null | Comma-separated group aliases for execution order |
| `--cancelled-action` | string | `"terminate"` | What the run does when Ctrl+C or SIGTERM cancels it: `terminate`, `rollback-file`, `rollback-run` or `rollback-release` (case-insensitive); migrate-down and baseline accept the option and ignore it |
| `--scope` | string | varies | validate-hash: `file`/`sqlblocks`/`disabled`; fix: `orphanedruns`/`all` |
| `--older-than` | int | 2 | Minutes since the last heartbeat (or the start of a run without one) after which an open run counts as orphaned; 0 repairs every open run |
| `--run-mode` | string | migrate | `simulate` previews the fix without applying changes |
| `--last-migration-status` | string | none | Status to set on every open record of an orphaned run: `migrated` or `not-migrated`; without it each record keeps its progress |
| `--startup-info` | bool | true | Show startup banner and configuration info |
| `--reveal-sensitive-data` | bool | false | Show connection strings unmasked in output |
| `--config-dir` | string? | null (CWD) | Override configuration directory |

---

## 2. Coverage Matrix

### Legend

| Symbol | Meaning |
|--------|---------|
| COVERED | Dedicated tests exist for this option's behavior |
| PARTIAL | Option is used/set in tests but not the primary test subject |
| **MISSING** | No tests found for this specific option behavior |

### migrate-up

| Option | Unit Tests | Engine Tests | Notes |
|--------|:----------:|:------------:|-------|
| `--product` | PARTIAL | COVERED | Foundational, tested implicitly in every scenario |
| `--environment` | PARTIAL | COVERED | Same as above |
| `--run-mode migrate` | COVERED | COVERED | Default mode, all HappyPath tests |
| `--run-mode simulate` | COVERED | COVERED | `SimulateModeTests` x5 DBs (S1-S3) |
| `--run-mode validate` | COVERED | COVERED | `SimulateModeTests.S4` x5 DBs |
| `--to-release` | COVERED | COVERED | `HappyPathTests` partial migration, `IncrementalTests` |
| `--allow-out-of-order` | COVERED | COVERED | 11 unit tests + `OutOfOrderBlockingTests` O1-O3 x5 DBs (blocking + allowing + data intact) |
| `--target-group` | COVERED | COVERED | `TargetGroupFilterTests` x5 DBs |
| `--target-group-migration-order` | COVERED | COVERED | `TargetGroupMigrationOrderTests` x5 DBs, 20+ unit tests |
| `--stop-rollback-on-missing-rollback-file` | COVERED | COVERED | Config inheritance + `RollbackTests` x5 DBs |
| `--cancelled-action` | COVERED | PARTIAL | `P0_CancelledActionOptionTests` (parsing), `P1_ConsoleCancellationTests` (Ctrl+C and SIGTERM), `P2_CancelledExitCodeTests` (exit code 130); all four actions run in `CancellationTests` x5 DBs through a `RunCancellation`, which the engine tests pass instead of the CLI option; `SqliteCliSignalTests` sends SIGINT to the real CLI process (default `terminate`) and checks exit code 130 and the run `Cancelled` |
| `--startup-info` | **MISSING** | **MISSING** | Hardcoded `false` in EngineTestHost. Cosmetic feature, not testable without console output capture. |
| `--reveal-sensitive-data` | COVERED | COVERED | Unit masking tests + `MigrationRunMetaTests` M4 x5 DBs (masking in settings JSON) |
| `--config-dir` | COVERED | N/A | 17 unit tests. Not testable at engine level (EngineTestHost bypasses config-dir pipeline). |

### migrate-down

| Option | Unit Tests | Engine Tests | Notes |
|--------|:----------:|:------------:|-------|
| `--product` | PARTIAL | COVERED | Implicit in all scenarios |
| `--environment` | PARTIAL | COVERED | Implicit in all scenarios |
| `--run-mode migrate` | COVERED | COVERED | All HappyPath + Error tests |
| `--run-mode simulate` | COVERED | COVERED | `SimulateModeTests.S5` x5 DBs |
| `--run-mode validate` | COVERED | COVERED | `SimulateModeTests` S6 (non-destructive) + S7 (missing rollback detection) x5 DBs |
| `--to-release` (required) | COVERED | COVERED | All tests use this, multiple release targets |
| `--target-group` | COVERED | COVERED | `TargetGroupFilterTests.T4` (`MigrateDown_BackendOnly_PreservesFrontend`) x5 DBs |
| `--cancelled-action` | COVERED | PARTIAL | Accepted (`P0_CancelledActionOptionTests`) and ignored (`CancellationTests`); a cancelled migrate-down stops before the next rollback block and the next one continues, and migrate-up refuses the file until the rollback has completed (`CancellationTests` x5 DBs) |
| `--startup-info` | **MISSING** | **MISSING** | Cosmetic feature, not testable without console output capture. |
| `--reveal-sensitive-data` | COVERED | COVERED | Unit masking + `MigrationRunMetaTests.M6` (MigrateDown settings JSON masked) x5 DBs |
| `--config-dir` | COVERED | N/A | 17 unit tests. Not testable at engine level. |

### validate-hash

| Option | Unit Tests | Engine Tests | Notes |
|--------|:----------:|:------------:|-------|
| `--product` | PARTIAL | COVERED | Implicit |
| `--environment` | PARTIAL | COVERED | Implicit |
| `--scope file` | COVERED | COVERED | `ValidateHashTests` x5 DBs |
| `--scope sqlblocks` | COVERED | COVERED | `ValidateHashTests` x5 DBs |
| `--scope disabled` | COVERED | COVERED | `ValidateHashTests` V7 (ignores modification) + V8 (pass after full migration) x5 DBs |
| `--target-group` | COVERED | PARTIAL | Unit filtering tested, engine tests don't explicitly filter |
| `--startup-info` | **MISSING** | **MISSING** | Cosmetic feature, not testable without console output capture. |
| `--reveal-sensitive-data` | COVERED | **MISSING** | Unit masking covered. ValidateHash is read-only (no MigrationRunMeta written). |
| `--config-dir` | COVERED | N/A | 17 unit tests. Not testable at engine level. |

### update-hash

| Option | Unit Tests | Engine Tests | Notes |
|--------|:----------:|:------------:|-------|
| `--product` | PARTIAL | COVERED | `UpdateHashTests` U1-U5 x5 DBs + embedded in feature tests |
| `--environment` | PARTIAL | COVERED | Implicit in all UpdateHash scenarios |
| `--target-group` | COVERED | COVERED | `TargetGroupFilterTests.T8` (`UpdateHash_BackendOnly`) x5 DBs |
| `--startup-info` | **MISSING** | **MISSING** | Cosmetic feature, not testable without console output capture. |
| `--reveal-sensitive-data` | COVERED | **MISSING** | Unit masking covered. UpdateHash is read-modify (no MigrationRunMeta written). |
| `--config-dir` | COVERED | N/A | 17 unit tests. Not testable at engine level. |

### Info

| Option | Unit Tests | Engine Tests | Notes |
|--------|:----------:|:------------:|-------|
| `--product` | PARTIAL | COVERED | `InfoTests` x5 DBs (status, history and the pending list) |
| `--environment` | PARTIAL | COVERED | Implicit in all Info scenarios |
| `--startup-info` | **MISSING** | **MISSING** | Cosmetic feature, not testable without console output capture. |
| `--reveal-sensitive-data` | PARTIAL | **MISSING** | Unit model tests. Info is read-only (no MigrationRunMeta written). |
| `--config-dir` | COVERED | N/A | 17 unit tests. Not testable at engine level. |
| **Overall** | PARTIAL | COVERED | 23 tests x 5 DBs = 115 tests. ScenarioContext `InfoAsync()` + `GetHistoryAsync()`; the printed pending list in `P2_InfoPendingOutputTests`. |

### Baseline

| Option | Unit Tests | Engine Tests | Notes |
|--------|:----------:|:------------:|-------|
| `--product` | PARTIAL | COVERED | Implicit in all scenarios |
| `--environment` | PARTIAL | COVERED | Implicit in all scenarios |
| `--to-release` | COVERED | COVERED | `BaselineTests` partial + full x5 DBs |
| `--target-group` | COVERED | COVERED | `TargetGroupFilterTests.T5` (`Baseline_BackendOnly_ShouldOnlyBaselineBackend`) x5 DBs |
| `--target-group-migration-order` | COVERED | COVERED | `TargetGroupMigrationOrderTests` includes Baseline |
| `--cancelled-action` | COVERED | PARTIAL | Accepted (`P0_CancelledActionOptionTests`) and ignored (`CancellationTests`); a cancelled baseline keeps the records it wrote (`CancellationTests` x5 DBs) |
| `--startup-info` | **MISSING** | **MISSING** | Cosmetic feature, not testable without console output capture. |
| `--reveal-sensitive-data` | PARTIAL | COVERED | `MigrationRunMetaTests.M7` (Baseline settings JSON masked) x5 DBs |
| `--config-dir` | COVERED | N/A | 17 unit tests. Not testable at engine level. |

### Fix

| Option | Unit Tests | Engine Tests | Notes |
|--------|:----------:|:------------:|-------|
| `--product` | PARTIAL | COVERED | `FixTests` x5 DBs (F1-F8) |
| `--environment` | PARTIAL | COVERED | Implicit in all Fix scenarios |
| `--scope orphanedruns` | COVERED | COVERED | F1-F8 all use OrphanedRuns scope |
| `--scope all` | COVERED | **MISSING** | Enum test only. Engine only tests OrphanedRuns scope. |
| `--older-than` | COVERED | COVERED | F4: threshold filtering |
| `--run-mode simulate` | COVERED | COVERED | F3: simulate + orphan persistence verification |
| `--last-migration-status` | COVERED | COVERED | F8: AssumedMigrationStatus=Migrated; `OrphanRepairTests` x5 DBs: without the option the repair keeps the progress of each open record, `not-migrated` sets it with the counter kept; `P0_LastMigrationStatusOptionTests`: no default, unknown value is a parse error |
| `--startup-info` | **MISSING** | **MISSING** | Cosmetic feature, not testable without console output capture. |
| `--reveal-sensitive-data` | PARTIAL | **MISSING** | Unit model test only. Fix does not write MigrationRunMeta settings JSON. |
| `--config-dir` | COVERED | N/A | 17 unit tests. Not testable at engine level. |
| **Overall** | COVERED (models) | COVERED | 8 tests x 5 DBs = 40 tests. ScenarioContext `FixIssuesAsync()` + `InsertOrphanedMigrationRun()`. |

---

## 3. Per-Command Test File Mapping

### 3.1 migrate-up

**Engine test files** (51 files, ~211 tests, all 5 DB engines):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| HappyPath | `HappyPathTests.cs` | SqlServer, MariaDb, MySql, Sqlite | All 4 releases migrate; partial with `--to-release`; multiple runs |
| Rollback (Full) | `RollbackTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Inject error → full rollback; chain execution; all tables dropped; a required rollback file that disappeared during the run stops the chain and keeps its file `Migrated` |
| Rollback (ErrorOnly) | `RollbackErrorOnlyTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Fail-fast without rollback; partial state on error |
| Rollback (Release) | `RollbackReleaseTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Roll back only latest release; preserve earlier |
| Terminate | `TerminateTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Stop execution, record failures |
| Ignore | `IgnoreTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Continue despite errors |
| RunAlways | `RunAlwaysTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Re-execute on each run without `--allow-out-of-order`; a third run after a failed re-run is still in order; the file runs in full from block 1 after an attempt that committed blocks: a failure (FileByFile and TargetByTarget), a cancel, a record on the atomic shared connection that is neither resumed nor finalized, and the open record of an orphaned run after its repair (`Infrastructure/RunAlwaysContract.cs`) |
| FlatLayout | `FlatLayoutTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Files at release root (no /Backend) |
| Incremental | `IncrementalTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Multiple migration runs, partial state progression |
| MultiTarget | `MultiTargetTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Multiple databases per target group |
| BlockLevel | `SqlServerBlockLevelTests.cs` | SqlServer only | GO statement splitting, multiple batches |

**Unit test files** (relevant to migrate-up):
- `P0_MigrationRunModeExtensionsTests.cs` — ShouldExecuteSql, ShouldWriteRepository, ShouldReadRepository (12 tests)
- `P1_OutOfOrderDetectionTests.cs` — DetectOutOfOrderFiles logic (11 tests)
- `P1_HandleMigrationErrorBehaviorTests.cs` — Error strategy dispatch
- `P1_TargetMigrationOrderExecutionTests.cs` — FileByFile vs TargetByTarget (20 tests)
- `P1_RequireRollbackFileValidationTests.cs` — Rollback file requirement validation
- `P1_FilterByTargetReleaseTests.cs` — `--to-release` filtering logic
- `P1_FilterByTargetGroupTests.cs` — `--target-group` filtering logic
- `P1_TargetGroupMigrationOrderTests.cs` — `--target-group-migration-order` parsing + validation (20+ tests)
- `P1_RollbackErrorActionTests.cs` — Rollback error handling
- `P1_MigrationErrorActionIgnoreTests.cs` — Ignore strategy
- `P1_MigrationErrorActionInheritanceTests.cs` — Error action cascading
- `P0_CancelledActionOptionTests.cs` — `--cancelled-action` parsing on the three run commands
- `P1_ConsoleCancellationTests.cs` — the first Ctrl+C or SIGTERM cancels with the action of the option, a second one ends the process
- `P2_CancelledExitCodeTests.cs` — exit code 130 for a cancelled command, 1 for a cancelled migrate-up whose rollback did not complete, for a migrate-up that ended as `Error` after the signal (a failure whose error recovery the cancel stopped) and for a `Cancelled` code without the signal
- `P1_IncompleteRollbackTests.cs` — migrate-up refuses a file whose rollback stopped in the middle; the message names release, file, target, the rollback blocks and the block with which migrate-down continues

### 3.2 migrate-down

**Engine test files** (15 files, ~50 tests):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| HappyPath | `HappyPathTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Down to R2 (R3+R4 rolled back); Down to R1; Down all |
| EdgeCases | `EdgeCaseTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Multiple rollback rounds; cascading state |
| Errors | `ErrorTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Missing rollback files (a required one stops the chain, keeps its file `Migrated`, and the next migrate-down rolls it back once the file is there); broken rollback SQL |
| Stopped files | `PostgreSqlStartedFileRollbackTests.cs` | SqlServer, MariaDb, MySql, Sqlite | A file that stopped after committed blocks (cancel, failure, ignored failed blocks, orphan repair) is rolled back from its first rollback block before the files it came after, and the next migrate-up runs it from block 1; a file without a committed block is named in a warning; a failed rollback of such a file stops in the middle; a missing rollback file keeps its committed blocks; Simulate lists its rollback (`Infrastructure/StartedFileRollbackContract.cs`) |

**Unit test files**: Same filtering/mode tests as migrate-up (shared logic), and `P1_MigrateDownSelectionTests.cs` — the records migrate-down rolls back, those it names in a warning, and the message of a missing required rollback file per kind of record.

### 3.3 validate-hash

**Engine test files** (5 files, 40 tests):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| ValidateHash | `ValidateHashTests.cs` | SqlServer, MariaDb, MySql, Sqlite | SqlBlocks scope; File scope; Disabled scope; after modification; after baseline; new/missing/modified detection |

**Unit test files**:
- `P1_ResolveHashValidationScopeTests.cs` — Scope resolution from config
- `P1_Sha256Tests.cs` — Hash calculation

### 3.4 update-hash

**Engine test files** (5 files, 25 tests):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| UpdateHash | `UpdateHashTests.cs` | SqlServer, MariaDb, MySql, Sqlite | U1: no updates after fresh migration; U2: update after file modification; U3: update then validate passes; U4: idempotent second run; U5: empty repo no-op |

Also tested as part of:
- `TargetGroupFilterTests.T8` (UpdateHash with target-group filter)
- `RunningGuardTests.G5` (UpdateHash with running migration run)
- `CancellationTests` (an update-hash cancelled between the two records of a file counts that file, x5 DBs)

**Unit test files**: Covered by shared filtering tests.

### 3.5 Info

**Engine test files** (5 files, 115 tests):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| Info | `InfoTests.cs` | SqlServer, MariaDb, MySql, Sqlite | I1: fresh repo zero counts; I2: full migration; I3: partial migration; I4: baseline; I5: target group status; I6: failed migration error state; I7: multiple runs history; I8: run details; the pending list (`PendingFiles`) with every `PendingMigrationReason` in the order of the next migrate-up, the pending count and `LastMigrationDate` of every target group; the open record of a run that ended abruptly as the orphan repair leaves it; a RunAlways file with committed blocks of a cancelled attempt as `RunAlways`; a file that a CLI tool runs, with committed blocks or every block of an earlier attempt, as `Failed`, which the next migrate-up runs in full; `RollbackIncomplete` with its rollback blocks after a migrate-down cancelled in the middle (`CancellationTests` x5 DBs) |

**Unit test files**:
- `P3_BaselineAndInfoModelTests.cs` — `MigrationStatusModelTests`, `MigrationHistoryModelTests`, `PendingMigrationModelTests` (model/property tests)
- `P1_PendingMigrationClassificationTests.cs` — the reason of a pending file and target, as the next migrate-up treats its record
- `P1_MigrationRecordMappingTests.cs` — `StartedAt` and `FinishedAt` of the record and history selects, read as UTC
- `P2_InfoPendingOutputTests.cs` — the pending list under its count, cut after 50 entries, a `RollbackIncomplete` entry with its rollback blocks and `migrate-down`, and `Pending=` and `LastMigration=` on every target group line
- `P0_ConfigDirTests.cs` — CLI parsing for Info command (2 tests)
- `MigrationCommandExhaustivenessTests.cs` — Exhaustiveness check includes `MigrationCommand.Info`

### 3.6 Baseline

**Engine test files** (5 files, 50 tests):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| Baseline | `BaselineTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Partial baseline (to R2); full baseline; no user tables created; idempotency; baseline + MigrateUp composition; baseline + MigrateDown = fail |

**Unit test files**:
- `P3_BaselineAndInfoModelTests.cs` — Baseline request/result model tests
- Shared filtering tests apply

### 3.7 Fix

**Engine test files** (5 files, 40 tests):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| Fix | `FixTests.cs` | SqlServer, MariaDb, MySql, Sqlite | F1: no orphans; F2: fix orphan; F3: dry run; F4: older-than filter; F5: fix then migrate; F6: multiple orphans; F7: orphan details; F8: assumed status migrated |

Also tested as part of `CancellationTests` (a fix cancelled between two orphaned runs keeps the run it closed and leaves the other open, x5 DBs) and of `OrphanRepairTests` (the repair of the open records of an orphaned run by their progress, by `fix` and by the auto-fix of the next migrate-up, and the run row closed as `Error` with the engine code `1006` and a message that names its last heartbeat, x5 DBs).

**Unit test files**:
- `P2_FixCommandTests.cs`:
  - `FixIssuesRequestModelTests` — Default values, property setting
  - `FixIssuesResultModelTests` — Result model, simulate distinction
  - `OrphanedRunInfoModelTests` — Data structure tests
  - `FixIssuesEnumTests` — Enum value verification (OrphanedRuns=2, All=1, Undefined=0)
  - `FixCommandSqlTemplateStructureTests` — the fix templates of every engine; `Repository_MigrationRecord_FixOrphaned` decides per record by its progress for status 0 and sets `FinishedAt` where it is empty; `Repository_MigrationRun_FixOrphaned` writes `ErrorCode` `1006` and the message of the repair where the row has none
  - `FixAssumedStatusPassThroughTests` — the CLI wrapper hands `FixAssumedMigrationStatus` to the engine unchanged, null included
  - `FixCommandConsoleOptionsTests` — FixOlderThanMinutes, FixAssumedMigrationStatus defaults
- `P0_LastMigrationStatusOptionTests.cs` — `--last-migration-status` without a default, both values, unknown values rejected
- `P1_OrphanRepairProjectionTests.cs` — the status of the open records of an open run as the orphan repair leaves them (`info`)
- `P0_ConfigDirTests.cs` — CLI parsing for Fix command
- `MigrationCommandExhaustivenessTests.cs` — Exhaustiveness check includes `MigrationCommand.FixIssues`

### 3.8 Cross-Cutting Feature Tests (Engine)

| Feature | Base + DB Files | Key Scenarios |
|---------|----------------|---------------|
| SimulateMode | 5 (x5 DBs) | S1-S3: MigrateUp Simulate; S4: Validate; S5: MigrateDown Simulate; S6-S7: MigrateDown Validate |
| TargetGroupFilter | 5 (x5 DBs) | Backend-only, Frontend-only, multi-group with allowOutOfOrder |
| TargetGroupMigrationOrder | 5 (x5 DBs) | Custom order, default order, Baseline + MigrateUp, an order of the only TargetGroup ignored with a warning |
| RunningGuard | 5 (x5 DBs) | Concurrent execution detection for MigrateUp, MigrateDown, UpdateHash, MigrateUp also after two finished runs; starts at the same moment take the run lock once (G8, 8 starts in each of 10 rounds) |
| MigrationHistoryTracking | 5 (x5 DBs) | MigrationRecordHistory entries on terminal status transitions (source file: `ArchiveRetentionTests.cs`) |
| DatabaseLog | 5 (x5 DBs) | Migration logging to database; the log indexes on `MigrationRunId` and `CreatedAt`, also added to the log table of an older engine by the next initialization; parallel first starts all initialize the log and insert the event catalogue once |
| Cancellation | 5 (x5 DBs) | migrate-up with all four actions at a block, file or target boundary, the atomic path, a cancel inside a sleeping block on the atomic path and on a target of its own (not SQLite), before the run starts, in Simulate and Validate, while a CLI tool runs; migrate-down, baseline and a running error recovery, whose run stays `Error` with the code of the failure; the refusal of a migrate-up after a rollback cancelled in the middle; a rollback action that stops at a missing required rollback file and keeps that file `Migrated`; update-hash and fix; schema version 2 with data upgraded to 3 (see 3.10) |
| BlockProgress | 5 (x5 DBs) | The block counters of a record while a run goes on: 0 while a CLI tool runs, the leading blocks before an ignored failure on the way up (also at the end of the file and after a cancel, so the next migrate-up resumes at the failed block) and in a rollback, a resumed file and a continued rollback from their start; a cancel after an ignored failed rollback block keeps the leading rollback blocks, and the next migrate-down runs the failed block again; a failed atomic rollback keeps the rollback blocks of the attempt it continued, and migrate-up refuses the file; a failed rollback through a CLI tool ends `Failed`; a file that a CLI tool runs does not resume an earlier attempt, in both `TargetMigrationOrder`s, and a rollback through a CLI tool does not continue one |
| OrphanRepair | 5 (x5 DBs) | The orphan repair of a run that ended abruptly, by the auto-fix of the next migrate-up and by `fix`: `Failed` with the committed blocks and a resume after them, `Migrated` with every block, `NotMigrated` without progress, an explicit status with the counter kept, `FinishedAt` without `DurationInMs` for a file that was running and the end kept for a rollback that was running, `LastMigrationDate` and the current release after a repair to `Migrated`, a rollback that was running with and without rollback progress and with every rollback block, a rollback left `Executing` in a run that has ended, the rule of the template against the projection of the status for every state, PostgreSQL sessions outside UTC; the run row `Error` with the engine code `1006` and a message that names its last heartbeat |
| CliSignal | 1 (SQLite) | The real CLI process gets SIGINT during a migrate-up: exit code 130 and the run `Cancelled` (Linux and macOS, see 3.10) |
| MigrationRunMeta | 5 (x5 DBs) | Execution metadata, settings JSON, RevealSensitiveData masking (MigrateUp M4, MigrateDown M6, Baseline M7) |
| OutOfOrderBlocking | 5 (x5 DBs) | O1: blocking when false; O2: allowing when true; O3: data integrity after blocking |
| UpdateHash | 5 (x5 DBs) | U1-U5: fresh migration, file modification, validate after update, idempotent, empty repo |
| MigSettingsInheritance | 5 (x5 DBs) | .migsettings file loading, config precedence |
| RepositoryIntegrity | 5 (x5 DBs) | Schema validation, table structure |
| AtomicSharedConnection | 1 (SqlServer) | Atomic transaction handling |
| CliTool | 12 (4 DBs) | File mode, Stdin mode, Docker exec mode |
| Compound/RoundTrip | 5 (x5 DBs) | Up → Down → Up |
| Compound/Recovery | 5 (x5 DBs) | Error(Rollback) → Up |

### 3.9 Global Option Tests (Unit)

| Test File | Options Tested | Test Count |
|-----------|---------------|------------|
| `P0_ConfigDirTests.cs` | `--config-dir` for all 7 commands | 17 |
| `P1_SensitiveDataMaskerTests.cs` | `--reveal-sensitive-data` masking logic | 20+ |
| `P2_ToDetailStringMaskingTests.cs` | Config detail string masking | Multiple |
| `P1_BuildMigrationRunSettingsJsonTests.cs` | ShowStartupInfo, RevealSensitiveData, AllowOutOfOrder in settings JSON | Multiple |
| `P1_RayMigratorOptionsValidatorTests.cs` | Global options validation | Multiple |
| `P1_RayAttributeTests.cs` | Custom validation attributes (RayEnum, RayRangeInt, RayConnectionString, etc.) | 30+ |

### 3.10 Cancellation and Exit Code 130

| Test File | Behaviour Tested |
|-----------|------------------|
| `P0_CancelledActionOptionTests.cs` | `--cancelled-action` (`-ca`) only on migrate-up, migrate-down and baseline; every value, case-insensitive and through `{ENV:VAR}`; `terminate` by default; an unknown value is a parse error |
| `P1_ConsoleCancellationTests.cs` | `ConsoleCancellation`: the first Ctrl+C or SIGTERM keeps the process alive and cancels with the action of the option, a second one keeps the default and ends the process; the cancel runs on the thread pool, so a slow cancel callback of a provider holds back neither the handler nor a second signal |
| `P2_CancelledExitCodeTests.cs` | `RayMigratorService.DoWorkAsync` exits with 130 and logs a warning for a cancelled run and, while the token of the process is cancelled, for update-hash and fix with `Cancelled` (1004) and for validate-hash and info with an `OperationCanceledException`; it exits with 1 for a cancelled migrate-up whose rollback did not complete, for a migrate-up with the result `Error` while the token of the process is cancelled (a failure whose error recovery the cancel stopped, or 1005), and for a 1004 or an `OperationCanceledException` that the cancel of the process did not cause |
| `SqliteCliSignalTests.cs` (engine) | The signal registration of the real `raymigrator` process: SIGINT when the console reports the second file of a SQLite migrate-up, exit code 130, the run row `Cancelled` (only the handler of the CLI records it; a process that the signal killed would leave the run open), the console warning of the cancel; skipped on Windows |

---

## 4. Gap Analysis

### ~~Priority 1: Entire commands without engine tests~~ (RESOLVED)

| Gap | Status | Details |
|-----|--------|---------|
| ~~**Info command**~~ | **RESOLVED** | 40 engine tests added (8 tests x 5 DBs). ScenarioContext now has `InfoAsync()` + `GetHistoryAsync()`. Covers: fresh repo, full/partial migration, baseline, target groups, error state, history. |
| ~~**Fix command**~~ | **RESOLVED** | 40 engine tests added (8 tests x 5 DBs). ScenarioContext now has `FixIssuesAsync()` + `InsertOrphanedMigrationRun()`. Covers: no orphans, fix orphan, simulate, older-than filter, fix-then-migrate, multiple orphans, details, assumed status. |

### Priority 2: Cosmetic / Not testable at engine level

| Gap | Impact | Status |
|-----|--------|--------|
| **`--startup-info` (all 7 commands)** | LOW | **WONTFIX** — Cosmetic console output. EngineTestHost hardcodes `false`. Would require console output capture infrastructure. |
| **`--config-dir` (all 7 commands)** | LOW | **WONTFIX** — Not testable at engine level. EngineTestHost bypasses the config-dir pipeline entirely. 17 unit tests in `P0_ConfigDirTests` prove parsing works. |
| **`--reveal-sensitive-data` for read-only commands** | LOW | **N/A** — ValidateHash, UpdateHash, Info, Fix do not write MigrationRunMeta settings JSON. Nothing to mask at engine level. |
| **Fix `--scope all`** | LOW | **MISSING** — Only `OrphanedRuns` scope tested. `All` scope shares the same code path with additional checks. |

### ~~Priority 3: Cross-command engine gaps~~ (RESOLVED)

| Gap | Status | Details |
|-----|--------|---------|
| ~~`--allow-out-of-order` blocking~~ | **RESOLVED** | `OutOfOrderBlockingTests` O1-O3 x5 DBs. Blocking (false) + allowing (true) + data integrity. |
| ~~MigrateDown + `--run-mode validate`~~ | **RESOLVED** | `SimulateModeTests` S6-S7 x5 DBs. Non-destructive validation + missing rollback detection. |
| ~~MigrateDown + `--target-group`~~ | **ALREADY COVERED** | Existing T4 (`MigrateDown_BackendOnly_PreservesFrontend`) x5 DBs. |
| ~~validate-hash + `--scope disabled`~~ | **RESOLVED** | `ValidateHashTests` V7-V8 x5 DBs. Ignores modification + pass after full migration. |
| ~~`--config-dir` engine integration~~ | **NOT FEASIBLE** | EngineTestHost bypasses config-dir pipeline. 17 unit tests sufficient. |

### ~~Priority 4: Minor gaps~~ (RESOLVED)

| Gap | Status | Details |
|-----|--------|---------|
| ~~`--reveal-sensitive-data` for non-MigrateUp~~ | **RESOLVED** | M6 (MigrateDown) + M7 (Baseline) masking tests added × 5 DBs. SensitiveDataMasker.BeginScope() per-command in ScenarioContext. |
| ~~Baseline + `--target-group`~~ | **ALREADY COVERED** | Existing T5 (`Baseline_BackendOnly_ShouldOnlyBaselineBackend`) covers this × 5 DBs. |
| ~~update-hash + `--target-group`~~ | **ALREADY COVERED** | Existing T8 (`UpdateHash_BackendOnly_AfterFullMigration_ShouldSucceed`) covers this × 5 DBs. |
| ~~update-hash dedicated tests~~ | **RESOLVED** | New `UpdateHashTests.cs` + 4 DB wrappers. U1-U5: fresh migration, file modification, validate after update, idempotent, empty repo. 25 tests. |

---

## 5. Statistics

| Metric | Count |
|--------|-------|
| Total CLI commands | 7 |
| Total unique options (global + shared + command-specific) | 21 |
| Total Command x Option combinations | 51 |
| Combinations with unit test coverage (COVERED or PARTIAL) | 44 (86.3%) |
| Combinations with engine test coverage (COVERED, PARTIAL, or N/A) | 44 (86.3%) |
| Combinations with **MISSING** engine coverage | 3 (5.9%) — all `--startup-info` related |
| Combinations marked N/A (not testable at engine level) | 7 — `--config-dir` x7 commands |
| Remaining actionable gap | 1 — Fix `--scope all` |
| Commands with zero engine tests | 0 |
| Engine test files total | ~171 |
| Engine test methods total | ~888 |
| New engine tests added (P1-P4) | 150 |
| Unit test files (CLI-relevant) | ~30 |
| Database engines tested | 5 (SqlServer, PostgreSQL, MariaDB, MySQL, SQLite) |

### Coverage by Command

| Command | Options | Unit Covered | Engine Covered | Engine N/A | Engine MISSING | Unit % | Engine % |
|---------|---------|-------------|---------------|-----------|---------------|--------|----------|
| migrate-up | 13 | 12 | 11 | 1 | 1 | 92% | 92% |
| migrate-down | 8 | 7 | 7 | 1 | 1 | 88% | 88% |
| validate-hash | 7 | 6 | 5 | 1 | 1 | 86% | 86% |
| update-hash | 5 | 4 | 3 | 1 | 1 | 80% | 80% |
| Info | 4 | 2 | 2 | 1 | 1 | 50% | 75% |
| Baseline | 7 | 6 | 6 | 1 | 1 | 86% | 86% |
| Fix | 8 | 7 | 5 | 1 | 2 | 88% | 75% |

---

## 6. Engine Test Infrastructure

### ScenarioContext API

The engine test harness (`ScenarioContext.cs`) exposes these methods:

| Method | Supported | Parameters |
|--------|-----------|------------|
| `MigrateUpAsync()` | Yes | toRelease, allowOutOfOrder, targetGroupAliases, runMode, targetGroupMigrationOrder, cancellation (`RunCancellation`) |
| `MigrateUpWithTokenAsync()` | Yes | cancellationToken, toRelease (a cancel of the token alone means `Terminate`) |
| `MigrateDownAsync()` | Yes | toRelease, targetGroupAliases, runMode, revealSensitiveData, cancellation |
| `BaselineAsync()` | Yes | toRelease, targetGroupAliases, targetGroupMigrationOrder, revealSensitiveData, cancellation |
| `ValidateHashAsync()` | Yes | scope, targetGroupAliases |
| `UpdateHashAsync()` | Yes | targetGroupAliases, cancellationToken |
| `InfoAsync()` | Yes | — (returns MigrationStatusInfo) |
| `GetHistoryAsync()` | Yes | limit |
| `FixIssuesAsync()` | Yes | scope, olderThanMinutes, assumedMigrationStatus, cancellationToken (run mode from the host) |
| `InsertOrphanedMigrationRun()` | Yes | minutesOld (helper for Fix tests) |
| `UseProgressObserver()` | Yes | observer (the cancellation tests cancel from a `CancellingProgressObserver`) |
| `UseLogObserver()` | Yes | observer (the cancellation tests of update-hash and fix cancel at a log line) |

### ScenarioBuilder Capabilities

```
// File mutations
.InjectError(release, filename)
.InjectErrorAtBlock(release, filename, blockIndex)   // SqlServer multi-GO files
.RemoveRollback(release, filename)
.BreakRollback(release, filename)
.SetFileToml(release, filename, key, value)
.SetMigSettings(relativeFilePath, entries)
.WithFlatLayoutForRelease(release)

// Error handling
.WithMigrationErrorAction(Rollback|RollbackErrorOnly|RollbackRelease|Terminate|Ignore)
.WithRollbackErrorAction(Terminate|Ignore)
.WithRequireRollbackFile(bool)
.WithStopRollbackOnMissingRollbackFile(bool)

// Execution order
.WithTargetMigrationOrder(FileByFile|TargetByTarget)
.WithTargetGroupMigrationOrder(csv)

// Topology
.WithMultiTarget(secondConnectionString)
.WithTargetGroup(alias, dbType, connStr, order?, hashScope?)

// CLI tools
.WithCliTool(alias, execPath, argTemplate, inputMode, timeoutInSeconds?, successExitCodes?)
.WithUseCliToolAlias(alias)
.WithCliToolParameters(Dictionary)

// Repository and log
.WithTableBaseName(prefix)                            // the scenario drops the tables of the prefix when it ends
.WithDatabaseLoggingInto(connectionString, minLevel)  // a log database of its own

// Logging & retries
.WithDatabaseLogging(minLevel)
.WithTargetMaxRetries(maxRetries, delayMs)
.WithTargetCommandTimeout(seconds)
```

### EngineTestHost Defaults

```csharp
ShowStartupInfo = false,        // Never tested as true
RevealSensitiveData = false,    // Default, tested via MigrationRunMetaTests
ConfigDir = null                // Always CWD
```

### Engine tests added (2026-04-10, P1-P4):

**P1 — Info/Fix commands** (80 tests):
- `InfoAsync()`, `GetHistoryAsync()`, `FixIssuesAsync()`, `InsertOrphanedMigrationRun()` added to ScenarioContext
- 10 new test files (InfoTests + FixTests, base + 4 DB wrappers each)

**P3 — Cross-command gaps** (35 tests):
- `OutOfOrderBlockingTests` (O1-O3): out-of-order blocking/allowing behavior
- `SimulateModeTests` (S6-S7): MigrateDown Validate mode
- `ValidateHashTests` (V7-V8): Disabled scope
- `SqlDialect`: `GetCreateSimpleTableSql`/`GetDropSimpleTableSql` helpers

**P4 — Minor gaps** (35 tests):
- `UpdateHashTests` (U1-U5): dedicated UpdateHash scenarios
- `MigrationRunMetaTests` (M6-M7): MigrateDown/Baseline masking
- `SensitiveDataMasker.BeginScope()` per-command in ScenarioContext (thread-safe for parallel engine tests)
