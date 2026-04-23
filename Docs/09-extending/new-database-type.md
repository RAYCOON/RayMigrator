# Adding a New Database Type

Step-by-step guide for implementing support for a new database system.

## Overview

RayMigrator uses a **plugin architecture** for database providers. Each database type is a separate project/assembly that requires:

1. A new project referencing `Database.Common` and `Shared`
2. A DAL class extending `DalBase` and decorated with `[DatabaseType]`
3. The 18 required SQL templates for repository and logging operations
4. No manual registration -- `DalFactory` auto-discovers implementations via dual-mode discovery (DependencyContext scanning for built-in DALs and filesystem scanning of `DataAccessLayers/` directory for external DAL plugins)

## Alternative: CLI Tool Execution Mode

Before building a full DAL, consider whether the **CLI tool execution mode** meets your needs. RayMigrator can execute migration SQL files via external CLI tools (e.g., `sqlcmd`, `psql`, `mysql`, `mariadb`, `sqlite3`) instead of the built-in DAL. This is configured via the `CliTools` array at the `RayMigrator` root level and referenced via `UseCliToolAlias` at the ProductDefaults, Product, TargetGroup, Target, or migration file level.

**When to use CLI tool execution:**
- Your database has a mature CLI tool but no .NET ADO.NET driver
- You need CLI-specific features (e.g., `:r` includes in sqlcmd, `\i` in psql)
- You want a quick integration path without implementing a full DAL

**Limitation:** CLI tool execution only replaces the migration file execution path. A DAL is still required for **repository operations** (migration tracking, logging). If no DAL exists for your database type, you must still implement at least the repository operations via a DAL plugin, or use a different database (e.g., SQLite) for the repository.

See [CLI Tools Configuration](../06-configuration-reference/cli-tools-options.md) for configuration details.

## Prerequisites

- Understanding of the target database's SQL dialect
- Familiarity with ADO.NET or the database's .NET provider
- Knowledge of the target database's DDL and DML syntax

## Existing DAL Implementations

Use these as reference when building a new DAL:

| Project | DatabaseType | ADO.NET Driver | Block Delimiter |
|---------|-------------|----------------|-----------------|
| `Database.SqlServer` | `SqlServer` | `Microsoft.Data.SqlClient` | `GO` |
| `Database.PostgreSQL` | `PostgreSQL` | `Npgsql` | `;` |
| `Database.MariaDb` | `MariaDb` | `MySqlConnector` | `;` |
| `Database.MySql` | `MySql` | `MySqlConnector` | `;` |
| `Database.Sqlite` | `Sqlite` | `Microsoft.Data.Sqlite` | `;` |

## Quick Start: Use the Example Project

The fastest way to start is to copy the `Raycoon.RayMigrator.Database.Example` skeleton project. It contains:

- `DalExample.cs` -- DAL class with all abstract methods stubbed as `NotImplementedException` and a commented-out `IsTransient` override template
- `Templates/` -- All 19 SQL template files with placeholder comments (18 required by the engine plus `Repository_MigrationRecordHistory_Archive.sql`)
- `.csproj` -- Pre-configured with template copying and DLL output targets

```bash
cp -r Raycoon.RayMigrator.Database.Example Raycoon.RayMigrator.Database.Oracle
```

Then rename files, update the `[DatabaseType]` attribute, update namespaces, add your ADO.NET driver NuGet package, and implement the TODO items.

## Step 1: Create the DAL Project

### Project Structure

Create a new project directory:

```
Raycoon.RayMigrator.Database.Oracle/
├── DalOracle.cs                                  # DAL class extending DalBase (includes IsTransient override)
├── Templates/
│   ├── DatabaseLogging_CheckCreate.sql
│   ├── DatabaseLogging_Insert.sql
│   ├── Repository_CheckCreate.sql
│   ├── Repository_Drop.sql
│   ├── Repository_Environment_CheckInsert.sql
│   ├── Repository_Migration_FixOrphaned.sql
│   ├── Repository_Migration_GetInterrupted.sql
│   ├── Repository_Migration_Insert.sql
│   ├── Repository_Migration_Select.sql
│   ├── Repository_Migration_Update.sql
│   ├── Repository_Migration_UpdateHash.sql
│   ├── Repository_Migration_UpdateRollback.sql
│   ├── Repository_MigrationRun_FixOrphaned.sql
│   ├── Repository_MigrationRun_Insert.sql
│   ├── Repository_MigrationRun_Select.sql
│   ├── Repository_MigrationRun_SelectOrphaned.sql
│   ├── Repository_MigrationRun_Update.sql
│   └── Repository_Product_CheckInsert.sql
└── Raycoon.RayMigrator.Database.Oracle.csproj
```

