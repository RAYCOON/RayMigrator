# Engine Tests

Comprehensive integration tests for the RayMigrator migration engine, covering all combinations of MigrationErrorAction, RollbackErrorAction, RequireRollbackFile, TargetMigrationOrder, and multi-phase workflows.

## Overview

The engine test project (`Raycoon.RayMigrator.Tests.Engine`) tests the core migration pipeline — MigrateUp, MigrateDown, rollback behavior, error handling, and recovery. It uses a **single unified set of migration files per database engine**, with test-specific modifications applied at runtime via the `ScenarioBuilder` fluent API.

| Metric | Value |
|--------|-------|
| Total test methods | ~876 (across 169 test class files) |
| PostgreSQL tests | ~179 (35 test classes) |
| SqlServer tests | ~183 (36 test classes, includes 6 block-level tests, 11 CLI tool tests, and 2 atomic shared connection tests) |
| MariaDb tests | ~175 (34 test classes) |
| MySql tests | ~175 (34 test classes) |
| Sqlite tests | ~164 (30 test classes) |
| Backend migration file sets | 5 engines x 24 files = 120 files |
| Frontend migration file sets | 5 engines x 16 files = 80 files |
| Database engines | PostgreSQL, SQL Server, MariaDB, MySQL, SQLite |

## Prerequisites

1. Docker installed and running (required for PostgreSQL, SQL Server, MariaDB, MySQL tests)
2. Test containers started (see [Test Infrastructure](test-infrastructure.md))
3. .NET 10+ SDK installed

> **Note**: SQLite tests use a local temporary file database and do not require Docker.

## Running Tests

### All Engine Tests

```bash
dotnet test Raycoon.RayMigrator.Tests.Engine/
```

### By Database Engine

```bash
# PostgreSQL only
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Engine=PostgreSQL"

# SQL Server only
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Engine=SqlServer"

# MariaDB only
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Engine=MariaDb"

# MySQL only
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Engine=MySql"

# SQLite only
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Engine=Sqlite"
```

### By Test Category

```bash
# Only MigrateUp tests
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Category=MigrateUp"

# Only MigrateDown tests
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Category=MigrateDown"

# Only Compound (round-trip/recovery) tests
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Category=Compound"

# Only CLI tool integration tests
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Category=CliTool"

# Only CLI tool Docker raw tests (docker exec -i stdin piping)
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Category=CliToolDocker"
```

### By Specific Test Class

```bash
# Only Rollback tests for PostgreSQL
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "FullyQualifiedName~RollbackTests&Engine=PostgreSQL"

# Only BlockLevel tests (SqlServer-only)
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "FullyQualifiedName~BlockLevelTests"
```

### By Feature Category

```bash
# Only Features tests (Baseline, ValidateHash, Simulate, etc.)
dotnet test Raycoon.RayMigrator.Tests.Engine/ --filter "Category=Features"
```

### Without Docker

Tests automatically skip when Docker containers are unavailable. No test failures occur — tests are marked as skipped via `Assert.SkipUnless`. SQLite tests use a local temporary file database and do not require Docker; `SqliteFixture.IsDatabaseAvailable` always returns `true`.

> **Note**: CliTool tests (`Category=CliTool`) and CliToolDocker tests (`Category=CliToolDocker`) both require Docker because they use `docker exec` to pipe SQL into the database containers. These tests are skipped when the target engine's Docker container is unavailable.

## Architecture

### Project Structure

