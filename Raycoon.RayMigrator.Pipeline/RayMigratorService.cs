using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Pipeline;

/// <summary>
/// Console client service that acts as a bridge between the command-line interface and the service layer
/// </summary>
public class RayMigratorService
{
    private readonly ILogger<RayMigratorService> _logger;
    private readonly RayMigratorConsoleOptions _consoleOptions;
    private readonly IMigrationService _migrationService;

    /// <summary>
    /// Public constructor.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="consoleOptions"></param>
    /// <param name="migrationService"></param>
    public RayMigratorService(
        ILogger<RayMigratorService> logger,
        RayMigratorConsoleOptions consoleOptions,
        IMigrationService migrationService)
    {
        _logger = logger;
        _consoleOptions = consoleOptions;
        _migrationService = migrationService;
    }

    /// <summary>
    /// Executes the migration service based on the command specified
    /// </summary>
    /// <exception cref="ConfigurationValidationException"></exception>
    public async Task<int> DoWorkAsync(IHost host)
    {
        try
        {
            _logger.LogDebug("Executing command: {Command}", _consoleOptions.Command);

            switch (_consoleOptions.Command)
            {
                case MigrationCommand.MigrateUp:
                    return await ExecuteMigrateUpAsync();

                case MigrationCommand.MigrateDown:
                    return await ExecuteMigrateDownAsync();

                case MigrationCommand.ValidateHash:
                    return await ExecuteValidateHashAsync();

                case MigrationCommand.UpdateHash:
                    return await ExecuteUpdateHashAsync();

                case MigrationCommand.Info:
                    return await ExecuteInfoAsync();

                case MigrationCommand.Baseline:
                    return await ExecuteBaselineAsync();

                case MigrationCommand.FixIssues:
                    return await ExecuteFixIssuesAsync();

                default:
                    throw new ConfigurationValidationException(
                        $"Unknown command: {_consoleOptions.Command}. Valid commands: {string.Join(", ", Enum.GetNames<MigrationCommand>())}");
            }
        }
        catch (MigrationAlreadyRunningException ex)
        {
            _logger.LogError(ex, "Another migration is already running for this product");
            _logger.LogInformation(
                "To resolve this issue, either wait for the running migration to complete, " +
                "or use the Fix command to clean up orphaned runs: " +
                "raymigrator Fix --product {Product} --environment {Environment} --scope OrphanedRuns",
                _consoleOptions.Product, _consoleOptions.Environment);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {Command}", _consoleOptions.Command);
            return 1; // Return error code
        }
    }

    private async Task<int> ExecuteMigrateUpAsync()
    {
        _logger.LogDebug("Executing Migrate-Up command for product {Product} in environment {Environment}",
            _consoleOptions.Product, _consoleOptions.Environment);

        var request = new MigrateUpRequest
        {
            ProductAlias = _consoleOptions.Product!,
            Environment = _consoleOptions.Environment!,
            TargetReleaseVersion = _consoleOptions.TargetReleaseVersion,
            RunMode = _consoleOptions.RunMode,
            ShowInfo = _consoleOptions.ShowStartupInfo,
            RevealSensitiveData = _consoleOptions.RevealSensitiveData,
            AllowOutOfOrder = _consoleOptions.AllowOutOfOrder == true,
            TargetGroupAliases = _consoleOptions.TargetGroupAliases,
            TargetGroupMigrationOrder = _consoleOptions.TargetGroupMigrationOrder,
        };

        var result = await _migrationService.MigrateUpAsync(request);

        if (!result.Success)
        {
            _logger.LogError("Migrate-Up failed for product {Product}: {Error}",
                _consoleOptions.Product, result.ErrorMessage);
            return 1;
        }

        return 0;
    }

    private async Task<int> ExecuteMigrateDownAsync()
    {
        _logger.LogDebug("Executing Migrate-Down command for product {Product} to release {TargetRelease}",
            _consoleOptions.Product, _consoleOptions.TargetReleaseVersion);

        var request = new MigrateDownRequest
        {
            ProductAlias = _consoleOptions.Product!,
            Environment = _consoleOptions.Environment!,
            TargetReleaseVersion = _consoleOptions.TargetReleaseVersion!,
            RunMode = _consoleOptions.RunMode,
            ShowInfo = _consoleOptions.ShowStartupInfo,
            RevealSensitiveData = _consoleOptions.RevealSensitiveData,
            TargetGroupAliases = _consoleOptions.TargetGroupAliases,
        };

        var result = await _migrationService.MigrateDownAsync(request);

        if (!result.Success)
        {
            _logger.LogError("Migrate-Down failed for product {Product}: {Error}",
                _consoleOptions.Product, result.ErrorMessage);
            return 1;
        }

        return 0;
    }

