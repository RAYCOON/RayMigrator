namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class RepositoryModel
{
    public string DatabaseType { get; set; } = "SqlServer";
    public string ConnectionString { get; set; } = "";
    public string SchemaName { get; set; } = "ray";
    public string TableBaseName { get; set; } = "";
    public int DbCommandTimeoutInSeconds { get; set; } = 60;
    public int DbCommandMaxRetries { get; set; } = 100;
    public int DbCommandWaitTimeInMsBeforeRetry { get; set; } = 250;
}
