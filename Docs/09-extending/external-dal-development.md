# External DAL Development

Guide for developing custom RayMigrator database providers in an external repository.

## Overview

RayMigrator's plugin architecture allows developing database providers (DALs) outside the main repository. Your DAL is built as a standalone .NET class library that references `Database.Common` and `Shared` via NuGet packages.

The `Database.Example` project in the main repository serves as a skeleton template. It contains placeholder implementations for all required methods and 30 SQL template files, one for each template the engine requires. The recommended workflow is to copy (or fork) this project and replace "Example" with your database type name throughout.

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

An assembly whose types do not load is skipped: `DalFactory` catches the `ReflectionTypeLoadException` of the scan, or the `TypeLoadException` of one type, and keeps the loader exceptions per assembly. When a configured `DatabaseType` is then unknown, its `ConfigurationValidationException` (`Cannot create specific DataAccessLayer. Unknown DataAccessLayer for DatabaseType [YourDb].`) names those assemblies with their loader exceptions and says that a DataAccessLayer built against an earlier version of RayMigrator must be rebuilt against this version. A DAL built against 0.19.0 or earlier does not load in 0.20.0: it overrides members that `DalBase` no longer has and lacks those it has now. Before 0.20.0 such a failure was swallowed, and the message named only the unknown type.

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
    <!-- Version = RayMigratorVersion in Directory.Build.props of the release you build against (0.20.0 on develop; 0.19.0 is the latest published package) -->
    <PackageReference Include="Raycoon.RayMigrator.Database.Common" Version="0.19.0" />
    <PackageReference Include="Raycoon.RayMigrator.Shared" Version="0.19.0" />
    <PackageReference Include="YourDb.AdoNetDriver" Version="..." />
