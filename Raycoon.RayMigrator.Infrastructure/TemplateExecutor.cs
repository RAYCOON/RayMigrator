using System.Data.Common;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Core.Recovery;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Core.Logging;

namespace Raycoon.RayMigrator.Core;

public class TemplateExecutor
{
    private readonly TemplateCache _templateCache;
    private readonly ILogger<TemplateExecutor> _logger;
    private readonly IMigrationContextAccessor _ctxAccessor;
    private RepositoryOptions? _repositoryBacking;
    private IDal? _repositoryDalBacking;

    /// <summary>
    /// Lazily-initialized repository options. Defers context access to support API endpoints
    /// where MigrationContext is set after DI resolution.
    /// </summary>
    private RepositoryOptions _repository
    {
        get
        {
            if (_repositoryBacking == null) InitializeFromContext();
            return _repositoryBacking!;
        }
    }

    private IDal _repositoryDal
    {
        get
        {
            if (_repositoryBacking == null) InitializeFromContext();
            return _repositoryDalBacking!;
        }
    }

    /// <summary>
    /// Public constructor. Uses IMigrationContextAccessor for per-request context isolation.
    /// Context access is deferred to first use (not constructor time) to support API endpoints
    /// where the MigrationContext is set after DI resolution.
    /// </summary>
    public TemplateExecutor(TemplateCache templateCache, ILogger<TemplateExecutor> logger, IMigrationContextAccessor ctxAccessor)
    {
        _templateCache = templateCache;
        _logger = logger;
        _ctxAccessor = ctxAccessor;
    }

    private void InitializeFromContext()
    {
        _logger.LogDebug("Initializing TemplateExecutor from MigrationContext (deferred initialization)");
        _repositoryBacking = _ctxAccessor.Current.RayMigratorOptions.Repository!;
        if (DalFactory.TryGetDal(_repositoryBacking.DatabaseType!, _repositoryBacking.ConnectionString!, out var dal))
            _repositoryDalBacking = dal!;
    }

