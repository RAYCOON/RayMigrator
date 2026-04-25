using System.Diagnostics;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Static helper that wraps System.Diagnostics.Process to execute commands inside Docker containers
/// with SQL content piped via stdin.
/// </summary>
public static class DockerExecHelper
{
    /// <summary>
    /// Result of a docker exec invocation.
    /// </summary>
    public record DockerExecResult(bool Success, int ExitCode, string Stdout, string Stderr);

    /// <summary>
    /// Runs a command (typically 'docker exec -i ...') with SQL content piped via stdin.
    /// </summary>
    /// <param name="executable">The executable to run (e.g., "docker").</param>
    /// <param name="arguments">The resolved argument string (e.g., "exec -i rm_db_sqlserver ...").</param>
    /// <param name="stdinContent">The SQL content to pipe into the process stdin.</param>
    /// <param name="timeoutSeconds">Maximum wait time before killing the process.</param>
    /// <returns>A result indicating success/failure, exit code, stdout, and stderr.</returns>
    public static async Task<DockerExecResult> ExecuteViaStdinAsync(
        string executable,
        string arguments,
        string stdinContent,
        int timeoutSeconds = 30)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Start reading stdout/stderr before writing stdin to prevent deadlocks on large payloads
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.StandardInput.WriteAsync(stdinContent);
        process.StandardInput.Close();

        bool exited = await WaitForExitAsync(process, TimeSpan.FromSeconds(timeoutSeconds));

        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            string timeoutStdout = await stdoutTask;
            string timeoutStderr = await stderrTask;
            return new DockerExecResult(
                Success: false,
                ExitCode: -1,
                Stdout: timeoutStdout,
                Stderr: $"Process timed out after {timeoutSeconds}s. {timeoutStderr}");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        return new DockerExecResult(
            Success: process.ExitCode == 0,
            ExitCode: process.ExitCode,
            Stdout: stdout,
            Stderr: stderr);
    }

    /// <summary>
    /// Resolves {Key} placeholders in an argument template from a parameters dictionary.
    /// </summary>
    /// <param name="argumentTemplate">The template string with {Key} placeholders.</param>
    /// <param name="parameters">Dictionary of key-value pairs for substitution.</param>
    /// <returns>The resolved argument string.</returns>
    public static string ResolveArguments(
        string argumentTemplate,
        Dictionary<string, string> parameters)
    {
        string resolved = argumentTemplate;
        foreach (var kvp in parameters)
        {
            resolved = resolved.Replace($"{{{kvp.Key}}}", kvp.Value);
        }
        return resolved;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