    private async Task<int> ExecuteValidateHashAsync()
    {
        _logger.LogDebug("Executing Validate-Hash command for product {Product} with scope {Scope}",
            _consoleOptions.Product, _consoleOptions.HashValidationScope);

        var request = new ValidateHashRequest
        {
            ProductAlias = _consoleOptions.Product!,
            HashValidationScope = _consoleOptions.HashValidationScope,
            ShowInfo = _consoleOptions.ShowStartupInfo,
            RevealSensitiveData = _consoleOptions.RevealSensitiveData,
            TargetGroupAliases = _consoleOptions.TargetGroupAliases,
        };

        var result = await _migrationService.ValidateHashAsync(request);

        if (!result.Success)
        {
            _logger.LogError("Validate-Hash failed for product {Product}: {Error}",
                _consoleOptions.Product, result.ErrorMessage);
            return 1;
        }

        // Log validation results
        _logger.LogInformation("Validate-Hash completed. Total: {Total}, Valid: {Valid}, Invalid: {Invalid}, Missing: {Missing}",
            result.TotalFiles, result.ValidFiles, result.InvalidFiles, result.MissingFiles);

        foreach (var issue in result.Issues)
        {
            _logger.LogWarning("Hash issue: {File} - {Type}: {Details}",
                issue.FileName, issue.IssueType, issue.Details);
        }

        return result.InvalidFiles > 0 || result.MissingFiles > 0 ? 1 : 0;
    }

    private async Task<int> ExecuteUpdateHashAsync()
    {
        _logger.LogDebug("Executing Update-Hash command for product {Product}", _consoleOptions.Product);

        var request = new UpdateHashRequest
        {
            ProductAlias = _consoleOptions.Product!,
            ShowInfo = _consoleOptions.ShowStartupInfo,
            RevealSensitiveData = _consoleOptions.RevealSensitiveData,
            TargetGroupAliases = _consoleOptions.TargetGroupAliases,
        };

        var result = await _migrationService.UpdateHashAsync(request);

        if (!result.Success)
        {
            _logger.LogError("Update-Hash failed for product {Product}: {Error}",
                _consoleOptions.Product, result.ErrorMessage);
            return 1;
        }

        _logger.LogInformation("Update-Hash completed. Updated: {Updated}, New: {New}, Removed: {Removed}",
            result.UpdatedFiles, result.NewFiles, result.RemovedFiles);

        return 0;
    }

    private async Task<int> ExecuteBaselineAsync()
    {
        var releaseLabel = string.IsNullOrWhiteSpace(_consoleOptions.TargetReleaseVersion)
            ? "all releases"
            : $"up to release {_consoleOptions.TargetReleaseVersion}";

        _logger.LogDebug("Executing Baseline command for product {Product} ({ReleaseScope})",
            _consoleOptions.Product, releaseLabel);

        var request = new BaselineRequest
        {
            ProductAlias = _consoleOptions.Product!,
            Environment = _consoleOptions.Environment!,
            TargetReleaseVersion = _consoleOptions.TargetReleaseVersion,
            ShowInfo = _consoleOptions.ShowStartupInfo,
            RevealSensitiveData = _consoleOptions.RevealSensitiveData,
            TargetGroupAliases = _consoleOptions.TargetGroupAliases,
            TargetGroupMigrationOrder = _consoleOptions.TargetGroupMigrationOrder,
        };

        var result = await _migrationService.BaselineAsync(request);

        if (!result.Success)
        {
            _logger.LogError("Baseline failed for product {Product}: {Error}",
                _consoleOptions.Product, result.ErrorMessage);
            return 1;
        }

        _logger.LogInformation("Baseline completed for product {Product}: {Count} file(s) baselined ({ReleaseScope})",
            _consoleOptions.Product, result.BaselinedFiles, releaseLabel);
        return 0;
    }

