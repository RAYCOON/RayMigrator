namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// Main interface for migration operations that can be used by any client (Console, Web, API, etc.)
/// </summary>
/// <remarks>
/// A cancelled run stops at the next block boundary and returns its result instead of throwing: migrate-up,
/// migrate-down and baseline end <c>Success = false</c> with <c>MigrationRunResult.Cancelled</c> (or <c>Error</c>,
/// when the rollback a cancelled migrate-up started did not complete cleanly, or when the migrate-up failed at a file
/// before the cancel stopped its error recovery), update-hash and fix with the error
/// code <c>TemplateResultCode.Cancelled</c>. The read commands (validate-hash, status, history) throw
/// <see cref="OperationCanceledException"/>. The token of a call cancels with
/// <c>MigrationCancelledAction.Terminate</c>; a host that lets the user choose the action passes a
/// <see cref="RunCancellation"/> in the request.
/// </remarks>
public interface IMigrationService
{
    /// <summary>
    /// Executes forward migrations up to a specified release
    /// </summary>
    /// <param name="request">Migration request parameters</param>
    /// <param name="cancellationToken">Stops the run at the next block boundary with <c>MigrationCancelledAction.Terminate</c>.</param>
    /// <returns>Result of the migration operation</returns>
    Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes rollback migrations down to a specified release
    /// </summary>
    /// <param name="request">Rollback request parameters</param>
    /// <param name="cancellationToken">Stops the rollback at the next rollback block; the next migrate-down continues it.</param>
    /// <returns>Result of the rollback operation</returns>
    Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates migration file integrity using hashes
    /// </summary>
    /// <param name="request">Validation request parameters</param>
    /// <param name="cancellationToken">Stops the validation; the call throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>Result of the validation operation</returns>
    Task<ValidationResult> ValidateHashAsync(ValidateHashRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates stored hashes for migration files
    /// </summary>
    /// <param name="request">Update request parameters</param>
    /// <param name="cancellationToken">Stops the update before the next record; the result names the records updated so far.</param>
    /// <returns>Result of the update operation</returns>
    Task<HashUpdateResult> UpdateHashAsync(UpdateHashRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all migration files up to a release as migrated without executing SQL.
    /// Used for onboarding existing databases into RayMigrator.
    /// </summary>
    /// <param name="request">Baseline request parameters</param>
    /// <param name="cancellationToken">Stops the baseline before the next file; the records written so far stay.</param>
    /// <returns>Result of the baseline operation</returns>
    Task<BaselineResult> BaselineAsync(BaselineRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current migration status for a product
    /// </summary>
    /// <param name="productAlias">Product identifier</param>
    /// <param name="cancellationToken">Stops the query; the call throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>Current migration status</returns>
    Task<MigrationStatusInfo> GetStatusAsync(string productAlias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets migration history for a product
    /// </summary>
    /// <param name="productAlias">Product identifier</param>
    /// <param name="limit">Number of records to return</param>
    /// <param name="cancellationToken">Stops the query; the call throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>Migration history</returns>
    Task<MigrationHistory> GetHistoryAsync(string productAlias, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fixes repository inconsistencies such as orphaned MigrationRun entries
    /// </summary>
    /// <param name="request">Fix request parameters</param>
    /// <param name="cancellationToken">Stops the fix before the next run; the result names the runs fixed so far.</param>
    /// <returns>Result of the fix operation</returns>
    Task<FixIssuesResult> FixIssuesAsync(FixIssuesRequest request, CancellationToken cancellationToken = default);
}
