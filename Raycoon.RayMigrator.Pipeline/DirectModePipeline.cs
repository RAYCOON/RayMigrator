
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

            // Check for Serilog configuration with helpful diagnostics
            if (!sourceResult.RayMigratorConfigSection.GetSection("Serilog").Exists())
            {
                Console.WriteLine(assemblyInfo);
                Console.ForegroundColor = ConsoleColor.Red;

                if (sourceResult.ConfigFileDiagnostics != null)
                {
                    var notFoundFiles = sourceResult.ConfigFileDiagnostics
                        .Where(f => !f.Found).Select(f => f.Filename).ToList();

                    if (notFoundFiles.Count > 0)
                    {
                        Console.WriteLine($"No valid RayMigrator configuration found for product [{consoleOptions.Product}], environment [{environment}].");
                        Console.WriteLine("The following expected configuration files were not found:");
                        foreach (var filename in notFoundFiles)
                            Console.WriteLine($"  - {filename}");
                        Console.WriteLine("Please verify the product and environment names and ensure the corresponding configuration files exist.");
                    }
                    else
                    {
                        Console.WriteLine("No 'Serilog' configuration found within the 'RayMigrator' section.");
                        Console.WriteLine("Please add a valid Serilog logger configuration to the RayMigrator configuration.");
                    }
                }
                else
                {
                    Console.WriteLine("No 'Serilog' configuration found within the 'RayMigrator' section of the bootstrap configuration.");
                    Console.WriteLine("Please add a valid Serilog logger configuration to appsettings.json.");
                }

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
                            services.AddSingleton<IValidateOptions<RayMigratorOptions>, RayMigratorOptionsValidator>();
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
                            var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>();
                            var tcLogger = serviceProvider.GetRequiredService<ILogger<TemplateCache>>();
                            return new TemplateCache(opts, consoleOptions.RevealSensitiveData, tcLogger);
                        });

                        services.AddSingleton<TemplateExecutor>();
                        services.AddScoped<RayMigratorService>();

                        // Create MigrationContext and set it on the singleton accessor
                        services.AddSingleton<MigrationContext>(serviceProvider =>
                        {
                            var opts = serviceProvider.GetRequiredService<IOptions<RayMigratorOptions>>().Value;
                            string rayMigratorVersion = Raycoon.RayMigrator.Shared.AssemblyInfoHelper.GetRayMigratorVersion();
                            var migCtx = new MigrationContext(opts, consoleOptions, rayMigratorVersion);

                            var accessor = serviceProvider.GetRequiredService<IMigrationContextAccessor>();
                            accessor.Current = migCtx;

                            return migCtx;
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


            #region Validate product alias

            var configuredProducts = rayMigratorOptions.Products ?? Enumerable.Empty<ProductOptions>();
            var matchingProduct = configuredProducts.FirstOrDefault(p => p.Alias == consoleOptions.Product);

            if (matchingProduct == null)
            {
                var availableAliases = configuredProducts
                    .Where(p => !string.IsNullOrWhiteSpace(p.Alias))
                    .Select(p => p.Alias!)
                    .ToList();

                string message = $"Product alias [{consoleOptions.Product}] not found in the loaded configuration.";

                var caseInsensitiveMatch = availableAliases
                    .FirstOrDefault(a => string.Equals(a, consoleOptions.Product, StringComparison.OrdinalIgnoreCase));

                if (caseInsensitiveMatch != null)
                {
                    message += $"\nDid you mean [{caseInsensitiveMatch}]? The --product parameter is case-sensitive.";
                }
                else if (availableAliases.Count > 0)
                {
                    message += $"\nAvailable product aliases: {string.Join(", ", availableAliases)}";
                    message += "\nNote: The --product and --environment parameters are case-sensitive.";
                }
                else
                {
                    message += "\nNo products are configured in the loaded configuration files.";
                    message += "\nNote: The --product and --environment parameters are case-sensitive.";
                }

                throw new ConfigurationValidationException(message);
            }

            #endregion Validate product alias


            ctx = host.Services.GetRequiredService<MigrationContext>();

            // Set the ambient MigrationLoggingContext so the enricher can add migration properties to all log events
            MigrationLoggingContext.Current = ctx;

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


            #region Initialize DalSpecificPropertiesDictionary

            logger.LogDebug(MigrationEvent.InitializeDalSpecificProperties, "Initializing DalSpecificPropertiesDictionary...");

            SetDalSpecificPropertiesDictionary(ctx, rayMigratorOptions.Repository!.DatabaseType!, rayMigratorOptions.Repository!.ConnectionString!, logger);

            foreach (TargetGroupOptions targetGroupOptions in ctx.ProductTargetGroupOptionsEnumerable!)
            {
                foreach (var targetOptions in targetGroupOptions.Targets!)
                {
                    SetDalSpecificPropertiesDictionary(ctx, targetGroupOptions.DatabaseType!, targetOptions.ConnectionString!, logger);
                }
            }

            // Add DatabaseLogging's DatabaseType to DalSpecificPropertiesDictionary (if configured)
            if (rayMigratorOptions.DatabaseLogging?.DatabaseType != null)
            {
                SetDalSpecificPropertiesDictionary(ctx, rayMigratorOptions.DatabaseLogging.DatabaseType,
                    rayMigratorOptions.DatabaseLogging.ConnectionString!, logger);
            }

            // Validate SchemaName based on DAL's SupportsSchema flag
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

            #endregion Initialize DalSpecificPropertiesDictionary


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
    /// Sets the DalSpecificPropertiesDictionary from a configured DatabaseType.
    /// </summary>
    private static void SetDalSpecificPropertiesDictionary(
        MigrationContext migrationContext,
        string databaseType,
        string connectionString,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        if (!DalFactory.TryGetDal(databaseType, connectionString, out IDal? dalInstance))
        {
            throw new ConfigurationValidationException($"Could not retrieve data access layer for DatabaseType [{databaseType}]");
        }

        bool successfullyAdded = migrationContext.DalSpecificPropertiesDictionary.TryAdd(databaseType, dalInstance!.DalSpecificProperties);
        if (successfullyAdded)
        {
            logger.LogDebug(MigrationEvent.InitializeDalSpecificProperties,
                "DalSpecificProperties added for DatabaseType [{DatabaseType}] with MultiLineCommentStart: [{CommentStart}], MultiLineCommentEnd: [{CommentEnd}], SqlScriptDelimiter: [{Delimiter}], SupportsSchema: [{SupportsSchema}], SupportsTransactionalDdl: [{SupportsTransactionalDdl}]",
                databaseType,
                dalInstance.DalSpecificProperties.SqlMultiLineCommentStart,
                dalInstance.DalSpecificProperties.SqlMultiLineCommentEnd,
                string.IsNullOrWhiteSpace(dalInstance.DalSpecificProperties.SqlBlockDelimiter) ? "{Empty}" : dalInstance.DalSpecificProperties.SqlBlockDelimiter,
                dalInstance.DalSpecificProperties.SupportsSchema,
                dalInstance.DalSpecificProperties.SupportsTransactionalDdl);
        }
    }
}
