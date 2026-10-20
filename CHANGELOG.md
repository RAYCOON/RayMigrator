# Changelog

All notable changes to RayMigrator are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
RayMigrator follows Semantic Versioning where applicable.

## [Unreleased]

## [0.20.0] — 2026-10-20

### Added

- A host can cancel a run and choose what the run does when it stops. The
  methods of `IMigrationService` take `CancellationToken cancellationToken
  = default`, and `MigrateUpRequest`, `MigrateDownRequest` and
  `BaselineRequest` take a `RunCancellation` (new in
  `Raycoon.RayMigrator.Services.Abstractions`): its `Cancel(action,
  reason)` cancels the run once, the first call decides the
  `MigrationCancelledAction` (new in `Core.Configuration.Enums`, a value of
  the run, not a configuration option) and later calls return false;
  `Token`, `Action`, `Reason` and `RequestedAtUtc` describe the request.
  The cancelling call runs the callbacks of the token, those of the
  database providers among them, synchronously on the calling thread and
  swallows their exceptions: the action and the token are set before, so
  the run stops at its next checkpoint all the same.
  The engine links the token of the call with the handle; a cancel through
  the token alone means `Terminate`. A cancelled run stops at the next
  block boundary: there are checkpoints before every file, target and
  block, and the token goes to the target DAL, which interrupts the block
  in progress where the provider can (SQL Server, PostgreSQL and MariaDB
  abort the statement; the `KILL QUERY` of MySQL and MariaDB ends a
  `SLEEP()` early without an error, and the further statements of the block
  run; SQLite only checks before a block). The
  interrupted file reports `FileOutcome.Cancelled` (100) and its record is
  `Failed` with the leading blocks that committed (with
  `MigrationErrorAction=Ignore` those before the first failed block; on the
  atomic path only those of an earlier attempt, normally none), so the
  next migrate-up resumes it like a failed file. Then the run takes the
  action: `Terminate` (10, the default) does nothing else, `RollbackFile`
  (21), `RollbackRun` (20) and `RollbackRelease` (22) run the rollback
  chain of `MigrationErrorAction.RollbackErrorOnly`, `Rollback` and
  `RollbackRelease` with the same rules for missing rollback files and
  `RollbackErrorAction`; a run cancelled between two files, or in
  FileByFile order between two targets of a file, has no interrupted file,
  so `RollbackFile` rolls nothing back and `RollbackRelease` takes the
  release of the file the run ran last; a file stopped between two of its
  targets counts as a finished file in the result and in
  `RunCompletedEvent`, since it migrated on the targets it ran on. This
  rollback cannot be cancelled again. The run is recorded as the new
  `MigrationRunResult.Cancelled` (70) with the new engine code
  `TemplateResultCode.Cancelled` (1004) and the message `Cancelled on
  request (<action>): <outcome>`, plus the reason; when the rollback
  reported a failure or a warning, the run is `Error` with
  `TemplateResultCode.CancelledRollbackIncomplete` (1005), the same rule
  that decides `Recovered` against `Error` after a failure. Migrate-down,
  baseline and an error recovery that is already running stop at the next
  block or file and ignore the action (logged when it is not `Terminate`):
  the record in progress of a migrate-down or an error recovery is `Failed`
  with its leading rollback blocks and the next migrate-down continues it,
  unless its first rollback block failed under `RollbackErrorAction=Ignore`,
  and the records a baseline wrote stay. A migrate-up that failed at a file
  and whose error recovery the cancel stopped stays `Error` with the code of
  that failure, not `Cancelled`: the run failed before the cancel, which
  only stopped the handling of the failure, so error rates, the exit code of
  the CLI (1, not 130) and the operator see the failed file. Its message
  names the failure first, then where the cancel stopped the recovery and
  what that leaves, then the reason: `The error recovery was cancelled on
  request in the rollback of <file> on target <target> after j of J rollback
  block(s); migrate-down continues that rollback, and migrate-up refuses
  <file> until it has completed`, `… before its first rollback: nothing was
  rolled back, <file> stays Failed with the blocks it committed, and the
  files this run migrated stay Migrated` or `… after i of m rollback(s): the
  files it did not reach keep their state`. The first block of a rollback
  file, an atomic rollback and a rollback through a CLI tool always
  complete, so a cancel leaves a record `Failed` without rollback progress
  only where `RollbackErrorAction=Ignore` went on after a failed first
  rollback block, which the message names: `… in the rollback of <file> on
  target <target>, whose first rollback block failed under
  RollbackErrorAction=Ignore: <file> is Failed without rollback progress,
  which migrate-down does not continue`. A
  migrate-up refuses to start while a file it would run has a record
  `Failed`, or `Executing` in a run that has ended, with some but not all of
  its rollback blocks (left by such a cancel, by a failed rollback block
  after the first, by the orphan repair of a run that ended abruptly in the
  middle of the rollback or by a repository write that failed), because it
  would run the file again from its first
  block on a target that kept part of it: no file runs, the run is
  `Error`, and the message names the release, file and target of each
  such record, its `k of n rollback blocks` and that migrate-down
  continues the rollback at block k+1. The run returns `Success = false`
  with `Result = Cancelled` (or `Error`) instead of throwing, and
  `RunCompletedEvent` reports it. update-hash and fix stop before the next
  record or run and return `Success = false`, `ErrorCode = Cancelled` and
  what they did so far (update-hash counts a file in `UpdatedFiles` and
  `UpdatedFileNames` once its first record is updated, so a cancel between
  the records of a file reports it); validate-hash, status and history throw
  `OperationCanceledException`. Simulate and Validate persist nothing, and
  neither does a run cancelled before its run row: it is `Cancelled` with
  `MigrationRunId` 0, and a rollback action reports that there is nothing
  to roll back. The repository writes of a run on the repository's own
  connection never take its token, so a record and its history row are
  never separated by a cancel; on the atomic path the record updates share
  the transaction of the file and take the token with it, so a cancel rolls
  back the blocks of the file and their updates as one. Every write after
  the stop runs to its end.
- `RepositoryQueryHelper` (`Raycoon.RayMigrator.Testing`) reads what the
  cancellation tests check: `GetMigrationHistoryStatuses(filename)`, the
  status and operation of each history row of a file in history order;
  `GetIndexColumns(indexName)`, the columns of a repository index in key
  order; `GetLookupName(tableName, id)`, the name of a lookup row; and
  `GetLogMessages(logLevelId)`, the messages of the log entries at one
  level with the exception text the database sink appends.
- `RepositoryQueryHelper` (`Raycoon.RayMigrator.Testing`) writes and reads
  what the tests of the orphan repair need:
  `SetMigrationRecordState(filename, targetAlias, migrationStatusId,
  fileUpBlocksMigrated, fileDownBlocksMigrated, migrationRunId)` writes the
  state a process leaves on a record when it ends abruptly in the middle of
  the file or of its rollback (the status, the counters and the run where
  given; a file in progress without `FinishedAt` and `DurationInMs`, a
  rollback in progress with the end and the duration of the last terminal
  status of the record), and `GetMigrationRecordEnd(filename)`
  returns `FinishedAt` as text and `DurationInMs` of the record of a file.
- `BaselineResult.Result` names the result the run row records (`Ok`,
  `Error` or `Cancelled`), and `RunCompletedEvent.Result` carries it for
  baseline as well; it was null for baseline before.
- The log table `MigrationLog` has two indexes, for the readers of the log
  and for a cleanup by age: `ix_{TableBaseName}MigrationLog_MigrationRunId`
  (`MigrationRunId`) and `ix_{TableBaseName}MigrationLog_CreatedAt`
  (`CreatedAt`), in snake_case on PostgreSQL, MySQL and MariaDB.
  `DatabaseLogging_CheckCreate` (every DAL) creates each index when it is
  missing, whether or not it creates the tables, so the next
  initialization of the database log also adds them to a log table that an
  older engine created and still reports that the log existed (result 0).
  This holds as well for a log database of its own, with its own
  connection, schema and prefix; the log tables are not part of the
  repository schema version. A parallel start of another host does not
  fail on the indexes: SQL Server, PostgreSQL and MySQL serialise the
  initialization with a lock apart from the repository schema lock,
  MariaDB and SQLite rely on `CREATE INDEX IF NOT EXISTS` and insert the
  event catalogue idempotently. The wait for that lock ends with the
  command timeout of `DatabaseLogging`
  (`DbCommandTimeoutInSeconds`, 20 seconds by default); SQL Server and
  MySQL give up after 60 seconds and report -1 only when that timeout is
  longer, and PostgreSQL waits for the lock until the timeout. The start
  that adds the indexes needs the right to create indexes on the log
  table; a later start finds them and needs that right no more. That first
  start builds them on the rows already there within the same command
  timeout. A timeout of `DatabaseLogging_CheckCreate` is not retried,
  other transient failures are, as for every log write: the attempt was
  rolled back whole, and a retry would build the indexes again and hold
  back the log writes of other hosts once more. The start then fails with
  a message that names the index build and
  `DatabaseLogging.DbCommandTimeoutInSeconds`; when updating, raise that
  timeout for the first start on a large log. `DalBase.IsTimeout` (new,
  virtual) tells a timeout from other failures: a `TimeoutException` in
  the chain, SqlClient error -2 and the `CommandTimeoutExpired` of
  MySqlConnector, and a DAL overrides it when its provider reports a
  timeout in another way. Like the shipped DALs, an external
  DAL adds the two indexes to its own `DatabaseLogging_CheckCreate`.
- The CLI stops a run on Ctrl+C and SIGTERM instead of dying with it. While
  a command runs, the first Ctrl+C (SIGINT) or SIGTERM cancels the
  `RunCancellation` of the process, which the requests of migrate-up,
  migrate-down and baseline carry and every call of `IMigrationService`
  observes as its token, and keeps the process alive: the command stops at
  its next checkpoint and records why, and a warning on the console and in
  the database log of the run says where it stops and what a second signal
  does. A second Ctrl+C or SIGTERM keeps the default and ends the process at
  once; the run row then stays open until a later start closes it as
  orphaned or `fix --scope orphanedruns` does. Before the command (loading
  the configuration, the logger, the connection check) and after it
  (flushing the log) both signals keep their default, because nothing there
  would observe the cancel. The handler queues the cancel to the thread
  pool: the cancel callbacks of the providers run on the calling thread
  (MySQL opens a connection for `KILL QUERY`), and Windows runs console
  control handlers under a lock, so a waiting handler could hold back a
  second Ctrl+C. SIGTERM is handled on Linux and macOS; on Windows .NET maps
  it to `CTRL_SHUTDOWN_EVENT`, after whose handler Windows ends the process
  whatever the handler returns, so only Ctrl+C cancels there. Ctrl+Break and
  Ctrl+\ (SIGQUIT) keep their default.
- `--cancelled-action` (`-ca`) on migrate-up, migrate-down and baseline
  chooses the `MigrationCancelledAction` of that cancel: `terminate` (the
  default), `rollback-file`, `rollback-run` or `rollback-release`,
  case-insensitive, `{ENV:...}` resolved, any other value a parse error
  (`RayMigratorConsoleOptions.CancelledAction`, `Terminate` for every other
  command). Migrate-down and baseline accept it so that a script can pass
  the same options to every run command, and ignore it as the engine does
  (logged when it is not `terminate`).
- Exit code `130` (128 + SIGINT) for a command that the cancel ended:
  migrate-up, migrate-down and baseline with `MigrationRunResult.Cancelled`,
  and, while the token of the process is cancelled, update-hash and fix
  with `TemplateResultCode.Cancelled` (1004) and validate-hash and info
  with an `OperationCanceledException`. `RayMigratorService` logs such a cancel
  as a warning instead of an error. A migrate-up that failed at a file before
  the cancel stopped its error recovery is `Error` with the code of the
  failure and exits with `1`. A cancelled migrate-up whose rollback did
  not complete cleanly (`Error` with `CancelledRollbackIncomplete`, 1005)
  exits with `1` like any failed run, and so do a 1004 and an
  `OperationCanceledException` that did not come from the cancel of the
  process.
- `MigrationStatusInfo.PendingFiles` lists what the next migrate-up runs: one
  `PendingMigrationInfo` (new in `Raycoon.RayMigrator.Services.Abstractions`)
  per file and target with `ReleaseVersion`, `TargetGroupAlias`,
  `TargetAlias` (null only when neither the configuration, the `Targets`
  filter of the file nor a record names a target), `Filename`, `FileOrderId`,
  `Reason`, `ResumeFromBlock`, `RollbackBlocksMigrated` and
  `RollbackBlocksTotal`. The list comes from the evaluation that
  counted `PendingMigrations` before, which is now always its length, and
  uses the checks of migrate-up: a RunAlways file is pending on every target
  its `Targets` filter selects, any other file on every selected target
  without a Migrated record whose hash matches under the HashValidationScope
  of its target group. The new `PendingMigrationReason` (`Undefined` 0,
  `NotMigrated` 10, `Failed` 20, `Resumable` 30, `RunAlways` 40,
  `HashMismatch` 50, `RollbackIncomplete` 60) says why: first
  `RollbackIncomplete` for a record `Failed`, or `Executing` in a run that
  has ended, with some but not all of its rollback blocks, also of a
  RunAlways file, which migrate-up refuses to
  run until migrate-down has completed the rollback; then `RunAlways` for a
  RunAlways file, which migrate-up always runs in full; then `Resumable`
  when migrate-up skips committed blocks (a failed or cancelled file whose
  blocks did not change, or an Executing record with every block that
  migrate-up only finalizes); `HashMismatch` for a Migrated record whose
  hash no longer matches; `Failed` for a failed attempt without a resume
  point; otherwise
  `NotMigrated` (no record, a rolled back one, or a Pending or Executing
  record of a run that is still open, if the orphan repair of the next
  migrate-up settles it as `NotMigrated`: without a committed block, or
  after a completed rollback). `ResumeFromBlock` is set for
  `Resumable` only and counts like `FileStartedEvent.ResumeFromBlock`: the
  committed leading blocks the next migrate-up skips, the block count of the
  file when it only finalizes the record. `RollbackBlocksMigrated` and
  `RollbackBlocksTotal` are set for `RollbackIncomplete` only: the rollback
  blocks that ran, after which migrate-down continues, and the blocks of the
  rollback file. The entries are in the order of a
  migrate-up without `--target-group-migration-order`: releases, the target
  groups of a release in their TargetGroupMigrationOrder (migsettings of the
  release, else the product) or in configuration order, then file by file
  or target by target as the TargetMigrationOrder of the group says, the
  targets in configuration order. A run with `--to-release` or
  `--target-group` runs a part of the list, and files of a release older
  than what a target has migrated are listed although migrate-up refuses them
  without `--allow-out-of-order`.
- `TargetGroupStatus.PendingMigrations` counts the entries of
  `PendingFiles` that belong to the target group.
- `info` lists the pending migrations under their count, which now follows
  `Total Executed`: release, target group, target, file and reason, a
  resumed file as `Resumable after block n`, a file whose rollback stopped
  in the middle as `RollbackIncomplete after k of n rollback blocks, run
  migrate-down`, in the order of the next
  migrate-up; the list shows the first 50 entries and ends with
  `… and n more` when there are more. Every target group line names
  `Pending=` and `LastMigration=`.
