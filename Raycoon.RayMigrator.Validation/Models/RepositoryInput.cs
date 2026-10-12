namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>Repository or DatabaseLogging section snapshot for validation.</summary>
public sealed class RepositoryInput
{
    public string? DatabaseType { get; init; }

    /// <summary>
    /// The connection string as configured, with <c>{ENV:...}</c> placeholders unresolved. The credential check
    /// (RULE_7_3) reads this value: a credential that enters through a placeholder is not hardcoded (#30).
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// The connection string after placeholder resolution, set by a caller that resolves placeholders before
    /// validating (the engine). Null means the value equals <see cref="ConnectionString"/>; the Config Wizard
    /// validates files without an environment and leaves it null. Rules that compare connection strings
    /// (RULE_7_1, RULE_7_2) prefer this value, so that two placeholders naming the same database are detected.
    /// </summary>
    public string? EffectiveConnectionString { get; init; }

    public string? SchemaName { get; init; }
    public string? TableBaseName { get; init; }
}
