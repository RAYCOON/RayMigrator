# Architectural Patterns

RayMigrator implements several established patterns to achieve its design goals.

## Options Pattern

**Purpose**: Strongly-typed configuration with validation and change notification.

**Implementation**:

```csharp
// Options class (Core/Configuration/Options/RayMigratorOptions.cs)
public class RayMigratorOptions
{
    [ValidateObjectMembers]
    public RepositoryOptions? Repository { get; set; }
    [ValidateObjectMembers]
    public DatabaseLoggingOptions? DatabaseLogging { get; set; }
    public SerilogOptions? Serilog { get; set; }
    [Required]
    [ValidateObjectMembers]
    public ProductDefaultOptions? ProductDefaults { get; set; }
    [Required]
    [ValidateEnumeratedItems]
    public List<ProductOptions>? Products { get; set; }
    [ValidateEnumeratedItems]
    public List<CliToolOptions>? CliTools { get; set; }
}

// Registration with the data annotations (every failed element named by alias) and validation at start
services.AddOptions<RayMigratorOptions>()
    .Configure(options => rayMigratorConfigurationSection.Bind(options))
    .ValidateOnStart();
services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorDataAnnotationValidateOptions>();

// PostConfigure merges ProductDefaults into Products/TargetGroups.
// The static MergeDefaults method can also be called directly
// by a host that builds RayMigratorOptions in memory.
// Copies: MigrationErrorAction, RollbackErrorAction, encoding, extensions,
//         RequireRollbackFile, StopRollbackOnMissingRollbackFile, UseCliToolAlias → Product level
//         TargetMigrationOrder, HashValidationScope, StopRollbackOnMissingRollbackFile, UseCliToolAlias → TargetGroup level
//         DbCommand* settings, UseCliToolAlias → Target level
services.AddTransient<IPostConfigureOptions<RayMigratorOptions>,
    ProductDefaultsPostConfigureOptions>();

// IValidateOptions implementation that delegates to the shared rule catalog
// (Raycoon.RayMigrator.Validation.RuleCatalog). The section as written lets RULE_7_3
// inspect connection strings before {ENV:...} placeholders are resolved.
services.AddSingleton<IValidateOptions<RayMigratorOptions>>(
    new RayMigratorOptionsValidator(sourceResult.RayMigratorConfigSectionAsWritten));

// PostConfigure delegates to a static method for reuse outside the DI pipeline
public class ProductDefaultsPostConfigureOptions : IPostConfigureOptions<RayMigratorOptions>
{
    public void PostConfigure(string? name, RayMigratorOptions options)
    {
        MergeDefaults(options);
    }

    public static void MergeDefaults(RayMigratorOptions options) { /* ... */ }
}
```

**Hierarchy**:
```
RayMigratorOptions
├── Repository (RepositoryOptions)
├── DatabaseLogging (DatabaseLoggingOptions)
├── Serilog (SerilogOptions)
├── ProductDefaults (ProductDefaultOptions)
│   └── TargetGroupDefaults (TargetGroupDefaultOptions)
│       └── TargetDefaults (TargetDefaultsOptions)
├── Products[] (ProductOptions)
│   └── TargetGroups[] (TargetGroupOptions)
│       └── Targets[] (TargetOptions)
└── CliTools[] (CliToolOptions)
```

## Repository Pattern

**Purpose**: Abstract data access for migration tracking.

**Implementation**:

The repository database is separate from target databases. SQL templates handle CRUD operations:

```
Repository_CheckCreate.sql              - Report the schema version, create RepositorySchemaVersion if missing
Repository_Upgrade_1.sql                - Schema step 1: create the repository tables with master data (ADR-024)
Repository_Upgrade_2.sql                - Schema step 2: run details on MigrationRun, MigrationRunSource lookup
Repository_MigratorMeta_CheckInsert.sql - Register the engine version, return MigratorMetaId
Repository_Drop.sql                     - Drop repository schema
Repository_Product_CheckInsert.sql      - Ensure product exists (Name + NameLower)
Repository_Environment_CheckInsert.sql  - Ensure environment exists (Name + NameLower)
Repository_Product_Select.sql           - Look up product id read-only (Simulate mode, no insert)
Repository_Environment_Select.sql       - Look up environment id read-only (Simulate mode, no insert)
Repository_MigrationRun_Insert.sql      - Record migration run
Repository_MigrationRun_Update.sql      - Update run result/status
Repository_MigrationRun_Select.sql      - Query migration runs (history)
Repository_MigrationRun_SelectOrphaned.sql - Find orphaned runs (Fix command)
Repository_MigrationRun_FixOrphaned.sql - Mark orphaned runs as Error
Repository_MigrationRun_Heartbeat.sql   - Refresh LastHeartbeatAt of a running run
Repository_MigrationRecord_Insert.sql         - Create migration record with block tracking
Repository_MigrationRecord_Update.sql         - Update migration block progress/status
Repository_MigrationRecord_UpdateRollback.sql - Update rollback fields (FileDown*)
Repository_MigrationRecord_UpdateHash.sql     - Update hash fields (update-hash command)
Repository_MigrationRecord_Select.sql         - Query migrations
Repository_MigrationRecord_FixOrphaned.sql    - Fix orphaned migration entries
Repository_MigrationRecord_GetInterrupted.sql - Find interrupted migrations for recovery
Repository_MigrationRecordHistory_Select.sql - Query terminal state transitions (info run history)
DatabaseLogging_CheckCreate.sql         - Create database logging table
DatabaseLogging_Insert.sql              - Insert log entry
```

**Tables**:
- `RepositorySchemaVersion` - Applied schema versions of the repository (ADR-024)
- `MigratorMeta` - RayMigrator versions that used the repository
- `Product` - Registered products (`Name` + `NameLower` with unique index)
- `Environment` - Registered environments (`Name` + `NameLower` with unique index)
- `MigrationRun` - Migration run sessions
- `MigrationRunMeta` - Run metadata (JSON settings)
- `MigrationRecord` - Individual migrations with block-level tracking
- `MigrationRecordHistory` - Migration audit trail (archived records)
- `MigrationRunMode` - Lookup: run modes (Migrate, Simulate, ...)
- `MigrationOperation` - Lookup: operation types (MigrateUp, MigrateDown, ...)
- `MigrationRunResult` - Lookup: run results (Running=10, PartialSuccess=50, Recovered=80, Error=90, Ok=100)
- `MigrationRunSource` - Lookup: where a run was started (Cli=10, Ci=20, Studio=30; schema version 2)
- `MigrationStatus` - Lookup: migration status values

## Template Pattern

**Purpose**: Database-specific SQL execution with placeholder substitution.

**Implementation**:

All `TemplateExecutor` methods are **synchronous** (internally calling async DAL methods with `.GetAwaiter().GetResult()`). Placeholder resolution happens in two stages:

```csharp
// Infrastructure/TemplateExecutor.cs (namespace: Raycoon.RayMigrator.Core)
public class TemplateExecutor
{
    private readonly TemplateCache _templateCache;
    private readonly ILogger<TemplateExecutor> _logger;
    private readonly IMigrationContextAccessor _ctxAccessor;
    private RepositoryOptions? _repositoryBacking;
    private IDal? _repositoryDalBacking;

    // Context access is deferred to first use (not constructor time) to support
    // scenarios where MigrationContext is set after DI resolution.
    public TemplateExecutor(TemplateCache templateCache, ILogger<TemplateExecutor> logger,
        IMigrationContextAccessor ctxAccessor) { /* ... */ }

    // Lazy initialization on first access
    private RepositoryOptions _repository { get => /* InitializeFromContext() if needed */ }
    private IDal _repositoryDal { get => /* InitializeFromContext() if needed */ }

    public void RepositoryProductCheckInsert()
    {
        // 1. Get template from cache ({ENV:*} already replaced during cache init)
        var template = _templateCache.GetRepositoryTemplate(TemplateType.Repository_Product_CheckInsert, _repository);
        //    → {CFG:*} placeholders replaced here via reflection-based property matching

        // 2. Build parameter list
        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", productName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", productName.ToLowerInvariant(), typeof(string)));

        // 3. Execute via DAL (returns "ResultCode,ResultMessage" string)
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        // 4. Use result (TemplateResponse has ResultCode + ResultMessage)
        _ctxAccessor.Current.MigrationState.ProductId = templateResponse.ResultCode;
    }

    // The schema steps (Repository_CheckCreate, Repository_Upgrade_{n}, Repository_MigratorMeta_CheckInsert)
    // run in RepositorySchemaExecutor; RepositoryCheckCreate() hands it the values of the current context
}
```

