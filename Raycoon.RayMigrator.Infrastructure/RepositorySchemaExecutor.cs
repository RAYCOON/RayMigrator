using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Logging;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// The schema steps of the repository (ADR-024) for one repository, without a migration context:
/// <c>Repository_CheckCreate</c> creates the schema version table when it is missing and reports the version the
/// repository has; every missing <c>Repository_Upgrade_{n}</c> step up to the target version runs in order, step 1
/// creating the tables of a fresh repository; and <c>Repository_MigratorMeta_CheckInsert</c> yields the
/// <c>MigratorMetaId</c> of this engine version. A repository that has tables but no schema version was created by a
/// RayMigrator before 0.17.0 and is refused, as is one whose version is newer than the target.
/// <see cref="TemplateExecutor.RepositoryCheckCreate"/> runs the steps for a run with the values of its context; a
/// host that manages repositories runs them through the repository schema service of the service layer with the
/// repository options alone.
/// </summary>
public sealed class RepositorySchemaExecutor
{
    private readonly TemplateCache _templateCache;
    private readonly ILogger _logger;
    private readonly RepositoryOptions _repository;
    private readonly IDal _repositoryDal;
    private readonly string _rayMigratorVersion;
    private readonly int _targetVersion;
    private readonly Func<object>? _logContext;

