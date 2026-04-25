namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class TargetDefaultsModel
{
    public int DbCommandTimeoutInSeconds { get; set; } = 20;
    public int DbCommandMaxRetries { get; set; }
    public int DbCommandWaitTimeInMsBeforeRetry { get; set; } = 250;
}
