# Engine Tests

Comprehensive integration tests for the RayMigrator migration engine, covering all combinations of MigrationErrorAction, RollbackErrorAction, RequireRollbackFile, TargetMigrationOrder, and multi-phase workflows.

## Overview

The engine test project (`Raycoon.RayMigrator.Tests.Engine`) tests the core migration pipeline — MigrateUp, MigrateDown, rollback behavior, error handling, and recovery. It uses a **single unified set of migration files per database engine**, with test-specific modifications applied at runtime via the `ScenarioBuilder` fluent API.

| Metric | Value |
|--------|-------|
| Total tests | ~1769, every theory row counted (1732 test methods across 257 test class files, including the engine-agnostic `CliToolPresetConsistencyTests` with 16 inline cases; 10 are skipped on purpose) |
| PostgreSQL tests | ~322 (50 test classes; most use unprefixed filenames as the "base" engine) |
| SqlServer tests | ~333 (54 test classes, includes 6 block-level tests, 3 resume tests, 12 CLI tool tests, 2 atomic shared connection tests and 1 killed pooled session test) |
| MariaDb tests | ~326 (52 test classes; one is `MariaDbSessionTimezoneTests` defined alongside `MySqlSessionTimezoneTests` in `P2_MySqlMariaDbSessionTimezoneTests.cs`) |
| MySql tests | ~326 (52 test classes; one is `MySqlSessionTimezoneTests` defined in the same shared P2 file) |
| Sqlite tests | ~336 (49 test classes; includes the SQLite-only `SqliteDatetimeCheckConstraintTests`, `SqliteCommandProfileTests`, `SqliteFileEncodingTests` and `SqliteCliSignalTests`) |
| Backend migration file sets | 5 engines x 24 files = 120 files |
| Frontend migration file sets | 5 engines x 16 files = 80 files |
| Database engines | PostgreSQL, SQL Server, MariaDB, MySQL, SQLite |