    /// <param name="templateCache">The templates of every database type of the process.</param>
    /// <param name="logger">Receives the Debug lines of every step and the Information line of a created or upgraded repository.</param>
    /// <param name="repository">The repository: database type, connection string, schema, prefix and command settings.</param>
    /// <param name="repositoryDal">The DAL of the repository's database type and connection string.</param>
    /// <param name="rayMigratorVersion">The engine version the steps and the <c>MigratorMeta</c> row record.</param>
    /// <param name="targetVersion">The schema version to reach, normally <see cref="RepositorySchema.CurrentVersion"/>.</param>
    /// <param name="logContext">What the log lines append as their migration context; null for the steps without a run.</param>
    public RepositorySchemaExecutor(TemplateCache templateCache, ILogger logger, RepositoryOptions repository, IDal repositoryDal,
        string rayMigratorVersion, int targetVersion, Func<object>? logContext = null)
    {
        _templateCache = templateCache ?? throw new ArgumentNullException(nameof(templateCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _repositoryDal = repositoryDal ?? throw new ArgumentNullException(nameof(repositoryDal));
        _rayMigratorVersion = rayMigratorVersion ?? throw new ArgumentNullException(nameof(rayMigratorVersion));
        _targetVersion = targetVersion;
        _logContext = logContext;

        // The templates and the DAL settings need every value; the data annotations of the options write the
        // documented defaults into empty settings, and RepositorySchemaService applies them before it gets here.
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(repository.DatabaseType)) missing.Add(nameof(RepositoryOptions.DatabaseType));
        if (string.IsNullOrWhiteSpace(repository.ConnectionString)) missing.Add(nameof(RepositoryOptions.ConnectionString));
        if (repository.TableBaseName is null) missing.Add(nameof(RepositoryOptions.TableBaseName) + " (the empty string for no prefix)");
        if (repository.DbCommandTimeoutInSeconds is null) missing.Add(nameof(RepositoryOptions.DbCommandTimeoutInSeconds));
        if (repository.DbCommandMaxRetries is null) missing.Add(nameof(RepositoryOptions.DbCommandMaxRetries));
        if (repository.DbCommandWaitTimeInMsBeforeRetry is null) missing.Add(nameof(RepositoryOptions.DbCommandWaitTimeInMsBeforeRetry));
        if (missing.Count > 0)
            throw new ArgumentException($"The repository options lack {string.Join(", ", missing)}; apply the data annotations' defaults first, as RepositorySchemaService does.", nameof(repository));
    }

    private object LogContext => _logContext?.Invoke() ?? string.Empty;

    /// <summary>
    /// Checks the schema version, applies the missing steps and registers the engine version.
    /// </summary>
    /// <exception cref="RepositorySchemaException">The repository has no schema version but repository tables, or a
    /// schema version newer than the target.</exception>
    /// <exception cref="TemplateExecutionException"></exception>
    /// <exception cref="TemplateResultException">A step did not return its own version as result code.</exception>
    public RepositorySchemaOutcome Ensure()
    {
        var eventId = MigrationEvent.TemplateExecutionRepositoryCheckCreate;

        _logger.LogDebug(eventId, "Checking the repository schema version (target version [{TargetVersion}])...{MigrationContext}", _targetVersion, LogContext);

        var check = Execute(TemplateType.Repository_CheckCreate, null, eventId);

        // Repository_CheckCreate returns 'schemaVersion,unversionedTables,message': the highest applied schema version (0 when
        // none) and 1 when the MigratorMeta table exists, which with version 0 means a repository of a RayMigrator before 0.17.0.
        int foundVersion = check.ResultCode;
        bool unversionedTables = check.ResultCodes.Length > 1 && check.ResultCodes[1] == 1;
        string schemaName = SensitiveDataMasker.Mask(_repository.SchemaName);

        if (foundVersion == 0 && unversionedTables)
        {
            throw new RepositorySchemaException(
                $"The repository (DatabaseType [{_repositoryDal.DatabaseType}], schema [{schemaName}]) has repository tables but no schema version: " +
                "a RayMigrator before 0.17.0 created it, and repositories are not upgraded across that boundary. " +
                "Drop the repository tables and let this version create them again.",
                foundVersion, _targetVersion);
        }

        if (foundVersion > _targetVersion)
        {
            throw new RepositorySchemaException(
                $"The repository (DatabaseType [{_repositoryDal.DatabaseType}], schema [{schemaName}]) has schema version [{foundVersion}], " +
                $"but RayMigrator [{_rayMigratorVersion}] supports version [{_targetVersion}] at most. Use a RayMigrator that supports this repository.",
                foundVersion, _targetVersion);
        }

        for (int step = foundVersion + 1; step <= _targetVersion; step++)
        {
            Upgrade(step);
        }

        // An operator should see a created or upgraded repository without Debug logging: a first run creates the schema and
        // the tables in a database that may belong to someone else (#27).
        if (foundVersion == 0)
        {
            _logger.LogInformation(eventId,
                "Repository created: DatabaseType [{DatabaseType}], schema [{SchemaName}], schema version [{SchemaVersion}].{MigrationContext}",
                _repositoryDal.DatabaseType, schemaName, _targetVersion, LogContext);
        }
        else if (foundVersion < _targetVersion)
        {
            _logger.LogInformation(eventId,
                "Repository schema upgraded: DatabaseType [{DatabaseType}], schema [{SchemaName}], version [{FromVersion}] to [{ToVersion}].{MigrationContext}",
                _repositoryDal.DatabaseType, schemaName, foundVersion, _targetVersion, LogContext);
        }

        return new RepositorySchemaOutcome(foundVersion, _targetVersion, RegisterEngineVersion());
    }

    /// <summary>
    /// Applies one schema step: <c>Repository_Upgrade_{step}</c> creates schema version <paramref name="step"/> on top of
    /// the version before it and writes its row of the schema version table. The step is idempotent per engine and
    /// serialised against a parallel start on the same repository.
    /// </summary>
    private void Upgrade(int step)
    {
        var templateType = RepositorySchema.UpgradeTemplate(step);
        var eventId = MigrationEvent.TemplateExecutionRepositoryUpgrade;

        _logger.LogDebug(eventId, "Applying repository schema version [{SchemaVersion}] ({TemplateType})...{MigrationContext}", step, templateType, LogContext);

        var parameters = new DalParameterList();
        parameters.AddParameter(new DalParameter("RayMigratorVersion", _rayMigratorVersion, typeof(string)));

        TemplateResponse response;
        try
        {
            response = Execute(templateType, parameters, eventId);
        }
        catch (TemplateExecutionException)
        {
            // Two processes that start on the same repository both read the version before either applies the step. The
            // step is serialised and idempotent where the engine allows it; SQLite cannot make ALTER TABLE ADD COLUMN
            // idempotent, so the second process fails on a column the first one added. The version row decides.
            if (!StepAppliedMeanwhile(step))
                throw;

            _logger.LogDebug(eventId, "Repository schema version [{SchemaVersion}] was applied by a parallel start meanwhile; the step is skipped.{MigrationContext}", step, LogContext);
            return;
        }

        if (response.ResultCode != step)
        {
            throw new TemplateResultException(
                $"Repository schema step [{step}] ({templateType}) returned result code [{response.ResultCode}] instead of its version. {response.ResultMessage}",
                response.ResultCode);
        }
    }

    /// <summary>
    /// Whether the repository already has schema version <paramref name="step"/>, read through <c>Repository_CheckCreate</c>
    /// after a failed step. A read that fails itself counts as "not applied", so the step's own exception surfaces.
    /// </summary>
    private bool StepAppliedMeanwhile(int step)
    {
        try
        {
            return Execute(TemplateType.Repository_CheckCreate, null, MigrationEvent.TemplateExecutionRepositoryCheckCreate).ResultCode >= step;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(MigrationEvent.TemplateExecutionRepositoryCheckCreate, "Could not re-read the repository schema version after a failed step: {Error}{MigrationContext}", ex.Message, LogContext);
            return false;
        }
    }

    /// <summary>
    /// Registers the engine version in <c>MigratorMeta</c> if it is not registered yet and returns the id of its row,
    /// which every run of this engine version references as <c>MigratorMetaId</c>.
    /// </summary>
    private int RegisterEngineVersion()
    {
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigratorMetaCheckInsert;

        _logger.LogDebug(eventId, "Registering RayMigrator version [{RayMigratorVersion}] in the repository...{MigrationContext}", _rayMigratorVersion, LogContext);

        var parameters = new DalParameterList();
        parameters.AddParameter(new DalParameter("RayMigratorVersion", _rayMigratorVersion, typeof(string)));
        parameters.AddParameter(new DalParameter("RepositoryDatabaseType", _repositoryDal.DatabaseType, typeof(string)));

        return Execute(TemplateType.Repository_MigratorMeta_CheckInsert, parameters, eventId).ResultCode;
    }

    private TemplateResponse Execute(TemplateType templateType, DalParameterList? parameters, EventId eventId)
    {
        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var response = TemplateExecutor.ExecuteScalar(template, _repositoryDal, _repository.GetDalSettings(), parameters);

        _logger.LogDebug(eventId, "Execution of Template [{TemplateInfo}] returned ResultCode [{ResultCode}] and ResultMessage [{ResultMessage}]{MigrationContext}",
            template, response.ResultCode, response.ResultMessage, LogContext);

        return response;
    }
}