> Note: The `Database.Example` skeleton also includes a `Repository_MigrationRecordHistory_Archive.sql` placeholder beyond the 18 required templates. This file has no matching `TemplateType` enum value and is silently skipped by `TemplateCache` during initialization.

### Project File (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>default</LangVersion>
        <RayMigratorDatabaseType>Oracle</RayMigratorDatabaseType>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Raycoon.RayMigrator.Database.Common\..." />
        <ProjectReference Include="..\Raycoon.RayMigrator.Shared\..." />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Oracle.ManagedDataAccess.Core" Version="..." />
    </ItemGroup>

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

    <!-- Copy DAL DLL to DataAccessLayers/{Type}/ -->
    <Target Name="CopyDalToDataAccessLayers" AfterTargets="Build">
        <MakeDir Directories="$(OutputPath)DataAccessLayers\$(RayMigratorDatabaseType)" />
        <Copy SourceFiles="$(TargetPath)"
              DestinationFolder="$(OutputPath)DataAccessLayers\$(RayMigratorDatabaseType)\"
              SkipUnchangedFiles="true" />
    </Target>

</Project>
```

For external development (outside the monorepo), replace `ProjectReference` with NuGet `PackageReference`:

```xml
<ItemGroup>
    <PackageReference Include="Raycoon.RayMigrator.Database.Common" Version="0.9.41" />
    <PackageReference Include="Raycoon.RayMigrator.Shared" Version="0.9.41" />
</ItemGroup>
```

## Step 2: Implement the DAL Class

Extend `DalBase` and decorate with `[DatabaseType]`:

```csharp
using System.Reflection;
using Oracle.ManagedDataAccess.Client;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Database.Oracle;

[DatabaseType("Oracle")]
public class DalOracle : DalBase, IDal
{
    private readonly string _connectionString;
    public override string DatabaseType { get; }
    public override DalSpecificProperties DalSpecificProperties { get; }

    private static readonly string[] s_transientCodes = ["12170", "12541", "12543", "3113", "3135"];

    public DalOracle(string connectionString)
    {
        _connectionString = connectionString;
        DatabaseType = this.GetType().GetCustomAttribute<DatabaseTypeAttribute>()!.DatabaseType;
        DalSpecificProperties = new DalSpecificProperties
        {
            SqlBlockDelimiter = "/",   // Oracle uses "/" as block separator
            SqlMultiLineCommentStart = "/*",
            SqlMultiLineCommentEnd = "*/",
            SupportsSchema = true,              // Set to true if your database supports schemas
            SupportsTransactionalDdl = true,    // Set to false if DDL causes implicit COMMIT
            IdentifierQuoteStart = "\"",        // Identifier quoting characters
            IdentifierQuoteEnd = "\"",
            DefaultSchema = "",                 // Default schema (e.g., "dbo", "public")
        };
    }

    // Override IsTransient to detect transient errors from your database driver.
    // Check your ADO.NET driver's exception type and compare the error code
    // against known transient error codes for your database engine.
    // The base implementation already handles TimeoutException
    // and recursively checks InnerException.
    public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
    {
        var exceptionType = ex.GetType();
        if (exceptionType.FullName == "Oracle.ManagedDataAccess.Client.OracleException")
        {
            var numberProp = exceptionType.GetProperty("Number");
            if (numberProp?.GetValue(ex) is int number)
            {
                var code = number.ToString();
                return (s_transientCodes.Contains(code), code);
            }
        }
        return base.IsTransient(ex);
    }

    public override void CheckConnectionStringOrValidateConnection(bool validateConnection)
    {
        using var connection = new OracleConnection(_connectionString);
        if (validateConnection)
        {
            connection.Open();
            connection.Close();
        }
    }

