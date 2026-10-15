using System.Diagnostics;
using System.Text;
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
    /// <summary>
    /// Runs the tool for one migration file and waits for it within the timeout of the request.
    /// </summary>
    /// <param name="request">The tool, its arguments and the file.</param>
    /// <param name="cancellationToken">
    /// Kills the tool and its child processes and throws <see cref="OperationCanceledException"/>. A tool that has exited
    /// when the cancel takes effect is judged by its exit code: a success keeps its result if the tool got the whole file;
    /// a failure, or a success before the tool got the whole file in Stdin mode, counts as the cancel.
    /// </param>
    /// <returns>
    /// The exit code, the output and whether the exit code counts as success. Without a cancel, a tool that exits or closes
    /// its standard input before it read the whole file in Stdin mode is judged by its exit code as well.
    /// </returns>
    /// <exception cref="CliToolExecutionException">
    /// The tool could not be started, or it ended with a success code in Stdin mode before it read the whole file and the
    /// caller did not cancel.
    /// </exception>
    /// <exception cref="CliToolTimeoutException">The tool ran longer than the timeout and was killed.</exception>
    /// <exception cref="OperationCanceledException">
    /// The caller cancelled: the tool was killed, or it failed after the cancel (Ctrl+C reaches the whole process group on
    /// Linux and macOS, so the tool gets the signal as well), or it ended after the cancel before it got the whole file in
    /// Stdin mode.
    /// </exception>
    Task<CliToolExecutionResult> ExecuteAsync(CliToolExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="ICliToolExecutor"/> using System.Diagnostics.Process.
/// </summary>
public class CliToolExecutor : ICliToolExecutor
{
    private readonly ILogger<CliToolExecutor> _logger;
    private readonly Func<Process, CancellationToken, Task> _waitForExitAsync;

    public CliToolExecutor(ILogger<CliToolExecutor> logger)
        : this(logger, static (process, cancellationToken) => process.WaitForExitAsync(cancellationToken))
    {
    }

    /// <summary>
    /// Creates the executor with the wait for the tool's exit replaced, so that a test can let a cancel overtake the exit
    /// of the tool, a race that real processes do not reproduce on demand.
    /// </summary>
    internal CliToolExecutor(ILogger<CliToolExecutor> logger, Func<Process, CancellationToken, Task> waitForExitAsync)
    {
        _logger = logger;
        _waitForExitAsync = waitForExitAsync;
    }

    /// <summary>
    /// Builds the process start info. In Stdin mode the content is piped as UTF-8 without BOM regardless of
    /// the console code page: without an explicit StandardInputEncoding .NET uses Console.InputEncoding, which
    /// is the OEM code page (e.g. 850) on a classic Windows console, so umlauts reached psql/mysql as cp850
    /// bytes and characters outside that code page became '?' (#4).
    /// </summary>
    internal static ProcessStartInfo CreateStartInfo(CliToolExecutionRequest request)
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

        if (psi.RedirectStandardInput)
            psi.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        return psi;
    }

    public async Task<CliToolExecutionResult> ExecuteAsync(CliToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var psi = CreateStartInfo(request);

        // Note: Arguments are NOT logged because they may contain passwords from CliToolParameters.
        // Only the executable path and filename are logged for traceability.
        _logger.LogDebug(
            "Executing CLI tool: {ExecutablePath} (InputMode={InputMode}, Timeout={Timeout}s, File={Filename})",
            request.ExecutablePath, request.InputMode, request.TimeoutInSeconds, request.Filename);

        // A run cancelled before the tool starts never starts it
        cancellationToken.ThrowIfCancellationRequested();

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
            Task<string>? stdoutTask = null;
            Task<string>? stderrTask = null;

            // Whether the tool did not get the whole file on its standard input: true in Stdin mode until the write of the file
            // completed, so that a write the cancel cut off leaves the input incomplete like a write that met a closed pipe
            bool inputIncomplete = request.InputMode == CliToolInputMode.Stdin && request.FileContent != null;

            try
            {
                // If Stdin mode: write file content and close stdin
                if (request.InputMode == CliToolInputMode.Stdin && request.FileContent != null)
                {
                    inputIncomplete = !await WriteInputAsync(process, request, cancellationToken);
                }

                // Read stdout and stderr BEFORE WaitForExitAsync to prevent deadlocks. The reads take no token: the
                // pipes close when the tool exits or is killed, and a cancel that arrives after the tool finished must
                // not discard what it did.
                stdoutTask = process.StandardOutput.ReadToEndAsync();
                stderrTask = process.StandardError.ReadToEndAsync();

                // Wait for process with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutInSeconds));

                await _waitForExitAsync(process, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout reached (not caller cancellation)
                _logger.LogWarning("CLI tool {ExecutablePath} timed out after {Timeout}s for {Filename}, killing process",
                    request.ExecutablePath, request.TimeoutInSeconds, request.Filename);
                KillProcessTree(process);

                throw new CliToolTimeoutException(
                    $"CLI tool '{request.ExecutablePath}' timed out after {request.TimeoutInSeconds} seconds while executing '{request.Filename}'.",
                    request.ExecutablePath, request.TimeoutInSeconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    // The caller cancelled: the tool must not run on after the run stopped
                    _logger.LogWarning("CLI tool {ExecutablePath} cancelled while executing {Filename}, killing process",
                        request.ExecutablePath, request.Filename);
                    KillProcessTree(process);

                    throw new OperationCanceledException(
                        $"CLI tool '{request.ExecutablePath}' was cancelled while executing '{request.Filename}'.", cancellationToken);
                }

                // The tool exited before the cancel took effect, which overtook its exit event: the exit code decides below
                _logger.LogDebug("CLI tool {ExecutablePath} exited before the cancel took effect for {Filename}",
                    request.ExecutablePath, request.Filename);
            }

            // The pipes are closed once the tool exited, so reads that had not started yet end at once
            var stdout = await (stdoutTask ?? process.StandardOutput.ReadToEndAsync());
            var stderr = await (stderrTask ?? process.StandardError.ReadToEndAsync());
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

            if (!isSuccess && cancellationToken.IsCancellationRequested)
            {
                // A tool that failed while the run was cancelled is reported as the cancel: Ctrl+C reaches the whole process
                // group on Linux and macOS, so the tool got the signal as well. As a tool failure it would start the error
                // recovery of MigrationErrorAction in a run that was asked to stop.
                _logger.LogWarning(
                    "CLI tool {ExecutablePath} exited with code {ExitCode} for {Filename} after the run was cancelled; counted as the cancel",
                    request.ExecutablePath, exitCode, request.Filename);
                throw new OperationCanceledException(
                    $"CLI tool '{request.ExecutablePath}' exited with code {exitCode} after the run was cancelled while executing '{request.Filename}'.",
                    cancellationToken);
            }

            if (isSuccess && inputIncomplete && cancellationToken.IsCancellationRequested)
            {
                // A tool that ended with a success code before it got the whole file counts as the cancel as well, also where
                // the cancel cut off the write of the file: it ran part of the file at most, and as a tool failure (below) it
                // would start the error recovery of MigrationErrorAction too
                _logger.LogWarning(
                    "CLI tool {ExecutablePath} exited with code {ExitCode} before it read all of {Filename} after the run was cancelled; counted as the cancel",
                    request.ExecutablePath, exitCode, request.Filename);
                throw new OperationCanceledException(
                    $"CLI tool '{request.ExecutablePath}' exited with code {exitCode} before it read all of '{request.Filename}' after the run was cancelled.",
                    cancellationToken);
            }

            if (isSuccess && inputIncomplete)
            {
                // A tool that did not read the whole file cannot have run it, whatever its exit code says
                throw new CliToolExecutionException(
                    $"CLI tool '{request.ExecutablePath}' exited with code {exitCode} before it read all of '{request.Filename}' from its standard input.",
                    request.ExecutablePath, exitCode);
            }

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

    /// <summary>
    /// Writes the file to the standard input of the tool and closes it. Returns false when the tool closed its input
    /// before it read the whole file, because it exited or does not read its input: the write meets a broken pipe and
    /// fails with an <see cref="IOException"/>, and the exit code of the tool decides.
    /// </summary>
    private async Task<bool> WriteInputAsync(Process process, CliToolExecutionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.WriteAsync(request.FileContent.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "CLI tool {ExecutablePath} closed its input before it read all of {Filename}",
                request.ExecutablePath, request.Filename);
            try
            {
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // ignored: the rest of the buffered input cannot reach a closed pipe either, and the writer is closed anyway
            }

            return false;
        }
    }

    /// <summary>Kills the tool and every process it started; a process that already exited is no error.</summary>
    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort kill
        }
    }
}