```
Raycoon.RayMigrator.Tests.Engine/
├── Infrastructure/
│   ├── CliToolConfigHelper.cs      — Pre-built CLI tool configs for CliTool tests (File and Stdin modes)
│   ├── DockerExecHelper.cs         — Static Process wrapper for docker exec -i stdin piping
│   ├── EngineTestHost.cs           — DI container (mirrors Program.cs setup)
│   ├── ScenarioBuilder.cs          — Fluent API for test scenario configuration
│   ├── ScenarioContext.cs          — Execution + assertions
│   ├── SqlDialect.cs               — Engine-specific error SQL
│   ├── EngineConfig.cs             — Engine configuration record
│   ├── MigrationRecordExpectation.cs  — Partial-match DTO for MigrationRecord table
│   ├── MigrationRunExpectation.cs     — Partial-match DTO for MigrationRun table
│   ├── PostgreSqlTestBase.cs       — PostgreSQL base class
│   ├── SqlServerTestBase.cs        — SqlServer base class
│   ├── MariaDbTestBase.cs          — MariaDB base class
│   ├── MySqlTestBase.cs            — MySQL base class
│   └── SqliteTestBase.cs           — SQLite base class
├── Fixtures/                       — Engine-specific xUnit fixtures (PostgreSQL, SqlServer, MariaDb, MySql, Sqlite)
├── Collections/                    — xUnit collection definitions
├── MigrationFiles/                 — ONE unified base set per engine
│   ├── PostgreSQL/                 — 24 files (4 releases × 6, Backend/ subfolder)
│   ├── PostgreSQL_Frontend/        — 16 files (4 releases × 4, Frontend/ subfolder)
│   ├── SqlServer/                  — 24 files (DML files have 3 GO blocks)
│   ├── SqlServer_Frontend/         — 16 files (4 releases × 4, Frontend/ subfolder)
│   ├── MariaDb/                    — 24 files
│   ├── MariaDb_Frontend/           — 16 files
│   ├── MySql/                      — 24 files
│   ├── MySql_Frontend/             — 16 files
│   ├── Sqlite/                     — 24 files
│   └── Sqlite_Frontend/            — 16 files
└── Tests/
    ├── MigrateUp/                  — 51 test classes (10 PostgreSQL, 11 SqlServer, 10 MariaDb, 10 MySql, 10 Sqlite)
    ├── MigrateDown/                — 15 test classes (3 per engine)
    ├── Compound/                   — 10 test classes (2 per engine)
    ├── Features/                   — 76 test classes (15 per engine + 1 SqlServer-only AtomicSharedConnectionTests)
    └── CliTool/                    — 17 test classes (4 engines × 4 classes [File, Stdin, Docker raw, ExitCodeRange] + 1 PresetConsistency; no Sqlite)
```

### ScenarioBuilder — Fluent Test Setup

Every test follows this pattern:

```csharp
[Fact]
public async Task ErrorInR3_OnlyR3RolledBack()
{
    Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

    await using var ctx = await CreateScenario()
        .InjectError("Release_3.0", "03_SeedDataC.sql")
        .WithMigrationErrorAction(MigrationErrorAction.RollbackRelease)
        .BuildAsync();

    await ctx.MigrateUpAsync();

    ctx.AssertSuccess(false);
    ctx.AssertRunResult(MigrationRunResult.Error);
    ctx.AssertFileStatuses(
        ("01_CreateTableA.sql", MigrationStatus.Migrated),
        ("01_CreateTableE.sql", MigrationStatus.NotMigrated),
        ...);
}
```

**Key builder methods:**
- `InjectError(release, filename)` — replaces SQL body with error-producing SQL
- `RemoveRollback(release, filename)` — deletes rollback file
- `BreakRollback(release, filename)` — replaces rollback SQL with broken SQL
- `SetFileToml(release, filename, key, value)` — overrides TOML metadata
- `InjectErrorAtBlock(release, filename, blockIndex)` — SqlServer: replaces specific GO block
- `WithMigrationErrorAction(action)` — sets product-level error action
- `WithRollbackErrorAction(action)` — sets rollback error action
- `WithRequireRollbackFile(bool)` — sets rollback file requirement
- `WithStopRollbackOnMissingRollbackFile(bool)` — sets whether the error-recovery rollback chain stops (`true`) or continues (`false`) when a rollback file is missing; only effective when `RequireRollbackFile=false`
- `WithTargetMigrationOrder(order)` — sets execution order (Simultaneously/Successively)
- `WithMultiTarget(connectionString)` — enables multi-target mode (adds a second target to the Backend target group)
- `WithTargetGroup(alias, databaseType, connectionString, order?, hashScope?)` — adds an additional target group (used for multi-target-group tests)
- `WithCliTool(alias, executablePath, argumentTemplate, inputMode, timeoutInSeconds?, successExitCodes?)` — registers a CLI tool in the product configuration
- `WithUseCliToolAlias(alias)` — sets the `UseCliToolAlias` on the product so all migrations use the registered CLI tool
- `WithCliToolParameters(parameters)` — sets named placeholder parameters for CLI tool argument template substitution
- `WithFlatLayoutForRelease(release)` — moves migration files for a release from the `Backend/` subdirectory to the release root (flat layout), enabling mixed flat/traditional layout tests
- `WithTargetGroupMigrationOrder(commaSeparated)` — sets `TargetGroupMigrationOrder` at the product level in appsettings (comma-separated string, e.g. `"Frontend,Backend"`)
- `SetMigSettings(relativeFilePath, entries)` — creates or updates a migsettings.txt file in the work directory
- `WithDatabaseLogging(minimumLevel?)` — enables database log sink for the test run (default level: Debug)
- `WithTargetMaxRetries(maxRetries, retryDelayMs?)` — sets `DbCommandMaxRetries` and `DbCommandWaitTimeInMsBeforeRetry` on the primary target (used for retry/atomic shared-connection tests)
- `WithTargetCommandTimeout(seconds)` — sets `DbCommandTimeoutInSeconds` on the primary target