> **Note**: PostgreSQL is treated as the "base" engine — most of its test classes have unprefixed filenames (`HappyPathTests.cs`, `RollbackTests.cs`, etc.) and live in the same `Tests.Engine` namespace; the CLI tool classes and the nine feature classes that run a shared contract (`PostgreSqlRepositorySchemaServiceTests`, `PostgreSqlRepositoryPrefixTests`, `PostgreSqlProductIsolationTests`, `PostgreSqlCancellationTests`, `PostgreSqlBlockProgressTests`, `PostgreSqlOrphanRepairTests`, `PostgreSqlRepositoryMaintenanceTests`, `PostgreSqlDalCancellationTests`, `PostgreSqlDalTimeoutTests`) carry the engine prefix like the other engines; `InfoTests`, `DatabaseLogTests` and `ReadModelContractTests` run a shared contract too but keep the unprefixed name. Each unprefixed class is replicated by engine-specific wrapper classes (e.g. `SqlServerHappyPathTests`, `MariaDbHappyPathTests`, `MySqlHappyPathTests`, `SqliteHappyPathTests`). The lone exception is `P2_MySqlMariaDbSessionTimezoneTests.cs`, which contains two engine-specific classes (one for MySQL, one for MariaDB) and runs against neither PostgreSQL nor SqlServer.

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
│   ├── BlockProgressContract.cs    — Shared assertions of BlockProgressTests: the block counters of a record count the leading blocks that committed at every moment of a run, read in the flow of the run: 0 while a CLI tool runs, the blocks before the first failure with MigrationErrorAction=Ignore, also at the end of the file and after a cancel, so the next migrate-up resumes at the failed block, the same in a rollback with RollbackErrorAction=Ignore until it ends, the committed blocks of a resumed file and of a continued rollback from their start, a cancel after an ignored failed rollback block that keeps the rollback blocks before it, a failed atomic rollback and a failed rollback through a CLI tool that keep the rollback blocks of the earlier attempt, a file of a CLI tool that does not resume an earlier attempt, and a rollback through a CLI tool that does not continue one
│   ├── CancellationContract.cs     — Shared assertions of CancellationTests: a cancelled run stops at the next block boundary and takes the MigrationCancelledAction of the test; migrate-down, baseline and a running error recovery stop and ignore it; the run is recorded as Cancelled (70), the lookup row of schema step 3, a migrate-up whose error recovery the cancel stopped as Error with its failure; a migrate-up refuses a file whose rollback stopped in the middle; update-hash and fix return what they did so far; the warnings of a cancel carry no stack trace
│   ├── CancellingProgressObserver.cs — Records the progress events like RecordingProgressObserver and cancels the run once, at the first event that matches its trigger (after a block, after a file, when a file starts), so the cancellation tests need no timing
│   ├── CliToolConfigHelper.cs      — Pre-built CLI tool configs for CliTool tests (File and Stdin modes)
│   ├── DalSharedConnectionCancellationContract.cs — Shared assertions of DalCancellationTests: the shared-connection calls of a DAL hand the caller's token to the command on an open connection and transaction, so the provider reports a token cancelled before the call
│   ├── DalTimeoutContract.cs       — Shared assertions of DalTimeoutTests: a command that runs into its command timeout is a timeout for DalBase.IsTimeout, however the provider reports it
│   ├── DalUserLockContract.cs      — Shared assertions of DalUserLockTests (MySQL, MariaDB): a call that fails after its GET_LOCK leaves no lock on the pooled session
│   ├── DatabaseLogIndexContract.cs — Shared assertions of the log index tests in DatabaseLogTests: DatabaseLogging_CheckCreate gives MigrationLog an index on MigrationRunId and one on CreatedAt, also on a log table of an older engine; hosts that start at the same moment against a log database without log tables all initialize the log, and the event catalogue holds each event once
│   ├── DockerExecHelper.cs         — Static Process wrapper for docker exec -i stdin piping
│   ├── EngineConfig.cs             — Engine configuration record
│   ├── EngineTestHost.cs           — DI container (mirrors Program.cs setup), built asynchronously because it initializes the database log writer (InitDatabaseLoggerAsync); loads the scenario's appsettings.json through JsonOptionsSource, so the host runs in the product scope of a run (ADR-026), stops on a placeholder whose variable is not set, names a failed element by alias and hands the rule catalog the section as written, like DirectModePipeline; hands every log event to the LogObserver of the test, on the logging thread before the call returns
│   ├── MariaDbTestBase.cs          — MariaDB base class
│   ├── MigrationRecordExpectation.cs  — Partial-match DTO for MigrationRecord table
│   ├── MigrationRunExpectation.cs     — Partial-match DTO for MigrationRun table
│   ├── MySqlTestBase.cs            — MySQL base class
│   ├── OrphanRepairContract.cs     — Shared assertions of OrphanRepairTests: the orphan repair (the auto-fix of the next start, or fix) settles every open record of a run that ended abruptly by its progress: Failed with the committed blocks, Migrated with every block, NotMigrated without progress, a rollback Failed with its rollback blocks or NotMigrated when every rollback block ran; FinishedAt set and DurationInMs NULL for a file that was running, the end and the duration of the last terminal status kept for a rollback that was running; the rule of the template is the rule of the projection of the status; the run closed as Error with the engine code 1006 and a message that names its last heartbeat in UTC; the open run and the open record are written by SQL
│   ├── ParameterRoundTripValues.cs — Values the parameter round-trip tests send through a template parameter
│   ├── PendingMigrationsContract.cs — Shared assertions of the pending list in InfoTests: MigrationStatusInfo.PendingFiles in the order and with the reason of the next migrate-up, which every scenario then runs and compares with the list
│   ├── PostgreSqlTestBase.cs       — PostgreSQL base class
│   ├── PrefixTableCleanup.cs       — The tables of a scenario's table prefix, which ScenarioContext.DisposeAsync drops: the repository and log tables of the prefix in the repository database and in a log database of its own
│   ├── PrefixTableCleanupContract.cs — Shared assertions of RepositoryPrefixTests: a disposed scenario with a prefix leaves no table of its prefix in the catalog
│   ├── ProbingProgressObserver.cs  — Records the progress events like RecordingProgressObserver and hands each one to a probe in the flow of the run, so a test can read the repository at an exact event
│   ├── ProductIsolationContract.cs — Shared assertions of ProductIsolationTests: a run of the main product beside a broken second product, and the broken product failing on its own findings
│   ├── ReadModelContract.cs        — Shared assertions of ReadModelContractTests: every repository table reads through the read model package
│   ├── RecordingProgressObserver.cs — Records the progress events of a run in order (ProgressEventsTests)
│   ├── RegistrationContract.cs     — Shared assertions of the first registrations in RepositoryIntegrityTests: hosts that start the first run of a new product or environment at the same moment register its name once and all get the Id of its row; eight starts of their own run the CheckInsert template on DALs of their own in each of five rounds (EngineTestHost.RegisterWithNewDalAsync)
│   ├── RepositoryMaintenanceContract.cs — Shared assertions of RepositoryMaintenanceTests: the retention of IRepositoryMaintenanceService on the history and the log (ADR-027), the oldest rows first, a cancel and a failure after a deleted batch
│   ├── RepositoryPrefixContract.cs — Shared assertions of RepositoryPrefixTests: a second repository with another table prefix beside a migrated one
│   ├── RepositorySchemaServiceContract.cs — Shared assertions of RepositorySchemaServiceTests: create a repository from its options, then find it current
│   ├── RunAlwaysContract.cs        — Shared assertions of the RunAlways tests: a RunAlways file runs in full from block 1 after an attempt that committed blocks (a failure, a cancel, a record on the atomic shared connection, the open record of an orphaned run), neither resumed nor finalized
│   ├── RunInitiatorValues.cs       — Initiator values a scenario passes to the engine (source, user, subject, host)
│   ├── RunLockContract.cs          — Shared assertions of the parallel starts in RunningGuardTests: hosts that start a run of one product and environment at the same moment take the run lock once; eight starts of their own run Repository_MigrationRun_Insert on DALs of their own in each of ten rounds (EngineTestHost.InsertMigrationRunWithNewDalAsync)
│   ├── ScenarioBuilder.cs          — Fluent API for test scenario configuration
│   ├── ScenarioContext.cs          — Execution + assertions
│   ├── SqlDialect.cs               — Engine-specific SQL helpers (`GetErrorSql`, `GetBrokenRollbackSql`, `GetCreateSimpleTableSql`, `GetDropSimpleTableSql`, `GetTomlHeader`, `GetSleepSql`, and `JoinBlocks`, `GetInsertIntoTableASql`, `GetDeleteFromTableASql`, `GetDeleteFromTableCSql` for the multi-block files of the cancellation tests)
│   ├── SqliteTestBase.cs           — SQLite base class
│   ├── SqlServerTestBase.cs        — SqlServer base class
│   └── StartedFileRollbackContract.cs — Shared assertions of StartedFileRollbackTests: migrate-down rolls back a file that stopped after committed blocks (a cancel, a failure, ignored failed blocks, the orphan repair) from its first rollback block, before the files it came after, and names a file that stopped without a committed block in a warning
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
- `FixIssuesAsync(scope?, olderThanMinutes?, assumedMigrationStatus?, cancellationToken?)` — executes the `fix` command (without `assumedMigrationStatus` the repair keeps the progress of every open record) in the host's run mode (rebuild with `MigrationRunMode.Simulate` for a preview), with `cancellationToken`, or the test's token without one
- `InsertOrphanedMigrationRun(minutesOld?, heartbeatMinutesOld?)` — inserts a fake `MigrationRun` record with Running status, a `StartedAt` in the past and, where given, a `LastHeartbeatAt` in the past, together with its `MigrationRunMeta` row as every run of the engine has one (helper for Fix tests)
- `SetMigrationRecordState(filename, targetAlias, status, fileUpBlocksMigrated?, fileDownBlocksMigrated?, migrationRunId?)` — writes the state a process leaves on a record when it ends abruptly in the middle of the file or of its rollback: the status, the counters and the run where given, no `FinishedAt` and no `DurationInMs` for a file in progress, the end and the duration of the last terminal status kept for a rollback in progress (`RepositoryQueryHelper.SetMigrationRecordState`); with `InsertOrphanedMigrationRun` the footprint of an abrupt end, which no command of the engine leaves
- `GetMigrationRecordEnd(filename)` — `FinishedAt` as text and `DurationInMs` of the record of a file, each null when empty
- `RebuildForAsync(command, mode, toRelease?)` — rebuilds DI container for next phase (no DB cleanup)
- `UseProgressObserver(observer)` — sets the progress observer of the host's context and of every context a later rebuild creates; the cancellation tests cancel from a `CancellingProgressObserver` at an exact progress event, and the block counter tests read the record from a `ProbingProgressObserver`
- `UseLogObserver(observer)` — sets an observer of the host's log events and of every host a later rebuild creates; it runs on the thread that logs, before the logging call returns, so that a test cancels update-hash or fix, which report no progress events, at an exact log line, or reads the record right after a repository write
- `InitDatabaseLoggerAsync()` — initializes the database log once more, as the next start of a host does (`DatabaseLogging_CheckCreate`); true when it created the log tables, false when they existed
- `InitDatabaseLoggerWithNewWriterAsync(start)` — initializes the database log as the start of another host does, with a `DatabaseLogWriter`, a DAL and a connection of its own, once `start` completes, so that several starts begin at the same moment; the writer is flushed afterwards; true when it created the log tables
- `RegisterWithNewDalAsync(templateType, name, start)` — registers a product or an environment as the first start of another host does: `Repository_Product_CheckInsert` or `Repository_Environment_CheckInsert` on a DAL and a connection of its own, once `start` completes; the Id of the row of the name
- `InsertMigrationRunWithNewDalAsync(start)` — inserts a run as the start of another host does: `Repository_MigrationRun_Insert` on a DAL and a connection of its own, with the product, environment and engine version a command of the host registered, once `start` completes; the Id of the run, or `-2` when an open run refused it
- `FixOrphanedRunAsync(migrationRunId, thresholdMinutes)` — closes an open run through the executor's orphan fix, as a parallel start and `fix` do (`0` closes it whatever its heartbeat); false when the run is alive and stays open

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
- `CountRunningMigrationRuns()` — counts the open MigrationRun rows, those with the result Running (10)
- `CountRepoRows(tableName)` — counts rows in any repository table
- `CountMigrationsByFilename(filename)` — counts MigrationRecord rows for a specific filename
- `CountMigrationsWithStatus(statusId)` — counts MigrationRecord rows with a specific status
- `GetLatestRunResultId()` — returns the MigrationRunResultId of the most recent run
- `InsertRunningMigrationRun()` — inserts a Running MigrationRun record and its `MigrationRunMeta` row (for concurrent-guard tests)
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
| #62 | RA-5 | RunAlways_FailedAfterACommittedBlock_NextRunStartsAtBlockOne | RunAlwaysTests |
| #63 | RA-6 | RunAlways_FailedAfterACommittedBlockInTargetByTargetOrder_NextRunStartsAtBlockOne | RunAlwaysTests |
| #64 | RA-7 | RunAlways_CancelledAfterBlockOne_NextRunStartsAtBlockOne | RunAlwaysTests |
| #65 | RA-8 | RunAlways_EarlierAttemptOnTheSharedConnection_RunsInFull (Failed with block 1, Executing with every block) | RunAlwaysTests |
| #66 | RA-9 | RunAlways_OpenRecordOfAnOrphanedRun_RunsInFullAfterTheRepair | RunAlwaysTests |
| #67 | RB-9 | MissingRollback_RequireTrue_DuringTheRecovery_KeepsTheStatus | RollbackTests |
| #68 | SF-1 | MigrateDown_FileCancelledAfterACommittedBlock_IsRolledBackFirst | MigrateDown/StartedFileRollbackTests |
| #69 | SF-2 | MigrateDown_FileFailedAfterACommittedBlock_IsRolledBack | MigrateDown/StartedFileRollbackTests |
| #70 | SF-3 | MigrateDown_FileEndedWithAnIgnoredBlockFailure_IsRolledBackWhole | MigrateDown/StartedFileRollbackTests |
| #71 | SF-4 | MigrateDown_FileKeptByTheOrphanRepair_IsRolledBack | MigrateDown/StartedFileRollbackTests |
| #72 | SF-5 | MigrateDown_FileWithoutACommittedBlock_IsNamedInAWarning | MigrateDown/StartedFileRollbackTests |
| #73 | SF-6 | MigrateDown_FailedRollbackOfAStoppedFile_StopsInTheMiddle | MigrateDown/StartedFileRollbackTests |
| #74 | SF-7 | MigrateDown_StoppedFileWithoutItsRequiredRollbackFile_AbortsAndKeepsItsBlocks | MigrateDown/StartedFileRollbackTests |
| #75 | SF-8 | MigrateDown_StoppedFileWithoutItsRollbackFile_GoesOnAndKeepsItsBlocks | MigrateDown/StartedFileRollbackTests |
| #76 | SF-9 | MigrateDown_SimulateWithAStoppedFile_ListsItsRollback | MigrateDown/StartedFileRollbackTests |