- `IRepositoryMaintenanceService` (new in
  `Raycoon.RayMigrator.Services.Abstractions`, implemented by
  `RepositoryMaintenanceService` and registered by `AddRayMigratorServices`
  as a singleton next to `IRepositorySchemaService`) deletes old rows of the
  two tables that grow with every run, `MigrationRecordHistory` and
  `MigrationLog`, from the repository options alone, for hosts that apply a
  retention policy (RayMigrator Studio, ADR-027):
  `CleanUpAsync(RepositoryOptions, RetentionRequest, CancellationToken =
  default)` returns a `RetentionResult(HistoryRows, LogRows, Batches,
  Completed, LogSkipped, Message)`. `RetentionRequest(HistoryCutoffUtc,
  LogCutoffUtc, BatchSize = 2000, TimeBudget = null, DryRun = false)` names
  the cutoff of each table (null leaves the table alone, at least one is set,
  both are `DateTimeKind.Utc` and count to the whole second, so a row is
  never deleted unless it is older), the most rows of one batch (at least 1,
  `RetentionRequest.DefaultBatchSize`) and how long the cleanup may delete
  (null means `RetentionRequest.DefaultTimeBudget`, 60 seconds,
  `Timeout.InfiniteTimeSpan` no limit); a request that breaks these rules is
  an `ArgumentException` before the database is touched. The options are
  validated like those of `IRepositorySchemaService` (placeholders, data
  annotations with their defaults, database type, RULE_4_2 to RULE_4_5,
  schema name); both services share that code now. The schema version is
  read through `Repository_CheckCreate`, as every command reads it, which
  creates the empty schema version table (on SQL Server and PostgreSQL also
  the schema) when it is missing: a database without a repository, a
  repository without a schema version and one newer than the engine are
  refused with `RepositorySchemaException`; an older repository is cleaned
  as it is and not upgraded, because the templates read only columns of
  schema version 1 and an upgrade would lock out the engines that still use
  it, with a warning when it lacks the indexes of version 3. The service
  deletes the history first, then the log, in batches of one statement each
  that commit on their own, the oldest rows first, until a batch deletes
  fewer rows than `BatchSize`. The time budget is checked before every batch
  once a batch has deleted rows, so a call always deletes one batch when
  there is one, also with a budget of zero; when it runs out the result is
  `Completed = false` with a message to run the cleanup again. The templates
  and the check of the schema version run with the command timeout of the
  repository but without its retries (`DbCommandMaxRetries`): a statement
  that timed out, and on SQLite one that reports BUSY after its busy
  timeout, would wait the whole command timeout again on every retry, which
  turned a budget of a minute into hours; the next batch and the next call
  are the retry. An error after a batch deleted rows returns `Completed =
  false` with the rows deleted so far and the error in `Message`, logged as
  a warning with the exception; an error before that throws, as nothing
  changed. The token is checked before every batch and reaches the batch in
  progress: SQL Server, PostgreSQL, MySQL and MariaDB abort the statement,
  which rolls back whole, SQLite completes it; a cancel after the checks of
  the options and the schema version, also one during the check of the log
  table between history and log, returns the rows deleted so far with
  `Completed = false` and does not throw, a cancel during those checks
  throws `OperationCanceledException`. A dry run counts the same rows in
  full and deletes nothing; the cleanup itself only asks whether the log
  table exists, so it does not read a large log whole before its first
  batch. Every batch spares the rows of a run that is
  still open (`MigrationRun.FinishedAt` null), so a running or orphaned run
  keeps its history and its log until a later start or `fix` closes it; a
  log row without a run (`MigrationRunId` null) falls by its age. The log
  is cleaned only where it shares the repository database and the schema and
  prefix of the repository, its default home; a log of its own elsewhere is
  left alone and reported as `LogSkipped = true`. `MigrationRecord`,
  `MigrationRun` and `MigrationRunMeta` are never deleted, so status and
  every decision of the engine stay as they were; the run history of `info`
  reports no migrations for a run whose history rows are gone. Serialising
  the cleanups of a repository is the host's business: two at once delete
  only rows that match. The guard reads the open runs of this repository, so
  when another repository writes into the same log table (same database,
  schema and prefix), its rows fall by their age even while its run is open.
- Four templates per DAL for the retention, with new `TemplateType`
  members: `Repository_MigrationRecordHistory_Delete` and
  `DatabaseLogging_Delete` delete one batch of the rows older than
  `@CutoffUtc` (UTC text `yyyy-MM-dd HH:mm:ss`), at most `@BatchSize`, and
  return `'n,message'` with the rows deleted;
  `Repository_MigrationRecordHistory_Count` returns `'n,message'` with the
  rows the delete would take, `DatabaseLogging_Count` `'n,1,message'`, or
  `'0,0,message'` when the log table is not in the repository database;
  with `@ExistsOnly` = 1 it counts nothing and returns `'0,1,message'` when
  the log table exists. The
  log templates run with the schema and prefix of the `Repository` section,
  not of `DatabaseLogging`, and join its `MigrationRun`. A batch is one
  DELETE: SQL Server deletes through a CTE with `TOP` in the order of the
  clustered key with `OPTION (LOOP JOIN)` (ordered by the time column, the
  optimizer read every matching row for each batch, so that a cleanup grew
  with the square of its rows), PostgreSQL deletes the ids of a `LIMIT`
  subquery in a data-modifying CTE that counts them in the same statement,
  MySQL and MariaDB use `DELETE … ORDER BY … LIMIT`, SQLite deletes the
  rowids of a `LIMIT` subquery, because `DELETE … LIMIT` needs a build with
  `SQLITE_ENABLE_UPDATE_DELETE_LIMIT`. SQLite resolves the tables of a
  statement when it prepares it, so its log count creates an empty log table
  for the count when the table is missing, inside a transaction that it rolls
  back. A count may exceed the int range of the result contract and is read
  as a long. The default batch size of 2,000 keeps SQL Server from
  escalating a batch to a table lock, which would hold back the log writes
  of running hosts: it escalates a statement at 5,000 locks on one index,
  and a batch takes a key lock per row and a page lock per page in every
  index; measured with the indexes of schema version 3, history batches of
  4,800 rows and log batches of 5,000 rows escalated, while 2,000 rows take
  at most 4,000 locks per index even with one row per page.
- `RepositorySchemaExecutor.CheckVersionAsync` reads the schema version of a
  repository without applying a step and refuses a repository without a
  schema version or a newer one, as `EnsureAsync`, which now uses it, does;
  `RepositoryMaintenanceExecutor` (`Raycoon.RayMigrator.Infrastructure`)
  runs the retention templates of one repository without a migration
  context and without the retries of the repository; its
  `LogTableExistsAsync` asks for the log table without a count.
- `TemplateResultCode.OrphanedRunClosed` (1006), the engine code of a run
  that the orphan repair closed. `Repository_MigrationRun_FixOrphaned`
  writes it into `MigrationRun.ErrorCode`; it is never the `ErrorCode` of
  a command's result, and the run history reports it with the run it closed
  (see Changed).

### Changed

- **Breaking (schema):** repository schema version 3. The next command
  applies `Repository_Upgrade_3` (every DAL, idempotent and serialised like
  step 2): the lookup row `Cancelled` (70) in `MigrationRunResult` and four
  indexes for the readers of the repository,
  `ix_{TableBaseName}MigrationRun_Product_Environment_StartedAt`
  (`ProductId`, `EnvironmentId`, `StartedAt`),
  `ix_{TableBaseName}MigrationRecordHistory_MigrationRunId`,
  `ix_{TableBaseName}MigrationRecordHistory_HistorizedAt` and
  `ix_{TableBaseName}MigrationRecord_Product_Environment` (`ProductId`,
  `EnvironmentId`), in snake_case on PostgreSQL, MySQL and MariaDB.
  `RepositorySchema.CurrentVersion` and `RepositoryReadModel.SchemaVersion`
  are 3, `TemplateType` has `Repository_Upgrade_3`; an external DAL ships
  the template too. Engines before this version refuse a repository of
  schema version 3. The data stays.
- **Breaking (API):** `DirectModePipeline.ExecuteAsync` and
  `RayMigratorService.DoWorkAsync` (`Raycoon.RayMigrator.Pipeline`) take the
  `RunCancellation` of the process as their last parameter; the pipeline
  lets Ctrl+C and SIGTERM cancel it while the command runs, and the caller
  disposes it.
- **Breaking (API):** every method of `IMigrationService` takes
  `CancellationToken cancellationToken = default` as its last parameter
  (`GetHistoryAsync(productAlias, limit, cancellationToken)`); callers
  compile unchanged, implementations must add the parameter.
- A migrate-up that stops at a failed file records the engine error code of
  the failure in `MigrationRun.ErrorCode` and in the `ErrorCode` of its
  `MigrationOperationResult`, like a failure outside a file did before, also
  when a cancel stopped its error recovery.
- **Breaking (API / external DALs):** the asynchronous members of `IDal`
  take `CancellationToken cancellationToken = default` as their last
  parameter: `ExecuteNonQueryAsync`, `ExecuteScalarAsync`,
  `ExecuteReaderAsync`, the shared-connection `ExecuteNonQueryAsync` and
  `ExecuteScalarAsync`, and `IsConnectionValid`, which is now
  `IsConnectionValidAsync`. A call that fails after the token was
  cancelled throws an `OperationCanceledException` that carries the token,
  whatever the provider reported (SqlClient, for one, reports a cancelled
  command as a `SqlException`), and starts no further retry. The provider's
  exception is its inner exception, and unless the provider reported a
  cancellation itself, its message names the provider's message, so that a
  database error that met the cancel stays readable.
  `RetryHelper.ExecuteWithRetryAsync` takes the token, waits for a retry
  with it and neither classifies nor retries a failure after the
  cancellation; `DalBase.IsTransient` never counts a cancellation as
  transient. `DalBase` implements the asynchronous members itself and calls
  protected members a DAL overrides instead: `ExecuteNonQueryCoreAsync`,
  `ExecuteScalarCoreAsync` (each also for a shared connection),
  `ExecuteReaderCoreAsync` and `IsConnectionValidCoreAsync`; its retry
  helpers `ExecuteWithRetryAsync` take the token as well. An external DAL
  must be rebuilt against this version: one built against an earlier
  version does not load, and `DalFactory` names the assemblies whose types
  did not load, with their loader exceptions, in its message for an unknown
  database type, which said nothing about them before. The shipped DALs
  pass the token to opening the connection and to the command, not to
  commit and rollback, so a commit that has started completes. The SQLite
  DAL observes it before a batch starts, because Microsoft.Data.Sqlite can
  neither interrupt a statement nor skip the statements left in a batch.
  When a call on a connection of its own fails, times out or is cancelled,
  the MySQL and MariaDB DALs release the `GET_LOCK` locks of the session
  that ran it (`DO RELEASE_ALL_LOCKS()`, best effort, without the token):
  a template that stopped between its `GET_LOCK` and its `RELEASE_LOCK`
  left the lock on the pooled session, and every other host waited for it
  until the pool handed the connection out again or pruned it as idle, up
  to 180 seconds. The templates of earlier versions had the same gap; a
  cancel makes it routine.
  The synchronous `ExecuteNonQuery`, which the database log writer calls
  from its own thread, has no token.
- **Breaking (API):** the repository access is asynchronous. Every public
  method of `TemplateExecutor` is `…Async`, returns a `Task` and takes
  `CancellationToken cancellationToken = default` as its last parameter
  (`RepositoryCheckCreateAsync`, `RepositoryMigrationUpdateAsync`,
  `ExecuteScalarWithNegativeResultCodeExceptionAsync` and so on); it awaits
  the DAL instead of blocking on it. A call that fails after the token was
  cancelled ends with the DAL's `OperationCanceledException` instead of a
  `TemplateExecutionException`. `IRepositorySchemaService.Ensure` is now
  `Task<RepositorySchemaResult> EnsureAsync(RepositoryOptions,
  CancellationToken = default)`, `RepositorySchemaExecutor.Ensure` is
  `EnsureAsync`, and `DatabaseLogWriter.InitDatabaseLogger` is
  `InitDatabaseLoggerAsync`; hosts such as RayMigrator Studio await them.
  `MigrationService` awaits the executor instead of running it through
  `Task.Run`, also in the atomic path, where the repository updates of a
  file share the target's connection and transaction, and the run
  heartbeat writes asynchronously.
- `Repository_MigrationRecord_Select` and
  `Repository_MigrationRecordHistory_Select` (every DAL) return `StartedAt`
  and `FinishedAt`, and `MigrationRecord` carries them as `DateTime?` of
  `DateTimeKind.Utc` on every engine, read as the read model reads the
  dates of the repository (a value without a kind is UTC, SQLite text is
  parsed as UTC). The two selects keep the same
  columns, since `TemplateExecutor.MapMigrationRecord` maps both. The mapping
  takes the dates only when the row has them, so an external DAL whose
  selects lack the columns keeps working: its records map with null dates and
  info reports no `LastMigrationDate` for its target groups. An external DAL
  adds `StartedAt` and `FinishedAt` to both selects.
- **Breaking (external DALs):** every DAL ships the four retention
  templates (`Repository_MigrationRecordHistory_Delete`,
  `Repository_MigrationRecordHistory_Count`, `DatabaseLogging_Delete`,
  `DatabaseLogging_Count`). The `TemplateCache` constructor throws a
  `ConfigurationValidationException` that names the missing files when a DAL
  folder under `DataAccessLayers/` lacks one of them, so a host that has
  such a folder cannot create its template cache and no command starts,
  whatever database it uses. `Database.Example` has stubs for the four and no
  longer the placeholder `Repository_MigrationRecordHistory_Archive.sql`,
  which had no `TemplateType`: the update templates write the history, and
  the retention templates delete it.
