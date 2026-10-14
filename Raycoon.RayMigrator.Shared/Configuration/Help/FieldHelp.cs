namespace Raycoon.RayMigrator.Shared.Configuration.Help;

/// <summary>
/// Help for one configuration field in the requested language: title, description, examples, the valid values,
/// the default, a note on inheritance and the field's location in the configuration.
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
