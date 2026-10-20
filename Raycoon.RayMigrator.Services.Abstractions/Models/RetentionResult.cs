namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// What <see cref="IRepositoryMaintenanceService.CleanUpAsync"/> deleted, or counted in a dry run.
/// </summary>
/// <param name="HistoryRows">The <c>MigrationRecordHistory</c> rows deleted (in a dry run: that a cleanup would delete).</param>
/// <param name="LogRows">The <c>MigrationLog</c> rows deleted (in a dry run: that a cleanup would delete).</param>
/// <param name="Batches">The batches that deleted rows, over both tables; 0 in a dry run.</param>
/// <param name="Completed">True when no row older than its cutoff is left in the tables the request names, as far as a
/// batch could see; false when the time budget ran out, the cleanup was cancelled, or it failed after a batch had deleted
/// rows, so a later call deletes the rest. A dry run is always complete.</param>
/// <param name="LogSkipped">The request names a log cutoff, but the repository database has no log table under the
/// schema and prefix of the repository: the log is written elsewhere or not at all, and no log row was touched.</param>
/// <param name="Message">Why the cleanup stopped early (with the error of a failure), why the log was skipped, or that it only
/// counted; null when it deleted everything it was asked to.</param>
public sealed record RetentionResult(long HistoryRows, long LogRows, int Batches, bool Completed, bool LogSkipped, string? Message);
