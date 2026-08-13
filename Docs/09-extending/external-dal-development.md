# External DAL Development

Guide for developing custom RayMigrator database providers in an external repository.

## Overview

RayMigrator's plugin architecture allows developing database providers (DALs) outside the main repository. Your DAL is built as a standalone .NET class library that references `Database.Common` and `Shared` via NuGet packages.

The `Database.Example` project in the main repository serves as a skeleton template. It contains placeholder implementations for all required methods and 19 SQL template files (the 18 required by the engine plus `Repository_MigrationRecordHistory_Archive.sql`, which is an extra placeholder for archive operations). The recommended workflow is to copy (or fork) this project and replace "Example" with your database type name throughout.

> **License note**: The `Raycoon.RayMigrator.Database.Example` directory is licensed under the **MIT License** (see `Raycoon.RayMigrator.Database.Example/LICENSE.md`), separately from the rest of RayMigrator (which is BUSL-1.1 with Additional Use Grant). You may freely copy the Example skeleton as a starting point for your own DAL plugin. The plugin source code you write from there is your own, under whatever license you choose. **Running** your plugin inside a RayMigrator process is a use of the Licensed Work and is governed by `LICENSE.md` — for this version that use is free of charge, whatever the size or nature of your organization.

## Alternative: CLI Tool Execution Mode

Before building a full external DAL, consider whether the **CLI tool execution mode** meets your needs. RayMigrator can execute migration SQL files via external CLI tools (e.g., `sqlcmd`, `psql`, `mysql`) instead of the built-in DAL. This is configured via the `CliTools` array at the `RayMigrator` root level and activated via `UseCliToolAlias` at the ProductDefaults, Product, TargetGroup, Target, or migration file level (TOML header or migsettings).

CLI tool execution only replaces the migration file execution path. A DAL is still required for repository and logging operations (migration tracking). If no DAL exists for your database type, you still need to implement a DAL plugin for repository operations, or use a different database (e.g., SQLite) as the repository.

See [CLI Tools Configuration](../06-configuration-reference/cli-tools-options.md) for configuration details.

## Discovery Mechanism

`DalFactory` uses dual-mode plugin discovery at startup:

**Mode 0: DependencyContext discovery** (built-in DALs, single-file publish compatible):
1. Reads `DependencyContext.Default` (from `deps.json`) to discover all runtime libraries whose name starts with `Raycoon.RayMigrator.`
2. Loads each assembly via `Assembly.Load` and scans for classes that implement `IDal`, are non-abstract, and carry a `[DatabaseType("...")]` attribute
3. Works in all deployment modes including single-file publish bundles

**Mode 1: Filesystem-based discovery** (external DAL plugins):
1. Scans `DataAccessLayers/` subdirectories relative to `AppDomain.CurrentDomain.BaseDirectory`
2. Loads all `.dll` files in each subdirectory via `Assembly.LoadFrom`
3. Finds classes that implement `IDal`, are non-abstract, and carry a `[DatabaseType("...")]` attribute

Both modes instantiate DAL classes via `Activator.CreateInstance(dalType, connectionString)` -- this requires a **public** class with a **public constructor accepting a single `string` parameter** (the connection string).

If a DAL type is found via both DependencyContext and filesystem scanning, the first registration wins (`TryAdd` semantics). Duplicate `[DatabaseType]` values are silently ignored (first wins).

## Project Setup

### 1. Create the Project

Use `Database.Example` as your starting point:

```bash
# Copy the skeleton project
cp -r Raycoon.RayMigrator.Database.Example Raycoon.RayMigrator.Database.YourDb

# Or start from scratch
dotnet new classlib -n Raycoon.RayMigrator.Database.YourDb
cd Raycoon.RayMigrator.Database.YourDb
```

### 2. Add NuGet References

`Database.Common` and `Shared` are configured as NuGet-packable projects (`GeneratePackageOnBuild=false`). Generate the packages explicitly before referencing them:

```bash
dotnet pack Raycoon.RayMigrator.Database.Common -c Release
dotnet pack Raycoon.RayMigrator.Shared -c Release
```

Then reference them in your external project:

```xml
<ItemGroup>
    <PackageReference Include="Raycoon.RayMigrator.Database.Common" Version="0.10.3" />
    <PackageReference Include="Raycoon.RayMigrator.Shared" Version="0.10.3" />
    <PackageReference Include="YourDb.AdoNetDriver" Version="..." />
</ItemGroup>
```

