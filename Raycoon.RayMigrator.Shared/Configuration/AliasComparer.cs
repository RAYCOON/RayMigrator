namespace Raycoon.RayMigrator.Shared.Configuration;

/// <summary>
/// The one definition of alias equality in RayMigrator: ordinal, case-insensitive.
/// Used by the configuration merger and by every alias lookup that is written or touched from now on,
/// so that the engine, RayMigrator Studio and the Config Wizard cannot drift apart (#23).
/// </summary>
public static class AliasComparer
{
    /// <summary>Comparison mode for <see cref="string.Equals(string?, string?, StringComparison)"/> calls.</summary>
    public const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

    /// <summary>Comparer instance for dictionaries, hash sets and LINQ set operations keyed by alias.</summary>
    public static StringComparer Instance => StringComparer.OrdinalIgnoreCase;

    /// <summary>Returns true when both aliases are equal under <see cref="Comparison"/>. Two nulls are equal.</summary>
    public static bool AliasEquals(string? first, string? second) => string.Equals(first, second, Comparison);
}
