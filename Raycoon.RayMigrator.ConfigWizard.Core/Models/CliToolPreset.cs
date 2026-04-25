namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Represents a pre-defined CLI tool configuration for a specific database engine.
/// </summary>
public class CliToolPreset
{
    public string Alias { get; set; } = "";

    /// <summary>Used internally for preset filtering. Not serialized to output config.</summary>
    public string DatabaseType { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string ArgumentTemplate { get; set; } = "";
    public string InputMode { get; set; } = "File";
    public List<string> SuccessExitCodes { get; set; } = new() { "0" };
    public int CliToolTimeoutInSeconds { get; set; } = 120;

    /// <summary>Description for the preset (displayed in UI).</summary>
    public string Description { get; set; } = "";

    /// <summary>Whether this is a Docker variant.</summary>
    public bool IsDockerVariant { get; set; }

    /// <summary>
    /// Default CliToolParameters keys this preset expects the Target to provide.
    /// Used to auto-scaffold CliToolParameters on targets.
    /// Example: ["Server", "User", "Password", "Database"]
    /// </summary>
    public List<string> ExpectedParameterKeys { get; set; } = new();
}