The version is controlled centrally via the `RayMigratorVersion` property in `Directory.Build.props` (currently `0.10.3`).

### 3. Multi-Target Frameworks

Match the target frameworks supported by your RayMigrator installation:

```xml
<PropertyGroup>
    <TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>
</PropertyGroup>
```

### 4. Configure Build Output

The `.csproj` must define the database type name, copy SQL templates to the correct output path, and place the DAL DLL into the `DataAccessLayers/{Type}/` directory. This matches the layout that `Database.Example` already provides:

```xml
<PropertyGroup>
    <RayMigratorDatabaseType>YourDb</RayMigratorDatabaseType>
</PropertyGroup>

<!-- Templates as Content (transitive propagation + NuGet contentFiles) -->
<ItemGroup>
    <Content Include="Templates\**\*.sql">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <Link>DataAccessLayers\$(RayMigratorDatabaseType)\%(RecursiveDir)%(Filename)%(Extension)</Link>
        <Pack>true</Pack>
        <PackagePath>contentFiles\any\any\DataAccessLayers\$(RayMigratorDatabaseType)\</PackagePath>
        <PackageCopyToOutput>true</PackageCopyToOutput>
    </Content>
</ItemGroup>

<!-- Copy DLL to DataAccessLayers/{Type}/ -->
<Target Name="CopyDalToDataAccessLayers" AfterTargets="Build">
    <MakeDir Directories="$(OutputPath)DataAccessLayers\$(RayMigratorDatabaseType)" />
    <Copy SourceFiles="$(TargetPath)"
          DestinationFolder="$(OutputPath)DataAccessLayers\$(RayMigratorDatabaseType)\"
          SkipUnchangedFiles="true" />
</Target>
```

## IDal Interface and DalBase

### IDal Interface

Your DAL class must implement `IDal` (defined in `Database.Common`). All methods:

| Method | Description |
|--------|-------------|
| `string DatabaseType { get; }` | Returns the database type name (from `[DatabaseType]` attribute) |
| `DalSpecificProperties DalSpecificProperties { get; }` | SQL dialect properties (block delimiter, comment markers) |
| `Task ExecuteNonQueryAsync(string, IDalSettings, DalParameterList?)` | Execute SQL asynchronously (INSERT, UPDATE, DDL) |
| `void ExecuteNonQuery(string, IDalSettings, DalParameterList?)` | Execute SQL synchronously |
| `Task<object?> ExecuteScalarAsync(string, IDalSettings, DalParameterList?)` | Execute scalar query asynchronously |
| `Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string, IDalSettings, DalParameterList?)` | Execute reader query asynchronously |
| `Task<bool> IsConnectionValid(string, IDalSettings)` | Test if a connection string is valid |
| `void CheckConnectionStringOrValidateConnection(bool)` | Parse or test the connection string |
| `bool TryGetDbTypeForType(Type, out DbType)` | Map .NET types to `DbType` |
| `bool TryGetDbSpecificSqlParameter<T>(DalParameterList, out List<T>?)` | Convert `DalParameterList` to database-specific parameters |
| `DbConnection CreateConnection()` | Create a new unopened connection using the DAL's connection string; used for shared-connection scenarios |
| `Task ExecuteNonQueryAsync(string, DbConnection, DbTransaction, int, DalParameterList?)` | Execute a non-query on a caller-provided connection and transaction; no retry or connection lifecycle management |
| `Task<object?> ExecuteScalarAsync(string, DbConnection, DbTransaction, int, DalParameterList?)` | Execute a scalar query on a caller-provided connection and transaction; no retry or connection lifecycle management |

**Note:** There is no synchronous `ExecuteScalar` method. Only `ExecuteScalarAsync` is defined. If you need synchronous scalar execution, use `.GetAwaiter().GetResult()`.

The three shared-connection members (`CreateConnection`, `ExecuteNonQueryAsync(DbConnection, ...)`, `ExecuteScalarAsync(DbConnection, ...)`) support the atomic shared-connection path used when the repository and target databases are the same instance. The caller opens the connection and owns the transaction; these methods must not create connections, manage transactions, or add retry logic.

### DalBase Abstract Class

Inherit from `DalBase` to get default implementations of:

