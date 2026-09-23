namespace Raycoon.RayMigrator.Shared.Configuration;

/// <summary>
/// Role of a configuration file in the appsettings hierarchy, in merge order (a higher value overrides a lower one).
/// Shared by the engine's <c>JsonOptionsSource</c> and the Config Wizard (#23).
/// </summary>
public enum ConfigFileRole
{
    /// <summary>appsettings.json - base configuration</summary>
    Base = 1,

    /// <summary>appsettings.{Environment}.json - environment-specific overrides</summary>
    Environment = 2,

    /// <summary>appsettings.{Product}.json - product-specific overrides</summary>
    Product = 3,

    /// <summary>appsettings.{Product}.{Environment}.json - product+environment overrides</summary>
    ProductEnvironment = 4
}
