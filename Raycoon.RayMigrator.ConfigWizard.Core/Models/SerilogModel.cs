namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class SerilogModel
{
    public string MinimumLevelDefault { get; set; } = "Information";
    public Dictionary<string, string> MinimumLevelOverrides { get; set; } = new();
    public List<SerilogSinkModel> WriteTo { get; set; } = new();
}

public class SerilogSinkModel
{
    public string Name { get; set; } = "Console";
    public Dictionary<string, string> Args { get; set; } = new();
}
