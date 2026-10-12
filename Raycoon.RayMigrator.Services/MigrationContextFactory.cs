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

        RayMigratorOptionsPreparer.Prepare(options);

        return Activate(new MigrationContext(options, consoleOptions, AssemblyInfoHelper.GetRayMigratorVersion()));
    }

    /// <inheritdoc/>
    public MigrationContext CreateFromValidatedOptions(RayMigratorOptions options, RayMigratorConsoleOptions consoleOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(consoleOptions);

        SensitiveDataMasker.RegisterSensitiveData(options);

        return Activate(new MigrationContext(options, consoleOptions, AssemblyInfoHelper.GetRayMigratorVersion()));
    }

    private MigrationContext Activate(MigrationContext ctx)
    {
        var options = ctx.RayMigratorOptions;

        _logger.LogDebug(MigrationEvent.InitializeDalSpecificProperties, "Initializing DalSpecificPropertiesDictionary...");

        SetDalSpecificProperties(ctx, options.Repository!.DatabaseType!, options.Repository.ConnectionString!);

        foreach (TargetGroupOptions targetGroupOptions in ctx.ProductTargetGroupOptionsEnumerable ?? [])
        {
            foreach (var targetOptions in targetGroupOptions.Targets ?? [])
            {
                SetDalSpecificProperties(ctx, targetGroupOptions.DatabaseType!, targetOptions.ConnectionString!);
            }
        }

        if (options.DatabaseLogging?.DatabaseType != null)
        {
            SetDalSpecificProperties(ctx, options.DatabaseLogging.DatabaseType, options.DatabaseLogging.ConnectionString!);
        }

        SchemaNameValidator.ValidateSchemaName(
            ctx.DalSpecificPropertiesDictionary,
            options.Repository.DatabaseType!,
            options.Repository.SchemaName,
            "Repository", _logger);

        if (options.DatabaseLogging?.DatabaseType != null)
        {
            SchemaNameValidator.ValidateSchemaName(
                ctx.DalSpecificPropertiesDictionary,
                options.DatabaseLogging.DatabaseType,
                options.DatabaseLogging.SchemaName,
                "DatabaseLogging", _logger);
        }

        _ctxAccessor.Current = ctx;
        MigrationLoggingContext.Current = ctx;

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
        }
    }
}
