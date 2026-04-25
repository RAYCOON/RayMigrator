
namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// Main interface for migration operations that can be used by any client (Console, Web, API, etc.)
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Executes forward migrations up to a specified release
    /// </summary>
    /// <param name="request">Migration request parameters</param>
    /// <returns>Result of the migration operation</returns>
    Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request);
    
    /// <summary>
    /// Executes rollback migrations down to a specified release
    /// </summary>
    /// <param name="request">Rollback request parameters</param>
    /// <returns>Result of the rollback operation</returns>
    Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request);
    
    /// <summary>
    /// Validates migration file integrity using hashes
    /// </summary>
    /// <param name="request">Validation request parameters</param>
    /// <returns>Result of the validation operation</returns>
    Task<ValidationResult> ValidateHashAsync(ValidateHashRequest request);
    
    /// <summary>
    /// Updates stored hashes for migration files
    /// </summary>
    /// <param name="request">Update request parameters</param>
    /// <returns>Result of the update operation</returns>
    Task<HashUpdateResult> UpdateHashAsync(UpdateHashRequest request);
    
    /// <summary>
    /// Marks all migration files up to a release as migrated without executing SQL.
    /// Used for onboarding existing databases into RayMigrator.
    /// </summary>
    /// <param name="request">Baseline request parameters</param>
    /// <returns>Result of the baseline operation</returns>
    Task<BaselineResult> BaselineAsync(BaselineRequest request);

    /// <summary>
    /// Gets the current migration status for a product
    /// </summary>
    /// <param name="productAlias">Product identifier</param>
    /// <returns>Current migration status</returns>
    Task<MigrationStatusInfo> GetStatusAsync(string productAlias);
    
    /// <summary>
    /// Gets migration history for a product
    /// </summary>
    /// <param name="productAlias">Product identifier</param>
    /// <param name="limit">Number of records to return</param>
    /// <returns>Migration history</returns>
    Task<MigrationHistory> GetHistoryAsync(string productAlias, int limit = 100);

    /// <summary>
    /// Fixes repository inconsistencies such as orphaned MigrationRun entries
    /// </summary>
    /// <param name="request">Fix request parameters</param>
    /// <returns>Result of the fix operation</returns>
    Task<FixIssuesResult> FixIssuesAsync(FixIssuesRequest request);
}