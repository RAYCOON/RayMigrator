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
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared;
using Raycoon.RayMigrator.Shared.Configuration;
using Raycoon.RayMigrator.Shared.Exceptions;
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
    public async Task BuildAsync(
        string configFilePath,
        string productAlias,
        MigrationCommand command,
        MigrationRunMode runMode,
        string? targetReleaseVersion = null,
        string environment = "Docker",
        RunInitiatorValues? initiator = null)
    {
        // Resolve migration files root directory (solution root / Testing / MigrationFiles)
        string solutionRoot = FindSolutionRoot();
        string migrationFilesRoot = Path.Combine(solutionRoot, "Testing", "MigrationFiles");

        // Load the configuration through the engine's loader, as the CLI does: the appsettings hierarchy of the
        // scenario's directory (only appsettings.json exists there), merged by alias and reduced to the product the
        // host is built for (ADR-026). The host configuration is the merged document as written, the section the
        // one with resolved placeholders, like DirectModePipeline uses them.
        if (!string.Equals(Path.GetFileName(configFilePath), ConfigurationFileChain.BaseFileName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The engine test host loads the hierarchy of a directory; the file must be {ConfigurationFileChain.BaseFileName}.", nameof(configFilePath));

        var sourceResult = new JsonOptionsSource(Path.GetDirectoryName(configFilePath)!).LoadAsync(productAlias, environment).GetAwaiter().GetResult();
        IConfigurationRoot configuration = sourceResult.HostConfiguration!;
        IConfigurationSection rayMigratorSection = sourceResult.RayMigratorConfigSection;

        // Like DirectModePipeline: a placeholder whose variable is not set stops the start before the host is built
        var unresolved = sourceResult.ReplacedEnvironmentVariables
            .Where(v => string.IsNullOrWhiteSpace(v.EnvironmentVariableValue))
            .Select(v => $"[{v.EnvironmentVariableName}] at [{v.Path}]")
            .Distinct()
            .ToList();
        if (unresolved.Count > 0)
        {
            throw new ApplicationStartupException(
                "RayMigrator is shutting down due to error(s) replacing configuration variables from environment variables: " +
                string.Join(", ", unresolved));
        }

        // Replace __MIGRATION_FILES_ROOT__ placeholder in configuration
        ReplaceMigrationFilesRoot(rayMigratorSection, migrationFilesRoot);

        // Create console options programmatically (no CLI parsing)
        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = command,
            Product = productAlias,
            Environment = environment,
            RunMode = runMode,
            TargetReleaseVersion = targetReleaseVersion,
            ShowStartupInfo = false,
            RevealSensitiveData = false,
            Source = initiator?.Source,
            InitiatedBy = initiator?.InitiatedBy,
            InitiatedBySubject = initiator?.InitiatedBySubject,
            Host = initiator?.Host
        };

        // Configure Serilog
        var serilogConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(rayMigratorSection)
            .Enrich.FromLogContext()
            .Enrich.With(new MigrationContextEnricher());

        RayMigratorDatabaseSink? databaseSink = null;
        // Database logging is on unless the section says Enabled: false; without a section it goes into the repository.
        var dbLoggingSection = rayMigratorSection.GetSection("DatabaseLogging");
        if (!dbLoggingSection.Exists() || !(bool.TryParse(dbLoggingSection["Enabled"], out var dbLoggingEnabled) && !dbLoggingEnabled))
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
                    .ValidateOnStart();
                services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorDataAnnotationValidateOptions>();

                services.AddTransient<IPostConfigureOptions<RayMigratorOptions>, ProductDefaultsPostConfigureOptions>();

                // Like DirectModePipeline: the rule catalog reads the connection strings as written, so RULE_7_3
                // sees a placeholder and not the resolved credential, paired by index with the bound lists
                services.AddSingleton<IValidateOptions<RayMigratorOptions>>(
                    new RayMigratorOptionsValidator(sourceResult.RayMigratorConfigSectionAsWritten));

                services.AddSingleton(consoleOptions);

                services.AddSingleton<DatabaseLogWriter>(serviceProvider =>
                {
                    rayMigratorOptions = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
                    _rayMigratorOptions = rayMigratorOptions;

                    if (rayMigratorOptions.IsDatabaseLoggingEnabled())
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
                    var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
                    var tcLogger = serviceProvider.GetRequiredService<ILogger<TemplateCache>>();
                    var templateCache = new TemplateCache(consoleOptions.RevealSensitiveData, tcLogger);
                    templateCache.ValidateConfigurationAgainstTemplateCache(opts);
                    return templateCache;
                });

                services.AddSingleton<TemplateExecutor>();

                // Create the MigrationContext through the factory (DAL properties, schema names, accessor, logging context)
                services.AddSingleton<MigrationContext>(serviceProvider =>
                {
                    var factory = serviceProvider.GetRequiredService<IMigrationContextFactory>();
                    return factory.CreateFromValidatedOptions(rayMigratorOptions, consoleOptions);
                });
            })
            .Build();

        _scope = _host.Services.CreateScope();

        // Resolve DatabaseLogWriter first (sets rayMigratorOptions via its factory)
        _dbLogWriter = _host.Services.GetRequiredService<DatabaseLogWriter>();
        var dbLogWriter = _dbLogWriter;

        var ctx = _host.Services.GetRequiredService<MigrationContext>();

        // Note: SensitiveDataMasker is initialized per-command in ScenarioContext methods
        // (BeginScope + RegisterSensitiveData), not here, to ensure AsyncLocal scope
        // is in the same async context as the service call.

        // Initialize DatabaseLogWriter (on by default, into the repository; off with Enabled: false)
        if (rayMigratorOptions.IsDatabaseLoggingEnabled())
        {
            string loggingDatabaseType = rayMigratorOptions.DatabaseLogging!.DatabaseType!;
            var templateCache = _host.Services.GetRequiredService<TemplateCache>();
            var templateExecutor = _host.Services.GetRequiredService<TemplateExecutor>();
            await dbLogWriter.InitDatabaseLoggerAsync(loggingDatabaseType, templateCache, templateExecutor);

            if (databaseSink != null)
            {
                databaseSink.SetWriter(dbLogWriter);
            }
        }

        // Validate connections
        var logger = _host.Services.GetRequiredService<ILogger<EngineTestHost>>();
        ConnectionValidator.ValidateTargetConnections(ctx, logger);
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

    /// <summary>
    /// Flushes the asynchronous DatabaseLogWriter queue so that tests can assert on MigrationLog rows.
    /// </summary>
    public void FlushDatabaseLog(TimeSpan? timeout = null) => _dbLogWriter?.Flush(timeout ?? TimeSpan.FromSeconds(30));

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
