namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Represents a single resolved configuration entry with its effective value and source.
/// </summary>
public class EffectiveConfigEntry
{
    public string Property { get; }
    public string Value { get; }
    public string Source { get; }

    public EffectiveConfigEntry(string property, string value, string source)
    {
        Property = property;
        Value = value;
        Source = source;
    }
}