- The block counters of a migration record count the leading blocks that
  committed on the target at every moment of a run, so that a run that
  ends abruptly leaves a counter the next start can trust. The record of a
  file that a CLI tool runs counts 0 committed blocks while the tool runs
  (it counted 1 before), and migrate-up neither resumes nor finalizes an
  earlier attempt of such a file, because the tool runs the whole file: an
  older record with committed blocks, for example of an attempt on the DAL
  before the target got the tool, made migrate-up log and report
  (`FileStartedEvent.ResumeFromBlock`) a resume after them, and a record
  that an older engine left `Executing` with every block was finalized as
  `Migrated`; `info` lists such a file as `Failed`. A rollback through such
  a tool reports `FileStartedEvent.ResumeFromBlock` 0 as well, because the
  tool runs the whole rollback file (it reported the rollback blocks of an
  earlier attempt on the DAL). With
  `MigrationErrorAction=Ignore` the record of a
  file with failed blocks counts the contiguous leading blocks that
  committed, the blocks before the first failed one: while the file runs
  (it counted the attempted blocks before), when the file ends and when a
  cancel stops it (both stored the block count of the file before), and
  also when the file fails with an exception, as under every other error
  action. The next migrate-up resumes such a file at its first failed
  block instead of running the whole file again, and runs the blocks after
  it again; when the first block failed, or the SQL blocks of the file
  changed, it runs the whole file; `info` lists such a file as
  `Resumable` after its committed blocks (`Failed` when none committed).
  From this version on, a `FileUpBlocksMigrated` equal to
  `FileUpBlocksTotal` always means that every block committed. With
  `RollbackErrorAction=Ignore` `FileDownBlocksMigrated` counts the leading
  rollback blocks that ran the same way while the rollback runs (it counted
  every rollback block that ran, also after a failed one); the completed
  rollback still stores the block count with `Failed`. A cancel of such a
  rollback keeps the leading rollback blocks as well, so that the next
  migrate-down runs the failed rollback block again; after a failed first
  rollback block it keeps none, and migrate-down does not continue the
  record. A failed atomic
  rollback keeps the rollback blocks of the attempt it continued (it stored
  0, so that migrate-down did not continue the rollback and migrate-up ran
  the file again over a half rolled back target), and a rollback through a
  CLI tool that fails under a `RollbackErrorAction`
  other than `Ignore` leaves its record `Failed` with these rollback blocks
  (it left the record `Executing` in a run that had ended). A resumed file
  carries its committed blocks on its record before its first block runs,
  and a rollback that continues one that stopped in the middle its
  rollback blocks; both counted 0 until the next block committed. A
  rollback through a CLI tool leaves `FileUpBlocksMigrated` of its record
  alone (it set it to 1).
- The orphan repair keeps the progress of a file. When the next migrate-up,
  migrate-down or baseline closes a run that ended abruptly (a crash, a
  kill, a second Ctrl+C or SIGTERM), or `fix` closes it, each `Pending` or
  `Executing` record of that run ends as a failure at the block that was
  running would have left it: `Failed` with its committed blocks when some
  but not all committed, so that the next migrate-up resumes after them and
  runs at most the block that was running again (a resumed file whose run
  ended between the insert of its record and the write of its resume point
  counts no block and runs from its first block); `Migrated` when every
  block committed and only the final status was missing; `NotMigrated`
  without a committed block, as before. A rollback that was running ends
  `Failed` with its rollback blocks, which migrate-down continues while
  migrate-up refuses the file, `Failed` without rollback progress when its
  first rollback block was running, or `NotMigrated` when every rollback
  block ran. Before, the repair set every open record to `NotMigrated`, and
  the next migrate-up ran the file again from its first block, the blocks
  that had committed included. With `MigrationErrorAction=Ignore` a file
  with failed blocks resumes at its first failed block after an abrupt end
  as well. With `--last-migration-status` `migrated` or `not-migrated`,
  `fix` still sets that status on every open record of the run and keeps the
  counters. The repair sets `FinishedAt` of every record it changes to the
  time of the repair where it is empty and leaves `DurationInMs` as it is,
  because the time until the repair is no execution time: a file that was
  running gets the time of the repair without a duration, and a record whose
  rollback was running keeps the end and the duration of its last terminal
  status; a record that the repair finished as `Migrated` therefore counts
  for `TargetGroupStatus.LastMigrationDate`. It writes no history row, as
  before. `info` lists the open records of a run that is still open as the
  repair will leave them (`Resumable` after their committed blocks, not at
  all with every block committed), because the next migrate-up repairs them
  first, and the status counts a record that the repair will finish as
  `Migrated` for its current release and its executed migrations, also per
  target group. The run that the auto-fix lets start takes its
  `FromReleaseVersion` from the records after the repair, and the warning of
  the auto-fix names how many open records it settled.
- **Breaking (behaviour, upgrade):** the orphan repair reads the block
  counters of an open record by the rules of this version, which an engine
  before it did not follow: with `MigrationErrorAction=Ignore` the record of
  a file counted the blocks it attempted, the failed ones included, and the
  record of a file that a CLI tool runs counted 1 block before the tool
  started. A run of such an engine that ended abruptly and is still open
  when this version repairs it can therefore be misread: a record that
  counts every block becomes `Migrated` although a block failed or the tool
  did not finish, and one that counts some blocks resumes after a block
  that failed. Close the open runs with `fix` of the earlier version before
  the update; it sets their open records to `NotMigrated`. When a run of an
  earlier version is still open after the update, close it with `fix
  --last-migration-status not-migrated` before the next migrate-up,
  migrate-down or baseline, whose auto-fix applies the rule; the files of
  its open records then run again from their first block. `info` shows
  beforehand how the repair would settle the open records.
- **Breaking (API / CLI):** `FixIssuesRequest.AssumedMigrationStatus` is
  `MigrationStatus?` and null by default (it was `NotMigrated`): null
  settles every open record of an orphaned run by its progress,
  `NotMigrated` or `Migrated` sets that status on all of them.
  `TemplateExecutor.RepositoryMigrationRecordFixOrphanedAsync` takes
  `MigrationStatus? status` and hands 0 (`Undefined`) to the template for
  null. The CLI option `--last-migration-status` of `fix` has no default any
  more, so `fix` without it keeps the progress, and an unknown value is
  still a parse error; `RayMigratorService` passes
  `FixAssumedMigrationStatus` through without a fallback. A host that leaves
  the status unset, as RayMigrator Studio does when its user chooses none,
  gets the progress rule; a host that wants the old behaviour sets
  `NotMigrated`.
- **Breaking (external DALs):** `Repository_MigrationRecord_FixOrphaned`
  receives `@MigrationStatusId = 0` when the engine leaves the status to the
  template, which the auto-fix always does and `fix` without
  `--last-migration-status` does: the template decides per open record from
  `FileUpBlocksMigrated`, `FileUpBlocksTotal`, `FileDownHash`,
  `FileDownBlocksMigrated` and `FileDownBlocksTotal` by the rule of the
  shipped templates, keeps the counters, sets `FinishedAt` where it is NULL
  and leaves `DurationInMs`; 30, 50 or 100 sets that status on every open
  record. A template of an earlier version writes the status 0 to the
  records, which no command handles; an external DAL ships the rule (the
  stub in `Database.Example` describes it).
- The orphan repair records why it closed a run. The run row that the
  auto-fix of the next migrate-up, migrate-down or baseline, or `fix`,
  closes as `Error` gets the `ErrorCode` 1006
  (`TemplateResultCode.OrphanedRunClosed`) and the `ErrorMessage` `Closed as
  orphaned by the repair: the run ended without finishing; last heartbeat
  <yyyy-MM-dd HH:mm:ss> UTC`, with `LastHeartbeatAt`, or `StartedAt` for a
  run without a heartbeat. Both stayed empty before, so the run history
  (`MigrationRunInfo.ErrorCode` and `ErrorMessage`, the read model) could
  not tell a run that ended abruptly from one that failed. A code or a
  message that the row has already stays. The message is the same for the
  auto-fix and for `fix`. `Repository_MigrationRun_FixOrphaned` of every
  DAL writes both; the template of an external DAL can set them the same
  way (the stub in `Database.Example` describes them), and one that leaves
  them alone keeps working: its run rows stay `Error` without a cause, as
  before.
- A RunAlways file always runs in full: migrate-up no longer resumes it
  after committed blocks of a failed or cancelled attempt and no longer
  finalizes a record of it that committed every block; it runs the file
  from its first block on every target its `Targets` filter selects, and
  `FileStartedEvent.ResumeFromBlock` is 0. The orphan repair settles an
  open record of a RunAlways file by its progress like any other, and the
  file still runs in full afterwards. The pending list reports it as
  `RunAlways` with no `ResumeFromBlock`; only `RollbackIncomplete`
  precedes it.
- `InsertRunningMigrationRun`, `InsertOrphanedMigrationRun` and
  `InsertOpenMigrationRun` of `RepositoryQueryHelper`
  (`Raycoon.RayMigrator.Testing`) write the `MigrationRunMeta` row of the
  run they insert as well, with an empty JSON object as its settings,
  because every run of the engine has one; a test that counts
  `MigrationRunMeta` rows counts it.
- A rollback file that `RequireRollbackFile=true` requires and that is
  missing when a rollback chain reaches it (migrate-down, the error
  recovery of a migrate-up, the rollback of a `MigrationCancelledAction`)
  leaves the record of its file in its status. The chain still aborts
  there, and the run ends as `Error`. The error message of a migrate-down
  and of the rollback of a cancel action names the missing rollback file and
  that the record keeps its status; the error recovery of a migrate-up logs
  it, and its run keeps the message of the failure that started the
  recovery. The record became `Failed` with every block counted and
  no rollback progress before: the next migrate-up ran the whole file
  again over what it had applied, and migrate-down never picked the record
  again. Once the rollback file is there, the next migrate-down rolls the
  file back.
- `TargetGroupMigrationOrder` on a product with one target group:
  migrate-up, baseline and info ignore an order that names that group's
  alias exactly; migrate-up and baseline refused such an order before.
  migrate-up and baseline log a warning for the order of
  `--target-group-migration-order` and of the migsettings of a release, info
  for that of the migsettings when it sorts the pending list, once per
  source and command (`TargetGroupMigrationOrder (source: migsettings of
  release <release>) is ignored: the product has only one TargetGroup
  [<alias>].`); the product setting gets no warning of the commands, because
  the validation at startup reports it as RULE_1_13, which stays a warning,
  says that the order is ignored and is reported only when the order has no
  error. Any other order on such a product (another alias, a case-only
  match, a duplicate, more than one alias) is still refused. The messages
  about an order name where it comes from
  (`source: --target-group-migration-order`,
  `migsettings of release <release>` or `product setting`) instead of the
  command that applies it.
- **Breaking (config):** the validation rules RULE_1_10 to RULE_1_12 compare
  the aliases of `TargetGroupMigrationOrder` exactly, like migrate-up: an
  alias that matches a target group only case-insensitively is an error with
  a hint to the exact alias, without a RULE_1_11 for that group. Such a
  configuration passed the validation before; now every command of its
  product stops at startup, info, validate-hash, update-hash, migrate-down
  and fix included, and the Config Wizard and hosts that validate with the
  rule catalog report the error. migrate-up and baseline refused it already,
  unless `--target-group-migration-order` or the migsettings of the release
  replaced the product setting. The validation checks the product setting;
  the migsettings and `--target-group-migration-order`
  sources are checked by the commands with the same rules.
- migrate-down rolls back a file that stopped after committed blocks. A record
  `Failed`, or `Executing` in a run that has ended, with some or every block
  committed and no rollback attempt (`FileUpBlocksMigrated > 0`,
  `FileDownHash` empty), which a failed, cancelled or abruptly ended
  migrate-up leaves and the next migrate-up would resume, is rolled back like
  a `Migrated` record when its release is after `--to-release`: its rollback
  file runs from its first block, in the order of the chain, and the record
  ends `NotMigrated`, so that the next migrate-up runs the file from its first
  block; `--run-mode simulate` lists it. Before, migrate-down picked only
  `Migrated` records and rollbacks that stopped in the middle and left such a
  file alone: once it had rolled back the files before it, the next migrate-up
  created again what they had created and resumed the file after its committed
  blocks, so what those blocks had written was missing while the run reported
  `Ok` (every version since 0.10.3 for a file that failed under
  `MigrationErrorAction=Terminate`; in this version also after a cancel, after
  ignored failed blocks and after the orphan repair kept the blocks of a
  file). A record without a committed block (`Failed` with 0, as the atomic
  path, a CLI tool and a failed first block leave it) is still not rolled
  back; migrate-down logs a warning (`File not rolled back`) for such a record
  in the range it rolls back, because with `MigrationErrorAction=Ignore` or a
  CLI tool blocks of the file may have run. A rollback file that fails on such
  a file leaves the record as every failed rollback does: `Failed` with the
  rollback blocks that ran, which migrate-down continues and migrate-up
  refuses when some ran. A missing rollback file leaves the record with its
  committed blocks, as it leaves a `Migrated` record, and the file stays
  pending for the next migrate-up, which resumes it unless it runs the file
  in full (a RunAlways file, changed SQL blocks, a CLI tool): a required one
  aborts the chain with a message that names
  the blocks and says that migrate-down rolls the file back once the rollback
  file is there, one that is not required is skipped with a warning that names
  them.
- **Breaking (behaviour):** a rollback file now runs for a file that stopped
  after committed blocks when migrate-down rolls back its release, and for a
  file that ended under `MigrationErrorAction=Ignore`, which the rollback
  chain of a `Rollback`/`RollbackRelease` action now runs, as does that of a
  `RollbackRun`/`RollbackRelease` cancel. A rollback file must tolerate a
  partially applied file (`DROP TABLE IF EXISTS`, `DELETE … WHERE`), as it
  must already for the error recovery of a `MigrationErrorAction` and for the
  rollback of a `MigrationCancelledAction`, which roll back the stopped file
  the same way; a rollback file that does not now fails the migrate-down at
  that file (`Error`), where it succeeded before and left the file's blocks
  behind, and the chain of a run at a file the run left under `Ignore`
  (`Error` instead of `Recovered` or `Cancelled`). A file with `Ignore` in a
  product with `RequireRollbackFile=false` needs a rollback file or
  `StopRollbackOnMissingRollbackFile=false`, otherwise the chain stops at it.
  With `RequireRollbackFile=true` a migrate-down now aborts at a file that
  stopped after committed blocks and whose rollback file is missing, also
  when the migration file and the rollback file were removed from disk,
  where it passed over the record of such a file before.
- The rollback chain of a `MigrationErrorAction` (`Rollback`,
  `RollbackRelease`) and of a `MigrationCancelledAction` (`RollbackRun`,
  `RollbackRelease`) rolls back the files the run left `Failed` under
  `MigrationErrorAction=Ignore` as well: every file the run ran, newest
  first, the stopped file first when it has a record — the files it
  migrated, those it finalized and those that ended with ignored failed
  blocks, with the blocks they committed, also none. `RollbackRelease` takes
  those of the stopped file's release; without a stopped file the release of
  the file the run ran last. `RollbackErrorOnly` and `RollbackFile` still
  roll back the stopped file only. Before, such a file was left `Failed`
  with its committed blocks while the chain rolled back the files before it,
  and the run still reported `Recovered` or `Cancelled` with a clean
  rollback; in 0.19.0 the next migrate-up ran the file again in full, so
  rows it had written into tables the chain did not drop were written twice;
  in this version it would have resumed the file after blocks whose effect
  the rollback had taken away, as migrate-down did until this version (see
  above). A rollback file that is missing or fails for such a file is
  handled as for any file of the chain (`RequireRollbackFile`,
  `StopRollbackOnMissingRollbackFile`, `RollbackErrorAction`), so a run
  whose chain stops there is `Error` (1005 after a cancel) instead of
  `Recovered` or `Cancelled`.

### Fixed

- `RepositoryQueryHelper` (`Raycoon.RayMigrator.Testing`) writes the
  PostgreSQL timestamps of the runs of `InsertRunningMigrationRun` and
  `InsertOrphanedMigrationRun`, and of the environment row they register,
  as `NOW()`, right in a session of any time zone. They were written as
  `NOW() AT TIME ZONE 'UTC'`, a time without zone that a `TIMESTAMPTZ`
  column took as a time of the session's zone, so in a session outside UTC
  a run seemed older or younger by the offset of that zone.
