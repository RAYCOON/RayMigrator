
namespace Raycoon.RayMigrator.Validation.Models;

/// <summary>Repository or DatabaseLogging section snapshot for validation.</summary>
public sealed class RepositoryInput
{
    public string? DatabaseType { get; init; }
    public string? ConnectionString { get; init; }
    public string? SchemaName { get; init; }
    public string? TableBaseName { get; init; }
}