</ItemGroup>
```

The version is controlled centrally via the `RayMigratorVersion` property in `Directory.Build.props`; the packages you pack carry that value, so the `PackageReference` versions must match it (see `CHANGELOG.md` for the released versions). Build the DAL against the version of RayMigrator that loads it: an assembly built against an earlier version may not load (see [Discovery Mechanism](#discovery-mechanism)), and one built against 0.19.0 or earlier does not load in 0.20.0.

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
| `Task ExecuteNonQueryAsync(string, IDalSettings, DalParameterList?, CancellationToken)` | Execute SQL asynchronously (INSERT, UPDATE, DDL) |
| `void ExecuteNonQuery(string, IDalSettings, DalParameterList?)` | Execute SQL synchronously; no token, the database log writer calls it from its own thread |
| `Task<object?> ExecuteScalarAsync(string, IDalSettings, DalParameterList?, CancellationToken)` | Execute scalar query asynchronously |
| `Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string, IDalSettings, DalParameterList?, CancellationToken)` | Execute reader query asynchronously |
| `Task<bool> IsConnectionValidAsync(string, IDalSettings, CancellationToken)` | Test if a connection string is valid |
| `void CheckConnectionStringOrValidateConnection(bool)` | Parse or test the connection string |
| `bool TryGetDbTypeForType(Type, out DbType)` | Map .NET types to `DbType` |
| `bool TryGetDbSpecificSqlParameter<T>(DalParameterList, out List<T>?)` | Convert `DalParameterList` to database-specific parameters |
| `DbConnection CreateConnection()` | Create a new unopened connection using the DAL's connection string; used for shared-connection scenarios |
| `Task ExecuteNonQueryAsync(string, DbConnection, DbTransaction, int, DalParameterList?, CancellationToken)` | Execute a non-query on a caller-provided connection and transaction; no retry or connection lifecycle management |
| `Task<object?> ExecuteScalarAsync(string, DbConnection, DbTransaction, int, DalParameterList?, CancellationToken)` | Execute a scalar query on a caller-provided connection and transaction; no retry or connection lifecycle management |

Every `CancellationToken` parameter is the last one and defaults to `default`. A call that fails after its token was cancelled must end with an `OperationCanceledException` that carries that token and must start no further retry; `DalBase` guarantees both for you (see [DAL Architecture: Cancellation](../03-database-layer/dal-architecture.md#cancellation)).

**Note:** There is no synchronous `ExecuteScalar` method. Only `ExecuteScalarAsync` is defined. If you need synchronous scalar execution, use `.GetAwaiter().GetResult()`.

The three shared-connection members (`CreateConnection`, `ExecuteNonQueryAsync(DbConnection, ...)`, `ExecuteScalarAsync(DbConnection, ...)`) support the atomic shared-connection path used when the repository and target databases are the same instance. The caller opens the connection and owns the transaction; these methods must not create connections, manage transactions, or add retry logic.

### DalBase Abstract Class

Inherit from `DalBase` to get default implementations of:

- **The asynchronous `IDal` members** -- `DalBase` implements `ExecuteNonQueryAsync`, `ExecuteScalarAsync`, `ExecuteReaderAsync`, `IsConnectionValidAsync` and the two shared-connection members on top of the protected `…CoreAsync` members you override, and turns whatever a call throws after the cancellation into an `OperationCanceledException` with the caller's token. The provider's exception stays its inner exception, and unless the provider reported a cancellation itself, its message names the provider's message, so that a database error that met the cancel stays readable.
- **`IsTransient(Exception)`** -- virtual; base handles `TimeoutException` and `InnerException` recursion and never counts a cancellation as transient. Override to add your database's specific transient error codes.
- **`IsTimeout(Exception)`** -- virtual; base recognises a `TimeoutException` in the chain of inner exceptions, as Npgsql reports a timeout, and never counts a cancellation as a timeout. Override it when your driver reports a command timeout in another way, as the SQL Server DAL does for error -2 and the MySQL and MariaDB DALs do for `CommandTimeoutExpired`: the initialization of the database log does not retry a timeout of `DatabaseLogging_CheckCreate` (see [Overriding IsTimeout](#overriding-istimeout)).
- **`ExecuteWithRetryAsync` / `ExecuteWithRetry`** -- protected helpers; call `RetryHelper` with your `IsTransient` override automatically. Use these in your `ExecuteNonQueryCoreAsync`, `ExecuteScalarCoreAsync`, `ExecuteReaderCoreAsync` and `ExecuteNonQuery` implementations; pass the token to the asynchronous ones, which wait for a retry with it and start no attempt after the cancellation.
- **`TryGetDbTypeForType`** -- Maps all common .NET types (int, string, DateTime, Guid, byte[], etc.) to `System.Data.DbType`
- **`TryGetDbSpecificSqlParameter<T>`** -- Converts a `DalParameterList` into a list of database-specific `IDbDataParameter` instances
- **`CreateParameter<T>(DbType, string, object?)`** -- Creates a single typed parameter (override for database-specific behavior, e.g., SQL Server sets `SqlParameter.Size` for strings)
- **`ConvertToDbValue(object?)`** -- Converts null to `DBNull.Value` (override for database-specific conversions, e.g., SQL Server clamps dates before 1753)

You must implement the `abstract` members: `DatabaseType`, `DalSpecificProperties`, `CheckConnectionStringOrValidateConnection`, `ExecuteNonQueryCoreAsync`, `ExecuteNonQuery`, `ExecuteScalarCoreAsync`, `ExecuteReaderCoreAsync`, `IsConnectionValidCoreAsync`, `CreateConnection`, `ExecuteNonQueryCoreAsync(DbConnection, DbTransaction, ...)`, and `ExecuteScalarCoreAsync(DbConnection, DbTransaction, ...)`. Each `…CoreAsync` member receives the caller's token: pass it to opening the connection and to the command, not to commit and rollback, because a commit that has started must complete or the caller cannot tell whether it happened. A driver that cannot interrupt a statement observes the token before the batch starts, as the SQLite DAL does.

When your templates take a lock that belongs to the session rather than to a transaction (`GET_LOCK()` on MySQL and MariaDB), release it after a call on a connection of its own that failed, timed out or was cancelled, before the connection goes back to the pool: a template that stopped between taking and releasing the lock would leave it on the pooled session, where it holds back every other host until the pool reuses or prunes the connection. The MySQL and MariaDB DALs run `DO RELEASE_ALL_LOCKS()` for that, best effort and without the token; the shared-connection members leave it to the caller, whose connection it is.

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

    // Standard managed-lifecycle methods (implement with retry wrappers via DalBase helpers);
    // DalBase exposes them as the IDal members ExecuteNonQueryAsync, ExecuteScalarAsync, ExecuteReaderAsync, IsConnectionValidAsync
    protected override async Task ExecuteNonQueryCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken) { ... }
    public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) { ... }
    protected override async Task<object?> ExecuteScalarCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken) { ... }
    protected override async Task<List<Dictionary<string, object?>>> ExecuteReaderCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken) { ... }
    protected override async Task<bool> IsConnectionValidCoreAsync(string connectionString, IDalSettings dalSettings, CancellationToken cancellationToken) { ... }
    public override void CheckConnectionStringOrValidateConnection(bool validateConnection) { ... }

    // Shared-connection methods (no retry, no connection management — caller controls lifecycle)
    public override DbConnection CreateConnection() => new YourDbConnection(_connectionString);
    protected override async Task ExecuteNonQueryCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken) { ... }
    protected override async Task<object?> ExecuteScalarCoreAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList, CancellationToken cancellationToken) { ... }
}
```