- A CLI tool in `Stdin` mode that exits before it read the whole migration
  file is judged by its exit code: a failure code fails the file with the
  exit code and the error output of the tool, as any other tool failure
  does, and a success code fails it with `CLI tool '<path>' exited with code
  <n> before it read all of '<file>' from its standard input.`, because the
  tool cannot have run the whole file. Before, the write of the file met
  the closed pipe, and its `IOException` failed the file whatever the tool
  reported. Every version with CLI tools in `Stdin` mode has the fault.
- `CliToolExecutor` kills the tool and the processes it started when the
  caller cancels; before, only the timeout killed it and a cancelled run
  left the tool running. A tool that fails, or that did not get the whole
  file in `Stdin` mode, after the cancel counts as cancelled and not as a
  tool failure for `MigrationErrorAction`: Ctrl+C reaches the whole process
  group on Linux and macOS, so the tool gets the signal as well, and the
  cancel may cut off the write of the file. A tool is not started when the
  token is already cancelled, the write to its standard input observes the
  token, and its output is read without the token, so a cancel that arrives
  after the tool got the whole file and
  exited keeps its result.
- A CLI tool in `Stdin` mode no longer hangs a run when it writes output
  while it reads the migration file, and `CliToolTimeoutInSeconds` now also
  covers the write of the file to the standard input of the tool.
  `CliToolExecutor` wrote the whole file before it started to read the
  output of the tool and before the timeout began: a tool that writes
  output as it reads stopped reading once its output pipe was full, so the
  write of a file larger than the pipe buffers never ended, and a tool that
  does not read its standard input held the run until it ended on its own.
  The timeout applied to neither, and a rollback file, which runs its tool
  without the cancellation token, could hang migrate-down, the error
  recovery of a `MigrationErrorAction` and the rollback of a
  `MigrationCancelledAction` until the process was ended. The executor now
  reads the output from the start and ends the write at the timeout as it
  ends the wait for the exit: it kills the tool and throws
  `CliToolTimeoutException`. Every version with CLI tools in `Stdin` mode
  has the fault.
- `TargetGroupStatus.LastMigrationDate` is set: the `FinishedAt` (UTC) of the
  Migrated record of the target group that finished last, null when none has
  an end. It was never set before.
- On SQLite, a migrate-up, migrate-down or baseline that started while
  another run of its product and environment was open failed with a
  template error (`UNIQUE constraint failed:
  MigrationRunMeta.MigrationRunId`) instead of reporting the parallel run,
  as soon as the repository held two runs. `Repository_MigrationRun_Insert`
  took `last_insert_rowid()` as the Id of the new run although the open run
  had kept its insert from adding a row; the function then returned the
  rowid of a row of the template's own state table, 2, and the template
  tried to write a `MigrationRunMeta` row for run 2, which already had one.
  The start therefore did not end with `MigrationAlreadyRunning` (-2), and
  its auto-fix did not close an orphaned run whose heartbeat had gone stale;
  only `fix` closed it. The template now takes the Id only when its insert
  added a row. Every earlier version has the fault on SQLite.
- Two hosts that initialize the database log at the same time against a
  log database without log tables no longer fail on MariaDB (duplicate
  key, 1062) and SQLite (`UNIQUE constraint failed`) because both insert
  the `MigrationEvent` catalogue: `DatabaseLogging_CheckCreate` inserts it
  idempotently there (`INSERT … ON DUPLICATE KEY UPDATE` on MariaDB, which
  tolerates the duplicate key and no other error, `INSERT OR IGNORE` on
  SQLite), and both starts may report that they created the log. SQL
  Server, PostgreSQL and MySQL check for the log tables under their
  initialization lock and were not affected.
- On SQLite, two migrate-up, migrate-down or baseline starts for the same
  product and environment at the same moment could both take the run lock
  and migrate the same targets in parallel. The repository runs its
  templates without a transaction of the driver, so
  `Repository_MigrationRun_Insert` checked for an open run and inserted the
  new run in statements that each committed on their own: a start that
  checked before the other had inserted its run found none and inserted a
  second open run. The template now runs the check, the insert and the
  `MigrationRunMeta` row in one `BEGIN IMMEDIATE` transaction, as the
  schema steps do; the second start waits for the first in the busy
  timeout (the command timeout of the repository) and is refused with
  `MigrationAlreadyRunning` (-2). Every earlier version has the fault on
  SQLite; SQL Server, PostgreSQL, MariaDB and MySQL check under their lock
  and were not affected. Like the shipped DALs, an external DAL serialises
  the check for an open run and its insert.
- Two first starts of a new product or environment at the same moment no
  longer fail on the unique index of `NameLower` (SQL Server 2601,
  PostgreSQL 23505, MySQL and MariaDB 1062, SQLite 19, none of them
  transient). `Repository_Product_CheckInsert` and
  `Repository_Environment_CheckInsert` looked the name up and inserted it
  without a lock, so both starts could find it missing, and the insert of
  the second failed. They now register the name idempotently, and both
  starts get the same Id: SQL Server looks the name up with
  `UPDLOCK, HOLDLOCK` in a transaction with the insert, PostgreSQL inserts
  with `ON CONFLICT (name_lower) DO NOTHING`, MySQL and MariaDB with
  `ON DUPLICATE KEY UPDATE` (which tolerates the duplicate key and no other
  error) and SQLite with `ON CONFLICT ("NameLower") DO NOTHING`, and each
  reads the Id from the table; on MySQL and MariaDB both starts may report
  that they created the name. Every earlier version has the fault. Like
  the shipped DALs, an external DAL tolerates a parallel first
  registration.
- The closing line of a rollback chain in the messages of migrate-down
  (`MigrationOperationResult.Messages`) counted a record that the chain
  skipped for a missing rollback file, or rolled back with rollback errors
  that `RollbackErrorAction=Ignore` skipped, as rolled back:
  `Successfully rolled back n migration(s)`. A chain that passed records with
  a warning now closes with `Rolled back k of n migration(s) cleanly; m with
  a warning`; a clean chain keeps the line.
- The rollback events of the error recovery of a `MigrationErrorAction` and
  of the rollback of a `MigrationCancelledAction` carry the position of each
  file (`FileStartedEvent.FileOrderId`) and, for a file whose rollback file
  is missing, the number of its blocks (`BlocksTotal`), as those of
  migrate-down do. The chain of a stopped migrate-up built its records
  without them, so these events reported 0 and 1. Every version since
  0.17.0, which added the events, has the fault.
- A migrate-up whose file failed before the record of the file was written
  (a repository write at the start of the file failed) no longer breaks the
  rollback of its `MigrationErrorAction`. The chain began with that file
  under record Id 0 and on the target of the file before it: it aborted
  there and rolled back nothing, or, without the rollback file of that
  file, stopped there or skipped it with a warning, and the run ended
  `Error` instead of `Recovered`. A file without a record ran nothing on its
  target, so the chain now leaves it out and rolls back the files the run
  ran in the scope of the action. Every earlier version has the fault;
  before 0.17.0 the chain took the record of the file before it instead.

## [0.19.0] — 2026-10-15

### Added

- `ConfigurationProductScope` in `Raycoon.RayMigrator.Shared` (namespace
  `Raycoon.RayMigrator.Shared.Configuration`): `Isolate` reduces a merged
  configuration to the scope of one product, the shared sections and the
  elements of `Products` whose alias equals the product's (compared like the
  merger compares aliases, without regard to case); the other products and
  elements without an alias are left out, elements that repeat the product's
  alias stay so that the rule catalog reports them. `ProductAliases` lists
  the product aliases of a document. The engine's loader applies it to the
  merged document before a run binds it; hosts such as RayMigrator Studio
  are to apply it the same way.
- `RayMigratorOptionsPreparer.IsolateProduct` reduces options a host built
  in memory to one product the same way, and `ProductAliasGuard.NotFoundMessage`
  builds the message of an unknown product alias from a list of aliases.
- `RayMigratorDataAnnotationValidateOptions` (`Raycoon.RayMigrator.Core`) runs
  the data annotations of the options as the options framework runs them and
  names the elements of a failure by alias
  (`RayMigratorOptions.Products[Crm].TargetGroups[Backend].Targets[Main]`
  instead of `Products[0].TargetGroups[0].Targets[0]`), because the position
  in the reduced list of a run no longer tells which product of the files
  failed; an element without an alias keeps its position. The CLI pipeline
  and `RayMigratorOptionsPreparer` use it in place of
  `ValidateDataAnnotations()`.
- `EncodingSupport.DefaultEncodingName` (`UTF-8`), the encoding of migration
  files when the configuration names none, and
  `RayEncodingAttribute.DefaultValue`, which exposes it for hosts that
  describe the options.

### Changed

- **Breaking (behavior):** a run loads, validates and masks only the
  product it runs for (ADR-026). The CLI reduces the merged appsettings
  hierarchy to the shared sections and the product named by `--product`
  before the configuration is bound, so the placeholder resolution, the
  data annotations, the defaults cascade, the rule catalog, the template
  check and the masker see one product: a missing connection string, an
  unknown database type or an unset `{ENV:...}` variable of another
  product no longer stops the run, and the rule catalog judges duplicate
  aliases, CLI tool references and connection strings within that product.
  The shared sections (`Repository`, `DatabaseLogging`, `ProductDefaults`,
  `Serilog` and every entry of `CliTools`, because a migration file may
  name any of them) are checked for every run as before. A `--product`
  that names none of the configured products now fails while the files
  are loaded, with the message that named the available aliases from the
  migration context before; `Program.Main` prints it on stdout with exit
  code 100, as before, but before Serilog is started, so the log file no
  longer carries it. Files without any product alias are refused later
  by the data annotations as before.
  Elements of `Products` without an alias are ignored by a run, because
  no `--product` can select them; a blank alias counts as none, a numeric
  alias by its text, as the binder reads it. An alias written as a
  `{ENV:...}` placeholder is resolved for the comparison only, the
  configuration keeps the placeholder until the section is resolved; a
  variable that is not set leaves the alias as written, so the message
  of an unknown product shows the placeholder. `IMigrationContextFactory.Create`
  gives options a host built in memory the same scope through
  `RayMigratorOptionsPreparer.IsolateProduct`, on a shallow copy of the
  options with a product list of its own, so the caller's options keep every
  product and a host can create one context per product from them in turn;
  a blank product alias is a `ConfigurationValidationException` there.
  `CreateFromValidatedOptions` applies no scope and takes the options as
  they are. Hosts such as RayMigrator Studio that bind a document of their
  own are to isolate it with `ConfigurationProductScope` before they bind.
- `MigrationFilesEncoding` defaults to UTF-8. A configuration that leaves
  the encoding out on `ProductDefaults` and on a product no longer fails
  the data annotations, and an empty value, which passed before and was
  already read as UTF-8, now gets `UTF-8` written: `[RayEncoding]` writes
  `EncodingSupport.DefaultEncodingName` into a value that is left out or
  empty on both levels, as `[RayRangeInt]` writes its numeric defaults,
  and a named default still cascades into the products. The named `UTF-8`
  decodes like the strict UTF-8 the engine used for an empty value, so no
  hash changes and no `update-hash` is needed. The settings snapshot of a
  run records `UTF-8` where it recorded an empty value before.

### Fixed

- The German help texts (`FieldHelp.de.resx`, `SectionHelp.de.resx` in
  `Raycoon.RayMigrator.Shared`) are written with umlauts and ß instead of
  the transliterations ae, oe, ue and ss.
- The help texts of `Repository.TableBaseName` and
  `DatabaseLogging.TableBaseName` suggest the lowercase prefixes `rm_` and
  `app1_` and name the resulting table names per database. The examples
  `RM_` and `App1_` were refused by `RULE_4_3` on PostgreSQL, MySQL and
  MariaDB, whose table names are snake_case. The example of the repository
  prefix names tables that exist (`app1_MigrationRecord`,
  `app1_migration_record`), and the help of `DatabaseLogging.TableBaseName`
  says that the log table is `migration_log` on PostgreSQL, MySQL and
  MariaDB and that a prefix left out takes `Repository.TableBaseName` when
  the log database has the repository's database type; it said the table
  was `MigrationLog` then.

## [0.18.0] — 2026-10-15

### Added

- `Raycoon.RayMigrator.Shared` carries the help texts of the configuration:
  `ConfigurationHelp` (section and field help in English and German from
  `Configuration/Help/*.resx`), `JsonPathRegistry` (field key to
  configuration path, now with the reverse lookup `TryGetFieldKey`) and the
  records `FieldHelp`, `SectionHelp` and `JsonPathInfo`, all in the namespace
  `Raycoon.RayMigrator.Shared.Configuration.Help`. New field help for
  `Repository.HeartbeatIntervalInSeconds` and
  `Repository.OrphanedRunThresholdInMinutes`. `TryGetFieldKey` finds the key
  of every option of the engine: through the inheritance of the defaults
  (`Products[].MigrationErrorAction` yields the key of
  `ProductDefaults.MigrationErrorAction`, the `TargetDefaults` fields the key
  of the target field that inherits them) and behind aliases and indexes
  (`Products[Shop].Alias`). `DatabaseLogging_Enable` names
  `DatabaseLogging.Enabled`. RayMigrator Studio reads the texts from the
  package; the Config Wizard keeps using them.
- `RayMigratorOptionsValidator.GetReport` is public: a host gets the full
  rule-catalog report (code, severity, path and message of every warning and
  error) of prepared options instead of one failure string.
- `IRepositorySchemaService` (`Raycoon.RayMigrator.Services.Abstractions`,
  registered by `AddRayMigratorServices`) creates or upgrades a repository
  from its `RepositoryOptions` alone: no product, no environment, no
  migration context. `Ensure` validates the options like the context factory
  validates the repository of a run (data annotations with their defaults, a
  DAL for the database type, the schema name against the DAL capabilities),
  resolves `{ENV:VAR}` placeholders with the environment of the process and
  refuses a missing variable like the preparer of a run, runs
  `Repository_CheckCreate`, the missing `Repository_Upgrade_{n}` steps
  and `Repository_MigratorMeta_CheckInsert`, and returns
  `RepositorySchemaResult` (version before and after, `MigratorMetaId`,
  `Created`, `Upgraded`). The rules of the catalog that judge the repository
  alone (RULE_4_2 to RULE_4_5) run before anything is created, so a
  repository the service creates passes the validation of every later run. The steps themselves moved from `TemplateExecutor`
  into `RepositorySchemaExecutor` (`Raycoon.RayMigrator.Infrastructure`);
  `TemplateExecutor.RepositoryCheckCreate` runs them with the values of the
  current context as before; the executor refuses repository options that
  lack their command settings or prefix with an `ArgumentException`. A host
  that managed repositories through a synthetic product can drop it.
