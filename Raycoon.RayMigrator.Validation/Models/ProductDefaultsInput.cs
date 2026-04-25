namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>
/// Defaults snapshot that cascade rules (RULE_8_x) compare products/target-groups against.
/// </summary>
public sealed class ProductDefaultsInput
{
    public string? MigrationErrorAction { get; init; }
    public string? RollbackErrorAction { get; init; }
    public string? MigrationFilesExtension { get; init; }
    public string? MigrationRollbackFilesPreExtension { get; init; }
    public string? UseCliToolAlias { get; init; }
    public string? TargetMigrationOrder { get; init; }
    public string? HashValidationScope { get; init; }
}
