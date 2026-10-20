# 11 — Production Operations

This chapter covers everything you need to deploy, monitor, and maintain RayMigrator in production environments using the CLI.

---

## Deployment Checklist

### Pre-Deployment

- [ ] All migration files reviewed and tested in staging
- [ ] Rollback files exist for all migrations (if `RequireRollbackFile = true`)
- [ ] Hash validation passes: `raymigrator validate-hash -p MyProduct -env Production`
- [ ] Database backup taken
- [ ] Connection strings verified via `{ENV:}` variables (no hardcoded credentials)
- [ ] Deployment window scheduled (for long-running migrations)
- [ ] No other RayMigrator instance running for the same product/environment
- [ ] After a RayMigrator update: the log account may create the log indexes once, and on a large log `DatabaseLogging.DbCommandTimeoutInSeconds` covers their build (see [Log Indexes After an Update](#log-indexes-after-an-update))
- [ ] Before a RayMigrator update: no run of the old version is left open; close one with `fix` of the old version (see [Open Runs Across an Update](#open-runs-across-an-update))

### Deployment

- [ ] Run Validate: `raymigrator migrate-up -p MyProduct -env Production -rm validate`
- [ ] Run Simulate: `raymigrator migrate-up -p MyProduct -env Production -rm simulate`
- [ ] Run Migrate: `raymigrator migrate-up -p MyProduct -env Production -rm migrate`
- [ ] Check exit code (0 = success)

### Post-Deployment

- [ ] Run Info to verify: `raymigrator info -p MyProduct -env Production`
- [ ] Validate hashes: `raymigrator validate-hash -p MyProduct -env Production`
- [ ] Verify application connectivity
- [ ] Monitor application logs for database errors

> **Tip:** Save this checklist as a template in your team's wiki and reference it for every deployment.

> **When a migration fails:** See [Error Scenarios and Recovery](../02-core-concepts/error-scenarios-and-recovery.md) for a complete matrix of error outcomes and step-by-step recovery procedures.

---

## CI/CD Pipeline Example

```yaml
# Example: GitHub Actions
name: Database Migration

on:
  push:
    branches: [main]
    paths: ['Migrations/**']

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0'
      - name: Install RayMigrator
        run: |
          gh release download --repo RAYCOON/raymigrator --pattern "RayMigrator-*-linux-x64.tar.gz" --dir /tmp
          sudo mkdir -p /opt/raymigrator
          sudo tar -xzf /tmp/RayMigrator-*-linux-x64.tar.gz -C /opt/raymigrator
          sudo ln -sf /opt/raymigrator/raymigrator /usr/local/bin/raymigrator
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      - name: Validate migrations
        run: raymigrator migrate-up -p MyProduct -env CI -rm validate --startup-info false
      - name: Validate hashes
        run: raymigrator validate-hash -p MyProduct -env CI --startup-info false

  deploy-staging:
    needs: validate
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0'
      - name: Install RayMigrator
        run: |
          gh release download --repo RAYCOON/raymigrator --pattern "RayMigrator-*-linux-x64.tar.gz" --dir /tmp
          sudo mkdir -p /opt/raymigrator
          sudo tar -xzf /tmp/RayMigrator-*-linux-x64.tar.gz -C /opt/raymigrator
          sudo ln -sf /opt/raymigrator/raymigrator /usr/local/bin/raymigrator
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      - name: Simulate in staging
        env:
          DB_CONNECTION: ${{ secrets.STAGING_DB_CONNECTION }}
        run: raymigrator migrate-up -p MyProduct -env Staging -rm simulate --startup-info false

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0'
      - name: Install RayMigrator
        run: |
          gh release download --repo RAYCOON/raymigrator --pattern "RayMigrator-*-linux-x64.tar.gz" --dir /tmp
          sudo mkdir -p /opt/raymigrator
          sudo tar -xzf /tmp/RayMigrator-*-linux-x64.tar.gz -C /opt/raymigrator
          sudo ln -sf /opt/raymigrator/raymigrator /usr/local/bin/raymigrator
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      - name: Backup database
        run: # your backup script
      - name: Migrate production
        env:
          DB_CONNECTION: ${{ secrets.PROD_DB_CONNECTION }}
        run: raymigrator migrate-up -p MyProduct -env Production -rm migrate --startup-info false
```

> **Archive layout matters.** The release archive (`RayMigrator-<version>-linux-x64.tar.gz`, `RayMigrator-<version>-osx-arm64.tar.gz`, `RayMigrator-<version>-win-x64.zip`) contains the `raymigrator` executable, the `DataAccessLayers/` directory (SQL templates and DAL assemblies for `SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite`) and the license files `LICENSE.md`, `NOTICE.md` and `THIRD-PARTY-NOTICES.md`. RayMigrator loads `DataAccessLayers/` from the executable's own directory, so the archive must be extracted as a whole into a directory of its own (`/opt/raymigrator` above). Do not extract it into `/usr/local/bin` and do not copy only the executable: make the executable reachable via a symlink (as above) or by adding the directory to `PATH`. The same applies to macOS (`/usr/local/raymigrator` or `/opt/raymigrator`). The published binaries are framework-dependent, so the .NET 10 runtime must be installed (the `Setup .NET` step above).

### Pipeline Best Practices

1. **Always validate before migrating.** The `Validate` run mode catches configuration errors and file issues without running SQL on the targets or writing the repository.
2. **Simulate in staging.** The `Simulate` run mode logs every SQL statement that would execute and reads existing repository records, but does not write repository records or execute SQL against target databases. Review the output before committing to production.
3. **Gate production deployments.** Use environment protection rules to require manual approval before the production job runs.
4. **Fail fast.** If `validate-hash` fails, stop the pipeline immediately.
5. **Suppress startup info.** Use `--startup-info false` for cleaner CI/CD output.
6. **Use environment variables for secrets.** Never hardcode connection strings in pipeline configuration. Use `{ENV:VARIABLE_NAME}` placeholders in `appsettings.json` and inject actual values via CI/CD secret variables.
7. **Store config files separately from migration files.** Use `--config-dir` (`-cd`) to point RayMigrator at a dedicated configuration directory when `appsettings.json` files are not in the working directory (e.g., mounted from a Kubernetes secret or a separate config repository).
8. **Give a cancelled job time to stop.** A cancelled pipeline job or a container stop sends SIGTERM on Linux and macOS. RayMigrator then stops at the next block boundary, records the run as `Cancelled` and exits with `130`; `--cancelled-action` chooses whether it rolls the interrupted file, the run or the release back first. A runner that kills the process before that (`docker stop`, for one, kills after 10 seconds by default) abandons the run like a crash, and it stays `Running` until RayMigrator closes it as orphaned. On Windows only Ctrl+C cancels a run. See [Cancelling a Run](../08-cli-reference/global-options.md#cancelling-a-run).

---

## Monitoring

### What to Watch

| Metric | Why It Matters |
|--------|---------------|
| **Exit codes** | Non-zero means something went wrong (see [Exit Codes](#exit-codes) below) |
| **Log levels** | Watch for Warning and Error messages |
| **Migration duration** | Sudden increases may indicate lock contention |
| **Repository state** | Check for `Failed` or `Executing` statuses that indicate issues |
| **MigrationRun result** | `Running` (10) entries that persist indicate crashed/orphaned runs; `Cancelled` (70) is a run that Ctrl+C or SIGTERM stopped |

### Repository Queries

Check migration status directly in the repository database:

```sql
-- SQL Server: Count migrations by status
SELECT m.MigrationStatusId, COUNT(*) AS Count
FROM ray.MigrationRecord m
GROUP BY m.MigrationStatusId;

-- Check for stuck/orphaned runs (status = Running)
SELECT *
FROM ray.MigrationRun
WHERE MigrationRunResultId = 10
  AND FinishedAt IS NULL;

-- Alert if a "Running" run has not written a heartbeat for longer than the orphan threshold
-- (Repository.OrphanedRunThresholdInMinutes, default 2; a run without a heartbeat counts from its start)
SELECT COUNT(*) AS OrphanedCount
FROM ray.MigrationRun
WHERE MigrationRunResultId = 10
  AND FinishedAt IS NULL
  AND DATEDIFF(MINUTE, COALESCE(LastHeartbeatAt, StartedAt), SYSUTCDATETIME()) > 2;

-- Recent migration runs with results
SELECT TOP 10
    Id,
    MigrationRunResultId,
    StartedAt,
    FinishedAt,
    DurationInMs
FROM ray.MigrationRun
ORDER BY StartedAt DESC;
```

```sql
-- PostgreSQL equivalent (unquoted snake_case identifiers, per DAL-017)
SELECT m.migration_status_id, COUNT(*) AS count
FROM ray.migration_record m
GROUP BY m.migration_status_id;

SELECT *
FROM ray.migration_run
WHERE migration_run_result_id = 10
  AND finished_at IS NULL;
```

> **Note:** MariaDB and MySQL also use unquoted snake_case identifiers (DAL-018). Replace `ray.` with the configured database name (no schema prefix) when querying repository tables on those engines. SQL Server and SQLite retain PascalCase.

> **Tip:** Create a monitoring dashboard that polls these queries periodically, so you are alerted to stuck runs or unexpected statuses.

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Execution error (migration failed, validation found issues, service exception) |
| 2 | Environment conflict: `--environment` and `DOTNET_ENVIRONMENT` are both set and differ |
| 3 | `--environment` is given but empty (or whitespace), whether or not `DOTNET_ENVIRONMENT` is set; the variable is only cross-checked and never replaces the option. Omitting `--environment` altogether is a parse error, because the option is required, and returns the parser's code `1` |
| 4 | No `Serilog` section inside the `RayMigrator` section of the loaded configuration, or no `RayMigrator` section in any loaded file. The message names the missing section and lists the files that were loaded; the files of the hierarchy that were not found are listed only when no loaded file carries a `RayMigrator` section (#31). A directory without any `appsettings*.json` file does not reach this check: it throws `ConfigurationValidationException` and exits with `100` |
| 5 | `System.CommandLine` threw while parsing or invoking the command; ordinary parse errors (invalid arguments, missing required options such as `--environment`) return the parser's own code, `1` |
| 100 | Unhandled exception before or while the command runs, including `ConfigurationValidationException` from option validation, `TemplateCache` and `DalFactory` (see TD-C-01 in [Risks and Technical Debt](../arc42/11-Risks-and-Technical-Debt.md)) |
| 130 | Ctrl+C or SIGTERM cancelled the command and it stopped cleanly (see [Cancelling a Run](../08-cli-reference/global-options.md#cancelling-a-run)): migrate-up, migrate-down or baseline ended with the run result `Cancelled`, update-hash or fix with the engine code `1004` (`Cancelled`), validate-hash or info was interrupted. A cancelled migrate-up whose rollback did not complete cleanly is an `Error` (engine code `1005`) and exits with `1`, and so do a migrate-up that failed at a file before the signal stopped its error recovery (`Error` with the code of the failure), a `1004` and an interruption that did not come from the signal |

---

## Troubleshooting Quick Reference

| Problem | Likely Cause | Solution |
|---------|-------------|----------|
| Exit code 1 | Migration SQL error or validation issue | Check logs, fix SQL, re-run |
| Exit code 2 | `--environment` differs from `DOTNET_ENVIRONMENT` | Ensure both values match, or unset `DOTNET_ENVIRONMENT` |
| Exit code 3 | `--environment` was passed empty; `DOTNET_ENVIRONMENT` does not replace it | Pass a non-empty `--environment` value (or `-env {ENV:DOTNET_ENVIRONMENT}` to take it from the variable) |
| Exit code 4 | No `Serilog` section inside `RayMigrator`, or no `RayMigrator` section in the loaded files | The message lists the loaded files: add the section to one of them. When it also lists files that were not found, check the product and environment names |
| Exit code 5, or the parser's own code | Bad CLI arguments | Check command syntax with `--help` |
| Exit code 130 | Ctrl+C or SIGTERM cancelled the command | Run it again: with the default `--cancelled-action terminate` a migrate-up resumes the interrupted file, a migrate-down continues the record in progress |
| Hash mismatch | File modified after execution | Run `update-hash` if intentional, or restore original file |
| "Orphaned run" warning | Previous run interrupted | Run `fix` command (use `--run-mode simulate` first to preview) |
| Connection timeout | Network or DB issue | Check connection string, increase `DbCommandTimeoutInSeconds` |
| Migration stuck in Executing | Process crashed mid-migration | Run `Fix --scope all` |
| "Another migration is already running" | Concurrent RayMigrator instance or orphaned run | Wait for the other instance to finish, or run `Fix --scope orphanedruns` |
| "File not found" error | Migration file moved or renamed | Restore original file path |

### Reading Log Output

RayMigrator uses structured logging with Serilog. Key log patterns to look for:

```
[ERR] Migration failed: 003_CreateReviews.sql — <error details>
[WRN] Hash mismatch detected for 001_CreateBooks.sql
[INF] Migration completed: 003_CreateReviews.sql (245ms)
[INF] MigrationRun completed with result: Ok
```

Two decisions that change what a run leaves behind are visible at the default Information level: `Repository created: DatabaseType [...], schema [...], VersionId [...]` when a run creates the repository schema and tables on first contact (also for `info`, `validate-hash`, `update-hash`, `baseline` and `fix`), and `Target [...] in TargetGroup [...] shares the repository connection: ...` once per run for every target on which migration files run on the atomic shared connection path, where all blocks of a file and the repository update commit or roll back together.

---

## Concurrency

RayMigrator enforces **exclusive migration runs** per product and environment. Only one migration process can execute for a given combination at any time; the run mode is not part of the check. However, different products and different environments CAN run concurrently without conflict.

### Database-Level Locking

Each database engine uses its own locking mechanism to prevent parallel migrations:

- **SQL Server**: `UPDLOCK, HOLDLOCK` table hints within a transaction
- **PostgreSQL**: `pg_advisory_xact_lock` (transaction-scoped advisory lock)
- **MariaDB / MySQL**: `GET_LOCK()` (cross-process advisory lock with immediate timeout)
- **SQLite**: a `BEGIN IMMEDIATE` transaction around the check and the insert, which takes the write lock of the file; a second instance waits for it and then finds the open run (before 0.20.0 two instances that started at the same moment could both run). This holds for processes on one host; a repository file on a network file system is not supported

On MariaDB and MySQL, when a call fails, times out or is cancelled, the DAL releases the `GET_LOCK()` locks of its session before the connection goes back to the pool, so a template that stopped halfway does not hold back another instance.

If two instances attempt to run simultaneously for the same product/environment:
- RayMigrator first checks for orphaned runs, whose last heartbeat is older than `Repository.OrphanedRunThresholdInMinutes` (default 2 minutes; a running command writes one every `Repository.HeartbeatIntervalInSeconds`, default 30 seconds), and auto-fixes them before retrying
- If the blocking run is genuinely active (its heartbeat is younger than the threshold), the second instance fails with a `MigrationAlreadyRunningException` (exit code 1)
- The error message recommends using the `fix` command to clean up orphaned runs

> **Important:** Ensure only one RayMigrator instance runs at a time per product/environment combination. Use pipeline serialization or deployment locks.

### Preventing Concurrent Runs

| Environment | Strategy |
|-------------|----------|
| CI/CD pipelines | Use pipeline concurrency groups or serialized stages |
| Manual deployments | Use a deployment lock (e.g., a shared mutex, a "deploying" flag in your team chat) |
| Scheduled jobs | Ensure cron schedules do not overlap |

---

## Database Logging

Migration logs land in the repository database by default; a `DatabaseLogging` section within the `RayMigrator` configuration section moves them to another database, changes the level, or switches them off (`"Enabled": false`):

```json
{
  "RayMigrator": {
    "DatabaseLogging": {
      "DatabaseType": "SqlServer",
      "ConnectionString": "{ENV:LOG_DB_CONNECTION}",
      "SchemaName": "logs",
      "MinimumLevel": "Information",
      "DbCommandTimeoutInSeconds": 20
    }
  }
}
```

Logs are written asynchronously via a `BlockingCollection<Action>` background queue and include:

| Field | Description |
|-------|-------------|
| Timestamp | When the log entry was created |
| Level | Debug, Information, Warning, Error, Critical |
| Message | The log message |
| Environment | The environment name |
| MigrationRunId | The current migration run ID |
| TargetGroupAlias | The target group alias |
| TargetAlias | The target alias |
| ReleaseVersion | The release version |
| Filename | The migration file name |

> **Note:** `DatabaseLogging.MinimumLevel` uses `Microsoft.Extensions.Logging.LogLevel` values (Trace, Debug, Information, Warning, Error, Critical, None). The `Serilog` section uses Serilog's own level names (Verbose, Debug, Information, Warning, Error, Fatal). Do not mix them.

> **Tip:** Use database logging in production to maintain a persistent, queryable audit trail of all migration activity. You can use a separate database from the repository to avoid coupling logging to migration operations.

### Log Indexes After an Update

RayMigrator gives `MigrationLog` two indexes, on `MigrationRunId` and on `CreatedAt` (`ix_{TableBaseName}MigrationLog_MigrationRunId` and `ix_{TableBaseName}MigrationLog_CreatedAt`, in snake_case on PostgreSQL, MySQL and MariaDB), and creates them at startup when they are missing, also on a log table that an older version created. Plan the first start after the update:

- **Rights.** That start needs the right to create indexes on the log table: on SQL Server `ALTER` on the table, on PostgreSQL the log account must own the table, on MariaDB and MySQL it needs the `INDEX` privilege. Later starts find the indexes and need the right no more. Without it the start ends with `DatabaseLogWriter could not be initialized` and exit code `1`. A DBA can create the two indexes beforehand under these names; a start that finds both creates nothing and needs no index right.
- **Time.** The indexes are built on the rows already in the log within `DatabaseLogging.DbCommandTimeoutInSeconds` (default 20 seconds); raise it for that start when the log is large. A timeout there is not retried, because every attempt would build the indexes from the start again: the start ends with `DatabaseLogWriter could not be initialized` and exit code `1`, and the message names the index build and `DatabaseLogging.DbCommandTimeoutInSeconds`. Once a start has built them, later starts find the indexes and need no longer timeout.
- **Other writers.** On SQL Server, PostgreSQL and SQLite the build blocks writes to the log table while it runs. A log entry that another host writes meanwhile can be lost when its write times out, which that host reports on stderr only, so choose a quiet moment for that start.
- **Parallel starts.** Two hosts that start at once do not fail on the indexes: SQL Server, PostgreSQL and MySQL serialise the log initialization with a lock of their own (the wait ends with `DatabaseLogging.DbCommandTimeoutInSeconds`, 20 seconds by default, and is not retried, so a start that waits while another host builds the indexes on a large log needs the longer timeout as well; only with a longer timeout, or `0`, do the templates of SQL Server and MySQL give up after 60 seconds with `-1, … could not acquire the database logging lock`), MariaDB and SQLite rely on `CREATE INDEX IF NOT EXISTS`.

### Log and History Retention

The log and the run history of the repository (`MigrationRecordHistory`) grow with every run, and no RayMigrator command deletes rows of either. A host that applies a retention policy, such as RayMigrator Studio, removes old rows through the engine's maintenance service (`IRepositoryMaintenanceService`): in batches, oldest first, never the rows of a run that is still open, and the log only where it lives in the repository database under the schema and prefix of the repository. `MigrationRecord`, `MigrationRun` and `MigrationRunMeta` are never deleted, so status and every decision of the engine stay as they were; the run history of `info` shows no migrations for a cleaned run. A cleanup that stops early, at its time budget, at a cancel or at an error after it deleted rows, reports what it deleted and that it did not complete, and the next call continues; it does not retry a failed statement. The CLI has no command for it. See [Retention](../03-database-layer/repository-schema.md#retention) and [ADR-027](../arc42/09-Architecture-Decisions.md#adr-027-the-engine-deletes-log-and-history-rows-on-a-hosts-request-and-never-reads-them-for-a-decision).

---

## Security Best Practices

1. **Never hardcode credentials** in configuration files. Always use `{ENV:VARIABLE_NAME}` placeholders for connection strings, API keys, and passwords.
2. **Use `--reveal-sensitive-data` sparingly** -- only for debugging, never in CI logs. When enabled, full connection strings and environment variable values are logged. Default is `false` (data is masked).
3. **Restrict repository access** -- only the migration service account needs write access to the repository schema.
4. **Audit trail** -- the repository tracks who ran what and when. Do not grant DELETE permissions on repository tables: the CLI never deletes rows. Only a host that applies a retention policy through the maintenance service needs DELETE, and only on `MigrationRecordHistory` and `MigrationLog`.
5. **Separate repository credentials** from target credentials where possible. The repository account does not need access to application tables.
6. **Rotate credentials** regularly and update the corresponding environment variables.

### Environment Variable Management

| Platform | Recommended Approach |
|----------|---------------------|
| GitHub Actions | Repository or environment secrets |
| Azure DevOps | Variable groups with secret variables |
| Kubernetes | Sealed Secrets or external secret stores |
| Docker | Environment variables via `docker-compose.yml` or `-e` flag |
| Local development | `.env` files (excluded from version control) |

---

## Backup Strategy

Always back up before production migrations.

### SQL Server

```bash
sqlcmd -S server -Q "BACKUP DATABASE [BookStore] TO DISK = '/backup/BookStore_pre_migration.bak'"
```

### PostgreSQL

```bash
pg_dump -h server -U user -d BookStore > /backup/BookStore_pre_migration.sql
```

### MariaDB

```bash
mariadb-dump -h server -u user -p BookStore > /backup/BookStore_pre_migration.sql
```

### MySQL

```bash
mysqldump -h server -u user -p BookStore > /backup/BookStore_pre_migration.sql
```

> **Tip:** Automate backups as part of your CI/CD pipeline, immediately before the Migrate step. This ensures you always have a restore point if a migration fails in an unexpected way.

### Backup Verification

A backup is only as good as its restore. Periodically test your restore process:

1. Restore the backup to a temporary database
2. Run `raymigrator validate-hash` against the restored database
3. Verify application connectivity against the restored database
4. Drop the temporary database

---

## Rollback Procedures

### Automatic Rollback

If `MigrationErrorAction` is set to `Rollback`, `RollbackErrorOnly`, or `RollbackRelease`, RayMigrator automatically executes rollback files when a migration fails.

| Action | Behavior |
|--------|----------|
| `Terminate` | Stop immediately, no rollback (default) |
| `Rollback` | Roll back ALL migrations in the current run |
| `RollbackErrorOnly` | Roll back only the migration file that caused the error |
| `RollbackRelease` | Roll back the current run's migrations of the failed release, keep previous releases |
| `Ignore` | Skip the error and continue with the next migration file |

When a rollback itself fails, the `RollbackErrorAction` setting controls what happens:

| RollbackErrorAction | Behavior |
|---------------------|----------|
| `Terminate` (default) | Stop the rollback chain immediately |
| `Ignore` | Skip the failed rollback block, run the remaining blocks and continue with the remaining files |

### Manual Rollback

Use `migrate-down` to manually roll back to a specific release:

```bash
# Roll back everything after Release 1.0
raymigrator migrate-down -p BookStore -env Production -rm migrate -tr "Release 1.0"

# Simulate rollback first to see what would happen
raymigrator migrate-down -p BookStore -env Production -rm simulate -tr "Release 1.0"
```

> **Warning:** Manual rollback executes rollback files in reverse order. Ensure all rollback files are present and tested before relying on this in production. A rollback file must also tolerate a partially applied file: `migrate-down` rolls back a file that stopped after some of its blocks from its first rollback block (see [Rollback Files](../07-migration-files/rollback-files.md#rollback-of-a-partially-applied-file)).

### Rollback File Requirements

If `RequireRollbackFile = true` (recommended for production), every migration file must have a corresponding rollback file:

```
Migrations/
  Release 1.0/
    Backend/
      001_CreateBooks.sql
      001_CreateBooks.rollback.sql
      002_CreateAuthors.sql
      002_CreateAuthors.rollback.sql
```

When `RequireRollbackFile = true`, a missing rollback file is treated as a structural error that always aborts the rollback chain, regardless of `RollbackErrorAction`. When `RequireRollbackFile = false`, a missing rollback file logs a warning. A user-initiated `migrate-down` skips the file and continues (the run ends `PartialSuccess`); the error-recovery rollback inside a `migrate-up` stops the chain unless `StopRollbackOnMissingRollbackFile = false` is configured (#20).

---

## Performance Considerations

### Long-Running Migrations

For large data migrations, consider:

1. **Increase timeout**: Set `DbCommandTimeoutInSeconds` appropriately
   ```json
   {
     "TargetDefaults": {
       "DbCommandTimeoutInSeconds": 300
     }
   }
   ```
2. **Disable transactions**: Large DDL operations on MariaDB/MySQL may not benefit from transactions
3. **Schedule during maintenance windows**: Avoid lock contention with application traffic
4. **Batch large data changes**: Split inserts/updates into smaller chunks across multiple migration files

### Retry Configuration

For transient database errors (network blips, deadlocks):

```json
{
  "TargetDefaults": {
    "DbCommandMaxRetries": 3,
    "DbCommandWaitTimeInMsBeforeRetry": 500
  }
}
```

RayMigrator automatically retries on known transient error codes for each database engine. Retry delay uses linear backoff: `base delay * attempt number`. When all retries are exhausted, a `RetryExhaustedException` is thrown.

---

## Fix Command: Repository Maintenance

The `fix` command resolves repository inconsistencies, most commonly orphaned migration runs left behind when a process crashes.

### Common Scenarios

**Process crash during migration**: A MigrationRun remains in `Running` (10) status, blocking new runs.

```bash
# Preview what would be fixed (no changes applied)
raymigrator fix -p MyProduct -env Production --run-mode simulate

# Fix orphaned runs whose last heartbeat is older than 2 minutes (default)
raymigrator fix -p MyProduct -env Production

# Fix only runs whose last heartbeat is older than 10 minutes
raymigrator fix -p MyProduct -env Production --older-than 10

# Fix all known issue types (not just orphaned runs)
raymigrator fix -p MyProduct -env Production --scope all
```

### Controlling Post-Fix Migration Status

The `--last-migration-status` option determines what status the open Migration records of an orphaned run receive:

| Value | Effect |
|-------|--------|
| none (default) | Each record keeps the progress of its file: `Failed` with its committed blocks, and the next migrate-up resumes after them; `Migrated` when every block committed; `NotMigrated` without progress |
| `not-migrated` | Every open record becomes `NotMigrated`; the next migrate-up runs the files again from their first block |
| `migrated` | Every open record becomes `Migrated`; the next migrate-up skips the files |

```bash
# Keep the progress of each file (default)
raymigrator fix -p MyProduct -env Production

# Mark the open records as "migrated" (skip next time)
raymigrator fix -p MyProduct -env Production --last-migration-status migrated

# Mark them as "not-migrated" (run again from block 1)
raymigrator fix -p MyProduct -env Production --last-migration-status not-migrated
```

> **Tip:** Always use `--run-mode simulate` first to preview what the Fix command would change before applying fixes in production.

### Open Runs Across an Update

From version 0.20.0 on, the repair settles an open record by its block counters (the default in the table above). Earlier versions counted differently while a file ran: with `MigrationErrorAction=Ignore` a record counted the blocks that were attempted, the failed ones included, and the record of a file that a CLI tool runs counted 1 block before the tool started. A run of an earlier version that ended abruptly and is still open after the update can therefore be misread: a record that counts every block becomes `Migrated` although a block failed or the tool did not finish, and a record that counts some blocks resumes after a block that failed.

- **Before the update**, close the open runs with `fix` of the old version, which sets their open records to `NotMigrated`; `fix --run-mode simulate` lists them first.
- **After the update**, when a run of the old version is still open, close it with `fix --last-migration-status not-migrated` before the next `migrate-up`, `migrate-down` or `baseline`, whose auto-fix would apply the rule. The files of its open records then run again from their first block.
- `info` shows beforehand how the repair would settle the open records of an open run: it lists them as the next `migrate-up` finds them after the repair.

---

## Multi-Environment Setup

A common production pattern uses environment-specific configuration files:

```
appsettings.json                          # Shared settings (product structure, defaults)
appsettings.Development.json              # Development overrides
appsettings.Staging.json                  # Staging overrides
appsettings.Production.json               # Production overrides
appsettings.MyProduct.json                # Product-specific overrides (optional)
appsettings.MyProduct.Production.json     # Product + environment overrides (optional)
```

### Example: Same Product, Different Environments

```bash
# All use the same migration files, different databases
raymigrator migrate-up -p BookStore -env Development -rm migrate
raymigrator migrate-up -p BookStore -env Staging -rm migrate
raymigrator migrate-up -p BookStore -env Production -rm migrate
```

Each environment loads its own `appsettings.{Environment}.json` file, which provides environment-specific connection strings via `{ENV:}` placeholders or direct values.

### Baseline for Multi-Environment Onboarding

When bringing an existing database under RayMigrator management at different points in time:

```bash
# Dev is at Release 3.0, Prod at Release 2.0
raymigrator baseline -p BookStore -env Development -tr "Release 3.0"
raymigrator baseline -p BookStore -env Production -tr "Release 2.0"
```

---

**Next:** [Chapter 12 — Quick Reference](12-reference.md)
