using System.Globalization;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// The retention templates of one repository (ADR-027), without a migration context:
/// <c>Repository_MigrationRecordHistory_Delete</c> and <c>DatabaseLogging_Delete</c> delete one batch of rows older than a
/// cutoff, the <c>_Count</c> templates count the same rows without deleting. Every template spares the rows of a run that
/// is still open (<c>MigrationRun.FinishedAt IS NULL</c>); a log row without a run falls by its age. The log templates run
/// against the repository database under the schema and prefix of the repository and join its <c>MigrationRun</c> table;
/// <c>DatabaseLogging_Count</c> also reports whether the log table is there. The repository maintenance service of the
/// service layer runs the batches.
/// </summary>
/// <remarks>
/// Every template runs with the command timeout of the repository and without a transaction of the DAL: a delete batch is
/// one statement in autocommit, which the database applies whole or not at all, also when the call is cancelled. It runs
/// without the retries of the repository: a statement that timed out, and on SQLite one that reports BUSY after its busy
/// timeout, which Microsoft.Data.Sqlite sets to the command timeout, would wait the whole command timeout again on every
/// retry, so that a time budget of a minute could turn into hours. A batch that fails is taken up by the next batch or the
/// next call, since every batch deletes the oldest rows that are left.
/// </remarks>
public sealed class RepositoryMaintenanceExecutor
{
    /// <summary>The format of the <c>@CutoffUtc</c> parameter: whole seconds, the precision SQLite, MySQL and MariaDB store.</summary>
    public const string CutoffFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly TemplateCache _templateCache;
    private readonly RepositoryOptions _repository;
    private readonly IDal _repositoryDal;
    private readonly DalSettings _dalSettings;

    /// <summary>Creates the executor for one repository and its DAL.</summary>
    /// <param name="templateCache">The templates of every database type of the process.</param>
    /// <param name="repository">The repository: database type, connection string, schema, prefix and command settings,
    /// with the defaults of the data annotations applied.</param>
    /// <param name="repositoryDal">The DAL of the repository's database type and connection string.</param>
    /// <exception cref="ArgumentException"><paramref name="repository"/> lacks a value the templates or the DAL settings need.</exception>
    public RepositoryMaintenanceExecutor(TemplateCache templateCache, RepositoryOptions repository, IDal repositoryDal)
    {
        ArgumentNullException.ThrowIfNull(templateCache);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(repositoryDal);
        RepositoryOptionsRequirements.ThrowIfIncomplete(repository, nameof(repository));

        _templateCache = templateCache;
        _repository = repository;
        _repositoryDal = repositoryDal;

        _dalSettings = repository.GetDalSettings();
        _dalSettings.MaxRetries = 0;
    }

