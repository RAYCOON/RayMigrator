using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests CliToolExecutor.ExecuteAsync with real OS processes.
/// Verifies File mode, Stdin mode, exit code evaluation, stderr capture, timeout, and cancellation.
/// Platform: macOS + Linux only (skipped on Windows).
/// </summary>
public class CliToolExecutorTests : IDisposable
{
    private readonly CliToolExecutor _executor;
    private readonly List<string> _tempFiles = new();

    public CliToolExecutorTests()
    {
        _executor = new CliToolExecutor(Substitute.For<ILogger<CliToolExecutor>>());
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); }
            catch { /* cleanup best effort */ }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string CreateTempFile(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static CliToolExecutionRequest CreateRequest(
        string executablePath,
        string arguments = "",
        CliToolInputMode inputMode = CliToolInputMode.File,
        string? fileContent = null,
        string? filePath = null,
        string filename = "test.sql",
        int timeoutInSeconds = 10,
        string[]? successExitCodes = null) => new()
    {
        ExecutablePath = executablePath,
        Arguments = arguments,
        InputMode = inputMode,
        FileContent = fileContent,
        FilePath = filePath ?? "/tmp/test.sql",
        Filename = filename,
        TimeoutInSeconds = timeoutInSeconds,
        ExitCodeMatcher = ExitCodeMatcher.TryParse(successExitCodes, out var matcher, out _) ? matcher : ExitCodeMatcher.Default
    };

    // ── File Mode ────────────────────────────────────────────────────────────

    [Fact]
    public async Task FileMode_CatReadsFile_ReturnsContentInStdout()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        string content = "SELECT 1;\nSELECT 2;";
        string filePath = CreateTempFile(content);

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/cat",
            arguments: filePath,
            filePath: filePath), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.TrimEnd().Should().Be(content);
    }

    [Fact]
    public async Task FileMode_ExitCode0_ReturnsSuccess()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/usr/bin/true"), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task FileMode_ExitCode1_ReturnsFailure()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/usr/bin/false"), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── Stdin Mode ───────────────────────────────────────────────────────────

    [Fact]
    public async Task StdinMode_CatEchoesStdin_ReturnsContentInStdout()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        string content = "SELECT 1;";

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/cat",
            inputMode: CliToolInputMode.Stdin,
            fileContent: content), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.StandardOutput.TrimEnd().Should().Be(content);
    }

    [Fact]
    public async Task StdinMode_MultilineContent_FullyPiped()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        string content = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"INSERT INTO t VALUES ({i});"));

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/cat",
            inputMode: CliToolInputMode.Stdin,
            fileContent: content), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.StandardOutput.TrimEnd().Should().Be(content);
    }

    [Fact]
    public async Task StdinMode_EmptyContent_HandlesGracefully()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/cat",
            inputMode: CliToolInputMode.Stdin,
            fileContent: ""), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().BeEmpty();
    }

    // ── Exit Code Evaluation ─────────────────────────────────────────────────

    [Fact]
    public async Task CustomSuccessExitCode_42_ReturnsSuccess()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/bash",
            arguments: "-c \"exit 42\"",
            successExitCodes: ["42"]), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(42);
    }

    [Fact]
    public async Task UnexpectedExitCode_NotInEitherList_ReturnsFailure()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/bash",
            arguments: "-c \"exit 99\"",
            successExitCodes: ["0"]), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(99);
        result.ErrorMessage.Should().Contain("not in SuccessExitCodes");
    }

    [Fact]
    public async Task MultipleSuccessCodes_AnyMatches_ReturnsSuccess()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/bash",
            arguments: "-c \"exit 2\"",
            successExitCodes: ["0", "2"]), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(2);
    }

    [Fact]
    public async Task ErrorExitCode_SpecificErrorMessage()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/usr/bin/false",
            filename: "migration_01.sql"), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("/usr/bin/false");
        result.ErrorMessage.Should().Contain("migration_01.sql");
    }

    // ── Stderr Capture ───────────────────────────────────────────────────────

    [Fact]
    public async Task StderrCaptured_ReturnsInResult()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/bash",
            arguments: "-c \"echo error >&2\""), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.StandardError.TrimEnd().Should().Be("error");
    }

    [Fact]
    public async Task BothStdoutAndStderr_CapturedSeparately()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/bash",
            arguments: "-c \"echo out; echo err >&2\""), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.StandardOutput.TrimEnd().Should().Be("out");
        result.StandardError.TrimEnd().Should().Be("err");
    }

    // ── Timeout & Error Handling ─────────────────────────────────────────────

    [Fact]
    public async Task Timeout_ThrowsCliToolTimeoutException()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var request = CreateRequest(
            executablePath: "/bin/bash",
            arguments: "-c \"sleep 999\"",
            timeoutInSeconds: 1);

        var act = () => _executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<CliToolTimeoutException>();
    }

    [Fact]
    public async Task NonexistentExecutable_ThrowsCliToolExecutionException()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var request = CreateRequest(executablePath: "/nonexistent/tool");

        var act = () => _executor.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<CliToolExecutionException>();
    }

    [Fact]
    public async Task Duration_ReturnsPositiveTimeSpan()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/usr/bin/true"), TestContext.Current.CancellationToken);

        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // ── Input the tool does not read ─────────────────────────────────────────

    /// <summary>More input than a pipe buffer holds, so that the write cannot complete before a tool that reads nothing exits.</summary>
    private static string LargerThanAPipe()
        => string.Concat(Enumerable.Repeat("SELECT 1; -- a line of input that no pipe buffer holds all of\n", 40000));

    [Fact]
    public async Task StdinMode_ToolFailsWithoutReadingItsInput_ReturnsTheFailureOfItsExitCode()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Act: the tool exits at once, so the write of the file meets a closed pipe
        var result = await _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/sh",
            arguments: "-c \"exit 3\"",
            inputMode: CliToolInputMode.Stdin,
            fileContent: LargerThanAPipe()), TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(3, because: "the exit code of the tool decides, not the input it did not read");
        result.ErrorMessage.Should().Contain("exited with code 3");
    }

    [Fact]
    public async Task StdinMode_ToolSucceedsWithoutReadingItsInput_ThrowsCliToolExecutionException()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Act
        var act = () => _executor.ExecuteAsync(CreateRequest(
            executablePath: "/bin/sh",
            arguments: "-c \"exit 0\"",
            inputMode: CliToolInputMode.Stdin,
            fileContent: LargerThanAPipe()), TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<CliToolExecutionException>(
            because: "a tool that did not get the whole file cannot have run it, whatever its exit code says");
        thrown.Which.Message.Should().Contain("exited with code 0 before it read all of 'test.sql'");
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CancellationToken_Cancelled_ThrowsOperationCancelled()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var request = CreateRequest(
            executablePath: "/bin/bash",
            arguments: "-c \"sleep 5\"",
            timeoutInSeconds: 30);

        var act = () => _executor.ExecuteAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Cancel_WhileTheToolRuns_KillsTheToolAndTheProcessesItStarted()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Arrange: the tool starts a child that would sleep for 30 seconds and writes the child's pid
        string pidFile = Path.Combine(Path.GetTempPath(), $"raycli_cancel_{Guid.NewGuid():N}.pid");
        _tempFiles.Add(pidFile);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var request = CreateRequest(
            executablePath: "/bin/bash",
            arguments: $"-c \"sleep 30 & echo $! > {pidFile}; wait\"",
            timeoutInSeconds: 60);
        var execution = _executor.ExecuteAsync(request, cts.Token);
        int childPid = await ReadPidAsync(pidFile);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await cts.CancelAsync();
        var act = () => execution;

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.CancellationToken.Should().Be(cts.Token,
            because: "the caller's cancel stopped the tool");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), because: "the tool was killed, not waited for");
        (await HasExitedAsync(childPid)).Should().BeTrue(because: "cancelling kills the tool and every process it started");
    }

    [Fact]
    public async Task Cancel_BeforeTheToolStarts_StartsNoProcess()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Arrange: the tool would leave a file behind if it ran
        string marker = Path.Combine(Path.GetTempPath(), $"raycli_cancel_{Guid.NewGuid():N}.marker");
        _tempFiles.Add(marker);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var request = CreateRequest(executablePath: "/usr/bin/touch", arguments: marker);

        // Act
        var act = () => _executor.ExecuteAsync(request, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(marker).Should().BeFalse(because: "a cancelled run never starts the tool");
    }

    /// <summary>
    /// An executor whose wait for the tool lets the tool exit and then cancels <paramref name="cts"/>: with
    /// <paramref name="cancelOvertakesTheExit"/> the wait then ends cancelled, as when the cancel overtakes the exit
    /// event of the tool, otherwise it ends normally with the token cancelled.
    /// </summary>
    private static CliToolExecutor CancelAfterTheExit(CancellationTokenSource cts, bool cancelOvertakesTheExit) =>
        new(Substitute.For<ILogger<CliToolExecutor>>(), async (process, token) =>
        {
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            await cts.CancelAsync();
            if (cancelOvertakesTheExit)
            {
                token.ThrowIfCancellationRequested();
            }
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancel_AfterASuccessfulToolExited_KeepsTheResult(bool cancelOvertakesTheExit)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Arrange
        using var cts = new CancellationTokenSource();
        var executor = CancelAfterTheExit(cts, cancelOvertakesTheExit);
        var request = CreateRequest(executablePath: "/bin/bash", arguments: "-c \"echo done\"");

        // Act
        var result = await executor.ExecuteAsync(request, cts.Token);

        // Assert
        cts.IsCancellationRequested.Should().BeTrue();
        result.Success.Should().BeTrue(because: "the tool finished before the cancel took effect, so the file is done");
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("done");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ToolFails_WhileTheRunIsCancelled_CountsAsTheCancel(bool cancelOvertakesTheExit)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Arrange: the tool ends with the exit code of a process that SIGINT ended, as Ctrl+C reaches the whole process group
        using var cts = new CancellationTokenSource();
        var executor = CancelAfterTheExit(cts, cancelOvertakesTheExit);
        var request = CreateRequest(executablePath: "/bin/bash", arguments: "-c \"exit 130\"");

        // Act
        var act = () => executor.ExecuteAsync(request, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
            because: "a tool that failed while the run was cancelled is reported as the cancel, not as a tool failure for MigrationErrorAction");
        thrown.Which.CancellationToken.Should().Be(cts.Token);
        thrown.Which.Message.Should().Contain("exited with code 130");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StdinMode_ToolSucceedsWithoutReadingItsInput_WhileTheRunIsCancelled_CountsAsTheCancel(bool cancelOvertakesTheExit)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Arrange: the tool exits with success at once, so the write of the file meets a closed pipe
        using var cts = new CancellationTokenSource();
        var executor = CancelAfterTheExit(cts, cancelOvertakesTheExit);
        var request = CreateRequest(
            executablePath: "/bin/bash",
            arguments: "-c \"exit 0\"",
            inputMode: CliToolInputMode.Stdin,
            fileContent: LargerThanAPipe());

        // Act
        var act = () => executor.ExecuteAsync(request, cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
            because: "a tool that did not get the whole file while the run was cancelled is reported as the cancel, not as a tool failure for MigrationErrorAction");
        thrown.Which.CancellationToken.Should().Be(cts.Token);
        thrown.Which.Message.Should().Contain("exited with code 0 before it read all of 'test.sql'");
    }

    [Fact]
    public async Task StdinMode_CancelCutsOffTheInputOfAToolThatSucceeded_CountsAsTheCancel()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Arrange: the tool exits with success at once, while a sleep it started in the background holds its input open
        // without reading it, so the write of the file waits for a full pipe until the cancel cuts it off. A background job
        // of a non-interactive bash reads /dev/null unless its input is redirected, hence the copy of the input in
        // descriptor 3; the sleep writes nothing, so the output of the tool ends with the tool.
        string toolPidFile = Path.Combine(Path.GetTempPath(), $"raycli_cutoff_{Guid.NewGuid():N}.pid");
        string sleepPidFile = Path.Combine(Path.GetTempPath(), $"raycli_cutoff_{Guid.NewGuid():N}.sleep.pid");
        _tempFiles.Add(toolPidFile);
        _tempFiles.Add(sleepPidFile);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var request = CreateRequest(
            executablePath: "/bin/bash",
            arguments: $"-c \"exec 3<&0; sleep 30 <&3 >/dev/null 2>&1 & echo $! > {sleepPidFile}; echo $$ > {toolPidFile}; exit 0\"",
            inputMode: CliToolInputMode.Stdin,
            fileContent: LargerThanAPipe(),
            timeoutInSeconds: 60);

        try
        {
            var execution = _executor.ExecuteAsync(request, cts.Token);
            int toolPid = await ReadPidAsync(toolPidFile);
            (await HasExitedAsync(toolPid)).Should().BeTrue(
                because: "the tool must have exited before the cancel, so that the cancel cuts off the write and does not kill the tool");
            execution.IsCompleted.Should().BeFalse(because: "the write of the file waits for the input pipe that the sleep does not read");

            // Act
            await cts.CancelAsync();
            var act = () => execution;

            // Assert
            var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
                because: "a tool whose input the cancel cut off did not get the whole file, whatever its exit code says");
            thrown.Which.CancellationToken.Should().Be(cts.Token);
            thrown.Which.Message.Should().Contain("exited with code 0 before it read all of 'test.sql'");
        }
        finally
        {
            try
            {
                using var sleep = System.Diagnostics.Process.GetProcessById(int.Parse(File.ReadAllText(sleepPidFile).Trim()));
                if (sleep.ProcessName == "sleep")
                    sleep.Kill();
            }
            catch
            {
                // ignored: best effort, the sleep ended already or never started
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StdinMode_CancelAfterASuccessfulToolReadTheWholeFile_KeepsTheResult(bool cancelOvertakesTheExit)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;

        // Arrange
        using var cts = new CancellationTokenSource();
        var executor = CancelAfterTheExit(cts, cancelOvertakesTheExit);
        var request = CreateRequest(executablePath: "/bin/cat", inputMode: CliToolInputMode.Stdin, fileContent: "SELECT 1;");

        // Act
        var result = await executor.ExecuteAsync(request, cts.Token);

        // Assert
        cts.IsCancellationRequested.Should().BeTrue();
        result.Success.Should().BeTrue(because: "the tool got the whole file and finished before the cancel took effect, so the file is done");
        result.StandardOutput.Should().Be("SELECT 1;", because: "the tool read all of its input");
    }

    private static async Task<int> ReadPidAsync(string pidFile)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(pidFile) && int.TryParse((await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken)).Trim(), out int pid))
                return pid;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"The tool wrote no pid to {pidFile}");
    }

    private static async Task<bool> HasExitedAsync(int pid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                if (process.HasExited)
                    return true;
            }
            catch (ArgumentException)
            {
                return true; // no process with this id any more
            }
            catch (InvalidOperationException)
            {
                return true;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }
}
