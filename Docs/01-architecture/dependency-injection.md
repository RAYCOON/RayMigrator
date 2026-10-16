# Dependency Injection

RayMigrator uses Microsoft.Extensions.DependencyInjection for its DI container.

## Container Configuration

RayMigrator supports two hosting modes with different DI configurations, controlled by the `RayMigratorHostMode` enum:

```csharp
public enum RayMigratorHostMode
{
    Cli,  // Short-lived process, singleton MigrationContext
    Api   // Long-lived server, per-request MigrationContext via AsyncLocal
}
```

### CLI Direct Mode (`DirectModePipeline.ExecuteAsync`)

`DirectModePipeline` (in `Raycoon.RayMigrator.Pipeline`) handles the DI registration of the CLI. `JsonOptionsSource`, the one `IOptionsSource` implementation, loads the appsettings hierarchy; the pipeline binds `RayMigratorOptions` from its configuration section:

```csharp
// DirectModePipeline.ExecuteAsync — called by Program.RunDirectMode()
host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, builder) =>
    {
        // Add the loaded configuration to the host builder
        if (sourceResult.HostConfiguration != null)
            builder.AddConfiguration(sourceResult.HostConfiguration);
    })
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        // Bind the options from the configuration section with validation; the data annotations name a failed element by alias
        services.AddOptions<RayMigratorOptions>()
            .Configure(options => sourceResult.RayMigratorConfigSection.Bind(options))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorDataAnnotationValidateOptions>();
        services.AddTransient<IPostConfigureOptions<RayMigratorOptions>,
            ProductDefaultsPostConfigureOptions>();
        // The validator checks connection strings as written, so RULE_7_3 sees {ENV:...} placeholders (#30)
        services.AddSingleton<IValidateOptions<RayMigratorOptions>>(
            new RayMigratorOptionsValidator(sourceResult.RayMigratorConfigSectionAsWritten));

        // Console parameters
        services.AddSingleton(consoleOptions);

        // Database logging
        services.AddSingleton<DatabaseLogWriter>(serviceProvider => { ... });

        // Service layer (CLI mode — SingletonMigrationContextAccessor)
        services.AddRayMigratorServices(RayMigratorHostMode.Cli);

        // Infrastructure — TemplateCache with explicit revealSensitiveData, validated against the bound options
        services.AddSingleton<TemplateCache>(serviceProvider =>
        {
            var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
            var tcLogger = serviceProvider.GetRequiredService<ILogger<TemplateCache>>();
            var templateCache = new TemplateCache(consoleOptions.RevealSensitiveData, tcLogger);
            templateCache.ValidateConfigurationAgainstTemplateCache(opts);
            return templateCache;
        });
        services.AddSingleton<TemplateExecutor>();

        // Console client
        services.AddScoped<RayMigratorService>();

        // Core - MigrationContext created by the factory, which fills the DAL properties, checks the
        // schema names and makes the context current on the singleton accessor
        services.AddSingleton<MigrationContext>(serviceProvider =>
        {
            var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
            var factory = serviceProvider.GetRequiredService<IMigrationContextFactory>();
            return factory.CreateFromValidatedOptions(opts, consoleOptions);
        });
    })
    .Build();
```

## Service Lifetimes

### Singleton Services

Services that maintain state throughout the application lifetime:

| Service | Lifetime | Rationale |
|---------|----------|-----------|
| `IMigrationContextAccessor` | Singleton (`SingletonMigrationContextAccessor`; `AsyncLocalMigrationContextAccessor` in API mode) | CLI: wraps single context; API: one context per async flow |
| `IMigrationContextFactory` | Singleton | Stateless factory, creates `MigrationContext` instances |
| `IRepositorySchemaService` | Singleton (`RepositorySchemaService`) | Creates or upgrades a repository without a run; depends on the host's `TemplateCache` |
| `IRepositoryMaintenanceService` | Singleton (`RepositoryMaintenanceService`) | Deletes old history and log rows of a repository without a run (retention, ADR-027); depends on the host's `TemplateCache` |
| `MigrationContext` | Singleton | Created once and set on accessor |
| `RayMigratorConsoleOptions` | Singleton | CLI parameters, single instance |
| `TemplateCache` | Singleton | Cache loaded templates for performance |
| `TemplateExecutor` | Singleton | Single context in CLI mode |
| `DatabaseLogWriter` | Singleton | Database logging, configured once at startup |
| `ILogger<T>` | Singleton | Serilog logger instances |

