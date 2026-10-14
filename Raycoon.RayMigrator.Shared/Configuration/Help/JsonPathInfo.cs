namespace Raycoon.RayMigrator.Shared.Configuration.Help;

/// <summary>
/// Language-independent location of a configuration field: its path relative to the <c>RayMigrator</c> section
/// (alias-keyed arrays as <c>[]</c>, for example <c>Products[].TargetGroups[].Alias</c>), the paths that inherit
/// from it and the path it inherits from.
/// </summary>
public record JsonPathInfo(
    string ConfigPath,
    IReadOnlyList<string>? InheritedByPaths = null,
    string? InheritedFromPath = null
);