The `TemplateCache` (in `Raycoon.RayMigrator.Core.Templates`) loads and caches all SQL templates once per process. It takes no options: the host validates each configuration it runs against the cache explicitly, so one cache serves many configurations:

```csharp
public class TemplateCache
{
    public TemplateCache(bool revealSensitiveData, ILogger<TemplateCache> logger) { /* ... */ }

    // Called by the host for every configuration it runs (the CLI pipeline once, after the options are bound)
    public void ValidateConfigurationAgainstTemplateCache(RayMigratorOptions options) { /* ... */ }

    // Returns all loaded DAL types (e.g. "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite")
    public List<string> GetAvailableDatabaseTypes() { /* ... */ }
}
```

**Placeholder Types**:

| Syntax | Source | Example |
|--------|--------|---------|
| `{CFG:SchemaName}` | Options | `{CFG:SchemaName}` → `migrations` |
| `{ENV:DB_PASSWORD}` | Environment | `{ENV:DB_PASSWORD}` → `secret123` |
| `@ProductAlias` | Parameters | SQL parameterized query |

**Template Structure**:
```sql
/*
================================================================================
RayMigrator SQL Template
================================================================================
[RayMigratorTemplate]
TemplateType   = "Repository_Upgrade_1"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-10-14.1"
*/

CREATE TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] (
    Id INT IDENTITY(1,1) NOT NULL,
    ProductId INT NOT NULL,
    -- ...
    CONSTRAINT pk_{CFG:TableBaseName}MigrationRecord PRIMARY KEY ( Id )
);
```

## Context Pattern

**Purpose**: Carry state through the execution pipeline.

**Implementation**:

```csharp
public class MigrationContext
{
    // Configuration
    public RayMigratorOptions RayMigratorOptions { get; set; }
    public RayMigratorConsoleOptions RayMigratorConsoleOptions { get; set; }
    public string RayMigratorVersion { get; set; }

    // Product shortcut (initialized in constructor, init-only)
    public IEnumerable<TargetGroupOptions>? ProductTargetGroupOptionsEnumerable { get; init; }

    // Runtime state (mutable)
    public MigrationState MigrationState { get; set; }
    public ConcurrentDictionary<string, DalSpecificProperties> DalSpecificPropertiesDictionary { get; set; }

    // Deep copy (creates new MigrationContext with deep-copied MigrationState)
    public MigrationContext Clone { get; }
}
```

### Context Accessor Pattern

The `MigrationContext` is not injected directly. Instead, services access it through `IMigrationContextAccessor`, which supports different hosting modes:

```csharp
public interface IMigrationContextAccessor
{
    MigrationContext Current { get; set; }
}

// CLI: Wraps a single MigrationContext instance
public class SingletonMigrationContextAccessor : IMigrationContextAccessor
{
    public MigrationContext Current { get; set; } = null!;
}

// Per-request isolation via AsyncLocal<T> (analogous to IHttpContextAccessor)
public class AsyncLocalMigrationContextAccessor : IMigrationContextAccessor
{
    private static readonly AsyncLocal<MigrationContext?> _current = new();
    public MigrationContext Current
    {
        get => _current.Value ?? throw new InvalidOperationException(
            "No MigrationContext available for the current execution context. " +
            "Ensure the context is set before accessing it.");
        set => _current.Value = value;
    }
}
```

DI registration is handled centrally via `AddRayMigratorServices` (in `Services/ServiceCollectionExtensions.cs`), which selects the appropriate accessor based on host mode:

```csharp
// Services/ServiceCollectionExtensions.cs
public static IServiceCollection AddRayMigratorServices(this IServiceCollection services,
    RayMigratorHostMode hostMode = RayMigratorHostMode.Cli)
{
    services.AddScoped<IMigrationService, MigrationService>();
    services.AddScoped<ICliToolExecutor, CliToolExecutor>();

    if (hostMode == RayMigratorHostMode.Cli)
        services.AddSingleton<IMigrationContextAccessor, SingletonMigrationContextAccessor>();
    else
        services.AddSingleton<IMigrationContextAccessor, AsyncLocalMigrationContextAccessor>();   // static AsyncLocal isolates each flow

    services.AddSingleton<IMigrationContextFactory, MigrationContextFactory>();
    services.AddSingleton<IRepositorySchemaService, RepositorySchemaService>();   // needs the host's TemplateCache
    return services;
}
```

