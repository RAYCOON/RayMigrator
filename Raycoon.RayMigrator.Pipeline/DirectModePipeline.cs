using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Core.Configuration.Sources;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Infrastructure;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;
using Serilog;
using Raycoon.RayMigrator.Core.Logging;

namespace Raycoon.RayMigrator.Pipeline;

/// <summary>
/// Unified execution pipeline for Direct mode (both Standalone/JSON and Managed/Admin-DB).
/// Handles the complete lifecycle after options have been loaded:
/// Serilog creation, DI host build, DatabaseLogWriter init, DAL properties,
/// connection validation, migration service execution, and shutdown.
/// </summary>
public static class DirectModePipeline
{
    /// <summary>
    /// Executes the Direct mode pipeline with the given options source result.
    /// </summary>
    public static async Task<int> ExecuteAsync(
        string[] args,
        OptionsSourceResult sourceResult,
        RayMigratorConsoleOptions consoleOptions,
        string assemblyInfo,
        string environment,
        string environmentOrigin)
    {
        global::Serilog.ILogger serilogLogger = null!;
        Microsoft.Extensions.Logging.ILogger? logger = null;
        bool isSerilogLoggerActivated = false;
        int exitCode = 0;

        try
        {
            #region Early configuration validation

            // Check for Serilog configuration with helpful diagnostics (#31)
            if (!sourceResult.RayMigratorConfigSection.GetSection("Serilog").Exists())
            {
                Console.WriteLine(assemblyInfo);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(BuildMissingSerilogSectionMessage(
                    sourceResult.RayMigratorConfigSection.Exists(),
                    sourceResult.ConfigFileDiagnostics,
                    consoleOptions.Product,
                    environment));
                Console.ResetColor();
                return 4;
            }

            #endregion Early configuration validation


            #region Create Serilog logger

            RayMigratorDatabaseSink? databaseSink;

            (serilogLogger, databaseSink) = SerilogFactory.Create(sourceResult.RayMigratorConfigSection);
            isSerilogLoggerActivated = true;

            serilogLogger.Information("RayMigrator starting up in {ModeName}, environment [{Environment}] (from {EnvironmentOrigin})",
                sourceResult.ModeName, environment, environmentOrigin);

            if (sourceResult.PreBuiltOptions != null)
            {
                serilogLogger.Information("Configuration loaded from Admin-DB for Product [{Product}] Environment [{Environment}]",
                    consoleOptions.Product, environment);
            }

            #endregion Create Serilog logger


            #region Log environment variable replacements

            LogEnvironmentVariableReplacements(
                sourceResult.ReplacedEnvironmentVariables,
                consoleOptions.RevealSensitiveData,
                serilogLogger);

            serilogLogger.Verbose("The following plain configuration, including data from resolved environment variables, was loaded:\n{RayMigratorConfigurationSection}",
                sourceResult.RayMigratorConfigSection.ToDetailString(consoleOptions.RevealSensitiveData));

            #endregion Log environment variable replacements


            #region Create host and DI container

            IHost host;
            RayMigratorOptions rayMigratorOptions;
            MigrationContext ctx;

            try
            {
                var hostBuilder = Host.CreateDefaultBuilder(args);

                // JSON mode: add configuration to the host builder
                if (sourceResult.HostConfiguration != null)
                {
                    hostBuilder.ConfigureAppConfiguration((context, builder) =>
                    {
                        builder.AddConfiguration(sourceResult.HostConfiguration);
                    });
                }

                host = hostBuilder
                    .UseSerilog()
                    .ConfigureServices((context, services) =>
                    {
                        // Register RayMigratorOptions differently based on source
                        if (sourceResult.PreBuiltOptions != null)
                        {
                            // Admin-DB mode: IValidateOptions is NOT invoked here — pre-built Options from
                            // AdminDbOptionsSource are trusted. If future Studio needs validation, run
                            // RayMigratorOptionsValidator.Validate(null, preBuilt) explicitly at that layer.
                            // Enum-typed values are the exception: the *Enum getters throw on an unparseable
                            // value (#3), so report those at startup instead of mid-run on first use.
                            ProductDefaultsPostConfigureOptions.EnsureEnumValuesAreParsable(sourceResult.PreBuiltOptions);
                            services.AddSingleton(Options.Create(sourceResult.PreBuiltOptions));
                        }
                        else
                        {
                            // JSON mode: bind from configuration section with validation
                            services.AddOptions<RayMigratorOptions>()
                                .Configure(options => sourceResult.RayMigratorConfigSection.Bind(options))
                                .ValidateDataAnnotations()
                                .ValidateOnStart();

                            services.AddTransient<IPostConfigureOptions<RayMigratorOptions>, ProductDefaultsPostConfigureOptions>();

                            // The validator reads the connection strings from the section as written, so that
                            // RULE_7_3 sees the {ENV:...} placeholder and not the resolved credential (#30).
                            services.AddSingleton<IValidateOptions<RayMigratorOptions>>(
                                new RayMigratorOptionsValidator(sourceResult.RayMigratorConfigSectionAsWritten));
                        }

                        services.AddSingleton(consoleOptions);

                        // Register DatabaseLogWriter with DalFactory
                        services.AddSingleton<DatabaseLogWriter>(serviceProvider =>
                        {
                            try
                            {
                                var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;

                                if (consoleOptions.ShowStartupInfo)
                                {
                                    Console.WriteLine();
                                    Console.WriteLine(assemblyInfo);
                                }

                                return CreateDatabaseLogWriter(opts, consoleOptions);
                            }
                            catch (Exception ex) when (ex is not ApplicationStartupException)
                            {
                                throw new ConfigurationValidationException("Unable to retrieve a valid RayMigrator configuration.", ex);
                            }
                        });

                        // Register Service Layer (includes IMigrationContextAccessor and IMigrationContextFactory)
                        services.AddRayMigratorServices(RayMigratorHostMode.Cli);

                        // Register TemplateCache
                        services.AddSingleton<TemplateCache>(serviceProvider =>
                        {
                            var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
                            var tcLogger = serviceProvider.GetRequiredService<ILogger<TemplateCache>>();
                            var templateCache = new TemplateCache(consoleOptions.RevealSensitiveData, tcLogger);
                            templateCache.ValidateConfigurationAgainstTemplateCache(opts);
                            return templateCache;
                        });

                        services.AddSingleton<TemplateExecutor>();
                        services.AddScoped<RayMigratorService>();

                        // Create the MigrationContext through the factory: it fills the DAL properties, checks the
                        // schema names and makes the context current (accessor and logging context)
                        services.AddSingleton<MigrationContext>(serviceProvider =>
                        {
                            var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
                            var factory = serviceProvider.GetRequiredService<IMigrationContextFactory>();
                            return factory.CreateFromValidatedOptions(opts, consoleOptions);
                        });
                    })
                    .Build();
            }
            catch (Exception ex)
            {
                throw new ApplicationStartupException($"Could not create host application ({sourceResult.ModeName})", ex);
            }

            serilogLogger.Debug("Host application successfully created ({ModeName})", sourceResult.ModeName);

            #endregion Create host and DI container


            // Resolve DatabaseLogWriter first — its factory triggers options validation (JSON mode)
            var dbLogWriter = host.Services.GetRequiredService<DatabaseLogWriter>();

            // Resolve the validated options
            rayMigratorOptions = host.Services.GetRequiredService<IOptions<RayMigratorOptions>>().Value;

            // Register sensitive configuration values for masking in TRACE logs
            RegisterSensitiveData(rayMigratorOptions);


            // Resolving the context runs the factory: product lookup, DAL properties, schema names, accessor and logging context
            ctx = host.Services.GetRequiredService<MigrationContext>();

            // Resolve standard ILogger from DI (now backed purely by Serilog)
            logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DirectModePipeline));
            logger.LogDebug("Logger successfully created ({ModeName}). DatabaseLogWriter not yet initialized.", sourceResult.ModeName);


