namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>One applied repository schema version (ADR-024); the highest <see cref="Version"/> is the repository's.</summary>
public sealed record RepositorySchemaVersionRow(int Version, DateTime AppliedAt, string RayMigratorVersion);
