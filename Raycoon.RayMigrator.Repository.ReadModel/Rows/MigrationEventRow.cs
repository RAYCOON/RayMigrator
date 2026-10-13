namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>The catalog of log events; <see cref="Id"/> is the <c>MigrationEvent</c> id of the engine.</summary>
public sealed record MigrationEventRow(int Id, string Name, string? Description);