### ScenarioContext — Execution + Assertions

**Execution:**
- `MigrateUpAsync(toRelease?, allowOutOfOrder?, targetGroupAliases?, runMode?, targetGroupMigrationOrder?)` — executes MigrateUp
- `MigrateDownAsync(toRelease, targetGroupAliases?, runMode?)` — executes MigrateDown
- `BaselineAsync(toRelease?, targetGroupAliases?, targetGroupMigrationOrder?)` — executes Baseline
- `ValidateHashAsync(scope?, targetGroupAliases?)` — executes ValidateHash
- `UpdateHashAsync(targetGroupAliases?)` — executes UpdateHash
- `RebuildForAsync(command, mode, toRelease?)` — rebuilds DI container for next phase (no DB cleanup)

**Assertions:**
- `AssertSuccess(bool)` — checks operation result
- `AssertRunResult(MigrationRunResult)` — checks latest MigrationRun.MigrationRunResultId
- `AssertRunCount(int)` — checks total MigrationRun count
- `AssertFileStatus(filename, MigrationStatus)` — checks MigrationStatusId
- `AssertFileStatuses(params ...)` — bulk file status check
- `AssertFileStatusForTarget(filename, target, status)` — multi-target status check
- `AssertMigrationRecord(filename, expectation)` — checks all MigrationRecord table columns
- `AssertMigrationRun(runIndex, expectation)` — checks all MigrationRun table columns
- `AssertTableExists(tableName, bool)` — checks user table existence on primary connection
- `AssertTableExistsOnConnection(connectionString, tableName, bool)` — checks user table existence on specific connection
- `AssertRowCount(tableName, int)` — checks user table row count
- `AssertRepositoryTableExists(tableName, bool)` — checks repository table existence
- `AssertProductExists(bool)` — checks that the Product record exists in the repository
- `AssertTargetGroupMigrationOrder(params string[])` — checks that MigrationRecord rows appear in the repository in the expected target group order (by ascending Id)

**Query helpers (for custom assertions):**
- `GetMigrationRunSettingsJson()` — returns the latest MigrationRun's settings JSON
- `GetMigrationConfigJson(filename)` — returns the stored config JSON for a specific migration file
- `CountMigrationHistory()` — counts rows in the MigrationRecordHistory archive table
- `CountLogEntries()` — counts database log entries (requires `WithDatabaseLogging`)
- `CountLogEntriesAtLevel(logLevelId)` — counts log entries at a specific Serilog level
- `CountMigrationsForTargetGroup(tgAlias)` — counts MigrationRecord rows for a target group
- `CountMigrations()` — counts all MigrationRecord rows
- `CountMigrationRuns()` — counts all MigrationRun rows
- `CountRepoRows(tableName)` — counts rows in any repository table
- `CountMigrationsByFilename(filename)` — counts MigrationRecord rows for a specific filename
- `CountMigrationsWithStatus(statusId)` — counts MigrationRecord rows with a specific status
- `GetLatestRunResultId()` — returns the MigrationRunResultId of the most recent run
- `InsertRunningMigrationRun()` — inserts a Running MigrationRun record (for concurrent-guard tests)
- `WorkDirectory` — path to the temporary work directory containing mutation-applied migration files

### Migration File Design

**Zero cascading failures** — each file within a release is independent:
- F1 and F2 create separate, unrelated tables (no FK)
- F3 (seed data) depends only on F1's table, not F2's
- No cross-release dependencies

| Release | F1 | F2 | F3 (seeds F1) |
|---------|----|----|---------------|
| Release_1.0 | tablea | tableb | 3 rows → tablea |
| Release_2.0 | tablec | tabled | 3 rows → tablec |
| Release_3.0 | tablee | tablef | 3 rows → tablee |
| Release_4.0 | tableg | tableh | 3 rows → tableg |

