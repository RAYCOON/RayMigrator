# Template Customization

Guide for customizing SQL templates used by RayMigrator.

## Overview

RayMigrator uses SQL templates for:
- Repository schema creation
- Logging schema creation
- Data operations (CRUD)
- Migration execution

Templates are database-specific and can be customized for special requirements.

## Template Location

Templates are stored as flat files within each DAL project (`Raycoon.RayMigrator.Database.SqlServer`, `.PostgreSQL`, `.MariaDb`, `.MySql`, `.Sqlite`). At build time, templates are copied to `DataAccessLayers/{DatabaseType}/` in the output directory via `<Content>` items that propagate transitively through ProjectReference:

```
Raycoon.RayMigrator.Database.SqlServer/
└── Templates/
    ├── DatabaseLogging_CheckCreate.sql
    ├── DatabaseLogging_Count.sql
    ├── DatabaseLogging_Delete.sql
    ├── DatabaseLogging_Insert.sql
    ├── Repository_CheckCreate.sql
    ├── Repository_Upgrade_1.sql
    ├── Repository_Upgrade_2.sql
    ├── Repository_Upgrade_3.sql
    ├── Repository_MigratorMeta_CheckInsert.sql
    ├── Repository_Drop.sql
    ├── Repository_Environment_CheckInsert.sql
    ├── Repository_Environment_Select.sql
    ├── Repository_MigrationRecordHistory_Count.sql
    ├── Repository_MigrationRecordHistory_Delete.sql
    ├── Repository_MigrationRecordHistory_Select.sql
    ├── Repository_MigrationRecord_FixOrphaned.sql
    ├── Repository_MigrationRecord_GetInterrupted.sql
    ├── Repository_MigrationRecord_Insert.sql
    ├── Repository_MigrationRecord_Select.sql
    ├── Repository_MigrationRecord_Update.sql
    ├── Repository_MigrationRecord_UpdateHash.sql
    ├── Repository_MigrationRecord_UpdateRollback.sql
    ├── Repository_MigrationRun_FixOrphaned.sql
    ├── Repository_MigrationRun_Heartbeat.sql
    ├── Repository_MigrationRun_Insert.sql
    ├── Repository_MigrationRun_Select.sql
    ├── Repository_MigrationRun_SelectOrphaned.sql
    ├── Repository_MigrationRun_Update.sql
    ├── Repository_Product_CheckInsert.sql
    └── Repository_Product_Select.sql
```

Each DAL (SqlServer, PostgreSQL, MariaDb, MySql, Sqlite) has the same 30 template files with database-specific SQL. Template file names correspond to `TemplateType` enum values. `TemplateCache` loads templates from `DataAccessLayers/{Type}/` on the filesystem at startup.

## Template TOML Headers

Each SQL template file begins with a TOML-like header inside a SQL block comment (`/* ... */`). This header is documentation only and is not parsed by the runtime -- it serves as a structured contract for template authors and reviewers:

```sql
/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_MigrationRecord_Insert"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-09-09.1"

[Description]
Function = """
Creates a new Migration record or resets an existing archived record
to track individual migration file execution.
Supports block-level tracking for recovery from interrupted migrations.
"""

Behaviour = """
- When @ExistingMigrationRecordId = 0: INSERT a new record (original behaviour)
- When @ExistingMigrationRecordId > 0: UPDATE the existing record, resetting all fields
- Return value >= 0: Success (MigrationRecordId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- FileUpBlocksMigrated starts at 0, incremented as blocks execute
- StartedAt timestamp recorded immediately
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'rm_')"

[Parameters]
# SQL parameters bound at runtime
ExistingMigrationRecordId = "INT | REQUIRED | 0 = INSERT new record, >0 = UPDATE existing record with this ID"
ProductId           = "INT | REQUIRED | Product ID from Product table"
EnvironmentId       = "INT | REQUIRED | Environment ID from Environment table"
MigrationRunId      = "INT | REQUIRED | Parent MigrationRun ID"
MigrationRunModeId  = "TINYINT | REQUIRED | Run mode of the command (always 100=Migrate: Validate and Simulate runs never write rows, #19)"
MigrationOperationId= "TINYINT | REQUIRED | Operation: 5=Rollback, 50=MigrateDown, 100=MigrateUp, 110=Baseline"
# ... additional parameters

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created = "N (MigrationRecordId),Migration record with Id [N] successfully created for file [Filename]"
Success_Updated = "N (MigrationRecordId),Migration record with Id [N] successfully reset for file [Filename]"

[ModificationNotes]
Note1 = "SELECT result format: 'code,message' - DO NOT change this format"
Note2 = "No commas allowed in error messages"
Note3 = "Use SYSUTCDATETIME() for StartedAt timestamp"
Note4 = "ResultCode catalog: see TemplateResultCode.cs in Shared project"
================================================================================
*/
```

