# Engine Tests

Comprehensive integration tests for the RayMigrator migration engine, covering all combinations of MigrationErrorAction, RollbackErrorAction, RequireRollbackFile, TargetMigrationOrder, and multi-phase workflows.

## Overview

The engine test project (`Raycoon.RayMigrator.Tests.Engine`) tests the core migration pipeline — MigrateUp, MigrateDown, rollback behavior, error handling, and recovery. It uses a **single unified set of migration files per database engine**, with test-specific modifications applied at runtime via the `ScenarioBuilder` fluent API.

| Metric | Value |
|--------|-------|
| Total tests | ~1559, every theory row counted (1532 test methods across 247 test class files, including the engine-agnostic `CliToolPresetConsistencyTests` with 16 inline cases; 10 are skipped on purpose) |
| PostgreSQL tests | ~302 (48 test classes; most use unprefixed filenames as the "base" engine) |
| SqlServer tests | ~313 (52 test classes, includes 6 block-level tests, 3 resume tests, 12 CLI tool tests, 2 atomic shared connection tests and 1 killed pooled session test) |
| MariaDb tests | ~306 (50 test classes; one is `MariaDbSessionTimezoneTests` defined alongside `MySqlSessionTimezoneTests` in `P2_MySqlMariaDbSessionTimezoneTests.cs`) |
| MySql tests | ~306 (50 test classes; one is `MySqlSessionTimezoneTests` defined in the same shared P2 file) |
| Sqlite tests | ~316 (47 test classes; includes the SQLite-only `SqliteDatetimeCheckConstraintTests`, `SqliteCommandProfileTests`, `SqliteFileEncodingTests` and `SqliteCliSignalTests`) |
| Backend migration file sets | 5 engines x 24 files = 120 files |
| Frontend migration file sets | 5 engines x 16 files = 80 files |
| Database engines | PostgreSQL, SQL Server, MariaDB, MySQL, SQLite |

> **Note**: PostgreSQL is treated as the "base" engine — most of its test classes have unprefixed filenames (`HappyPathTests.cs`, `RollbackTests.cs`, etc.) and live in the same `Tests.Engine` namespace; the CLI tool classes and the seven feature classes that run a shared contract (`PostgreSqlRepositorySchemaServiceTests`, `PostgreSqlRepositoryPrefixTests`, `PostgreSqlProductIsolationTests`, `PostgreSqlCancellationTests`, `PostgreSqlRepositoryMaintenanceTests`, `PostgreSqlDalCancellationTests`, `PostgreSqlDalTimeoutTests`) carry the engine prefix like the other engines; `InfoTests`, `DatabaseLogTests` and `ReadModelContractTests` run a shared contract too but keep the unprefixed name. Each unprefixed class is replicated by engine-specific wrapper classes (e.g. `SqlServerHappyPathTests`, `MariaDbHappyPathTests`, `MySqlHappyPathTests`, `SqliteHappyPathTests`). The lone exception is `P2_MySqlMariaDbSessionTimezoneTests.cs`, which contains two engine-specific classes (one for MySQL, one for MariaDB) and runs against neither PostgreSQL nor SqlServer.

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

Tests automatically skip when Docker containers are unavailable. No test failures occur — tests call `Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available")` (xUnit v3 native skip support). SQLite tests use a local temporary file database and do not require Docker; `SqliteFixture.IsDatabaseAvailable` always returns `true`.

> **Note**: CliTool tests (`Category=CliTool`) and CliToolDocker tests (`Category=CliToolDocker`) both require Docker because they use `docker exec` to pipe SQL into the database containers. These tests are skipped when the target engine's Docker container is unavailable.

## Architecture

### Project Structure