    public override async Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings,
        DalParameterList? dalParameterList = null)
    {
        // Use the DalBase retry helpers which automatically route through your IsTransient override:
        await ExecuteWithRetryAsync(
            async () =>
            {
                // Open connection, build command, execute — your implementation here
                await using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();
                // ... build and execute command
            },
            dalSettings);
    }

    // Shared-connection methods: no retry, no connection management
    public override DbConnection CreateConnection() => new OracleConnection(_connectionString);

    public override async Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection,
        DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        // Execute command on the caller-provided connection/transaction — do NOT open, close, or retry
        // ... build command from (OracleConnection)connection, attach (OracleTransaction)transaction
    }

    public override async Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection,
        DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null)
    {
        // Execute scalar on the caller-provided connection/transaction — do NOT open, close, or retry
        // ... build command from (OracleConnection)connection, attach (OracleTransaction)transaction
        return null; // replace with actual result
    }

    // ... implement remaining abstract methods (ExecuteNonQuery, ExecuteScalarAsync(IDalSettings),
    //     ExecuteReaderAsync, IsConnectionValid)
}
```

### Abstract Methods to Implement

All of the following methods from `DalBase` must be implemented:

| Method | Description |
|--------|-------------|
| `CheckConnectionStringOrValidateConnection(bool)` | Parse and optionally validate the connection string |
| `ExecuteNonQueryAsync(string, IDalSettings, DalParameterList?)` | Async non-query execution (INSERT, UPDATE, DDL) |
| `ExecuteNonQuery(string, IDalSettings, DalParameterList?)` | Synchronous non-query execution |
| `ExecuteScalarAsync(string, IDalSettings, DalParameterList?)` | Async scalar query (returns single value) |
| `ExecuteReaderAsync(string, IDalSettings, DalParameterList?)` | Async reader query (returns `List<Dictionary<string, object?>>`) |
| `IsConnectionValid(string, IDalSettings)` | Async test whether a connection string is valid (returns `Task<bool>`) |
| `CreateConnection()` | Returns a new unopened `DbConnection` using the DAL's connection string; used for shared-connection (atomic) scenarios |
| `ExecuteNonQueryAsync(string, DbConnection, DbTransaction, int, DalParameterList?)` | Execute a non-query on a caller-provided connection and transaction; must not create connections, manage transactions, or add retry logic |
| `ExecuteScalarAsync(string, DbConnection, DbTransaction, int, DalParameterList?)` | Execute a scalar query on a caller-provided connection and transaction; must not create connections, manage transactions, or add retry logic |

The three managed-lifecycle methods (`ExecuteNonQueryAsync(IDalSettings)`, `ExecuteScalarAsync(IDalSettings)`, `ExecuteNonQuery`) support normal operation where the DAL controls connection and retry lifecycle. Each should:
1. Convert `DalParameterList` to database-specific parameters (using `TryGetDbSpecificSqlParameter<T>`)
2. Respect `dalSettings.UseTransaction` to wrap execution in a transaction when requested
3. Respect `dalSettings.DbCommandTimeoutInSeconds` for command timeout
4. Use `ExecuteWithRetryAsync` / `ExecuteWithRetry` (the protected `DalBase` helpers) to handle retries; they automatically route through your `IsTransient` override

The three shared-connection methods (`CreateConnection`, `ExecuteNonQueryAsync(DbConnection, ...)`, `ExecuteScalarAsync(DbConnection, ...)`) are used by the atomic shared-connection path when the repository and target database are on the same server. The caller opens the connection, begins the transaction, and passes both in. These methods must execute the command using only the provided connection and transaction.

You may also optionally override the following virtual methods from `DalBase`:
- `IsTransient(Exception ex)` -- detect transient errors specific to your database driver; the base implementation handles `TimeoutException` and `InnerException` recursion
- `CreateParameter<T>(DbType dbType, string parameterName, object? parameterValue)` -- for database-specific parameter adjustments (e.g., SQL Server sets `SqlParameter.Size` for strings)
- `ConvertToDbValue(object? value)` -- for database-specific value conversions (e.g., SQL Server clamps dates before 1753)

### Key Requirements

- **Class must be `public`**: `DalFactory` uses `Activator.CreateInstance` from a different assembly
- **Constructor must accept `string connectionString`**: This is how `DalFactory` creates instances
- **`[DatabaseType]` attribute is mandatory**: The string value becomes the `DatabaseType` in configuration

## Step 3: Create SQL Templates

All 18 templates must be present. `TemplateCache` validates their existence at startup. Templates are loaded from the filesystem (`DataAccessLayers/{Type}/`), delivered as `<Content>` items that propagate transitively through ProjectReference and as `contentFiles` in NuGet packages.

### Template Placeholders

| Placeholder | Description |
|-------------|-------------|
| `{CFG:SchemaName}` | Schema name from configuration |
| `{CFG:TableBaseName}` | Table prefix from configuration |
| `@ParameterName` | SQL parameter |

### Result Convention

Templates that return results must follow the `{code},{message}` pattern:

```sql
-- Success (code >= 0)
SELECT '1,Operation successful';

