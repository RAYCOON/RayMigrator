
using Microsoft.Extensions.DependencyInjection;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Services.Abstractions;

namespace Raycoon.RayMigrator.Services;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all RayMigrator services with the specified host mode.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="hostMode">CLI or API hosting mode.</param>
    public static IServiceCollection AddRayMigratorServices(this IServiceCollection services, RayMigratorHostMode hostMode = RayMigratorHostMode.Cli)
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