- The testing package handles repositories with a table prefix:
  `DatabaseCleanupHelper.CleanDatabase` and `CleanAllDatabases` take the
  `tableBaseName` as an optional last parameter and name the repository
  tables through `RepositoryNaming` in FK-safe drop order (SQL Server also
  drops the foreign keys and tables another prefix left in the schema), and
  `RepositoryQueryHelper` takes the prefix as an optional constructor
  parameter and applies it to every repository table. The engine tests'
  `ScenarioBuilder` has `WithTableBaseName`.

### Changed

- **Breaking (binary):** `DatabaseCleanupHelper.CleanDatabase`,
  `CleanAllDatabases` and the constructor of `RepositoryQueryHelper` in the
  testing package gained an optional `tableBaseName` parameter; code that
  recompiles is unaffected, an assembly compiled against 0.17.0 throws
  `MissingMethodException` until it is rebuilt.
- A repository without a `TableBaseName` runs with no prefix, as documented:
  the preparer of a run writes the empty string into a missing prefix instead
  of leaving the templates with an unreplaced `{CFG:TableBaseName}`.
- **Breaking (schema):** the primary key and foreign key names of the
  repository and log tables carry the table prefix like the index names
  (`pk_{TableBaseName}MigrationRun`, `fk_{TableBaseName}MigrationRun_Product`
  and their snake_case forms; the SQL Server log table's key is
  `pk_{TableBaseName}MigrationLog`). Two repositories with different
  prefixes can now share one database or schema; before, the second one
  failed on the first name that already existed (SQL Server and PostgreSQL in
  `Repository_CheckCreate`, MySQL and MariaDB in step 1). A repository created
  by 0.17.0 keeps its names and keeps working, but it still owns the
  unprefixed key names: a new repository without a prefix in the same schema
  collides with it until the old one is recreated. New rules: RULE_4_4
  (Error), `TableBaseName` has at most 16 bytes in UTF-8 (16 ASCII
  characters), so that the longest prefixed index name fits the identifier
  limit of PostgreSQL, MySQL and MariaDB; RULE_4_5 (Error), `TableBaseName`
  consists of ASCII letters, digits and underscores, the characters that need
  no quoting on any engine (the SQL Server templates write the prefixed names
  without brackets, so a prefix such as `rm-`, which passed before, is refused
  with a message instead of failing inside `Repository_CheckCreate`).
- **Breaking (API):** the Admin-DB bootstrap contract is gone:
  `RayMigratorBootstrapOptions`, `AdminDbOptions` and `OperatingMode` left
  `Raycoon.RayMigrator.Core`, `OptionsSourceResult.PreBuiltOptions` and the
  pre-built branch of `DirectModePipeline` are removed. Nothing read them:
  the engine always loaded the appsettings hierarchy, and RayMigrator Studio
  builds its options in memory and runs them through
  `IMigrationContextFactory` (ADR-023).
- **Breaking (API):** `ContextHelpProvider`, `JsonPathRegistry` and the help
  records left `Raycoon.RayMigrator.ConfigWizard.Core` for the Shared package
  (`ContextHelpProvider` is now `ConfigurationHelp`); the unread
  `ValidationMessages*.resx` resources are gone. The wizard projects are not
  packaged, so no NuGet consumer is affected.

## [0.17.0] — 2026-10-13

### Added

- The repository schema is versioned (ADR-024). The new table
  `RepositorySchemaVersion` records every applied schema version;
  `RepositorySchema.CurrentVersion` in `Raycoon.RayMigrator.Core` names the
  newest one, and every DAL ships one `Repository_Upgrade_{n}` template per
  version, each creating its version on top of the one before, idempotent
  and serialised against a parallel start. `Repository_CheckCreate` now
  creates only the version table and reports the version found;
  `TemplateExecutor.RepositoryCheckCreate` applies the missing steps in
  order, logs a created or upgraded repository at Information level and then
  registers the engine version through the new
  `Repository_MigratorMeta_CheckInsert`, whose result is the
  `MigratorMetaId`. A repository whose schema version is newer than the
  engine supports is refused with the new `RepositorySchemaException`
  before anything is written. New log events
  `TemplateExecutionRepositoryUpgrade` (101) and
  `TemplateExecutionRepositoryMigratorMetaCheckInsert` (102).
- Schema version 2: a run row records what it does and who started it
  where. `MigrationRun` gains `MigrationOperationId`, `MigrationRunSourceId`
  (new lookup table `MigrationRunSource`: Cli = 10, Ci = 20, Studio = 30),
  `InitiatedBy`, `InitiatedBySubject`, `Host`, `ErrorCode`, `ErrorMessage`
  and `LastHeartbeatAt`, all nullable. `RayMigratorConsoleOptions` takes
  `Source`, `InitiatedBy`, `InitiatedBySubject` and `Host` from a host; the
  CLI fills the OS user, the machine name and `Cli`, or `Ci` when one of
  `CI`, `GITHUB_ACTIONS`, `GITLAB_CI`, `TF_BUILD`, `JENKINS_URL` or
  `TEAMCITY_VERSION` is set (`RunInitiator`). A run that does not end Ok
  stores the message of the failure that decided its result, cut to 4000
  characters, and the engine error code when there is one. `MigrationRunInfo`
  reports `Source`, `InitiatedBy`, `InitiatedBySubject`, `Host`, `ErrorCode`,
  `ErrorMessage` and `LastHeartbeatAt`, and its `Operation` comes from the
  run row instead of being derived from the records.
- A running migrate-up, migrate-down or baseline writes a heartbeat into
  its run row (`Repository_MigrationRun_Heartbeat`, every
  `Repository.HeartbeatIntervalInSeconds`, default 30, 0 = off) from its own
  connection; a failed beat is a warning, never the end of the run. An open
  run counts as orphaned when its last heartbeat (or its start, for a run
  without one) is older than `Repository.OrphanedRunThresholdInMinutes`
  (default 2): the next start for the same product and environment closes
  it as Error and takes the run lock, `fix` lists and repairs it, and
  `OrphanedRunInfo` reports `LastHeartbeatAt`, `MinutesSinceHeartbeat`,
  `InitiatedBy` and `Host`. A run that still writes heartbeats is alive and
  blocks a parallel start however long it runs; `fix` refuses to close it
  (`-31`).
- New package `Raycoon.RayMigrator.Repository.ReadModel` (ADR-025): the
  technology-neutral read model of the repository for RayMigrator Studio
  and other readers. `RepositoryNaming` gives the table and column names
  of every engine (PascalCase or snake_case, schema, prefix, quoting) and
  a `SELECT` per table; one positional record per table names the columns
  and their .NET types (`MigrationRunRow`, `MigrationRecordRow`,
  `MigrationLogRow`, `LookupRow`, ...); `RepositoryRowMapper` builds the
  records from ADO.NET rows of any of the five providers;
  `RepositoryReadModel.SchemaVersion` is the schema version of this
  release. The package depends on `Raycoon.RayMigrator.Core` only.
- `DatabaseLogWriter.FlushAsync(timeout)` waits until every queued log
  entry is written and leaves the queue open, for a host that runs several
  migrations in one process; `Flush()` stays the CLI's shutdown call.
- Typed progress events for hosts. `MigrationContext.ProgressObserver` takes an
  `IMigrationProgressObserver` (namespace `Raycoon.RayMigrator.Core.Progress`)
  that receives `RunStartedEvent` (after the run row exists, with its id),
  `FileStartedEvent` and `FileCompletedEvent` for every file on every target
  (migration, rollback and baseline, with the record id and a `FileOutcome`),
  `BlockCompletedEvent` for every executed SQL block, and `RunCompletedEvent`
  with the result. Every completion follows a start for the same file and
  target; a rollback without a rollback file reports `Skipped`; the block
  events of the atomic path follow the commit; and `FromRelease` is reported
  in Simulate and Validate as well. A host shows progress without parsing
  log text. An observer that throws is logged and the run continues. The
  default observer ignores every event; the CLI does not use them.
- `BaselineResult.BaselinedRecords` counts the records a baseline wrote, one
  per file and target that was pending, also when the baseline failed midway.
  `BaselinedFiles` keeps counting files per release and target group.

### Changed

- **Breaking:** repositories created by 0.16.0 or earlier carry no schema
  version and are refused with `RepositorySchemaException`; drop their
  tables once and let this version create them again. From this version on
  a schema change ships as a new `Repository_Upgrade_{n}` step that the
  next command applies, and the data stays. The table count checks of
  `Repository_CheckCreate` and their codes `-10` (`RepositoryIncomplete`)
  and `-11` (`RepositoryPartialWithoutVersionTable`) are gone; `-12` is
  returned by `Repository_MigratorMeta_CheckInsert`. External DALs must
  ship `Repository_Upgrade_1.sql` and
  `Repository_MigratorMeta_CheckInsert.sql`; `TemplateCache` requires 25
  templates per engine (`Repository_Upgrade_2.sql` and
  `Repository_MigrationRun_Heartbeat.sql` included). ADR-007 and ADR-016 are
  superseded in their repository part.
- **Breaking:** database logging is on by default and goes into the
  repository. Without a `DatabaseLogging` section, or with one that leaves
  values out, the database type, connection string, schema and table prefix
  come from `Repository` and `MinimumLevel` is `Information`
  (`DatabaseLoggingDefaults`, applied before validation); `MigrationLog` and
  `MigrationEvent` are created in the repository schema on the first run.
  `"DatabaseLogging": { "Enabled": false }` switches it off, a section with
  another `DatabaseType` describes a separate log database. A section
  without `MinimumLevel` no longer ends the start with an exception.
- **Breaking:** orphaned runs are detected by heartbeat age, not by run age.
  The auto-fix threshold of 10 minutes after the start
  (`AutoFixOrphanedRunsThresholdMinutes`) is gone; `fix --older-than` and
  `FixIssuesRequest.OlderThanMinutes` mean minutes since the last heartbeat
  and default to 2 instead of 60. A healthy run longer than 10 minutes is
  no longer closed by a parallel start, and a crashed pipeline run is
  released after 2 minutes. `Repository_MigrationRun_FixOrphaned` takes
  `@ThresholdMinutes` and `Repository_MigrationRun_SelectOrphaned` returns
  `LastHeartbeatAt`, `MinutesSinceHeartbeat`, `InitiatedBy` and `Host`.