**SqlServer DML files** use 3 `GO`-separated blocks (one INSERT per block) to enable block-level testing. All other engines have single-block files.

**Frontend migration files** (`PostgreSQL_Frontend/`, `SqlServer_Frontend/`, etc.) contain a `Frontend/` target group subfolder with 4 files per release (2 tables × 2 = create + rollback). These are used by `TargetGroupFilterTests` (target group filtering) and `TargetGroupMigrationOrderTests` (execution order verification across multiple target groups).

## Complete Test Matrix

### Abbreviation Key

**Status**: M=Migrated(100), NM=NotMigrated(50), F=Failed(30), NR=NoRecord
**Actions**: T=Terminate, RB=Rollback, RR=RollbackRelease, RE=RollbackErrorOnly, IG=Ignore
**Config**: RbErr=RollbackErrorAction, ReqRB=RequireRollbackFile

### Test Catalog with Abbreviations

The catalog lists the unique test scenarios defined for the base PostgreSQL test classes. Each scenario is replicated across all supported engines (MariaDb, MySql, SqlServer) by engine-specific test classes with the same test methods.

| ID | Abbr | Test Name | Class |
|----|------|-----------|-------|
| #1 | HP-1 | AllFourReleases_AllSucceed | HappyPathTests |
| #2 | HP-2 | TwoReleases_PartialMigration | HappyPathTests |
| #3 | T-1 | ErrorInFirstFile | TerminateTests |
| #4 | T-2 | ErrorInMiddleRelease | TerminateTests |
| #5 | T-3 | ErrorInLastFile | TerminateTests |
| #6 | RB-1 | ErrorInR4_AllRolledBack | RollbackTests |
| #7 | RB-2 | ErrorInR2_AllRolledBack | RollbackTests |
| #8 | RB-3 | ErrorInR1_SingleFileRolledBack | RollbackTests |
| #9 | RB-4 | BrokenRollback_Terminate_ChainAborted | RollbackTests |
| #10 | RB-5 | BrokenRollback_Ignore_ChainContinues | RollbackTests |
| #11 | RB-6 | MissingRollback_RequireTrue_PreValidationFails | RollbackTests |
| #12 | RB-7 | MissingRollback_RequireFalse_ChainSkipsAndContinues | RollbackTests |
| #13 | RB-8 | MissingMultipleRollbacks_RequireFalse | RollbackTests |
| #14 | RR-1 | ErrorInR3_OnlyR3RolledBack | RollbackReleaseTests |
| #15 | RR-2 | ErrorInR2_OnlyR2RolledBack | RollbackReleaseTests |
| #16 | RR-3 | ErrorInR1_AllR1RolledBack | RollbackReleaseTests |
| #17 | RR-4 | ErrorAtReleaseBoundary | RollbackReleaseTests |
| #18 | RR-5 | BrokenRollback_Terminate | RollbackReleaseTests |
| #19 | RR-6 | BrokenRollback_Ignore | RollbackReleaseTests |
| #20 | RR-7 | MissingRollback_RequireFalse | RollbackReleaseTests |
| #21 | RE-1 | OnlyErrorFileRolledBack | RollbackErrorOnlyTests |
| #22 | RE-2 | BrokenRollback | RollbackErrorOnlyTests |
| #23 | RE-3 | MissingRollback_RequireFalse | RollbackErrorOnlyTests |
| #24 | IG-1 | SingleError_ExecutionContinues | IgnoreTests |
| #25 | IG-2 | MultipleErrors_AllIgnored | IgnoreTests |
| #26 | IG-3 | IgnoredFile_NotInRollbackChain | IgnoreTests |
| #27 | IN-1 | TwoPhases_AllSucceed | IncrementalTests |
| #28 | IN-2 | R1R2First_R3Error_Rollback_OnlyCurrentRunRolledBack | IncrementalTests |
| #29 | IN-3 | R1R2First_R3Error_RollbackRelease_SameAsRollback | IncrementalTests |
| #30 | IN-4 | R1First_R2R3Error_Rollback_R2AndR3RolledBack | IncrementalTests |
| #31 | IN-5 | R1First_R2R3Error_RollbackRelease_OnlyR3RolledBack | IncrementalTests |
| #32 | MT-1 | Simultaneously_Ignore_SkipsSecondTarget | MultiTargetTests |
| #33 | MT-2 | Simultaneously_Rollback_BothTargets | MultiTargetTests |
| #34 | MT-3 | Successively_Terminate_SecondTargetNeverStarts | MultiTargetTests |
| #35 | MT-4 | Successively_Rollback_BothTargets | MultiTargetTests |
| #36 | MD-1 | ToRelease2_R3R4RolledBack | MigrateDown/HappyPathTests |
| #37 | MD-2 | ToRelease1_OnlyR1Stays | MigrateDown/HappyPathTests |
| #38 | MD-3 | FullRollback_AllReleases | MigrateDown/HappyPathTests |
| #39 | ME-1 | MissingRollback_RequireFalse_SkipAndContinue | MigrateDown/ErrorTests |
| #40 | ME-2 | MissingRollback_RequireTrue_ChainAborted | MigrateDown/ErrorTests |
| #41 | ME-3 | BrokenRollback_Terminate_ChainAborted | MigrateDown/ErrorTests |
| #42 | ME-4 | BrokenRollback_Ignore_ChainContinues | MigrateDown/ErrorTests |
| #43 | EC-1 | NoOp_AlreadyAtTarget | MigrateDown/EdgeCaseTests |
| #44 | EC-2 | NoOp_BelowTarget | MigrateDown/EdgeCaseTests |
| #45 | EC-3 | PartialDown_ToR2_ThenDown_ToR1 | MigrateDown/EdgeCaseTests |
| #46 | RT-1 | UpDownUp_AllMigratedAgain | Compound/RoundTripTests |
| #47 | RT-2 | UpErrorRollbackRelease_ThenMigrateDown | Compound/RoundTripTests |
| #48 | RC-1 | AfterTerminate_IncrementalRerun | Compound/RecoveryTests |
| #49 | RC-2 | NothingToMigrate_SecondRun | Compound/RecoveryTests |
| #50 | BL-1 | ErrorAtBlock2Of3_Terminate | BlockLevelTests (SS only) |
| #51 | BL-2 | ErrorAtBlock1Of3_Terminate | BlockLevelTests (SS only) |
| #52 | BL-3 | ErrorAtBlock0Of3_Terminate | BlockLevelTests (SS only) |
| #53 | BL-4 | ErrorAtBlock2Of3_Rollback_CompleteRollbackExecuted | BlockLevelTests (SS only) |
| #54 | BL-5 | ErrorAtBlock1Of3_Rollback_PrecedingFilesAlsoRolledBack | BlockLevelTests (SS only) |
| #55 | BL-6 | MultiBlock_AllSucceed_BlockCountCorrect | BlockLevelTests (SS only) |
| #56 | RA-1 | RunAlways_ReExecutedOnSecondRun | RunAlwaysTests |
| #57 | RA-2 | RunAlways_FailsOnRerun_Terminate | RunAlwaysTests |
| #58 | RA-3 | RunAlways_FailsOnRerun_Rollback | RunAlwaysTests |
| #59 | RA-4 | RunAlwaysFalse_NotReExecuted | RunAlwaysTests |
| #60 | FL-1 | MixedFlatAndTraditionalLayout_AllReleasesMigrateSuccessfully | FlatLayoutTests |
| #61 | FL-2 | MixedLayout_MigrateDownToRelease2_RollsBackFlatAndTraditionalReleases | FlatLayoutTests |

