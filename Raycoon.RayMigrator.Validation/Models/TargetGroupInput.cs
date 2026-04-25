namespace Raycoon.RayMigrator.Validation.Models;

public sealed class TargetGroupInput
{
    public string? Alias { get; init; }
    public string? DatabaseType { get; init; }
    public string? UseCliToolAlias { get; init; }

    /// <summary>Merged effective value after ProductDefaults.TargetGroupDefaults cascade.</summary>
    public string? EffectiveTargetMigrationOrder { get; init; }
    public string? EffectiveHashValidationScope { get; init; }

    public IReadOnlyList<TargetInput> Targets { get; init; } = Array.Empty<TargetInput>();
}