A factory creates `MigrationContext` instances (CLI creates one at startup):

```csharp
public interface IMigrationContextFactory
{
    // Options a host built in memory: prepared (placeholders, defaults, data annotations, rule catalog), then activated
    MigrationContext Create(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions);

    // Options the CLI pipeline already bound and validated
    MigrationContext CreateFromValidatedOptions(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions);
}
```

**State Flow**:
```mermaid
flowchart LR
    A[Create Context] --> B[Initialize Options]
    B --> C[Set Initial State]
    C --> D[Update State: Discovery]
    D --> E[Update State: Validation]
    E --> F[Update State: Execution]
    F --> G[Update State: Complete/Error]
```

**Usage**:
```csharp
public class MigrationService
{
    private readonly IMigrationContextAccessor _ctxAccessor;

    public async Task<MigrationOperationResult> MigrateUpAsync(...)
    {
        // Check current product
        var product = _ctxAccessor.Current.RayMigratorOptions.Products
            .FirstOrDefault(p => p.Alias == request.ProductAlias);

        // Update state
        _ctxAccessor.Current.MigrationState.MigrationOperation = MigrationOperation.MigrateUp;
        _ctxAccessor.Current.MigrationState.ProductId = productId;
    }
}
```

## Plugin Pattern (DAL Discovery)

**Purpose**: Discover and load database-specific Data Access Layer implementations at runtime.

**Implementation**:

Each DAL is a separate assembly deployed into a `DataAccessLayers/{DatabaseType}/` subdirectory. `DalFactory` uses dual-mode discovery in its static constructor:

1. **DependencyContext-based**: Scans `DependencyContext.Default` (from deps.json) for runtime libraries whose names start with `Raycoon.RayMigrator.`, loads them via `Assembly.Load`, and discovers classes implementing `IDal` with a `[DatabaseType]` attribute. Works with single-file publish.
2. **Filesystem-based**: Scans `DataAccessLayers/` subdirectories at startup, loads DLLs via `Assembly.LoadFrom`, and discovers classes implementing `IDal` with a `[DatabaseType]` attribute (for external DAL plugins).

```
DataAccessLayers/
├── SqlServer/
│   ├── Raycoon.RayMigrator.Database.SqlServer.dll
│   ├── Repository_CheckCreate.sql
│   └── ... (25 templates per DAL, flat layout)
├── PostgreSQL/
│   ├── Raycoon.RayMigrator.Database.PostgreSQL.dll
│   └── *.sql (25 template files)
├── MariaDb/
│   ├── Raycoon.RayMigrator.Database.MariaDb.dll
│   └── *.sql (25 template files)
├── MySql/
│   ├── Raycoon.RayMigrator.Database.MySql.dll
│   └── *.sql (25 template files)
└── Sqlite/
    ├── Raycoon.RayMigrator.Database.Sqlite.dll
    └── *.sql (25 template files)
```

```csharp
// Attribute for auto-discovery (Database.Common/DatabaseTypeAttribute.cs)
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class DatabaseTypeAttribute : Attribute
{
    public string DatabaseType { get; }
    public DatabaseTypeAttribute(string databaseType) { DatabaseType = databaseType; }
}

// DAL implementation (each in its own assembly)
[DatabaseType("SqlServer")]
public class DalSqlServer : DalBase, IDal
{
    public DalSqlServer(string connectionString) { /* ... */ }
}
```

This allows external developers to create their own DAL plugins without modifying the core codebase. See `Database.Example` for a skeleton template.

## Factory Pattern

**Purpose**: Create database-specific implementations based on configuration.

**Implementation**:

RayMigrator uses a static `DalFactory` with reflection-based auto-discovery (not DI-registered):