-- Error (code < 0)
SELECT '-1,Error description';
```

See `Database.SqlServer/Templates/` or `Database.PostgreSQL/Templates/` for reference implementations. All five built-in DALs (`SqlServer`, `PostgreSQL`, `MariaDb`, `MySql`, `Sqlite`) provide complete template sets of 18 files. Each template file in `Database.Example/Templates/` contains a TODO comment pointing to the reference implementations. Note that the extra `Repository_MigrationRecordHistory_Archive.sql` file in `Database.Example/Templates/` is not required by the engine and is silently skipped during loading.

> **Note on `Repository_Product_CheckInsert.sql` and `Repository_Environment_CheckInsert.sql`** (current version `2026-04-17.1`): both templates bind two SQL parameters — `@Name` (original casing) and `@NameLower` (pre-computed lowercase). Lookup must be performed by `NameLower` to remain case-insensitive; the `Product` and `Environment` tables each have a `UNIQUE` index on `NameLower`.

## Step 4: RetryHelper Integration

`DalBase` provides protected retry helpers (`ExecuteWithRetryAsync`, `ExecuteWithRetry`) that automatically route through the virtual `IsTransient` method you override. Override `IsTransient` in your DAL class to detect transient errors specific to your database driver. The base implementation handles `TimeoutException` and recursively checks `InnerException`.

```csharp
// In your DAL class: override IsTransient for database-specific error codes
private static readonly string[] s_transientCodes = ["12170", "12541", "12543", "3113", "3135"];

public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
{
    var exceptionType = ex.GetType();
    if (exceptionType.FullName == "Oracle.ManagedDataAccess.Client.OracleException")
    {
        var numberProp = exceptionType.GetProperty("Number");
        if (numberProp?.GetValue(ex) is int number)
        {
            var code = number.ToString();
            return (s_transientCodes.Contains(code), code);
        }
    }
    return base.IsTransient(ex);  // handles TimeoutException, InnerException recursion
}
```

Use the protected `DalBase` retry helpers in your method implementations:

```csharp
// Async (void)
public override async Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
{
    await ExecuteWithRetryAsync(
        async () => { await ExecuteNonQueryAsyncInternal(sqlCode, dalSettings, dalParameterList); },
        dalSettings);
}

// Async (with return value)
public override async Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
{
    return await ExecuteWithRetryAsync(
        async () => await ExecuteScalarAsyncInternal(sqlCode, dalSettings, dalParameterList),
        dalSettings);
}