            #region Initialize DatabaseLogWriter

            string loggingDatabaseType = string.Empty;
            try
            {
                if (rayMigratorOptions.DatabaseLogging != null)
                {
                    loggingDatabaseType = rayMigratorOptions.DatabaseLogging!.DatabaseType!;
                    var templateCache = host.Services.GetRequiredService<TemplateCache>();
                    var templateExecutor = host.Services.GetRequiredService<TemplateExecutor>();

                    bool hasCreatedRepository = dbLogWriter.InitDatabaseLogger(loggingDatabaseType, templateCache, templateExecutor);
                    string initInfoAddon = hasCreatedRepository
                        ? "Infrastructure for database-logging successfully created."
                        : "Infrastructure for database-logging already exists.";
                    logger.LogDebug(MigrationEvent.CreateDatabaseLogger, "DatabaseLogWriter initialized. {InitInfo}", initInfoAddon);

                    // Wire up the database sink with the now-initialized writer
                    if (databaseSink != null)
                    {
                        databaseSink.SetWriter(dbLogWriter);
                    }

                    logger.LogDebug(MigrationEvent.CreateDatabaseLogger, "DatabaseLogWriter uses DAL of type [{LoggingDatabaseType}]", loggingDatabaseType);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationStartupException(
                    $"DatabaseLogWriter could not be initialized for logging into a database of type [{loggingDatabaseType}]", ex);
            }

            #endregion Initialize DatabaseLogWriter


            #region Validate ConnectionStrings and DB-Connections

            logger.LogDebug(MigrationEvent.ValidateConnectionStrings, "Validating ConnectionStrings and DB-Connections...");
            ConnectionValidator.ValidateTargetConnections(ctx, logger);

            #endregion Validate ConnectionStrings and DB-Connections


            #region Start RayMigratorService

            logger.LogDebug(MigrationEvent.RayMigratorServiceStart, "Instantiating RayMigratorService from host...");
            var rayMigratorService = host.Services.GetRequiredService<RayMigratorService>();

            logger.LogDebug(MigrationEvent.RayMigratorServiceStart, "RayMigrator starts executing ({ModeName})", sourceResult.ModeName);
            exitCode = await rayMigratorService.DoWorkAsync(host);

            #endregion Start RayMigratorService


            #region Stop RayMigratorService

            dbLogWriter.Flush();
            await host.StopAsync();
            logger.LogDebug(MigrationEvent.RayMigratorServiceShutdown, "RayMigrator finished with exit code {ExitCode}", exitCode);

            #endregion Stop RayMigratorService
        }
        catch (ApplicationStartupException ex)
        {
            LogFatalError(ex, ApplicationStartupException.AbortMessage, logger, serilogLogger, isSerilogLoggerActivated);
            exitCode = 1;
            return exitCode;
        }
        catch (Exception ex)
        {
            LogFatalError(ex, "RayMigrator aborted with error(s)", logger, serilogLogger, isSerilogLoggerActivated,
                MigrationEvent.RayMigratorServiceShutdown);
            exitCode = 100;
            return exitCode;
        }
        finally
        {
            try
            {
                await Log.CloseAndFlushAsync();
            }
            catch
            {
                // ignored
            }
        }

