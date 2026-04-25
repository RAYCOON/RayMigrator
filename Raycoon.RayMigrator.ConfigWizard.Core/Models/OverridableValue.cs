namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Represents a value that can optionally override a default from the parent level.
/// </summary>
public class OverridableValue<T>
{
    public bool IsOverridden { get; set; }
    public T? Value { get; set; }

    public T GetEffectiveValue(T defaultValue) =>
        IsOverridden && Value is not null ? Value : defaultValue;
}
