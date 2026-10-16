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

/// <summary>
/// Runs the repository templates of a run against the repository DAL: every read and write of the repository tables
/// that the migration service makes, with the parameters taken from the migration context of the run.
/// </summary>
/// <remarks>
/// Every method that reaches the database takes a <see cref="CancellationToken"/> and hands it to the DAL. A failure on
/// the database becomes a <see cref="TemplateExecutionException"/>, a negative result code a
/// <see cref="TemplateResultException"/>; a call that fails after the token was cancelled ends with the DAL's
/// <see cref="OperationCanceledException"/> instead.
/// </remarks>
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
    /// (ADR-024): the schema steps of <see cref="RepositorySchemaExecutor"/> run with the repository, the target
    /// version and the engine version of the current context, and the <c>MigratorMetaId</c> they yield goes into
    /// the migration state of the run.
    /// </summary>
    /// <exception cref="RepositorySchemaException">The repository has no schema version but repository tables, or a
    /// schema version newer than this engine supports.</exception>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task RepositoryCheckCreateAsync(CancellationToken cancellationToken = default)
    {
        var context = _ctxAccessor.Current;
        var schema = new RepositorySchemaExecutor(_templateCache, _logger, _repository, _repositoryDal,
            context.RayMigratorVersion, context.RepositorySchemaTargetVersion, () => _ctxAccessor.Current.Clone);

        context.MigrationState.MigratorMetaId = (await schema.EnsureAsync(cancellationToken)).MigratorMetaId;
    }

    /// <summary>
    /// Checks for existing product-name and optionally creates a new Product-entry in the repository and returns the ProductId.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task RepositoryProductCheckInsertAsync(CancellationToken cancellationToken = default)
    {
        var templateType = TemplateType.Repository_Product_CheckInsert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryProductCheckInsert;
        string productName = _ctxAccessor.Current.RayMigratorConsoleOptions.Product;
        
        _logger.LogDebug(eventId, "Searching for product with name {ProductName}. Insert a new product-entry if name does not yet exist{MigrationContext}", productName , _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", productName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", productName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);

        _ctxAccessor.Current.MigrationState.ProductId = templateResponse.ResultCode;
    }

    /// <summary>
    /// Checks for existing environment name and optionally creates a new Environment entry in the repository and returns the EnvironmentId.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task RepositoryEnvironmentCheckInsertAsync(CancellationToken cancellationToken = default)
    {
        var templateType = TemplateType.Repository_Environment_CheckInsert;
        var eventId = MigrationEvent.TemplateExecutionRepositoryEnvironmentCheckInsert;
        string environmentName = _ctxAccessor.Current.RayMigratorConsoleOptions.Environment;

        _logger.LogDebug(eventId, "Searching for environment with name {EnvironmentName}. Insert a new environment entry if name does not yet exist{MigrationContext}", environmentName, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", environmentName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", environmentName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);

        _ctxAccessor.Current.MigrationState.EnvironmentId = templateResponse.ResultCode;
    }

    /// <summary>
    /// Looks up the ProductId of the current product without inserting anything.
    /// Used by run modes that must not write to the repository (Simulate).
    /// Sets <c>MigrationState.ProductId</c> to the id, or to 0 when the product is not registered yet.
    /// </summary>
    /// <returns>True when the product exists in the repository.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task<bool> RepositoryProductSelectAsync(CancellationToken cancellationToken = default)
    {
        var templateType = TemplateType.Repository_Product_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryProductSelect;
        string productName = _ctxAccessor.Current.RayMigratorConsoleOptions.Product;

        _logger.LogDebug(eventId, "Looking up product with name {ProductName} (read-only, no insert){MigrationContext}", productName, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", productName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", productName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);

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
    public async Task<bool> RepositoryEnvironmentSelectAsync(CancellationToken cancellationToken = default)
    {
        var templateType = TemplateType.Repository_Environment_Select;
        var eventId = MigrationEvent.TemplateExecutionRepositoryEnvironmentSelect;
        string environmentName = _ctxAccessor.Current.RayMigratorConsoleOptions.Environment;

        _logger.LogDebug(eventId, "Looking up environment with name {EnvironmentName} (read-only, no insert){MigrationContext}", environmentName, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("Name", environmentName, typeof(string)));
        dalParameterList.AddParameter(new DalParameter("NameLower", environmentName.ToLowerInvariant(), typeof(string)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);

        _ctxAccessor.Current.MigrationState.EnvironmentId = templateResponse.ResultCode;
        return templateResponse.ResultCode > 0;
    }

    /// <summary>
    /// OK: Creates a new MigrationRun in the database and inserts the settings JSON into MigrationRunMeta.
    /// </summary>
    /// <param name="migrationRunSettingsJson">JSON snapshot of all RayMigrator settings at migration start.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task RepositoryMigrationRunInsertAsync(string migrationRunSettingsJson, CancellationToken cancellationToken = default)
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
            var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
                template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);
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
    public async Task RepositoryMigrationRunUpdateAsync(
        MigrationRunResult runResult,
        string? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
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
        await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);

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
    /// Used by the Fix command and by the auto-fix of a start that finds another run open. Parameters are passed directly
    /// (not from MigrationContext).
    /// </summary>
    /// <param name="productId">The product ID to query.</param>
    /// <param name="environmentId">The environment ID to query.</param>
    /// <returns>List of raw row dictionaries with orphaned run data.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task<List<Dictionary<string, object?>>> RepositoryMigrationRunSelectOrphanedAsync(
        int productId,
        int environmentId,
        CancellationToken cancellationToken = default)
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
            rows = await _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
    public async Task<bool> RepositoryMigrationRunHeartbeatAsync(CancellationToken cancellationToken = default)
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

        // A beat is a liveness signal: it does not retry (the next tick is the retry), so a transient-error burst on the
        // repository neither silences the heartbeat for minutes nor holds the end of the run.
        var settings = _repository.GetDalSettings();
        settings.MaxRetries = 0;

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, settings, dalParameterList, null, eventId, cancellationToken);

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
    /// <returns>
    /// True when the run was closed. False when the run is alive: its heartbeat is younger than
    /// <paramref name="thresholdMinutes"/> (it beat between the orphan select and this call), the template answered
    /// <c>-31</c> and the row is untouched; the caller must leave the run's records alone as well.
    /// </returns>
    public async Task<bool> RepositoryMigrationRunFixOrphanedAsync(
        int migrationRunId,
        int thresholdMinutes,
        CancellationToken cancellationToken = default)
    {
        var templateType = TemplateType.Repository_MigrationRun_FixOrphaned;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationRunFixOrphaned;

        _logger.LogDebug(eventId, "Fixing orphaned MigrationRun {MigrationRunId} (heartbeat older than {ThresholdMinutes} minutes){MigrationContext}",
            migrationRunId, thresholdMinutes, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", migrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("ThresholdMinutes", thresholdMinutes, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        try
        {
            await ExecuteScalarWithNegativeResultCodeExceptionAsync(
                template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);
            return true;
        }
        catch (TemplateResultException ex) when (ex.ResultCode == TemplateResultCode.MigrationRunNotInRunningState)
        {
            _logger.LogInformation(eventId, "MigrationRun {MigrationRunId} wrote a heartbeat younger than {ThresholdMinutes} minutes meanwhile and stays open{MigrationContext}",
                migrationRunId, thresholdMinutes, _ctxAccessor.Current.Clone);
            return false;
        }
    }

    /// <summary>
    /// Settles the open MigrationRecord entries (Pending, Executing) of an orphaned MigrationRun that the caller has
    /// closed before (<see cref="RepositoryMigrationRunFixOrphanedAsync"/>). Used by the auto-fix of a parallel start and
    /// by the Fix command. Parameters are passed directly (not from MigrationContext).
    /// </summary>
    /// <param name="migrationRunId">The MigrationRun ID whose open entries to settle.</param>
    /// <param name="status">
    /// Null lets the template decide per entry from its progress, as a failure at the block that was running would have
    /// left it: Failed after some but not all committed blocks, so that the next migrate-up resumes after them; Migrated
    /// when every block committed; NotMigrated without a committed block; for a rollback that was running Failed with its
    /// rollback blocks, which migrate-down continues, Failed without rollback progress, or NotMigrated when every rollback
    /// block ran. A status (NotMigrated, Migrated) is set on every open entry. The executor hands null to the template as
    /// <c>@MigrationStatusId = 0</c>. The counters stay, FinishedAt is set to the time of the repair where it is empty,
    /// and DurationInMs stays as it is: a file that was running gets the time of the repair without a duration, a record
    /// whose rollback was running keeps the end and the duration of its last terminal status.
    /// </param>
    /// <returns>Number of updated MigrationRecord entries (0 = none found, which is not an error).</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task<int> RepositoryMigrationRecordFixOrphanedAsync(
        int migrationRunId,
        MigrationStatus? status,
        CancellationToken cancellationToken = default)
    {
        var templateType = TemplateType.Repository_MigrationRecord_FixOrphaned;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationFixOrphaned;

        _logger.LogDebug(eventId, "Fixing orphaned MigrationRecord entries for MigrationRunId {MigrationRunId} {Settlement}{MigrationContext}",
            migrationRunId, status is { } assumed ? $"with status {assumed}" : "by progress", _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("MigrationRunId", migrationRunId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("MigrationStatusId", (byte)(status ?? MigrationStatus.Undefined), typeof(byte)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);

        return templateResponse.ResultCode;
    }

    /// <summary>
    /// Checks for interrupted migrations that can be resumed.
    /// </summary>
    /// <returns>An InterruptedMigrationInfo object if an interrupted migration is found, null otherwise.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task<InterruptedMigrationInfo?> RepositoryMigrationGetInterruptedAsync(CancellationToken cancellationToken = default)
    {
        var templateType = TemplateType.Repository_MigrationRecord_GetInterrupted;
        var eventId = MigrationEvent.TemplateExecutionRepositoryMigrationGetInterrupted;

        _logger.LogDebug(eventId, "Checking for interrupted migrations for product {ProductId} with environment {Environment} ({EnvironmentId}){MigrationContext}",
            _ctxAccessor.Current.MigrationState.ProductId, _ctxAccessor.Current.RayMigratorConsoleOptions.Environment, _ctxAccessor.Current.MigrationState.EnvironmentId, _ctxAccessor.Current.Clone);

        DalParameterList dalParameterList = new DalParameterList();
        dalParameterList.AddParameter(new DalParameter("ProductId", _ctxAccessor.Current.MigrationState.ProductId, typeof(int)));
        dalParameterList.AddParameter(new DalParameter("EnvironmentId", _ctxAccessor.Current.MigrationState.EnvironmentId, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);

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
    public async Task<int> RepositoryMigrationInsertAsync(
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
        bool migrateDownFileExists,
        CancellationToken cancellationToken = default)
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
        var templateResponse = await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);

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
    /// block that failed (#11); with MigrationErrorAction=Ignore it ends before the first failed block, also when the
    /// file ran to its end, so a value equal to the block count always means that every block committed. While the file
    /// runs (<see cref="MigrationStatus.Executing"/>) it counts the same, so that a run that ends abruptly leaves a
    /// counter the next start can trust: a block that commits after a failed one does not count, and a file that a CLI
    /// tool runs counts 0.
    /// </param>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task RepositoryMigrationUpdateAsync(
        int migrationRecordId,
        MigrationStatus migrationStatus,
        int fileUpBlocksMigrated,
        CancellationToken cancellationToken = default)
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
        await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);
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
    public async Task RepositoryMigrationUpdateRollbackAsync(
        int migrationRecordId,
        MigrationStatus migrationStatus,
        string fileDownHash,
        string? fileDownConfigHash,
        string fileDownBlocksHash,
        int fileDownBlocksMigrated,
        int fileDownBlocksTotal,
        string? fileDownConfigJson,
        CancellationToken cancellationToken = default)
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
        await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);
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
    public async Task RepositoryMigrationUpdateAsync(
        int migrationRecordId,
        MigrationStatus migrationStatus,
        int fileUpBlocksMigrated,
        DbConnection connection,
        DbTransaction transaction,
        int repoCommandTimeoutInSeconds,
        CancellationToken cancellationToken = default)
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
        await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, connection, transaction, repoCommandTimeoutInSeconds, dalParameterList, _logger, eventId, cancellationToken);
    }

    /// <summary>
    /// Updates a MigrationRecord entry with rollback (FileDown*) fields and status using a caller-provided
    /// shared connection and transaction for atomic execution with target rollback blocks.
    /// </summary>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task RepositoryMigrationUpdateRollbackAsync(
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
        int repoCommandTimeoutInSeconds,
        CancellationToken cancellationToken = default)
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
        await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, connection, transaction, repoCommandTimeoutInSeconds, dalParameterList, _logger, eventId, cancellationToken);
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
    public async Task<List<MigrationRecord>> RepositoryMigrationSelectAsync(CancellationToken cancellationToken = default)
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
            rows = await _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
    public async Task<List<MigrationRecord>> RepositoryMigrationRecordHistorySelectAsync(CancellationToken cancellationToken = default)
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
            rows = await _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
    /// contract) to a <see cref="MigrationRecord"/>. <c>StartedAt</c> and <c>FinishedAt</c> may be absent: the selects of
    /// an external DAL written for an engine before 0.20.0 do not return them, and their rows map with null dates.
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
        StartedAt = ReadOptionalDateTime(row, "StartedAt"),
        FinishedAt = ReadOptionalDateTime(row, "FinishedAt"),
    };

    /// <summary>
    /// A date column of a repository row as a <see cref="DateTimeKind.Utc"/> value, or null when the column is NULL or
    /// absent. The repository stores UTC on every engine, and the rules are those of <c>RepositoryRowMapper</c> of the
    /// read model: a value without a kind (SQL Server, MySQL and MariaDB) is UTC, a local one is converted, SQLite text is
    /// parsed as UTC.
    /// </summary>
    private static DateTime? ReadOptionalDateTime(Dictionary<string, object?> row, string column)
    {
        if (!row.TryGetValue(column, out var value) || value == null || value == DBNull.Value)
            return null;

        return value switch
        {
            DateTime { Kind: DateTimeKind.Utc } utc => utc,
            DateTime { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
            DateTime unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc),
            DateTimeOffset offset => offset.UtcDateTime,
            string text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => DateTime.SpecifyKind(Convert.ToDateTime(value, CultureInfo.InvariantCulture), DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Updates the hash fields of an existing MigrationRecord entry.
    /// Used by the Update-Hash command to synchronize repository hashes with changed files on disk.
    /// </summary>
    /// <param name="migrationRecordId">The migration record ID to update.</param>
    /// <param name="fileUpHash">New SHA256 hash of the entire file.</param>
    /// <param name="fileUpConfigHash">New SHA256 hash of the TOML config section.</param>
    /// <param name="fileUpBlocksHash">New SHA256 hash of the SQL blocks.</param>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task RepositoryMigrationUpdateHashAsync(
        int migrationRecordId,
        string fileUpHash,
        string? fileUpConfigHash,
        string fileUpBlocksHash,
        CancellationToken cancellationToken = default)
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
        await ExecuteScalarWithNegativeResultCodeExceptionAsync(
            template, _repositoryDal, _repository.GetDalSettings(), dalParameterList, _logger, eventId, cancellationToken);
    }

    /// <summary>
    /// Selects MigrationRun records for the current product.
    /// Returns a list of MigrationRunInfo-compatible data for history display.
    /// </summary>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <returns>List of dictionaries with MigrationRun data from the repository.</returns>
    /// <exception cref="TemplateExecutionException"></exception>
    public async Task<List<Dictionary<string, object?>>> RepositoryMigrationRunSelectAsync(
        int limit,
        CancellationToken cancellationToken = default)
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
            rows = await _repositoryDal.ExecuteReaderAsync(template.Content, _repository.GetDalSettings(), dalParameterList, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
    public async Task<TemplateResponse> ExecuteScalarWithNegativeResultCodeExceptionAsync(
        Template template,
        IDal dal,
        DalSettings dalSettings,
        DalParameterList? dalParameterList,
        ILogger? logger = null,
        EventId? eventId = null,
        CancellationToken cancellationToken = default)
    {
        TemplateResponse templateResponse = await ExecuteScalarAsync(template, dal, dalSettings, dalParameterList, cancellationToken);

        if (logger != null)
        {
            logger.LogDebug((EventId) eventId!, "Execution of Template [{TemplateInfo}] returned ResultCode [{ResultCode}] and ResultMessage [{ResultMessage}]{MigrationContext}", template, templateResponse.ResultCode, templateResponse.ResultMessage, _ctxAccessor.Current.Clone);
        }
        
        return templateResponse;
    }

    /// <summary>
    /// Executes a template as ExecuteScalar and validates the result contract <c>'code[,code...],message'</c>; a
    /// failing execution becomes a <see cref="TemplateExecutionException"/>, a negative code a
    /// <see cref="TemplateResultException"/>, a cancellation the DAL's <see cref="OperationCanceledException"/>. No context
    /// and no logging: the schema steps run through it without a run.
    /// </summary>
    internal static async Task<TemplateResponse> ExecuteScalarAsync(
        Template template,
        IDal dal,
        DalSettings dalSettings,
        DalParameterList? dalParameterList,
        CancellationToken cancellationToken = default)
    {
        object? returnValue = await ExecuteScalarValueAsync(template, dal, dalSettings, dalParameterList, cancellationToken);

        return GetValidatedTemplateResponseFromExecuteScalar(returnValue, template);
    }

    /// <summary>
    /// Executes a template as ExecuteScalar and returns the scalar as the DAL read it, for a caller that validates the
    /// result itself (the retention counts, whose first code may exceed the range of an <see cref="int"/>); a failing
    /// execution becomes a <see cref="TemplateExecutionException"/>, a cancellation the DAL's
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    internal static async Task<object?> ExecuteScalarValueAsync(
        Template template,
        IDal dal,
        DalSettings dalSettings,
        DalParameterList? dalParameterList,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dal.ExecuteScalarAsync(template.Content, dalSettings, dalParameterList, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TemplateExecutionException($"Error executing template [{template}] with dal for DB [{dal.DatabaseType}]", ex);
        }
    }

    /// <summary>
    /// Executes a template as ExecuteScalar on a caller-provided shared connection and transaction,
    /// then validates the result. Used for atomic shared-connection execution where the caller
    /// controls the connection/transaction lifecycle.
    /// Throws TemplateExecutionException on negative result codes.
    /// </summary>
    public async Task<TemplateResponse> ExecuteScalarWithNegativeResultCodeExceptionAsync(
        Template template, IDal dal, DbConnection connection, DbTransaction transaction,
        int commandTimeoutInSeconds, DalParameterList? dalParameterList,
        ILogger? logger = null, EventId? eventId = null,
        CancellationToken cancellationToken = default)
    {
        object? returnValue;

        try
        {
            returnValue = await dal.ExecuteScalarAsync(
                template.Content, connection, transaction, commandTimeoutInSeconds, dalParameterList, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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
    /// Validates the scalar a template returned against the result contract <c>'code[,code...],message'</c>: the
    /// leading integer tokens are the codes, the rest is the message.
    /// </summary>
    /// <exception cref="TemplateResultException">The result is empty, starts with no integer, or carries a known negative
    /// code.</exception>
    /// <exception cref="UndefinedTemplateResultException">The result carries a negative code that is not catalogued.</exception>
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