### Scoped Services

Services created per operation:

| Service | Rationale |
|---------|-----------|
| `IMigrationService` | Fresh instance per operation |
| `ICliToolExecutor` | External CLI tool execution, one per operation |
| `RayMigratorService` | CLI-to-service bridge (Pipeline project), one per operation |

### Not DI-Registered (Static Factory)

| Component | Access Pattern |
|-----------|---------------|
| `IDal` implementations | `DalFactory.TryGetDal(databaseType, connectionString, out IDal?)` |

`DalFactory` is a static class that discovers DAL implementations via reflection (`[DatabaseType]` attribute) using DependencyContext-based scanning for built-in DALs (from deps.json, works with single-file publish) and filesystem scanning of `DataAccessLayers/` subdirectories for external DAL plugins, and caches instances in a `ConcurrentDictionary` keyed by database type and connection string.

## Registration Patterns

### Options Pattern

Configuration sections are bound to strongly-typed options classes with DataAnnotations validation. The binding is the CLI pipeline's way of loading and validating the files; the services never consume `IOptions<RayMigratorOptions>`. They read the configuration of the current run from `MigrationContext.RayMigratorOptions`, which the context factory receives from the pipeline (`CreateFromValidatedOptions`) or prepares itself for a host that built the options in memory (`Create`, see [MigrationContext](../02-core-concepts/migration-context.md#hosting-the-engine-in-process)).

```csharp
// Registration with validation (the data annotations name a failed element by alias)
services.AddOptions<RayMigratorOptions>()
    .Configure(options => sourceResult.RayMigratorConfigSection.Bind(options))
    .ValidateOnStart();
services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorDataAnnotationValidateOptions>();

// PostConfigure merges defaults (MigrationErrorAction, RollbackErrorAction, encoding, etc.)
// into product/target options
services.AddTransient<IPostConfigureOptions<RayMigratorOptions>,
    ProductDefaultsPostConfigureOptions>();

// IValidateOptions implementation that delegates to the shared rule catalog. It receives the
// RayMigrator section as written, so RULE_7_3 inspects connection strings before placeholder resolution.
services.AddSingleton<IValidateOptions<RayMigratorOptions>>(
    new RayMigratorOptionsValidator(sourceResult.RayMigratorConfigSectionAsWritten));
```

`ProductDefaultsPostConfigureOptions` delegates to the static `MergeDefaults(RayMigratorOptions)` method, which `RayMigratorOptionsPreparer` also runs for options a host built in memory (`IMigrationContextFactory.Create`). This avoids duplicating the merge logic.

### Static Factory Pattern for Database Access

Database access layers are created via the static `DalFactory`, not DI:

```csharp
// DalFactory discovers built-in DALs via DependencyContext (deps.json, works with single-file publish)
// and scans DataAccessLayers/ subdirectories for external DAL plugins decorated with [DatabaseType]
// DAL instances are cached by "{databaseType}_{connectionString}" key in a ConcurrentDictionary

// Usage in TemplateExecutor.InitializeFromContext() (lazy, on first access):
DalFactory.TryGetDal(
    _ctxAccessor.Current.RayMigratorOptions.Repository!.DatabaseType!,
    _ctxAccessor.Current.RayMigratorOptions.Repository.ConnectionString!,
    out var repositoryDalInstance);
_repositoryDalBacking = repositoryDalInstance!;
```

### Interface Segregation

Services expose interfaces from abstraction assemblies:

```
Raycoon.RayMigrator.Services.Abstractions
├── IMigrationService.cs
├── IRepositorySchemaService.cs
├── IRepositoryMaintenanceService.cs
└── Models/
    ├── Requests.cs    (MigrateUpRequest, MigrateDownRequest, ValidateHashRequest,
    │                    UpdateHashRequest, BaselineRequest, FixIssuesRequest)
    ├── Results.cs     (OperationResult, MigrationOperationResult, MigrationFileResult,
    │                    ValidationResult, HashValidationIssue, HashUpdateResult,
    │                    BaselineResult, MigrationStatusInfo, TargetGroupStatus,
    │                    MigrationHistory, MigrationRunInfo, FixIssuesResult, OrphanedRunInfo)
    ├── RunCancellation.cs          (the handle a host cancels one run with)
    ├── PendingMigrationInfo.cs     (one entry of MigrationStatusInfo.PendingFiles)
    ├── PendingMigrationReason.cs
    ├── RepositorySchemaResult.cs
    ├── RetentionRequest.cs
    └── RetentionResult.cs

Raycoon.RayMigrator.Services
├── CliToolExecutor.cs  (ICliToolExecutor, CliToolExecutor, CliToolExecutionRequest,
│                         CliToolExecutionResult)
├── RepositorySchemaService.cs
├── RepositoryMaintenanceService.cs
└── ServiceCollectionExtensions.cs
```