```
Raycoon.RayMigrator.Tests.Engine/
├── Infrastructure/
│   ├── CancellationContract.cs     — Shared assertions of CancellationTests: a cancelled run stops at the next block boundary and takes the MigrationCancelledAction of the test; migrate-down, baseline and a running error recovery stop and ignore it; the run is recorded as Cancelled (70), the lookup row of schema step 3, a migrate-up whose error recovery the cancel stopped as Error with its failure; a migrate-up refuses a file whose rollback stopped in the middle; update-hash and fix return what they did so far; the warnings of a cancel carry no stack trace
│   ├── CancellingProgressObserver.cs — Records the progress events like RecordingProgressObserver and cancels the run once, at the first event that matches its trigger (after a block, after a file, when a file starts), so the cancellation tests need no timing
│   ├── CliToolConfigHelper.cs      — Pre-built CLI tool configs for CliTool tests (File and Stdin modes)
│   ├── DalSharedConnectionCancellationContract.cs — Shared assertions of DalCancellationTests: the shared-connection calls of a DAL hand the caller's token to the command on an open connection and transaction, so the provider reports a token cancelled before the call
│   ├── DalTimeoutContract.cs       — Shared assertions of DalTimeoutTests: a command that runs into its command timeout is a timeout for DalBase.IsTimeout, however the provider reports it
│   ├── DalUserLockContract.cs      — Shared assertions of DalUserLockTests (MySQL, MariaDB): a call that fails after its GET_LOCK leaves no lock on the pooled session
│   ├── DatabaseLogIndexContract.cs — Shared assertions of the log index tests in DatabaseLogTests: DatabaseLogging_CheckCreate gives MigrationLog an index on MigrationRunId and one on CreatedAt, also on a log table of an older engine
│   ├── DockerExecHelper.cs         — Static Process wrapper for docker exec -i stdin piping
│   ├── EngineConfig.cs             — Engine configuration record
│   ├── EngineTestHost.cs           — DI container (mirrors Program.cs setup), built asynchronously because it initializes the database log writer (InitDatabaseLoggerAsync); loads the scenario's appsettings.json through JsonOptionsSource, so the host runs in the product scope of a run (ADR-026), stops on a placeholder whose variable is not set, names a failed element by alias and hands the rule catalog the section as written, like DirectModePipeline; hands every log event to the LogObserver of the test, on the logging thread before the call returns
│   ├── MariaDbTestBase.cs          — MariaDB base class
│   ├── MigrationRecordExpectation.cs  — Partial-match DTO for MigrationRecord table
│   ├── MigrationRunExpectation.cs     — Partial-match DTO for MigrationRun table
│   ├── MySqlTestBase.cs            — MySQL base class
│   ├── ParameterRoundTripValues.cs — Values the parameter round-trip tests send through a template parameter
│   ├── PendingMigrationsContract.cs — Shared assertions of the pending list in InfoTests: MigrationStatusInfo.PendingFiles in the order and with the reason of the next migrate-up, which every scenario then runs and compares with the list
│   ├── PostgreSqlTestBase.cs       — PostgreSQL base class
│   ├── PrefixTableCleanup.cs       — The tables of a scenario's table prefix, which ScenarioContext.DisposeAsync drops: the repository and log tables of the prefix in the repository database and in a log database of its own
│   ├── PrefixTableCleanupContract.cs — Shared assertions of RepositoryPrefixTests: a disposed scenario with a prefix leaves no table of its prefix in the catalog
│   ├── ProductIsolationContract.cs — Shared assertions of ProductIsolationTests: a run of the main product beside a broken second product, and the broken product failing on its own findings
│   ├── ReadModelContract.cs        — Shared assertions of ReadModelContractTests: every repository table reads through the read model package
│   ├── RecordingProgressObserver.cs — Records the progress events of a run in order (ProgressEventsTests)
│   ├── RepositoryMaintenanceContract.cs — Shared assertions of RepositoryMaintenanceTests: the retention of IRepositoryMaintenanceService on the history and the log (ADR-027), the oldest rows first, a cancel and a failure after a deleted batch
│   ├── RepositoryPrefixContract.cs — Shared assertions of RepositoryPrefixTests: a second repository with another table prefix beside a migrated one
│   ├── RepositorySchemaServiceContract.cs — Shared assertions of RepositorySchemaServiceTests: create a repository from its options, then find it current
│   ├── RunInitiatorValues.cs       — Initiator values a scenario passes to the engine (source, user, subject, host)
│   ├── ScenarioBuilder.cs          — Fluent API for test scenario configuration
│   ├── ScenarioContext.cs          — Execution + assertions
│   ├── SqlDialect.cs               — Engine-specific SQL helpers (`GetErrorSql`, `GetBrokenRollbackSql`, `GetCreateSimpleTableSql`, `GetDropSimpleTableSql`, `GetTomlHeader`, `GetSleepSql`, and `JoinBlocks`, `GetInsertIntoTableASql`, `GetDeleteFromTableCSql` for the multi-block files of the cancellation tests)
│   ├── SqliteTestBase.cs           — SQLite base class
│   └── SqlServerTestBase.cs        — SqlServer base class
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
    ├── MigrateUp/                  — 52 test class files (10 PostgreSQL base + 12 SqlServer + 10 MariaDb + 10 MySql + 10 Sqlite)
    ├── MigrateDown/                — 15 test class files (3 per engine)
    ├── Compound/                   — 10 test class files (2 per engine)
    ├── Features/                   — 153 test class files (29 PostgreSQL + 31 SqlServer + 30 MariaDb + 30 MySql + 32 Sqlite + 1 shared P2 file `P2_MySqlMariaDbSessionTimezoneTests.cs` containing one MySQL and one MariaDB class)
    └── CliTool/                    — 17 test class files (4 engines × 4 classes [File, Stdin, Docker raw, ExitCodeRange] + 1 PresetConsistency; no Sqlite)
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
- `WithTargetMigrationOrder(order)` — sets execution order (FileByFile/TargetByTarget)
- `WithMultiTarget(connectionString)` — enables multi-target mode (adds a second target to the Backend target group)
- `WithTargetGroup(alias, databaseType, connectionString, order?, hashScope?)` — adds an additional target group (used for multi-target-group tests)
- `WithCliTool(alias, executablePath, argumentTemplate, inputMode, timeoutInSeconds?, successExitCodes?)` — registers a CLI tool in the product configuration
- `WithUseCliToolAlias(alias)` — sets the `UseCliToolAlias` on the product so all migrations use the registered CLI tool
- `WithCliToolParameters(parameters)` — sets named placeholder parameters for CLI tool argument template substitution
- `WithFlatLayoutForRelease(release)` — moves migration files for a release from the `Backend/` subdirectory to the release root (flat layout), enabling mixed flat/traditional layout tests
- `WithTargetGroupMigrationOrder(commaSeparated)` — sets `TargetGroupMigrationOrder` at the product level in appsettings (comma-separated string, e.g. `"Frontend,Backend"`)
- `SetMigSettings(relativeFilePath, entries)` — creates or updates a migsettings.txt file in the work directory
- `WithDatabaseLogging(minimumLevel?)` — enables database log sink for the test run (default level: Debug)
- `WithDatabaseLoggingInto(connectionString, minimumLevel?)` — enables the database log like `WithDatabaseLogging`, but into the database of `connectionString` with the schema and prefix of the repository: a log of its own beside the repository, which the maintenance service skips (`RepositoryMaintenanceTests`); the builder drops the log tables of that database before the scenario starts, and a scenario with a table prefix drops them again when it ends
- `WithTargetMaxRetries(maxRetries, retryDelayMs?)` — sets `DbCommandMaxRetries` and `DbCommandWaitTimeInMsBeforeRetry` on the primary target (used for retry/atomic shared-connection tests)
- `WithTargetCommandTimeout(seconds)` — sets `DbCommandTimeoutInSeconds` on the primary target
- `WithTargetConnectionString(connectionString)` — replaces the connection string of the main target while the repository keeps the fixture's, so that the file runs block by block instead of on the atomic shared connection (the cancellation and pending tests use the second database of the fixture); `BuildAsync` cleans such a target before the scenario starts, without the table prefix, and swallows the errors of that cleanup, so an unreachable connection string proves that a command never connects to its targets (#6)
- `WithTableBaseName(tableBaseName)` — sets the table prefix (`Repository.TableBaseName`, and `DatabaseLogging.TableBaseName` when the logging section is written); the cleanup before the run and the query helper of the scenario use the same prefix, and the scenario drops the repository and log tables of its prefix again when it ends or when its host fails to build (`PrefixTableCleanup`, in the repository database and in a log database of its own)
- `WithMigrationFilesEncoding(encodingName)` — writes `ProductDefaults.MigrationFilesEncoding`; without the call the key is left out and the engine's default UTF-8 applies, so every scenario exercises the default
- `WithSecondProduct(alias, broken?)` — adds a second product to the configuration file, sound (the main target, database type and migration files of the scenario) or broken (a target without a connection string, a target group of the database type `NoSuchDb`, and a migration files directory written as the `{ENV:...}` placeholder of the builder's `UnsetVariableName`, which no test sets unless it wants a directory that does not exist); the host still runs for `EngineTest` and must not notice a broken second product (ADR-026)
- `ForProduct(alias)` — builds the host for another product of the configuration than `ScenarioBuilder.MainProductAlias` (`EngineTest`), for example the broken one; a host that fails to build is disposed

### ScenarioContext — Execution + Assertions

**Execution:**
- `MigrateUpAsync(toRelease?, allowOutOfOrder?, targetGroupAliases?, runMode?, targetGroupMigrationOrder?, cancellation?)` — executes MigrateUp; `cancellation` is the `RunCancellation` a test cancels the run with, the call itself gets the test's token
- `MigrateUpWithTokenAsync(cancellationToken, toRelease?)` — executes MigrateUp with `cancellationToken` as the token of the call and no `RunCancellation`, so that a cancel of the token alone stops the run with `Terminate`
- `MigrateDownAsync(toRelease, targetGroupAliases?, runMode?, revealSensitiveData?, cancellation?)` — executes MigrateDown
- `BaselineAsync(toRelease?, targetGroupAliases?, targetGroupMigrationOrder?, revealSensitiveData?, cancellation?)` — executes Baseline
- `ValidateHashAsync(scope?, targetGroupAliases?)` — executes ValidateHash
- `UpdateHashAsync(targetGroupAliases?, cancellationToken?)` — executes UpdateHash with `cancellationToken`, or the test's token without one
- `InfoAsync()` — executes the `info` command (returns `MigrationStatusInfo`)
- `GetHistoryAsync(limit?)` — fetches the run history (returns `MigrationHistory`)
- `FixIssuesAsync(scope?, olderThanMinutes?, assumedMigrationStatus?, cancellationToken?)` — executes the `fix` command in the host's run mode (rebuild with `MigrationRunMode.Simulate` for a preview), with `cancellationToken`, or the test's token without one
- `InsertOrphanedMigrationRun(minutesOld?)` — inserts a fake `MigrationRun` record with Running status and a `StartedAt` in the past (helper for Fix tests)
- `RebuildForAsync(command, mode, toRelease?)` — rebuilds DI container for next phase (no DB cleanup)
- `UseProgressObserver(observer)` — sets the progress observer of the host's context and of every context a later rebuild creates; the cancellation tests cancel from a `CancellingProgressObserver` at an exact progress event
- `UseLogObserver(observer)` — sets an observer of the host's log events and of every host a later rebuild creates; it runs on the thread that logs, before the logging call returns, so that a test cancels update-hash or fix, which report no progress events, at an exact log line
- `InitDatabaseLoggerAsync()` — initializes the database log once more, as the next start of a host does (`DatabaseLogging_CheckCreate`); true when it created the log tables, false when they existed

**Assertions:**
- `AssertSuccess(bool)` — checks operation result
- `AssertRunResult(MigrationRunResult)` — checks latest MigrationRun.MigrationRunResultId
- `AssertRunCount(int)` — checks total MigrationRun count
- `AssertFileStatus(filename, MigrationStatus)` — checks MigrationStatusId
- `AssertFileStatuses(params ...)` — bulk file status check
- `AssertFileStatusForTarget(filename, target, status)` — multi-target status check
- `AssertMigrationRecord(filename, expectation)` — checks selected MigrationRecord columns via `MigrationRecordExpectation` (partial match)
- `AssertMigrationRun(runIndex, expectation)` — checks selected MigrationRun columns via `MigrationRunExpectation` (partial match)
- `AssertTableExists(tableName, bool)` — checks user table existence on primary connection
- `AssertTableExistsOnConnection(connectionString, tableName, bool)` — checks user table existence on specific connection
- `AssertRowCount(tableName, int)` — checks user table row count
- `AssertRepositoryTableExists(tableName, bool)` — checks repository table existence
- `AssertProductExists(bool)` — checks that the Product record exists in the repository
- `AssertEnvironmentExists(environmentName, bool)` — checks that an Environment record exists (case-insensitive via NameLower)
- `AssertTargetGroupMigrationOrder(params string[])` — checks that MigrationRecord rows appear in the repository in the expected target group order (by ascending Id)

**Query helpers (for custom assertions):**
- `GetMigrationRunSettingsJson()` — returns the latest MigrationRun's settings JSON
- `GetMigrationConfigJson(filename)` — returns the stored config JSON for a specific migration file
- `CountMigrationHistory()` — counts rows in the MigrationRecordHistory archive table
- `GetMigrationHistory(filename)` — returns the history rows of a file in order: the status each recorded and the operation that caused it
- `GetRepositoryIndexColumns(indexName)` — returns the columns of a repository index in key order, empty when the index does not exist
- `GetRepositoryLookupName(tableName, id)` — returns the name of a lookup row, for example `Cancelled` for id 70 of `MigrationRunResult`
- `GetMigrationRecord(filename)` — returns the `MigrationRecord` row of a file, null when the file has none
- `GetLogMessages(logLevelId)` — returns the messages of the log entries at one level in the order they were written, each with the exception text the database sink appends (call `FlushDatabaseLog` first); the cancellation tests check with it that the warnings of a cancel carry no stack trace
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

**Disposal:** `DisposeAsync` disposes the host first, which flushes the database log into the tables the cleanup drops. A scenario with a table prefix then drops the repository and log tables of its prefix (`PrefixTableCleanup`); the cleanup never throws, because an exception there would replace the one of a failing test, and a table that stays is dropped by the next scenario with the prefix before it starts. Last, the work directory is deleted. A scenario without a prefix keeps its tables for inspection until the next scenario cleans them before it starts.

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

The catalog lists the unique test scenarios defined for the base PostgreSQL test classes. Each scenario is replicated across all supported engines (SqlServer, MariaDb, MySql, Sqlite) by engine-specific test classes with the same test methods (with the exceptions noted in the Features table — SqlServer-only block-level and atomic-shared-connection scenarios, and Sqlite-only datetime-CHECK-constraint scenarios).

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
| #32 | MT-1 | FileByFile_Ignore_SkipsSecondTarget | MultiTargetTests |
| #33 | MT-2 | FileByFile_Rollback_BothTargets | MultiTargetTests |
| #34 | MT-3 | TargetByTarget_Terminate_SecondTargetNeverStarts | MultiTargetTests |
| #35 | MT-4 | TargetByTarget_Rollback_BothTargets | MultiTargetTests |
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

**Features tests** (all 5 engines have 28 base feature test classes each; SqlServer, PostgreSQL, MariaDb and MySql add the DAL timeout class, SqlServer the atomic-shared-connection and killed-pooled-session classes, MariaDb and MySql the DAL user lock class, Sqlite the datetime-CHECK-constraint, command-profile, file-encoding and CLI signal classes, plus a P2 file shared between MySql and MariaDb):

| Class | Tests | Description |
|-------|-------|-------------|
| `MigrationHistoryTrackingTests` | 5 | MigrationRecordHistory records written inline on terminal status transitions (simulate, first run, failed retry, down-then-up, baseline). Source file: `ArchiveRetentionTests.cs`. |
| `BaselineTests` | 10 | Baseline marking, incremental baseline, baseline then MigrateUp |
| `DatabaseLogTests` | 8 | Log entries written after MigrateUp, multiple log levels, logs during error, log tables created, `MigrationEventId` stored; the indexes of `DatabaseLogging_CheckCreate` (`Infrastructure/DatabaseLogIndexContract.cs`): `MigrationRunId` and `CreatedAt` on a fresh log table, added to the log table of an older engine by the next initialization, which still reports that the log existed, and named with the longest table prefix in full |
| `DatabaseLogDefaultTests` | 3 | Database logging into the repository by default at Information, a section with only MinimumLevel, `Enabled: false` creates no log tables |
| `ReadModelContractTests` | 1 | After a migrate-up every repository table reads through `RepositoryNaming.SelectStatement` via the engine DAL and maps with `RepositoryRowMapper`: schema version, run, records, lookups, log rows, UTC timestamps (`Infrastructure/ReadModelContract.cs`) |
| `FixTests` | 9 | Fix command: no orphans, fix orphan, simulate, older-than filter, fix-then-migrate, multiple orphans, details, assumed-status=Migrated, scope all |
| `InfoTests` | 23 | Info command: fresh repo, full/partial migration, baseline, target groups, error state, a modified file counted as pending, multiple-run history, run details, the history of a migrate-down and of a rolled back migrate-up, the runs of the requested environment only; the pending list (`Infrastructure/PendingMigrationsContract.cs`): every file `NotMigrated` on a fresh repository, the remaining files after a partial run, `Failed`, `Resumable` after a cancel in a file, `RunAlways` after every run, `HashMismatch`, a modified file of an older release listed although migrate-up refuses it out of order, the pending pairs after a failed second target in both `TargetMigrationOrder`s, the target groups in their `TargetGroupMigrationOrder`, and per target group the pending count and `LastMigrationDate`; each scenario then runs the next migrate-up and compares the files it starts with the list |
| `MigrationRunMetaTests` | 8 | MigrationRunSettingsJson content (console options, product, masked credentials, MigrateDown masking, Baseline masking), `FromReleaseVersion` as the highest migrated release when the run started |
| `MigSettingsInheritanceTests` | 13 | migsettings.txt inheritance (root, release, target group, TOML override) |
| `OutOfOrderBlockingTests` | 3 | Out-of-order migration blocking (false=blocked, true=allowed, data integrity after blocking) |
| `RepositoryIntegrityTests` | 4 | Repository tables, Product record, Migration records after successful run |
| `HeartbeatTests` | 6 | The run heartbeat: a long block refreshes `LastHeartbeatAt`, an open run with a fresh heartbeat blocks a new start however old its start, a stale heartbeat is auto-fixed, `fix` reports and filters by heartbeat age, the orphan fix leaves an alive run open, a heartbeat younger than the threshold stays alive across a minute boundary |
| `MigrationRunDetailsTests` | 6 | Run details of schema version 2: operation, source, initiator and host from the console options, NULL source and the machine name without them, the error message of a failed run, migrate-down and baseline operations, the history DTO, and the upgrade of a version 1 repository by the next command |
| `RepositorySchemaTests` | 5 | Versioned repository schema (ADR-024): fresh repository gets every step and the engine version, second run changes nothing, a newer repository and one without a schema version are refused before a schema step runs or a row is written, parallel start on an empty repository creates it once |
| `RepositorySchemaServiceTests` | 1 | `IRepositorySchemaService.EnsureAsync` from the repository options alone, without product, environment or run: a fresh repository gets one `RepositorySchemaVersion` row per step, one `MigratorMeta` row and the `MigrationRecordHistory` table; a second call finds the current version and changes nothing (`Infrastructure/RepositorySchemaServiceContract.cs`). The class name carries the engine prefix on every engine (`PostgreSqlRepositorySchemaServiceTests`, `SqlServerRepositorySchemaServiceTests`, ...) |
| `RepositoryPrefixTests` | 3 | A migrate-up against a repository with table prefix `a_` (`WithTableBaseName`), then a second repository with prefix `b_` created through the schema service in the same database or schema; the second repository is complete and empty and the first keeps its run, because every index and constraint name carries the prefix (`Infrastructure/RepositoryPrefixContract.cs`). A disposed scenario with a prefix leaves no table of its prefix, with the log in the repository and in a database of its own (`Infrastructure/PrefixTableCleanupContract.cs`); the test reads the catalog by the prefix, not the table list of `DatabaseCleanupHelper`, so it also fails for a table of the prefix that the cleanup does not know. Engine-prefixed class names as above |
| `CancellationTests` | 23 (SQLite 21) | The cancellation of a run (ADR-028, `Infrastructure/CancellationContract.cs`); the cancel comes from a `CancellingProgressObserver` at an exact event, or for update-hash and fix from a log line (`UseLogObserver`): `Terminate` leaves the interrupted file `Failed` with its committed blocks for the next migrate-up to resume (FileByFile and TargetByTarget); `RollbackFile` rolls back the interrupted file only, `RollbackRun` every file of the run and `RollbackRelease` the interrupted file and the files of its release, newest first, while the release before stays; `RollbackFile` without a rollback file ends `Error` with `CancelledRollbackIncomplete` (1005); a cancel through the token alone terminates; on the atomic shared connection nothing of the file commits and the next run executes it whole; a cancel between files starts no further file; in FileByFile order a cancel between the two targets of a file counts the file as finished in the result and in `RunCompletedEvent`, and `RollbackFile` has nothing to roll back; a handle cancelled before the call ends `Cancelled` with `MigrationRunId` 0 and nothing to roll back, and a migrate-down cancelled the same way stops before its first rollback; Simulate and Validate stop the same way and persist nothing; a cancel while a CLI tool runs kills the tool (not on Windows); migrate-down stops before the next rollback block and the next migrate-down continues; after a migrate-down cancelled in the middle of a rollback, info lists the file `RollbackIncomplete` with its rollback blocks, the next migrate-up refuses before its first file, the next migrate-down continues the rollback and the migrate-up after it runs the release again; a cancel during the error recovery stops it and ignores the action, and the run stays `Error` with the code of the failure and a message that names the failure first (result, run row and `RunCompletedEvent`); a cancelled baseline keeps the records it wrote; update-hash cancelled between the two records of a file counts that file, and fix cancelled between two orphaned runs keeps the run it closed; schema version 2 is upgraded to 3 by the next command (lookup row `Cancelled`, four indexes), and every record, run and history row of a migrated repository reads the same afterwards; a cancel inside a sleeping block ends the sleep within seconds (not on SQLite, which checks only before a block): SQL Server and PostgreSQL abort it, the `KILL QUERY` of MySQL and MariaDB ends it early without an error; on the atomic path nothing of the file commits, on a target of its own the record keeps the first block, and on MySQL and MariaDB the ended sleep as well; the warnings of a cancel carry no stack trace. Engine-prefixed class names as above |
| `DalCancellationTests` | 2 | The shared-connection calls of the DAL (`ExecuteNonQueryAsync`, `ExecuteScalarAsync` on the caller's open connection and transaction, as the atomic path uses them) hand the caller's token to the command: a token cancelled before the call is reported by the provider, without a failure of the statement (`Infrastructure/DalSharedConnectionCancellationContract.cs`). The unit tests cannot show this for Npgsql and MySqlConnector, which check the connection before the token. Engine-prefixed class names as above |
| `DalTimeoutTests` | 1 (not SQLite) | A command that runs into its command timeout of one second is a timeout for `DalBase.IsTimeout` (SqlClient error -2, a `TimeoutException` from Npgsql, `CommandTimeoutExpired` of MySqlConnector), and another failure is not; MySQL and MariaDB run a long query instead of `SLEEP()`, which `KILL QUERY` ends without an error (`Infrastructure/DalTimeoutContract.cs`). SQLite has no command timeout for a running statement. Engine-prefixed class names |
| `DalUserLockTests` | 1 (MySQL and MariaDB only) | A call that fails after its `GET_LOCK` leaves no lock behind: a session of its own outside the pool of the DAL finds the lock free (`IS_FREE_LOCK`), because the DAL runs `DO RELEASE_ALL_LOCKS()` before the connection goes back to the pool (`Infrastructure/DalUserLockContract.cs`). Engine-prefixed class names |
| `RepositoryMaintenanceTests` | 14 (SQLite 15) | The retention of `IRepositoryMaintenanceService.CleanUpAsync` (ADR-027, `Infrastructure/RepositoryMaintenanceContract.cs`): a cutoff in the future deletes the history and the log and keeps the runs and records, so status and the next migrate-up do not change; an open run keeps its rows until it is closed; log rows without a run fall by their age; a cutoff between two runs deletes the rows of the older run only; a dry run counts the rows and deletes nothing; a batch size of one takes one batch per row; a time budget of zero stops after one batch and the next call finishes; a cleanup that stops early has deleted the oldest rows (the rows are aged in the order they were written, the order of the clustered key in which SQL Server deletes); a repository without log tables and a log in a database of its own (`WithDatabaseLoggingInto`) are skipped and left alone; the longest table prefix; a cancel between two batches returns the rows deleted so far, a cancel at the check of the log table returns the deleted history without throwing, a cancel in a dry run throws `OperationCanceledException`; on SQLite, a batch that fails after a deleted batch (another connection holds the write lock beyond the command timeout of one second) is not retried, and the call returns what it deleted with `Completed = false` and the error in the message. The scenarios age rows with UPDATEs instead of waiting. Engine-prefixed class names as above |
| `ProductIsolationTests` | 2 | The product scope of a run (ADR-026): the configuration carries a second product `Broken` whose target has no connection string, whose target group names the database type `NoSuchDb` and whose directory is a placeholder of a variable that is not set (`WithSecondProduct(alias, broken: true)`); migrate-up and info of `EngineTest` succeed beside it, and a host built for `Broken` (`ForProduct`) fails on the broken product alone: on the unset variable first (`ApplicationStartupException`), and once the test sets the variable to a directory that does not exist, on the data annotations, which name `Products[Broken]`, the connection string and the directory (`Infrastructure/ProductIsolationContract.cs`). Engine-prefixed class names as above |
| `ProgressEventsTests` | 8 | Typed progress events through `MigrationContext.ProgressObserver`: run, file and block events of migrate-up, simulated outcomes and the start release without a run row, rollbacks and skipped records of migrate-down, baselined files without blocks, the failed file and run, and an observer that throws does not abort the run |
| `ParameterRoundTripTests` | 2 (PostgreSQL 3) | A parameter value with quotes, backslashes, `$$`, other placeholders, comment and statement markers and non-ASCII text comes back unchanged through the DAL; PostgreSQL also inside a `DO` block |
| `RunningGuardTests` | 6 | Concurrent run prevention (MigrateUp, MigrateDown, Baseline blocked; ValidateHash/UpdateHash allowed) |
| `SimulateModeTests` | 14 | Simulate mode (no user tables written), Validate mode, MigrateDown simulate, MigrateDown Validate (non-destructive + missing rollback detection), Simulate reads the same records as Migrate: preview of pending files only, nothing pending after a full migrate, rollback candidates, empty repository, an environment that is not registered yet, for which nothing is inserted (#7) |
| `TargetGroupMigrationOrderTests` | 10 | TargetGroupMigrationOrder overrides (CLI, appsettings, migsettings), wrong-case error, Baseline, single-TG rejection |
| `TargetGroupFilterTests` | 12 | Filtering by target group alias (Backend-only, Frontend-only, both) |
| `UpdateHashTests` | 9 | update-hash: no updates after fresh migration, update after file modification, validate passes after update, idempotent second run, empty-repo no-op, files without TOML header, two targets (the record of every target updated, a deleted file counted once) |
| `ValidateHashTests` | 12 | Hash validation in File and SqlBlocks scopes, detecting modifications, Disabled scope (ignores modification, passes after full migration), the host in RunMode Validate as the CLI builds it (unchanged, modified, deleted and unmigrated files, #5) |
| `SqlServerAtomicSharedConnectionTests` | 2 | SqlServer-only: atomic shared-connection path — transient error triggers file-level retry with full transaction rollback (ASC1), permanent error rolls back all blocks including prior DDL (ASC2) |
| `SqlServerKilledPooledSessionTests` | 1 | SqlServer-only: a migration file that alters the login RayMigrator uses invalidates the pooled sessions (errors 4021 and 596); the DAL retries, clears the pool and the repository update succeeds on a fresh connection |
| `SqliteDatetimeCheckConstraintTests` | 3 | SQLite-only: DAL-021 strict ISO-8601 CHECK constraints on repository datetime columns (rejects bad input, accepts `datetime('now')`, allows NULLs on nullable columns) |
| `SqliteCommandProfileTests` | 18 | SQLite-only, database independent (#6): read-only commands start against an unreachable target and write no log, product or environment rows; state changing commands log; baseline runs appear as `MigrationOperation.Baseline` in the history |
| `SqliteFileEncodingTests` | 7 | SQLite-only (#4): migration files are decoded strictly with `MigrationFilesEncoding`; an invalid byte aborts discovery and names file and encoding, a byte-order mark decides the encoding, `migsettings.txt` is always UTF-8; a scenario without the setting reads UTF-8 and naming `UTF-8` afterwards keeps every hash valid (0.19.0 default) |
| `SqliteCliSignalTests` | 1 | SQLite-only, Linux and macOS (the test sends a POSIX signal): the real `raymigrator` process runs a migrate-up and gets SIGINT when its console reports the second file, while the seed file runs a block of several seconds; the process exits with `130`, the run row is `Cancelled`, which only the handler of the CLI records (a process that the signal killed would leave the run open), the seed file is not `Migrated`, and the console shows the warning of the cancel |
| `MySqlSessionTimezoneTests` / `MariaDbSessionTimezoneTests` | 2 + 2 | DAL-014 session time-zone enforcement: every DAL execute method issues `SET time_zone = '+00:00';` after opening a connection; `DateTime.UtcNow` round-trips through `TIMESTAMP` columns with millisecond fidelity. Defined together in `P2_MySqlMariaDbSessionTimezoneTests.cs`. |

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

Tests verify the following database columns via `MigrationRecordExpectation` and `MigrationRunExpectation` (partial-match DTOs — only non-null fields are asserted):

**MigrationRun table** (`MigrationRunExpectation`):
- `MigrationRunResultId` (Ok=100, Error=90, Recovered=80, Cancelled=70, PartialSuccess=50)
- `EnvironmentId` (FK to Environment lookup — typically resolves to "Docker")
- `FromReleaseVersion`, `ToReleaseVersion`

**MigrationRecord table** (`MigrationRecordExpectation`):
- `MigrationStatusId` (Migrated=100, NotMigrated=50, Failed=30)
- `MigrationOperationId` (MigrateUp=100)
- `EnvironmentId`, `ReleaseVersion`, `TargetGroupAlias`, `TargetAlias`
- `FileOrderId` (1-12, sequential)
- `FileUpBlocksMigrated`, `FileUpBlocksTotal` (block-level execution tracking)
- `MigrateDownFileExists` (set at MigrateUp discovery time)
- `FileDownBlocksMigrated`, `FileDownBlocksTotal` (rollback block tracking)

### Block Count Semantics

- **Non-Ignore mode, error file**: `FileUpBlocksMigrated` = the blocks that committed before the failing one (0 when the first block fails, and on the atomic shared connection, whose transaction rolls the whole file back) (#11)
- **Ignore mode, error file**: `FileUpBlocksMigrated` = `FileUpBlocksTotal` (all blocks attempted; DB record updated after every block attempt)
- **Cancelled file**: status `Failed` and `FileUpBlocksMigrated` = the blocks that committed before the cancel, so the next migrate-up resumes with the first block that did not commit (on the atomic shared connection only those of an earlier attempt, normally 0; `FileUpBlocksTotal` in Ignore mode, as after an error there, so the next run executes the whole file)
- **Rollback cancelled in the middle**: status `Failed` with `0 < FileDownBlocksMigrated < FileDownBlocksTotal`; the next migrate-up refuses to start, the next migrate-down continues with rollback block `FileDownBlocksMigrated + 1`
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

**MigrateUp vs MigrateDown**: This matrix applies to the ROLLBACK portion of both operations. For MigrateUp with automatic rollback (triggered by `MigrationErrorAction=Rollback`), the migration itself failed, so the run never ends `Ok`: it is `Recovered` when the rollback chain completed without a failure or warning, and `Error` otherwise (#18). A migrate-up cancelled with `RollbackFile`, `RollbackRun` or `RollbackRelease` runs the same chain and ends `Cancelled` when it completed without a failure or warning, `Error` with `CancelledRollbackIncomplete` (1005) otherwise ([ADR-028](../arc42/09-Architecture-Decisions.md#adr-028-a-run-cancels-at-the-next-block-boundary-with-a-migrationcancelledaction-and-records-cancelled)). For MigrateDown, the rollback IS the operation, so the matrix directly determines the result.

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