    /// <summary>
    /// Makes sure the repository exists in the schema version this run expects and registers the engine version
    /// (ADR-024): <c>Repository_CheckCreate</c> creates the schema version table when it is missing and reports the
    /// version the repository has; every missing <c>Repository_Upgrade_{n}</c> step up to
    /// <see cref="MigrationContext.RepositorySchemaTargetVersion"/> runs in order, step 1 creating the tables of a
    /// fresh repository; and <c>Repository_MigratorMeta_CheckInsert</c> yields the <c>MigratorMetaId</c> of this
    /// engine version. A repository that has tables but no schema version was created by a RayMigrator before
    /// 0.17.0 and is refused, as is one whose version is newer than the target.
    /// </summary>
    /// <exception cref="RepositorySchemaException">The repository has no schema version but repository tables, or a
    /// schema version newer than this engine supports.</exception>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryCheckCreate()
    {
        var eventId = MigrationEvent.TemplateExecutionRepositoryCheckCreate;
        int targetVersion = _ctxAccessor.Current.RepositorySchemaTargetVersion;

        _logger.LogDebug(eventId, "Checking the repository schema version (target version [{TargetVersion}])...{MigrationContext}", targetVersion, _ctxAccessor.Current.Clone);

        var checkTemplate = _templateCache.GetRepositoryTemplate(TemplateType.Repository_CheckCreate, _repository);
        var check = ExecuteScalarWithNegativeResultCodeException(checkTemplate, _repositoryDal, _repository.GetDalSettings(), null, _logger, eventId);

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
                foundVersion, targetVersion);
        }

        if (foundVersion > targetVersion)
        {
            throw new RepositorySchemaException(
                $"The repository (DatabaseType [{_repositoryDal.DatabaseType}], schema [{schemaName}]) has schema version [{foundVersion}], " +
                $"but RayMigrator [{_ctxAccessor.Current.RayMigratorVersion}] supports version [{targetVersion}] at most. Use a RayMigrator that supports this repository.",
                foundVersion, targetVersion);
        }

        for (int step = foundVersion + 1; step <= targetVersion; step++)
        {
            RepositoryUpgrade(step);
        }

        // An operator should see a created or upgraded repository without Debug logging: a first run creates the schema and
        // the tables in a database that may belong to someone else (#27).
        if (foundVersion == 0)
        {
            _logger.LogInformation(eventId,
                "Repository created: DatabaseType [{DatabaseType}], schema [{SchemaName}], schema version [{SchemaVersion}].{MigrationContext}",
                _repositoryDal.DatabaseType, schemaName, targetVersion, _ctxAccessor.Current.Clone);
        }
        else if (foundVersion < targetVersion)
        {
            _logger.LogInformation(eventId,
                "Repository schema upgraded: DatabaseType [{DatabaseType}], schema [{SchemaName}], version [{FromVersion}] to [{ToVersion}].{MigrationContext}",
                _repositoryDal.DatabaseType, schemaName, foundVersion, targetVersion, _ctxAccessor.Current.Clone);
        }

        _ctxAccessor.Current.MigrationState.MigratorMetaId = RepositoryMigratorMetaCheckInsert();
    }

    /// <summary>
    /// Applies one schema step: <c>Repository_Upgrade_{step}</c> creates schema version <paramref name="step"/> on top of
    /// the version before it and writes its row of the schema version table. The step is idempotent per engine and
    /// serialised against a parallel start on the same repository.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    /// <exception cref="TemplateResultException">The step did not return its own version as result code.</exception>
    private void RepositoryUpgrade(int step)
    {
        var templateType = RepositorySchema.UpgradeTemplate(step);
        var eventId = MigrationEvent.TemplateExecutionRepositoryUpgrade;

        _logger.LogDebug(eventId, "Applying repository schema version [{SchemaVersion}] ({TemplateType})...{MigrationContext}", step, templateType, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("RayMigratorVersion", _ctxAccessor.Current.RayMigratorVersion, typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        TemplateResponse templateResponse;
        try
        {
            templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
        }
        catch (TemplateExecutionException)
        {
            // Two processes that start on the same repository both read the version before either applies the step. The
            // step is serialised and idempotent where the engine allows it; SQLite cannot make ALTER TABLE ADD COLUMN
            // idempotent, so the second process fails on a column the first one added. The version row decides.
            if (!StepAppliedMeanwhile(step))
                throw;

            _logger.LogDebug(eventId, "Repository schema version [{SchemaVersion}] was applied by a parallel start meanwhile; the step is skipped.{MigrationContext}", step, _ctxAccessor.Current.Clone);
            return;
        }

        if (templateResponse.ResultCode != step)
        {
            throw new TemplateResultException(
                $"Repository schema step [{step}] ({template}) returned result code [{templateResponse.ResultCode}] instead of its version. {templateResponse.ResultMessage}",
                templateResponse.ResultCode);
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
            var checkTemplate = _templateCache.GetRepositoryTemplate(TemplateType.Repository_CheckCreate, _repository);
            var check = ExecuteScalarWithNegativeResultCodeException(checkTemplate, _repositoryDal, _repository.GetDalSettings(), null, _logger, MigrationEvent.TemplateExecutionRepositoryCheckCreate);
            return check.ResultCode >= step;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(MigrationEvent.TemplateExecutionRepositoryCheckCreate, "Could not re-read the repository schema version after a failed step: {Error}{MigrationContext}", ex.Message, _ctxAccessor.Current.Clone);
            return false;
        }
    }
    /// <summary>
    /// Registers the engine version in <c>MigratorMeta</c> if it is not registered yet and returns the id of its row,
    /// which every run of this engine version references as <c>MigratorMetaId</c>.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    private int RepositoryMigratorMetaCheckInsert()
    {
        var templateType = TemplateType.Repository_MigratorMeta_CheckInsert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigratorMetaCheckInsert;

        _logger.LogDebug(eventId, "Registering RayMigrator version [{RayMigratorVersion}] in the repository...{MigrationContext}", _ctxAccessor.Current.RayMigratorVersion, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("RayMigratorVersion", _ctxAccessor.Current.RayMigratorVersion, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("RepositoryDatabaseType", _repositoryDal.DatabaseType, typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        return templateResponse.ResultCode;
    }

    /// <summary>
    /// Checks for existing product-name and optionally creates a new Product-entry in the repository and returns the ProductId.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryProductCheckInsert()
    {
        var templateType = TemplateType.Repository_Product_CheckInsert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryProductCheckInsert;
        string productName = _ctxAccessor.Current.RayMigratorConsoleOptions.Product;
        
        _logger.LogDebug(eventId, "Searching for product with name {ProductName}. Insert a new product-entry if name does not yet exist{MigrationContext}", productName , _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", productName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", productName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.ProductId = templateResponse.ResultCode;
    }

    /// <summary>
    /// Checks for existing environment name and optionally creates a new Environment entry in the repository and returns the EnvironmentId.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryEnvironmentCheckInsert()
    {
        var templateType = TemplateType.Repository_Environment_CheckInsert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryEnvironmentCheckInsert;
        string environmentName = _ctxAccessor.Current.RayMigratorConsoleOptions.Environment;

        _logger.LogDebug(eventId, "Searching for environment with name {EnvironmentName}. Insert a new environment entry if name does not yet exist{MigrationContext}", environmentName, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", environmentName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", environmentName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.EnvironmentId = templateResponse.ResultCode;
    }

    /// <summary>
    /// Looks up the ProductId of the current product without inserting anything.
    /// Used by run modes that must not write to the repository (Simulate).
    /// Sets <c>MigrationState.ProductId</c> to the id, or to 0 when the product is not registered yet.
    /// </summary>
    /// <returns>True when the product exists in the repository.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public bool RepositoryProductSelect()
    {
        var templateType = TemplateType.Repository_Product_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryProductSelect;
        string productName = _ctxAccessor.Current.RayMigratorConsoleOptions.Product;

        _logger.LogDebug(eventId, "Looking up product with name {ProductName} (read-only, no insert){MigrationContext}", productName, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", productName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", productName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.ProductId = templateResponse.ResultCode;
        return templateResponse.ResultCode > 0;
    }

    /// <summary>
    /// Looks up the EnvironmentId of the current environment without inserting anything.
    /// Used by run modes that must not write to the repository (Simulate).
    /// Sets <c>MigrationState.EnvironmentId</c> to the id, or to 0 when the environment is not registered yet.
    /// </summary>
    /// <returns>True when the environment exists in the repository.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public bool RepositoryEnvironmentSelect()
    {
        var templateType = TemplateType.Repository_Environment_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryEnvironmentSelect;
        string environmentName = _ctxAccessor.Current.RayMigratorConsoleOptions.Environment;

        _logger.LogDebug(eventId, "Looking up environment with name {EnvironmentName} (read-only, no insert){MigrationContext}", environmentName, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", environmentName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", environmentName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.EnvironmentId = templateResponse.ResultCode;
        return templateResponse.ResultCode > 0;
    }

    /// <summary>
    /// OK: Creates a new MigrationRun in the database and inserts the settings JSON into MigrationRunMeta.
    /// </summary>
    /// <param name="migrationRunSettingsJson">JSON snapshot of all RayMigrator settings at migration start.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationRunInsert(string migrationRunSettingsJson)
    {
        var templateType = TemplateType.Repository_MigrationRun_Insert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunInsert;

        _logger.LogDebug(eventId, "Create MigrationRun-entry{MigrationContext}", _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunModeId", (byte) _ctxAccessor.Current.RayMigratorConsoleOptions.RunMode, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("MigratorMetaId", _ctxAccessor.Current.MigrationState.MigratorMetaId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunResultId", (byte) _ctxAccessor.Current.MigrationState.MigrationRunResult, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FromReleaseVersion", _ctxAccessor.Current.MigrationState.FromReleaseVersion, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("ToReleaseVersion", _ctxAccessor.Current.RayMigratorConsoleOptions.TargetReleaseVersion, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunSettingsJson", migrationRunSettingsJson, typeof(string)));

        // Run details (schema version 2): what the run does and who started it where. The operation is the one the
        // service set for the command; source and initiator come from the host, the host name from the machine when
        // the host did not name one.
        var consoleOptions = _ctxAccessor.Current.RayMigratorConsoleOptions;
        var source = consoleOptions.Source is { } s && s != MigrationRunSource.Undefined ? (byte?)s : null;
        dalParameterList.AddParameter(new DalParameter("MigrationOperationId", (byte)_ctxAccessor.Current.MigrationState.MigrationOperation, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunSourceId", source, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("InitiatedBy", Truncate(consoleOptions.InitiatedBy, 200), typeof(string)));
        dalParameterList.AddParameter(new DalParameter("InitiatedBySubject", Truncate(consoleOptions.InitiatedBySubject, 200), typeof(string)));
        dalParameterList.AddParameter(new DalParameter("Host", Truncate(consoleOptions.Host ?? RunInitiator.CurrentHost, 200), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        try
        {
            var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
            _ctxAccessor.Current.MigrationState.MigrationRunId = templateResponse.ResultCode;
        }
        catch (TemplateResultException ex) when (ex.ResultCode == -2)
        {
            throw new MigrationAlreadyRunningException(
                ex.Message, _ctxAccessor.Current.MigrationState.ProductId);
        }
    }

    /// <summary>
    /// The longest error message stored in <c>MigrationRun.ErrorMessage</c>; longer messages are cut.
    /// </summary>
    public const int MaxErrorMessageLength = 4000;

    /// <summary>
    /// Updates an existing MigrationRun with the final result status, the completion time and, for a run that did
    /// not end <see cref="MigrationRunResult.Ok"/>, the code and message of the error that decided the result.
    /// </summary>
    /// <param name="runResult">The final result of the migration run.</param>
    /// <param name="errorCode">The engine error code of the failure (<c>TemplateResultCode</c>), or null.</param>
    /// <param name="errorMessage">The message of the failure, cut to <see cref="MaxErrorMessageLength"/>, or null.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationRunUpdate(MigrationRunResult runResult, string? errorCode = null, string? errorMessage = null)
    {
        var templateType = TemplateType.Repository_MigrationRun_Update;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunUpdate;

        _logger.LogDebug(eventId, "Update MigrationRun-entry with result {MigrationRunResult}{MigrationContext}",
            runResult, _ctxAccessor.Current.Clone);

        // Validate state
        if (_ctxAccessor.Current.MigrationState.MigrationRunId < 1)
        {
            throw new RayMigratorInternalException(
                $"Invalid MigrationRunId [{_ctxAccessor.Current.MigrationState.MigrationRunId}] when updating MigrationRun.");
        }

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", _ctxAccessor.Current.MigrationState.MigrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunResultId", (byte)runResult, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("ErrorCode", Truncate(errorCode, 100), typeof(string)));
        dalParameterList.AddParameter(new DalParameter("ErrorMessage", Truncate(errorMessage, MaxErrorMessageLength), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.MigrationRunResult = runResult;
    }

    /// <summary>Cuts <paramref name="value"/> to <paramref name="maxLength"/> characters; null and empty stay null.</summary>
    internal static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Selects orphaned MigrationRun entries (Running state, FinishedAt IS NULL).
    /// Used by the Fix command. Parameters are passed directly (not from MigrationContext).
    /// </summary>
    /// <param name="productId">The product ID to query.</param>
    /// <param name="environmentId">The environment ID to query.</param>
    /// <returns>List of raw row dictionaries with orphaned run data.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public List<Dictionary<string, object?>> RepositoryMigrationRunSelectOrphaned(int productId, int environmentId)
    {
        var templateType = TemplateType.Repository_MigrationRun_SelectOrphaned;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunSelectOrphaned;

        _logger.LogDebug(eventId, "Selecting orphaned MigrationRun entries for product {ProductId} with environment {Environment} ({EnvironmentId}){MigrationContext}",
            productId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, environmentId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", productId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", environmentId, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{_repositoryDal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        _logger.LogDebug(eventId, "Found {Count} orphaned MigrationRun entries{MigrationContext}", rows.Count, _ctxAccessor.Current.Clone);
        return rows;
    }

    /// <summary>
    /// Writes one heartbeat for the current run: sets <c>MigrationRun.LastHeartbeatAt</c> to now while the row is
    /// still open (<c>FinishedAt IS NULL</c>).
    /// </summary>
    /// <returns>True when the row was open and updated; false when the run has already finished.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public bool RepositoryMigrationRunHeartbeat()
    {
        var templateType = TemplateType.Repository_MigrationRun_Heartbeat;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunHeartbeat;
        int migrationRunId = _ctxAccessor.Current.MigrationState.MigrationRunId;

        if (migrationRunId < 1)
        {
            throw new RayMigratorInternalException($"Invalid MigrationRunId [{migrationRunId}] when writing a heartbeat.");
        }

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", migrationRunId, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, null, eventId);

        return templateResponse.ResultCode > 0;
    }

    /// <summary>
    /// Marks a single orphaned MigrationRun as Error with FinishedAt set, provided its last heartbeat (or its
    /// start, when it never wrote one) is at least <paramref name="thresholdMinutes"/> old; a run that is alive
    /// is left open and reported as <c>-31</c>. Used by the Fix command and the auto-fix of a parallel start.
    /// Parameters are passed directly (not from MigrationContext).
    /// </summary>
    /// <param name="migrationRunId">The ID of the orphaned MigrationRun to fix.</param>
    /// <param name="thresholdMinutes">The minimum age of the last heartbeat in minutes; 0 closes the run regardless.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationRunFixOrphaned(int migrationRunId, int thresholdMinutes)
    {
        var templateType = TemplateType.Repository_MigrationRun_FixOrphaned;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunFixOrphaned;

        _logger.LogDebug(eventId, "Fixing orphaned MigrationRun {MigrationRunId} (heartbeat older than {ThresholdMinutes} minutes){MigrationContext}",
            migrationRunId, thresholdMinutes, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", migrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("ThresholdMinutes", thresholdMinutes, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Updates orphaned MigrationRecord entries (Running/Unclear) for a given MigrationRun.
    /// Used by the Fix command. Parameters are passed directly (not from MigrationContext).
    /// </summary>
    /// <param name="migrationRunId">The MigrationRun ID whose orphaned migrations to fix.</param>
    /// <param name="status">Target status: NotMigrated (50) or Migrated (100).</param>
    /// <returns>Number of updated MigrationRecord entries (0 = none found, which is not an error).</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public int RepositoryMigrationRecordFixOrphaned(int migrationRunId, MigrationStatus status)
    {
        var templateType = TemplateType.Repository_MigrationRecord_FixOrphaned;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationFixOrphaned;

        _logger.LogDebug(eventId, "Fixing orphaned MigrationRecord entries for MigrationRunId {MigrationRunId} with status {Status}{MigrationContext}",
            migrationRunId, status, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", migrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)status, typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        return templateResponse.ResultCode;
    }

    /// <summary>
    /// Checks for interrupted migrations that can be resumed.
    /// </summary>
    /// <returns>An InterruptedMigrationInfo object if an interrupted migration is found, null otherwise.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public InterruptedMigrationInfo? RepositoryMigrationGetInterrupted()
    {
        var templateType = TemplateType.Repository_MigrationRecord_GetInterrupted;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationGetInterrupted;

        _logger.LogDebug(eventId, "Checking for interrupted migrations for product {ProductId} with environment {Environment} ({EnvironmentId}){MigrationContext}",
            _ctxAccessor.Current.MigrationState.ProductId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, _ctxAccessor.Current.MigrationState.EnvironmentId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        // ResultCode 0 means no interrupted migration found
        if (templateResponse.ResultCode == 0)
        {
            return null;
        }

        // Parse the pipe-separated result message
        // Format: MigrationRecordId|MigrationRunId|ReleaseVersion|Filename|FileUpBlocksMigrated|FileUpBlocksTotal|EnvironmentId|TargetGroupAlias|TargetAlias
        var parts = (templateResponse.ResultMessage ?? string.Empty).Split('|');
        if (parts.Length < 9)
        {
            _logger.LogWarning("Unexpected format in interrupted migration response: {Response}", templateResponse.ResultMessage);
            return null;
        }

        // Environment text value is run-constant (WHERE constraint in SQL filters by EnvironmentId);
        // fill from console options rather than a redundant JOIN to the Environment table.
        return new InterruptedMigrationInfo
        {
            MigrationRecordId = int.Parse(parts[0]),
            MigrationRunId = int.Parse(parts[1]),
            ReleaseVersion = parts[2],
            Filename = parts[3],
            BlocksMigrated = int.Parse(parts[4]),
            BlocksTotal = int.Parse(parts[5]),
            EnvironmentId = int.Parse(parts[6]),
            Environment = _ctxAccessor.Current.RayMigratorConsoleOptions.Environment,
            TargetGroupAlias = parts[7],
            TargetAlias = parts[8]
        };
    }

    /// <summary>
    /// Creates a new MigrationRecord entry or resets an existing archived record
    /// with block-level tracking for recovery support.
    /// </summary>
    /// <param name="existingMigrationRecordId">0 to INSERT a new record, or the existing Migration ID to UPDATE/reset.</param>
    /// <param name="filename">The migration filename.</param>
    /// <param name="releaseVersion">The release version from file path.</param>
    /// <param name="targetGroupAlias">The target group alias.</param>
    /// <param name="targetAlias">The target alias.</param>
    /// <param name="fileOrderId">The file order ID.</param>
    /// <param name="fileUpHash">Hash of the entire file.</param>
    /// <param name="fileUpConfigHash">Hash of the TOML configuration section.</param>
    /// <param name="fileUpBlocksHash">Hash of the SQL content blocks.</param>
    /// <param name="fileUpBlocksTotal">Total number of blocks in the file.</param>
    /// <param name="fileUpConfigJson">JSON representation of file configuration.</param>
    /// <param name="migrateDownFileExists">Whether a down-migration file exists.</param>
    /// <returns>The MigrationRecordId (new or existing).</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public int RepositoryMigrationInsert(
        int existingMigrationRecordId,
        string filename,
        string releaseVersion,
        string targetGroupAlias,
        string targetAlias,
        int fileOrderId,
        string fileUpHash,
        string? fileUpConfigHash,
        string fileUpBlocksHash,
        int fileUpBlocksTotal,
        string? fileUpConfigJson,
        bool migrateDownFileExists)
    {
        var templateType = TemplateType.Repository_MigrationRecord_Insert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationInsert;

        _logger.LogDebug(eventId, "Create MigrationRecord-entry for file {Filename}{MigrationContext}", filename, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ExistingMigrationRecordId", existingMigrationRecordId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", _ctxAccessor.Current.MigrationState.MigrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunModeId", (byte)_ctxAccessor.Current.RayMigratorConsoleOptions.RunMode, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("MigrationOperationId", (byte)_ctxAccessor.Current.MigrationState.MigrationOperation, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)MigrationStatus.Pending, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("ReleaseVersion", releaseVersion, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("TargetGroupAlias", targetGroupAlias, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("TargetAlias", targetAlias, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("Filename", filename, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileOrderId", fileOrderId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileUpHash", fileUpHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpConfigHash", fileUpConfigHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksHash", fileUpBlocksHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksTotal", fileUpBlocksTotal, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileUpConfigJson", fileUpConfigJson ?? "{}", typeof(string)));
        dalParameterList.AddParameter(new DalParameter("MigrateDownFileExists", migrateDownFileExists, typeof(bool)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);

        _ctxAccessor.Current.MigrationState.MigrationRecordId = templateResponse.ResultCode;
        return templateResponse.ResultCode;
    }

    /// <summary>
    /// Updates a MigrationRecord entry with block progress or final status.
    /// </summary>
    /// <param name="migrationRecordId">The migration record ID to update.</param>
    /// <param name="migrationStatus">The migration status.</param>
    /// <param name="fileUpBlocksMigrated">
    /// Number of leading blocks that are committed on the target. For a <see cref="MigrationStatus.Failed"/> record this
    /// is what the next run skips when it resumes the file (<c>FindResumableBlock</c>), so it must never include the
    /// block that failed (#11). Ignore-mode failures store the total to opt out of resuming.
    /// </param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdate(int migrationRecordId, MigrationStatus migrationStatus, int fileUpBlocksMigrated)
    {
        var templateType = TemplateType.Repository_MigrationRecord_Update;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdate;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationRecordId} - Block {Block} - Status {Status}{MigrationContext}",
            migrationRecordId, fileUpBlocksMigrated, migrationStatus, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRecordId", migrationRecordId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)migrationStatus, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksMigrated", fileUpBlocksMigrated, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Updates a MigrationRecord entry with rollback (FileDown*) fields and status.
    /// Used during rollback execution to track rollback file metadata and block progress.
    /// </summary>
    /// <param name="migrationRecordId">The migration record ID to update.</param>
    /// <param name="migrationStatus">The migration status.</param>
    /// <param name="fileDownHash">SHA256 hash of the rollback file.</param>
    /// <param name="fileDownConfigHash">SHA256 hash of the TOML config in the rollback file.</param>
    /// <param name="fileDownBlocksHash">SHA256 hash of the SQL blocks in the rollback file.</param>
    /// <param name="fileDownBlocksMigrated">Number of rollback blocks successfully executed.</param>
    /// <param name="fileDownBlocksTotal">Total number of rollback blocks.</param>
    /// <param name="fileDownConfigJson">JSON of parsed TOML configuration from the rollback file.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdateRollback(
        int migrationRecordId,
        MigrationStatus migrationStatus,
        string fileDownHash,
        string? fileDownConfigHash,
        string fileDownBlocksHash,
        int fileDownBlocksMigrated,
        int fileDownBlocksTotal,
        string? fileDownConfigJson)
    {
        var templateType = TemplateType.Repository_MigrationRecord_UpdateRollback;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdateRollback;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationRecordId} rollback - Block {Block}/{Total} - Status {Status}{MigrationContext}",
            migrationRecordId, fileDownBlocksMigrated, fileDownBlocksTotal, migrationStatus, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRecordId", migrationRecordId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)migrationStatus, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FileDownHash", fileDownHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownConfigHash", fileDownConfigHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksHash", fileDownBlocksHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksMigrated", fileDownBlocksMigrated, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksTotal", fileDownBlocksTotal, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileDownConfigJson", fileDownConfigJson ?? "{}", typeof(string)));
        // The run performing the rollback and what it does (MigrateDown or Rollback): the history row is attributed to
        // this run while the record keeps the MigrationRunId of the run that created it (#13)
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", _ctxAccessor.Current.MigrationState.MigrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationOperationId", (byte)_ctxAccessor.Current.MigrationState.MigrationOperation, typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Updates a MigrationRecord entry with block progress or final status using a caller-provided
    /// shared connection and transaction for atomic execution with target SQL blocks.
    /// </summary>
    /// <param name="migrationRecordId">The migration record ID to update.</param>
    /// <param name="migrationStatus">The migration status.</param>
    /// <param name="fileUpBlocksMigrated">Number of blocks successfully migrated.</param>
    /// <param name="connection">The shared database connection.</param>
    /// <param name="transaction">The shared database transaction.</param>
    /// <param name="repoCommandTimeoutInSeconds">Command timeout for the repository update.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdate(
        int migrationRecordId,
        MigrationStatus migrationStatus,
        int fileUpBlocksMigrated,
        DbConnection connection,
        DbTransaction transaction,
        int repoCommandTimeoutInSeconds)
    {
        var templateType = TemplateType.Repository_MigrationRecord_Update;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdate;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationRecordId} - Block {Block} - Status {Status} (atomic shared connection){MigrationContext}",
            migrationRecordId, fileUpBlocksMigrated, migrationStatus, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRecordId", migrationRecordId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)migrationStatus, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksMigrated", fileUpBlocksMigrated, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, connection, transaction, repoCommandTimeoutInSeconds, dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Updates a MigrationRecord entry with rollback (FileDown*) fields and status using a caller-provided
    /// shared connection and transaction for atomic execution with target rollback blocks.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdateRollback(
        int migrationRecordId,
        MigrationStatus migrationStatus,
        string fileDownHash,
        string? fileDownConfigHash,
        string fileDownBlocksHash,
        int fileDownBlocksMigrated,
        int fileDownBlocksTotal,
        string? fileDownConfigJson,
        DbConnection connection,
        DbTransaction transaction,
        int repoCommandTimeoutInSeconds)
    {
        var templateType = TemplateType.Repository_MigrationRecord_UpdateRollback;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdateRollback;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationRecordId} rollback - Block {Block}/{Total} - Status {Status} (atomic shared connection){MigrationContext}",
            migrationRecordId, fileDownBlocksMigrated, fileDownBlocksTotal, migrationStatus, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRecordId", migrationRecordId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)migrationStatus, typeof(byte)));
        dalParameterList.AddParameter(new DalParameter("FileDownHash", fileDownHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownConfigHash", fileDownConfigHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksHash", fileDownBlocksHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksMigrated", fileDownBlocksMigrated, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileDownBlocksTotal", fileDownBlocksTotal, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileDownConfigJson", fileDownConfigJson ?? "{}", typeof(string)));
        // The run performing the rollback and what it does (MigrateDown or Rollback): the history row is attributed to
        // this run while the record keeps the MigrationRunId of the run that created it (#13)
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", _ctxAccessor.Current.MigrationState.MigrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationOperationId", (byte)_ctxAccessor.Current.MigrationState.MigrationOperation, typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, connection, transaction, repoCommandTimeoutInSeconds, dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Selects all MigrationRecord entries for the current product and environment.
    /// Returns a list of MigrationRecord objects for comparison with files on disk.
    /// </summary>
    /// <remarks>
    /// MigrationRecord rows are only ever written by runs in <see cref="MigrationRunMode.Migrate"/> mode
    /// (see <c>MigrationRunModeExtensions.ShouldWriteRepository</c>), so the query always filters on
    /// <see cref="MigrationRunMode.Migrate"/> regardless of the run mode of the current command.
    /// Deriving the filter from the context run mode made every command that runs in another mode
    /// (e.g. <c>validate-hash</c> in <see cref="MigrationRunMode.Validate"/>) see an empty repository (#5).
    /// </remarks>
    /// <returns>List of MigrationRecord objects from the repository.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public List<MigrationRecord> RepositoryMigrationSelect()
    {
        const MigrationRunMode recordRunMode = MigrationRunMode.Migrate;
        var templateType = TemplateType.Repository_MigrationRecord_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationSelect;

        _logger.LogDebug(eventId, "Selecting migrations for product {ProductId} with environment {Environment} ({EnvironmentId}) with run mode {RunMode} (current command runs in {CurrentRunMode}){MigrationContext}",
            _ctxAccessor.Current.MigrationState.ProductId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, _ctxAccessor.Current.MigrationState.EnvironmentId, recordRunMode, _ctxAccessor.Current.RayMigratorConsoleOptions.RunMode, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunModeId", (byte)recordRunMode, typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{_repositoryDal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        var records = rows.Select(MapMigrationRecord).ToList();

        _logger.LogDebug(eventId, "Found {Count} migration records in repository{MigrationContext}", records.Count, _ctxAccessor.Current.Clone);
        return records;
    }

    /// <summary>
    /// Selects the terminal state transitions (MigrationRecordHistory rows) of all records of the current product and
    /// environment, ordered by history id. <c>Id</c> is the MigrationRecordId, <c>MigrationRunId</c> the run that caused
    /// the transition: migrate-down and error-recovery rollbacks change records that belong to another run, so the
    /// info run history is built from these rows instead of the records (#13).
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public List<MigrationRecord> RepositoryMigrationRecordHistorySelect()
    {
        const MigrationRunMode recordRunMode = MigrationRunMode.Migrate;
        var templateType = TemplateType.Repository_MigrationRecordHistory_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRecordHistorySelect;

        _logger.LogDebug(eventId, "Selecting migration record history for product {ProductId} with environment {Environment} ({EnvironmentId}){MigrationContext}",
            _ctxAccessor.Current.MigrationState.ProductId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, _ctxAccessor.Current.MigrationState.EnvironmentId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationRunModeId", (byte)recordRunMode, typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{_repositoryDal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        var records = rows.Select(MapMigrationRecord).ToList();

        _logger.LogDebug(eventId, "Found {Count} migration record history rows in repository{MigrationContext}", records.Count, _ctxAccessor.Current.Clone);
        return records;
    }

    /// <summary>
    /// Maps one row of Repository_MigrationRecord_Select or Repository_MigrationRecordHistory_Select (same column
    /// contract) to a <see cref="MigrationRecord"/>.
    /// </summary>
    internal static MigrationRecord MapMigrationRecord(Dictionary<string, object?> row) => new()
    {
        Id = Convert.ToInt32(row["Id"]),
        ProductId = Convert.ToInt32(row["ProductId"]),
        MigrationRunId = Convert.ToInt32(row["MigrationRunId"]),
        MigrationOperationId = (MigrationOperation)Convert.ToByte(row["MigrationOperationId"]),
        MigrationStatusId = (MigrationStatus)Convert.ToByte(row["MigrationStatusId"]),
        ReleaseVersion = row["ReleaseVersion"]?.ToString() ?? string.Empty,
        TargetGroupAlias = row["TargetGroupAlias"]?.ToString() ?? string.Empty,
        TargetAlias = row["TargetAlias"]?.ToString() ?? string.Empty,
        Filename = row["Filename"]?.ToString() ?? string.Empty,
        FileOrderId = Convert.ToInt32(row["FileOrderId"]),
        FileUpHash = row["FileUpHash"]?.ToString() ?? string.Empty,
        FileUpConfigHash = row["FileUpConfigHash"]?.ToString(),
        FileUpBlocksHash = row["FileUpBlocksHash"]?.ToString() ?? string.Empty,
        FileUpBlocksMigrated = Convert.ToInt32(row["FileUpBlocksMigrated"]),
        FileUpBlocksTotal = Convert.ToInt32(row["FileUpBlocksTotal"]),
        MigrateDownFileExists = Convert.ToBoolean(row["MigrateDownFileExists"]),
        FileDownHash = row["FileDownHash"]?.ToString(),
        FileDownConfigHash = row["FileDownConfigHash"]?.ToString(),
        FileDownBlocksHash = row["FileDownBlocksHash"]?.ToString(),
        FileDownBlocksMigrated = row["FileDownBlocksMigrated"] != null ? Convert.ToInt32(row["FileDownBlocksMigrated"]) : null,
        FileDownBlocksTotal = row["FileDownBlocksTotal"] != null ? Convert.ToInt32(row["FileDownBlocksTotal"]) : null,
    };

    /// <summary>
    /// Updates the hash fields of an existing MigrationRecord entry.
    /// Used by the Update-Hash command to synchronize repository hashes with changed files on disk.
    /// </summary>
    /// <param name="migrationRecordId">The migration record ID to update.</param>
    /// <param name="fileUpHash">New SHA256 hash of the entire file.</param>
    /// <param name="fileUpConfigHash">New SHA256 hash of the TOML config section.</param>
    /// <param name="fileUpBlocksHash">New SHA256 hash of the SQL blocks.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public void RepositoryMigrationUpdateHash(
        int migrationRecordId,
        string fileUpHash,
        string? fileUpConfigHash,
        string fileUpBlocksHash)
    {
        var templateType = TemplateType.Repository_MigrationRecord_UpdateHash;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationUpdateHash;

        _logger.LogDebug(eventId, "Update MigrationRecord-entry {MigrationRecordId} hashes{MigrationContext}",
            migrationRecordId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRecordId", migrationRecordId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("FileUpHash", fileUpHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpConfigHash", fileUpConfigHash, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("FileUpBlocksHash", fileUpBlocksHash, typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        ExecuteScalarWithNegativeResultCodeException(template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId);
    }

    /// <summary>
    /// Selects MigrationRun records for the current product.
    /// Returns a list of MigrationRunInfo-compatible data for history display.
    /// </summary>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <returns>List of dictionaries with MigrationRun data from the repository.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public List<Dictionary<string, object?>> RepositoryMigrationRunSelect(int limit)
    {
        var templateType = TemplateType.Repository_MigrationRun_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunSelect;

        _logger.LogDebug(eventId, "Selecting MigrationRun records for product {ProductId} and environment {EnvironmentId} with limit {Limit}{MigrationContext}",
            _ctxAccessor.Current.MigrationState.ProductId, _ctxAccessor.Current.MigrationState.EnvironmentId, limit, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("Limit", limit, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{_repositoryDal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        _logger.LogDebug(eventId, "Found {Count} MigrationRun records{MigrationContext}", rows.Count, _ctxAccessor.Current.Clone);
        return rows;
    }

    /// <summary>
    /// Executes a template as ExecuteScalar and validates the result.
    /// Throws TemplateExecutionException on negative result codes.
    /// </summary>
    public TemplateResponse ExecuteScalarWithNegativeResultCodeException(Template template, IDal dal, DalSettings dalSettings, DalParameterList? dalParameterList, ILogger? logger = null, EventId? eventId = null)
    {
        object? returnValue;
        
        try
        {
            returnValue = dal.ExecuteScalarAsync(template.Content, dalSettings, dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{dal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }        
        
        TemplateResponse templateResponse = GetValidatedTemplateResponseFromExecuteScalar(returnValue, template);

        if (logger != null)
        {
            logger.LogDebug((EventId) eventId!, "Execution of Template [{TemplateInfo}] returned ResultCode [{ResultCode}] and ResultMessage [{ResultMessage}]{MigrationContext}", template, templateResponse.ResultCode, templateResponse.ResultMessage, _ctxAccessor.Current.Clone);
        }
        
        return templateResponse;
    }

    /// <summary>
    /// Executes a template as ExecuteScalar on a caller-provided shared connection and transaction,
    /// then validates the result. Used for atomic shared-connection execution where the caller
    /// controls the connection/transaction lifecycle.
    /// Throws TemplateExecutionException on negative result codes.
    /// </summary>
    public TemplateResponse ExecuteScalarWithNegativeResultCodeException(
        Template template, IDal dal, DbConnection connection, DbTransaction transaction,
        int commandTimeoutInSeconds, DalParameterList? dalParameterList,
        ILogger? logger = null, EventId? eventId = null)
    {
        object? returnValue;

        try
        {
            returnValue = dal.ExecuteScalarAsync(template.Content, connection, transaction, commandTimeoutInSeconds, dalParameterList).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error executing template [{template}] with dal for DB [{dal.DatabaseType}]";
            throw new TemplateExecutionException(errorMessage, ex);
        }

        TemplateResponse templateResponse = GetValidatedTemplateResponseFromExecuteScalar(returnValue, template);

        if (logger != null)
        {
            logger.LogDebug((EventId) eventId!, "Execution of Template [{TemplateInfo}] returned ResultCode [{ResultCode}] and ResultMessage [{ResultMessage}]{MigrationContext}", template, templateResponse.ResultCode, templateResponse.ResultMessage, _ctxAccessor.Current.Clone);
        }

        return templateResponse;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="inputObject"></param>
    /// <param name="template"></param>
    /// <returns></returns>
    internal static TemplateResponse GetValidatedTemplateResponseFromExecuteScalar(object? inputObject, Template template)
    {
        if (inputObject == null || inputObject.ToString() == null)
        {
            throw new TemplateResultException($"Error executing template {template}: Execution of template returned [null] as a result");
        }

        string resultString = inputObject.ToString()!.Trim();
        if (string.IsNullOrEmpty(resultString))
        {
            throw new TemplateResultException($"Error executing template {template}: Execution returned empty string as result");
        }

        // Contract 'code[,code...],message': the leading integer tokens are the codes, everything after them is
        // the message. Messages contain no commas by contract; one that does is still kept intact because the
        // split stops at the first token that is not an integer (#27).
        string[] tokens = resultString.Split(',');
        List<int> codes = [];
        int messageStart = 0;
        while (messageStart < tokens.Length
               && int.TryParse(tokens[messageStart].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
        {
            codes.Add(code);
            messageStart++;
        }

        if (codes.Count == 0)
        {
            throw new TemplateResultException($"Error executing template {template}: Execution returned incorrect result. " +
                                                $"The first (or only) part of [{resultString}], which is [{tokens[0].Trim()}], needs to be converted into an integer-value. This was NOT possible!");
        }

        TemplateResponse templateResponse = new()
        {
            ResultCode = codes[0],
            ResultCodes = codes.ToArray(),
            ResultMessage = string.Join(',', tokens.Skip(messageStart)).Trim()
        };
        int resultCode = templateResponse.ResultCode;

        if (resultCode < 0)
        {
            // Template returns a negative result => error
            string errorMessage = $"Error executing template {template}. Template-execution returned a negative ResultCode [{templateResponse.ResultCode}] with ErrorMessage: {templateResponse.ResultMessage}.";

            if (TemplateResultCode.IsKnown(resultCode))
                throw new TemplateResultException(errorMessage, templateResponse.ResultCode);
            else
                throw new UndefinedTemplateResultException(errorMessage, templateResponse.ResultCode);
        }
        
        // Everything OK
        return templateResponse;
    }

}