### Header Sections

| Section | Purpose |
|---------|---------|
| `[RayMigratorTemplate]` | Template identity: `TemplateType`, `DatabaseType`, `Author`, `Version` |
| `[Description]` | `Function` (what the template does) and `Behaviour` (return value semantics) |
| `[ConfigPlaceholders]` | Documents which `{CFG:*}` placeholders the template uses |
| `[Parameters]` | Documents which `@Parameter` SQL parameters are expected at runtime |
| `[ReturnValues]` | Documents the `'code[,code...],message'` SELECT format returned by the template |
| `[ModificationNotes]` | Rules and constraints for anyone modifying the template |

## Placeholder Syntax

### Configuration Placeholders

Format: `{CFG:PropertyName}`

| Placeholder | Source | Example Value |
|-------------|--------|---------------|
| `{CFG:SchemaName}` | Repository.SchemaName | `ray` |
| `{CFG:TableBaseName}` | Repository.TableBaseName | `Ray` |

These are the only two allowed `{CFG:*}` placeholders, defined in `ConfigurationConstants.AllowedTemplateVariableNameReplacements`. Replacement uses reflection to match the placeholder name to properties on the `RepositoryOptions` (or `DatabaseLoggingOptions`) class passed to `TemplateCache.GetTemplate<T>()` or `GetRepositoryTemplate<T>()`.

### Environment Placeholders

Format: `{ENV:VariableName}`

| Placeholder | Source | Example |
|-------------|--------|---------|
| `{ENV:DB_PASSWORD}` | System environment variable | `SecretPassword` |

`{ENV:*}` placeholders are resolved once during `TemplateCache` initialization at startup. If a referenced environment variable does not exist or is empty/whitespace, a `ConfigurationValidationException` is thrown and startup is aborted.

### SQL Parameters

Format: `@ParameterName`

Standard ADO.NET parameters used in DML operations.

## Template Result Convention

All templates that use `ExecuteScalar` must return a result string in the format `'code,message'`, optionally with further integer codes before the message (`'code,code,message'`):

- **ResultCode >= 0**: Success (e.g., the new record ID)
- **ResultCode < 0**: Error (migration aborted, see `TemplateResultCode.cs` for the catalog of known codes)
- **Further codes**: template-specific information; `Repository_CheckCreate` returns the schema version as its first code and `unversionedTables` (`1`/`0`) as its second, from which the engine decides which `Repository_Upgrade_{n}` steps to run (ADR-024); `DatabaseLogging_Count` returns the row count and `1`, or `0,0` when the log table is not in the repository database, and with `@ExistsOnly` = 1 `0,1` without a count. The first code of a count template may exceed the `int` range and is read as a `long`
- **ResultMessage**: A human-readable description after the last code; it must not start with an integer followed by a comma

The result is parsed by `TemplateExecutor.ExecuteScalarWithNegativeResultCodeExceptionAsync()` into a `TemplateResponse` object with `ResultCode` (int), `ResultCodes` (int[]) and `ResultMessage` (string?).

Known result codes (from `TemplateResultCode` in `Shared/Constants/`):

**SQL Template ResultCodes (negative, returned by SQL templates):**

| Code | Constant | Meaning |
|------|----------|---------|
| `-1` | `GeneralError` | General/unclassified template error; also `DatabaseLogging_CheckCreate` on SQL Server and MySQL when the logging lock is not acquired within 60 seconds (reached only when `DatabaseLogging.DbCommandTimeoutInSeconds` is above 60 or 0; the default of 20 seconds times out first, and the initialization does not retry that timeout) |
| `-2` | `MigrationAlreadyRunning` | Parallel run prevention |
| `-12` | `RepositoryMultipleVersionEntries` | Multiple MigratorMeta entries for one RayMigrator version (`Repository_MigratorMeta_CheckInsert`) |
| `-20` | `ProductNameEmpty` | Product name is NULL or empty |
| `-30` | `MigrationRunNotFound` | MigrationRun with given Id does not exist |
| `-31` | `MigrationRunNotInRunningState` | MigrationRun not in Running state |
| `-32` | `RepositorySchemaLockNotAcquired` | MariaDB and MySQL: the repository schema lock (`GET_LOCK()`) was not acquired within 60 seconds |
| `-40` | `MigrationNotFound` | Migration with given Id does not exist |
| `-50` | `EnvironmentNameEmpty` | Environment name is NULL or empty |

