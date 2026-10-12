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

        // Register context accessor based on host mode. Both are singletons: the CLI holds one context for the
        // whole process, and the API accessor isolates per async flow through a static AsyncLocal, so a scoped
        // registration would isolate nothing and would only make the singleton factory a captive dependency.
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

        return services;
    }
}
