namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>An environment a product was migrated in; <see cref="NameLower"/> is the unique key.</summary>
public sealed record EnvironmentRow(int Id, string Name, string NameLower, DateTime CreatedAt);