- `IMigrationContextFactory` is the entry point of a host for one run, the CLI
  pipeline included. `Create` prepares options a host built in memory with the
  new `RayMigratorOptionsPreparer`, which gives them what options bound from
  the `appsettings*.json` files always got: `{ENV:VARIABLE}` placeholders are
  resolved in every string of the options graph (a missing variable is an
  error that names the variable and the path), the product defaults are
  merged down, every enum-typed value is checked, the data annotations run
  and write their documented defaults into null properties, and the rule
  catalog is applied. `CreateFromValidatedOptions` skips the preparation for
  options the options framework already validated. Both fill the
  `DalSpecificPropertiesDictionary` for the repository, the product's targets
  and the database logging, check the schema names, and make the context
  current (`IMigrationContextAccessor` and `MigrationLoggingContext`). A host
  that built the context itself got none of this: pre-built options kept null
  timeouts that later failed with an `InvalidOperationException`, every
  engine used the SQL Server block delimiter `GO`, and the Serilog enricher
  saw no run properties. The factory now lives in `Raycoon.RayMigrator.Services`,
  takes the accessor and a logger, and no longer takes a version argument (the
  engine knows its own). The accessor is a singleton in both host modes; the
  API accessor isolates per async flow through its `AsyncLocal` anyway.
  `ProductAliasGuard` explains an unknown product alias with the same message
  the CLI showed, now also for hosts. The preparer checks connection strings
  that contain placeholders as they were written, so a credential a
  placeholder supplies is not reported as hardcoded (#30).
- The result contracts of `IMigrationService` name the repository run and the
  target. `MigrationOperationResult`, `BaselineResult` and `MigrationRunInfo`
  carry `MigrationRunId`, the id of the run row (0 when the run mode writes no
  row); the random `RunId` Guid that was never stored is gone, as is the
  `InitiatedBy` placeholder that nothing ever filled. `MigrationFileResult`
  carries `TargetAlias` and `MigrationRecordId` wherever the entry belongs to
  one target (TargetByTarget order, failures, rollbacks). `MigrationRunInfo`
  carries `Environment` and `FromRelease`; `MigrationHistory`,
  `MigrationStatusInfo` and `BaselineResult` carry `Environment`.
- `MigrateUpAsync`, `MigrateDownAsync` and `BaselineAsync` refuse a request
  whose `TargetReleaseVersion` or `Environment` differs from the
  `MigrationContext`, like the run-mode check does: the run row stamps the
  target release and the environment from the context, so a host that let
  them diverge recorded a release the run never aimed at, or ran one
  environment's files against another environment's rows. The settings
  snapshot of a migrate-up run records the `AllowOutOfOrder` value of the
  request, which is the value the run used.
- `MigrationService` reads the configuration of the current run from its
  `MigrationContext` instead of from `IOptions<RayMigratorOptions>`, and
  `TemplateCache` no longer takes the options: a host builds the cache once
  per process and calls `ValidateConfigurationAgainstTemplateCache` for each
  configuration it runs. A host that creates one context per run from
  pre-built options (RayMigrator Studio) can now execute migrations; before,
  the service read the products and the CLI tools from the options registered
  in the container, so a context built from other options failed with
  "Sequence contains no matching element". The CLI behaves as before. Breaking
  for hosts that construct `MigrationService` or `TemplateCache` themselves.

### Fixed

- A parallel start and the `fix` command close an orphaned run before they
  rewrite its records, and a run that wrote a heartbeat between the orphan
  select and the fix (`-31`) is left alone, records included; a start that
  finds every candidate alive fails with `MigrationAlreadyRunningException`
  as documented instead of a template result error. On PostgreSQL
  `Repository_MigrationRun_FixOrphaned` reports the alive run with
  `RAISE NOTICE` instead of `RAISE EXCEPTION`, so the engine reads the
  `-31` as a result code like on the other engines.
- SQL Server measures the heartbeat age in elapsed seconds, as the other
  engines do. `DATEDIFF(MINUTE)` counted minute boundaries, so a run that
  beat a second before the minute turned was orphaned after a few seconds
  of silence with a threshold of 1, and after 60 seconds with the default
  2. `DurationInMs` of a repaired run uses `DATEDIFF_BIG`, which no longer
  overflows for a run orphaned longer than 24 days.
- A heartbeat does not retry a failed write (the next tick is the retry),
  and the stop of the heartbeat waits at most 10 seconds for a beat in
  progress; a transient-error burst on the repository neither silenced the
  heartbeat of an alive run for minutes nor held the end of the run.
- On MariaDB and MySQL every repository schema template reports `-32`
  (`RepositorySchemaLockNotAcquired`) when `GET_LOCK()` does not answer
  within 60 seconds, and writes neither the version row nor the
  `MigratorMeta` row without the lock; before, the script went on
  unserialised, and two first runs of one engine version could register
  it twice.
- On MariaDB and MySQL a repository whose first schema step lost its
  connection after creating `MigratorMeta` is completed by the next command.
  `Repository_CheckCreate` took every repository with a `MigratorMeta` table
  and no version row for one of a RayMigrator before 0.17.0 and refused it;
  it now does so only when `MigratorMeta` has rows.
- A SQLite script that opens its own transaction (`BEGIN IMMEDIATE`, as
  the repository schema steps do) and fails before `COMMIT` is rolled back
  by the DAL. Microsoft.Data.Sqlite pools the connection, and a handle
  returned with an open transaction kept the file's write lock, so every
  later writer failed with "database is locked" until the process ended.
  Two processes that start on an empty SQLite repository at the same time
  no longer fail: both read the schema version before either applies a
  step, and the second one failed on a column the first had added (SQLite
  has no `ADD COLUMN IF NOT EXISTS`); a step that fails is now accepted
  when a re-read of the version shows it applied.
- The PostgreSQL, MySQL, MariaDB and SQLite DALs substitute template
  parameters into the SQL text, because their providers cannot bind
  parameters in the scripts the templates use. The substitution now scans
  the text once and matches whole identifiers (`DalParameterSubstitution` in
  `Raycoon.RayMigrator.Database.Common`): a value that itself contains
  `@Name` is no longer substituted a second time, which corrupted the value
  and, with a quote in the second value, broke out of the literal; and
  `@Product` no longer touches `@ProductId`. The PostgreSQL DAL no longer
  doubles backslashes, which stored `path\to` as `path\\to` because a
  standard literal treats a backslash as an ordinary character, and it gives
  every `DO $$` block a tag that is unique to the call, so a value that
  contains `$$` can no longer end the block. This affects every free-text
  value the repository stores, such as `MigrationLog.Message` and the
  settings snapshot of a run.
- `GetHistoryAsync` and `GetStatusAsync` read the runs of the requested
  environment only. `Repository_MigrationRun_Select` filtered by product
  alone, so the history listed the runs of every environment of the product
  (with zero counts, because the record history is per environment) and the
  status reported the last run of whichever environment ran last. The
  template takes `@EnvironmentId` in all five engines.
- `MigrationRun.FromReleaseVersion` holds the release the run started from:
  the highest Migrated release of the product and environment, read right
  before the run row is inserted, or `NULL` for a first run. Every run row
  stored the literal text `FromReleaseVersion` before, because the parameter
  was bound to its own name instead of a value.
- `RULE_7_3` (hardcoded credential) no longer fires for a connection string
  that supplies its credential through an `{ENV:VARIABLE}` placeholder. The
  CLI ran the rule catalog on the resolved values, so a placeholder resolving
  to `...;Password=...;` was reported as a hardcoded credential and the hint
  recommended exactly what the configuration already did, while the Config
  Wizard showed no hint for the same files. The validator now checks the
  connection strings as they are written in the files; a literal
  `Password=` next to a placeholder is still reported. `RULE_7_1` and
  `RULE_7_2` keep comparing the resolved values, so two variables that name
  the same database are still detected. (#30)
- An empty or whitespace `--environment` value no longer falls back to
  `DOTNET_ENVIRONMENT`. The resolver reported the variable's value in the
  startup line while the rest of the run used the empty option value: `info`
  and simulate runs showed an empty environment and skipped environment-specific
  files, and `migrate-up` created the repository and then failed on
  `Repository_Environment_CheckInsert`. An empty value now exits with code 3,
  whether or not the variable is set, and the message names the empty argument.
  `DOTNET_ENVIRONMENT` remains a cross-check only (exit code 2 when it differs
  from `--environment`). (#32)
- A loaded configuration without a `Serilog` section still exits with code 4,
  but the message now names the missing section and lists the files that were
  loaded. Before, whenever one of the optional product or product+environment
  files of the `appsettings*.json` hierarchy was absent, which is the case in
  most setups, the message blamed those files and asked to verify the product
  and environment names. The files that were not found are now listed only
  when no loaded file carries a `RayMigrator` section at all, because then a
  wrong product or environment name is the likely cause. (#31)

## [0.16.0] — 2026-10-10

### Added

- The transient error codes a DAL retries ship as an editable
  `DataAccessLayers/{DatabaseType}/TransientErrorCodes.txt` next to the SQL
  templates (one code per line, `#` comments). The file is the complete list
  and replaces the built-in list; a missing file keeps the built-in list, an
  empty file retries nothing and logs a Warning, a malformed file aborts the
  start naming the file and the line. The effective source is logged once per
  DatabaseType. External DAL plugins join in by overriding
  `DefaultTransientErrorCodes` and classifying through `IsTransientCode`.
  Like edited templates, the file is overwritten by every upgrade. (#24,
  ADR-022)

### Changed

- Two decisions an operator should see at the default Information level were
  logged at Debug only: that a run created the repository (schema and tables)
  on first contact, and that a target shares the repository connection and
  therefore runs on the atomic shared connection path, where all blocks of a
  file and the repository update commit or roll back together. Both are now
  one Information line per run. To make the creation detectable, the SQL
  template result contract allows further integer codes between the result
  code and the message (`'code[,code...],message'`); `Repository_CheckCreate`
  returns `RepositoryWasCreated` as its second code in all five databases. A
  customised `Repository_CheckCreate` template in the old `'code,message'`
  format keeps working but produces no "Repository created" line. (#27)

### Fixed

- SQL Server: a migration file that alters the login RayMigrator itself
  connects with (`ALTER LOGIN [sa] WITH DEFAULT_LANGUAGE = German`, a default
  database change, ...) invalidates every pooled session opened before the
  change. SQL Server fails the reset on reuse with error 4021 (*Resetting the
  connection results in a different state than the initial login*) and kills
  the session (596); the repository update after the file, and the database
  log insert, failed with that error even with `DbCommandMaxRetries` set,
  because neither number was classified as transient. Both numbers are now
  retried, and the SQL Server DAL clears its connection pool when either
  appears in the error chain, so the next attempt logs in afresh instead of
  failing once per poisoned pooled connection.

- The RULE_7_3 hint for a hardcoded credential printed its placeholder with
  doubled braces (`{{ENV:VARIABLE}}`) in the CLI log and in the Config Wizard,
  because the message template was used verbatim instead of being formatted.
  It now reads `{ENV:VARIABLE}`. (#26)

- A migration file with `RunAlways = true` in an older release was reported as
  out of order on every `migrate-up` once a newer release had been migrated, so
  every later run aborted unless `--allow-out-of-order` was passed. A RunAlways
  file now counts as out of order only on a target that has never received it;
  once applied, it re-runs on every migration without the flag. (#25)

## [0.15.0] — 2026-09-23

### Changed

- **Breaking (behaviour):** the `appsettings*.json` hierarchy merges arrays
  whose elements carry an `Alias` (`Products`, `TargetGroups`, `Targets`,
  `CliTools`, any future one) **by alias**, case-insensitively and at every
  nesting level: a later file overrides the element with the same alias
  property by property, new aliases are appended after the elements of the
  earlier file, elements the later file does not mention are kept. Every
  other array (`Serilog.Using`, `Serilog.WriteTo`, ...) is replaced as a whole
  by the later file. Until now the .NET configuration provider merged every
  array by position, so an environment file that listed products in another
  order than the base file, or named only the product it changed, attached
  its values (target groups included) to the wrong product; a `migrate-up`
  could migrate another product's database with exit code `0`. Hierarchies
  that relied on positional merging of non-alias arrays (a later file
  changing one `WriteTo` sink and inheriting the others) must now repeat the
  whole array. (#23, ADR-021)
- The Config Wizard imports and merges with the same code as the engine:
  `ConfigFileMerger` delegates to the shared merger, merged alias arrays keep
  the base order (override elements were listed first before), file names are
  classified through the shared chain so that `appsettings.{Product}.json`
  is recognized as a product file when the base file defines that product,
  and files with comments or trailing commas import like the engine reads
  them. (#23)
- **Breaking (behaviour, Config Wizard export):** the wizard places every
  exported value in the highest file in which it holds for every combination
  that file applies to: identical in all product-environment combinations
  into `appsettings.json`, identical within one environment into
  `appsettings.{Environment}.json`, identical across all environments of one
  product into `appsettings.{Product}.json`, otherwise into
  `appsettings.{Product}.{Environment}.json`. No file repeats what the files
  below it already provide, elements of `Products` belong to their product
  (base file or that product's product-environment file), `CliTools` are
  overridden by alias instead of repeated, and files without content are not
  written; with a single combination the export is the base file alone. Until
  now promotion stopped at the environment level and covered a fixed list of
  fields, product-environment files were pruned against the environment file
  before the product file (a wrong value on a conflict), arrays were skipped
  and `CliTools` were repeated on every level. (#23, Part B)

### Added

- `Raycoon.RayMigrator.Shared.Configuration`: `ConfigurationJsonMerger`
  (the merge, the alias-array rule, the shared JSON reader options),
  `AliasComparer` (ordinal, case-insensitive alias equality),
  `ConfigurationFileChain` (the four file names in merge order and the
  inverse classification) and `ConfigFileRole` (moved from the wizard).
  `JsonOptionsSource` merges the existing files as JSON documents and feeds
  one document to the configuration builder; its constructors and
  `LoadAsync` are unchanged, `reloadOnChange` is gone. Golden merge cases
  under `Testing/ConfigMergeCases/` run in both `Tests.Unit` and
  `Tests.Unit.ConfigWizard.Core`. (#23)
- `ConfigurationJsonDiff` in `Raycoon.RayMigrator.Shared.Configuration`
  (`Diff` and `TryCommon`, the inverse operations of the shared merger) and
  `HierarchyFactoring` in `Raycoon.RayMigrator.ConfigWizard.Core`; golden
  export cases under `Testing/ConfigExportCases/` run in the wizard suite
  (files, round trip, no repetition, minimality) and in `Tests.Unit` (the
  engine reads the exported files back to the effective configuration).
  (#23, Part B)

## [0.14.0] — 2026-09-09

### Added

- **Breaking (behaviour):** `MigrationRunResult.Recovered` (80). A `migrate-up`
  that failed and whose configured error recovery (`MigrationErrorAction =
  Rollback`, `RollbackRelease` or `RollbackErrorOnly`) rolled back without a
  failure or warning is now persisted as `Recovered` instead of `Error`: every
  record the recovery touched is `NotMigrated` again, repository and database
  are consistent. `Terminate`, a failed rollback block, a missing rollback
  file and a stopped chain keep `Error`. `info` shows the value in the run
  history. The CLI exit code stays 1. (#18)

### Changed

- **Breaking (CLI / API):** `fix --dry-run` is replaced by `fix --run-mode
  simulate`, the preview every other command already uses: `simulate` lists
  the orphaned runs that would be repaired, writes nothing and leaves no
  DatabaseLogging rows; `migrate` (default) repairs; `validate` is rejected
  for `fix`. The parallel plumbing is gone: `RayMigratorConsoleOptions.FixDryRun`,
  `FixIssuesRequest.DryRun` (now `RunMode`, which must match the context like
  every other request), `FixIssuesResult.WasDryRun` (now `WasSimulated`) and
  the `fixDryRun` parameter of `MigrationCommandExtensions.GetProfile`. (#22)
- **Breaking (CLI):** `validate-hash --scope` accepts `sqlblocks` only, the
  lower-cased enum name like every other CLI value; the former second spelling
  `sqlblock` is rejected with `Allowed values: [file, sqlblocks, disabled]`.
  (#21)
- Builds outside the release workflows identify themselves as pre-releases of
  the version RayMigrator is working towards, with the commit hash:
  `0.14.0-dev+39b6fa3` (`VersionSuffix` `dev` in `Directory.Build.props`, the
  hash the SDK already appends to the InformationalVersion, shortened to seven
  characters by `AssemblyInfoHelper.FormatVersion`). Release builds keep the
  bare tag version. The string appears in the banner, the start-up log, the
  settings snapshot and `MigratorMeta.RayMigratorVersion`, so a repository
  created by a develop build names the exact commit whose templates created
  it. `RayMigratorVersion` in `Directory.Build.props` and the Licensed Work
  line in `LICENSE.md` are now bumped right after a release instead of right
  before it.
- **Breaking (behaviour, hosts):** `AssemblyInfoHelper.GetRayMigratorVersion`
  reads the InformationalVersion of the engine assembly instead of the entry
  assembly. RayMigrator Studio, which passes this value to
  `IMigrationContextFactory.Create`, therefore records the engine version in
  `MigratorMeta` instead of its own; until now a Studio-managed repository
  claimed to be created by RayMigrator "0.9.x".
- **Breaking (behaviour):** repositories and log databases are no longer
  upgraded in place. `Repository_CheckCreate` of all five DALs writes the
  lookup master data (`MigrationRunMode`, `MigrationOperation`,
  `MigrationRunResult`, `MigrationStatus`) only when the repository is
  created: the blocks that back-filled `Baseline` (110), `PartialSuccess` (50)
  and `Recovered` (80) into existing SqlServer and PostgreSQL repositories are
  gone, and MySql, MariaDb and Sqlite no longer re-seed on every start
  (`INSERT IGNORE` / `INSERT OR IGNORE` replaced by inserts gated on the
  pre-DDL existence check). `DatabaseLogging_CheckCreate` writes the
  `MigrationEvent` catalogue only when the log tables are created; the
  catalogue upgrade of 0.13.0 (missing rows, rename of id 100, removal of
  id 32) is gone. A repository created by an earlier version must be dropped
  and recreated. A log database created before 0.13.0 keeps
  its old catalogue names until it is recreated. External DALs: no upgrade
  blocks are required in the "already exists" branch. (#6, #12, #18)
- **Breaking (behaviour):** the config hash of a migration file without a
  TOML block is stored as NULL instead of "", and the read side no longer
  maps "" to null (`TemplateExecutor.NormalizeConfigHash` and
  `MigrationService.ConfigHashesEqual` are removed). On a repository written
  by 0.12.0 or 0.13.0, `update-hash` reports every TOML-less file as updated
  once and then converges; `migrate-up` is not affected because the config
  hash takes no part in the "already migrated" decision. (#9)
- **Breaking (schema):** `MigratorMeta` lost the `CreatedByRayMigratorVersion`
  column and the hand-maintained `RepositoryVersion` constant. The remaining
  version column is `RayMigratorVersion` (`raymigrator_version` on
  PostgreSQL/MySQL/MariaDB) and holds the RayMigrator version that used the
  repository: the first row is the version that created it and therefore
  identifies the schema, every later row is a version that ran on it. The
  Note4 header invariant and its two unit tests are gone, the `-12` result
  message names `RayMigratorVersion` and `RepositoryDatabaseType` only, and the
  SqlServer primary key is `pk_MigratorMeta`. Existing repositories must be
  dropped and recreated. (follow-up to #6, #12, #18)
- **Breaking (config):** the former `TargetMigrationOrder` names `Simultaneously`
  and `Successively` are no longer accepted as aliases; a configuration that
  still uses them fails validation with the usual `Allowed values:
  [FileByFile, TargetByTarget]` error. The alias mechanism
  (`EnumAliasAttribute`, `EnumTypeExtensions.Aliases`) is removed, so the
  settings snapshot, the log output and the wizard export can only ever
  contain the current names. (#19)
- **Breaking (config):** `CliTools[].InputMode` is required; the silent
  `File` default is gone. A tool without it fails validation (`RayEnum` in
  Core, new RULE_3_11 in the Validation project and the wizard). (#19)
- The parallel-run guard in `Repository_MigrationRun_Insert` (all five DALs)
  no longer filters on `MigrationRunModeId`, which is always 100; it blocks a
  second run for the same product and environment while one is unfinished,
  and its error message says so. (#19)

### Fixed

- The result of the error-recovery rollback chain was discarded by
  `HandleMigrationError`, so a clean rollback and an aborted one were
  indistinguishable in the repository. (#18)
- Documentation: `error-handling.md` still described an `Ignore` run as
  `Error`; the enum tables in several documents lacked `PartialSuccess`. (#18)

## [0.13.0] — 2026-09-09

### Added

- **Breaking (behaviour):** `MigrationRunResult.PartialSuccess` (50). A `migrate-up` that continued
  past a failed file with `MigrationErrorAction = Ignore`, and a
  `migrate-down` that skipped a missing rollback file
  (`RequireRollbackFile = false`) or ignored a failed rollback
  (`RollbackErrorAction = Ignore`), are now persisted as `PartialSuccess`
  instead of `Error` / `Ok`. `info` shows the value in the run history. The CLI
  exit codes are unchanged (1 for migrate-up, 0 for migrate-down). The
  repository lookup table is upgraded idempotently on the next start. (#18)

### Changed

- **Breaking (API):** `TargetMigrationOrder` members are named after what they do: `FileByFile`
  (each file is applied to every target of the group before the next file,
  formerly `Simultaneously`) and `TargetByTarget` (all files on one target
  before the next target, formerly `Successively`, the default). Nothing runs
  concurrently in either order, which the old names suggested. The former
  names are still accepted in `appsettings.json` and in the configuration
  wizard as aliases; everything RayMigrator writes (settings snapshot, log
  output, wizard export) uses the new names. (#19)
- **Breaking (API):** enum housekeeping (#19). `CommandProfile` moved from
  `Raycoon.RayMigrator.Core.Configuration.Enums` to
  `Raycoon.RayMigrator.Core.Configuration` and lost its two fields without a
  consumer (`ExecutesMigrations`, `ReadsRepository`); `MigrationEvent` moved to
  `Raycoon.RayMigrator.Core.Logging`. `MigrationRunModeExtensions` gained
  `ShouldConnectToTargets()`, which the profile uses instead of a raw
  comparison. The never-assigned `MigrationState.MigrationStatus` property was
  removed. `CliToolOptions.DefaultInputMode` names the input mode of a tool
  without `InputMode`. `OperatingMode`, `RayMigratorBootstrapOptions` and
  `AdminDbOptions` stay as the contract consumed by RayMigrator Studio; their
  documentation now says that the Engine never reads them. The SQL template
  headers no longer list `10=Validate, 20=Simulate` for `MigrationRunModeId`,
  which is always 100 because those run modes never write rows.
- **Breaking (API):** the enum behind `fix --scope` is named `FixScope` (formerly `FixIssues`, a
  name shared with the command, the request and the service method), and the
  console option `RayMigratorConsoleOptions.FixIssues` is `FixScope`. The CLI
  values `orphanedruns` and `all` are unchanged. (#16)

### Fixed

- `fix --scope all` was a synonym for `orphanedruns`: the scope was logged and
  never evaluated. The service now expands the scope into the repairs to run
  (`All` = every known repair, currently the orphaned-run repair), reports
  them in the result (`FixIssuesResult.Repairs`) and the log, and rejects an
  unknown scope instead of running the default repair. (#16)
- **Breaking (API / external DALs):** the `info` run history now shows what a `migrate-down` run did. Records
  rolled back by `migrate-down` are stamped `MigrateDown`, records rolled back
  by the error recovery inside a `migrate-up` are stamped `Rollback`, and the
  `MigrationRecordHistory` row written for the transition carries the run that
  caused it. The history is built from those rows (new template
  `Repository_MigrationRecordHistory_Select`), so a migrate-down run lists the
  records it rolled back and the migrate-up run that created them keeps its
  count. Until now a migrate-down run appeared as an empty `MigrateUp` run,
  and `MigrationOperation.MigrateDown` / `Rollback` were never written. No
  schema change: the record keeps the `MigrationRunId` of the run that created
  it. External DALs must add `Repository_MigrationRecordHistory_Select.sql` and
  the two new parameters `MigrationRunId` / `MigrationOperationId` of
  `Repository_MigrationRecord_UpdateRollback.sql` (see the five shipped DALs). (#13)
- `info` reads `LastRunResult` and the last migration date from the newest
  `MigrationRun` row instead of deriving them from an arbitrary migration
  record; a failed migrate-down no longer shows `Ok` and a clean one no longer
  shows `Error`. (#14)
- DatabaseLogging now stores the EventId of every logger call in
  `MigrationLog.MigrationEventId`. The sink read a Serilog property that is
  never produced (`EventId_Id`), so every row carried 0 (`UnspecifiedEvent`)
  and the `MigrationEvent` lookup table could never be joined meaningfully.
  The lookup table seeded by `DatabaseLogging_CheckCreate` now matches the
  `MigrationEvent` catalogue in code (id 100 is
  `TemplateExecutionRepositoryCheckCreate`, the sixteen template-execution
  events 110 to 136 are present, the orphan id 32 is gone); existing log
  databases are upgraded idempotently on the next start. The event names of
  the four template events whose name differed from their constant now equal
  the constant (`TemplateExecutionRepositoryProductSelect` and siblings).
  `MigrationEvent` is a static class and the unused
  `MigrationState.MigrationEvent` property was removed. External DALs must
  ship the updated catalogue in `DatabaseLogging_CheckCreate.sql`. (#12)
- Block-level resume no longer skips the block that failed. A `Failed`
  `MigrationRecord` now stores the number of blocks that are committed on the
  target in `FileUpBlocksMigrated` (0 when the file ran in one transaction that
  was rolled back), and the next `migrate-up` resumes with the first block that
  is not. Until now the record stored the 1-based index of the failing block,
  so the resume started one block too late and the file was marked `Migrated`
  although that block never ran. Records written under
  `MigrationErrorAction = Ignore` keep storing the total and are re-executed as
  a whole, as before. (#11)
- `RollbackErrorAction = Ignore` is now honoured when rollback files are
  executed through a CLI tool (`UseCliToolAlias`). A non-zero exit code marks
  the record `Failed` and the rollback chain continues with the next file,
  exactly like a failed block on the DAL path. Until now the CLI tool's
  exception aborted the whole chain regardless of the setting, so `Ignore`
  behaved like `Terminate`. (#15)
- **Breaking (behaviour):** enum values in migration file headers and `migsettings.txt`
  (`MigrationErrorAction`, `RollbackErrorAction`) now follow the same rule as
  `appsettings.json`: only the member names are accepted, case-insensitively.
  Numeric values such as `21`, values that are no member at all such as `99`,
  and comma-separated lists such as `Terminate,Ignore` are rejected with a
  `MigrationFileParsingException` that lists the valid values; until now they
  were accepted and reached the executor as undefined values. The `--scope`
  parsers of `validate-hash` and `fix` throw on an unknown value instead of
  silently falling back to `file` / `orphanedruns`, `GetProfile` rejects
  `MigrationRunMode.Undefined` for the migrate commands, and `MigrateUpAsync`
  / `MigrateDownAsync` reject a request whose run mode is `Undefined` or
  differs from the `MigrationContext`. (#17)

## [0.12.0] — 2026-09-08

### Added

- The TOML `Targets` key (migration file header or `migsettings.txt`) now
  restricts a file to the named targets of its TargetGroup: the file is
  executed by `migrate-up`, recorded by `baseline` and counted as pending by
  `info` on those targets only, the other targets of the group skip it and
  get no `MigrationRecord` for it. `null`, `[]` and `["*"]` keep their
  meaning (all targets). An alias that is not a target of the file's
  TargetGroup, or that differs from the configured alias only in case, is a
  configuration error reported during file discovery, before anything is
  executed (`ConfigurationValidationException`). Until now
  the key was parsed, inherited across all settings levels and stored in
  `FileUpConfigJson`, but ignored at execution time, so a file with
  `Targets = ["Main"]` ran on every target. (#10)

### Fixed

- `info`, `update-hash`, `fix` and `validate-hash` no longer require every
  target database to be reachable at start-up. The start-up connection check,
  the DatabaseLogging sink and the product/environment bookkeeping used to be
  steered by the hidden `MigrationRunMode` each CLI handler assigned to its
  command (`Migrate` for `info`, `Validate` for `validate-hash`), so a
  read-only status query aborted when a target it never touches was down,
  every `info` call filled the audit log while `validate-hash` left no trace,
  and a read-only command on a fresh repository registered product and
  environment as a side effect. Side effects now follow a per-command
  `CommandProfile` (`MigrationCommandExtensions.GetProfile()`): `info` and
  `validate-hash` no longer write DatabaseLogging rows and no longer insert
  product/environment rows, `fix --dry-run` no longer writes DatabaseLogging
  rows either, while `update-hash`, `baseline` and `fix` still do.
  Baseline runs are shown as `Baseline` under **Operation** in the `info`
  run history instead of `MigrateUp` (new `MigrationOperation.Baseline = 110`,
  seeded into the `MigrationOperation` lookup table of every DAL; existing
  repositories receive the row on their next run). Error-recovery rollbacks
  take the run mode from the request that started the run instead of the
  context. (#6)

- `validate-hash` compared the files on disk against an empty record set:
  the repository query used the command's own `Validate` run mode as the
  `MigrationRunModeId` filter, but records are only ever written in
  `Migrate` mode. Every file was therefore reported as `New`, the
  `Valid`/`Modified`/`Missing` counters stayed at zero and the exit code
  was always `0`, so modified or deleted migration files were never
  detected. `TemplateExecutor.RepositoryMigrationSelect()` now always
  reads `Migrate`-mode records regardless of the run mode of the calling
  command; the `overrideRunMode` parameter that `migrate-up`/`migrate-down`
  used to pass is gone. The CLI wrapper also treated a validation result
  with hash issues (`Success = false`, no error message) as a command
  failure and aborted with `Validate-Hash failed … : null` before listing
  the issues; it now prints the counters and every `Hash issue` line and
  returns exit code `1` for `Modified`/`Missing` files. Teams that gate CI
  on `validate-hash` had no coverage before this fix. (#5)
- `--run-mode simulate` queried the repository with `ProductId = 0` and
  `EnvironmentId = 0`: the two ids are assigned by the `*_CheckInsert`
  templates, which Simulate correctly skips because they insert. The
  record query therefore never matched, `migrate-up` previewed every file
  as "Would execute" (including long-migrated ones) and `migrate-down`
  always reported "No migrations found to roll back". Simulate now resolves
  both ids through two new read-only templates,
  `Repository_Product_Select` and `Repository_Environment_Select` (added to
  all five DALs and to `Database.Example`; the required template count per
  DAL is now 20), and reads the same records as Migrate mode. On a
  repository that has no product/environment row yet it logs that and keeps
  today's "everything is pending" behaviour. Because Simulate now sees the
  real records, the out-of-order check applies to it as well: a Simulate run
  fails on an out-of-order file unless `--allow-out-of-order` is given, exactly
  like Migrate. External DALs must add the two templates, otherwise
  `TemplateCache` reports them as missing at startup. (#7)
- In a TargetGroup with several targets, "already migrated" was decided per
  file: the first `Migrated` record of any target marked the file as done for
  all targets. A file that succeeded on one target and failed on another was
  never retried on the failed target (`migrate-up` reported "already applied",
  `info` showed 0 pending, `baseline` skipped it), and the lagging target
  stayed behind silently. The decision is now made per `(file, target)` pair:
  `migrate-up` executes a file only on the targets that still need it,
  `baseline` marks only those pairs, `info` counts pending pairs (a modified
  file now also counts as pending, matching what `migrate-up` would do),
  `validate-hash` compares the hash of every target's record and reports a
  deleted file once instead of once per target, and the out-of-order check
  compares a file's release with the highest migrated release of each pending
  target so that a lagging target catching up is not blocked. With a single
  target per TargetGroup nothing changes. (#8)
- `update-hash` reported and wrote an update for every migration file
  without a TOML header on every run: the repository stores a missing config
  hash as an empty string while a parsed file carries `null`, and the two were
  compared with `!=`. Stored config hashes are now read back as `null` when
  empty and the comparison treats both as "no TOML block", so `update-hash`
  converges (`Updated: 0` on an unchanged repository). In TargetGroups with
  several targets `update-hash` updated only the first target's record and
  left the others stale; it now updates the record of every target and
  reports `UpdatedFiles` (files) and `UpdatedRecords` (file/target pairs).
  A deleted file is counted as removed once instead of once per target. (#9)
- Migration files were decoded with a replacing decoder: bytes that are not
  valid for the configured `MigrationFilesEncoding` (typically a windows-1252
  file with umlauts under the `UTF-8` default) were silently turned into
  U+FFFD, executed in that garbled form and hashed in that garbled form, so
  the data on the target was wrong and `validate-hash` still reported `Valid`.
  Files are now read strictly: an invalid byte sequence aborts the run before
  any file is executed with a `MigrationFileParsingException` that names the
  file, the encoding, the offending bytes and the byte offset. A byte-order
  mark (UTF-8, UTF-16 LE/BE, UTF-32 LE/BE) is detected per file, overrides
  the configured encoding and is never part of the hashed text, so re-saving a
  file with or without BOM does not change its hash. Code-page encodings such
  as `windows-1252` are accepted directly; the CodePagesEncodingProvider is
  registered by the console host, the ConfigWizard and the engine itself, so
  the "register the provider" advice is gone from the error message and the
  docs. The ConfigWizard's field help advertised `UTF-8-BOM`, which its own
  validator rejected; it now lists real encoding names and explains that
  `UTF-8-BOM` and `ANSI` are not encodings. `migsettings.txt` files are always
  read as UTF-8 (a BOM is accepted) regardless of `MigrationFilesEncoding`.
  In CLI-tool `Stdin` mode the SQL is now piped as UTF-8 without BOM instead
  of the console input code page (cp850 on a classic Windows console), which
  garbled non-ASCII characters on the way to `psql`/`mysql`. (#4)

### Changed

- **Breaking (behaviour):** migration files and `migsettings.txt` files that
  already carry a restrictive `Targets` value stop running on the targets they
  do not name after this upgrade; the value used to be ignored. Anyone who
  used the key as pure documentation must widen it to `["*"]` or remove it. A
  value that names a target alias which does not exist in the TargetGroup now
  aborts the run instead of being stored silently. (#10)
- `validate-hash` now runs in `Migrate` mode like the other non-migrate
  commands; `--run-mode` is a `migrate-up` / `migrate-down` concept and no
  longer leaks into other commands (no user-visible option changes).
  `ParseRunMode()` throws for an unknown value instead of falling back to
  `Migrate`; a `MigrationContext` built with `RunMode = Undefined` or
  `Command = None` is rejected; `IMigrationContextFactory.Create` takes the
  `MigrationCommand` instead of hard-coding `MigrateUp`. The DatabaseLogging
  sink gates on the enricher's new `DbLogEnabled` property instead of
  `RunModeId`; the `info` history header column `Command` is now
  `Operation`. (#6)
- **Breaking (behaviour):** a migration file that contains bytes invalid for
  its encoding is now a hard error instead of being executed with replacement
  characters. Repositories that were migrated with such files carry the hash
  of the garbled text; after correcting `MigrationFilesEncoding` run
  `update-hash` once to store the hash of the correctly decoded text. (#4)
- **Breaking (API / external DALs):** `IMigrationContextFactory.Create` takes
  the `MigrationCommand` before the run mode, and a `MigrationContext` with
  `RunMode = Undefined` or `Command = None` is rejected;
  `TemplateExecutor.RepositoryMigrationSelect()` lost its `overrideRunMode`
  parameter; the update-hash result reports `UpdatedRecords` next to
  `UpdatedFiles`; every DAL must ship the two new templates
  `Repository_Product_Select` and `Repository_Environment_Select` (required
  template count 20), otherwise `TemplateCache` reports them as missing at
  startup, and its `Repository_CheckCreate` must seed `MigrationOperation`
  row 110 (`Baseline`). (#6, #7, #9)

## [0.11.1] — 2026-09-05

### Changed

- **Breaking (Linux/macOS):** the CLI executable is now shipped as
  lowercase `raymigrator` (`raymigrator.exe` on Windows) instead of
  `RayMigrator`. `AssemblyName` changed accordingly, so the managed
  assembly, `deps.json` and `runtimeconfig.json` are renamed as well and
  `--help` now prints `raymigrator [command] [options]`. Pipelines that
  invoke `RayMigrator ...` on case-sensitive filesystems fail with
  "command not found" after upgrading; on Windows the old spelling keeps
  working because `PATH` resolution is case-insensitive. Release archive
  names (`RayMigrator-<version>-<rid>.*`) are unchanged. All documentation
  and the ConfigWizard field help use the new name. (#1)
- **Breaking (all platforms):** the seven CLI command verbs are now
  kebab-lowercase: `migrate-up`, `migrate-down`, `validate-hash`,
  `update-hash`, `info`, `baseline`, `fix`. `System.CommandLine` matches
  command names case-sensitively and no aliases for the old PascalCase
  spelling are registered, so `Migrate-Up` etc. are rejected on Windows
  too. The `--TargetGroup-MigrationOrder` option on `migrate-up` and
  `baseline` is renamed to `--target-group-migration-order` (the `-tgmo`
  short alias is unchanged). Option values such as `--run-mode migrate`
  and `--scope sqlblock` were already accepted in any case; help text,
  validation messages and documentation now show them in lowercase.
  Every pipeline invoking the CLI must be updated in the same deployment.
  (#2)
- An enum-typed configuration value (`MigrationErrorAction`,
  `RollbackErrorAction`, `TargetMigrationOrder`, `HashValidationScope`,
  `InputMode`) that cannot be parsed now fails with a
  `ConfigurationValidationException` instead of silently falling back to
  `Undefined` / `File`. In JSON mode such values were already rejected at
  startup; the report now comes from `ProductDefaultsPostConfigureOptions`
  (one message listing every invalid value with its location, same wording
  as `RayEnumAttribute`) rather than from the `OptionsValidationException`
  of the data-annotation step, because that step reflects over the `*Enum`
  getters and would otherwise surface only the first failure. Pre-built
  options (Admin-DB mode) are probed the same way before the host starts.
  The defaults merge applies the same rule, so `Undefined`, numeric strings
  and comma lists are neither copied to products nor accepted. The
  ConfigWizard's own enum checks are now case-insensitive as well. (#3)

### Fixed

- Configuration enum values are now parsed case-insensitively, matching
  what `[RayEnum]` validation has always accepted. A case variant such as
  `"MigrationErrorAction": "rollback"` no longer passes validation and then
  silently degrades to `Undefined` at runtime (which behaved like
  `Terminate`, skipped the pre-flight rollback checks, kept hash validation
  on for `"disabled"`, ran target groups serially for `"simultaneously"`
  and passed a file path to a CLI tool configured with `"stdin"`). The
  shipped examples and the ConfigWizard keep emitting canonical PascalCase;
  lowercase is tolerated, not preferred. (#3)

## [0.11.0] — 2026-08-17

### Licensing

- Switched from RayMigrator Dual License Agreement (RMLA) v1.0 to
  Business Source License 1.1 (BUSL-1.1) with a custom Additional Use Grant.
  Versions less than or equal to 0.10.3 remain under RMLA v1.0.
- **Unconditional Additional Use Grant.** Production use of this version is
  free of charge for everyone, for any purpose. No organization-size
  threshold, no restriction by legal form or sector, no internal-use
  requirement, and no restriction on offering the Licensed Work to third
  parties as a hosted, SaaS, or managed service. Non-production use is free
  under the BSL grant itself.
- BUSL-1.1 applies separately to each version; the Additional Use Grant
  above is the one distributed with this version. Future versions carry
  their own `LICENSE.md` and may grant different terms.
- Removed from the Additional Use Grant, all obsolete under the
  unconditional grant: the Eligible Licensees clause (§ 14 / § 13 BGB), the
  eligibility categories and thresholds, the internal-use and
  no-third-party-offering conditions, the Anti-Circumvention block, the
  Conflict Rule, the Doubt clause, and seven now-unused definitions.
- Consumers (§ 13 BGB) are no longer excluded from production use. Added a
  Supplemental Terms clause under which the statutory rules on liability and
  limitation periods apply to consumers in place of the liability cap, the
  backup obligation and the shortened limitation period (§ Liability
  §§ 4, 5 and 6).
- Reworded the backup obligation (Supplemental Terms § Liability § 5) to
  refer declaratorily to § 254 BGB rather than fixing the legal consequence.
- Added Supplemental Terms (governing law, liability, termination details)
  to align the license with mandatory provisions of German law without
  modifying the BSL 1.1 License Text itself (BSL 1.1 Covenant 4).
- Tightened Trademark Reservation: **modified or derivative** versions must
  adopt a distinct name not derived from the "RayMigrator" / "RAYCOON" Marks.
  Verbatim redistribution of unmodified copies under the original name,
  package identifiers, binary names, configuration keys, and CLI command
  names is expressly permitted — the reservation does not restrict the
  redistribution right the BSL grants.
- Moved the Definitions, the Database.Example carve-out and the Trademark
  Reservation out of the Additional Use Grant into the Supplemental Terms.
  The Additional Use Grant now consists of the grant itself and nothing
  else, so it cannot be read as imposing an additional restriction on the
  rights granted by BSL 1.1 (BSL 1.1 Covenant 2(a)).
- Change Date: four (4) years after each version's first publicly available
  distribution — per-version, see `LICENSE.md` Parameters. First
  publication dates are tracked in `Docs/license-change-dates.md`.
- Change License: Apache License, Version 2.0.
- The `Raycoon.RayMigrator.Database.Example` skeleton project is licensed
  permissively under the MIT License (see its own `LICENSE.md`) so external
  developers can copy it as a starting point for their own DAL plugins.
- Liability cap for non-consumer licensees set to a flat **EUR 10,000 per
  calendar year** (see `LICENSE.md` Supplemental Terms § Liability § 4). The
  previous two-branch cap referencing fees paid under a commercial license was
  removed along with the paid tier. The reference period is the calendar year
  rather than a "contract year", because the grant is perpetual, unconditional
  and free of charge and therefore has no contract term to count from.
- Trademark status: the names "RayMigrator" and "RAYCOON" are claimed
  as **unregistered** trademarks; no DPMA/EUIPO filing has been made.
- Added a **Precedence over Accompanying Materials** clause to the
  Supplemental Terms: `LICENSE.md` is the sole statement of the licensing
  terms, and statements in the website, README files, package metadata,
  changelogs, documentation or marketing are informative only and are not
  guarantees. This replaces the Conflict Rule that was removed with the
  conditional Additional Use Grant and closes the gap it left.
- Added a **Contractual Penalty** clause to the Supplemental Terms, covering
  breaches of the Trademark Reservation and of the BSL notice obligation.
  The amount is set by the Licensor at reasonable discretion under § 315 BGB
  and is reviewable by the court, rather than being a flat figure — a single
  flat penalty applied to breaches of differing weight is unenforceable in
  German standard business terms. The clause does not apply to consumers.

### Documentation

- Added a pre-1.0 maturity notice to `README.md`, `NUGET_README.md`,
  `NOTICE.md`, and the CLI startup banner.
- `COMMERCIAL-LICENSE.md` reframed as a licensing overview: no commercial
  license is required for this version; RayMigrator Studio remains a
  separately licensed product.
- Added `Docs/license-change-dates.md` as the per-version Change Date
  register.

### Security

- Bumped `Microsoft.Data.Sqlite` from 10.0.5 to 10.0.11, resolving
  **CVE-2025-6965** (GHSA-2m69-gcr7-jv3q, CVSS 7.2 High). The 10.0.5 line
  pulled `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, which bundles a SQLite build
  older than 3.50.2 — in those builds the number of aggregate terms can
  exceed the number of available columns, causing memory corruption.
  10.0.11 moves the SQLitePCLRaw chain to 2.1.12, above the affected range.
  `dotnet list package --vulnerable --include-transitive` now reports no
  vulnerable packages across the solution.

### Dependencies

- Bumped `Raycoon.Serilog.Sinks.SQLite` from 1.2.1 to 1.2.2. The release only
  raises the sink's own dependency floors (`Microsoft.Data.Sqlite` >= 10.0.11
  for the CVE above, `Serilog` >= 4.4.0); no API or behaviour changes.
- Bumped `Serilog` from 4.3.1 to 4.4.0, required by the sink above. A lower
  direct pin fails restore with NU1605 (package downgrade). This also raises
  the floor declared by the published `Raycoon.RayMigrator.Core` and
  `Raycoon.RayMigrator.Infrastructure` packages: consumers that pin Serilog
  4.3.x directly must move to 4.4.0 as well. `Database.Common` and `Shared`
  carry no Serilog dependency, so external DAL plugins are unaffected.
- Bumped the 13 `Microsoft.Extensions.*` packages from 10.0.5 to 10.0.11
  (.NET 10 servicing). This raises the floor declared by every published
  RayMigrator package that carries one of them; consumers pinning 10.0.5
  directly must move up as well. `Serilog.Extensions.Hosting` only requires
  >= 10.0.0, so no downgrade conflict arises.
- Bumped `Microsoft.Data.SqlClient` from 7.0.0 to 7.0.2. Fixes a
  `NullReferenceException` in `SqlCommand.Cancel()` when the connection has
  already been torn down — reachable from command timeouts. 7.0.2 also
  aligns the companion packages onto a single version, so the transitive
  `Microsoft.Data.SqlClient.Extensions.Abstractions` and
  `Microsoft.Data.SqlClient.Internal.Logging` move from 1.0.0 to 7.0.2. The
  accompanying `AssemblyVersion` change on those two is .NET Framework only
  and does not affect RayMigrator.
- Bumped `Npgsql` from 10.0.2 to 10.0.3 and `Serilog.Settings.Configuration`
  from 10.0.0 to 10.0.1; both are routine servicing releases.
- Bumped `Microsoft.AspNetCore.Components.WebAssembly` and its `DevServer`
  counterpart from 10.0.5 to 10.0.11. Config Wizard only; the CLI and the
  Engine packages are unaffected. Building the wizard now expects an SDK
  from the 10.0.400 band (runtime 10.0.11) so the WASM runtime pack matches.
- Bumped `System.CommandLine` from 2.0.5 to 2.0.11 (six servicing releases).
  Parse edge cases, option-parsing consistency, error-message wording and
  help formatting changed; the API additions (`HelpAction.MaxWidth`,
  `SetAction(Task<int>)`, `ArgumentResult.Implicit`) are unused so far. The
  one documented migration item for this range — custom help and version
  actions must set `ClearsParseErrors` — was already satisfied by
  `LogoHelpAction`, so no source change was required.
- Bumped `MySqlConnector` from 2.5.0 to 2.6.2, affecting both the MariaDb
  and the MySql DAL. Two behaviour changes are worth knowing about even
  though neither reaches RayMigrator's own schema, which uses `TIMESTAMP`
  exclusively: `BINARY` columns are no longer auto-detected as `Guid`, and
  negative `DATETIME` ticks are now rejected. Migration authors relying on
  either behaviour in their own scripts should re-check them. Parameter
  parsing and argument validation are also stricter, and
  `EnableResultSetHeaderEvent` is now opt-in.
- Bumped `MudBlazor` from 9.2.0 to 9.8.0 (six minor releases, four of them
  carrying breaking changes). Config Wizard only. Of the changes that touch
  components the wizard uses, only two can manifest: `MudNumericField` no
  longer steps on mouse wheel (the spinner arrows and keyboard still work),
  and `MudAppBar` no longer compensates for the scrollbar during a dialog's
  scroll lock, so content anchored to the right of the app bar shifts by the
  scrollbar width while a dialog is open — in the wizard that is the language
  selector, measured at 11 px. Cosmetic and self-reverting on close. The
  remaining breaking changes are unreachable here: no `Class` is passed to
  any `MudSwitch` or to the one adorned `MudTextField`, and `OnPaste` is
  unused.
- Bumped `Microsoft.NET.Test.Sdk` from 18.3.0 to 18.9.0 and
  `coverlet.collector` from 8.0.1 to 10.0.1. Test infrastructure only,
  redistributed in nothing. Coverlet 10 declares no breaking changes, adds
  .NET 10 support, and fixes an 8.0.1 defect that could produce an empty
  coverage report without failing; a collection run now yields 5566 of
  11624 lines across 18 assemblies. Coverlet skipped its 9.x line, so the
  two-major jump is smaller than it looks.
- Bumped `NSubstitute` from 5.3.0 to 6.2.0. Test infrastructure only. The
  breaking changes are a target-framework floor of .NET 8 / netstandard2.0,
  removal of legacy obsolete APIs, and nullable annotations on the public
  API. None of them bite: all three consuming test projects target
  `net10.0`, no legacy or `CompatArg` API is used, and the build stays at
  zero warnings, so the nullable annotations required no source change.

### NuGet

- Package version bumped to 0.11.0.
- Added `busl-1.1`, `source-available`, `database-migration`, `dotnet`
  package tags for license-aware searches on nuget.org.
- Added explicit `<IsPackable>true</IsPackable>` on packageable projects
  (defensive against future SDK default changes).

### Distribution

- Self-contained and framework-dependent publish output now includes
  `LICENSE.md` and `NOTICE.md` next to the binary, satisfying BUSL-1.1
  Section 3 (Notices) requirements.
- Added `THIRD-PARTY-NOTICES.md`, shipped alongside them. Several bundled
  dependencies (Serilog, SQLitePCLRaw) are Apache-2.0 and require their
  notices to travel with the redistribution; `Microsoft.Data.SqlClient.SNI.runtime`
  carries Microsoft Software License Terms with their own distribution
  conditions. The release workflow fails if any of the three files is missing.
