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
    ├── DatabaseLogging_Insert.sql
    ├── Repository_CheckCreate.sql
    ├── Repository_Drop.sql
    ├── Repository_Environment_CheckInsert.sql
    ├── Repository_MigrationRecord_FixOrphaned.sql
    ├── Repository_MigrationRecord_GetInterrupted.sql
    ├── Repository_MigrationRecord_Insert.sql
    ├── Repository_MigrationRecord_Select.sql
    ├── Repository_MigrationRecord_Update.sql
    ├── Repository_MigrationRecord_UpdateHash.sql
    ├── Repository_MigrationRecord_UpdateRollback.sql
    ├── Repository_MigrationRun_FixOrphaned.sql
    ├── Repository_MigrationRun_Insert.sql
    ├── Repository_MigrationRun_Select.sql
    ├── Repository_MigrationRun_SelectOrphaned.sql
    ├── Repository_MigrationRun_Update.sql
    └── Repository_Product_CheckInsert.sql
```

Each DAL (SqlServer, PostgreSQL, MariaDb, MySql, Sqlite) has the same 18 template files with database-specific SQL. Template file names correspond to `TemplateType` enum values. `TemplateCache` loads templates from `DataAccessLayers/{Type}/` on the filesystem at startup.

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
Version        = "2025-01-29.1"

[Description]
Function = """
Creates a new Migration record to track individual migration file execution.
Supports block-level tracking for recovery from interrupted migrations.
"""

Behaviour = """
- Return value >= 0: Success (MigrationId returned, logged at Debug level)
- Return value < 0: Error (logged at Error level, migration aborted)
- FileUpBlocksMigrated starts at 0, incremented as blocks execute
- StartedAt timestamp recorded immediately
"""

[ConfigPlaceholders]
# Replaced when loading the template (compile-time)
SchemaName    = "Database schema from Repository configuration (e.g., 'ray')"
TableBaseName = "Table name prefix from Repository configuration (e.g., '' or 'RM_')"

[Parameters]
# SQL parameters bound at runtime
ProductId           = "INT | REQUIRED | Product ID from Product table"
MigrationRunId      = "INT | REQUIRED | Parent MigrationRun ID"
MigrationRunModeId  = "TINYINT | REQUIRED | Run mode: 10=Validate, 20=Simulate, 100=Migrate"
MigrationOperationId= "TINYINT | REQUIRED | Operation: 5=Rollback, 50=MigrateDown, 100=MigrateUp"
# ... additional parameters

[ReturnValues]
# Format: SELECT 'code,message'
Success_Created = "N (MigrationId),Migration record with Id [N] successfully created for file [Filename]"

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
| `[ReturnValues]` | Documents the `'code,message'` SELECT format returned by the template |
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

All templates that use `ExecuteScalar` must return a result string in the format `'code,message'`:

- **ResultCode >= 0**: Success (e.g., the new record ID)
- **ResultCode < 0**: Error (migration aborted, see `TemplateResultCode.cs` for the catalog of known codes)
- **ResultMessage**: A human-readable description after the comma

The result is parsed by `TemplateExecutor.ExecuteScalarWithNegativeResultCodeException()` into a `TemplateResponse` object with `ResultCode` (int) and `ResultMessage` (string?).

Known result codes (from `TemplateResultCode` in `Shared/Constants/`):

**SQL Template ResultCodes (negative, returned by SQL templates):**

| Code | Constant | Meaning |
|------|----------|---------|
| `-1` | `GeneralError` | General/unclassified template error |
| `-2` | `MigrationAlreadyRunning` | Parallel run prevention |
| `-10` | `RepositoryIncomplete` | Wrong table count (MigratorMeta exists) |
| `-11` | `RepositoryPartialWithoutVersionTable` | Partial tables, no MigratorMeta |
| `-12` | `RepositoryMultipleVersionEntries` | Multiple MigratorMeta entries |
| `-20` | `ProductNameEmpty` | Product name is NULL or empty |
| `-30` | `MigrationRunNotFound` | MigrationRun with given Id does not exist |
| `-31` | `MigrationRunNotInRunningState` | MigrationRun not in Running state |
| `-40` | `MigrationNotFound` | Migration with given Id does not exist |
| `-50` | `EnvironmentNameEmpty` | Environment name is NULL or empty |

Unknown negative codes (e.g., from user-customized templates) throw `UndefinedTemplateResultException` instead of `TemplateResultException`.

**C# Backend ErrorCodes (positive, assigned by backend logic):**

| Code | Constant | Meaning |
|------|----------|---------|
| `1001` | `RequireRollbackFileValidationFailed` | Missing rollback files when RequireRollbackFile is enabled |
| `1002` | `MigrationFileParsingFailed` | Migration file parsing error |
| `1003` | `ConfigurationValidationFailed` | Configuration validation error |

## Template Examples

### Inline Historization in Update Templates

`Repository_MigrationRecord_Update.sql` and `Repository_MigrationRecord_UpdateRollback.sql` include an inline `INSERT INTO MigrationRecordHistory` that fires whenever a migration record transitions to a terminal state. The insert is gated on `MigrationStatusId IN (30, 50, 100)` (Failed, NotMigrated, Migrated). This means historization happens at the moment each record reaches its final status — no separate archive step is required.

When customizing these templates, preserve the inline `INSERT INTO MigrationRecordHistory` block and ensure the `HistorizedAt` column is populated with the current UTC timestamp.

### Schema Creation Template

The `Repository_CheckCreate.sql` template creates all 11 repository tables (MigratorMeta, Product, Environment, MigrationRun, MigrationRunMeta, MigrationRecord, MigrationRecordHistory, MigrationRunMode, MigrationOperation, MigrationRunResult, MigrationStatus) in a single template. Here is the MigrationRecord table excerpt (SQL Server):

```sql
-- Repository_CheckCreate.sql (excerpt: MigrationRecord table)
CREATE  TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] (
	Id                   int    IDENTITY(1,1)  NOT NULL,
	ProductId            int      NOT NULL,
	MigrationRunId       int      NOT NULL,
	MigrationRunModeId   tinyint      NOT NULL,
	MigrationOperationId tinyint      NOT NULL,
	MigrationStatusId    tinyint      NOT NULL,
	Environment          nvarchar(100)      NOT NULL,
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
	FileUpConfigJson     varchar(max)      NULL,
	MigrateDownFileExists bit      NOT NULL,
	FileDownHash         varchar(100)      NULL,
	FileDownConfigHash   varchar(100)      NULL,
	FileDownBlocksHash   varchar(100)      NULL,
	FileDownBlocksMigrated int      NULL,
	FileDownBlocksTotal  int      NULL,
	FileDownConfigJson   varchar(max)      NULL,
	StartedAt            datetime2      NULL,
	FinishedAt           datetime2      NULL,
	DurationInMs         bigint      NULL,
	CONSTRAINT pk_MigrationRecord PRIMARY KEY  ( Id )
 );