```csharp
// IDal interface (Database.Common/IDal.cs)
public interface IDal
{
    string DatabaseType { get; }
    DalSpecificProperties DalSpecificProperties { get; }
    Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings);
    void CheckConnectionStringOrValidateConnection(bool validateConnection);
    bool TryGetDbTypeForType(Type type, out DbType dbType);
    bool TryGetDbSpecificSqlParameter<T>(DalParameterList dalParameterList, out List<T>? sqlParameterList) where T : class, IDbDataParameter, new();
    // Shared-connection methods for atomic operations (repository + target in one transaction):
    DbConnection CreateConnection();
    Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null);
    Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null);
}

// Factory (Database/DalFactory.cs) - static, not DI-registered
public static class DalFactory
{
    private static readonly Dictionary<string, Type> DalTypeMapping = new();
    private static readonly ConcurrentDictionary<string, IDal> DalInstances = new();
    static DalFactory()
    {
        // Mode 0: Discover built-in DAL assemblies from DependencyContext (deps.json).
        // Works with single-file publish. No hardcoded assembly list.
        var context = DependencyContext.Default;
        if (context != null)
            foreach (var lib in context.RuntimeLibraries)
                if (lib.Name.StartsWith("Raycoon.RayMigrator."))
                {
                    var assembly = Assembly.Load(new AssemblyName(lib.Name));
                    ScanAssemblyForDals(assembly);
                }

        // Mode 1: Filesystem-based discovery (external DAL plugins)
        string dalRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataAccessLayers");
        if (Directory.Exists(dalRootPath))
            foreach (string subDir in Directory.GetDirectories(dalRootPath))
                foreach (string dllFile in Directory.GetFiles(subDir, "*.dll"))
                {
                    var assembly = Assembly.LoadFrom(dllFile);
                    ScanAssemblyForDals(assembly);
                }
    }

    // Resolution: creates or retrieves cached instance by "{databaseType}_{connectionString}" key
    public static bool TryGetDal(string databaseType, string connectionString, out IDal? dalInstance)
    {
        if (DalTypeMapping.TryGetValue(databaseType, out Type? dalType))
        {
            dalInstance = DalInstances.GetOrAdd($"{databaseType}_{connectionString}",
                _ => (IDal)Activator.CreateInstance(dalType, connectionString)!);
            return true;
        }
        throw new ConfigurationValidationException($"Cannot create specific DataAccessLayer. Unknown DataAccessLayer for DatabaseType [{databaseType}].");
    }
}
```

A `MigrationContextFactory` creates `MigrationContext` instances:

```csharp
public interface IMigrationContextFactory
{
    MigrationContext Create(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions);
    MigrationContext CreateFromValidatedOptions(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions);
}

public sealed class MigrationContextFactory : IMigrationContextFactory
{
    public MigrationContext Create(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions)
    {
        RayMigratorOptionsPreparer.Prepare(options);
        return Activate(new MigrationContext(options, consoleOptions, AssemblyInfoHelper.GetRayMigratorVersion()));
    }

    // CreateFromValidatedOptions skips the preparation. Activate makes the context current (accessor and
    // logging context), fills the DAL properties and checks the schema names.
}
```

## Request/Response Pattern

**Purpose**: Type-safe service method contracts.

**Implementation**:

```csharp
// Request (Services.Abstractions/Models/Requests.cs)
public class MigrateUpRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? TargetReleaseVersion { get; set; }
    public MigrationRunMode RunMode { get; set; } = MigrationRunMode.Migrate;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
    public bool AllowOutOfOrder { get; set; } = false;
    public string[]? TargetGroupAliases { get; set; }
    public string[]? TargetGroupMigrationOrder { get; set; }
}

// Response (Services.Abstractions/Models/Results.cs)
public abstract class OperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }  // Negative = SQL template ResultCode, positive = C# backend ErrorCode, null = unclassified
    public List<string> Messages { get; set; } = new List<string>();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
}

public class MigrationOperationResult : OperationResult
{
    public int MigrationRunId { get; set; }   // MigrationRun.Id; 0 when the run mode writes no run row
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public MigrationOperation Operation { get; set; }
    public MigrationRunResult Result { get; set; }
    public int TotalMigrations { get; set; }
    public int SuccessfulMigrations { get; set; }
    public int FailedMigrations { get; set; }
    public string? CurrentRelease { get; set; }
    public List<MigrationFileResult> MigrationResults { get; set; } = new List<MigrationFileResult>();
}

// Service contract (Services.Abstractions/IMigrationService.cs)
public interface IMigrationService
{
    Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request);
    Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request);
    Task<ValidationResult> ValidateHashAsync(ValidateHashRequest request);
    Task<HashUpdateResult> UpdateHashAsync(UpdateHashRequest request);
    Task<BaselineResult> BaselineAsync(BaselineRequest request);
    Task<MigrationStatusInfo> GetStatusAsync(string productAlias);
    Task<MigrationHistory> GetHistoryAsync(string productAlias, int limit = 100);
    Task<FixIssuesResult> FixIssuesAsync(FixIssuesRequest request);
}
```

