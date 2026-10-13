namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>The settings snapshot of a run (secrets masked) and its description.</summary>
public sealed record MigrationRunMetaRow(int MigrationRunId, string? MigrationRunSettingsJson, string? Description);
