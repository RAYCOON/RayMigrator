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

The unified `DirectModePipeline` (in `Raycoon.RayMigrator.Pipeline`) handles DI registration. The `IOptionsSource` implementation determines how `RayMigratorOptions` are loaded:

```csharp
// DirectModePipeline.ExecuteAsync — called by Program.RunDirectMode()
host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, builder) =>
    {
        // JSON mode: add configuration to the host builder
        if (sourceResult.HostConfiguration != null)
            builder.AddConfiguration(sourceResult.HostConfiguration);
    })
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        // Register RayMigratorOptions differently based on source
        if (sourceResult.PreBuiltOptions != null)
        {
            // Pre-built mode: register pre-built options directly
            services.AddSingleton(Options.Create(sourceResult.PreBuiltOptions));
        }
        else
        {
            // JSON mode: bind from configuration section with validation
            services.AddOptions<RayMigratorOptions>()
                .Configure(options => sourceResult.RayMigratorConfigSection.Bind(options))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddTransient<IPostConfigureOptions<RayMigratorOptions>,
                ProductDefaultsPostConfigureOptions>();
            services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorOptionsValidator>();
        }

        // Console parameters
        services.AddSingleton(consoleOptions);

        // Database logging
        services.AddSingleton<DatabaseLogWriter>(serviceProvider => { ... });

        // Service layer (CLI mode — SingletonMigrationContextAccessor)
        services.AddRayMigratorServices(RayMigratorHostMode.Cli);

        // Infrastructure — TemplateCache with explicit revealSensitiveData
        services.AddSingleton<TemplateCache>(serviceProvider =>
        {
            var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>();
            var tcLogger = serviceProvider.GetRequiredService<ILogger<TemplateCache>>();
            return new TemplateCache(opts, consoleOptions.RevealSensitiveData, tcLogger);
        });
        services.AddSingleton<TemplateExecutor>();

        // Console client
        services.AddScoped<RayMigratorService>();

        // Core - MigrationContext created and set on singleton accessor
        services.AddSingleton<MigrationContext>(serviceProvider =>
        {
            var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
            string rayMigratorVersion = Raycoon.RayMigrator.Shared.AssemblyInfoHelper.GetRayMigratorVersion();
            var migCtx = new MigrationContext(opts, consoleOptions, rayMigratorVersion);

            // Set context on the singleton accessor so all services can access it
            var accessor = serviceProvider.GetRequiredService<IMigrationContextAccessor>();
            accessor.Current = migCtx;

            return migCtx;
        });
    })
    .Build();
```

## Service Lifetimes

### Singleton Services

Services that maintain state throughout the application lifetime:

| Service | Lifetime | Rationale |
|---------|----------|-----------|
| `IMigrationContextAccessor` | Singleton (`SingletonMigrationContextAccessor`) | CLI: wraps single context |
| `IMigrationContextFactory` | Singleton | Stateless factory, creates `MigrationContext` instances |
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

Configuration sections are bound to strongly-typed options classes with DataAnnotations validation:

```csharp
// Registration with validation (JSON mode)
services.AddOptions<RayMigratorOptions>()
    .Configure(options => sourceResult.RayMigratorConfigSection.Bind(options))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// PostConfigure merges defaults (MigrationErrorAction, RollbackErrorAction, encoding, etc.)
// into product/target options
services.AddTransient<IPostConfigureOptions<RayMigratorOptions>,
    ProductDefaultsPostConfigureOptions>();

// IValidateOptions implementation that delegates to the shared rule catalog.
services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorOptionsValidator>();

// Pre-built mode: pre-built options, no PostConfigure needed
// (MergeDefaults was already called by the options provider)
services.AddSingleton(Options.Create(rayMigratorOptions));

// Injection
public class MigrationService
{
    private readonly RayMigratorOptions _options;

    public MigrationService(IOptions<RayMigratorOptions> options)
    {
        _options = options.Value;
    }
}
```

