
using FluentAssertions;
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
}
