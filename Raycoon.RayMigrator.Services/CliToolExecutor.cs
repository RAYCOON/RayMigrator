
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Services;

/// <summary>
/// Request model for CLI tool execution.
/// </summary>
public class CliToolExecutionRequest
{
    public required string ExecutablePath { get; init; }
    public required string Arguments { get; init; }
    public required CliToolInputMode InputMode { get; init; }
    public string? FileContent { get; init; }
    public required string FilePath { get; init; }
    public required string Filename { get; init; }
    public required int TimeoutInSeconds { get; init; }
    public required ExitCodeMatcher ExitCodeMatcher { get; init; }
}

/// <summary>
/// Result model for CLI tool execution.
/// </summary>
public class CliToolExecutionResult
{
    public required bool Success { get; init; }
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Executes external SQL CLI tools (sqlcmd, psql, mysql, mariadb, sqlite3) as an alternative to DAL-based SQL execution.
/// </summary>
public interface ICliToolExecutor
{
    Task<CliToolExecutionResult> ExecuteAsync(CliToolExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="ICliToolExecutor"/> using System.Diagnostics.Process.
/// </summary>
public class CliToolExecutor : ICliToolExecutor
{
    private readonly ILogger<CliToolExecutor> _logger;

    public CliToolExecutor(ILogger<CliToolExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<CliToolExecutionResult> ExecuteAsync(CliToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            Arguments = request.Arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.InputMode == CliToolInputMode.Stdin
        };

        // Note: Arguments are NOT logged because they may contain passwords from CliToolParameters.
        // Only the executable path and filename are logged for traceability.
        _logger.LogDebug(
            "Executing CLI tool: {ExecutablePath} (InputMode={InputMode}, Timeout={Timeout}s, File={Filename})",
            request.ExecutablePath, request.InputMode, request.TimeoutInSeconds, request.Filename);

        var stopwatch = Stopwatch.StartNew();
        Process process;

        try
        {
            process = Process.Start(psi)
                      ?? throw new CliToolExecutionException(
                          $"Process.Start returned null for '{request.ExecutablePath}'.",
                          request.ExecutablePath);
        }
        catch (CliToolExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to start CLI tool {ExecutablePath}", request.ExecutablePath);
            throw new CliToolExecutionException(
                $"Failed to start CLI tool '{request.ExecutablePath}': {ex.Message}",
                request.ExecutablePath, ex);
        }

        try
        {
            // If Stdin mode: write file content and close stdin
            if (request.InputMode == CliToolInputMode.Stdin && request.FileContent != null)
            {
                await process.StandardInput.WriteAsync(request.FileContent);
                process.StandardInput.Close();
            }

            // Read stdout and stderr BEFORE WaitForExitAsync to prevent deadlocks
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            // Wait for process with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutInSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout reached (not caller cancellation)
                _logger.LogWarning("CLI tool {ExecutablePath} timed out after {Timeout}s for {Filename}, killing process",
                    request.ExecutablePath, request.TimeoutInSeconds, request.Filename);
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort kill
                }

                throw new CliToolTimeoutException(
                    $"CLI tool '{request.ExecutablePath}' timed out after {request.TimeoutInSeconds} seconds while executing '{request.Filename}'.",
                    request.ExecutablePath, request.TimeoutInSeconds);
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            stopwatch.Stop();

            // Log output
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                _logger.LogDebug("CLI tool stdout for {Filename}:\n{StdOut}", request.Filename, stdout);
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogWarning("CLI tool stderr for {Filename}:\n{StdErr}", request.Filename, stderr);
            }

            // Evaluate exit code against whitelist
            int exitCode = process.ExitCode;
            bool isSuccess = request.ExitCodeMatcher.IsMatch(exitCode);

            string? errorMessage = null;
            if (!isSuccess)
            {
                errorMessage = $"CLI tool '{request.ExecutablePath}' exited with code {exitCode} " +
                               $"(not in SuccessExitCodes {request.ExitCodeMatcher}) while executing '{request.Filename}'.";

                _logger.LogWarning(
                    "CLI tool {ExecutablePath} returned non-success exit code {ExitCode} for {Filename}. " +
                    "SuccessExitCodes: {SuccessCodes}",
                    request.ExecutablePath, exitCode, request.Filename,
                    request.ExitCodeMatcher.ToString());
            }

            _logger.LogInformation(
                "CLI tool {ExecutablePath} completed for {Filename}: ExitCode={ExitCode}, Success={Success}, Duration={Duration}ms",
                request.ExecutablePath, request.Filename, exitCode, isSuccess, stopwatch.ElapsedMilliseconds);

            return new CliToolExecutionResult
            {
                Success = isSuccess,
                ExitCode = exitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                Duration = stopwatch.Elapsed,
                ErrorMessage = errorMessage
            };
        }
        finally
        {
            process.Dispose();
        }
    }
}