RA-5 to RA-9 come from `Infrastructure/RunAlwaysContract.cs` and make the three-block seed of release 1.0 a RunAlways file: after an attempt that committed blocks (a failure at block 2, a cancel after block 1, a record state written by SQL on the atomic shared connection, the open record of an orphaned run), the next migrate-up starts the file with `ResumeFromBlock` 0 and runs every block, block 1 a second time; it neither finalizes a record that committed every block nor carries the blocks of the earlier attempt over to the new record, and the orphan repair settles an open record of the file by its progress (`Failed` with block 1) before the file runs in full.

SF-1 to SF-9 come from `Infrastructure/StartedFileRollbackContract.cs`. The three-block seed of release 1.0 runs on the second database and stops after block 1: a cancel with `Terminate` (SF-1), a failure of block 2 under `Terminate` with a stable hash (SF-2), a failed block 2 under `MigrationErrorAction=Ignore` (SF-3), or an open record of an orphaned run that the auto-fix of migrate-down settles (SF-4). migrate-down to before every release rolls the seed back first, from its first rollback block, then the files before it; the seed ends `NotMigrated`, and the next migrate-up runs it from its first block, so the table has every row again. A seed without a committed block (the atomic shared connection, SF-5) is not rolled back and named in a warning, and the run stays `Ok`. A rollback of the seed that fails at its second rollback block stops in the middle, migrate-up refuses the file and the next migrate-down continues it (SF-6); a missing rollback file keeps the committed block of the seed, aborts the chain with `RequireRollbackFile=true` (SF-7) and is skipped with a warning that names the block otherwise (SF-8); Simulate lists the rollback of the seed and changes nothing (SF-9).