// Sync
public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
{
    ExecuteWithRetry(
        () => { ExecuteNonQueryInternal(sqlCode, dalSettings, dalParameterList); },
        dalSettings);
}
```

When all retries are exhausted, `RetryHelper` (used internally by `DalBase`) throws a `RetryExhaustedException` (defined in `Database.Common`) with `AttemptsMade` and `LastErrorCode` (`string?`) properties.

## Step 5: Deployment

### Monorepo (project reference)

1. Add project to `RayMigrator.sln`
2. Add `ProjectReference` to `Console.csproj`:
   ```xml
   <ProjectReference Include="..\Raycoon.RayMigrator.Database.Oracle\Raycoon.RayMigrator.Database.Oracle.csproj" />
   ```
3. Add a Copy entry to the `CopyDalAssembliesToDataAccessLayers` target in `Console.csproj`:
   ```xml
   <Copy SourceFiles="$(OutputPath)Raycoon.RayMigrator.Database.Oracle.dll"
         DestinationFolder="$(OutputPath)DataAccessLayers/Oracle/"
         SkipUnchangedFiles="true"
         Condition="Exists('$(OutputPath)Raycoon.RayMigrator.Database.Oracle.dll')" />
   ```
   Also add a template copy entry to the `CopyDalTemplatesToDataAccessLayersPublish` target for the publish workflow (copies SQL templates from source into `$(PublishDir)DataAccessLayers/Oracle/`).

### External (plugin drop-in)

1. Build your project (the `.csproj` template already copies the DLL and templates to `DataAccessLayers/{Type}/` in the output)
2. Copy the `DataAccessLayers/Oracle/` directory from your build output into the RayMigrator installation's base directory:
   ```
   <RayMigrator installation>/
   └── DataAccessLayers/
       └── Oracle/
           ├── Raycoon.RayMigrator.Database.Oracle.dll
           └── *.sql (18 template files)
   ```
3. Copy any ADO.NET driver DLLs that are **not** already present in the RayMigrator root directory into the `DataAccessLayers/Oracle/` directory
4. **Do NOT copy** `Database.Common.dll` or `Shared.dll` -- they must come from the app's root directory

`DalFactory` uses dual-mode discovery at startup: first it uses `DependencyContext.Default` (from `deps.json`) to discover and load all `Raycoon.RayMigrator.*` assemblies, scanning each for `IDal` implementations with a `[DatabaseType]` attribute. Then it scans all `DataAccessLayers/` subdirectories for external DAL plugin DLLs and discovers classes via the same criteria. This works in all deployment modes including single-file publish.

## Step 6: Testing

### Unit Tests

```csharp
[Fact]
public void DatabaseType_ReturnsOracle()
{
    var dal = new DalOracle("Data Source=localhost/XE;User Id=test;Password=test;");
    dal.DatabaseType.Should().Be("Oracle");
}
```

### Integration Tests

Create a Docker container for testing and add tests to the integration test project.

## Checklist

- [ ] Create DAL project with `.csproj` referencing `Database.Common` and `Shared`
- [ ] Implement `public` class extending `DalBase` with `[DatabaseType("YourType")]` attribute
- [ ] Accept `string connectionString` as the sole constructor parameter
- [ ] Set `DalSpecificProperties` (block delimiter, comment markers, `SupportsSchema`, `SupportsTransactionalDdl`, identifier quoting, `DefaultSchema`)
- [ ] Implement all 9 abstract methods:
  - [ ] `CheckConnectionStringOrValidateConnection`
  - [ ] `ExecuteNonQueryAsync(string, IDalSettings, DalParameterList?)`
  - [ ] `ExecuteNonQuery`
  - [ ] `ExecuteScalarAsync(string, IDalSettings, DalParameterList?)`
  - [ ] `ExecuteReaderAsync`
  - [ ] `IsConnectionValid`
  - [ ] `CreateConnection`
  - [ ] `ExecuteNonQueryAsync(string, DbConnection, DbTransaction, int, DalParameterList?)`
  - [ ] `ExecuteScalarAsync(string, DbConnection, DbTransaction, int, DalParameterList?)`
- [ ] Override `IsTransient(Exception)` to detect your database's transient error codes
- [ ] Create all 18 SQL templates in `Templates/` directory
- [ ] Configure `.csproj` template copying and DLL output targets
- [ ] For monorepo: add `ProjectReference` and copy target to `Console.csproj`
- [ ] Verify `DalFactory` auto-discovers the new DAL at runtime
- [ ] Write unit tests
- [ ] Write integration tests with Docker
- [ ] Update documentation

## Related Documentation

- [DAL Architecture](../03-database-layer/dal-architecture.md) - Plugin architecture overview
- [External DAL Development](external-dal-development.md) - Complete guide for external developers
- [Template System](../03-database-layer/template-system.md) - How templates work
- [SQL Dialects](../03-database-layer/sql-dialects.md) - Dialect differences
