# Troubleshooting

> **For systematic error recovery:** If a migration run has failed and you need to determine the exact recovery procedure based on your `MigrationErrorAction` configuration, see [Error Scenarios and Recovery](../02-core-concepts/error-scenarios-and-recovery.md).

Common issues and solutions for RayMigrator.

## Connection Issues

### Unable to Connect to Database

**Symptoms:**
- "Connection refused" error
- "Network path not found" error
- Timeout when connecting

**Solutions:**

1. **Verify connection string**
   ```bash
   raymigrator migrate-up -p MyProduct -env Dev -rm simulate --reveal-sensitive-data true
   ```
   Check the logged connection string is correct.

2. **Test database connectivity**
   ```bash
   # SQL Server
   sqlcmd -S localhost -U sa -P "password" -Q "SELECT 1"

   # PostgreSQL
   psql -h localhost -U postgres -c "SELECT 1"

   # MariaDB
   mariadb -h localhost -u root -p -e "SELECT 1"

   # MySQL
   mysql -h localhost -P 3307 -u root -p -e "SELECT 1"
   ```

3. **Check firewall rules**
   - Ensure database port is accessible
   - Default ports: SQL Server (1433), PostgreSQL (5432), MariaDB (3306), MySQL (3307 in test setup, 3306 default)

4. **Verify SSL/TLS settings**
   ```
   # SQL Server - Add to connection string:
   TrustServerCertificate=true;

   # PostgreSQL:
   SSL Mode=Require;
   ```

### Authentication Failed

**Symptoms:**
- "Login failed" error
- "Authentication failed" error

**Solutions:**

1. **Verify credentials**
   - Check username and password
   - Ensure user has required permissions

2. **Check authentication mode**
   - SQL Server: Windows vs SQL authentication
   - Ensure server allows the authentication type

3. **Verify user permissions**
   ```sql
   -- SQL Server: Check permissions
   SELECT dp.name, dp.type_desc, pe.permission_name
   FROM sys.database_principals dp
   JOIN sys.database_permissions pe ON dp.principal_id = pe.grantee_principal_id
   WHERE dp.name = 'raymigrator';
   ```

## Docker Issues

### Containers Not Starting

**Symptoms:**
- `docker-compose up` fails or containers exit immediately

**Solutions:**

1. **Use the correct env-file and profile**
   ```bash
   cd Testing/Docker/
   docker-compose --env-file default.env --profile all up -d
   ```
   Available profiles: `all`, `sqlserver`, `postgresql`, `mariadb`, `mysql`.

2. **Or use the provided PowerShell scripts**
   ```bash
   ./Testing/Docker/RunDocker.default.all.ps1         # All databases
   ./Testing/Docker/RunDocker.default.sqlserver.ps1   # SQL Server only
   ./Testing/Docker/RunDocker.default.postgresql.ps1  # PostgreSQL only
   ./Testing/Docker/RunDocker.default.mariadb.ps1     # MariaDB only
   ./Testing/Docker/RunDocker.default.mysql.ps1       # MySQL only
   ```

3. **Check port conflicts**
   Default port mappings:
   - SQL Server: `1433`
   - PostgreSQL: `5432`
   - MariaDB: `3306`
   - MySQL: `3307` (maps host 3307 to container 3306)

### Connecting to Docker Databases

Use `docker exec` to connect directly to database containers (no local CLI tools required):

```bash
# SQL Server
docker exec rm_db_sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1"'

# PostgreSQL
docker exec rm_db_postgresql psql -U postgres -d raydb -c "SELECT 1"

# MariaDB (uses 'mariadb' CLI, not 'mysql')
docker exec rm_db_mariadb mariadb -u rayuser -praypass123 raydb -e "SELECT 1"

# MySQL
docker exec rm_db_mysql mysql -u rayuser -praypass123 raydb -e "SELECT 1"
```

## Migration Execution Issues

### Migration Run Already Running

**Symptoms:**
- Error: "RayMigrator aborted because another migration is already running"

**Solutions:**

1. **Wait for existing migration to complete**

2. **Check for stuck runs**
   ```sql
   SELECT * FROM ray.RayMigrationRun
   WHERE MigrationRunResultId = 10 -- Running
   ORDER BY StartedAt DESC;
   ```

3. **Fix stuck run** (use the `fix` command, or manual SQL below)

   > **Note**: Table names in the SQL examples below use simplified names (e.g., `MigrationRun`). Your actual table names include the configured `SchemaName` and `TableBaseName` prefix (e.g., `[ray].[RayMigrationRun]` for SQL Server with SchemaName=`ray` and TableBaseName=`Ray`). Adjust the schema and prefix to match your configuration.

   ```sql
   -- Mark orphaned run as manually terminated
   UPDATE MigrationRun
   SET MigrationRunResultId = 90, FinishedAt = SYSUTCDATETIME()
   WHERE Id = @StuckRunId AND MigrationRunResultId = 10;
   ```