**Features tests** (all 5 engines have 30 base feature test classes each; SqlServer, PostgreSQL, MariaDb and MySql add the DAL timeout class, SqlServer the atomic-shared-connection and killed-pooled-session classes, MariaDb and MySql the DAL user lock class, Sqlite the datetime-CHECK-constraint, command-profile, file-encoding and CLI signal classes, plus a P2 file shared between MySql and MariaDb):

| Class | Tests | Description |
|-------|-------|-------------|
| `MigrationHistoryTrackingTests` | 5 | MigrationRecordHistory records written inline on terminal status transitions (simulate, first run, failed retry, down-then-up, baseline). Source file: `ArchiveRetentionTests.cs`. |
| `BaselineTests` | 10 | Baseline marking, incremental baseline, baseline then MigrateUp |
| `DatabaseLogTests` | 9 | Log entries written after MigrateUp, multiple log levels, logs during error, log tables created, `MigrationEventId` stored; the indexes of `DatabaseLogging_CheckCreate` (`Infrastructure/DatabaseLogIndexContract.cs`): `MigrationRunId` and `CreatedAt` on a fresh log table, added to the log table of an older engine by the next initialization, which still reports that the log existed, and named with the longest table prefix in full; parallel first starts: in each of 10 rounds the test drops the log tables of its prefix `rm_lograce_` and 8 writers of their own initialize the log at once, every start succeeds, at least one reports that it created the log, and the event catalogue holds each event once (MariaDB and SQLite insert it idempotently, the other engines decide under their lock) |
| `DatabaseLogDefaultTests` | 3 | Database logging into the repository by default at Information, a section with only MinimumLevel, `Enabled: false` creates no log tables |
| `ReadModelContractTests` | 1 | After a migrate-up every repository table reads through `RepositoryNaming.SelectStatement` via the engine DAL and maps with `RepositoryRowMapper`: schema version, run, records, lookups, log rows, UTC timestamps (`Infrastructure/ReadModelContract.cs`) |
| `FixTests` | 9 | Fix command: no orphans, fix orphan, simulate, older-than filter, fix-then-migrate, multiple orphans, details, assumed-status=Migrated, scope all |
| `InfoTests` | 29 | Info command: fresh repo, full/partial migration, baseline, target groups, error state, a modified file counted as pending, multiple-run history, run details, the history of a migrate-down and of a rolled back migrate-up, the runs of the requested environment only; the pending list (`Infrastructure/PendingMigrationsContract.cs`): every file `NotMigrated` on a fresh repository, the remaining files after a partial run, `Failed`, `Resumable` after a cancel in a file, `RunAlways` after every run and also after a cancel in it, which the next migrate-up runs from block 1, `Failed` for a file that a CLI tool runs after committed blocks or every block of an earlier attempt, which the next migrate-up runs in full in both `TargetMigrationOrder`s, `HashMismatch`, the open record of a run that ended abruptly as the orphan repair of the next migrate-up leaves it (`Resumable` after its committed blocks, not listed with every block committed), a modified file of an older release listed although migrate-up refuses it out of order, the pending pairs after a failed second target in both `TargetMigrationOrder`s, the target groups in their `TargetGroupMigrationOrder`, an order of the product that names exactly the only target group ignored instead of refused, without a warning of the status (the validation reports RULE_1_13), and per target group the pending count and `LastMigrationDate`; each scenario then runs the next migrate-up and compares the files it starts with the list |
| `MigrationRunMetaTests` | 8 | MigrationRunSettingsJson content (console options, product, masked credentials, MigrateDown masking, Baseline masking), `FromReleaseVersion` as the highest migrated release when the run started |
| `MigSettingsInheritanceTests` | 13 | migsettings.txt inheritance (root, release, target group, TOML override) |
| `OutOfOrderBlockingTests` | 3 | Out-of-order migration blocking (false=blocked, true=allowed, data integrity after blocking) |
| `RepositoryIntegrityTests` | 4 | Repository tables, Product record, Migration records after successful run; first registrations at the same moment (`Infrastructure/RegistrationContract.cs`): in each of 5 rounds 8 starts of their own run `Repository_Product_CheckInsert` (and `Repository_Environment_CheckInsert`) at once with a new name, each on a DAL and a connection of its own; none fails, all get the Id of the one new row, and a later start finds the same Id. Before the idempotent registration of 0.20.0 the second insert failed on the unique index of `NameLower` |
| `HeartbeatTests` | 6 | The run heartbeat: a long block refreshes `LastHeartbeatAt`, an open run with a fresh heartbeat blocks a new start however old its start, a stale heartbeat is auto-fixed, `fix` reports and filters by heartbeat age, the orphan fix leaves an alive run open, a heartbeat younger than the threshold stays alive across a minute boundary |
| `MigrationRunDetailsTests` | 6 | Run details of schema version 2: operation, source, initiator and host from the console options, NULL source and the machine name without them, the error message of a failed run, migrate-down and baseline operations, the history DTO, and the upgrade of a version 1 repository by the next command |
| `RepositorySchemaTests` | 5 | Versioned repository schema (ADR-024): fresh repository gets every step and the engine version, second run changes nothing, a newer repository and one without a schema version are refused before a schema step runs or a row is written, parallel start on an empty repository creates it once |
| `RepositorySchemaServiceTests` | 1 | `IRepositorySchemaService.EnsureAsync` from the repository options alone, without product, environment or run: a fresh repository gets one `RepositorySchemaVersion` row per step, one `MigratorMeta` row and the `MigrationRecordHistory` table; a second call finds the current version and changes nothing (`Infrastructure/RepositorySchemaServiceContract.cs`). The class name carries the engine prefix on every engine (`PostgreSqlRepositorySchemaServiceTests`, `SqlServerRepositorySchemaServiceTests`, ...) |
| `RepositoryPrefixTests` | 3 | A migrate-up against a repository with table prefix `a_` (`WithTableBaseName`), then a second repository with prefix `b_` created through the schema service in the same database or schema; the second repository is complete and empty and the first keeps its run, because every index and constraint name carries the prefix (`Infrastructure/RepositoryPrefixContract.cs`). A disposed scenario with a prefix leaves no table of its prefix, with the log in the repository and in a database of its own (`Infrastructure/PrefixTableCleanupContract.cs`); the test reads the catalog by the prefix, not the table list of `DatabaseCleanupHelper`, so it also fails for a table of the prefix that the cleanup does not know. Engine-prefixed class names as above |
| `CancellationTests` | 24 (SQLite 22) | The cancellation of a run (ADR-028, `Infrastructure/CancellationContract.cs`); the cancel comes from a `CancellingProgressObserver` at an exact event, or for update-hash and fix from a log line (`UseLogObserver`): `Terminate` leaves the interrupted file `Failed` with its committed blocks for the next migrate-up to resume (FileByFile and TargetByTarget); `RollbackFile` rolls back the interrupted file only, `RollbackRun` every file of the run and `RollbackRelease` the interrupted file and the files of its release, newest first, while the release before stays; `RollbackFile` without a rollback file ends `Error` with `CancelledRollbackIncomplete` (1005); a cancel through the token alone terminates; on the atomic shared connection nothing of the file commits and the next run executes it whole; a cancel between files starts no further file; in FileByFile order a cancel between the two targets of a file counts the file as finished in the result and in `RunCompletedEvent`, and `RollbackFile` has nothing to roll back; a handle cancelled before the call ends `Cancelled` with `MigrationRunId` 0 and nothing to roll back, and a migrate-down cancelled the same way stops before its first rollback; Simulate and Validate stop the same way and persist nothing; a cancel while a CLI tool runs kills the tool (not on Windows); migrate-down stops before the next rollback block and the next migrate-down continues; after a migrate-down cancelled in the middle of a rollback, info lists the file `RollbackIncomplete` with its rollback blocks, the next migrate-up refuses before its first file, the next migrate-down continues the rollback and the migrate-up after it runs the release again; a cancel during the error recovery stops it and ignores the action, and the run stays `Error` with the code of the failure and a message that names the failure first (result, run row and `RunCompletedEvent`); a cancel before the error recovery reaches its first record rolls nothing back, the failed file stays `Failed` with its committed blocks and the files of the run stay `Migrated`, and the message says that nothing was rolled back; a cancelled baseline keeps the records it wrote; update-hash cancelled between the two records of a file counts that file, and fix cancelled between two orphaned runs keeps the run it closed; schema version 2 is upgraded to 3 by the next command (lookup row `Cancelled`, four indexes), and every record, run and history row of a migrated repository reads the same afterwards; a cancel inside a sleeping block ends the sleep within seconds (not on SQLite, which checks only before a block): SQL Server and PostgreSQL abort it, the `KILL QUERY` of MySQL and MariaDB ends it early without an error; on the atomic path nothing of the file commits, on a target of its own the record keeps the first block, and on MySQL and MariaDB the ended sleep as well; a `RollbackRelease` whose chain finds a required rollback file missing ends `Error` with 1005, keeps that file `Migrated` and names the file, and migrate-down rolls it back once the file is there; the warnings of a cancel carry no stack trace. Engine-prefixed class names as above |
| `BlockProgressTests` | 15 | The block counters of a record at every moment of a run (`Infrastructure/BlockProgressContract.cs`), read in the flow of the run at a progress event (`ProbingProgressObserver`, `CancellingProgressObserver`) or a log line: a file that a CLI tool runs counts 0 while the tool runs and its one block once it finished, and a rollback through the tool leaves the up counter alone (a shell as the tool, not on Windows); with `MigrationErrorAction=Ignore` block 2 of three fails and the record counts 1 after blocks 2 and 3 and keeps 1 with `Failed` when the file ends; a cancel after the failed block 2 keeps 1 as well, `info` lists the seed `Resumable` after block 1 and the next migrate-up resumes with block 2; a block 2 that fails until the test creates its table leaves `Failed` with 1, and the next migrate-up resumes with it and completes the file without running block 1 again; a failed block 1 leaves `Failed` with 0, `info` lists `Failed`, and the next migrate-up runs the whole file; with `RollbackErrorAction=Ignore` the rollback counts 1 the same way and stores 3 with `Failed` when it ends; a resumed file (FileByFile and TargetByTarget) shows `Executing` with its committed block when it starts, and a cancel there keeps it for the run after, which resumes with block 2; a continued rollback shows its rollback block when it starts, and the migrate-down after a cancel there completes it; with `RollbackErrorAction=Ignore` a cancel after the failed rollback block 2 keeps 1, and the next migrate-down starts with block 2, which fails again, and stores 3 with `Failed`; on the repository connection a migrate-down whose rollback file sets `RollbackErrorAction=Ignore` is cancelled after rollback block 1, and the next one takes the atomic path, where block 2 fails: the record keeps 1, and migrate-up refuses the file; a rollback through a CLI tool (a shell that fails for rollback files) that fails ends `Failed` with no rollback block and aborts the chain; a file that runs through a CLI tool after an attempt on the DAL committed block 1 starts with `ResumeFromBlock` 0 (FileByFile and TargetByTarget); a rollback through a CLI tool after a migrate-down on the DAL was cancelled after rollback block 1 starts with `ResumeFromBlock` 0 as well; a rollback whose progress write after rollback block 2 fails counts that block (`Failed` with 2), so that the next migrate-down continues after it; a rollback through a CLI tool on one target of two reports one block there while the other target rolls back its three rollback blocks on the DAL; a rollback through a CLI tool that exits without reading the rollback file it gets on its standard input ends `Failed` with no rollback block and aborts the chain, and with `RollbackErrorAction=Ignore` the record counts its rollback and the chain goes on (not on Windows). Engine-prefixed class names as above |
| `OrphanRepairTests` | 9 | The orphan repair of a run that ended abruptly (`Infrastructure/OrphanRepairContract.cs`); the open run (heartbeat three minutes old) and its open record are written by SQL, the committed blocks come from a cancel or a migrate-down: the auto-fix of the next migrate-up repairs an `Executing` record with block 1 of 3 to `Failed` with 1 and resumes with block 2, without running block 1 again; `fix` without a status does the same and gives the record a `FinishedAt` but no `DurationInMs`; both close the run as `Error` with the engine code `1006` and the message `Closed as orphaned by the repair: the run ended without finishing; last heartbeat <yyyy-MM-dd HH:mm:ss> UTC`, the heartbeat the test wrote three minutes before; `fix --last-migration-status not-migrated` sets `NotMigrated` with the counter kept, and the next migrate-up runs the file from block 1; a record without a committed block becomes `NotMigrated` and runs from block 1; a record with every block committed becomes `Migrated`, `info` no longer lists it while the run is open, and the next migrate-up runs nothing of it; a record that `fix` finishes as `Migrated` moves `TargetGroupStatus.LastMigrationDate` to the time of the repair; a rollback that was running after rollback block 1 of 3 becomes `Failed` with 1, migrate-up refuses the file and migrate-down continues with rollback block 2; a rollback that had run every rollback block becomes `NotMigrated`; a rollback without rollback progress becomes `Failed`, migrate-up does not refuse it and runs the file from block 1; a rollback that was running keeps the end and the duration of its last terminal status; a rollback record left `Executing` with part of its rollback blocks in a run that has ended (the process ended between the two steps of the repair) is listed as `RollbackIncomplete`, migrate-up refuses it and migrate-down continues it; while a run is open whose record the repair will finish as `Migrated`, the status names its release as the current release and counts the file as executed, and the next migrate-up starts from that release; `fix` without a status settles open records in every state as the projection of the status does. The PostgreSQL class runs its sessions in the time zone Asia/Kathmandu (+05:45), so that the message of the repair is checked against the heartbeat in UTC. Engine-prefixed class names as above |
| `DalCancellationTests` | 2 | The shared-connection calls of the DAL (`ExecuteNonQueryAsync`, `ExecuteScalarAsync` on the caller's open connection and transaction, as the atomic path uses them) hand the caller's token to the command: a token cancelled before the call is reported by the provider, without a failure of the statement (`Infrastructure/DalSharedConnectionCancellationContract.cs`). The unit tests cannot show this for Npgsql and MySqlConnector, which check the connection before the token. Engine-prefixed class names as above |
| `DalTimeoutTests` | 1 (not SQLite) | A command that runs into its command timeout of one second is a timeout for `DalBase.IsTimeout` (SqlClient error -2, a `TimeoutException` from Npgsql, `CommandTimeoutExpired` of MySqlConnector), and another failure is not; MySQL and MariaDB run a long query instead of `SLEEP()`, which `KILL QUERY` ends without an error (`Infrastructure/DalTimeoutContract.cs`). SQLite has no command timeout for a running statement. Engine-prefixed class names |
| `DalUserLockTests` | 1 (MySQL and MariaDB only) | A call that fails after its `GET_LOCK` leaves no lock behind: a session of its own outside the pool of the DAL finds the lock free (`IS_FREE_LOCK`), because the DAL runs `DO RELEASE_ALL_LOCKS()` before the connection goes back to the pool (`Infrastructure/DalUserLockContract.cs`). Engine-prefixed class names |
| `RepositoryMaintenanceTests` | 14 (SQLite 15) | The retention of `IRepositoryMaintenanceService.CleanUpAsync` (ADR-027, `Infrastructure/RepositoryMaintenanceContract.cs`): a cutoff in the future deletes the history and the log and keeps the runs and records, so status and the next migrate-up do not change; an open run keeps its rows until it is closed; log rows without a run fall by their age; a cutoff between two runs deletes the rows of the older run only; a dry run counts the rows and deletes nothing; a batch size of one takes one batch per row; a time budget of zero stops after one batch and the next call finishes; a cleanup that stops early has deleted the oldest rows (the rows are aged in the order they were written, the order of the clustered key in which SQL Server deletes); a repository without log tables and a log in a database of its own (`WithDatabaseLoggingInto`) are skipped and left alone; the longest table prefix; a cancel between two batches returns the rows deleted so far, a cancel at the check of the log table returns the deleted history without throwing, a cancel in a dry run throws `OperationCanceledException`; on SQLite, a batch that fails after a deleted batch (another connection holds the write lock beyond the command timeout of one second) is not retried, and the call returns what it deleted with `Completed = false` and the error in the message. The scenarios age rows with UPDATEs instead of waiting. Engine-prefixed class names as above |
| `ProductIsolationTests` | 2 | The product scope of a run (ADR-026): the configuration carries a second product `Broken` whose target has no connection string, whose target group names the database type `NoSuchDb` and whose directory is a placeholder of a variable that is not set (`WithSecondProduct(alias, broken: true)`); migrate-up and info of `EngineTest` succeed beside it, and a host built for `Broken` (`ForProduct`) fails on the broken product alone: on the unset variable first (`ApplicationStartupException`), and once the test sets the variable to a directory that does not exist, on the data annotations, which name `Products[Broken]`, the connection string and the directory (`Infrastructure/ProductIsolationContract.cs`). Engine-prefixed class names as above |
| `ProgressEventsTests` | 8 | Typed progress events through `MigrationContext.ProgressObserver`: run, file and block events of migrate-up, simulated outcomes and the start release without a run row, rollbacks and skipped records of migrate-down, baselined files without blocks, the failed file and run, and an observer that throws does not abort the run |
| `ParameterRoundTripTests` | 2 (PostgreSQL 3) | A parameter value with quotes, backslashes, `$$`, other placeholders, comment and statement markers and non-ASCII text comes back unchanged through the DAL; PostgreSQL also inside a `DO` block |
| `RunningGuardTests` | 7 | Concurrent run prevention (MigrateUp, MigrateDown, Baseline blocked; ValidateHash/UpdateHash allowed); MigrateUp blocked the same way after two finished runs, and the refused start writes neither a run nor a `MigrationRunMeta` row; starts at the same moment (`Infrastructure/RunLockContract.cs`): in each of 10 rounds 8 starts of their own run `Repository_MigrationRun_Insert` at once, each on a DAL and a connection of its own, exactly one gets a run, the others are refused with `-2`, one run is open and every run has its `MigrationRunMeta` row; the round closes the run through the orphan fix. On SQLite several starts took the lock in most rounds before the `BEGIN IMMEDIATE` transaction of the template (0.20.0) |
| `SimulateModeTests` | 14 | Simulate mode (no user tables written), Validate mode, MigrateDown simulate, MigrateDown Validate (non-destructive + missing rollback detection), Simulate reads the same records as Migrate: preview of pending files only, nothing pending after a full migrate, rollback candidates, empty repository, an environment that is not registered yet, for which nothing is inserted (#7) |
| `TargetGroupMigrationOrderTests` | 12 | TargetGroupMigrationOrder overrides (CLI, appsettings, migsettings), wrong-case error, Baseline, an order of the only TargetGroup ignored by migrate-up and baseline, with one warning per run for the CLI order, one per release for the migsettings and no warning of the run for the product setting, a wrong-case order of the only TargetGroup refused |
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
| **Missing rb + ReqRB=true** | RB-6, RB-9 | — | — |
| **Missing rb + ReqRB=false** | RB-7, RB-8 | RR-7 | RE-3 |

#### MigrateDown x Complication

|  | Success | Missing rb (Req=true) | Missing rb (Req=false) | Broken rb (Term) | Broken rb (Ign) |
|---|---|---|---|---|---|
| **MigrateDown** | MD-1, MD-2, MD-3 | ME-2 | ME-1 | ME-3 | ME-4 |
| **MigrateDown, file stopped after committed blocks** | SF-1, SF-2, SF-3, SF-4, SF-9 | SF-7 | SF-8 | SF-6 | — |

#### Multi-Phase Scenarios

|  | Incremental Up | Up→Down | Up→Down→Up | Recovery | RunAlways |
|---|---|---|---|---|---|
| **Success** | IN-1 | MD-1,2,3, EC-1,2,3 | RT-1 | RC-2 | RA-1, RA-4 |
| **Error + Rollback** | IN-2, IN-4 | — | — | — | RA-3 |
| **Error + RbRelease** | IN-3, IN-5 | — | RT-2 | — | — |
| **Error + Terminate** | — | — | — | RC-1 | RA-2, RA-5, RA-6 |
| **Cancel + Terminate** | — | — | — | — | RA-7 |
| **Earlier attempt (record by SQL)** | — | — | — | — | RA-8, RA-9 |
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
- **Ignore mode, error file**: `FileUpBlocksMigrated` = the blocks before the first failed block, while the file runs (also after a later block committed; DB record updated after every block attempt) and when it ends; the next migrate-up resumes at the failed block, or runs the whole file when the first block failed. A `FileUpBlocksMigrated` equal to `FileUpBlocksTotal` always means that every block committed
- **While a file runs**: `FileUpBlocksMigrated` = the leading blocks that committed; 0 while a CLI tool runs; a resumed file shows the blocks it resumes after from the start; a file that a CLI tool runs starts at block 1 also after committed blocks of an earlier attempt (`BlockProgressContract`), and is not finalized from one (`PendingMigrationsContract`)
- **Cancelled file**: status `Failed` and `FileUpBlocksMigrated` = the blocks that committed before the cancel, so the next migrate-up resumes with the first block that did not commit (on the atomic shared connection only those of an earlier attempt, normally 0; in Ignore mode the blocks before the first failed one, as at the end of such a file)
- **Rollback cancelled in the middle**: status `Failed` with `0 < FileDownBlocksMigrated < FileDownBlocksTotal`; the next migrate-up refuses to start, the next migrate-down continues with rollback block `FileDownBlocksMigrated + 1`
- **File that stopped after committed blocks, rolled back by migrate-down**: the rollback starts at rollback block 1 (`ResumeFromBlock` 0) and ends `NotMigrated` with `FileDownBlocksMigrated` = `FileDownBlocksTotal`; `FileUpBlocksMigrated` keeps the committed blocks until the next migrate-up inserts the record anew and runs the file from block 1 (`StartedFileRollbackContract`)
- **Open record after the orphan repair**: `Failed` with `0 < FileUpBlocksMigrated < FileUpBlocksTotal` when some blocks committed, so the next migrate-up resumes after them; `Migrated` when every block committed; `NotMigrated` without a committed block; a rollback that was running `Failed` with its rollback blocks, `Failed` without rollback progress, or `NotMigrated` when every rollback block ran; the counters stay, `FinishedAt` is the time of the repair and `DurationInMs` stays `NULL` for a file that was running, while a rollback that was running keeps the end and the duration of its last terminal status (`OrphanRepairContract`)
- **Successful file**: `FileUpBlocksMigrated` = `FileUpBlocksTotal`
- **After successful rollback**: `FileDownBlocksMigrated` = `FileDownBlocksTotal`
- **While a rollback runs**: `FileDownBlocksMigrated` = the leading rollback blocks that ran, also with `RollbackErrorAction=Ignore` after a failed block and after a cancel there; a continued rollback shows its earlier rollback blocks from the start, and keeps them when it fails on the atomic path or through a CLI tool; `FileDownBlocksTotal` once an ignored failure ends the rollback
- **Missing rollback file**: `FileDownBlocksMigrated` and `FileDownBlocksTotal` remain `NULL`

### Rollback Result Matrix

How the engine handles each combination of rollback file existence, SQL outcome, `RequireRollbackFile`, and `RollbackErrorAction`:

| # | Rollback File | SQL | RequireRB | RbErrAction | File Status | Chain | RunResult | Success | Engine API |
|---|--------------|-----|-----------|-------------|-------------|-------|-----------|---------|------------|
| 1 | **exists** | **OK** | any | any | NotMigrated(50) | continues | Ok(100) | true | SuccessCount++ |
| 2 | **exists** | **FAILS** | any | **Terminate** | Failed(30) | **ABORTED** | Error(90) | false | AddFailure |
| 3 | **exists** | **FAILS** | any | **Ignore** | Failed(30) | continues | PartialSuccess(50) | **true** | AddWarning |
| 4 | **missing** | — | **true** | any* | **Unchanged** | **ABORTED** | Error(90) | false | AddFailure |
| 5 | **missing** | — | **false** | any* | **Unchanged** | continues | PartialSuccess(50) | **true** | AddWarning |

*`RequireRollbackFile` takes precedence over `RollbackErrorAction` for missing files. `RollbackErrorAction` only applies to SQL execution errors.

**Key insight**: Rows 3 and 5 return `Success=true` by design, and their warning makes the run `PartialSuccess` (50) instead of `Ok`. Row 4 leaves the status unchanged as well, because nothing of the rollback ran, but aborts the chain. When the administrator chooses `Ignore` or `RequireRollbackFile=false`, they explicitly tolerate the situation. For row 3, the file is marked `Failed(30)` (SQL execution failed). For row 5, the file's status is **not updated** — it retains its previous value (e.g., `Migrated(100)`) because the missing rollback file is a known acceptable condition, not an error. In both rows, the engine uses `AddWarning` (not `AddFailure`), so `FailCount` stays 0 and `AllSuccessful` (= `FailCount == 0`) evaluates to `true`.

**Impact on other files in the rollback chain**:

| Position relative to problem file | Chain continues (rows 1,3,5) | Chain aborted (rows 2,4) |
|-----------------------------------|------------------------------|--------------------------|
| Before problem (newer releases) | NotMigrated (already rolled back) | NotMigrated (already rolled back) |
| **Problem file, failed rollback SQL (rows 2, 3)** | Failed(30) | Failed(30) |
| **Problem file, missing rollback file (rows 4, 5)** | Unchanged (Migrated) | Unchanged (Migrated) |
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
