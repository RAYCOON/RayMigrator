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

// Registration with DataAnnotations validation
services.AddOptions<RayMigratorOptions>()
    .Configure(options => rayMigratorConfigurationSection.Bind(options))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// PostConfigure merges ProductDefaults into Products/TargetGroups.
// The static MergeDefaults method can also be called directly
// after building RayMigratorOptions from an alternative source.
// Copies: MigrationErrorAction, RollbackErrorAction, encoding, extensions,
//         RequireRollbackFile, StopRollbackOnMissingRollbackFile, UseCliToolAlias → Product level
//         TargetMigrationOrder, HashValidationScope, StopRollbackOnMissingRollbackFile, UseCliToolAlias → TargetGroup level
//         DbCommand* settings, UseCliToolAlias → Target level
services.AddTransient<IPostConfigureOptions<RayMigratorOptions>,
    ProductDefaultsPostConfigureOptions>();

// IValidateOptions implementation that delegates to the shared rule catalog
// (Raycoon.RayMigrator.Validation.RuleCatalog).
services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorOptionsValidator>();

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

**Bootstrap Options**: Before loading the full `RayMigratorOptions`, RayMigrator loads a minimal `RayMigratorBootstrapOptions` to determine the operating mode:

```csharp
public class RayMigratorBootstrapOptions
{
    public AdminDbOptions? AdminDb { get; set; }
    public SerilogOptions? Serilog { get; set; }
}
```

## Repository Pattern

**Purpose**: Abstract data access for migration tracking.

**Implementation**:

The repository database is separate from target databases. SQL templates handle CRUD operations:

```
Repository_CheckCreate.sql              - Create repository structure
Repository_Drop.sql                     - Drop repository schema
Repository_Product_CheckInsert.sql      - Ensure product exists (Name + NameLower)
Repository_Environment_CheckInsert.sql  - Ensure environment exists (Name + NameLower)
Repository_MigrationRun_Insert.sql      - Record migration run
Repository_MigrationRun_Update.sql      - Update run result/status
Repository_MigrationRun_Select.sql      - Query migration runs (history)
Repository_MigrationRun_SelectOrphaned.sql - Find orphaned runs (Fix command)
Repository_MigrationRun_FixOrphaned.sql - Mark orphaned runs as Error
Repository_MigrationRecord_Insert.sql         - Create migration record with block tracking
Repository_MigrationRecord_Update.sql         - Update migration block progress/status
Repository_MigrationRecord_UpdateRollback.sql - Update rollback fields (FileDown*)
Repository_MigrationRecord_UpdateHash.sql     - Update hash fields (Update-Hash command)
Repository_MigrationRecord_Select.sql         - Query migrations
Repository_MigrationRecord_FixOrphaned.sql    - Fix orphaned migration entries
Repository_MigrationRecord_GetInterrupted.sql - Find interrupted migrations for recovery
DatabaseLogging_CheckCreate.sql         - Create database logging table
DatabaseLogging_Insert.sql              - Insert log entry
```

**Tables**:
- `MigratorMeta` - Repository versioning
- `Product` - Registered products (`Name` + `NameLower` with unique index)
- `Environment` - Registered environments (`Name` + `NameLower` with unique index)
- `MigrationRun` - Migration run sessions
- `MigrationRunMeta` - Run metadata (JSON settings)
- `MigrationRecord` - Individual migrations with block-level tracking
- `MigrationRecordHistory` - Migration audit trail (archived records)
- `MigrationRunMode` - Lookup: run modes (Migrate, Simulate, ...)
- `MigrationOperation` - Lookup: operation types (MigrateUp, MigrateDown, ...)
- `MigrationRunResult` - Lookup: run results (Running=10, Error=90, Ok=100)
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

    public void RepositoryCheckCreate()
    {
        // 1. Get template from cache ({ENV:*} already replaced during cache init)
        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        //    → {CFG:*} placeholders replaced here via reflection-based property matching

        // 2. Build parameter list
        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("RepositoryDatabaseType", ...));

        // 3. Execute via DAL (returns "ResultCode,ResultMessage" string)
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        // 4. Use result (TemplateResponse has ResultCode + ResultMessage)
        _ctxAccessor.Current.MigrationState.MigratorMetaId = templateResponse.ResultCode;
    }
}
```

The `TemplateCache` (in `Raycoon.RayMigrator.Core.Templates`) loads and caches all SQL templates at startup. It supports deferred validation via the `validateConfiguration` parameter, which is set to `false` when Products configuration is not yet available at cache initialization:

```csharp
public class TemplateCache
{
    public TemplateCache(IOptions<RayMigratorOptions>? options, bool revealSensitiveData,
        ILogger<TemplateCache> logger, bool validateConfiguration = true) { /* ... */ }

    // Called on-demand when configuration becomes available
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
TemplateType   = "Repository_CheckCreate"
DatabaseType   = "SqlServer"
Author         = "RAYCOON.com GmbH (https://raycoon.com)"
Version        = "2026-04-18.1"
*/

CREATE TABLE [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRecord] (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductId INT NOT NULL,
    -- ...
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
        services.AddScoped<IMigrationContextAccessor, AsyncLocalMigrationContextAccessor>();

    services.AddSingleton<IMigrationContextFactory, MigrationContextFactory>();
    return services;
}
```

A factory creates `MigrationContext` instances (CLI creates one at startup):

```csharp
public interface IMigrationContextFactory
{
    MigrationContext Create(RayMigratorOptions options, string product, string environment,
        MigrationRunMode runMode, string version, string? targetReleaseVersion = null,
        bool revealSensitiveData = false);
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
│   └── ... (18 templates per DAL, flat layout)
├── PostgreSQL/
│   ├── Raycoon.RayMigrator.Database.PostgreSQL.dll
│   └── *.sql (18 template files)
├── MariaDb/
│   ├── Raycoon.RayMigrator.Database.MariaDb.dll
│   └── *.sql (18 template files)
├── MySql/
│   ├── Raycoon.RayMigrator.Database.MySql.dll
│   └── *.sql (18 template files)
└── Sqlite/
    ├── Raycoon.RayMigrator.Database.Sqlite.dll
    └── *.sql (18 template files)
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
    MigrationContext Create(
        RayMigratorOptions options, string product, string environment,
        MigrationRunMode runMode, string version,
        string? targetReleaseVersion = null, bool revealSensitiveData = false);
}

public class MigrationContextFactory : IMigrationContextFactory
{
    public MigrationContext Create(/* ... */)
    {
        var consoleOptions = new RayMigratorConsoleOptions { /* ... */ };
        return new MigrationContext(options, consoleOptions, version);
    }
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
    public Guid RunId { get; set; }
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

Configuration loading is handled by `IOptionsSource` implementations. The `JsonOptionsSource` builds the configuration hierarchy from multiple JSON files, conditionally adding files only if they exist on disk. It tracks which files were searched to provide helpful diagnostics on startup.

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
