using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Core.Logging;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Services;

/// <summary>
/// Default <see cref="IMigrationContextFactory"/>. Registered by <see cref="ServiceCollectionExtensions.AddRayMigratorServices"/>.
/// </summary>
public sealed class MigrationContextFactory : IMigrationContextFactory
{
    private readonly IMigrationContextAccessor _ctxAccessor;
    private readonly ILogger<MigrationContextFactory> _logger;

    /// <summary>Creates the factory. Every context it creates becomes current through <paramref name="ctxAccessor"/>.</summary>
    public MigrationContextFactory(IMigrationContextAccessor ctxAccessor, ILogger<MigrationContextFactory> logger)
    {
        _ctxAccessor = ctxAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public MigrationContext Create(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(consoleOptions);

        // The run sees its own product only (ADR-026): the scope is applied to a copy of the options with a list of
        // its own, so a host that creates a context per product from the same options keeps them intact; the
        // sections and the product objects stay shared, the preparer writes into them idempotently.
        var scoped = WithOwnProductList(options);
        RayMigratorOptionsPreparer.IsolateProduct(scoped, consoleOptions.Product);
        RayMigratorOptionsPreparer.Prepare(scoped);

        return Activate(new MigrationContext(scoped, consoleOptions, AssemblyInfoHelper.GetRayMigratorVersion()));
    }

    /// <inheritdoc/>
    public MigrationContext CreateFromValidatedOptions(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(consoleOptions);

        SensitiveDataMasker.RegisterSensitiveData(options);

        return Activate(new MigrationContext(options, consoleOptions, AssemblyInfoHelper.GetRayMigratorVersion()));
    }

    /// <summary>A shallow copy of the options whose <see cref="RayMigratorOptions.Products"/> is a list of its own.</summary>
    private static RayMigratorOptions WithOwnProductList(RayMigratorOptions options) => new()
    {
        Repository = options.Repository,
        DatabaseLogging = options.DatabaseLogging,
        Serilog = options.Serilog,
        ProductDefaults = options.ProductDefaults,
        Products = options.Products?.ToList(),
        CliTools = options.CliTools
    };

    private MigrationContext Activate(MigrationContext ctx)
    {
        var options = ctx.RayMigratorOptions;

        // Current first: the enricher then stamps the log lines below with the run properties of this context
        _ctxAccessor.Current = ctx;
        MigrationLoggingContext.Current = ctx;

        _logger.LogDebug(MigrationEvent.InitializeDalSpecificProperties, "Initializing DalSpecificPropertiesDictionary...");

        SetDalSpecificProperties(ctx, options.Repository!.DatabaseType!, options.Repository.ConnectionString!);

        foreach (TargetGroupOptions targetGroupOptions in ctx.ProductTargetGroupOptionsEnumerable ?? [])
        {
            foreach (var targetOptions in targetGroupOptions.Targets ?? [])
            {
                SetDalSpecificProperties(ctx, targetGroupOptions.DatabaseType!, targetOptions.ConnectionString!);
            }
        }

        if (options.IsDatabaseLoggingEnabled() && options.DatabaseLogging?.DatabaseType != null)
        {
            SetDalSpecificProperties(ctx, options.DatabaseLogging.DatabaseType, options.DatabaseLogging.ConnectionString!);
        }

        SchemaNameValidator.ValidateSchemaName(
            ctx.DalSpecificPropertiesDictionary,
            options.Repository.DatabaseType!,
            options.Repository.SchemaName,
            "Repository", _logger);

        if (options.IsDatabaseLoggingEnabled() && options.DatabaseLogging?.DatabaseType != null)
        {
            SchemaNameValidator.ValidateSchemaName(
                ctx.DalSpecificPropertiesDictionary,
                options.DatabaseLogging.DatabaseType,
                options.DatabaseLogging.SchemaName,
                "DatabaseLogging", _logger);
        }

        return ctx;
    }

    private void SetDalSpecificProperties(MigrationContext ctx, string databaseType, string connectionString)
    {
        if (!DalFactory.TryGetDal(databaseType, connectionString, out IDal? dalInstance))
        {
            throw new ConfigurationValidationException($"Could not retrieve data access layer for DatabaseType [{databaseType}]");
        }

        bool added = ctx.DalSpecificPropertiesDictionary.TryAdd(databaseType, dalInstance!.DalSpecificProperties);
        if (added)
        {
            _logger.LogDebug(MigrationEvent.InitializeDalSpecificProperties,
                "DalSpecificProperties added for DatabaseType [{DatabaseType}] with MultiLineCommentStart: [{CommentStart}], MultiLineCommentEnd: [{CommentEnd}], SqlScriptDelimiter: [{Delimiter}], SupportsSchema: [{SupportsSchema}], SupportsTransactionalDdl: [{SupportsTransactionalDdl}]",
                databaseType,
                dalInstance.DalSpecificProperties.SqlMultiLineCommentStart,
                dalInstance.DalSpecificProperties.SqlMultiLineCommentEnd,
                string.IsNullOrWhiteSpace(dalInstance.DalSpecificProperties.SqlBlockDelimiter) ? "{Empty}" : dalInstance.DalSpecificProperties.SqlBlockDelimiter,
                dalInstance.DalSpecificProperties.SupportsSchema,
                dalInstance.DalSpecificProperties.SupportsTransactionalDdl);

            if (dalInstance is DalBase dalBase)
            {
                LogTransientErrorCodes(dalBase, databaseType, ctx.RayMigratorConsoleOptions.RevealSensitiveData);
            }
        }
    }

    /// <summary>
    /// Reports where a DAL's transient error codes come from (ADR-022): Information when a TransientErrorCodes.txt
    /// replaced the built-in list (the path only with RevealSensitiveData), Debug for the built-in list, and a Warning
    /// when the effective list is empty, because then no database error is retried.
    /// </summary>
    private void LogTransientErrorCodes(DalBase dal, string databaseType, bool revealSensitiveData)
    {
        string codes = string.Join(", ", dal.TransientErrorCodes);
        // The file path reveals the installation folder; show it only when sensitive data may be revealed.
        string source = revealSensitiveData && dal.TransientErrorCodesSource != null
            ? dal.TransientErrorCodesSource
            : TransientErrorCodesFile.FileName;
        if (dal.TransientErrorCodesSource != null)
        {
            _logger.LogInformation(MigrationEvent.InitializeDalSpecificProperties,
                "Transient error codes for DatabaseType [{DatabaseType}] loaded from [{Source}]: [{Codes}]",
                databaseType, source, codes);
        }
        else
        {
            _logger.LogDebug(MigrationEvent.InitializeDalSpecificProperties,
                "Transient error codes for DatabaseType [{DatabaseType}] are the built-in list: [{Codes}]",
                databaseType, codes);
        }

        if (dal.TransientErrorCodes.Count == 0)
        {
            _logger.LogWarning(MigrationEvent.InitializeDalSpecificProperties,
                "No transient error codes for DatabaseType [{DatabaseType}]: no database error is retried. Add codes to [{Source}] or delete the file to restore the built-in list",
                databaseType, source);
        }
    }
}
