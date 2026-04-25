namespace Raycoon.RayMigrator.Validation.Models;

public sealed class ProductInput
{
    public string? Alias { get; init; }
    public string? MigrationFilesRootDirectory { get; init; }
    public string? TargetGroupMigrationOrder { get; init; }
    public string? UseCliToolAlias { get; init; }

    /// <summary>Merged effective value after ProductDefaults cascade.</summary>
    public string? EffectiveMigrationErrorAction { get; init; }
    public string? EffectiveRollbackErrorAction { get; init; }
    public string? EffectiveMigrationFilesExtension { get; init; }
    public string? EffectiveRollbackPreExtension { get; init; }

    public IReadOnlyList<TargetGroupInput> TargetGroups { get; init; } = Array.Empty<TargetGroupInput>();
}
