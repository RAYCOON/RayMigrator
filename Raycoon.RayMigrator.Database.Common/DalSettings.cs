namespace Raycoon.RayMigrator.Database.Common;

public class DalSettings : IDalSettings
{
    /// <inheritdoc />
    public bool UseTransaction { get; set; }

    /// <inheritdoc />
    public int DbCommandTimeoutInSeconds { get; set; }

    /// <inheritdoc />
    public int MaxRetries { get; set; } = 3;

    /// <inheritdoc />
    public int RetryDelayMs { get; set; } = 500;
}