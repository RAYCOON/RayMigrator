using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared;
using Raycoon.RayMigrator.Shared.Constants;
using Serilog;
using Serilog.Events;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Builds a fully configured DI container for engine tests, replicating the
/// setup from Program.cs without CLI argument parsing.
/// Adapted from IntegrationTestHost with identical DI wiring.
/// </summary>
public sealed class EngineTestHost : IDisposable
{
    private IHost? _host;
    private IServiceScope? _scope;
    private RayMigratorOptions? _rayMigratorOptions;
    private global::Serilog.Core.Logger? _serilogLogger;
    private DatabaseLogWriter? _dbLogWriter;

    public IServiceProvider Services => _host!.Services;
    public IMigrationService MigrationService => _scope!.ServiceProvider.GetRequiredService<IMigrationService>();
    public MigrationContext MigrationContext => Services.GetRequiredService<MigrationContext>();
    public RayMigratorOptions RayMigratorOptions => _rayMigratorOptions!;

    /// <summary>
    /// Builds the host with full DI configuration analogous to Program.cs.
    /// </summary>
    public void Build(
        string configFilePath,
        string productAlias,
        MigrationCommand command,
        MigrationRunMode runMode,
        string? targetReleaseVersion = null)
    {
        // Resolve migration files root directory (solution root / Testing / MigrationFiles)
        string solutionRoot = FindSolutionRoot();
        string migrationFilesRoot = Path.Combine(solutionRoot, "Testing", "MigrationFiles");

        // Build configuration
        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile(configFilePath, optional: false, reloadOnChange: false);

        IConfigurationRoot configuration = configurationBuilder.Build();
        IConfigurationSection rayMigratorSection = configuration.GetSection(InternalConstants.RayMigratorSectionName);

        // Replace __MIGRATION_FILES_ROOT__ placeholder in configuration
        ReplaceMigrationFilesRoot(rayMigratorSection, migrationFilesRoot);

        // Create console options programmatically (no CLI parsing)
        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = command,
            Product = productAlias,
            Environment = "Docker",
            RunMode = runMode,
            TargetReleaseVersion = targetReleaseVersion,
            ShowStartupInfo = false,
            RevealSensitiveData = false
        };

        // Configure Serilog
        var serilogConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(rayMigratorSection)
            .Enrich.FromLogContext()
            .Enrich.With(new MigrationContextEnricher());

        RayMigratorDatabaseSink? databaseSink = null;
        var dbLoggingSection = rayMigratorSection.GetSection("DatabaseLogging");
        if (dbLoggingSection.Exists())
        {
            LogEventLevel dbSinkMinimumLevel = LogEventLevel.Information;
            var minLevelStr = dbLoggingSection["MinimumLevel"];
            if (!string.IsNullOrWhiteSpace(minLevelStr) && Enum.TryParse<LogLevel>(minLevelStr, true, out var parsedLevel))
            {
                dbSinkMinimumLevel = MapToSerilogLevel(parsedLevel);
            }
            databaseSink = new RayMigratorDatabaseSink(dbSinkMinimumLevel);
            serilogConfiguration = serilogConfiguration.WriteTo.Sink(databaseSink);
        }

        _serilogLogger = serilogConfiguration.CreateLogger();

