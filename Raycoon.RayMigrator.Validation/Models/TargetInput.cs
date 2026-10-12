namespace Raycoon.RayMigrator.Validation.Models;

public sealed class TargetInput
{
    public string? Alias { get; init; }

    /// <summary>
    /// The connection string as configured, with <c>{ENV:...}</c> placeholders unresolved. The credential check
    /// (RULE_7_3) reads this value: a credential that enters through a placeholder is not hardcoded (#30).
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// The connection string after placeholder resolution, set by a caller that resolves placeholders before
    /// validating (the engine). Null means the value equals <see cref="ConnectionString"/>. Rules that compare
    /// connection strings (RULE_7_1, RULE_7_2) prefer this value.
    /// </summary>
    public string? EffectiveConnectionString { get; init; }

    public string? UseCliToolAlias { get; init; }

    /// <summary>Resolved via Target -> TargetGroup -> Product -> Defaults cascade.</summary>
    public string? EffectiveUseCliToolAlias { get; init; }

    /// <summary>Parameter map as configured on the target (not merged with parents).</summary>
    public IReadOnlyDictionary<string, string>? CliToolParameters { get; init; }

    /// <summary>Effective parameter map after inheritance resolution (may include parent-contributed keys).</summary>
    public IReadOnlyDictionary<string, string>? EffectiveCliToolParameters { get; init; }
}