## Template Contract

All 30 template files must be present, one per `TemplateType` member except `Undefined`. `TemplateCache` loads templates from `DataAccessLayers/{Type}/` on the filesystem. Templates are delivered as `<Content>` items that propagate transitively through ProjectReference and as `contentFiles` in NuGet packages. `TemplateCache` validates completeness at startup and throws a `ConfigurationValidationException` listing any missing templates; because the constructor checks every DAL folder under `DataAccessLayers/`, a folder that lacks a template stops every command of the host, whatever database it uses. Template files must not be empty -- if a template is not needed for your database type, add a SQL comment explaining why.

> **Breaking (0.20.0):** an external DAL ships `Repository_Upgrade_3.sql` (schema version 3) and the four retention templates `Repository_MigrationRecordHistory_Delete.sql`, `Repository_MigrationRecordHistory_Count.sql`, `DatabaseLogging_Delete.sql` and `DatabaseLogging_Count.sql`, and implements the protected `…CoreAsync` members with the token instead of the former public asynchronous members, so it must be rebuilt against 0.20.0: an assembly built against an earlier version does not load, and `DalFactory` names it with its loader exceptions in the message for an unknown database type (see [Discovery Mechanism](#discovery-mechanism)). `DatabaseLogging_Count.sql` takes `@ExistsOnly` (see the table below). It should also give `MigrationLog` its two indexes in `DatabaseLogging_CheckCreate.sql` and return `StartedAt` and `FinishedAt` from both record selects; without them it keeps working, with slower log queries and null dates. See `CHANGELOG.md` (0.20.0).

The `Database.Example` project includes all 30 files as placeholders with TODO comments; the earlier extra placeholder `Repository_MigrationRecordHistory_Archive.sql`, which had no `TemplateType`, is gone (the update templates write the history, the retention templates delete it). Use `Database.SqlServer` or `Database.PostgreSQL` templates as reference implementations.

### Required Templates

| Template | Purpose |
|----------|---------|
| `DatabaseLogging_CheckCreate.sql` | Create logging table if not exists, and the indexes `ix_{CFG:TableBaseName}MigrationLog_MigrationRunId` and `ix_{CFG:TableBaseName}MigrationLog_CreatedAt` outside the creation branch when they are missing; return `1` when created, `0` otherwise (also when only the indexes were added). Look an index up before you create it, so that a start that finds both runs no DDL, and serialise a parallel start (see [Logging Schema: Indexes](../03-database-layer/logging-schema.md#indexes)) |
| `DatabaseLogging_Insert.sql` | Insert log entry |
| `DatabaseLogging_Delete.sql` | Delete one batch of at most `@BatchSize` log rows created before `@CutoffUtc`, the oldest first, sparing the rows of open runs; runs against the repository database with the schema and prefix of `Repository`; return `'n,message'` with the rows deleted |
| `DatabaseLogging_Count.sql` | Count the rows `DatabaseLogging_Delete.sql` would take: `'n,1,message'`, or `'0,0,message'` when the log table is not in the repository database. With `@ExistsOnly` = 1 count nothing and return `'0,1,message'` when the log table exists (`'0,0,message'` when not): the cleanup asks that before it deletes, so that a large log is not read whole; with `0` count, as the dry run does |
| `Repository_CheckCreate.sql` | Create the `RepositorySchemaVersion` table if missing and return `'schemaVersion,unversionedTables,message'` (ADR-024); creates nothing else |
| `Repository_Upgrade_1.sql` | Schema step 1: create all repository tables, indexes, foreign keys and lookup master data and write version row 1; must be idempotent and return `'1,message'`. Every key, foreign key and index name carries `{CFG:TableBaseName}` after its `pk_`, `fk_`, `ix_` or `uix_` (see [Template Placeholders](#template-placeholders)) |
| `Repository_Upgrade_2.sql` | Schema step 2: add the `MigrationRunSource` lookup and the nullable run detail columns of `MigrationRun`, write version row 2; must be idempotent and return `'2,message'` |
| `Repository_Upgrade_3.sql` | Schema step 3: add the row `Cancelled` (70) to `MigrationRunResult` and the indexes `ix_{CFG:TableBaseName}MigrationRun_Product_Environment_StartedAt`, `ix_{CFG:TableBaseName}MigrationRecordHistory_MigrationRunId`, `ix_{CFG:TableBaseName}MigrationRecordHistory_HistorizedAt` and `ix_{CFG:TableBaseName}MigrationRecord_Product_Environment`, write version row 3; must be idempotent and return `'3,message'`. Every later schema version is a further `Repository_Upgrade_{n}.sql` |
| `Repository_MigratorMeta_CheckInsert.sql` | Register the RayMigrator version in `MigratorMeta` if missing, return the `MigratorMetaId` (`-12` when several rows match) |
| `Repository_Drop.sql` | Drop all repository tables and schema |
| `Repository_Environment_CheckInsert.sql` | Insert environment if not exists, return EnvironmentId |
| `Repository_MigrationRecord_FixOrphaned.sql` | Settle the open records of an orphaned run: with `@MigrationStatusId = 0` decide per record from `FileUpBlocksMigrated`/`FileUpBlocksTotal` and the `FileDown*` columns as the shipped templates do (keep the counters), with 30, 50 or 100 set that status on every open record; set `FinishedAt` where it is NULL, leave `DurationInMs` |
| `Repository_MigrationRecord_GetInterrupted.sql` | Get interrupted migrations |
| `Repository_MigrationRecordHistory_Select.sql` | Select the terminal state transitions (MigrationRecordHistory rows) of a product/environment for the `info` run history; the same columns as `Repository_MigrationRecord_Select.sql`, `StartedAt` and `FinishedAt` included |
| `Repository_MigrationRecordHistory_Delete.sql` | Delete one batch of at most `@BatchSize` history rows historized before `@CutoffUtc`, the oldest first, sparing the rows of open runs; return `'n,message'` with the rows deleted |
| `Repository_MigrationRecordHistory_Count.sql` | Count the rows `Repository_MigrationRecordHistory_Delete.sql` would take: `'n,message'` |
| `Repository_MigrationRecord_Insert.sql` | Insert migration record |
| `Repository_MigrationRecord_Select.sql` | Select migrations for a product, `StartedAt` and `FinishedAt` included (`TargetGroupStatus.LastMigrationDate`); a select without the two columns keeps working, its records map with null dates |
| `Repository_MigrationRecord_Update.sql` | Update migration status and hash (includes inline MigrationRecordHistory insert) |
| `Repository_MigrationRecord_UpdateHash.sql` | Update migration hash only |
| `Repository_MigrationRecord_UpdateRollback.sql` | Update after rollback (includes inline MigrationRecordHistory insert) |
| `Repository_MigrationRun_FixOrphaned.sql` | Fix orphaned run records |
| `Repository_MigrationRun_Heartbeat.sql` | Refresh `LastHeartbeatAt` of a running MigrationRun while it is open; return `1` when updated, `0` when the run has finished |
| `Repository_MigrationRun_Insert.sql` | Insert migration run record |
| `Repository_MigrationRun_Select.sql` | Select the newest runs of a product and environment (`@ProductId`, `@EnvironmentId`, `@Limit`) |
| `Repository_MigrationRun_SelectOrphaned.sql` | Select orphaned runs |
| `Repository_MigrationRun_Update.sql` | Update run result |
| `Repository_Product_CheckInsert.sql` | Lookup product by `NameLower`, insert if not exists; returns `ProductId` |
| `Repository_Product_Select.sql` | Lookup product by `NameLower` WITHOUT inserting; returns `ProductId` or `0` when not registered (used by Simulate mode, #7) |
| `Repository_Environment_Select.sql` | Lookup environment by `NameLower` WITHOUT inserting; returns `EnvironmentId` or `0` when not registered (used by Simulate mode, #7) |

> **Note on `Repository_Product_CheckInsert.sql` signature**: the template binds two SQL parameters — `@Name` (original casing) and `@NameLower` (pre-computed lowercase). Lookup must be performed by `NameLower` to remain case-insensitive. See `Database.SqlServer/Templates/Repository_Product_CheckInsert.sql` and `Database.MariaDb/Templates/Repository_Product_CheckInsert.sql` for reference implementations.

### Template Placeholders

- `{CFG:SchemaName}` -- Schema name from configuration
- `{CFG:TableBaseName}` -- Table name prefix from configuration
- `@ParameterName` -- SQL parameters (bound via `DalParameterList`)

Placeholders are replaced at runtime by `TemplateCache.GetTemplate<T>()` using reflection-based property matching. Any unreplaced `{CFG:...}` placeholder after substitution causes a `ConfigurationValidationException`.

The table prefix `{CFG:TableBaseName}` belongs in every constraint and index name of the repository and log tables, not only in the table names: `pk_{CFG:TableBaseName}MigrationRun`, `fk_{CFG:TableBaseName}MigrationRun_Product`, `ix_{CFG:TableBaseName}MigrationRecordHistory`, `uix_{CFG:TableBaseName}Product_NameLower`, `ix_{CFG:TableBaseName}MigrationLog_CreatedAt` (in your engine's casing), and a guard that checks for a constraint or an index looks for the prefixed name. Two repositories with different prefixes can then share one database or schema. The prefix has at most 16 characters (RULE_4_4); make sure the longest name fits your engine's identifier limit.

### Result Convention

Templates returning results must use `{code},{message}` format:
- Positive codes = success (e.g., `1,Product registered`)
- Negative codes = error (e.g., `-1,Error registering product`)
- Count templates return the row count as their first code, which the engine reads as a `long`

## RetryHelper Integration

`DalBase` provides protected retry helpers (`ExecuteWithRetryAsync`, `ExecuteWithRetry`) that automatically route through the virtual `IsTransient` method. Override `IsTransient` in your DAL class to detect transient errors specific to your database driver. The base implementation already handles `TimeoutException`, recursively checks `InnerException` and never counts a cancellation as transient. Override `DefaultTransientErrorCodes` with your built-in list and classify the driver's code through `IsTransientCode`; `DalFactory` then loads an optional `DataAccessLayers/YourDb/TransientErrorCodes.txt` that replaces the list at runtime (ADR-022, see [Transient Error Codes](../03-database-layer/transient-error-codes.md)).

### Overriding IsTransient

```csharp
// In your DAL class
private static readonly string[] TransientCodes = ["1205", "1213", "2006"]; // your DB's built-in transient codes

protected override IReadOnlyCollection<string> DefaultTransientErrorCodes => TransientCodes;

public override (bool isTransient, string? errorCode) IsTransient(Exception ex)
{
    // Use reflection to check exception type — avoids hard NuGet dependency on the driver
    var exceptionType = ex.GetType();
    if (exceptionType.FullName == "YourDb.YourDbException")
    {
        var numberProp = exceptionType.GetProperty("Number");
        if (numberProp?.GetValue(ex) is int number)
        {
            return IsTransientCode(number.ToString(CultureInfo.InvariantCulture));
        }
    }
    return base.IsTransient(ex); // handles TimeoutException, InnerException recursion
}
```

### Overriding IsTimeout

`IsTimeout` tells a command that ran into its command timeout from other failures. The initialization of the database log retries a transient failure of `DatabaseLogging_CheckCreate` but not a timeout, because the attempt was rolled back whole and a retry would build the indexes of the log table from the start again (see [Logging Schema: Indexes](../03-database-layer/logging-schema.md#indexes)). The base recognises a `TimeoutException` in the chain of inner exceptions; override it when your driver reports a timeout in another way:

```csharp
public override bool IsTimeout(Exception ex)
{
    // Use reflection to check exception type — avoids hard NuGet dependency on the driver
    var exceptionType = ex.GetType();
    if (exceptionType.FullName == "YourDb.YourDbException"
        && exceptionType.GetProperty("IsCommandTimeout")?.GetValue(ex) is true)
    {
        return true;
    }
    return base.IsTimeout(ex); // handles TimeoutException in the chain; a cancellation is never a timeout
}
```

### Using the DalBase Retry Helpers

```csharp
protected override async Task ExecuteNonQueryCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
{
    await ExecuteWithRetryAsync(
        async () => { await ExecuteNonQueryAsyncInternal(sqlCode, dalSettings, dalParameterList, cancellationToken); },
        dalSettings, cancellationToken: cancellationToken);
}

protected override async Task<object?> ExecuteScalarCoreAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList, CancellationToken cancellationToken)
{
    return await ExecuteWithRetryAsync(
        async () => await ExecuteScalarAsyncInternal(sqlCode, dalSettings, dalParameterList, cancellationToken),
        dalSettings, cancellationToken: cancellationToken);
}

public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null)
{
    ExecuteWithRetry(
        () => { ExecuteNonQueryInternal(sqlCode, dalSettings, dalParameterList); },
        dalSettings);
}
```

The `DalBase` helpers internally call `RetryHelper.ExecuteWithRetryAsync` / `RetryHelper.ExecuteWithRetry` (defined in `Database.Common`) passing your `IsTransient` override as the predicate; `RetryHelper.ExecuteWithRetryAsync` takes the token, waits for a retry with it and neither classifies nor retries a failure after the cancellation. The `RetryHelper` public signatures accept:

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

> **Note**: The defaults above are the `DalSettings` class property initializers. When the engine creates `DalSettings` for a migration target, it populates `MaxRetries` and `RetryDelayMs` from `TargetDefaults` in configuration (effective defaults: `MaxRetries=0`, `RetryDelayMs=250`). The class defaults apply only when code creates a `DalSettings` instance without setting these properties explicitly. The retention of `IRepositoryMaintenanceService` calls your DAL with `MaxRetries = 0`, and the initialization of the database log calls it with `MaxRetries = 0` and retries itself; through the `DalBase` helpers your DAL then makes one attempt.

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
├── Repository_Upgrade_1.sql
├── Repository_Upgrade_2.sql
├── Repository_Upgrade_3.sql
├── Repository_MigratorMeta_CheckInsert.sql
├── ... (30 .sql files total)
├── TransientErrorCodes.txt   (optional: replaces the built-in transient error codes, ADR-022)
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
3. `TemplateCache` validates that all 30 templates are present for each discovered DAL
4. `ValidateConfigurationAgainstTemplateCache` verifies that configured `DatabaseType` values (in Repository and TargetGroups) match available DALs
5. `MigrationContextFactory` logs the source of the transient error codes per DatabaseType: Debug for the built-in list, Information when a `TransientErrorCodes.txt` was loaded
6. Use your database type in configuration:

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

When the database type stays unknown although `DataAccessLayers/YourDb/` is deployed, read the message for the unknown type: it names the assemblies whose types did not load with their loader exceptions (see [Discovery Mechanism](#discovery-mechanism)); a DAL built against an earlier version of RayMigrator must be rebuilt.

## Related Documentation

- [Adding New Database Type](new-database-type.md) - Step-by-step implementation guide
- [DAL Architecture](../03-database-layer/dal-architecture.md) - Plugin architecture overview
- [Template System](../03-database-layer/template-system.md) - How templates work
- [Naming Conventions per Engine](../03-database-layer/sql-dialects.md#naming-conventions-per-engine) - Policy for identifier casing in repository tables and the guidance for third-party DAL plugin authors