## Service Registration Extension

**Location**: `Raycoon.RayMigrator.Services/ServiceCollectionExtensions.cs`

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRayMigratorServices(
        this IServiceCollection services,
        RayMigratorHostMode hostMode = RayMigratorHostMode.Cli)
    {
        // Register the main migration service
        services.AddScoped<IMigrationService, MigrationService>();

        // Register CLI tool executor for external SQL tool execution
        services.AddScoped<ICliToolExecutor, CliToolExecutor>();

        // Register context accessor based on host mode. Both are singletons: the API accessor isolates
        // per async flow through a static AsyncLocal, so a scoped registration would isolate nothing
        if (hostMode == RayMigratorHostMode.Cli)
        {
            services.AddSingleton<IMigrationContextAccessor, SingletonMigrationContextAccessor>();
        }
        else
        {
            services.AddSingleton<IMigrationContextAccessor, AsyncLocalMigrationContextAccessor>();
        }

        // Register the context factory, the entry point of every host for one run
        services.AddSingleton<IMigrationContextFactory, MigrationContextFactory>();

        // Register the schema service for hosts that create or upgrade a repository without a run (needs the host's TemplateCache)
        services.AddSingleton<IRepositorySchemaService, RepositorySchemaService>();

        // Register the maintenance service for hosts that delete old history and log rows of a repository (ADR-027)
        services.AddSingleton<IRepositoryMaintenanceService, RepositoryMaintenanceService>();

        return services;
    }
}
```

> **Note**: Infrastructure services (`TemplateCache`, `TemplateExecutor`) are registered directly in `DirectModePipeline` (CLI), not in this extension method. `TemplateCache` is always Singleton. `TemplateExecutor` is Singleton in CLI mode. `RepositorySchemaService` and `RepositoryMaintenanceService` take the `TemplateCache` in their constructors, so a host registers the cache before it resolves `IRepositorySchemaService` or `IRepositoryMaintenanceService`. Database layer uses the static `DalFactory` -- no DI registration needed.

## Constructor Injection Examples

### MigrationContext (Created via factory or directly)

In CLI mode, the DI factory of `MigrationContext` calls `IMigrationContextFactory.CreateFromValidatedOptions`, which creates the context and sets it on `IMigrationContextAccessor`:

```csharp
public class MigrationContext
{
    public MigrationContext(
        RayMigratorOptions rayMigratorOptions,
        RayMigratorConsoleOptions rayMigratorConsoleOptions,
        string rayMigratorVersion,
        MigrationState? migrationState = null)
    {
        RayMigratorOptions = rayMigratorOptions;
        RayMigratorConsoleOptions = rayMigratorConsoleOptions;
        RayMigratorVersion = rayMigratorVersion;

        // Eagerly resolve TargetGroups for current product
        ProductTargetGroupOptionsEnumerable = RayMigratorOptions.Products!
            .First(p => p.Alias == RayMigratorConsoleOptions.Product).TargetGroups;

        // Create or deep-copy MigrationState
        MigrationState = migrationState == null
            ? new MigrationState()
            : new MigrationState
            {
                MigrationEvent = migrationState.MigrationEvent,
                MigratorMetaId = migrationState.MigratorMetaId,
                ProductId = migrationState.ProductId,
                EnvironmentId = migrationState.EnvironmentId,
                MigrationRunId = migrationState.MigrationRunId,
                MigrationRecordId = migrationState.MigrationRecordId,
                ReleaseVersionFromFileNameWithPath = migrationState.ReleaseVersionFromFileNameWithPath,
                FilenameWithRelativePath = migrationState.FilenameWithRelativePath,
                FileOrderId = migrationState.FileOrderId,
                FileBlockId = migrationState.FileBlockId,
                MigrationRunResult = migrationState.MigrationRunResult,
                MigrationOperation = migrationState.MigrationOperation,
                TargetGroupAlias = migrationState.TargetGroupAlias,
                HashValidationScope = migrationState.HashValidationScope,
                TargetAlias = migrationState.TargetAlias
            };
    }
}
```

### MigrationService (Multiple dependencies)

```csharp
public class MigrationService : IMigrationService
{
    private readonly ILogger<MigrationService> _logger;
    private readonly TemplateExecutor _templateExecutor;
    private readonly IMigrationContextAccessor _ctxAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICliToolExecutor _cliToolExecutor;

