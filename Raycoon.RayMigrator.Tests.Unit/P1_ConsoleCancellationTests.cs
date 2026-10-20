using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Services.Abstractions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the signal logic of the CLI (<see cref="ConsoleCancellation"/>), driven without real signals. The first Ctrl+C
/// or SIGTERM cancels the handle of the process with the <c>--cancelled-action</c> and suppresses the default; every
/// later signal keeps the default, which ends the process. The cancel runs on the thread pool, in the execution context
/// of the command, so that a slow cancel callback of a provider holds back neither the handler nor a second signal.
/// </summary>
public class ConsoleCancellationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>A value of the flow the listener is created in; the logger records what it sees.</summary>
    private static readonly AsyncLocal<string?> CommandFlow = new();

    private sealed class RecordingLogger : ILogger
    {
        public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception, string? Flow)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((logLevel, formatter(state, exception), exception, CommandFlow.Value));
    }

    private static RayMigratorConsoleOptions Options(
        MigrationCommand command,
        MigrationRunMode runMode = MigrationRunMode.Migrate,
        MigrationCancelledAction cancelledAction = MigrationCancelledAction.Terminate) => new()
    {
        Command = command,
        Product = "P",
        Environment = "Dev",
        RunMode = runMode,
        ShowStartupInfo = false,
        RevealSensitiveData = false,
        CancelledAction = cancelledAction
    };

    private static void WaitForCancel(RunCancellation cancellation)
        => cancellation.Token.WaitHandle.WaitOne(Timeout).Should().BeTrue(because: "the first signal cancels the handle on the thread pool");

    [Theory]
    [InlineData(ConsoleCancellation.CtrlC, MigrationCancelledAction.Terminate)]
    [InlineData(ConsoleCancellation.CtrlC, MigrationCancelledAction.RollbackFile)]
    [InlineData(ConsoleCancellation.CtrlC, MigrationCancelledAction.RollbackRun)]
    [InlineData(ConsoleCancellation.CtrlC, MigrationCancelledAction.RollbackRelease)]
    [InlineData(ConsoleCancellation.SigTerm, MigrationCancelledAction.Terminate)]
    [InlineData(ConsoleCancellation.SigTerm, MigrationCancelledAction.RollbackRelease)]
    public void OnSignal_First_SuppressesTheDefaultAndCancelsWithTheActionOfTheOption(string signal, MigrationCancelledAction action)
    {
        // Arrange
        using var cancellation = new RunCancellation();
        using var listener = new ConsoleCancellation(cancellation, Options(MigrationCommand.MigrateUp, cancelledAction: action), NullLogger.Instance);

        // Act
        bool suppressDefault = listener.OnSignal(signal);
        WaitForCancel(cancellation);

        // Assert
        suppressDefault.Should().BeTrue(because: "the process must live on until the command stopped at its next checkpoint");
        cancellation.Action.Should().Be(action);
        cancellation.Reason.Should().Be(signal, because: "the run records which signal stopped it");
    }

    [Theory]
    [InlineData(ConsoleCancellation.CtrlC, ConsoleCancellation.CtrlC)]
    [InlineData(ConsoleCancellation.SigTerm, ConsoleCancellation.SigTerm)]
    [InlineData(ConsoleCancellation.SigTerm, ConsoleCancellation.CtrlC)]
    [InlineData(ConsoleCancellation.CtrlC, ConsoleCancellation.SigTerm)]
    public void OnSignal_Second_KeepsTheDefaultSoThatTheProcessEnds(string first, string second)
    {
        // Arrange
        using var cancellation = new RunCancellation();
        using var listener = new ConsoleCancellation(cancellation, Options(MigrationCommand.MigrateUp), NullLogger.Instance);

        // Act
        bool firstSuppresses = listener.OnSignal(first);
        bool secondSuppresses = listener.OnSignal(second);
        bool thirdSuppresses = listener.OnSignal(second);
        WaitForCancel(cancellation);

        // Assert
        firstSuppresses.Should().BeTrue();
        secondSuppresses.Should().BeFalse(because: "a second signal abandons the run, whichever signal came first");
        thirdSuppresses.Should().BeFalse();
        cancellation.Reason.Should().Be(first, because: "only the first signal cancels");
    }

    [Fact]
    public void OnSignal_SlowCancelCallback_HoldsBackNeitherTheHandlerNorASecondSignal()
    {
        // Arrange
        using var cancellation = new RunCancellation();
        using var listener = new ConsoleCancellation(cancellation, Options(MigrationCommand.MigrateUp), NullLogger.Instance);
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var callbackFinished = new ManualResetEventSlim();
        int handlerThread = Environment.CurrentManagedThreadId;
        int callbackThread = 0;
        // Like the cancel callback of a provider that opens a connection for KILL QUERY
        using var registration = cancellation.Token.Register(() =>
        {
            callbackThread = Environment.CurrentManagedThreadId;
            callbackEntered.Set();
            releaseCallback.Wait(Timeout);
            callbackFinished.Set();
        });

        // Act
        bool firstSuppresses = listener.OnSignal(ConsoleCancellation.CtrlC);
        bool callbackRunning = callbackEntered.Wait(Timeout, TestContext.Current.CancellationToken);
        bool secondSuppresses = listener.OnSignal(ConsoleCancellation.CtrlC);
        bool callbackStillBlocked = !callbackFinished.IsSet;
        releaseCallback.Set();
        bool callbackCompleted = callbackFinished.Wait(Timeout, TestContext.Current.CancellationToken);

        // Assert
        firstSuppresses.Should().BeTrue();
        callbackRunning.Should().BeTrue();
        callbackCompleted.Should().BeTrue();
        secondSuppresses.Should().BeFalse(because: "the second Ctrl+C must take effect while the first cancel still waits for a provider");
        callbackStillBlocked.Should().BeTrue(because: "neither signal may wait for the cancel callbacks");
        callbackThread.Should().NotBe(handlerThread, because: "the cancel runs on the thread pool, not on the thread of the signal");
    }

    [Fact]
    public void OnSignal_LogsTheWarningInTheExecutionContextOfTheCommand()
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var logger = new RecordingLogger();
        CommandFlow.Value = "command";
        using var listener = new ConsoleCancellation(cancellation, Options(MigrationCommand.MigrateUp), logger);
        CommandFlow.Value = "signal handler";

        // Act
        listener.OnSignal(ConsoleCancellation.CtrlC);
        WaitForCancel(cancellation);

        // Assert
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Which.Flow.Should().Be("command",
            because: "the warning carries the migration context of the command, which decides whether and where the database log stores it");
    }

    [Theory]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, "at the next block boundary (action: rollback-run)", true)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "at the next rollback block", true)]
    [InlineData(MigrationCommand.Baseline, MigrationRunMode.Migrate, "before the next file", true)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Simulate, "at the next block boundary", false)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Validate, "at the next block boundary", false)]
    [InlineData(MigrationCommand.ValidateHash, MigrationRunMode.Migrate, "cancelling validate-hash", false)]
    [InlineData(MigrationCommand.UpdateHash, MigrationRunMode.Migrate, "cancelling update-hash", false)]
    [InlineData(MigrationCommand.Info, MigrationRunMode.Migrate, "cancelling info", false)]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Simulate, "cancelling fix", false)]
    public void OnSignal_First_WarnsWhereTheCommandStopsAndWhatASecondSignalDoes(
        MigrationCommand command, MigrationRunMode runMode, string stop, bool writesRunRow)
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var logger = new RecordingLogger();
        using var listener = new ConsoleCancellation(cancellation, Options(command, runMode, MigrationCancelledAction.RollbackRun), logger);

        // Act
        listener.OnSignal(ConsoleCancellation.CtrlC);
        WaitForCancel(cancellation);

        // Assert
        var warning = logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Which.Message;
        warning.Should().Contain("Ctrl+C").And.Contain(stop);
        if (writesRunRow)
        {
            warning.Should().Contain("abandons the run").And.Contain("run row stays open").And.Contain("raymigrator fix --scope orphanedruns",
                because: "an abandoned run keeps its run row open until a later start or fix closes it");
        }
        else
        {
            warning.Should().Contain("ends the process at once").And.NotContain("run row",
                because: $"{command} in {runMode} mode writes no run row");
        }
    }

    [Fact]
    public void OnSignal_AfterDispose_KeepsTheDefaultAndDoesNotCancel()
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var listener = new ConsoleCancellation(cancellation, Options(MigrationCommand.MigrateUp), NullLogger.Instance);
        listener.Dispose();

        // Act
        bool suppressDefault = listener.OnSignal(ConsoleCancellation.CtrlC);

        // Assert
        suppressDefault.Should().BeFalse(because: "after the command the signals end the process as by default");
        cancellation.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void OnSignal_CancelCallbackThrows_TheCommandStillStopsWithTheActionOfTheOption()
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var logger = new RecordingLogger();
        using var listener = new ConsoleCancellation(cancellation,
            Options(MigrationCommand.MigrateUp, cancelledAction: MigrationCancelledAction.RollbackRun), logger);
        using var registration = cancellation.Token.Register(() => throw new InvalidOperationException("KILL QUERY failed"));

        // Act
        listener.OnSignal(ConsoleCancellation.SigTerm);
        WaitForCancel(cancellation);

        // Assert
        cancellation.Action.Should().Be(MigrationCancelledAction.RollbackRun,
            because: "RunCancellation swallows the failure of a cancel callback, so the command stops at its next checkpoint");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.StartsWith("SIGTERM received", StringComparison.Ordinal));
    }

    [Fact]
    public void Listen_RegistersTheHandlersAndDisposeRemovesThem()
    {
        // Arrange
        using var cancellation = new RunCancellation();

        // Act
        var act = () =>
        {
            using var listener = ConsoleCancellation.Listen(cancellation, Options(MigrationCommand.Info), NullLogger.Instance);
            listener.Dispose();
        };

        // Assert
        act.Should().NotThrow(because: "registering and removing Ctrl+C and SIGTERM must work on every platform the CLI runs on");
        cancellation.IsCancellationRequested.Should().BeFalse();
    }
}
