namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>A product (the alias of the configuration); <see cref="NameLower"/> is the unique key.</summary>
public sealed record ProductRow(int Id, string Name, string NameLower, DateTime CreatedAt);