- **`IsTransient(Exception)`** -- virtual; base handles `TimeoutException` and `InnerException` recursion. Override to add your database's specific transient error codes.
- **`ExecuteWithRetryAsync` / `ExecuteWithRetry`** -- protected helpers; call `RetryHelper` with your `IsTransient` override automatically. Use these in your `ExecuteNonQueryAsync`, `ExecuteScalarAsync`, and `ExecuteNonQuery` implementations.
- **`TryGetDbTypeForType`** -- Maps all common .NET types (int, string, DateTime, Guid, byte[], etc.) to `System.Data.DbType`
- **`TryGetDbSpecificSqlParameter<T>`** -- Converts a `DalParameterList` into a list of database-specific `IDbDataParameter` instances
- **`CreateParameter<T>(DbType, string, object?)`** -- Creates a single typed parameter (override for database-specific behavior, e.g., SQL Server sets `SqlParameter.Size` for strings)
- **`ConvertToDbValue(object?)`** -- Converts null to `DBNull.Value` (override for database-specific conversions, e.g., SQL Server clamps dates before 1753)

You must implement the `abstract` members: `DatabaseType`, `DalSpecificProperties`, `CheckConnectionStringOrValidateConnection`, `ExecuteNonQueryAsync`, `ExecuteNonQuery`, `ExecuteScalarAsync`, `ExecuteReaderAsync`, `IsConnectionValid`, `CreateConnection`, `ExecuteNonQueryAsync(DbConnection, DbTransaction, ...)`, and `ExecuteScalarAsync(DbConnection, DbTransaction, ...)`.

### DalSpecificProperties

Set your database's SQL dialect properties in the constructor:

```csharp
DalSpecificProperties = new DalSpecificProperties
{
    // "GO" for SQL Server, ";" for PostgreSQL/MariaDB/MySQL/SQLite
    SqlBlockDelimiter = ";",
    SqlMultiLineCommentStart = "/*",
    SqlMultiLineCommentEnd = "*/",
    // Set to true if your database supports schemas (e.g., SQL Server, PostgreSQL).
    // When true, SchemaName is required in Repository/DatabaseLogging configuration.
    // When false (default), SchemaName is optional and ignored if provided.
    SupportsSchema = false,
    // Set to false if DDL causes implicit COMMIT (e.g., MariaDB/MySQL).
    // When false, RayMigrator logs a safety warning for DDL in transaction-enabled migrations.
    SupportsTransactionalDdl = true,
    // Identifier quoting characters: "["/"]" for SQL Server, "\"/"\" for PostgreSQL/SQLite, "`"/"`" for MariaDB/MySQL
    IdentifierQuoteStart = "\"",
    IdentifierQuoteEnd = "\"",
    // Default schema name: "dbo" for SQL Server, "public" for PostgreSQL, empty for others
    DefaultSchema = "",
    // Set to true if unquoted identifiers are folded to lowercase (PostgreSQL)
    FoldsUnquotedIdentifiersToLower = false,
};
```

### DatabaseType Attribute

The `[DatabaseType("YourDb")]` attribute is the runtime lookup key. `DalFactory` uses it to map configuration values to DAL classes. The value must match what users specify in `"DatabaseType": "YourDb"` in their configuration. The DAL class reads it in its constructor via reflection:

```csharp
DatabaseType = this.GetType().GetCustomAttribute<DatabaseTypeAttribute>()!.DatabaseType;
```

### Class Visibility

DAL classes **must be `public`** (not `internal`). `DalFactory` instantiates them via `Activator.CreateInstance` across assembly boundaries, which requires public visibility.

### Constructor Signature

The constructor must accept exactly one `string` parameter (the connection string):

```csharp
[DatabaseType("YourDb")]
public class DalYourDb : DalBase, IDal
{
    private readonly string _connectionString;

    public DalYourDb(string connectionString)
    {
        _connectionString = connectionString;
        DatabaseType = this.GetType().GetCustomAttribute<DatabaseTypeAttribute>()!.DatabaseType;
        DalSpecificProperties = new DalSpecificProperties { ... };
    }

    // Standard managed-lifecycle methods (implement with retry wrappers via DalBase helpers)
    public override async Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) { ... }
    public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) { ... }
    public override async Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) { ... }
    public override async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) { ... }
    public override async Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings) { ... }
    public override void CheckConnectionStringOrValidateConnection(bool validateConnection) { ... }

    // Shared-connection methods (no retry, no connection management — caller controls lifecycle)
    public override DbConnection CreateConnection() => new YourDbConnection(_connectionString);
    public override async Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null) { ... }
    public override async Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null) { ... }
}
```

## Template Contract