**Features tests** (all 5 engines, 15 test classes each; SqlServer has 16):

| Class | Tests | Description |
|-------|-------|-------------|
| `MigrationHistoryTrackingTests` | 5 | MigrationRecordHistory records written inline on terminal status transitions (simulate, first run, failed retry, down-then-up, baseline). Source file: `ArchiveRetentionTests.cs`. |
| `BaselineTests` | 10 | Baseline marking, incremental baseline, baseline then MigrateUp |
| `DatabaseLogTests` | 3 | Log entries written after MigrateUp, multiple log levels, logs during error |
| `FixTests` | 8 | Fix command: no orphans, fix orphan, dry-run, older-than filter, fix-then-migrate, multiple orphans, details, assumed-status=Migrated |
| `InfoTests` | 8 | Info command: fresh repo, full/partial migration, baseline, target groups, error state, multiple-run history, run details |
| `MigrationRunMetaTests` | 7 | MigrationRunSettingsJson content (console options, product, masked credentials, MigrateDown masking, Baseline masking) |
| `MigSettingsInheritanceTests` | 13 | migsettings.txt inheritance (root, release, target group, TOML override) |
| `OutOfOrderBlockingTests` | 3 | Out-of-order migration blocking (false=blocked, true=allowed, data integrity after blocking) |
| `RepositoryIntegrityTests` | 4 | Repository tables, Product record, Migration records after successful run |
| `RunningGuardTests` | 6 | Concurrent run prevention (MigrateUp, MigrateDown, Baseline blocked; ValidateHash/UpdateHash allowed) |
| `SimulateModeTests` | 7 | Simulate mode (no user tables written), Validate mode, MigrateDown simulate, MigrateDown Validate (non-destructive + missing rollback detection) |
| `TargetGroupMigrationOrderTests` | 10 | TargetGroupMigrationOrder overrides (CLI, appsettings, migsettings), wrong-case error, Baseline, single-TG rejection |
| `TargetGroupFilterTests` | 12 | Filtering by target group alias (Backend-only, Frontend-only, both) |
| `UpdateHashTests` | 5 | Update-Hash: no updates after fresh migration, update after file modification, validate passes after update, idempotent second run, empty-repo no-op |
| `ValidateHashTests` | 8 | Hash validation in File and SqlBlocks scopes, detecting modifications, Disabled scope (ignores modification, passes after full migration) |
| `SqlServerAtomicSharedConnectionTests` | 2 | SqlServer-only: atomic shared-connection path — transient error triggers file-level retry with full transaction rollback (ASC1), permanent error rolls back all blocks including prior DDL (ASC2) |