```

### DML Operation Template

```sql
-- Repository_MigrationRecord_Insert.sql (SQL Server, excerpt)
INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
(
    ProductId,
    MigrationRunId,
    MigrationRunModeId,
    MigrationOperationId,
    MigrationStatusId,
    Environment,
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
    + ISNULL(@Filename, 'NULL') + ']';
```

### Case-Insensitive Check-Insert Templates

`Repository_Product_CheckInsert.sql` and `Repository_Environment_CheckInsert.sql` (current version `2026-04-17.1`) follow a two-parameter contract:

- `@Name` -- the name in original casing (e.g., `MyApplication`, `Docker`)
- `@NameLower` -- the pre-computed lowercase form used for the lookup

The lookup must be performed against `NameLower` to remain case-insensitive. Both `Product` and `Environment` tables carry a `UNIQUE` index on `NameLower`. Here is the SQL Server Product template (excerpt):

```sql
-- Repository_Product_CheckInsert.sql (SQL Server, excerpt)
if (@Name IS NULL OR LEN(@Name) = 0)
    begin
        SELECT '-20,Product with empty name [' + ISNULL(@Name, 'NULL') + '] is not allowed!'
        return;
    end;

declare @ProductId int, @numberOfRows int;

select @ProductId = [Id] from [{CFG:SchemaName}].[{CFG:TableBaseName}Product] where [NameLower] = @NameLower;
SET @numberOfRows = @@rowcount;

if (@numberOfRows = 1)
    begin
        SELECT CAST(@ProductId AS varchar(10)) + ',Product [' + @Name + '] with Id [' + CAST(@ProductId AS varchar(10)) + '] found';
        return;
    end;

if (@numberOfRows = 0)
    begin
        INSERT INTO [{CFG:SchemaName}].[{CFG:TableBaseName}Product] (Name, NameLower, CreatedAt)
        VALUES (@Name, @NameLower, SYSUTCDATETIME());

        SET @ProductId = SCOPE_IDENTITY();
        SELECT CAST(@ProductId AS varchar(10)) + ',Product [' + @Name + '] with Id [' + CAST(@ProductId AS varchar(10)) + '] successfully created';
        return;
    end;
```

The C# caller (`TemplateExecutor.RepositoryProductCheckInsert` / `RepositoryEnvironmentCheckInsert`) pre-computes the lowercase form via `.ToLowerInvariant()` before binding the parameters.

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
public string Process(string template)
{
    if (template.Contains("CREATE TABLE"))
    {
        template = template.Replace(
            "CONSTRAINT [PK_",
            "[AuditCreatedBy] NVARCHAR(100) NULL,\n" +
            "[AuditModifiedBy] NVARCHAR(100) NULL,\n" +
            "CONSTRAINT [PK_");
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

`TemplateExecutor` is a concrete class (namespace `Raycoon.RayMigrator.Core`, source file in the Infrastructure project) that executes SQL templates via `IDal`. All public methods are **synchronous** (internally calling async DAL methods with `.GetAwaiter().GetResult()`). Repository options and the DAL instance are lazily initialized from `IMigrationContextAccessor.Current` on first use:

```csharp
public class TemplateExecutor
{
    public TemplateExecutor(TemplateCache templateCache, ILogger<TemplateExecutor> logger,
        IMigrationContextAccessor ctxAccessor) { ... }

    // Repository infrastructure
    public void RepositoryCheckCreate() { ... }
    public void RepositoryProductCheckInsert() { ... }
    public void RepositoryEnvironmentCheckInsert() { ... }

    // MigrationRun operations
    public void RepositoryMigrationRunInsert(string migrationRunSettingsJson) { ... }
    public void RepositoryMigrationRunUpdate(MigrationRunResult runResult) { ... }
    public List<Dictionary<string, object?>> RepositoryMigrationRunSelect(int limit) { ... }
    public List<Dictionary<string, object?>> RepositoryMigrationRunSelectOrphaned(int productId, string environment) { ... }
    public void RepositoryMigrationRunFixOrphaned(int migrationRunId) { ... }

    // Migration operations
    public int RepositoryMigrationInsert(int existingMigrationId, string filename, string releaseVersion,
        string targetGroupAlias, string targetAlias, int fileOrderId, string fileUpHash,
        string? fileUpConfigHash, string fileUpBlocksHash, int fileUpBlocksTotal,
        string? fileUpConfigJson, bool migrateDownFileExists) { ... }   // Returns MigrationId
    public List<MigrationRecord> RepositoryMigrationSelect(MigrationRunMode? overrideRunMode = null) { ... }
    public InterruptedMigrationInfo? RepositoryMigrationGetInterrupted() { ... }
    public int RepositoryMigrationFixOrphaned(int migrationRunId, MigrationStatus status) { ... }
    public void RepositoryMigrationUpdateHash(int migrationId, string fileUpHash,
        string? fileUpConfigHash, string fileUpBlocksHash) { ... }

    // Standard update (DAL manages connection lifecycle)
    public void RepositoryMigrationUpdate(int migrationId, MigrationStatus migrationStatus,
        int fileUpBlocksMigrated) { ... }
    public void RepositoryMigrationUpdateRollback(int migrationId, MigrationStatus migrationStatus,
        string fileDownHash, string? fileDownConfigHash, string fileDownBlocksHash,
        int fileDownBlocksMigrated, int fileDownBlocksTotal, string? fileDownConfigJson) { ... }

    // Shared-connection update overloads (atomic path: caller provides connection + transaction)
    public void RepositoryMigrationUpdate(int migrationId, MigrationStatus migrationStatus,
        int fileUpBlocksMigrated, DbConnection connection, DbTransaction transaction,
        int repoCommandTimeoutInSeconds) { ... }
    public void RepositoryMigrationUpdateRollback(int migrationId, MigrationStatus migrationStatus,
        string fileDownHash, string? fileDownConfigHash, string fileDownBlocksHash,
        int fileDownBlocksMigrated, int fileDownBlocksTotal, string? fileDownConfigJson,
        DbConnection connection, DbTransaction transaction, int repoCommandTimeoutInSeconds) { ... }

    // Core execution helpers
    // Standard overload: DAL manages connection
    public TemplateResponse ExecuteScalarWithNegativeResultCodeException(
        Template template, IDal dal, DalSettings dalSettings,
        DalParameterList? dalParameterList, ILogger? logger = null, EventId? eventId = null) { ... }
    // Shared-connection overload: caller provides connection + transaction
    public TemplateResponse ExecuteScalarWithNegativeResultCodeException(
        Template template, IDal dal, DbConnection connection, DbTransaction transaction,
        int commandTimeoutInSeconds, DalParameterList? dalParameterList,
        ILogger? logger = null, EventId? eventId = null) { ... }

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

    public TemplateCache(IOptions<RayMigratorOptions>? options, bool revealSensitiveData,
        ILogger<TemplateCache> logger, bool validateConfiguration = true) { ... }

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

The `validateConfiguration` constructor parameter (default `true`) controls whether Products/Repository configuration is validated against loaded templates at construction time. Set to `false` when Products/Repository config is not yet known at construction time.

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

The project uses xUnit and FluentAssertions for testing:

```csharp
[Fact]
public void CustomTemplate_CreatesRepositoryTables()
{
    // TemplateExecutor.RepositoryCheckCreate() creates all repository tables
    // using the Repository_CheckCreate.sql template
    _templateExecutor.RepositoryCheckCreate();

    // Verify table exists using the DAL
    // TryGetDal returns true with the DAL, or throws ConfigurationValidationException for unknown types
    DalFactory.TryGetDal("SqlServer", connectionString, out var dal);
    var result = dal!.ExecuteScalarAsync(
        "SELECT 1 FROM sys.tables WHERE name = 'RayMigrationRecord'",
        dalSettings).GetAwaiter().GetResult();

    result.Should().NotBeNull();
}
```

## Best Practices

### 1. Preserve Backwards Compatibility

```sql
-- Check if column exists before adding
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]')
    AND name = 'NewColumn'
)
BEGIN
    ALTER TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord]
    ADD [NewColumn] NVARCHAR(100) NULL;
END
```

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
TemplateType   = "Repository_CheckCreate"
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