        RayMigratorOptions rayMigratorOptions = null!;

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, builder) =>
            {
                builder.AddConfiguration(configuration);
            })
            .UseSerilog(_serilogLogger, dispose: false)
            .ConfigureServices((context, services) =>
            {
                services.AddOptions<RayMigratorOptions>()
                    .Configure(options => rayMigratorSection.Bind(options))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddTransient<IPostConfigureOptions<RayMigratorOptions>, ProductDefaultsPostConfigureOptions>();
                services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorOptionsValidator>();

                services.AddSingleton(consoleOptions);

                services.AddSingleton<DatabaseLogWriter>(serviceProvider =>
                {
                    rayMigratorOptions = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
                    _rayMigratorOptions = rayMigratorOptions;

                    if (rayMigratorOptions.DatabaseLogging != null)
                    {
                        if (DalFactory.TryGetDal(
                                rayMigratorOptions.DatabaseLogging!.DatabaseType!,
                                rayMigratorOptions.DatabaseLogging.ConnectionString!,
                                out IDal? dalInstance))
                        {
                            ConnectionValidator.ValidateDatabaseLoggerConnection(rayMigratorOptions, dalInstance!, consoleOptions);
                            return new DatabaseLogWriter(rayMigratorOptions, dalInstance);
                        }
                    }

                    return new DatabaseLogWriter(rayMigratorOptions);
                });

                // Register Service Layer (includes IMigrationContextAccessor and IMigrationContextFactory)
                services.AddRayMigratorServices(RayMigratorHostMode.Cli);

                // Register TemplateCache with explicit revealSensitiveData parameter
                services.AddSingleton<TemplateCache>(serviceProvider =>
                {
                    var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>();
                    var tcLogger = serviceProvider.GetRequiredService<ILogger<TemplateCache>>();
                    return new TemplateCache(opts, consoleOptions.RevealSensitiveData, tcLogger);
                });

                services.AddSingleton<TemplateExecutor>();

                // Create MigrationContext and set it on the singleton accessor
                services.AddSingleton<MigrationContext>(serviceProvider =>
                {
                    string rayMigratorVersion = AssemblyInfoHelper.GetRayMigratorVersion();
                    var ctx = new MigrationContext(rayMigratorOptions, consoleOptions, rayMigratorVersion);

                    var accessor = serviceProvider.GetRequiredService<IMigrationContextAccessor>();
                    accessor.Current = ctx;

                    return ctx;
                });
            })
            .Build();

        _scope = _host.Services.CreateScope();

        // Resolve DatabaseLogWriter first (sets rayMigratorOptions via its factory)
        _dbLogWriter = _host.Services.GetRequiredService<DatabaseLogWriter>();
        var dbLogWriter = _dbLogWriter;

        var ctx = _host.Services.GetRequiredService<MigrationContext>();
        MigrationLoggingContext.Current = ctx;

        // Note: SensitiveDataMasker is initialized per-command in ScenarioContext methods
        // (BeginScope + RegisterSensitiveData), not here, to ensure AsyncLocal scope
        // is in the same async context as the service call.

        // Initialize DatabaseLogWriter
        if (rayMigratorOptions.DatabaseLogging != null)
        {
            string loggingDatabaseType = rayMigratorOptions.DatabaseLogging!.DatabaseType!;
            var templateCache = _host.Services.GetRequiredService<TemplateCache>();
            var templateExecutor = _host.Services.GetRequiredService<TemplateExecutor>();
            dbLogWriter.InitDatabaseLogger(loggingDatabaseType, templateCache, templateExecutor);

            if (databaseSink != null)
            {
                databaseSink.SetWriter(dbLogWriter);
            }
        }

        // Initialize DalSpecificPropertiesDictionary
        SetDalSpecificPropertiesDictionary(ctx, rayMigratorOptions.Repository!.DatabaseType!, rayMigratorOptions.Repository!.ConnectionString!);

        foreach (TargetGroupOptions targetGroupOptions in ctx.ProductTargetGroupOptionsEnumerable!)
        {
            foreach (var targetOptions in targetGroupOptions.Targets!)
            {
                SetDalSpecificPropertiesDictionary(ctx, targetGroupOptions.DatabaseType!, targetOptions.ConnectionString!);
            }
        }

        // Add DatabaseLogging's DatabaseType to DalSpecificPropertiesDictionary (if configured)
        if (rayMigratorOptions.DatabaseLogging?.DatabaseType != null)
        {
            SetDalSpecificPropertiesDictionary(ctx, rayMigratorOptions.DatabaseLogging.DatabaseType,
                rayMigratorOptions.DatabaseLogging.ConnectionString!);
        }

        // Validate SchemaName based on DAL's SupportsSchema flag
        var logger = _host.Services.GetRequiredService<ILogger<EngineTestHost>>();

        SchemaNameValidator.ValidateSchemaName(
            ctx.DalSpecificPropertiesDictionary,
            rayMigratorOptions.Repository!.DatabaseType!,
            rayMigratorOptions.Repository.SchemaName,
            "Repository", logger);

        if (rayMigratorOptions.DatabaseLogging?.DatabaseType != null)
        {
            SchemaNameValidator.ValidateSchemaName(
                ctx.DalSpecificPropertiesDictionary,
                rayMigratorOptions.DatabaseLogging.DatabaseType,
                rayMigratorOptions.DatabaseLogging.SchemaName,
                "DatabaseLogging", logger);
        }

        // Validate connections
        ConnectionValidator.ValidateTargetConnections(ctx, logger);
    }

    private static void SetDalSpecificPropertiesDictionary(MigrationContext ctx, string databaseType, string connectionString)
    {
        if (!DalFactory.TryGetDal(databaseType, connectionString, out IDal? dalInstance))
            return;

        ctx.DalSpecificPropertiesDictionary.TryAdd(databaseType, dalInstance!.DalSpecificProperties);
    }

    private static void ReplaceMigrationFilesRoot(IConfigurationSection section, string migrationFilesRoot)
    {
        foreach (var child in section.GetChildren())
        {
            if (child.Value != null && child.Value.Contains("__MIGRATION_FILES_ROOT__"))
            {
                child.Value = child.Value.Replace("__MIGRATION_FILES_ROOT__", migrationFilesRoot);
            }
            else
            {
                ReplaceMigrationFilesRoot(child, migrationFilesRoot);
            }
        }
    }

    private static string FindSolutionRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "RayMigrator.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not find RayMigrator.sln in any parent directory.");
    }

    private static LogEventLevel MapToSerilogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Critical => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };

    public void Dispose()
    {
        // Flush pending database log entries before disposal
        try
        {
            _dbLogWriter?.Flush(TimeSpan.FromSeconds(30));
        }
        catch
        {
            // ignored
        }

        _scope?.Dispose();
        _scope = null;

        _host?.Dispose();
        _host = null;

        // Dispose the instance logger (not the static Log.Logger which other engines may use)
        try
        {
            _serilogLogger?.Dispose();
            _serilogLogger = null;
        }
        catch
        {
            // ignored
        }

        // Note: Masking scope is managed per-command in ScenarioContext, not here.
    }
}