**CliTool tests** (PostgreSQL, SqlServer, MariaDb, MySql — 4 engines × 2 classes [File + Stdin] = 8 test classes; not available for SQLite):

| Class | Tests | Description |
|-------|-------|-------------|
| `{Engine}CliToolFileTests` | 3 | Full MigrateUp via CLI tool in File mode: all releases succeed, partial migration, simulate mode |
| `{Engine}CliToolStdinTests` | 3 | Full MigrateUp via CLI tool in Stdin mode: all releases succeed, partial migration, simulate mode |

CLI tool tests use `CliToolConfigHelper` to obtain pre-built configurations that pipe migration SQL through `docker exec` into the running database container. Tests verify that the engine correctly delegates SQL execution to the external CLI tool instead of executing directly through the DAL.

**CliToolDocker tests** (`Category=CliToolDocker`) — PostgreSQL, SqlServer, MariaDb, MySql — 4 engines × 1 class = 4 test classes; not available for SQLite:

| Class | Tests | Description |
|-------|-------|-------------|
| `{Engine}CliToolDockerTests` | 3 | Raw `docker exec -i` + stdin piping tests against Docker containers |

These tests exercise `DockerExecHelper` directly, independent of the migration engine:

| Test | Description |
|------|-------------|
| `DockerExec_CreateTableAndInsert_DataExists` | Creates a table and inserts a row via stdin; verifies row count via `RepositoryQueryHelper` |
| `DockerExec_InvalidSql_ReturnsNonZeroExitCode` | Sends invalid SQL; asserts non-zero exit code from the CLI tool |
| `DockerExec_EmptyInput_Succeeds` | Sends empty stdin; asserts exit code 0 |

All four classes use `[Trait("Category", "CliToolDocker")]` and skip via `Assert.SkipUnless(Fixture.IsDatabaseAvailable)` when the target container is not running.

**CliTool ExitCodeRange tests** — PostgreSQL, SqlServer, MariaDb, MySql — 4 engines × 1 class = 4 test classes; not available for SQLite:

| Class | Tests | Description |
|-------|-------|-------------|
| `{Engine}CliToolExitCodeRangeTests` | 2 | Verifies `SuccessExitCodes` range notation through the full engine path (Config → MigrationService → CliToolExecutor → ExitCodeMatcher) |

| Test | Description |
|------|-------------|
| `RangeIncludesZero_ToolReturnsZero_MigrationSucceeds` | Closed range `"0..5"` includes 0; CLI tool returns 0 on success → migration succeeds |
| `RangeExcludesZero_ToolReturnsZero_MigrationFails` | Closed range `"1..5"` excludes 0; CLI tool returns 0 → ExitCodeMatcher rejects → migration fails |

**CliTool Preset Consistency tests** — 1 test class, 16 test cases (no Docker required):

