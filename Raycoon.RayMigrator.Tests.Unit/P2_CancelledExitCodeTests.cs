using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Constants;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: the exit codes of the CLI wrapper when Ctrl+C or SIGTERM cancelled the command. A command that ended because
/// of the cancel exits with 130 and logs a warning, not an error: a run with <see cref="MigrationRunResult.Cancelled"/>,
/// update-hash and fix with <see cref="TemplateResultCode.Cancelled"/>, a read command whose
/// <see cref="OperationCanceledException"/> arrives while the token of the process is cancelled. A cancelled migrate-up
/// whose rollback did not complete, and one that failed at a file before the cancel stopped its error recovery, is an
/// <see cref="MigrationRunResult.Error"/> and exits with 1. Every service call observes the token of the process, and
/// the run requests carry its handle.
/// </summary>
public class CancelledExitCodeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private sealed class RecordingLogger : ILogger<RayMigratorService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static RayMigratorConsoleOptions Options(
        MigrationCommand command,
        MigrationCancelledAction cancelledAction = MigrationCancelledAction.Terminate) => new()
    {
        Command = command,
        Product = "P",
        Environment = "Dev",
        TargetReleaseVersion = command == MigrationCommand.MigrateDown ? "1.0" : null,
        RunMode = MigrationRunMode.Migrate,
        ShowStartupInfo = false,
        RevealSensitiveData = false,
        CancelledAction = cancelledAction
    };

    private static (RayMigratorService Wrapper, RecordingLogger Logger) CreateWrapper(RayMigratorConsoleOptions options, IMigrationService service)
    {
        var logger = new RecordingLogger();
        return (new RayMigratorService(logger, options, service), logger);
    }

    /// <summary>Lets the run method of <paramref name="command"/> return a result with <paramref name="result"/>.</summary>
    private static void SetUpRun(IMigrationService service, MigrationCommand command, MigrationRunResult result, int? errorCode, string message)
    {
        var operationResult = new MigrationOperationResult
        {
            Success = result == MigrationRunResult.Ok, Result = result, ErrorCode = errorCode, ErrorMessage = message, ProductAlias = "P"
        };

        switch (command)
        {
            case MigrationCommand.MigrateUp:
                service.MigrateUpAsync(Arg.Any<MigrateUpRequest>(), Arg.Any<CancellationToken>()).Returns(operationResult);
                break;
            case MigrationCommand.MigrateDown:
                service.MigrateDownAsync(Arg.Any<MigrateDownRequest>(), Arg.Any<CancellationToken>()).Returns(operationResult);
                break;
            case MigrationCommand.Baseline:
                service.BaselineAsync(Arg.Any<BaselineRequest>(), Arg.Any<CancellationToken>()).Returns(new BaselineResult
                {
                    Success = result == MigrationRunResult.Ok, Result = result, ErrorCode = errorCode, ErrorMessage = message, ProductAlias = "P"
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Not a run command.");
        }
    }

    /// <summary>Lets update-hash or fix return a failed result with <paramref name="errorCode"/>.</summary>
    private static void SetUpRepair(IMigrationService service, MigrationCommand command, int errorCode, string message)
    {
        if (command == MigrationCommand.UpdateHash)
        {
            service.UpdateHashAsync(Arg.Any<UpdateHashRequest>(), Arg.Any<CancellationToken>())
                .Returns(new HashUpdateResult { Success = false, ErrorCode = errorCode, ErrorMessage = message, ProductAlias = "P" });
        }
        else
        {
            service.FixIssuesAsync(Arg.Any<FixIssuesRequest>(), Arg.Any<CancellationToken>())
                .Returns(new FixIssuesResult { Success = false, ErrorCode = errorCode, ErrorMessage = message, ProductAlias = "P" });
        }
    }

    /// <summary>Lets the read command throw <paramref name="exception"/>: validate-hash, or the status query of info.</summary>
    private static void SetUpReadThrows(IMigrationService service, MigrationCommand command, Exception exception)
    {
        if (command == MigrationCommand.ValidateHash)
        {
            service.ValidateHashAsync(Arg.Any<ValidateHashRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(exception);
        }
        else
        {
            service.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(exception);
        }
    }

    [Theory]
    [InlineData(MigrationCommand.MigrateUp)]
    [InlineData(MigrationCommand.MigrateDown)]
    [InlineData(MigrationCommand.Baseline)]
    public async Task DoWorkAsync_RunCancelled_Returns130AndLogsAWarning(MigrationCommand command)
    {
        // Arrange
        var service = Substitute.For<IMigrationService>();
        SetUpRun(service, command, MigrationRunResult.Cancelled, TemplateResultCode.Cancelled, "Cancelled on request (Terminate): stopped before the next file");
        var (wrapper, logger) = CreateWrapper(Options(command), service);
        using var cancellation = new RunCancellation();

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(130, because: "a command that the cancel ended exits like a process that SIGINT ended (128 + 2)");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("cancelled for product P")
            && e.Message.Contains("Cancelled on request (Terminate)"));
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error, because: "a cancel on request is not a failure of the command");
    }

    [Fact]
    public async Task DoWorkAsync_CancelledRollbackIncomplete_ReturnsOneAndLogsAnError()
    {
        // Arrange
        var service = Substitute.For<IMigrationService>();
        SetUpRun(service, MigrationCommand.MigrateUp, MigrationRunResult.Error, TemplateResultCode.CancelledRollbackIncomplete,
            "Cancelled on request (RollbackRun): the rollback did not complete");
        var (wrapper, logger) = CreateWrapper(Options(MigrationCommand.MigrateUp, MigrationCancelledAction.RollbackRun), service);
        using var cancellation = new RunCancellation();

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(1, because: "a rollback that did not complete leaves the state unclear, like any failed run");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("the rollback did not complete"));
    }

    [Theory]
    [InlineData(null, "no such table: T. The error recovery was cancelled on request after 1 of 3 rollback(s): the files it did not reach keep their state. Reason: Ctrl+C")]
    [InlineData(TemplateResultCode.CancelledRollbackIncomplete, "Cancelled on request (RollbackRun): the rollback did not complete. Reason: Ctrl+C")]
    public async Task DoWorkAsync_MigrateUpErrorWhileTheProcessTokenIsCancelled_ReturnsOneAndLogsAnError(int? errorCode, string message)
    {
        // Arrange: Ctrl+C cancelled the process, and the run still ended as Error: it failed at a file before the cancel
        // stopped its error recovery, or the rollback of the cancel action did not complete
        using var cancellation = new RunCancellation();
        cancellation.Cancel(MigrationCancelledAction.RollbackRun, ConsoleCancellation.CtrlC);
        var service = Substitute.For<IMigrationService>();
        SetUpRun(service, MigrationCommand.MigrateUp, MigrationRunResult.Error, errorCode, message);
        var (wrapper, logger) = CreateWrapper(Options(MigrationCommand.MigrateUp, MigrationCancelledAction.RollbackRun), service);

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(1, because: "the result Error decides the exit code, not the signal, so a pipeline sees the failure");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains(message));
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning, because: "the command failed and did not end as cancelled");
    }

    [Theory]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunResult.Error)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunResult.Recovered)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunResult.Error)]
    [InlineData(MigrationCommand.Baseline, MigrationRunResult.Error)]
    public async Task DoWorkAsync_RunFailedWithoutCancel_StillReturnsOne(MigrationCommand command, MigrationRunResult result)
    {
        // Arrange
        var service = Substitute.For<IMigrationService>();
        SetUpRun(service, command, result, null, "block 2 failed");
        var (wrapper, logger) = CreateWrapper(Options(command), service);
        using var cancellation = new RunCancellation();

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(1);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("block 2 failed"));
    }

    [Theory]
    [InlineData(MigrationCommand.UpdateHash)]
    [InlineData(MigrationCommand.FixIssues)]
    public async Task DoWorkAsync_RepairCancelled_Returns130AndLogsAWarning(MigrationCommand command)
    {
        // Arrange: Ctrl+C cancelled the handle of the process, and the command returns what it did so far
        var service = Substitute.For<IMigrationService>();
        SetUpRepair(service, command, TemplateResultCode.Cancelled, "Cancelled on request: 2 done before the stop");
        var (wrapper, logger) = CreateWrapper(Options(command), service);
        using var cancellation = new RunCancellation();
        cancellation.Cancel(MigrationCancelledAction.Terminate, "Ctrl+C");

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(130, because: "update-hash and fix return what they did so far with the engine code Cancelled");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("2 done before the stop"));
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData(MigrationCommand.UpdateHash)]
    [InlineData(MigrationCommand.FixIssues)]
    public async Task DoWorkAsync_RepairWithTheCancelledCodeButNoSignal_ReturnsOneAndLogsAnError(MigrationCommand command)
    {
        // Arrange: the engine maps any OperationCanceledException of a failure to the code Cancelled, also one that no
        // signal caused, such as a provider timeout reported as a TaskCanceledException
        var service = Substitute.For<IMigrationService>();
        SetUpRepair(service, command, TemplateResultCode.Cancelled, "A task was canceled.");
        var (wrapper, logger) = CreateWrapper(Options(command), service);
        using var cancellation = new RunCancellation();

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(1, because: "only a command that Ctrl+C or SIGTERM ended exits with 130");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("A task was canceled."));
    }

    [Theory]
    [InlineData(MigrationCommand.UpdateHash)]
    [InlineData(MigrationCommand.FixIssues)]
    public async Task DoWorkAsync_RepairFailed_ReturnsOne(MigrationCommand command)
    {
        // Arrange
        var service = Substitute.For<IMigrationService>();
        SetUpRepair(service, command, TemplateResultCode.GeneralError, "repository unreachable");
        var (wrapper, logger) = CreateWrapper(Options(command), service);
        using var cancellation = new RunCancellation();

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(1);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("repository unreachable"));
    }

    [Theory]
    [InlineData(MigrationCommand.ValidateHash)]
    [InlineData(MigrationCommand.Info)]
    public async Task DoWorkAsync_ReadCommandCancelledWhileTheProcessTokenIsCancelled_Returns130(MigrationCommand command)
    {
        // Arrange
        using var cancellation = new RunCancellation();
        cancellation.Cancel(MigrationCancelledAction.Terminate, ConsoleCancellation.CtrlC);
        var service = Substitute.For<IMigrationService>();
        SetUpReadThrows(service, command, new OperationCanceledException(cancellation.Token));
        var (wrapper, logger) = CreateWrapper(Options(command), service);

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(130, because: "the read commands throw when they are cancelled instead of returning a result");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("cancelled on request")
            && e.Message.Contains("Ctrl+C"));
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData(MigrationCommand.ValidateHash)]
    [InlineData(MigrationCommand.Info)]
    public async Task DoWorkAsync_ReadCommandCancelledWithoutTheProcessToken_ReturnsOne(MigrationCommand command)
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var service = Substitute.For<IMigrationService>();
        SetUpReadThrows(service, command, new OperationCanceledException("a provider timed out"));
        var (wrapper, logger) = CreateWrapper(Options(command), service);

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(1, because: "only a cancel of the process makes 130; any other cancellation is a failure");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData(MigrationCommand.MigrateUp)]
    [InlineData(MigrationCommand.MigrateDown)]
    [InlineData(MigrationCommand.Baseline)]
    public async Task DoWorkAsync_RunCommand_PassesTheHandleInTheRequestAndItsToken(MigrationCommand command)
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var service = Substitute.For<IMigrationService>();
        SetUpRun(service, command, MigrationRunResult.Ok, null, string.Empty);
        var (wrapper, _) = CreateWrapper(Options(command), service);

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(0);
        switch (command)
        {
            case MigrationCommand.MigrateUp:
                await service.Received(1).MigrateUpAsync(Arg.Is<MigrateUpRequest>(r => r.Cancellation == cancellation), cancellation.Token);
                break;
            case MigrationCommand.MigrateDown:
                await service.Received(1).MigrateDownAsync(Arg.Is<MigrateDownRequest>(r => r.Cancellation == cancellation), cancellation.Token);
                break;
            default:
                await service.Received(1).BaselineAsync(Arg.Is<BaselineRequest>(r => r.Cancellation == cancellation), cancellation.Token);
                break;
        }
    }

    [Theory]
    [InlineData(MigrationCommand.ValidateHash)]
    [InlineData(MigrationCommand.UpdateHash)]
    [InlineData(MigrationCommand.Info)]
    [InlineData(MigrationCommand.FixIssues)]
    public async Task DoWorkAsync_OtherCommand_PassesTheTokenOfTheProcess(MigrationCommand command)
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var service = Substitute.For<IMigrationService>();
        service.ValidateHashAsync(Arg.Any<ValidateHashRequest>(), Arg.Any<CancellationToken>()).Returns(new ValidationResult { Success = true });
        service.UpdateHashAsync(Arg.Any<UpdateHashRequest>(), Arg.Any<CancellationToken>()).Returns(new HashUpdateResult { Success = true });
        service.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new MigrationStatusInfo { ProductAlias = "P" });
        service.GetHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new MigrationHistory());
        service.FixIssuesAsync(Arg.Any<FixIssuesRequest>(), Arg.Any<CancellationToken>()).Returns(new FixIssuesResult { Success = true });
        var (wrapper, _) = CreateWrapper(Options(command), service);

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(0);
        switch (command)
        {
            case MigrationCommand.ValidateHash:
                await service.Received(1).ValidateHashAsync(Arg.Any<ValidateHashRequest>(), cancellation.Token);
                break;
            case MigrationCommand.UpdateHash:
                await service.Received(1).UpdateHashAsync(Arg.Any<UpdateHashRequest>(), cancellation.Token);
                break;
            case MigrationCommand.Info:
                await service.Received(1).GetStatusAsync("P", cancellation.Token);
                await service.Received(1).GetHistoryAsync("P", 10, cancellation.Token);
                break;
            default:
                await service.Received(1).FixIssuesAsync(Arg.Any<FixIssuesRequest>(), cancellation.Token);
                break;
        }
    }

    [Theory]
    [InlineData(MigrationCancelledAction.Terminate)]
    [InlineData(MigrationCancelledAction.RollbackFile)]
    [InlineData(MigrationCancelledAction.RollbackRun)]
    [InlineData(MigrationCancelledAction.RollbackRelease)]
    public async Task DoWorkAsync_FirstCtrlCDuringTheRun_CancelsTheRequestWithTheActionOfTheOption(MigrationCancelledAction action)
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var options = Options(MigrationCommand.MigrateUp, action);
        using var listener = new ConsoleCancellation(cancellation, options, NullLogger.Instance);
        MigrateUpRequest? received = null;
        var service = Substitute.For<IMigrationService>();
        service.MigrateUpAsync(Arg.Any<MigrateUpRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            // The user presses Ctrl+C while the engine runs; the engine stops at its next checkpoint
            received = call.Arg<MigrateUpRequest>();
            listener.OnSignal(ConsoleCancellation.CtrlC).Should().BeTrue();
            call.Arg<CancellationToken>().WaitHandle.WaitOne(Timeout).Should().BeTrue();
            return new MigrationOperationResult
            {
                Success = false, Result = MigrationRunResult.Cancelled, ErrorCode = TemplateResultCode.Cancelled,
                ErrorMessage = $"Cancelled on request ({received.Cancellation!.Action}): stopped before the next file"
            };
        });
        var (wrapper, _) = CreateWrapper(options, service);

        // Act
        var exitCode = await wrapper.DoWorkAsync(null!, cancellation);

        // Assert
        exitCode.Should().Be(130);
        received!.Cancellation.Should().BeSameAs(cancellation);
        received.Cancellation!.Action.Should().Be(action, because: "the first Ctrl+C cancels with the --cancelled-action of the command");
        received.Cancellation.Reason.Should().Be(ConsoleCancellation.CtrlC);
    }
}
