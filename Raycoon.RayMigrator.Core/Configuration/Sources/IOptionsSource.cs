namespace Raycoon.RayMigrator.Core.Configuration.Sources;

/// <summary>
/// Abstraction for loading RayMigrator configuration for the CLI pipeline. The one implementation reads the
/// appsettings.json hierarchy (<c>JsonOptionsSource</c>); a host that builds options in memory goes through
/// <c>IMigrationContextFactory</c> instead.
/// </summary>
public interface IOptionsSource
{
    /// <summary>
    /// Loads configuration for the specified product and environment. The result holds the shared sections and
    /// the product only (ADR-026); the other products of the files are not loaded.
    /// </summary>
    /// <param name="product">The product alias to load configuration for.</param>
    /// <param name="environment">The environment name (e.g. "Docker", "Production").</param>
    /// <returns>An <see cref="OptionsSourceResult"/> containing all data needed by the execution pipeline.</returns>
    Task<OptionsSourceResult> LoadAsync(string product, string environment);
}