| Class | Tests | Description |
|-------|-------|-------------|
| `CliToolPresetConsistencyTests` | 16 | Verifies Docker presets in `CliToolPresetProvider` are structurally consistent with test configurations in `CliToolConfigHelper` |

Tests validate that for each of the 4 Docker presets (sqlcmd-docker, psql-docker, mariadb-docker, mysql-docker): the resolved argument template, input mode, executable path, and timeout match the corresponding test configuration. These are pure unit tests that run without Docker.

### Cross-Reference Matrices

#### MigrationErrorAction x Error Position

|  | R1/F1 | R1/F3 | R2/F1 | R2/F2 | R3/F3 | R4/F3 |
|---|---|---|---|---|---|---|
| **Terminate** | T-1 | — | — | T-2 | — | T-3 |
| **Rollback** | RB-3 | — | — | RB-2 | — | RB-1 |
| **RollbackRelease** | — | RR-3 | RR-4 | RR-2 | RR-1 | — |
| **RollbackErrorOnly** | — | — | — | RE-1 | — | — |
| **Ignore** | — | — | — | IG-1 | — | — |

#### Rollback Complication x MigrationErrorAction

|  | Rollback | RollbackRelease | RollbackErrorOnly |
|---|---|---|---|
| **All rollbacks OK** | RB-1, RB-2, RB-3 | RR-1, RR-2, RR-3, RR-4 | RE-1 |
| **Broken rb + RbErr=Terminate** | RB-4 | RR-5 | RE-2 |
| **Broken rb + RbErr=Ignore** | RB-5 | RR-6 | — |
| **Missing rb + ReqRB=true** | RB-6 | — | — |
| **Missing rb + ReqRB=false** | RB-7, RB-8 | RR-7 | RE-3 |

#### MigrateDown x Complication

|  | Success | Missing rb (Req=true) | Missing rb (Req=false) | Broken rb (Term) | Broken rb (Ign) |
|---|---|---|---|---|---|
| **MigrateDown** | MD-1, MD-2, MD-3 | ME-2 | ME-1 | ME-3 | ME-4 |

#### Multi-Phase Scenarios

|  | Incremental Up | Up→Down | Up→Down→Up | Recovery | RunAlways |
|---|---|---|---|---|---|
| **Success** | IN-1 | MD-1,2,3, EC-1,2,3 | RT-1 | RC-2 | RA-1, RA-4 |
| **Error + Rollback** | IN-2, IN-4 | — | — | — | RA-3 |
| **Error + RbRelease** | IN-3, IN-5 | — | RT-2 | — | — |
| **Error + Terminate** | — | — | — | RC-1 | RA-2 |
| **Down-Error** | — | ME-1,2,3,4 | — | — | — |

#### Block-Level (SqlServer only)

| Block Error Position | Terminate | Rollback |
|---------------------|-----------|----------|
| Block 0 of 3 | BL-3 | — |
| Block 1 of 3 | BL-2 | BL-5 |
| Block 2 of 3 | BL-1 | BL-4 |
| No error | — | BL-6 |

## Repository Assertions

Tests verify the following database columns:

**MigrationRun table:**
- `MigrationRunResultId` (Ok=100, Error=90)
- `Environment` (always "Docker")
- `FromReleaseVersion`, `ToReleaseVersion`

**MigrationRecord table:**
- `MigrationStatusId` (Migrated=100, NotMigrated=50, Failed=30)
- `MigrationOperationId` (MigrateUp=100)
- `Environment`, `ReleaseVersion`, `TargetGroupAlias`, `TargetAlias`
- `FileOrderId` (1-12, sequential)
- `FileUpBlocksMigrated`, `FileUpBlocksTotal` (block-level execution tracking)
- `MigrateDownFileExists` (set at MigrateUp discovery time)
- `FileDownBlocksMigrated`, `FileDownBlocksTotal` (rollback block tracking)

### Block Count Semantics

- **Non-Ignore mode, error file**: `FileUpBlocksMigrated` = 1-based index of the block that failed (ranges from 1 to `FileUpBlocksTotal`; equals 1 when the first block fails, equals `FileUpBlocksTotal` when the last block fails)
- **Ignore mode, error file**: `FileUpBlocksMigrated` = `FileUpBlocksTotal` (all blocks attempted; DB record updated after every block attempt)
- **Successful file**: `FileUpBlocksMigrated` = `FileUpBlocksTotal`
- **After successful rollback**: `FileDownBlocksMigrated` = `FileDownBlocksTotal`
- **Missing rollback file**: `FileDownBlocksMigrated` and `FileDownBlocksTotal` remain `NULL`