All 18 template files must be present. `TemplateCache` loads templates from `DataAccessLayers/{Type}/` on the filesystem. Templates are delivered as `<Content>` items that propagate transitively through ProjectReference and as `contentFiles` in NuGet packages. `TemplateCache` validates completeness at startup and throws a `ConfigurationValidationException` listing any missing templates. Template files must not be empty -- if a template is not needed for your database type, add a SQL comment explaining why.

The `Database.Example` project includes all 19 files as placeholders with TODO comments (18 required templates plus `Repository_MigrationRecordHistory_Archive.sql`). `TemplateCache` recognizes only the 18 files that correspond to `TemplateType` enum values; `Repository_MigrationRecordHistory_Archive.sql` is silently skipped during loading. Use `Database.SqlServer` or `Database.PostgreSQL` templates as reference implementations.

### Required Templates

| Template | Purpose |
|----------|---------|
| `DatabaseLogging_CheckCreate.sql` | Create logging table if not exists |
| `DatabaseLogging_Insert.sql` | Insert log entry |
| `Repository_CheckCreate.sql` | Create all repository tables |
| `Repository_Drop.sql` | Drop all repository tables and schema |
| `Repository_Environment_CheckInsert.sql` | Insert environment if not exists, return EnvironmentId |
| `Repository_MigrationRecord_FixOrphaned.sql` | Fix orphaned migration records |
| `Repository_MigrationRecord_GetInterrupted.sql` | Get interrupted migrations |
| `Repository_MigrationRecord_Insert.sql` | Insert migration record |
| `Repository_MigrationRecord_Select.sql` | Select migrations for a product |
| `Repository_MigrationRecord_Update.sql` | Update migration status and hash (includes inline MigrationRecordHistory insert) |
| `Repository_MigrationRecord_UpdateHash.sql` | Update migration hash only |
| `Repository_MigrationRecord_UpdateRollback.sql` | Update after rollback (includes inline MigrationRecordHistory insert) |
| `Repository_MigrationRun_FixOrphaned.sql` | Fix orphaned run records |
| `Repository_MigrationRun_Insert.sql` | Insert migration run record |
| `Repository_MigrationRun_Select.sql` | Select current run for product |
| `Repository_MigrationRun_SelectOrphaned.sql` | Select orphaned runs |
| `Repository_MigrationRun_Update.sql` | Update run result |
| `Repository_Product_CheckInsert.sql` | Lookup product by `NameLower`, insert if not exists; returns `ProductId` |

> **Note on `Repository_Product_CheckInsert.sql` signature**: the template binds two SQL parameters — `@Name` (original casing) and `@NameLower` (pre-computed lowercase). Lookup must be performed by `NameLower` to remain case-insensitive. See `Database.SqlServer/Templates/Repository_Product_CheckInsert.sql` and `Database.MariaDb/Templates/Repository_Product_CheckInsert.sql` for reference implementations.

### Template Placeholders

- `{CFG:SchemaName}` -- Schema name from configuration
- `{CFG:TableBaseName}` -- Table name prefix from configuration
- `@ParameterName` -- SQL parameters (bound via `DalParameterList`)

Placeholders are replaced at runtime by `TemplateCache.GetTemplate<T>()` using reflection-based property matching. Any unreplaced `{CFG:...}` placeholder after substitution causes a `ConfigurationValidationException`.

### Result Convention

Templates returning results must use `{code},{message}` format:
- Positive codes = success (e.g., `1,Product registered`)
- Negative codes = error (e.g., `-1,Error registering product`)

## RetryHelper Integration

`DalBase` provides protected retry helpers (`ExecuteWithRetryAsync`, `ExecuteWithRetry`) that automatically route through the virtual `IsTransient` method. Override `IsTransient` in your DAL class to detect transient errors specific to your database driver. The base implementation already handles `TimeoutException` and recursively checks `InnerException`.

### Overriding IsTransient

```csharp
// In your DAL class
private static readonly string[] s_transientCodes = ["1205", "1213", "2006"]; // your DB's transient codes

public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
{
    // Use reflection to check exception type — avoids hard NuGet dependency on the driver
    var exceptionType = ex.GetType();
    if (exceptionType.FullName == "YourDb.YourDbException")
    {
        var numberProp = exceptionType.GetProperty("Number");
        if (numberProp?.GetValue(ex) is int number)
        {
            var code = number.ToString();
            return (s_transientCodes.Contains(code), code);
        }
    }
    return base.IsTransient(ex); // handles TimeoutException, InnerException recursion
}
```

### Using the DalBase Retry Helpers

