using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// Deletes old rows of the two tables that grow with every run, <c>MigrationRecordHistory</c> and <c>MigrationLog</c>,
/// from the repository options alone, without a product, an environment or a migration context (ADR-027): for hosts that
/// apply a retention policy to a repository (RayMigrator Studio). Registered by <c>AddRayMigratorServices</c>; needs the
/// host's <c>TemplateCache</c>. The engine reads neither table for a decision: every decision of a command reads
/// <c>MigrationRecord</c>, which the service never deletes, like <c>MigrationRun</c> and <c>MigrationRunMeta</c>. Only the
/// run history of info reads <c>MigrationRecordHistory</c>; a run whose history rows are gone reports no migrations there.
/// </summary>
/// <remarks>
/// <para>
/// The rows of a run that is still open (<c>MigrationRun.FinishedAt</c> is null) stay whatever their age, so a running
/// command, and an orphaned run until a later start or <c>fix</c> closes it, keeps its history and its log. A log row
/// without a run (<c>MigrationRunId</c> null) falls by its age.
/// </para>
/// <para>
/// The log is cleaned only where it shares the repository database and the schema and prefix of the repository, the
/// default home of the log; a log of its own elsewhere is reported as <see cref="RetentionResult.LogSkipped"/>. The
/// guard of the open runs reads the <c>MigrationRun</c> table of this repository: when another repository writes into
/// the same log table (same database, same schema and prefix), the open runs of that repository are not protected, and
/// its rows fall by their age.
/// </para>
/// <para>
/// The templates and the check of the schema version run with the command timeout of the repository but without its
/// retries (<c>DbCommandMaxRetries</c>): a statement that timed out, and on SQLite one that reports BUSY after its busy
/// timeout, would wait the whole command timeout again on every retry, far beyond the time budget. The next batch and the
/// next call are the retry; every batch deletes the oldest rows that are left.
/// </para>
/// <para>
/// Serialising the cleanups of one repository is the host's business. Two cleanups that run at once do not harm each
/// other: both delete only rows that match, and a batch whose rows the other cleanup deleted first deletes fewer, which
/// may end that cleanup as completed while the other one deletes the rest.
/// </para>
/// </remarks>
public interface IRepositoryMaintenanceService
{
    /// <summary>
    /// Deletes the history rows historized before <see cref="RetentionRequest.HistoryCutoffUtc"/> and the log rows created
    /// before <see cref="RetentionRequest.LogCutoffUtc"/>, the history first, in batches of
    /// <see cref="RetentionRequest.BatchSize"/> rows, the oldest first, until a batch finds fewer rows than it may delete
    /// or the time budget is spent, or counts the same rows without deleting (<see cref="RetentionRequest.DryRun"/>).
    /// Every batch is one statement that commits on its own, so the rows deleted so far stay deleted when the cleanup
    /// stops early; a later call continues. The documented defaults are written into the null command settings of
    /// <paramref name="repository"/>; a null <c>TableBaseName</c> becomes the empty prefix.
    /// </summary>
    /// <param name="repository">The repository: database type, connection string, schema, prefix and command settings.</param>
    /// <param name="request">The cutoffs, the batch size, the time budget and whether to count only.</param>
    /// <param name="cancellationToken">Checked before every batch and handed to the batch in progress, which the providers
    /// that can interrupt a statement (SQL Server, PostgreSQL, MySQL, MariaDB) abort and roll back; SQLite runs a batch
    /// that has started to its end. Once the cleanup has started to delete, a cancellation returns the rows deleted so far
    /// with <see cref="RetentionResult.Completed"/> false; during the checks before that, and in a dry run, it ends the
    /// call with an <see cref="OperationCanceledException"/>.</param>
    /// <exception cref="ArgumentException">Both cutoffs are null, a cutoff is not a UTC time, the batch size is not
    /// positive or the time budget is negative.</exception>
    /// <exception cref="Raycoon.RayMigrator.Shared.Exceptions.ConfigurationValidationException">The options are incomplete
    /// or invalid, the database type is unknown or has no templates, or the schema name is missing where the engine needs one.</exception>
    /// <exception cref="Raycoon.RayMigrator.Shared.Exceptions.RepositorySchemaException">The database has no repository, a
    /// repository without a schema version (created before 0.17.0), or one of a schema version newer than this engine
    /// supports. A repository of an older schema version is cleaned as it is and not upgraded.</exception>
    /// <exception cref="Raycoon.RayMigrator.Shared.Exceptions.TemplateExecutionException">A template failed on the database before
    /// a batch deleted rows. A failure after that ends the call with the rows deleted so far, <see cref="RetentionResult.Completed"/>
    /// false and the error in <see cref="RetentionResult.Message"/>.</exception>
    Task<RetentionResult> CleanUpAsync(RepositoryOptions repository, RetentionRequest request, CancellationToken cancellationToken = default);
}