    private async Task<int> ExecuteInfoAsync()
    {
        _logger.LogDebug("Executing Info command for product {Product} in environment {Environment}",
            _consoleOptions.Product, _consoleOptions.Environment);

        var status = await _migrationService.GetStatusAsync(_consoleOptions.Product!);
        var history = await _migrationService.GetHistoryAsync(_consoleOptions.Product!, 10);

        // Display status summary
        _logger.LogInformation("--- Migration Status for product {Product}, environment {Environment} ---", status.ProductAlias, _consoleOptions.Environment);
        _logger.LogInformation("  Current Release:     {Release}", status.CurrentRelease);
        _logger.LogInformation("  Pending Migrations:  {Pending}", status.PendingMigrations);
        _logger.LogInformation("  Total Executed:      {Total}", status.TotalMigrationsExecuted);

        // Display target groups
        if (status.TargetGroups.Count > 0)
        {
            _logger.LogInformation("--- Target Groups ---");
            foreach (var (alias, tg) in status.TargetGroups)
            {
                _logger.LogInformation("  [{Alias}] Type={DbType}, Release={Release}, Executed={Count}, Targets=[{Targets}]",
                    tg.Alias, tg.DatabaseType, tg.CurrentRelease, tg.ExecutedMigrations,
                    string.Join(", ", tg.Targets));
            }
        }

        // Display recent history
        if (history.Runs.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- Last {history.Runs.Count} Migration Runs ---");
            sb.AppendLine($"  {"RunId",7}  {"Command",-12}  {"RunMode",-10}  {"Result",-9}  {"# Migrations",12}  {"StartedAt",-19}  {"FinishedAt",-19}  {"DurationInMs",12}");
            sb.AppendLine($"  {new string('─', 7)}  {new string('─', 12)}  {new string('─', 10)}  {new string('─', 9)}  {new string('─', 12)}  {new string('─', 19)}  {new string('─', 19)}  {new string('─', 12)}");
            foreach (var run in history.Runs)
            {
                var finishedAt = run.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
                var durationInMs = (run.CompletedAt - run.StartedAt)?.TotalMilliseconds is { } ms
                    ? $"{ms,12:F0}"
                    : $"{"—",12}";
                sb.AppendLine($"  {run.MigrationRunId,7}  {run.Operation,-12}  {run.RunMode,-10}  {run.Result,-9}  {run.TotalMigrations,12}  {run.StartedAt:yyyy-MM-dd HH:mm:ss}  {finishedAt,-19}  {durationInMs}");
            }
            _logger.LogInformation("\n{MigrationRunHistory}", sb.ToString().TrimEnd());
        }
        else
        {
            _logger.LogDebug("No migration runs found.");
        }

        return 0;
    }

    private async Task<int> ExecuteFixIssuesAsync()
    {
        _logger.LogDebug("Executing Fix command for product {Product} in environment {Environment}",
            _consoleOptions.Product, _consoleOptions.Environment);

        var request = new FixIssuesRequest
        {
            ProductAlias = _consoleOptions.Product!,
            Environment = _consoleOptions.Environment!,
            Scope = _consoleOptions.FixIssues ?? FixIssues.OrphanedRuns,
            OlderThanMinutes = _consoleOptions.FixOlderThanMinutes ?? 60,
            DryRun = _consoleOptions.FixDryRun ?? false,
            AssumedMigrationStatus = _consoleOptions.FixAssumedMigrationStatus ?? MigrationStatus.NotMigrated,
            ShowInfo = _consoleOptions.ShowStartupInfo,
            RevealSensitiveData = _consoleOptions.RevealSensitiveData
        };

        var result = await _migrationService.FixIssuesAsync(request);

        if (!result.Success)
        {
            _logger.LogError("Fix command failed for product {Product}: {Error}",
                _consoleOptions.Product, result.ErrorMessage);
            return 1;
        }

        if (result.WasDryRun)
        {
            _logger.LogInformation("Fix dry-run completed for product {Product}: {Found} orphaned run(s) found",
                _consoleOptions.Product, result.OrphanedRunsFound);
        }
        else
        {
            _logger.LogInformation("Fix completed for product {Product}: {Fixed} orphaned run(s) fixed",
                _consoleOptions.Product, result.OrphanedRunsFixed);
        }

        return 0;
    }
}
