using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// Class to validate ConnectionStrings and connections to Target-databases.
/// </summary>
/// <remarks>This is a post-validation after all IValidateOptions of RayMigratorOptions have been performed.</remarks>
public static class ConnectionValidator
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="rayMigratorOptions"></param>
    /// <param name="dalInstance"></param>
    /// <param name="rayMigratorConsoleOptions"></param>
    /// <exception cref="ApplicationStartupException"></exception>
    public static void ValidateDatabaseLoggerConnection(RayMigratorOptions rayMigratorOptions, IDal dalInstance, RayMigratorConsoleOptions rayMigratorConsoleOptions)
    {
        try
        {
            bool tryEstablishConnection = rayMigratorConsoleOptions.RunMode == MigrationRunMode.Migrate;
            dalInstance!.CheckConnectionStringOrValidateConnection(tryEstablishConnection);
        }
        catch (Exception ex)
        {
            if (rayMigratorConsoleOptions.RevealSensitiveData)
            {
                throw new ApplicationStartupException($"Failed to validate or connect with the following ConnectionString for DatabaseLogging: [{rayMigratorOptions.DatabaseLogging!.ConnectionString!}]: " + ex.GetExceptionDetails());
            }

            throw new ApplicationStartupException("Failed to validate or connect with ConnectionString for DatabaseLogging: " + ex.GetExceptionDetails());
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="logger"></param>
    /// <exception cref="ApplicationStartupException"></exception>
    public static void ValidateTargetConnections(MigrationContext ctx, ILogger logger)
    {
        foreach (TargetGroupOptions targetGroupOptions in ctx.ProductTargetGroupOptionsEnumerable!)
        {
            foreach (TargetOptions targetOptions in targetGroupOptions.Targets!)
            {
                if (DalFactory.TryGetDal(targetGroupOptions.DatabaseType!, targetOptions.ConnectionString!, out var dalInstance))
                {
                    try
                    {
                        bool tryEstablishConnection = (ctx.RayMigratorConsoleOptions.RunMode == MigrationRunMode.Migrate || ctx.RayMigratorConsoleOptions.RunMode == MigrationRunMode.Simulate);

                        logger.LogDebug(MigrationEvent.ValidateConnectionStrings,
                            "Validating [{DatabaseType}]-ConnectionString for TargetGroup [{TargetGroupAlias}] and Target [{TargetAlias}]",
                            targetGroupOptions.DatabaseType, targetGroupOptions.Alias, targetOptions.Alias);

                        if (tryEstablishConnection)
                        {
                            logger.LogDebug(MigrationEvent.ValidateConnectionStrings,
                                "Trying to establish connection for TargetGroup [{TargetGroupAlias}] Target [{TargetAlias}]",
                                targetGroupOptions.Alias, targetOptions.Alias);
                        }

                        if (ctx.RayMigratorConsoleOptions.RevealSensitiveData)
                        {
                            logger.LogDebug(MigrationEvent.ValidateConnectionStrings,
                                "ConnectionString for TargetGroup [{TargetGroupAlias}] Target [{TargetAlias}]: [{ConnectionString}]",
                                targetGroupOptions.Alias, targetOptions.Alias, targetOptions.ConnectionString!);
                        }

                        dalInstance!.CheckConnectionStringOrValidateConnection(tryEstablishConnection);
                    }
                    catch (Exception ex)
                    {
                        if (ctx.RayMigratorConsoleOptions.RevealSensitiveData)
                        {
                            throw new ApplicationStartupException($"Failed to validate or connect to DatabaseType [{targetGroupOptions.DatabaseType!}] using the following ConnectionString for a Target in TargetGroup with Alias [{targetGroupOptions.Alias}]: [{targetOptions.ConnectionString!}]: " + ex.GetExceptionDetails());
                        }

                        throw new ApplicationStartupException($"Failed to validate or connect to DatabaseType [{targetGroupOptions.DatabaseType!}] using the provided ConnectionString for a Target in TargetGroup with Alias [{targetGroupOptions.Alias}]: " + ex.GetExceptionDetails());
                    }
                }
                else
                {
                    throw new ApplicationStartupException($"Could not get Data Access Layer of DatabaseType [{targetGroupOptions.DatabaseType!}] in TargetGroup with Alias [{targetGroupOptions.Alias}]");
                }
            }
        }
    }
}