Unknown negative codes (e.g., from user-customized templates) throw `UndefinedTemplateResultException` instead of `TemplateResultException`.

**C# Backend ErrorCodes (positive, assigned by backend logic):**

| Code | Constant | Meaning |
|------|----------|---------|
| `1001` | `RequireRollbackFileValidationFailed` | Missing rollback files when RequireRollbackFile is enabled |
| `1002` | `MigrationFileParsingFailed` | Migration file parsing error |
| `1003` | `ConfigurationValidationFailed` | Configuration validation error |
| `1004` | `Cancelled` | The host cancelled the run and the engine stopped it: the run of a migrate-up, migrate-down or baseline is `MigrationRunResult.Cancelled`. Also the `ErrorCode` of an update-hash or fix that stopped early. The read commands (validate-hash, status, history) throw an `OperationCanceledException` instead, and the repository maintenance service reports a stopped cleanup with `RetentionResult.Completed = false` and no code |
| `1005` | `CancelledRollbackIncomplete` | A cancelled migrate-up whose rollback did not complete cleanly (`MigrationRunResult.Error`) |
| `1006` | `OrphanedRunClosed` | The orphan repair closed the run (`MigrationRunResult.Error`): `Repository_MigrationRun_FixOrphaned` writes it into `MigrationRun.ErrorCode`, with a message that names the last heartbeat, where the row has none; it is never the `ErrorCode` of a command's result, and the run history reports it with the run it closed |

## Template Examples

### Inline Historization in Update Templates

`Repository_MigrationRecord_Update.sql` and `Repository_MigrationRecord_UpdateRollback.sql` include an inline `INSERT INTO MigrationRecordHistory` that fires whenever a migration record transitions to a terminal state. The insert is gated on `MigrationStatusId IN (30, 50, 100)` (Failed, NotMigrated, Migrated). This means historization happens at the moment each record reaches its final status — no separate archive step is required.

When customizing these templates, preserve the inline `INSERT INTO MigrationRecordHistory` block and ensure the `HistorizedAt` column is populated with the current UTC timestamp.

### Retention Templates

