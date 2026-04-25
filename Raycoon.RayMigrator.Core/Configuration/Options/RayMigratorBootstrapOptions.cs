
namespace Raycoon.RayMigrator.Core.Configuration.Options;

/// <summary>
/// Bootstrap configuration for RayMigrator. Contains only the minimal settings needed
/// to initialize the Admin-DB and configure logging.
/// This is loaded from appsettings.json before any migration-specific configuration.
/// </summary>
public class RayMigratorBootstrapOptions
{
    /// <summary>
    /// Admin database configuration.
    /// When set, RayMigrator reads Products/Environments/Targets from the Admin-DB.
    /// </summary>
    public AdminDbOptions? AdminDb { get; set; }

    /// <summary>
    /// Serilog configuration marker. The actual Serilog settings are read from the
    /// RayMigrator section by Serilog's own configuration reader.
    /// </summary>
    public SerilogOptions? Serilog { get; set; }
}

/// <summary>
/// Configuration for the Admin database connection.
/// </summary>
public class AdminDbOptions
{
    /// <summary>
    /// Database provider for the Admin-DB.
    /// Supported values: "Sqlite", "SqlServer", "PostgreSQL", "MariaDb", "MySql"
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Connection string for the Admin-DB. Supports {ENV:VARIABLE_NAME} placeholder syntax.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Optional schema name for non-SQLite providers (e.g. "admin" for PostgreSQL/SqlServer).
    /// </summary>
    public string? SchemaName { get; set; }
}