### Migration Timeout

**Symptoms:**
- "Timeout expired" error
- Long-running migrations fail

**Solutions:**

1. **Increase timeout**
   ```json
   {
     "Products": [{
       "TargetGroups": [{
         "Targets": [{
           "DbCommandTimeoutInSeconds": 300
         }]
       }]
     }]
   }
   ```

2. **Split large migrations**
   - Break into multiple smaller files
   - Use `GO` to separate blocks

3. **Run outside transaction**
   ```sql
   /*
   [RayMigrator]
   UseTransaction = false
   */
   ```

### SQL Syntax Error

**Symptoms:**
- "Incorrect syntax" error
- SQL execution fails

**Solutions:**

1. **Check database-specific syntax**
   - SQL Server uses `GO`, others use `;`
   - Identifier quoting: `[name]` vs `"name"` vs `` `name` ``

2. **Test SQL manually**
   ```bash
   # Copy SQL and run in database client
   ```

3. **Check delimiter configuration**
   - Ensure DAL matches target database type

### Hash Validation Failed

**Symptoms:**
- "Hash mismatch" error
- Migration blocked

**Solutions:**

1. **Check if file was intentionally modified**
   ```bash
   git diff path/to/migration.sql
   ```

2. **Update hash if change is valid**
   ```bash
   raymigrator update-hash -p MyProduct -env Prod
   ```

3. **Restore original file**
   ```bash
   git checkout path/to/migration.sql
   ```

4. **Disable validation temporarily**
   ```json
   {
     "TargetGroups": [{
       "HashValidationScope": "Disabled"
     }]
   }
   ```

## Configuration Issues

### Configuration File Not Found

**Symptoms:**
- "Product alias [X] not found in the loaded configuration" error when `appsettings.json` is not in the current working directory
- Configuration appears empty or defaults are used unexpectedly

**Solutions:**

1. **Use `--config-dir` to point to the correct directory**
   ```bash
   raymigrator migrate-up -p MyProduct -env Dev --config-dir /path/to/config
   ```
   The `--config-dir` (`-cd`) global option overrides the directory where RayMigrator searches for all configuration files (`appsettings.json`, `appsettings.{Environment}.json`, `appsettings.{Product}.json`, and `appsettings.{Product}.{Environment}.json`). When omitted, the current working directory is used.

2. **Run from the correct directory**
   ```bash
   cd /path/containing/appsettings
   raymigrator migrate-up -p MyProduct -env Dev
   ```

3. **Use an environment variable for the config path**
   ```bash
   raymigrator migrate-up -p MyProduct -env Dev --config-dir "{ENV:CONFIG_DIR}"
   ```

### Product Not Found

**Symptoms:**
- "Product alias [X] not found in the loaded configuration" error

**Solutions:**

1. **Check product alias matches exactly**
   - The `--product` parameter is **always case-sensitive**
   - RayMigrator will suggest a case-insensitive match if one exists (e.g., "Did you mean [MyProduct]?")
   - Check `appsettings.json` for exact spelling

2. **Verify configuration loaded**
   ```bash
   raymigrator migrate-up -p MyProduct -env Dev -rm simulate --startup-info true
   ```

3. **Check appsettings hierarchy**
   - `appsettings.json` loaded first
   - `appsettings.{Environment}.json` overrides

### Target Group Not Found

**Symptoms:**
- No migrations executed for a target group
- "TargetGroup 'X' not found" warning

**Solutions:**

1. **Directory name must match alias**
   ```
   MigrationFiles/
   └── Release 1.0/
       └── Backend/    ← Must match TargetGroup.Alias
   ```

2. **Check configuration**
   ```json
   {
     "TargetGroups": [{
       "Alias": "Backend"  ← Must match directory name
     }]
   }
   ```

### Environment Variables Not Resolved

