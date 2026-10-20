using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// The values the repository templates and the DAL settings of a repository need, checked by the executors that work
/// on a repository without a migration context (<see cref="RepositorySchemaExecutor"/>,
/// <see cref="RepositoryMaintenanceExecutor"/>). The data annotations of the options write the documented defaults into
/// empty settings; the repository services of the service layer apply them before they create an executor.
/// </summary>
internal static class RepositoryOptionsRequirements
{
    /// <summary>
    /// Throws when <paramref name="repository"/> lacks a value the templates or the DAL settings need.
    /// </summary>
    /// <exception cref="ArgumentException">The database type, the connection string, the prefix (the empty string for
    /// none) or a command setting is missing.</exception>
    internal static void ThrowIfIncomplete(RepositoryOptions repository, string paramName)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(repository.DatabaseType)) missing.Add(nameof(RepositoryOptions.DatabaseType));
        if (string.IsNullOrWhiteSpace(repository.ConnectionString)) missing.Add(nameof(RepositoryOptions.ConnectionString));
        if (repository.TableBaseName == null) missing.Add(nameof(RepositoryOptions.TableBaseName) + " (the empty string for no prefix)");
        if (repository.DbCommandTimeoutInSeconds == null) missing.Add(nameof(RepositoryOptions.DbCommandTimeoutInSeconds));
        if (repository.DbCommandMaxRetries == null) missing.Add(nameof(RepositoryOptions.DbCommandMaxRetries));
        if (repository.DbCommandWaitTimeInMsBeforeRetry == null) missing.Add(nameof(RepositoryOptions.DbCommandWaitTimeInMsBeforeRetry));
        if (missing.Count > 0)
            throw new ArgumentException($"The repository options lack {string.Join(", ", missing)}; apply the data annotations' defaults first, as the repository services do.", paramName);
    }
}
