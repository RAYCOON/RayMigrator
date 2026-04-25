namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Section-level help displayed at the top of each wizard step.
/// </summary>
public record SectionHelp(string Title, string Description);

/// <summary>
/// Language-independent JSON configuration path metadata for a wizard field.
/// All paths are relative to the RayMigrator root section.
/// </summary>
public record JsonPathInfo(
    string ConfigPath,
    IReadOnlyList<string>? InheritedByPaths = null,
    string? InheritedFromPath = null
);

/// <summary>
/// Field-level help displayed in a modal dialog when the user clicks the help icon on a specific field.
/// </summary>
public record FieldHelp(
    string Title,
    string Description,
    string? Examples = null,
    string? ValidValues = null,
    string? DefaultValue = null,
    string? InheritanceNote = null,
    JsonPathInfo? JsonPath = null
);
