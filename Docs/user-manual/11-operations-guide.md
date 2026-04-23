# 11 — Production Operations

This chapter covers everything you need to deploy, monitor, and maintain RayMigrator in production environments using the CLI (direct mode).

---

## Deployment Checklist

### Pre-Deployment

- [ ] All migration files reviewed and tested in staging
- [ ] Rollback files exist for all migrations (if `RequireRollbackFile = true`)
- [ ] Hash validation passes: `RayMigrator Validate-Hash -p MyProduct -env Production`
- [ ] Database backup taken
- [ ] Connection strings verified via `{ENV:}` variables (no hardcoded credentials)
- [ ] Deployment window scheduled (for long-running migrations)
- [ ] No other RayMigrator instance running for the same product/environment

### Deployment

- [ ] Run Validate: `RayMigrator Migrate-Up -p MyProduct -env Production -rm Validate`
- [ ] Run Simulate: `RayMigrator Migrate-Up -p MyProduct -env Production -rm Simulate`
- [ ] Run Migrate: `RayMigrator Migrate-Up -p MyProduct -env Production -rm Migrate`
- [ ] Check exit code (0 = success)

### Post-Deployment

- [ ] Run Info to verify: `RayMigrator Info -p MyProduct -env Production`
- [ ] Validate hashes: `RayMigrator Validate-Hash -p MyProduct -env Production`
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
          gh release download --repo RAYCOON/RayMigrator --pattern "RayMigrator-*-linux-x64.tar.gz" --dir /tmp
          tar -xzf /tmp/RayMigrator-*-linux-x64.tar.gz -C /usr/local/bin
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      - name: Validate migrations
        run: RayMigrator Migrate-Up -p MyProduct -env CI -rm Validate --startup-info false
      - name: Validate hashes
        run: RayMigrator Validate-Hash -p MyProduct -env CI --startup-info false

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
          gh release download --repo RAYCOON/RayMigrator --pattern "RayMigrator-*-linux-x64.tar.gz" --dir /tmp
          tar -xzf /tmp/RayMigrator-*-linux-x64.tar.gz -C /usr/local/bin
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      - name: Simulate in staging
        env:
          DB_CONNECTION: ${{ secrets.STAGING_DB_CONNECTION }}
        run: RayMigrator Migrate-Up -p MyProduct -env Staging -rm Simulate --startup-info false

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
          gh release download --repo RAYCOON/RayMigrator --pattern "RayMigrator-*-linux-x64.tar.gz" --dir /tmp
          tar -xzf /tmp/RayMigrator-*-linux-x64.tar.gz -C /usr/local/bin
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      - name: Backup database
        run: # your backup script
      - name: Migrate production
        env:
          DB_CONNECTION: ${{ secrets.PROD_DB_CONNECTION }}
        run: RayMigrator Migrate-Up -p MyProduct -env Production -rm Migrate --startup-info false
```

### Pipeline Best Practices

1. **Always validate before migrating.** The `Validate` run mode catches configuration errors and file issues without touching any database.
2. **Simulate in staging.** The `Simulate` run mode logs every SQL statement that would execute and reads existing repository records, but does not write repository records or execute SQL against target databases. Review the output before committing to production.
3. **Gate production deployments.** Use environment protection rules to require manual approval before the production job runs.
4. **Fail fast.** If `Validate-Hash` fails, stop the pipeline immediately.
5. **Suppress startup info.** Use `--startup-info false` for cleaner CI/CD output.
6. **Use environment variables for secrets.** Never hardcode connection strings in pipeline configuration. Use `{ENV:VARIABLE_NAME}` placeholders in `appsettings.json` and inject actual values via CI/CD secret variables.
7. **Store config files separately from migration files.** Use `--config-dir` (`-cd`) to point RayMigrator at a dedicated configuration directory when `appsettings.json` files are not in the working directory (e.g., mounted from a Kubernetes secret or a separate config repository).

---

## Monitoring

### What to Watch

| Metric | Why It Matters |
|--------|---------------|
| **Exit codes** | Non-zero means something went wrong (see [Exit Codes](#exit-codes) below) |
| **Log levels** | Watch for Warning and Error messages |
| **Migration duration** | Sudden increases may indicate lock contention |
| **Repository state** | Check for `Failed` or `Executing` statuses that indicate issues |
| **MigrationRun result** | `Running` (10) entries that persist indicate crashed/orphaned runs |

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

-- Alert if any run has been "Running" for > 1 hour
SELECT COUNT(*) AS OrphanedCount
FROM ray.MigrationRun
WHERE MigrationRunResultId = 10
  AND FinishedAt IS NULL
  AND DATEDIFF(MINUTE, StartedAt, SYSUTCDATETIME()) > 60;

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
-- PostgreSQL equivalent
SELECT m."MigrationStatusId", COUNT(*) AS "Count"
FROM ray."MigrationRecord" m
GROUP BY m."MigrationStatusId";

SELECT *
FROM ray."MigrationRun"
WHERE "MigrationRunResultId" = 10
  AND "FinishedAt" IS NULL;
```

