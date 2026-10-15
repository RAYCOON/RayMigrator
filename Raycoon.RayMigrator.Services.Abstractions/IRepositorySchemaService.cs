using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// Creates or upgrades the schema of a repository from its options alone, without a product, an environment or a
/// migration context: for hosts that manage repositories (RayMigrator Studio) before any product runs against them.
/// Registered by <c>AddRayMigratorServices</c>; needs the host's <c>TemplateCache</c>.
/// </summary>
public interface IRepositorySchemaService
{
    /// <summary>
    /// Makes sure the repository has every schema step up to <c>RepositorySchema.CurrentVersion</c> and registers this
    /// engine version in <c>MigratorMeta</c> (ADR-024). A fresh repository gets its schema and tables, an older one the
    /// missing steps, a current one stays as it is. The documented defaults are written into the null command settings
    /// of <paramref name="repository"/>; a null <c>TableBaseName</c> becomes the empty prefix.
    /// </summary>
    /// <exception cref="Raycoon.RayMigrator.Shared.Exceptions.ConfigurationValidationException">The options are incomplete
    /// or invalid, the database type is unknown or has no templates, or the schema name is missing where the engine needs one.</exception>
    /// <exception cref="Raycoon.RayMigrator.Shared.Exceptions.RepositorySchemaException">The repository has tables but no
    /// schema version (created before 0.17.0), or a schema version newer than this engine supports.</exception>
    /// <exception cref="Raycoon.RayMigrator.Shared.Exceptions.TemplateExecutionException">A step failed on the database.</exception>
    RepositorySchemaResult Ensure(RepositoryOptions repository);
}
