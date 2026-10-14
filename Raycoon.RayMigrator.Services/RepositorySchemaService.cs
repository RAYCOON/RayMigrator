using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using DataAnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared;
using Raycoon.RayMigrator.Shared.Exceptions;

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
    public RepositorySchemaResult Ensure(RepositoryOptions repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        Validate(repository);

        string databaseType = repository.DatabaseType!;
        var available = _templateCache.GetAvailableDatabaseTypes();
        if (!available.Contains(databaseType, StringComparer.Ordinal))
        {
            throw new ConfigurationValidationException(
                $"Unknown DatabaseType [{databaseType}] for the repository. Templates exist for [{string.Join(", ", available)}].");
        }

        DalFactory.TryGetDal(databaseType, repository.ConnectionString!, out IDal? dal);
        var dalProperties = new ConcurrentDictionary<string, DalSpecificProperties>();
        dalProperties.TryAdd(databaseType, dal!.DalSpecificProperties);
        SchemaNameValidator.ValidateSchemaName(dalProperties, databaseType, repository.SchemaName, "Repository", _logger);

        // The steps log the schema name and may quote the connection string in an error: masked, as a run masks them.
        using var masking = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValues([repository.ConnectionString, repository.SchemaName, repository.TableBaseName]);

        var outcome = new RepositorySchemaExecutor(_templateCache, _logger, repository, dal,
            AssemblyInfoHelper.GetRayMigratorVersion(), RepositorySchema.CurrentVersion).Ensure();

        return new RepositorySchemaResult(dal.DatabaseType, outcome.VersionBefore, outcome.VersionAfter, outcome.MigratorMetaId);
    }

    /// <summary>
    /// The data annotations of <see cref="RepositoryOptions"/>: the required database type, the connection string
    /// syntax and the documented defaults of the command settings, which the attributes write into null properties.
    /// </summary>
    private static void Validate(RepositoryOptions repository)
    {
        var findings = new List<DataAnnotationResult>();
        Validator.TryValidateObject(repository, new ValidationContext(repository), findings, validateAllProperties: true);
        if (string.IsNullOrWhiteSpace(repository.ConnectionString))
            findings.Add(new DataAnnotationResult("The ConnectionString of the repository is required.", [nameof(RepositoryOptions.ConnectionString)]));

        if (findings.Count > 0)
        {
            throw new ConfigurationValidationException(
                "The repository options are not valid: " + string.Join(" ", findings.Select(f => f.ErrorMessage)));
        }

        // The templates substitute the prefix and refuse a missing value; no prefix is the empty string.
        repository.TableBaseName ??= string.Empty;
    }
}
