
namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>
/// Neutral input DTO handed to the <see cref="RuleCatalog"/>.
/// Both Engine and ConfigWizard adapters flatten their native model onto this shape
/// with effective values (defaults-cascade already resolved) so rules never see that complexity.
/// </summary>
public sealed class ValidationInput
{
    public RepositoryInput? Repository { get; init; }
    public RepositoryInput? DatabaseLogging { get; init; }
    public IReadOnlyList<CliToolInput> CliTools { get; init; } = Array.Empty<CliToolInput>();
    public IReadOnlyList<ProductInput> Products { get; init; } = Array.Empty<ProductInput>();
    public ProductDefaultsInput Defaults { get; init; } = new();
}
