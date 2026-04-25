namespace Raycoon.RayMigrator.Core.Configuration.Replacer;

public class EnvironmentVariableWithMetadata
{
    public string Path { get; set; } = null!;
    public string ConfigurationKey { get; set; } = null!;
    public string? ConfigurationValue { get; set; }
    public string? ConfigurationValueReplaced { get; set; }
    public string EnvironmentVariableName { get; set; } = null!;
    public string? EnvironmentVariableValue { get; set; }
}