> **Tip:** Create a monitoring dashboard that polls these queries periodically, so you are alerted to stuck runs or unexpected statuses.

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Execution error (migration failed, validation found issues, service exception) |
| 2 | Environment conflict (`--environment` and `DOTNET_ENVIRONMENT` have different values) |
| 3 | Missing required environment (neither `--environment` nor `DOTNET_ENVIRONMENT` is set) |
| 4 | Missing or invalid configuration (no Serilog section found, config files not found) |
| 5 | Command-line parsing error (invalid arguments, missing required options) |
| 100 | Unhandled exception |

---

## Troubleshooting Quick Reference

| Problem | Likely Cause | Solution |
|---------|-------------|----------|
| Exit code 1 | Migration SQL error or validation issue | Check logs, fix SQL, re-run |
| Exit code 2 | `--environment` differs from `DOTNET_ENVIRONMENT` | Ensure both values match, or use only one |
| Exit code 3 | No environment specified | Add `--environment` or set `DOTNET_ENVIRONMENT` |
| Exit code 4 | Missing or invalid configuration | Check appsettings.json exists with valid Serilog section |
| Exit code 5 | Bad CLI arguments | Check command syntax with `--help` |
| Hash mismatch | File modified after execution | Run `Update-Hash` if intentional, or restore original file |
| "Orphaned run" warning | Previous run interrupted | Run `Fix` command (use `--dry-run` first to preview) |
| Connection timeout | Network or DB issue | Check connection string, increase `DbCommandTimeoutInSeconds` |
| Migration stuck in Executing | Process crashed mid-migration | Run `Fix --scope All` |
| "Another migration is already running" | Concurrent RayMigrator instance or orphaned run | Wait for the other instance to finish, or run `Fix --scope OrphanedRuns` |
| "File not found" error | Migration file moved or renamed | Restore original file path |

### Reading Log Output

RayMigrator uses structured logging with Serilog. Key log patterns to look for:

```
[ERR] Migration failed: 003_CreateReviews.sql — <error details>
[WRN] Hash mismatch detected for 001_CreateBooks.sql
[INF] Migration completed: 003_CreateReviews.sql (245ms)
[INF] MigrationRun completed with result: Ok
```

---

## Concurrency

RayMigrator enforces **exclusive migration runs** per product, environment, and run mode combination. Only one migration process can execute for a given combination at any time. However, different products and different environments CAN run concurrently without conflict.

### Database-Level Locking

Each database engine uses its own locking mechanism to prevent parallel migrations:

- **SQL Server**: `UPDLOCK, HOLDLOCK` table hints within a transaction
- **PostgreSQL**: `pg_advisory_xact_lock` (transaction-scoped advisory lock)
- **MariaDB / MySQL**: `GET_LOCK()` (cross-process advisory lock with immediate timeout)
- **SQLite**: File-level locking (inherent to SQLite's architecture)

If two instances attempt to run simultaneously for the same product/environment:
- RayMigrator first checks for orphaned runs older than 10 minutes and auto-fixes them before retrying
- If the blocking run is genuinely active (or less than 10 minutes old), the second instance fails with a `MigrationAlreadyRunningException` (exit code 1)
- The error message recommends using the `Fix` command to clean up orphaned runs

> **Important:** Ensure only one RayMigrator instance runs at a time per product/environment combination. Use pipeline serialization or deployment locks.

### Preventing Concurrent Runs

| Environment | Strategy |
|-------------|----------|
| CI/CD pipelines | Use pipeline concurrency groups or serialized stages |
| Manual deployments | Use a deployment lock (e.g., a shared mutex, a "deploying" flag in your team chat) |
| Scheduled jobs | Ensure cron schedules do not overlap |

---

## Database Logging

Store migration logs directly in a database for centralized monitoring. Database logging is activated by the presence of the `DatabaseLogging` section within the `RayMigrator` configuration section:

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

---

## Security Best Practices

1. **Never hardcode credentials** in configuration files. Always use `{ENV:VARIABLE_NAME}` placeholders for connection strings, API keys, and passwords.
2. **Use `--reveal-sensitive-data` sparingly** -- only for debugging, never in CI logs. When enabled, full connection strings and environment variable values are logged. Default is `false` (data is masked).
3. **Restrict repository access** -- only the migration service account needs write access to the repository schema.
4. **Audit trail** -- the repository tracks who ran what and when. Do not grant DELETE permissions on repository tables.
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
2. Run `RayMigrator Validate-Hash` against the restored database
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
| `RollbackRelease` | Roll back all migrations from the failed release, keep previous releases |
| `Ignore` | Skip the error and continue with the next migration file |

When a rollback itself fails, the `RollbackErrorAction` setting controls what happens:

| RollbackErrorAction | Behavior |
|---------------------|----------|
| `Terminate` (default) | Stop the rollback chain immediately |
| `Ignore` | Skip the failed rollback, continue with remaining files |

### Manual Rollback

Use `Migrate-Down` to manually roll back to a specific release:

```bash
# Roll back everything after Release 1.0
RayMigrator Migrate-Down -p BookStore -env Production -rm Migrate -tr "Release 1.0"

# Simulate rollback first to see what would happen
RayMigrator Migrate-Down -p BookStore -env Production -rm Simulate -tr "Release 1.0"
```

> **Warning:** Manual rollback executes rollback files in reverse order. Ensure all rollback files are present and tested before relying on this in production.

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

When `RequireRollbackFile = true`, a missing rollback file is treated as a structural error that always aborts the rollback chain, regardless of `RollbackErrorAction`. When `RequireRollbackFile = false`, a missing rollback file logs a warning and the rollback chain continues to the next file.

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

The `Fix` command resolves repository inconsistencies, most commonly orphaned migration runs left behind when a process crashes.

### Common Scenarios

**Process crash during migration**: A MigrationRun remains in `Running` (10) status, blocking new runs.

```bash
# Preview what would be fixed (no changes applied)
RayMigrator Fix -p MyProduct -env Production --dry-run

# Fix orphaned runs older than 60 minutes (default)
RayMigrator Fix -p MyProduct -env Production

# Fix runs older than 10 minutes
RayMigrator Fix -p MyProduct -env Production --older-than 10

# Fix all known issue types (not just orphaned runs)
RayMigrator Fix -p MyProduct -env Production --scope All
```

### Controlling Post-Fix Migration Status

The `--last-migration-status` option determines what status orphaned Migration records receive:

| Value | Effect |
|-------|--------|
| `not-migrated` (default) | Orphaned migrations will be re-executed on the next Migrate-Up run |
| `migrated` | Orphaned migrations will be skipped on the next Migrate-Up run |

```bash
# Mark orphaned migrations as "migrated" (skip next time)
RayMigrator Fix -p MyProduct -env Production --last-migration-status migrated

# Mark as "not-migrated" (re-execute next time, default)
RayMigrator Fix -p MyProduct -env Production --last-migration-status not-migrated
```

> **Tip:** Always use `--dry-run` first to preview what the Fix command would change before applying fixes in production.

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
RayMigrator Migrate-Up -p BookStore -env Development -rm Migrate
RayMigrator Migrate-Up -p BookStore -env Staging -rm Migrate
RayMigrator Migrate-Up -p BookStore -env Production -rm Migrate
```

Each environment loads its own `appsettings.{Environment}.json` file, which provides environment-specific connection strings via `{ENV:}` placeholders or direct values.

### Baseline for Multi-Environment Onboarding

When bringing an existing database under RayMigrator management at different points in time:

```bash
# Dev is at Release 3.0, Prod at Release 2.0
RayMigrator Baseline -p BookStore -env Development -tr "Release 3.0"
RayMigrator Baseline -p BookStore -env Production -tr "Release 2.0"
```

---

**Next:** [Chapter 12 — Quick Reference](12-reference.md)