## Strategy Pattern

**Purpose**: Configurable error handling behavior.

**Implementation**:

The `MigrationErrorAction` enum defines the available strategies. See [Error Handling](../02-core-concepts/error-handling.md) for all values and their detailed behavior.

```csharp
// MigrationService.cs — supports file-level override via TOML MigrationErrorAction
private async Task HandleMigrationError(
    ProductOptions productOptions,
    MigrationFileInfo failedFile,
    int failedMigrationRecordId,
    List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)> successfullyMigratedRecords)
{
    // File-level TOML override takes precedence over product-level setting
    var errorAction = failedFile.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

    switch (errorAction)
    {
        case MigrationErrorAction.Terminate:
            // No rollback - database may be in unclear state
            break;

        case MigrationErrorAction.RollbackErrorOnly:
            // Rollback only the failed migration
            await RollbackSingleMigration(productOptions, failedFile, failedMigrationRecordId);
            break;

        case MigrationErrorAction.Rollback:
            // Rollback failed + all previously successful migrations (reverse order)
            await ExecuteRollbackForMigrations(recordsToRollback, productOptions, runMode);
            break;

        case MigrationErrorAction.RollbackRelease:
            // Rollback only migrations from the release that caused the error
            // Filters successfullyMigratedRecords to same release as failedFile
            await ExecuteRollbackForMigrations(releaseRecords, productOptions, runMode);
            break;

        case MigrationErrorAction.Ignore:
            // Log error and continue with next file
            break;
    }
}
```

## Builder Pattern

**Purpose**: Construct complex configuration objects.

**Implementation**:

Configuration loading of the CLI is handled by `IOptionsSource`, whose one implementation `JsonOptionsSource` builds the configuration hierarchy from multiple JSON files, conditionally adding files only if they exist on disk. It tracks which files were searched to provide helpful diagnostics on startup.

The base path for file lookup is resolved via `ResolveBasePath(configDir)`: when the `--config-dir` global option is provided, that directory is used; otherwise the current working directory is used.

```csharp
// Pipeline/JsonOptionsSource.cs
// _basePath is resolved from configDir (from --config-dir option) or current working directory
var configFilesSearched = new List<(string Filename, bool Found)>();

configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(_basePath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
configFilesSearched.Add((Path.Combine(_basePath, "appsettings.json"), File.Exists(Path.Combine(_basePath, "appsettings.json"))));

// Conditionally add environment-specific configuration
string envConfigurationFilename = $"appsettings.{environment}.json";
bool envConfigExists = !string.IsNullOrWhiteSpace(environment) && File.Exists(Path.Combine(_basePath, envConfigurationFilename));
configFilesSearched.Add((Path.Combine(_basePath, envConfigurationFilename), envConfigExists));
if (envConfigExists)
    configurationBuilder.AddJsonFile(envConfigurationFilename, optional: true, reloadOnChange: true);

// Conditionally add product-specific configurations
if (File.Exists(Path.Combine(_basePath, $"appsettings.{product}.json")))
    configurationBuilder.AddJsonFile($"appsettings.{product}.json", optional: true, reloadOnChange: true);

if (File.Exists(Path.Combine(_basePath, $"appsettings.{product}.{environment}.json")))
    configurationBuilder.AddJsonFile($"appsettings.{product}.{environment}.json", optional: true, reloadOnChange: true);

IConfigurationRoot rayMigratorConfiguration = configurationBuilder.Build();

// Environment variables are resolved separately via EnvironmentVariableReplacer
EnvironmentVariableReplacer.ReplaceWithEnvironmentVariables(rayMigratorConfigurationSection);
```

The result is returned as an `OptionsSourceResult`, which the `DirectModePipeline` uses to build the DI host.

> **Note**: Unlike the typical .NET pattern, `AddEnvironmentVariables()` and `AddCommandLine(args)` are **not** used. Instead, `{ENV:VAR}` placeholders in configuration values are resolved by a custom `EnvironmentVariableReplacer` after loading.

## Related Documentation

- [Design Decisions](design-decisions.md) - Why these patterns
- [Data Flow](data-flow.md) - Pattern interactions
- [Component Responsibilities](component-responsibilities.md) - Where patterns are implemented
- [Dependency Injection](dependency-injection.md) - Full DI registration details
