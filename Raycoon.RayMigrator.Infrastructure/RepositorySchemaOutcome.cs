namespace Raycoon.RayMigrator.Core;

/// <summary>
/// What <see cref="RepositorySchemaExecutor.EnsureAsync"/> found and did: the schema version the repository had (0 for a
/// fresh one), the version it has now and the <c>MigratorMetaId</c> of this engine version.
/// </summary>
public sealed record RepositorySchemaOutcome(int VersionBefore, int VersionAfter, int MigratorMetaId);
