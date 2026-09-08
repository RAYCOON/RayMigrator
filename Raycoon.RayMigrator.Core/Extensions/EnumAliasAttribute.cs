namespace Raycoon.RayMigrator.Core.Extensions;

/// <summary>
/// A former name of an enum member that configuration files may still use. <c>OptionsEnumParser</c> and
/// <c>RayEnumAttribute</c> accept an alias (case-insensitively) and resolve it to the member; the allowed-value
/// lists and every value the engine writes (settings snapshots, log output) use the member name only (#19).
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class EnumAliasAttribute : Attribute
{
    public EnumAliasAttribute(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("An enum alias must not be empty.", nameof(alias));
        Alias = alias;
    }

    public string Alias { get; }
}
