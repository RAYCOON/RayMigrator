namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>
/// A row of the lookup tables <c>MigrationRunMode</c>, <c>MigrationOperation</c>, <c>MigrationRunResult</c>,
/// <c>MigrationStatus</c> and <c>MigrationRunSource</c>; <see cref="Id"/> is the value of the enum of the same name in
/// <c>Raycoon.RayMigrator.Core.Configuration.Enums</c>.
/// </summary>
public sealed record LookupRow(byte Id, string Name, string? Description);
