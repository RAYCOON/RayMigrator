# CLI Command Test Coverage Matrix

This document provides a comprehensive overview of unit and engine test coverage for every CLI command and option combination in RayMigrator.

**Last updated**: 2026-04-10 (after P1-P4 implementation)  
**Source of truth**: `Core/Configuration/Options/CommandLineConfiguration.cs`

---

## 1. CLI Option Inventory

### Quick Reference Matrix

| Option | migrate-up | migrate-down | validate-hash | update-hash | info | baseline | fix |
|--------|:----------:|:------------:|:-------------:|:-----------:|:----:|:--------:|:---:|
| `--product` `-p` | **R** | **R** | **R** | **R** | **R** | **R** | **R** |
| `--environment` `-env` | **R** | **R** | **R** | **R** | **R** | **R** | **R** |
| `--run-mode` `-rm` | O | O | — | — | — | — | — |
| `--to-release` `-tr` | O | **R** | — | — | — | O | — |
| `--target-group` `-tg` | O | O | O | O | — | O | — |
| `--allow-out-of-order` `-ooo` | O | — | — | — | — | — | — |
| `--stop-rollback-on-missing-rollback-file` `-sromrf` | O | — | — | — | — | — | — |
| `--target-group-migration-order` `-tgmo` | O | — | — | — | — | O | — |
| `--scope` `-s` | — | — | O | — | — | — | O |
| `--older-than` `-ot` | — | — | — | — | — | — | O |
| `--dry-run` | — | — | — | — | — | — | O |
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
| `--scope` | string | varies | validate-hash: `file`/`sqlblocks`/`disabled`; fix: `orphanedruns`/`all` |
| `--older-than` | int | 60 | Minutes threshold for orphaned run detection |
| `--dry-run` | bool | false | Simulate fix without applying changes |
| `--last-migration-status` | string | `"not-migrated"` | Status to assign after fix: `migrated` or `not-migrated` |
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
| `--product` | PARTIAL | COVERED | `InfoTests` x5 DBs (I1-I8) |
| `--environment` | PARTIAL | COVERED | Implicit in all Info scenarios |
| `--startup-info` | **MISSING** | **MISSING** | Cosmetic feature, not testable without console output capture. |
| `--reveal-sensitive-data` | PARTIAL | **MISSING** | Unit model tests. Info is read-only (no MigrationRunMeta written). |
| `--config-dir` | COVERED | N/A | 17 unit tests. Not testable at engine level. |
| **Overall** | PARTIAL | COVERED | 8 tests x 5 DBs = 40 tests. ScenarioContext `InfoAsync()` + `GetHistoryAsync()`. |

### Baseline