`ProductDefaultsPostConfigureOptions` delegates to the static `MergeDefaults(RayMigratorOptions)` method, which can also be called directly after building `RayMigratorOptions` from an alternative source. This avoids duplicating the merge logic.

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
└── Models/
    ├── Requests.cs    (MigrateUpRequest, MigrateDownRequest, ValidateHashRequest,
    │                    UpdateHashRequest, BaselineRequest, FixIssuesRequest)
    └── Results.cs     (OperationResult, MigrationOperationResult, MigrationFileResult,
                         ValidationResult, HashValidationIssue, HashUpdateResult,
                         BaselineResult, MigrationStatusInfo, TargetGroupStatus,
                         MigrationHistory, MigrationRunInfo, FixIssuesResult, OrphanedRunInfo)

Raycoon.RayMigrator.Services
├── CliToolExecutor.cs  (ICliToolExecutor, CliToolExecutor, CliToolExecutionRequest,
│                         CliToolExecutionResult)
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

        // Register context accessor based on host mode
        if (hostMode == RayMigratorHostMode.Cli)
        {
            services.AddSingleton<IMigrationContextAccessor, SingletonMigrationContextAccessor>();
        }
        else
        {
            services.AddScoped<IMigrationContextAccessor, AsyncLocalMigrationContextAccessor>();
        }

        // Register context factory (always singleton, stateless)
        services.AddSingleton<IMigrationContextFactory, MigrationContextFactory>();

        return services;
    }
}
```

> **Note**: Infrastructure services (`TemplateCache`, `TemplateExecutor`) are registered directly in `DirectModePipeline` (CLI), not in this extension method. `TemplateCache` is always Singleton. `TemplateExecutor` is Singleton in CLI mode. Database layer uses the static `DalFactory` -- no DI registration needed.

## Constructor Injection Examples

### MigrationContext (Created via factory or directly)

In CLI mode, `MigrationContext` is created directly in the DI factory and set on `IMigrationContextAccessor`:

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
                MigrationStatus = migrationState.MigrationStatus,
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
    private readonly IOptions<RayMigratorOptions> _options;
    private readonly TemplateExecutor _templateExecutor;
    private readonly IMigrationContextAccessor _ctxAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICliToolExecutor _cliToolExecutor;

    public MigrationService(
        ILogger<MigrationService> logger,
        IOptions<RayMigratorOptions> options,
        TemplateExecutor templateExecutor,
        IMigrationContextAccessor ctxAccessor,
        IServiceProvider serviceProvider,
        ICliToolExecutor cliToolExecutor)
    {
        _logger = logger;
        _options = options;
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

### TemplateCache (Decoupled from MigrationContext)

```csharp
public class TemplateCache
{
    private readonly RayMigratorOptions? _options;
    private readonly bool _revealSensitiveData;
    private readonly ILogger _logger;

    public TemplateCache(
        IOptions<RayMigratorOptions>? options,
        bool revealSensitiveData,
        ILogger<TemplateCache> logger,
        bool validateConfiguration = true)
    {
        _options = options?.Value;
        _revealSensitiveData = revealSensitiveData;
        _logger = logger;
        Initialize();
        if (validateConfiguration && _options != null)
            ValidateConfigurationAgainstTemplateCache(_options);
    }

    /// Can be called on-demand when Products/Repository config becomes available
    public void ValidateConfigurationAgainstTemplateCache(RayMigratorOptions options) { ... }

    /// Gets all available DAL names (e.g. "SqlServer", "PostgreSQL", "MariaDb", "MySql")
    public List<string> GetAvailableDatabaseTypes() { ... }
}
```

## Testing with DI

The unit test project (`Raycoon.RayMigrator.Tests.Unit`) uses xUnit, FluentAssertions, and NSubstitute. `InternalsVisibleTo` is configured in Services and Core `.csproj` files to allow testing of internal methods.

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