### Rollback Result Matrix

How the engine handles each combination of rollback file existence, SQL outcome, `RequireRollbackFile`, and `RollbackErrorAction`:

| # | Rollback File | SQL | RequireRB | RbErrAction | File Status | Chain | RunResult | Success | Engine API |
|---|--------------|-----|-----------|-------------|-------------|-------|-----------|---------|------------|
| 1 | **exists** | **OK** | any | any | NotMigrated(50) | continues | Ok(100) | true | SuccessCount++ |
| 2 | **exists** | **FAILS** | any | **Terminate** | Failed(30) | **ABORTED** | Error(90) | false | AddFailure |
| 3 | **exists** | **FAILS** | any | **Ignore** | Failed(30) | continues | Ok(100) | **true** | AddWarning |
| 4 | **missing** | — | **true** | any* | Failed(30) | **ABORTED** | Error(90) | false | AddFailure |
| 5 | **missing** | — | **false** | any* | **Unchanged** | continues | Ok(100) | **true** | AddWarning |

*`RequireRollbackFile` takes precedence over `RollbackErrorAction` for missing files. `RollbackErrorAction` only applies to SQL execution errors.

**Key insight**: Rows 3 and 5 return `Success=true` by design. When the administrator chooses `Ignore` or `RequireRollbackFile=false`, they explicitly tolerate the situation. For row 3, the file is marked `Failed(30)` (SQL execution failed). For row 5, the file's status is **not updated** — it retains its previous value (e.g., `Migrated(100)`) because the missing rollback file is a known acceptable condition, not an error. In both rows, the engine uses `AddWarning` (not `AddFailure`), so `FailCount` stays 0 and `AllSuccessful` (= `FailCount == 0`) evaluates to `true`.

**Impact on other files in the rollback chain**:

| Position relative to problem file | Chain continues (rows 1,3,5) | Chain aborted (rows 2,4) |
|-----------------------------------|------------------------------|--------------------------|
| Before problem (newer releases) | NotMigrated (already rolled back) | NotMigrated (already rolled back) |
| **Problem file (row 3)** | Failed(30) | Failed(30) |
| **Problem file (row 5, RequireRB=false)** | Unchanged (Migrated) | Unchanged (Migrated) |
| After problem (older releases) | Processed normally → NotMigrated | **NOT processed → stay Migrated** |

**MigrateUp vs MigrateDown**: This matrix applies to the ROLLBACK portion of both operations. For MigrateUp with automatic rollback (triggered by `MigrationErrorAction=Rollback`), the overall `MigrationRunResult` is always `Error` because the migration itself failed — regardless of how the rollback went. For MigrateDown, the rollback IS the operation, so the matrix directly determines the result.

## Extending the Tests

### Adding a New Scenario

No new migration files needed — just add a test method:

```csharp
[Fact]
public async Task MyNewScenario()
{
    Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

    await using var ctx = await CreateScenario()
        .InjectError("Release_2.0", "02_CreateTableD.sql")
        .WithMigrationErrorAction(MigrationErrorAction.Rollback)
        .WithRollbackErrorAction(RollbackErrorAction.Ignore)
        .BuildAsync();

    await ctx.MigrateUpAsync();

    ctx.AssertSuccess(false);
    ctx.AssertFileStatuses(/* expected statuses */);
}
```

### Adding a New Database Engine

To add a new database engine, create a fixture in `Fixtures/`, a collection definition in `Collections/`, a test base class in `Infrastructure/`, and migration files in `MigrationFiles/{Engine}/` and `MigrationFiles/{Engine}_Frontend/`. Then add engine-specific test classes in `Tests/MigrateUp/`, `Tests/MigrateDown/`, `Tests/Compound/`, and `Tests/Features/` following the pattern of the existing engines.

### Adding a New Feature Scenario

Feature tests live in `Tests/Features/`. To add a new scenario:

```csharp
[Fact]
public async Task MyNewScenario()
{
    Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");

    await using var ctx = await CreateScenario()
        .WithDatabaseLogging()
        .BuildAsync();

    await ctx.MigrateUpAsync();

    ctx.AssertSuccess(true);
    ctx.CountLogEntries().Should().BeGreaterThan(0);
}
```