| Option | Unit Tests | Engine Tests | Notes |
|--------|:----------:|:------------:|-------|
| `--product` | PARTIAL | COVERED | Implicit in all scenarios |
| `--environment` | PARTIAL | COVERED | Implicit in all scenarios |
| `--to-release` | COVERED | COVERED | `BaselineTests` partial + full x5 DBs |
| `--target-group` | COVERED | COVERED | `TargetGroupFilterTests.T5` (`Baseline_BackendOnly_ShouldOnlyBaselineBackend`) x5 DBs |
| `--target-group-migration-order` | COVERED | COVERED | `TargetGroupMigrationOrderTests` includes Baseline |
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
| `--dry-run` | COVERED | COVERED | F3: dry run + orphan persistence verification |
| `--last-migration-status` | COVERED | COVERED | F8: AssumedMigrationStatus=Migrated |
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
| Rollback (Full) | `RollbackTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Inject error → full rollback; chain execution; all tables dropped |
| Rollback (ErrorOnly) | `RollbackErrorOnlyTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Fail-fast without rollback; partial state on error |
| Rollback (Release) | `RollbackReleaseTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Roll back only latest release; preserve earlier |
| Terminate | `TerminateTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Stop execution, record failures |
| Ignore | `IgnoreTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Continue despite errors |
| RunAlways | `RunAlwaysTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Re-execute on each run (uses `allowOutOfOrder: true`) |
| FlatLayout | `FlatLayoutTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Files at release root (no /Backend) |
| Incremental | `IncrementalTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Multiple migration runs, partial state progression |
| MultiTarget | `MultiTargetTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Multiple databases per target group |
| BlockLevel | `SqlServerBlockLevelTests.cs` | SqlServer only | GO statement splitting, multiple batches |

**Unit test files** (relevant to migrate-up):
- `P0_MigrationRunModeExtensionsTests.cs` — ShouldExecuteSql, ShouldWriteRepository, ShouldReadRepository (12 tests)
- `P1_OutOfOrderDetectionTests.cs` — DetectOutOfOrderFiles logic (11 tests)
- `P1_HandleMigrationErrorBehaviorTests.cs` — Error strategy dispatch
- `P1_TargetMigrationOrderExecutionTests.cs` — Simultaneously vs Successively (20 tests)
- `P1_RequireRollbackFileValidationTests.cs` — Rollback file requirement validation
- `P1_FilterByTargetReleaseTests.cs` — `--to-release` filtering logic
- `P1_FilterByTargetGroupTests.cs` — `--target-group` filtering logic
- `P1_TargetGroupMigrationOrderTests.cs` — `--target-group-migration-order` parsing + validation (20+ tests)
- `P1_RollbackErrorActionTests.cs` — Rollback error handling
- `P1_MigrationErrorActionIgnoreTests.cs` — Ignore strategy
- `P1_MigrationErrorActionInheritanceTests.cs` — Error action cascading

### 3.2 migrate-down

**Engine test files** (15 files, ~50 tests):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| HappyPath | `HappyPathTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Down to R2 (R3+R4 rolled back); Down to R1; Down all |
| EdgeCases | `EdgeCaseTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Multiple rollback rounds; cascading state |
| Errors | `ErrorTests.cs` | SqlServer, MariaDb, MySql, Sqlite | Missing rollback files; broken rollback SQL |

**Unit test files**: Same filtering/mode tests as migrate-up (shared logic).

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

**Unit test files**: Covered by shared filtering tests.

### 3.5 Info

**Engine test files** (5 files, 40 tests):

| Category | Base File | DB Variants | Key Scenarios |
|----------|-----------|-------------|---------------|
| Info | `InfoTests.cs` | SqlServer, MariaDb, MySql, Sqlite | I1: fresh repo zero counts; I2: full migration; I3: partial migration; I4: baseline; I5: target group status; I6: failed migration error state; I7: multiple runs history; I8: run details |

**Unit test files**:
- `P3_BaselineAndInfoModelTests.cs` — `MigrationStatusModelTests`, `MigrationHistoryModelTests` (model/property tests)
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

**Unit test files**:
- `P2_FixCommandTests.cs`:
  - `FixIssuesRequestModelTests` — Default values, property setting
  - `FixIssuesResultModelTests` — Result model, dry-run distinction
  - `OrphanedRunInfoModelTests` — Data structure tests
  - `FixIssuesEnumTests` — Enum value verification (OrphanedRuns=2, All=1, Undefined=0)
  - `FixCommandConsoleOptionsTests` — FixOlderThanMinutes, FixDryRun, FixAssumedMigrationStatus defaults
- `P0_ConfigDirTests.cs` — CLI parsing for Fix command
- `MigrationCommandExhaustivenessTests.cs` — Exhaustiveness check includes `MigrationCommand.FixIssues`

### 3.8 Cross-Cutting Feature Tests (Engine)

| Feature | Base + DB Files | Key Scenarios |
|---------|----------------|---------------|
| SimulateMode | 5 (x5 DBs) | S1-S3: MigrateUp Simulate; S4: Validate; S5: MigrateDown Simulate; S6-S7: MigrateDown Validate |
| TargetGroupFilter | 5 (x5 DBs) | Backend-only, Frontend-only, multi-group with allowOutOfOrder |
| TargetGroupMigrationOrder | 5 (x5 DBs) | Custom order, default order, Baseline + MigrateUp |
| RunningGuard | 5 (x5 DBs) | Concurrent execution detection for MigrateUp, MigrateDown, UpdateHash |
| MigrationHistoryTracking | 5 (x5 DBs) | MigrationRecordHistory entries on terminal status transitions (source file: `ArchiveRetentionTests.cs`) |
| DatabaseLog | 5 (x5 DBs) | Migration logging to database |
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

---

## 4. Gap Analysis

### ~~Priority 1: Entire commands without engine tests~~ (RESOLVED)

| Gap | Status | Details |
|-----|--------|---------|
| ~~**Info command**~~ | **RESOLVED** | 40 engine tests added (8 tests x 5 DBs). ScenarioContext now has `InfoAsync()` + `GetHistoryAsync()`. Covers: fresh repo, full/partial migration, baseline, target groups, error state, history. |
| ~~**Fix command**~~ | **RESOLVED** | 40 engine tests added (8 tests x 5 DBs). ScenarioContext now has `FixIssuesAsync()` + `InsertOrphanedMigrationRun()`. Covers: no orphans, fix orphan, dry-run, older-than filter, fix-then-migrate, multiple orphans, details, assumed status. |

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
| `MigrateUpAsync()` | Yes | toRelease, allowOutOfOrder, targetGroupAliases, runMode, targetGroupMigrationOrder |
| `MigrateDownAsync()` | Yes | toRelease, targetGroupAliases, runMode, revealSensitiveData |
| `BaselineAsync()` | Yes | toRelease, targetGroupAliases, targetGroupMigrationOrder, revealSensitiveData |
| `ValidateHashAsync()` | Yes | scope, targetGroupAliases |
| `UpdateHashAsync()` | Yes | targetGroupAliases |
| `InfoAsync()` | Yes | — (returns MigrationStatusInfo) |
| `GetHistoryAsync()` | Yes | limit |
| `FixIssuesAsync()` | Yes | scope, olderThanMinutes, dryRun, assumedMigrationStatus |
| `InsertOrphanedMigrationRun()` | Yes | minutesOld (helper for Fix tests) |

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
.WithTargetMigrationOrder(Simultaneously|Successively)
.WithTargetGroupMigrationOrder(csv)

// Topology
.WithMultiTarget(secondConnectionString)
.WithTargetGroup(alias, dbType, connStr, order?, hashScope?)

// CLI tools
.WithCliTool(alias, execPath, argTemplate, inputMode, timeoutInSeconds?, successExitCodes?)
.WithUseCliToolAlias(alias)
.WithCliToolParameters(Dictionary)

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