    public MigrationService(
        ILogger<MigrationService> logger,
        TemplateExecutor templateExecutor,
        IMigrationContextAccessor ctxAccessor,
        IServiceProvider serviceProvider,
        ICliToolExecutor cliToolExecutor)
    {
        _logger = logger;
        _templateExecutor = templateExecutor;
        _ctxAccessor = ctxAccessor;
        _serviceProvider = serviceProvider;
        _cliToolExecutor = cliToolExecutor;
    }

    // Access the current context via accessor:
    // _ctxAccessor.Current.MigrationState.ProductId = productId;
}
```

### TemplateExecutor

```csharp
public class TemplateExecutor
{
    private readonly TemplateCache _templateCache;
    private readonly ILogger<TemplateExecutor> _logger;
    private readonly IMigrationContextAccessor _ctxAccessor;
    private RepositoryOptions? _repositoryBacking;
    private IDal? _repositoryDalBacking;

    // Context access is deferred to first use (not constructor time) to support
    // scenarios where MigrationContext is set after DI resolution.
    public TemplateExecutor(
        TemplateCache templateCache,
        ILogger<TemplateExecutor> logger,
        IMigrationContextAccessor ctxAccessor)
    {
        _templateCache = templateCache;
        _logger = logger;
        _ctxAccessor = ctxAccessor;
        // _repository and _repositoryDal are lazily initialized on first access
    }

    // Lazy properties that resolve from context on first use
    private RepositoryOptions _repository
    {
        get { if (_repositoryBacking == null) InitializeFromContext(); return _repositoryBacking!; }
    }
    private IDal _repositoryDal
    {
        get { if (_repositoryBacking == null) InitializeFromContext(); return _repositoryDalBacking!; }
    }

    private void InitializeFromContext()
    {
        _repositoryBacking = _ctxAccessor.Current.RayMigratorOptions.Repository!;
        // Resolves DAL via static DalFactory (not DI)
        if (DalFactory.TryGetDal(_repositoryBacking.DatabaseType!, _repositoryBacking.ConnectionString!, out var dal))
            _repositoryDalBacking = dal!;
    }
}
```

### TemplateCache (Decoupled from MigrationContext and from the options)

```csharp
public class TemplateCache
{
    private readonly bool _revealSensitiveData;
    private readonly ILogger _logger;

    public TemplateCache(bool revealSensitiveData, ILogger<TemplateCache> logger)
    {
        _revealSensitiveData = revealSensitiveData;
        _logger = logger;
        Initialize();
    }

    /// Called by the host for every configuration it runs; the CLI pipeline calls it once after the options are bound
    public void ValidateConfigurationAgainstTemplateCache(RayMigratorOptions options) { ... }

    /// Gets all available DAL names (e.g. "SqlServer", "PostgreSQL", "MariaDb", "MySql")
    public List<string> GetAvailableDatabaseTypes() { ... }
}
```

## Testing with DI

The unit test project (`Raycoon.RayMigrator.Tests.Unit`) uses xUnit, AwesomeAssertions, and NSubstitute. `InternalsVisibleTo` is configured in Services and Core `.csproj` files to allow testing of internal methods.

### Unit Test Approach

```csharp
public class MigrationServiceTests
{
    // MigrationContext and TemplateExecutor are concrete classes (not interfaces),
    // which makes them difficult to mock directly.
    // RuntimeHelpers.GetUninitializedObject is used to create test instances
    // without calling the constructor.
}
```

## Related Documentation

- [Overview](overview.md) - Architecture overview
- [Patterns](patterns.md) - Related patterns
- [Component Responsibilities](component-responsibilities.md) - Service details