        return exitCode;
    }

    /// <summary>
    /// Builds the message for a loaded configuration without a <c>Serilog</c> section inside <c>RayMigrator</c> (exit
    /// code 4), without console output. The message names the section that is missing and lists the files that were
    /// loaded. The files of the hierarchy that were not found are named only when no loaded file carries a
    /// <c>RayMigrator</c> section at all, because then a wrong product or environment name is the likely cause; they
    /// are optional layers that are absent in most setups, so naming them for a merely missing <c>Serilog</c> section
    /// blamed the wrong thing (#31). A source without file diagnostics (Admin-DB mode) gets the bootstrap message.
    /// </summary>
    internal static string BuildMissingSerilogSectionMessage(
        bool hasRayMigratorSection,
        IReadOnlyList<(string Filename, bool Found)>? configFileDiagnostics,
        string product,
        string environment)
    {
        if (configFileDiagnostics == null)
        {
            return "No 'Serilog' configuration found within the 'RayMigrator' section of the bootstrap configuration." + Environment.NewLine +
                   "Please add a valid Serilog logger configuration to appsettings.json.";
        }

        var loadedFiles = configFileDiagnostics.Where(f => f.Found).Select(f => $"  - {f.Filename}").ToList();
        var missingFiles = configFileDiagnostics.Where(f => !f.Found).Select(f => $"  - {f.Filename}").ToList();
        List<string> lines = [];

        if (hasRayMigratorSection)
        {
            lines.Add("No 'Serilog' section found within the 'RayMigrator' section of the loaded configuration.");
            lines.Add($"Configuration files loaded for product [{product}], environment [{environment}]:");
            lines.AddRange(loadedFiles);
            lines.Add("Please add a Serilog logger configuration to the 'RayMigrator' section of one of these files.");
        }
        else
        {
            lines.Add($"No 'RayMigrator' section found in the configuration loaded for product [{product}], environment [{environment}].");
            lines.Add("Configuration files loaded:");
            lines.AddRange(loadedFiles);

            if (missingFiles.Count > 0)
            {
                lines.Add("Files of the hierarchy that were not found:");
                lines.AddRange(missingFiles);
                lines.Add("Please verify the product and environment names, or add a 'RayMigrator' section with a Serilog logger configuration to one of the loaded files.");
            }
            else
            {
                lines.Add("Please add a 'RayMigrator' section with a Serilog logger configuration to one of these files.");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Consolidated error logging that falls back gracefully through available loggers.
    /// Used by both ApplicationStartupException and general exception catch blocks.
    /// </summary>
    private static void LogFatalError(
        Exception ex,
        string message,
        Microsoft.Extensions.Logging.ILogger? logger,
        global::Serilog.ILogger? serilogLogger,
        bool isSerilogActive,
        EventId? eventId = null)
    {
        if (logger != null)
        {
            if (eventId.HasValue)
                logger.LogCritical(eventId.Value, ex, message);
            else
                logger.LogCritical(ex, message);
        }
        else if (isSerilogActive && serilogLogger != null)
        {
            serilogLogger.Fatal(ex, message);
        }
        else
        {
            Console.WriteLine($"\n*** ERROR (RayMigrator) *** [{DateTime.Now.ToString("u")}]\n{message}: " + ex.GetExceptionDetails());
        }
    }

    /// <summary>
    /// Logs environment variable replacements and validates they all resolved successfully.
    /// </summary>
    private static void LogEnvironmentVariableReplacements(
        List<EnvironmentVariableWithMetadata> replacedEnvironmentVariables,
        bool revealSensitiveData,
        global::Serilog.ILogger serilogLogger)
    {
        if (replacedEnvironmentVariables.Count == 0)
            return;

        bool isSuccessful = true;

        foreach (var replacedEnvVar in replacedEnvironmentVariables)
        {
            if (!string.IsNullOrWhiteSpace(replacedEnvVar.EnvironmentVariableValue))
            {
                if (revealSensitiveData)
                {
                    serilogLogger.Debug("Replaced setting [{ChildPath}] from environment-variable [{EnvVarName}] with value [{EnvVarValue}]",
                        replacedEnvVar.Path, replacedEnvVar.EnvironmentVariableName, replacedEnvVar.EnvironmentVariableValue);
                }
                else
                {
                    serilogLogger.Debug("Replaced setting [{ChildPath}] from environment-variable(s)", replacedEnvVar.Path);
                }
            }
            else
            {
                isSuccessful = false;
                serilogLogger.Error(
                    "Could not replace setting for configuration-path [{VariablePath}] from environment-variable [{EnvironmentVariableName}]: " +
                    "Environment-variable does not exist, is empty or contains whitespaces only",
                    replacedEnvVar.Path, replacedEnvVar.EnvironmentVariableName);
            }
        }

        if (!isSuccessful)
        {
            throw new ApplicationStartupException(
                "RayMigrator is shutting down due to error(s) replacing configuration variables from environment variables. " +
                "Check the previous errors for environment-variable replacements that need to be fixed.");
        }

        serilogLogger.Debug("[{ReplacedEnvironmentVariablesCount}] configuration values were successfully set from values of environment-variables",
            replacedEnvironmentVariables.Count);

        // Register resolved environment variable values for masking in TRACE logs
        SensitiveDataMasker.RegisterSensitiveValues(
            replacedEnvironmentVariables
                .Where(v => !string.IsNullOrWhiteSpace(v.EnvironmentVariableValue))
                .Select(v => v.EnvironmentVariableValue!));
    }

    /// <summary>
    /// Registers all sensitive configuration values from options for masking in TRACE logs.
    /// </summary>
    private static void RegisterSensitiveData(RayMigratorOptions rayMigratorOptions)
    {
        SensitiveDataMasker.RegisterSensitiveData(rayMigratorOptions);
    }

    /// <summary>
    /// Creates a DatabaseLogWriter, validating the connection if DatabaseLogging is configured.
    /// </summary>
    private static DatabaseLogWriter CreateDatabaseLogWriter(
        RayMigratorOptions rayMigratorOptions,
        RayMigratorConsoleOptions consoleOptions)
    {
        try
        {
            if (rayMigratorOptions.DatabaseLogging != null)
            {
                if (DalFactory.TryGetDal(rayMigratorOptions.DatabaseLogging!.DatabaseType!,
                        rayMigratorOptions.DatabaseLogging.ConnectionString!, out IDal? dalInstance))
                {
                    ConnectionValidator.ValidateDatabaseLoggerConnection(rayMigratorOptions, dalInstance!, consoleOptions);
                    return new DatabaseLogWriter(rayMigratorOptions, dalInstance);
                }

                throw new ApplicationStartupException("Unable to create a DAL instance for database logging.");
            }

            return new DatabaseLogWriter(rayMigratorOptions); // return a non-functional dummy for DI
        }
        catch (Exception ex) when (ex is not ApplicationStartupException)
        {
            throw new ApplicationStartupException("Could not add DatabaseLogWriter as a service to DI.", ex);
        }
    }

    /// <summary>
    /// Reports where a DAL's transient error codes come from (ADR-022): Information when a TransientErrorCodes.txt
    /// replaced the built-in list (the path only with RevealSensitiveData), Debug for the built-in list, and a Warning
    /// when the effective list is empty, because then no database error is retried.
    /// </summary>
    private static void LogTransientErrorCodes(DalBase dal, string databaseType, bool revealSensitiveData, Microsoft.Extensions.Logging.ILogger logger)
    {
        string codes = string.Join(", ", dal.TransientErrorCodes);
        // The file path reveals the installation folder; show it only when sensitive data may be revealed.
        string source = revealSensitiveData && dal.TransientErrorCodesSource != null
            ? dal.TransientErrorCodesSource
            : TransientErrorCodesFile.FileName;
        if (dal.TransientErrorCodesSource != null)
        {
            logger.LogInformation(MigrationEvent.InitializeDalSpecificProperties,
                "Transient error codes for DatabaseType [{DatabaseType}] loaded from [{Source}]: [{Codes}]",
                databaseType, source, codes);
        }
        else
        {
            logger.LogDebug(MigrationEvent.InitializeDalSpecificProperties,
                "Transient error codes for DatabaseType [{DatabaseType}] are the built-in list: [{Codes}]",
                databaseType, codes);
        }

        if (dal.TransientErrorCodes.Count == 0)
        {
            logger.LogWarning(MigrationEvent.InitializeDalSpecificProperties,
                "No transient error codes for DatabaseType [{DatabaseType}]: no database error is retried. Add codes to [{Source}] or delete the file to restore the built-in list",
                databaseType, source);
        }
    }
}
