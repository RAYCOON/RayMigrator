using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared;

namespace Raycoon.RayMigrator.Services;

/// <summary>
/// Default <see cref="IRepositorySchemaService"/>: validates the repository options the way the context factory
/// validates the repository of a run (data annotations with their defaults, a DAL for the database type, the schema
/// name against the DAL capabilities), masks the secrets of the options for the log, and runs the schema steps of
/// <see cref="RepositorySchemaExecutor"/> against the repository.
/// </summary>
public sealed class RepositorySchemaService : IRepositorySchemaService
{
    private readonly TemplateCache _templateCache;
    private readonly ILogger<RepositorySchemaService> _logger;

    /// <summary>Creates the service over the host's template cache.</summary>
    public RepositorySchemaService(TemplateCache templateCache, ILogger<RepositorySchemaService> logger)
    {
        _templateCache = templateCache ?? throw new ArgumentNullException(nameof(templateCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<RepositorySchemaResult> EnsureAsync(RepositoryOptions repository, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        // The steps log the schema name and may quote the connection string in an error: masked, as a run masks them.
        using var masking = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var dal = RepositoryAccess.Prepare(repository, _templateCache, _logger);

        var outcome = await new RepositorySchemaExecutor(_templateCache, _logger, repository, dal,
            AssemblyInfoHelper.GetRayMigratorVersion(), RepositorySchema.CurrentVersion).EnsureAsync(cancellationToken);

        return new RepositorySchemaResult(dal.DatabaseType, outcome.VersionBefore, outcome.VersionAfter, outcome.MigratorMetaId);
    }
}
