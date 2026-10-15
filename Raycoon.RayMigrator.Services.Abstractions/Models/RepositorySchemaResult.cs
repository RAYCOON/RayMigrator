namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// What <see cref="IRepositorySchemaService.EnsureAsync"/> found and did.
/// </summary>
/// <param name="DatabaseType">The database type of the repository as the DAL names it.</param>
/// <param name="VersionBefore">The schema version the repository had; 0 for a fresh repository.</param>
/// <param name="VersionAfter">The schema version the repository has now.</param>
/// <param name="MigratorMetaId">The <c>MigratorMeta</c> row of this engine version.</param>
public sealed record RepositorySchemaResult(string DatabaseType, int VersionBefore, int VersionAfter, int MigratorMetaId)
{
    /// <summary>The repository was created: it had no schema version before.</summary>
    public bool Created => VersionBefore == 0;

    /// <summary>The repository existed in an older schema version and got the missing steps.</summary>
    public bool Upgraded => VersionBefore > 0 && VersionBefore < VersionAfter;
}