**Symptoms:**
- `{ENV:VARIABLE}` appears in output literally
- Connection fails with placeholder in string
- Connection fails with empty segments (variable exists but resolves to null -- see [Known Behaviors](#known-behaviors-and-limitations))

**Solutions:**

1. **Verify variable is set**
   ```bash
   echo $VARIABLE
   # or
   printenv VARIABLE
   ```

2. **Check syntax**
   ```
   Correct:   {ENV:VARIABLE_NAME}
   Incorrect: ${VARIABLE_NAME}
   Incorrect: %VARIABLE_NAME%
   ```

3. **Set variable before running**
   ```bash
   export VARIABLE_NAME="value"
   raymigrator migrate-up ...
   ```

4. **Check for unresolved placeholders**
   If the environment variable does not exist, is empty, or is whitespace-only, RayMigrator logs an error and terminates at startup. Use `--reveal-sensitive-data true --startup-info true` to inspect the resolved configuration values. See [Environment Variables](../06-configuration-reference/environment-variables.md) for details.

## Repository Issues

### Repository Tables Missing

**Symptoms:**
- "Invalid object name 'ray.RayMigrationRecord'" error
- Repository not created

**Solutions:**

1. **Run migration to create repository**
   - Repository is auto-created on first run

2. **Check schema permissions**
   ```sql
   -- User needs CREATE TABLE permission
   GRANT CREATE TABLE TO [raymigrator];
   ```

3. **Verify schema exists**
   ```sql
   -- Create schema if needed
   CREATE SCHEMA [ray];
   ```

### Repository Version Mismatch

**Symptoms:**
- "Repository version not compatible" error

**Solutions:**

1. **Upgrade repository**
   - Backup existing data
   - Drop and recreate tables
   - Or run migration files for upgrade

2. **Check RayMigrator version**
   ```bash
   raymigrator --version
   ```

### Product or Environment Name Empty (`TemplateResultException`)

**Symptoms:**
- `TemplateResultException` with `ResultCode = -20` ("Product with empty name [NULL] is not allowed!")
- `TemplateResultException` with `ResultCode = -50` ("Environment with empty name [NULL] is not allowed!")

**Explanation:**

The `Repository_Product_CheckInsert` template rejects a `NULL`/empty `Name` parameter with `-20 ProductNameEmpty`. The `Repository_Environment_CheckInsert` template rejects a `NULL`/empty `Name` parameter with `-50 EnvironmentNameEmpty`. Both tables look up existing rows via a `NameLower` column with a unique index, so duplicate entries are also prevented at the database level.

**Solutions:**

1. **Check the CLI arguments** — ensure `--product` and `--environment` are passed, resolved (`{ENV:...}` placeholders), and non-empty after trimming.
2. **Inspect the resolved configuration** with `--startup-info true --reveal-sensitive-data true` to confirm the values reaching the repository templates.
3. **Check existing rows** if a rename is suspected:
   ```sql
   SELECT Id, Name, NameLower, CreatedAt FROM ray.RayProduct;
   SELECT Id, Name, NameLower, CreatedAt FROM ray.RayEnvironment;
   ```

## Rollback Issues

### Rollback File Not Found

**Symptoms:**
- "Rollback file not found" warning
- Migration status becomes "Failed"

**Solutions:**

1. **Create rollback file**
   ```
   001_CreateTable.sql         ← Migration
   001_CreateTable.rollback.sql ← Create this
   ```

2. **Check extension configuration**
   ```json
   {
     "MigrationRollbackFilesPreExtension": "rollback"
   }
   ```

3. **Allow rollback to continue past missing files (when `RequireRollbackFile = false`)**
   If rollback files are intentionally absent for some migrations, use `StopRollbackOnMissingRollbackFile = false` to allow the error-recovery rollback chain to skip missing files and continue:
   ```json
   {
     "ProductDefaults": {
       "StopRollbackOnMissingRollbackFile": false
     }
   }
   ```
   This can also be overridden at runtime via the CLI option `--stop-rollback-on-missing-rollback-file false` (`-sromrf false`) on the `migrate-up` command. Note: this setting only affects error-recovery rollback (triggered by `MigrationErrorAction`); it has no effect on explicit `migrate-down` execution.

### Rollback Failed

**Symptoms:**
- Rollback SQL errors
- Partial rollback

**Solutions:**

1. **Check `RollbackErrorAction` configuration**
   The `RollbackErrorAction` setting controls behavior when a rollback operation encounters an error:
   - `Terminate` (default) -- Instantly terminate the rollback chain and do not perform any further rollbacks.
   - `Ignore` -- Skip the failed rollback file (mark it as Failed) and continue the rollback chain with the next file.

   ```json
   {
     "ProductDefaults": {
       "RollbackErrorAction": "Terminate"
     }
   }
   ```
   This setting can be configured at multiple levels: `ProductDefaults` and `Product` in appsettings, plus any migsettings level (product, release, target group) and per-file TOML metadata. See [Rollback Error Handling](../02-core-concepts/error-handling.md#rollback-error-handling) for the full priority chain.

2. **Check `MigrationErrorAction` for rollback scope**
   The `MigrationErrorAction` determines which migrations are rolled back when an error occurs:
   - `Terminate` (default) -- Stop immediately, no rollback.
   - `Rollback` -- Roll back all migrations from the current MigrationRun.
   - `RollbackErrorOnly` -- Roll back only the file that caused the error.
   - `RollbackRelease` -- Roll back all migrations from the release that caused the error. Earlier releases remain intact.
   - `Ignore` -- Skip the failed file and continue execution.

3. **Fix rollback SQL and retry**

4. **Manual cleanup**
   - Review failed SQL
   - Execute corrections manually
   - Update repository state

5. **Check transaction support**
   - MariaDB and MySQL have limited DDL transaction support. DDL statements (CREATE TABLE, ALTER TABLE, DROP TABLE, etc.) cause an **implicit commit**, meaning they cannot be rolled back within a transaction. Set `UseTransaction = false` for migration files that contain only DDL on these databases.
   - SQL Server, PostgreSQL, and SQLite fully support DDL in transactions.

## Logging Issues

### No Log Output

**Symptoms:**
- Console is silent
- No log files created

**Solutions:**

1. **Check Serilog configuration** (inside the `RayMigrator` section)
   ```json
   {
     "RayMigrator": {
       "Serilog": {
         "MinimumLevel": "Information",
         "WriteTo": [{ "Name": "Console" }]
       }
     }
   }
   ```

2. **Check log file permissions**
   - Ensure write access to log directory

3. **Enable verbose logging**
   ```json
   {
     "RayMigrator": {
       "Serilog": {
         "MinimumLevel": "Debug"
       }
     }
   }
   ```

### Database Logging Not Working

**Symptoms:**
- No entries in MigrationLog table

**Solutions:**

1. **Verify DatabaseLogging configuration** (inside `RayMigrator` section, activated by presence of the section)
   ```json
   {
     "RayMigrator": {
       "DatabaseLogging": {
         "DatabaseType": "SqlServer",
         "ConnectionString": "...",
         "SchemaName": "logs",
         "MinimumLevel": "Information"
       }
     }
   }
   ```

2. **Check logging tables exist**
   ```sql
   SELECT * FROM ray.RayMigrationLog;
   ```

## Performance Issues

### Migrations Running Slowly

**Solutions:**

1. **Add indexes to large tables before data operations**

2. **Use batch operations**
   ```sql
   -- Instead of individual inserts
   INSERT INTO Table (Col1) VALUES (1), (2), (3)...
   ```

3. **Run without transactions for large operations**
   ```sql
   /*
   [RayMigrator]
   UseTransaction = false
   */
   ```

4. **Check database performance**
   - Monitor query plans
   - Check for blocking

## Recovery Issues

### Interrupted Migration Detected

**Symptoms:**
- "Interrupted migration detected" message on startup
- Migration cannot proceed without user action

**Solutions:**

1. **Resume from last block**
   ```bash
   # Default behavior - resumes from last successful block
   raymigrator migrate-up -p MyProduct -env Prod -rm migrate
   ```

2. **Check interrupted migration details**
   ```sql
   SELECT
       m.Id, m.Filename,
       m.FileUpBlocksMigrated, m.FileUpBlocksTotal,
       m.MigrationStatusId
   FROM MigrationRecord m
   WHERE m.MigrationStatusId IN (10, 20)  -- Pending or Executing
     AND m.FileUpBlocksMigrated < m.FileUpBlocksTotal
     AND m.FinishedAt IS NULL;
   ```

3. **Cleanup** via `fix` command:
   ```bash
   # Preview what would be fixed (no changes applied)
   raymigrator fix -p MyProduct -env Production --scope orphanedruns --dry-run

   # Fix orphaned runs (default: only runs older than 60 minutes)
   raymigrator fix -p MyProduct -env Production --scope orphanedruns

   # Fix runs older than 10 minutes
   raymigrator fix -p MyProduct -env Production --scope orphanedruns --older-than 10

   # Fix and mark orphaned migrations as "migrated" (skip next time) instead of "not-migrated" (re-execute next time)
   raymigrator fix -p MyProduct -env Production --scope orphanedruns --last-migration-status migrated
   ```
   - Or use direct SQL to update repository state (see Orphaned Migration Run section below)

### Orphaned Migration Run

**Symptoms:**
- Error: "Migration run is orphaned (running for X hours)"
- Previous process crashed without cleanup

**Solutions:**

1. **Investigate before cleanup**
   ```sql
   -- Find orphaned runs
   SELECT mr.*, p.Name AS ProductName
   FROM MigrationRun mr
   JOIN Product p ON mr.ProductId = p.Id
   WHERE mr.MigrationRunResultId = 10  -- Running
     AND mr.FinishedAt IS NULL
     AND DATEDIFF(HOUR, mr.StartedAt, GETUTCDATE()) > 1;
   ```

2. **Manual cleanup**
   ```sql
   -- Mark as error-terminated
   UPDATE MigrationRun
   SET
       MigrationRunResultId = 90,  -- Error
       FinishedAt = SYSUTCDATETIME(),
       DurationInMs = DATEDIFF(MILLISECOND, StartedAt, SYSUTCDATETIME())
   WHERE Id = @OrphanedRunId;
   ```

3. **Also fix orphaned MigrationRecord rows**

   > **Note**: The `fix` command sets orphaned migrations to status `50` (NotMigrated) by default, or `100` (Migrated) when `--last-migration-status migrated` is specified. The manual SQL below marks them as `30` (Failed), which is also valid for manual DBA cleanup.

   ```sql
   UPDATE MigrationRecord
   SET
       MigrationStatusId = 30,  -- Failed (use 50=NotMigrated or 100=Migrated to match Fix command behavior)
       FinishedAt = SYSUTCDATETIME()
   WHERE MigrationRunId = @OrphanedRunId
     AND MigrationStatusId IN (10, 20);  -- Pending or Executing
   ```

### Auto-Fix of Orphaned Runs

**Symptoms:**
- Log message: "Parallel migration detected but N orphaned run(s) found older than 10 minutes. Auto-fixing."
- Migration proceeds after a brief delay

**Explanation:**

When RayMigrator detects a parallel-run conflict (another migration appears to be running), it checks if the existing run is orphaned (older than 10 minutes). If so, it automatically marks the orphaned run as Error and proceeds with the new migration. This threshold is controlled by the internal constant `AutoFixOrphanedRunsThresholdMinutes` (default: 10 minutes).

If the orphaned run is younger than 10 minutes, RayMigrator aborts with a `MigrationAlreadyRunningException`. Use the `fix` command with `--older-than 0` for immediate manual cleanup.

### Transient Database Error After Retries

**Symptoms:**
- "Operation failed after 3 attempts" error
- Database connectivity issues

**Solutions:**

1. **Check network connectivity**
   ```bash
   # Test basic connectivity
   ping database-server
   telnet database-server 1433
   ```

2. **Increase retry settings** (per target)
   ```json
   {
     "Targets": [{
       "DbCommandMaxRetries": 5,
       "DbCommandWaitTimeInMsBeforeRetry": 1000
     }]
   }
   ```

3. **Check for database-side issues**
   - Server overloaded
   - Too many connections
   - Maintenance window
   - Azure SQL throttling

4. **Review transient error codes by database type**

   **SQL Server:**
   - `-2`: Timeout expired
   - `20`: Instance connection error (broken TDS connection / encryption negotiation failure)
   - `64`: Connection established but lost (ERROR_NETNAME_DELETED)
   - `233`: Connection closed during initialization (connection pool exhaustion / server busy)
   - `10053`: Software caused connection abort (WSAECONNABORTED)
   - `10054`: Connection forcibly closed by remote host (WSAECONNRESET)
   - `10060`: Connection attempt timed out (WSAETIMEDOUT)
   - `40197`: Service error processing request (Azure SQL)
   - `40501`: Service is currently busy (Azure SQL)
   - `40613`: Database unavailable (Azure SQL) -- wait and retry
   - `49918`/`49919`/`49920`: Resource or operation limits (Azure SQL)

   **PostgreSQL** (uses SQLSTATE codes):
   - `08000`/`08001`/`08003`/`08004`/`08006`: Connection errors
   - `57P01`/`57P02`/`57P03`: Server shutdown or cannot connect
   - `40001`: Serialization failure
   - `40P01`: Deadlock detected

   **MariaDB/MySQL:**
   - `1040`: Too many connections
   - `1205`: Lock wait timeout exceeded
   - `1213`: Deadlock found
   - `1614`: Transaction rolled back (semi-sync replication uncertainty)
   - `2002`/`2003`: Cannot connect to server
   - `2006`: Server has gone away
   - `2013`: Lost connection during query
   - `2055`: Lost connection at reading authorization packet

   **SQLite:**
   - `5` (SQLITE_BUSY): Database file is locked by another process
   - `6` (SQLITE_LOCKED): Table in the database is locked

### Block-Level Progress Not Persisted

**Symptoms:**
- Resume starts from beginning instead of last block
- `FileUpBlocksMigrated` always 0

**Solutions:**

1. **Verify repository schema version**
   ```sql
   SELECT * FROM MigratorMeta
   ORDER BY CreatedAt DESC;
   ```

2. **Check MigrationRecord table has tracking columns**
   ```sql
   SELECT FileUpBlocksMigrated, FileUpBlocksTotal
   FROM MigrationRecord
   WHERE MigrationRunId = @RecentRunId;
   ```

3. **Ensure templates are updated**
   - Verify `Repository_MigrationRecord_Update.sql` exists
   - Check template version matches schema version

## CLI Tool Execution Issues

### CLI Tool Not Found

**Symptoms:**
- `CliToolExecutionException`: "Failed to start CLI tool 'sqlcmd': No such file or directory"
- `CliToolExecutionException`: "Process.Start returned null"

**Solutions:**

1. **Verify the executable path**
   The `ExecutablePath` in `CliTools[]` must point to an executable that is either an absolute path or available on the system `PATH`:
   ```json
   {
     "CliTools": [{
       "Alias": "sqlcmd-tool",
       "ExecutablePath": "/opt/mssql-tools18/bin/sqlcmd"
     }]
   }
   ```

2. **Test the executable manually**
   ```bash
   # Verify the tool exists and is executable
   which sqlcmd
   sqlcmd --version
   ```

3. **Check file permissions**
   Ensure the executable has execute permissions on the current platform.

### CLI Tool Timeout

**Symptoms:**
- `CliToolTimeoutException`: "CLI tool 'psql' timed out after 120 seconds"

**Solutions:**

1. **Increase the timeout**
   ```json
   {
     "CliTools": [{
       "Alias": "psql-tool",
       "CliToolTimeoutInSeconds": 600
     }]
   }
   ```

2. **Split large migrations** into smaller files to reduce execution time per file.

3. **Check for interactive prompts** — CLI tools that prompt for input (e.g., password) will hang if `CliToolParameters` does not supply the required values. Ensure all placeholders in the `ArgumentTemplate` are covered.

### CLI Tool Exit Code Error

**Symptoms:**
- `CliToolExecutionException`: "CLI tool 'sqlcmd' exited with exit code 1 (not in SuccessExitCodes [0]) while executing 'V001__create_users.sql'."

**Solutions:**

1. **Check `SuccessExitCodes`**
   By default, only exit code `0` is treated as success. Any exit code not in the whitelist is treated as failure. Use range notation to cover multiple codes:
   ```json
   {
     "CliTools": [{
       "Alias": "sqlcmd-tool",
       "SuccessExitCodes": ["0", "1..3"]
     }]
   }
   ```

2. **Enable debug logging** to see stdout and stderr output from the tool:
   ```json
   {
     "RayMigrator": {
       "Serilog": {
         "MinimumLevel": "Debug"
       }
     }
   }
   ```

3. **Run the CLI tool manually** to reproduce the error outside of RayMigrator.

### UseCliToolAlias Not Found

**Symptoms:**
- `ConfigurationValidationException`: "UseCliToolAlias 'X' references a CLI tool that is not defined in CliTools[]"
- `ConfigurationValidationException`: "UseCliToolAlias 'X' is specified but no CliTools are defined in configuration"

**Solutions:**

1. **Verify the alias matches a CliTools[] entry**
   The `UseCliToolAlias` value must match the `Alias` property of one of the entries in the `RayMigrator.CliTools[]` array (case-insensitive comparison):
   ```json
   {
     "CliTools": [{
       "Alias": "sqlcmd-tool"
     }],
     "Products": [{
       "UseCliToolAlias": "sqlcmd-tool"
     }]
   }
   ```

2. **Check the inheritance chain**
   `UseCliToolAlias` cascades through: `ProductDefaults` -> `Product` -> `TargetGroup` -> `Target` -> `migsettings` -> TOML. A non-null value at any level overrides the parent. Use `--startup-info true` to see resolved configuration.

## DAL Discovery Issues

### Unknown DatabaseType Error

**Symptoms:**
- "Cannot create specific DataAccessLayer. Unknown DataAccessLayer for DatabaseType [X]."
- `ConfigurationValidationException` at startup

**Solutions:**

1. **Verify built-in DALs are discovered and `DataAccessLayers/` exists for external plugins**
   The `DalFactory` uses `DependencyContext.Default` (reads from `deps.json`) to discover built-in DAL assemblies, and scans `DataAccessLayers/` subdirectories relative to the application base directory for external DAL plugin DLLs. For external plugins, each database type requires its own subdirectory:
   ```
   <output-dir>/
   └── DataAccessLayers/
       ├── SqlServer/
       │   └── Raycoon.RayMigrator.Database.SqlServer.dll
       ├── PostgreSQL/
       │   └── Raycoon.RayMigrator.Database.PostgreSQL.dll
       ├── MariaDb/
       │   └── Raycoon.RayMigrator.Database.MariaDb.dll
       ├── MySql/
       │   └── Raycoon.RayMigrator.Database.MySql.dll
       └── Sqlite/
           └── Raycoon.RayMigrator.Database.Sqlite.dll
   ```

2. **Rebuild the project**
   The Console project has a post-build target (`CopyDalAssembliesToDataAccessLayers`) that copies DAL DLLs and their SQL template files into the correct subdirectories. If the directory is missing, a clean rebuild usually resolves the issue:
   ```bash
   dotnet build -c Debug
   ```

3. **Check for missing `deps.json` (single-file publish)**
   Built-in DALs are discovered via `DependencyContext.Default`, which reads from the `deps.json` file. If this file is missing or incomplete (e.g., after a custom publish step), built-in DALs will not be found. Ensure the publish configuration generates the `deps.json` file.

4. **Check the subdirectory name matches the `DatabaseType` attribute**
   The subdirectory name does not need to match the configured `DatabaseType` value. DAL discovery works by scanning all DLLs for classes implementing `IDal` with a `[DatabaseType("X")]` attribute. However, each DAL subdirectory must contain the required SQL template files (flat layout, directly in `DataAccessLayers/{Type}/`, not in a `Templates/` subfolder).

### TemplateCache Initialization Error

**Symptoms:**
- "No DataAccessLayer templates found. Ensure the DataAccessLayers/ directory exists at [path] and contains subdirectories with SQL template files for each database type."
- "Could not find the following templates for DataAccessLayer [X]: ..."

**Solutions:**

1. **Ensure `DataAccessLayers/` exists in the output directory**
   The directory must be present at the application base directory path. For published deployments, verify the publish target also copies DAL assemblies.

2. **Verify each DAL subdirectory contains SQL template files**
   Each database type subdirectory (e.g., `DataAccessLayers/SqlServer/`) must contain the required `.sql` template files directly (flat layout, no `Templates/` subfolder).

## Multi-Target Framework Issues

### `dotnet run` Fails with Multi-Target Error

**Symptoms:**
- Error about multiple target frameworks when using `dotnet run`
- "Your project does not target a single TargetFramework"

**Solutions:**

1. **Specify the `--framework` flag**
   All RayMigrator projects target `net10.0;net9.0;net8.0`. When using `dotnet run`, you must specify the target framework:
   ```bash
   dotnet run --framework net10.0 -- migrate-up -p MyProduct -env Dev -rm migrate
   ```

2. **Run from the Console project directory**
   ```bash
   cd Raycoon.RayMigrator.Console
   dotnet run --framework net10.0 -- migrate-up -p MyProduct -env Dev -rm migrate
   ```

3. **Use the compiled executable instead**
   After building, run the executable directly (no `--framework` needed):
   ```bash
   dotnet build -c Release
   ./bin/Release/net10.0/raymigrator migrate-up -p MyProduct -env Dev -rm migrate
   ```

## Known Behaviors and Limitations

### SQL Block Splitting: `GO` Inside Comments or String Literals

The SQL block splitter for SQL Server uses a regex-based approach to split on `GO` statements. The pattern matches `GO` on its own line (case-insensitive). **Known limitation:** `GO` appearing inside multi-line comments (`/* ... GO ... */`) or string literals that span multiple lines will still be treated as a block delimiter and cause an incorrect split.

**Workaround:**
- Avoid placing `GO` on its own line inside multi-line comments.
- If a string literal must contain `GO` on its own line, consider using dynamic SQL or a different approach.

### Environment Variables Not Resolved

When using `{ENV:VARIABLE_NAME}` placeholders in configuration, if the referenced environment variable does not exist, is empty, or is whitespace-only, RayMigrator logs each unresolved variable as an error and **terminates with an `ApplicationStartupException`**. All unresolved variables are reported in a single startup attempt, so you can fix all of them at once.

> **Note:** In SQL migration files (not configuration), unresolved `{ENV:}` placeholders are replaced with an empty string and logged as a warning instead of causing termination. See [Environment Variables](../06-configuration-reference/environment-variables.md) for full details.

**How to debug:**
```bash
raymigrator migrate-up -p MyProduct -env Dev -rm simulate --reveal-sensitive-data true --startup-info true
```
Check the logged connection strings and configuration values for unexpected empty values.

**Prevention:**
- Verify all referenced environment variables are set before running RayMigrator.
- Use `printenv VARIABLE_NAME` or `echo $VARIABLE_NAME` to confirm values.

### MySQL / MariaDB TIMESTAMP Y2038 Limit

RayMigrator's repository audit columns (`CreatedAt`, `StartedAt`, `FinishedAt`, `HistorizedAt`) on MySQL and MariaDB use the `TIMESTAMP` data type, which internally stores a 32-bit Unix epoch. This format **overflows on 2038-01-19 03:14:07 UTC** — a value past that instant cannot be inserted and triggers an out-of-range error.

**Why this is not a blocker for RayMigrator:**

- Audit columns record wall-clock `NOW()` at the moment RayMigrator executes (migration runs, record updates, historization). There is no user-controlled path that inserts a future date beyond a few minutes from real time.
- The 2038 ceiling is more than a decade away from today's build; by the time it matters, MySQL 8.4+ / MariaDB 10.11+ 64-bit-TIMESTAMP support is expected to be widespread.

**When this becomes relevant:**

If a future RayMigrator feature needs to persist application-supplied future dates (far-future scheduled runs, certificate expiry audit, etc.) in the repository, migrate to:
- MySQL 8.4+ with 64-bit TIMESTAMP support, or
- MariaDB 10.11+ with the equivalent 64-bit TIMESTAMP option.

The column types would then need to change accordingly. For the current RayMigrator audit use case, standard `TIMESTAMP` on MySQL 8.0+ and MariaDB 10.5+ is sufficient.

### DDL Transactions on MariaDB and MySQL

MariaDB and MySQL do not support DDL statements inside transactions. DDL operations (`CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, `CREATE INDEX`, etc.) cause an **implicit commit**. This means:
- Any pending DML changes in the same transaction are committed when a DDL statement executes.
- If the migration fails after a DDL statement, the DDL change **cannot be rolled back**.

**Recommendation:**
- Set `UseTransaction = false` in the TOML metadata for migration files containing DDL on MariaDB/MySQL.
- Separate DDL and DML into different migration files when possible.

### UseTransaction Ignored When CLI Tool Is Active

When a migration file has `UseTransaction` explicitly set in its TOML header but execution is routed through an external CLI tool via `UseCliToolAlias`, the `UseTransaction` setting has no effect. CLI tools manage their own transaction behavior. RayMigrator logs a safety warning: `"UseTransaction has no effect when a CLI tool executes the migration."`

**Recommendation:**
- Remove `UseTransaction` from TOML headers of files that are executed via CLI tools, or accept the warning as informational.
- The warning only appears when `UseTransaction` is explicitly set in the TOML header; default transaction behavior does not trigger the warning.

### SQL Block Splitting Skipped for CLI Tool Execution

When a migration file is executed by an external CLI tool (via `UseCliToolAlias`), SQL block splitting (e.g., splitting on `GO` for SQL Server) is skipped. CLI tools execute the entire file as a single unit, so delimiter-based splitting is not needed. This means:
- Block-level tracking (`FileUpBlocksMigrated` / `FileUpBlocksTotal`) always shows 1 block total for CLI-tool-executed files.
- The `GO` inside comments/string literals limitation does not apply to CLI-tool-executed files.

### Atomic Shared Connection Activates Automatically

When the Repository and a migration Target share the **same ConnectionString**, RayMigrator activates the atomic shared-connection path automatically. All SQL blocks and the corresponding repository status updates execute in a single transaction. This is transparent and requires no configuration change.

**Verification:**

Enable Information-level logging (the default). When the atomic path is active, RayMigrator logs:
```
Using atomic shared connection for {Filename} (target and repository share the same database)
```

**Conditions that prevent the atomic path (fallback to standard behavior):**
- `UseTransaction = false` in the migration file's TOML metadata
- `MigrationErrorAction = Ignore` in configuration
- Repository and Target use different `DatabaseType` values
- Repository and Target have different `ConnectionString` values (ordinal/byte-exact comparison)

See [Atomic Shared Connection](../02-core-concepts/error-handling.md#atomic-shared-connection) for full details.

### SplitSqlIntoBlocks Fallback Behavior

When all blocks are empty after splitting SQL content by the block delimiter, the method falls back to returning the original content as a single block. This prevents migrations from being silently skipped when the delimiter produces only whitespace blocks.

## Getting Help

If these solutions don't resolve your issue:

1. **Enable debug logging** (inside the `RayMigrator` section)
   ```json
   {
     "RayMigrator": {
       "Serilog": {
         "MinimumLevel": "Debug"
       }
     }
   }
   ```

2. **Run in simulate mode first**
   ```bash
   raymigrator migrate-up -p MyProduct -env Dev -rm simulate
   ```

3. **Check GitHub issues**
   - https://github.com/RAYCOON/RayMigrator/issues

4. **Report new issues**
   - Include RayMigrator version
   - Include database type and version
   - Include relevant configuration (redact credentials)
   - Include complete error message and stack trace

## Related Documentation

- [Configuration Reference](../06-configuration-reference/appsettings-hierarchy.md)
- [CLI Tools Options](../06-configuration-reference/cli-tools-options.md)
- [Error Handling](../02-core-concepts/error-handling.md)
- [CLI Reference](../08-cli-reference/global-options.md)
- [DAL Architecture](../03-database-layer/dal-architecture.md)
- [SQL Dialects and DDL Transactions](../03-database-layer/sql-dialects.md)
- [Migration State Machine](../02-core-concepts/migration-state-machine.md)
- [Test Infrastructure](../10-testing/test-infrastructure.md)