```csharp
public override async Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
{
    await ExecuteWithRetryAsync(
        async () => { await ExecuteNonQueryAsyncInternal(sqlCode, dalSettings, dalParameterList); },
        dalSettings);
}

public override async Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
{
    return await ExecuteWithRetryAsync(
        async () => await ExecuteScalarAsyncInternal(sqlCode, dalSettings, dalParameterList),
        dalSettings);
}

public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
{
    ExecuteWithRetry(
        () => { ExecuteNonQueryInternal(sqlCode, dalSettings, dalParameterList); },
        dalSettings);
}
```

The `DalBase` helpers internally call `RetryHelper.ExecuteWithRetryAsync` / `RetryHelper.ExecuteWithRetry` (defined in `Database.Common`) passing your `IsTransient` override as the predicate. The `RetryHelper` public signatures accept:

```csharp
Func<Exception, (bool isTransient, string? errorCode)> isTransientPredicate
```

When all retries are exhausted, `RetryHelper` throws a `RetryExhaustedException` (defined in `Database.Common`) with `AttemptsMade` (`int`) and `LastErrorCode` (`string?`) properties.

### IDalSettings Properties

The `IDalSettings` interface provides retry configuration per operation:

| Property | Default | Description |
|----------|---------|-------------|
| `UseTransaction` | -- | Whether to wrap execution in a transaction |
| `DbCommandTimeoutInSeconds` | -- | SQL command timeout |
| `MaxRetries` | 3 | Maximum retry attempts (0 = disabled) |
| `RetryDelayMs` | 500 | Base delay in ms (linear backoff: `delay * attempt`) |

> **Note**: The defaults above are the `DalSettings` class property initializers. When the engine creates `DalSettings` for a migration target, it populates `MaxRetries` and `RetryDelayMs` from `TargetDefaults` in configuration (effective defaults: `MaxRetries=0`, `RetryDelayMs=250`). The class defaults apply only when code creates a `DalSettings` instance without setting these properties explicitly.

## Build & Deployment

### Build

```bash
dotnet build -c Release
```

The post-build target in the `.csproj` automatically places the DLL and templates into `bin/Release/{tfm}/DataAccessLayers/YourDb/`.

### Deploy to RayMigrator

Copy the entire `DataAccessLayers/YourDb/` folder from your build output into the RayMigrator installation directory:

```bash
# From your build output (adjust tfm to match your RayMigrator installation):
cp -r bin/Release/net10.0/DataAccessLayers/YourDb/ \
   /path/to/raymigrator/DataAccessLayers/YourDb/
```

Result:
```
DataAccessLayers/YourDb/
├── Raycoon.RayMigrator.Database.YourDb.dll
├── DatabaseLogging_CheckCreate.sql
├── DatabaseLogging_Insert.sql
├── Repository_CheckCreate.sql
├── ... (18 .sql files total)
```

For built-in DALs, the Console project's `CopyDalAssembliesToDataAccessLayers` post-build target handles this automatically. External DALs must be deployed manually.

### Dependency Rules

- **DO NOT** copy `Raycoon.RayMigrator.Database.Common.dll` or `Raycoon.RayMigrator.Shared.dll` into your `DataAccessLayers/YourDb/` folder. These are loaded from the application's root directory.
- Copy any ADO.NET driver DLLs that are **not** already present in the RayMigrator root directory into the `DataAccessLayers/YourDb/` directory.
- `Assembly.LoadFrom` resolves dependencies from both the DLL's directory and the application base directory.

## Verification

After deployment, verify your DAL is discovered:

1. Start RayMigrator with logging at Debug level
2. `TemplateCache` logs each discovered DAL: `DataAccessLayer [YourDb] found`
3. `TemplateCache` validates that all 18 templates are present for each discovered DAL
4. `ValidateConfigurationAgainstTemplateCache` verifies that configured `DatabaseType` values (in Repository and TargetGroups) match available DALs
5. Use your database type in configuration:

```json
{
  "RayMigrator": {
    "Products": [{
      "TargetGroups": [{
        "DatabaseType": "YourDb",
        "Targets": [{
          "ConnectionString": "..."
        }]
      }]
    }]
  }
}
```

## Related Documentation

- [Adding New Database Type](new-database-type.md) - Step-by-step implementation guide
- [DAL Architecture](../03-database-layer/dal-architecture.md) - Plugin architecture overview
- [Template System](../03-database-layer/template-system.md) - How templates work
- [Naming Conventions per Engine](../03-database-layer/sql-dialects.md#naming-conventions-per-engine) - Policy for identifier casing in repository tables and the guidance for third-party DAL plugin authors