    /// <summary>
    /// Counts the <c>MigrationRecordHistory</c> rows historized before <paramref name="cutoffUtc"/> that do not belong to an
    /// open run: the rows <see cref="DeleteHistoryAsync"/> deletes.
    /// </summary>
    /// <param name="cutoffUtc">The cutoff in UTC; it counts to the whole second (<see cref="FormatCutoff"/>).</param>
    /// <param name="cancellationToken">Reaches the DAL call.</param>
    /// <exception cref="TemplateExecutionException">The template failed on the database.</exception>
    /// <exception cref="TemplateResultException">The template returned an error code or no count.</exception>
    public async Task<long> CountHistoryAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var (rows, _) = await CountAsync(TemplateType.Repository_MigrationRecordHistory_Count, CutoffParameters(cutoffUtc), cancellationToken);
        return rows;
    }

    /// <summary>
    /// Deletes one batch of the rows <see cref="CountHistoryAsync"/> counts, the oldest first, and returns how many it
    /// deleted; fewer than <paramref name="batchSize"/> means no such row is left.
    /// </summary>
    /// <param name="cutoffUtc">The cutoff in UTC; it counts to the whole second (<see cref="FormatCutoff"/>).</param>
    /// <param name="batchSize">The most rows the one statement of the batch deletes.</param>
    /// <param name="cancellationToken">Reaches the DAL call; a cancelled statement rolls back.</param>
    /// <exception cref="TemplateExecutionException">The template failed on the database.</exception>
    /// <exception cref="TemplateResultException">The template returned an error code.</exception>
    public Task<int> DeleteHistoryAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
        => DeleteAsync(TemplateType.Repository_MigrationRecordHistory_Delete, cutoffUtc, batchSize, cancellationToken);

    /// <summary>
    /// Counts the <c>MigrationLog</c> rows created before <paramref name="cutoffUtc"/> that do not belong to an open run of
    /// the repository, the rows <see cref="DeleteLogAsync"/> deletes; null when the repository database has no log table
    /// under the schema and prefix of the repository, because the log is written elsewhere or not at all.
    /// </summary>
    /// <param name="cutoffUtc">The cutoff in UTC; it counts to the whole second (<see cref="FormatCutoff"/>).</param>
    /// <param name="cancellationToken">Reaches the DAL call.</param>
    /// <exception cref="TemplateExecutionException">The template failed on the database.</exception>
    /// <exception cref="TemplateResultException">The template returned an error code or no count.</exception>
    public async Task<long?> CountLogAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var (rows, logTableExists) = await RunLogCountAsync(cutoffUtc, existsOnly: false, cancellationToken);
        return logTableExists ? rows : null;
    }

    /// <summary>
    /// Whether the repository database has the log table under the schema and prefix of the repository, which
    /// <see cref="DeleteLogAsync"/> needs: <c>DatabaseLogging_Count</c> with <c>@ExistsOnly</c> = 1, which counts nothing,
    /// so that a cleanup of a large log does not read it whole before its first batch.
    /// </summary>
    /// <param name="cancellationToken">Reaches the DAL call.</param>
    /// <exception cref="TemplateExecutionException">The template failed on the database.</exception>
    /// <exception cref="TemplateResultException">The template returned an error code or not the code of the existence.</exception>
    public async Task<bool> LogTableExistsAsync(CancellationToken cancellationToken = default)
    {
        var (_, logTableExists) = await RunLogCountAsync(DateTime.UnixEpoch, existsOnly: true, cancellationToken);
        return logTableExists;
    }

    /// <summary>
    /// Deletes one batch of the rows <see cref="CountLogAsync"/> counts, the oldest first, and returns how many it deleted;
    /// fewer than <paramref name="batchSize"/> means no such row is left. Call it only for a log table that
    /// <see cref="LogTableExistsAsync"/> or <see cref="CountLogAsync"/> found.
    /// </summary>
    /// <param name="cutoffUtc">The cutoff in UTC; it counts to the whole second (<see cref="FormatCutoff"/>).</param>
    /// <param name="batchSize">The most rows the one statement of the batch deletes.</param>
    /// <param name="cancellationToken">Reaches the DAL call; a cancelled statement rolls back.</param>
    /// <exception cref="TemplateExecutionException">The template failed on the database.</exception>
    /// <exception cref="TemplateResultException">The template returned an error code.</exception>
    public Task<int> DeleteLogAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
        => DeleteAsync(TemplateType.DatabaseLogging_Delete, cutoffUtc, batchSize, cancellationToken);

    /// <summary>
    /// The <c>@CutoffUtc</c> parameter of the retention templates: the cutoff in invariant
    /// <c>yyyy-MM-dd HH:mm:ss</c>, cut to the whole second, so that a row is never deleted unless it is older than the
    /// cutoff. The templates convert it into the type of their column; SQLite compares the text of its ISO-8601 columns.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="cutoffUtc"/> is not a UTC time.</exception>
    public static string FormatCutoff(DateTime cutoffUtc)
    {
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException($"The cutoff must be a UTC time (DateTimeKind.Utc), not [{cutoffUtc.Kind}].", nameof(cutoffUtc));

        return cutoffUtc.ToString(CutoffFormat, CultureInfo.InvariantCulture);
    }

    private async Task<(long Rows, bool LogTableExists)> RunLogCountAsync(DateTime cutoffUtc, bool existsOnly, CancellationToken cancellationToken)
    {
        var parameters = CutoffParameters(cutoffUtc);
        parameters.AddParameter(new DalParameter("ExistsOnly", existsOnly ? 1 : 0, typeof(int)));
        var (rows, codes) = await CountAsync(TemplateType.DatabaseLogging_Count, parameters, cancellationToken);

        // The second code says whether the log table exists; without it the template does not keep the contract.
        if (codes.Length < 2)
        {
            throw new TemplateResultException(
                $"Template [{TemplateType.DatabaseLogging_Count}] of DatabaseType [{_repository.DatabaseType}] returned no second code; it must report whether the log table exists ('rows,1,message' or '0,0,message').");
        }

        return (rows, codes[1] == 1);
    }

    private async Task<int> DeleteAsync(TemplateType templateType, DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var parameters = CutoffParameters(cutoffUtc);
        parameters.AddParameter(new DalParameter("BatchSize", batchSize, typeof(int)));

        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        var response = await TemplateExecutor.ExecuteScalarAsync(template, _repositoryDal, _dalSettings, parameters, cancellationToken);
        return response.ResultCode;
    }

    /// <summary>
    /// Runs a count template and reads its codes. The first code is the row count, which may exceed the range of the
    /// <see cref="int"/> codes of the result contract, so it is read as a <see cref="long"/>; a result that does not start
    /// with a count is validated by the contract, which raises the error of a negative code.
    /// </summary>
    private async Task<(long Rows, int[] Codes)> CountAsync(TemplateType templateType, DalParameterList parameters, CancellationToken cancellationToken)
    {
        var template = _templateCache.GetRepositoryTemplate(templateType, _repository);
        object? value = await TemplateExecutor.ExecuteScalarValueAsync(template, _repositoryDal, _dalSettings, parameters, cancellationToken);
        return ParseCount(value, template);
    }

    /// <summary>
    /// Reads <c>'rows[,code...],message'</c>: the row count as a <see cref="long"/> and the codes after it; the first entry of
    /// the codes is the count cut to the <see cref="int"/> range, so that the indexes match the result contract.
    /// </summary>
    internal static (long Rows, int[] Codes) ParseCount(object? value, Template template)
    {
        string[] tokens = (value?.ToString() ?? string.Empty).Trim().Split(',');
        if (!long.TryParse(tokens[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long rows))
        {
            // Not a count: an error code, a message without code or nothing at all, which the contract turns into its exception.
            var response = TemplateExecutor.GetValidatedTemplateResponseFromExecuteScalar(value, template);
            throw new TemplateResultException(
                $"Error executing template {template}: the template returned [{response}] instead of a row count.", response.ResultCode);
        }

        var codes = new List<int> { (int)Math.Min(rows, int.MaxValue) };
        for (int i = 1; i < tokens.Length && int.TryParse(tokens[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code); i++)
            codes.Add(code);

        return (rows, codes.ToArray());
    }

    private static DalParameterList CutoffParameters(DateTime cutoffUtc)
    {
        var parameters = new DalParameterList();
        parameters.AddParameter(new DalParameter("CutoffUtc", FormatCutoff(cutoffUtc), typeof(string)));
        return parameters;
    }
}
