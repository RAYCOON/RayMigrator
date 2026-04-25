namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class DatabaseLoggingModel
{
    public string DatabaseType { get; set; } = "SqlServer";
    public string ConnectionString { get; set; } = "";
    public string SchemaName { get; set; } = "ray";
    public string TableBaseName { get; set; } = "";
    public string MinimumLevel { get; set; } = "Information";
    public int DbCommandTimeoutInSeconds { get; set; } = 20;
}