`Repository_MigrationRecordHistory_Delete`, `Repository_MigrationRecordHistory_Count`, `DatabaseLogging_Delete` and `DatabaseLogging_Count` serve `IRepositoryMaintenanceService` and never run in a command (see [Repository Schema: Retention](../03-database-layer/repository-schema.md#retention)). When customizing them, keep the contract the service relies on: a delete is one statement that removes at most `@BatchSize` rows older than `@CutoffUtc` (UTC text `yyyy-MM-dd HH:mm:ss`), the oldest first, spares every row whose `MigrationRunId` points to a `MigrationRun` with `FinishedAt IS NULL`, and returns `'n,message'` with the rows it deleted; the service stops a table after a batch that deleted fewer than `@BatchSize`, so a delete must not take fewer while more such rows are there. A count counts the same rows. The log templates run with the schema and prefix of the `Repository` section, and `DatabaseLogging_Count` reports `'0,0,message'` when the log table is not there. `DatabaseLogging_Count` also takes `@ExistsOnly`: with `1` it counts nothing and returns `'0,1,message'` when the log table exists, which the service asks before it deletes from the log, so that a large log is not read whole; with `0` it counts, for the dry run. The service runs the four templates with the command timeout of the repository and without its retries; the next batch or the next call takes a failed batch up.

### Schema Creation Template

The schema is versioned (ADR-024): `Repository_CheckCreate.sql` creates only the `RepositorySchemaVersion` table and reports the version, and the schema step `Repository_Upgrade_1.sql` creates all 11 repository tables (MigratorMeta, Product, Environment, MigrationRun, MigrationRunMeta, MigrationRecord, MigrationRecordHistory, MigrationRunMode, MigrationOperation, MigrationRunResult, MigrationStatus) with their master data. A customised step must stay idempotent and return its own version number; a later version is a new `Repository_Upgrade_{n}` template, never a change to an existing step. Every key, foreign key and index name carries the `{CFG:TableBaseName}` prefix (`pk_{CFG:TableBaseName}MigrationRecord` below), so that repositories with different prefixes can share a schema; keep it in the names you add. Here is the MigrationRecord table excerpt (SQL Server):

```sql
-- Repository_Upgrade_1.sql (excerpt: MigrationRecord table)
CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] (
	Id                   int    IDENTITY(1,1)  NOT NULL,
	ProductId            int      NOT NULL,
	EnvironmentId        int      NOT NULL,
	MigrationRunId       int      NOT NULL,
	MigrationRunModeId   tinyint      NOT NULL,
	MigrationOperationId tinyint      NOT NULL,
	MigrationStatusId    tinyint      NOT NULL,
	ReleaseVersion       nvarchar(100)      NOT NULL,
	TargetGroupAlias     nvarchar(100)      NOT NULL,
	TargetAlias          nvarchar(100)      NOT NULL,
	Filename             nvarchar(200)      NOT NULL,
	FileOrderId          int      NOT NULL,
	FileUpHash           varchar(100)      NOT NULL,
	FileUpConfigHash     varchar(100)      NULL,
	FileUpBlocksHash     varchar(100)      NOT NULL,
	FileUpBlocksMigrated int      NOT NULL,
	FileUpBlocksTotal    int      NOT NULL,
	FileUpConfigJson     nvarchar(max)     NULL,
	MigrateDownFileExists bit      NOT NULL,
	FileDownHash         varchar(100)      NULL,
	FileDownConfigHash   varchar(100)      NULL,
	FileDownBlocksHash   varchar(100)      NULL,
	FileDownBlocksMigrated int      NULL,
	FileDownBlocksTotal  int      NULL,
	FileDownConfigJson   nvarchar(max)     NULL,
	StartedAt            datetime2(3)   NULL,
	FinishedAt           datetime2(3)   NULL,
	DurationInMs         bigint      NULL,
	CONSTRAINT pk_{CFG:TableBaseName}MigrationRecord PRIMARY KEY  ( Id )
 );
```

### DML Operation Template

The current `Repository_MigrationRecord_Insert.sql` template supports both INSERT (when `@ExistingMigrationRecordId = 0`) and UPDATE/reset (when `@ExistingMigrationRecordId > 0`) of an existing archived record.

```sql
-- Repository_MigrationRecord_Insert.sql (SQL Server, excerpt)
IF @ExistingMigrationRecordId > 0
BEGIN
    -- Reset existing archived record (UPDATE path omitted for brevity)
END
ELSE
BEGIN
    -- Insert new record
    INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
    (
        ProductId,
        EnvironmentId,
        MigrationRunId,
        MigrationRunModeId,
        MigrationOperationId,
        MigrationStatusId,
        ReleaseVersion,
        TargetGroupAlias,
        TargetAlias,
        Filename,
        FileOrderId,
        FileUpHash,
        FileUpConfigHash,
        FileUpBlocksHash,
        FileUpBlocksMigrated,
        FileUpBlocksTotal,
        FileUpConfigJson,
        MigrateDownFileExists,
        StartedAt
    )
    VALUES
    (
        @ProductId,
        @EnvironmentId,
        @MigrationRunId,
        @MigrationRunModeId,
        @MigrationOperationId,
        @MigrationStatusId,
        -- ... remaining @Parameters
        0,  -- FileUpBlocksMigrated starts at 0
        @FileUpBlocksTotal,
        @FileUpConfigJson,
        @MigrateDownFileExists,
        SYSUTCDATETIME()
    );

    SET @MigrationRecordId = SCOPE_IDENTITY();

    -- Returns: 'code,message' format
    SELECT CAST(@MigrationRecordId AS VARCHAR(10)) + ',Migration record with Id ['
        + CAST(@MigrationRecordId AS VARCHAR(10)) + '] successfully created for file ['
        + COALESCE(@Filename, 'NULL') + ']';
END
```

### Case-Insensitive Check-Insert Templates

`Repository_Product_CheckInsert.sql` and `Repository_Environment_CheckInsert.sql` follow a two-parameter contract:

- `@Name` -- the name in original casing (e.g., `MyApplication`, `Docker`)
- `@NameLower` -- the pre-computed lowercase form used for the lookup

The lookup must be performed against `NameLower` to remain case-insensitive. Both `Product` and `Environment` tables carry a `UNIQUE` index on `NameLower`, and two first starts of the same name at the same moment must both succeed with the Id of the one row: the SQL Server template looks the name up with `UPDLOCK, ROWLOCK, HOLDLOCK` in a transaction with the insert, so the second start waits and then finds the row (the other engines insert idempotently, see [External DAL Development](external-dal-development.md#required-templates)). Here is the SQL Server Product template (excerpt):

```sql
-- Repository_Product_CheckInsert.sql (SQL Server, excerpt)
if (@Name IS NULL OR LEN(@Name) = 0)
    begin
        SELECT '-20,Product with empty name [' + ISNULL(@Name, 'NULL') + '] is not allowed!'
        return;
    end;

declare @ProductId int, @numberOfRows int;

BEGIN TRANSACTION;

select @ProductId = [Id] from [{CFG:SchemaName}].[{CFG:TableBaseName}Product] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) where [NameLower] = @NameLower;
SET @numberOfRows = @@rowcount;

if (@numberOfRows = 1)
    begin
        COMMIT TRANSACTION;
        SELECT CAST(@ProductId AS varchar(10)) + ',Product [' + @Name + '] with Id [' + CAST(@ProductId AS varchar(10)) + '] found';
        return;
    end;

if (@numberOfRows = 0)
    begin
        INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}Product] (Name, NameLower, CreatedAt)
        VALUES (@Name, @NameLower, SYSUTCDATETIME());

        SET @ProductId = SCOPE_IDENTITY();
        COMMIT TRANSACTION;
        SELECT CAST(@ProductId AS varchar(10)) + ',Product [' + @Name + '] with Id [' + CAST(@ProductId AS varchar(10)) + '] successfully created';
        return;
    end;
```

The C# caller (`TemplateExecutor.RepositoryProductCheckInsertAsync` / `RepositoryEnvironmentCheckInsertAsync`) pre-computes the lowercase form via `.ToLowerInvariant()` before binding the parameters.

When customizing these templates, preserve both parameters and the lookup-by-`NameLower` semantics; returning a `ProductId` or `EnvironmentId` via the `{code},{message}` convention is required by the engine.

## Customization Approaches

### 1. Replace Template Files

Since templates are loaded from the output directory at runtime, you can replace template files directly. Place modified `.sql` files in the corresponding `DataAccessLayers/{DatabaseType}/` directory in the build output:

```
bin/Debug/net10.0/DataAccessLayers/SqlServer/
└── Repository_CheckCreate.sql  <-- Your customized version
```

> **Note**: The DAL classes (`DalSqlServer`, `DalPostgreSql`, `DalMariaDb`, `DalMySql`, `DalSqlite`) are `public` (required for cross-assembly discovery via `Activator.CreateInstance`) but do not expose a `GetTemplate()` override point. Template customization is done by replacing the physical SQL files, not by subclassing the DAL.

### 2. Template Post-Processing

> **Design Pattern**: There is currently no `ITemplatePostProcessor` interface in the codebase. This is a suggested pattern for future extensibility.

To add post-processing, you would modify `TemplateCache` to apply transformations after loading:

```csharp
// Hypothetical extension — not currently implemented
// Runs on the loaded SQL Server templates, before {CFG:*} is replaced, so the key names still read pk_{CFG:TableBaseName}...
public string Process(string template)
{
    if (template.Contains("PRIMARY KEY"))
    {
        template = template.Replace(
            "CONSTRAINT pk_{CFG:TableBaseName}",
            "[AuditCreatedBy] NVARCHAR(100) NULL,\n" +
            "[AuditModifiedBy] NVARCHAR(100) NULL,\n" +
            "CONSTRAINT pk_{CFG:TableBaseName}");
    }
    return template;
}
```

### 3. Custom Placeholder Resolution

> **Design Pattern**: There is currently no `IPlaceholderResolver` interface. Placeholder resolution is handled internally by `TemplateCache` (for `{ENV:*}` during initialization and `{CFG:*}` per call via reflection-based property matching). The allowed `{CFG:*}` names are hardcoded in `ConfigurationConstants.AllowedTemplateVariableNameReplacements` (currently `SchemaName` and `TableBaseName`).

To add custom placeholders, you would need to extend `AllowedTemplateVariableNameReplacements` in `ConfigurationConstants` and add matching properties to the options class passed to `GetTemplate<T>()`.

## Common Customizations

### Adding Audit Columns

```sql
-- Custom: Add audit columns to MigrationRecord table (SQL Server example)
CREATE TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] (
    -- Standard columns...
    Id INT NOT NULL IDENTITY(1,1),
    ProductId INT NOT NULL,
    -- ...

    -- Custom audit columns
    CreatedBy NVARCHAR(100) NULL DEFAULT SYSTEM_USER,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedBy NVARCHAR(100) NULL,
    ModifiedAt DATETIME2 NULL,

    -- Constraints...
);
```

### Custom Indexes

```sql
-- Add index for common query pattern
CREATE NONCLUSTERED INDEX [IX_{CFG:TableBaseName}MigrationRecord_ReleaseVersion_State]
    ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord](
        [ReleaseVersion],
        [MigrationStatusId]
    )
    INCLUDE ([Filename], [TargetGroupAlias]);
```

### Partitioning (Enterprise)

```sql
-- Partition MigrationRecordHistory by date
CREATE PARTITION FUNCTION [PF_{CFG:TableBaseName}MigrationRecordHistory_Date](DATETIME2)
AS RANGE RIGHT FOR VALUES (
    '2024-01-01', '2024-04-01', '2024-07-01', '2024-10-01',
    '2025-01-01', '2025-04-01', '2025-07-01', '2025-10-01'
);

CREATE PARTITION SCHEME [PS_{CFG:TableBaseName}MigrationRecordHistory_Date]
AS PARTITION [PF_{CFG:TableBaseName}MigrationRecordHistory_Date]
ALL TO ([PRIMARY]);

CREATE TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecordHistory] (
    -- columns...
) ON [PS_{CFG:TableBaseName}MigrationRecordHistory_Date]([CreatedAt]);
```

### Row-Level Security

```sql
-- Add RLS for multi-tenant scenarios
CREATE SECURITY POLICY [{CFG:SchemaName}].[{CFG:TableBaseName}TenantPolicy]
ADD FILTER PREDICATE [{CFG:SchemaName}].fn_TenantFilter([ProductId])
ON [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord];
```

## Template Executor

### TemplateExecutor

`TemplateExecutor` is a concrete class (namespace `Raycoon.RayMigrator.Core`, source file in the Infrastructure project) that executes SQL templates via `IDal`. All public methods are **asynchronous** (`…Async`, returning a `Task`) and take `CancellationToken cancellationToken = default` as their last parameter, which they hand to the DAL; a call that fails after the token was cancelled ends with the DAL's `OperationCanceledException` instead of a `TemplateExecutionException`. Repository options and the DAL instance are lazily initialized from `IMigrationContextAccessor.Current` on first use:

```csharp
public class TemplateExecutor
{
    public TemplateExecutor(TemplateCache templateCache, ILogger<TemplateExecutor> logger,
        IMigrationContextAccessor ctxAccessor) { ... }

    // Every method below ends with CancellationToken cancellationToken = default (written as "ct" here)

    // Repository infrastructure
    public Task RepositoryCheckCreateAsync(ct) { ... }   // schema steps through RepositorySchemaExecutor
    public Task RepositoryProductCheckInsertAsync(ct) { ... }
    public Task RepositoryEnvironmentCheckInsertAsync(ct) { ... }
    public Task<bool> RepositoryProductSelectAsync(ct) { ... }
    public Task<bool> RepositoryEnvironmentSelectAsync(ct) { ... }

    // MigrationRun operations
    public Task RepositoryMigrationRunInsertAsync(string migrationRunSettingsJson, ct) { ... }
    public Task RepositoryMigrationRunUpdateAsync(MigrationRunResult runResult, string? errorCode = null, string? errorMessage = null, ct) { ... }
    public Task<bool> RepositoryMigrationRunHeartbeatAsync(ct) { ... }
    public Task<List<Dictionary<string, object?>>> RepositoryMigrationRunSelectAsync(int limit, ct) { ... }
    public Task<List<Dictionary<string, object?>>> RepositoryMigrationRunSelectOrphanedAsync(int productId, int environmentId, ct) { ... }
    public Task<bool> RepositoryMigrationRunFixOrphanedAsync(int migrationRunId, int thresholdMinutes, ct) { ... }

    // Migration operations
    public Task<int> RepositoryMigrationInsertAsync(int existingMigrationRecordId, string filename, string releaseVersion,
        string targetGroupAlias, string targetAlias, int fileOrderId, string fileUpHash,
        string? fileUpConfigHash, string fileUpBlocksHash, int fileUpBlocksTotal,
        string? fileUpConfigJson, bool migrateDownFileExists, ct) { ... }   // Returns MigrationRecordId
    public Task<List<MigrationRecord>> RepositoryMigrationSelectAsync(ct) { ... }   // always reads Migrate-mode records
    public Task<List<MigrationRecord>> RepositoryMigrationRecordHistorySelectAsync(ct) { ... }
    public Task<InterruptedMigrationInfo?> RepositoryMigrationGetInterruptedAsync(ct) { ... }
    public Task<int> RepositoryMigrationRecordFixOrphanedAsync(int migrationRunId, MigrationStatus? status, ct) { ... }
    public Task RepositoryMigrationUpdateHashAsync(int migrationRecordId, string fileUpHash,
        string? fileUpConfigHash, string fileUpBlocksHash, ct) { ... }

    // Standard update (DAL manages connection lifecycle)
    public Task RepositoryMigrationUpdateAsync(int migrationRecordId, MigrationStatus migrationStatus,
        int fileUpBlocksMigrated, ct) { ... }
    public Task RepositoryMigrationUpdateRollbackAsync(int migrationRecordId, MigrationStatus migrationStatus,
        string fileDownHash, string? fileDownConfigHash, string fileDownBlocksHash,
        int fileDownBlocksMigrated, int fileDownBlocksTotal, string? fileDownConfigJson, ct) { ... }

    // Shared-connection update overloads (atomic path: caller provides connection + transaction)
    public Task RepositoryMigrationUpdateAsync(int migrationRecordId, MigrationStatus migrationStatus,
        int fileUpBlocksMigrated, DbConnection connection, DbTransaction transaction,
        int repoCommandTimeoutInSeconds, ct) { ... }
    public Task RepositoryMigrationUpdateRollbackAsync(int migrationRecordId, MigrationStatus migrationStatus,
        string fileDownHash, string? fileDownConfigHash, string fileDownBlocksHash,
        int fileDownBlocksMigrated, int fileDownBlocksTotal, string? fileDownConfigJson,
        DbConnection connection, DbTransaction transaction, int repoCommandTimeoutInSeconds, ct) { ... }

    // Core execution helpers
    // Standard overload: DAL manages connection
    public Task<TemplateResponse> ExecuteScalarWithNegativeResultCodeExceptionAsync(
        Template template, IDal dal, DalSettings dalSettings,
        DalParameterList? dalParameterList, ILogger? logger = null, EventId? eventId = null, ct) { ... }
    // Shared-connection overload: caller provides connection + transaction
    public Task<TemplateResponse> ExecuteScalarWithNegativeResultCodeExceptionAsync(
        Template template, IDal dal, DbConnection connection, DbTransaction transaction,
        int commandTimeoutInSeconds, DalParameterList? dalParameterList,
        ILogger? logger = null, EventId? eventId = null, ct) { ... }

    // Template resolution + execution flow:
    // 1. Get template: _templateCache.GetRepositoryTemplate(templateType, _repository)
    //    -> {CFG:*} placeholders replaced here via reflection
    // 2. Build DalParameterList with @Parameters
    // 3. Execute via _repositoryDal.ExecuteScalarAsync(...) or ExecuteReaderAsync(...)
    // 4. Parse result string into TemplateResponse (ResultCode, ResultMessage)
}
```

### TemplateCache

`TemplateCache` is a concrete class (namespace `Raycoon.RayMigrator.Core.Templates`, source file in the Infrastructure project) that eagerly loads all templates from the filesystem at startup:

```csharp
public class TemplateCache
{
    // Internal storage: Dictionary<databaseType, Dictionary<TemplateType, Template>>
    // All templates loaded during Initialize() from filesystem:
    //   Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataAccessLayers")

    // {ENV:*} placeholders are replaced during Initialize() (once at startup)
    // {CFG:*} placeholders are replaced per call in GetTemplate/GetRepositoryTemplate

    public TemplateCache(bool revealSensitiveData, ILogger<TemplateCache> logger) { ... }

    // Template retrieval (all replace {CFG:*} placeholders via reflection)
    public Template GetTemplate<T>(string databaseType, TemplateType templateType, T propertyClass) { ... }
    public Template GetRepositoryTemplate<T>(TemplateType templateType, T propertyClass)
        where T : RepositoryOptions { ... }
    public string GetTemplateContent<T>(string databaseType, TemplateType templateType, T propertyClass) { ... }

    // Validation and discovery
    public void ValidateConfigurationAgainstTemplateCache(RayMigratorOptions options) { ... }
    public List<string> GetAvailableDatabaseTypes() { ... }
}
```

The constructor loads the templates only. `ValidateConfigurationAgainstTemplateCache(options)` checks that the Repository and every TargetGroup `DatabaseType` of a configuration has templates; the CLI pipeline calls it once after the options are bound, a host for every configuration it runs.

## Testing Custom Templates

### Verifying Placeholder Resolution

After customizing templates, verify that `{CFG:*}` placeholders resolve correctly by checking the output SQL. The `TemplateCache.GetRepositoryTemplate<T>()` method uses reflection to match `{CFG:PropertyName}` to properties on the `RepositoryOptions` class. Only two property names are allowed (defined in `ConfigurationConstants.AllowedTemplateVariableNameReplacements`):

```csharp
// RepositoryOptions properties used for {CFG:*} resolution:
// SchemaName    -> {CFG:SchemaName}
// TableBaseName -> {CFG:TableBaseName}
```

If any `{CFG:*}` placeholders remain unreplaced after substitution, `TemplateCache` throws a `ConfigurationValidationException`.

### Integration Tests

The project uses xUnit and AwesomeAssertions for testing:

```csharp
[Fact]
public async Task CustomTemplate_CreatesRepositoryTables()
{
    // TemplateExecutor.RepositoryCheckCreateAsync() creates the repository through
    // Repository_CheckCreate.sql and the schema steps Repository_Upgrade_{n}.sql
    await _templateExecutor.RepositoryCheckCreateAsync(TestContext.Current.CancellationToken);

    // Verify table exists using the DAL
    // TryGetDal returns true with the DAL, or throws ConfigurationValidationException for unknown types
    DalFactory.TryGetDal("SqlServer", connectionString, out var dal);
    var result = await dal!.ExecuteScalarAsync(
        "SELECT 1 FROM sys.tables WHERE name = 'RayMigrationRecord'",
        dalSettings, cancellationToken: TestContext.Current.CancellationToken);

    result.Should().NotBeNull();
}
```

## Best Practices

### 1. Schema Changes Are New Steps

The repository is upgraded in place in numbered steps (ADR-024): a change to its schema or its lookup master data is a new `Repository_Upgrade_{n}.sql` in every DAL plus a bump of `RepositorySchema.CurrentVersion`, not a change to an existing step, and the next command (or a host through `IRepositorySchemaService.EnsureAsync`) applies the missing steps. `RepositorySchemaVersion` records the applied versions; `MigratorMeta.RayMigratorVersion` only lists the RayMigrator versions that used the repository. `Repository_CheckCreate` creates nothing but the schema and the version table. The log tables are not versioned, because a log database of its own is out of reach of a repository step: `DatabaseLogging_CheckCreate` creates the two `MigrationLog` indexes outside its creation branch, each only when it is missing, so that a log table an older engine created gets them at its next start, and writes its master data only when it creates the tables. Keep that shape: an addition to an existing log table must be idempotent and must not fail on a log account that lacks the right for it once the addition is there; do not add `ALTER TABLE` guards or master-data back-fill blocks. A timeout of `DatabaseLogging_CheckCreate` is not retried, because a retry would build an index from the start again (`DalBase.IsTimeout`): keep an addition that can take long, such as an index over a large log, within `DatabaseLogging.DbCommandTimeoutInSeconds`, or tell the operators to raise it for the first start. Bump the `Version` header of every template file whose SQL or whose description of its behaviour changes (file revision), also when no statement changes; a corrected example in the header alone, such as the casing of the example prefix, keeps it.

### 2. Use Idempotent Scripts

```sql
-- Safe to run multiple times
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(...))
BEGIN
    CREATE TABLE ...
END
```

### 3. Document Custom Changes

Follow the existing TOML header convention (see [Template TOML Headers](#template-toml-headers)) and add a `[ModificationNotes]` section:

```sql
/*
================================================================================
RayMigrator SQL Template (CUSTOMIZED)
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_1"
DatabaseType   = "SqlServer"
Author         = "Your Organization"
Version        = "2025-06-15.1"

[ModificationNotes]
Custom1 = "Added AuditCreatedBy, AuditModifiedBy columns to MigrationRecord table"
Custom2 = "Added index on ReleaseVersion, MigrationStatusId (MigrationRecord table)"
Custom3 = "Reason: Corporate audit requirements"
================================================================================
*/
```

### 4. Version Control Templates

Keep custom templates in version control alongside migration files.

### 5. Test Across Database Versions

Ensure custom SQL works with all supported database versions.

## Related Documentation

- [DAL Architecture](../03-database-layer/dal-architecture.md)
- [Template System](../03-database-layer/template-system.md)
- [Repository Schema](../03-database-layer/repository-schema.md)
- [Adding New Database](new-database-type.md)
