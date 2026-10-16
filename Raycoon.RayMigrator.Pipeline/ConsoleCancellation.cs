using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Services.Abstractions;

namespace Raycoon.RayMigrator.Pipeline;

/// <summary>
/// Lets Ctrl+C and SIGTERM cancel the command the CLI runs. The first signal cancels the <see cref="RunCancellation"/>
/// of the process with the <c>--cancelled-action</c> of the command and keeps the process alive, so that the command
/// stops at its next checkpoint and records why; every later signal keeps the default and ends the process at once.
/// The pipeline listens only while the command runs: <see cref="Listen"/> right before
/// <c>RayMigratorService.DoWorkAsync</c>, <see cref="Dispose"/> right after it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RunCancellation.Cancel"/> runs the cancel callbacks of the providers on the calling thread, and MySQL
/// opens a connection of its own for KILL QUERY. The handler therefore only counts the signal and queues the cancel to
/// the thread pool. Windows runs the console control handlers of a process under a lock that removing a handler takes
/// as well (the reason why the <c>ConsoleLifetime</c> of Microsoft.Extensions.Hosting never unregisters from within
/// its handler), so a handler that waited for a provider would hold back the <see cref="Dispose"/> after the command
/// and, behind the same lock, possibly a second Ctrl+C. On Linux and macOS .NET starts a thread of its own for every
/// SIGINT and SIGTERM; there a slow handler would hold back nothing. The queued cancel runs in the execution context
/// the listener was created in, so that its warning carries the migration context of the command: the database log
/// stores it with the run, and a command without database log does not write it there.
/// </para>
/// <para>
/// SIGTERM is listened to on Linux and macOS only. On Windows .NET maps it to <c>CTRL_SHUTDOWN_EVENT</c>, and Windows
/// ends the process as soon as a handler of that event returns, whatever it returns; a graceful stop there would have
/// to block the handler until the command ended, as <c>ConsoleLifetime</c> does. Ctrl+Break and Ctrl+\ (SIGQUIT),
/// which <see cref="Console.CancelKeyPress"/> reports as <see cref="ConsoleSpecialKey.ControlBreak"/>, keep their
/// default as well.
/// </para>
/// </remarks>
internal sealed class ConsoleCancellation : IDisposable
{
    /// <summary>The signal name a Ctrl+C gives the cancel as its reason.</summary>
    internal const string CtrlC = "Ctrl+C";

    /// <summary>The signal name a SIGTERM gives the cancel as its reason.</summary>
    internal const string SigTerm = "SIGTERM";

    private const string AbandonRunHint =
        "A second Ctrl+C or SIGTERM abandons the run: its run row stays open until a later start closes it as orphaned or 'raymigrator fix --scope orphanedruns' does";

    private const string EndAtOnceHint = "A second Ctrl+C or SIGTERM ends the process at once";

    private readonly RunCancellation _cancellation;
    private readonly MigrationCommand _command;
    private readonly MigrationCancelledAction _action;
    private readonly bool _writesRunRow;
    private readonly ILogger _logger;
    private readonly ExecutionContext? _executionContext;
    private int _signals;
    private int _disposed;
    private ConsoleCancelEventHandler? _cancelKeyPressHandler;
    private PosixSignalRegistration? _sigTermRegistration;

    /// <summary>
    /// Creates the listener without registering a handler; <see cref="OnSignal"/> decides each signal. The cancel of
    /// the first signal runs in the execution context of the caller.
    /// </summary>
    /// <param name="cancellation">The handle of the process, the one the command runs with.</param>
    /// <param name="consoleOptions">The command and its <see cref="RayMigratorConsoleOptions.CancelledAction"/>.</param>
    /// <param name="logger">Receives the warning of the first signal.</param>
    internal ConsoleCancellation(RunCancellation cancellation, RayMigratorConsoleOptions consoleOptions, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        ArgumentNullException.ThrowIfNull(consoleOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _cancellation = cancellation;
        _command = consoleOptions.Command;
        _action = consoleOptions.CancelledAction;
        _writesRunRow = consoleOptions.Command is MigrationCommand.MigrateUp or MigrationCommand.MigrateDown or MigrationCommand.Baseline
            && consoleOptions.GetProfile().WritesRepository;
        _logger = logger;
        _executionContext = ExecutionContext.Capture();
    }

    /// <summary>
    /// Registers the handlers for Ctrl+C and, on Linux and macOS, SIGTERM; disposing the listener removes them, and
    /// the signals have their default again. A signal whose handler the platform refuses keeps its default.
    /// </summary>
    /// <param name="cancellation">The handle of the process, the one the command runs with.</param>
    /// <param name="consoleOptions">The command and its <see cref="RayMigratorConsoleOptions.CancelledAction"/>.</param>
    /// <param name="logger">Receives the warning of the first signal.</param>
    internal static ConsoleCancellation Listen(RunCancellation cancellation, RayMigratorConsoleOptions consoleOptions, ILogger logger)
    {
        var listener = new ConsoleCancellation(cancellation, consoleOptions, logger);
        listener.Register();
        return listener;
    }

    /// <summary>
    /// Decides one signal. The first queues the cancel of the command to the thread pool and returns true: the
    /// default is suppressed and the process lives on until the command stopped. Every later signal returns false,
    /// so the process ends at once as by default.
    /// </summary>
    /// <param name="signal">The signal, <see cref="CtrlC"/> or <see cref="SigTerm"/>; the cancel gives it as its reason.</param>
    internal bool OnSignal(string signal)
    {
        // A signal that arrives while the listener is being disposed belongs to the shutdown after the command
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Increment(ref _signals) > 1)
        {
            return false;
        }

        ThreadPool.QueueUserWorkItem(static state => state.Listener.CancelInCommandContext(state.Signal), (Listener: this, Signal: signal),
            preferLocal: false);
        return true;
    }

    /// <summary>The CLI spelling of <paramref name="action"/>, as <c>--cancelled-action</c> takes it (<c>rollback-file</c>).</summary>
    internal static string OptionValueOf(MigrationCancelledAction action) => action switch
    {
        MigrationCancelledAction.Terminate => "terminate",
        MigrationCancelledAction.RollbackFile => "rollback-file",
        MigrationCancelledAction.RollbackRun => "rollback-run",
        MigrationCancelledAction.RollbackRelease => "rollback-release",
        _ => action.ToString()
    };

    /// <summary>The CLI verb of a command that is not a run (<c>validate-hash</c>, <c>update-hash</c>, <c>info</c>, <c>fix</c>).</summary>
    private static string VerbOf(MigrationCommand command) => command switch
    {
        MigrationCommand.ValidateHash => "validate-hash",
        MigrationCommand.UpdateHash => "update-hash",
        MigrationCommand.Info => "info",
        MigrationCommand.FixIssues => "fix",
        _ => command.ToString()
    };

    /// <summary>Removes the handlers; a cancel that is still queued does nothing any more.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_cancelKeyPressHandler != null)
        {
            Console.CancelKeyPress -= _cancelKeyPressHandler;
            _cancelKeyPressHandler = null;
        }

        _sigTermRegistration?.Dispose();
        _sigTermRegistration = null;
    }

    private void Register()
    {
        ConsoleCancelEventHandler cancelKeyPressHandler = (_, e) =>
        {
            // Ctrl+Break and Ctrl+\ (SIGQUIT) arrive as ControlBreak and keep their default
            if (e.SpecialKey == ConsoleSpecialKey.ControlC && OnSignal(CtrlC))
            {
                e.Cancel = true;
            }
        };

        try
        {
            Console.CancelKeyPress += cancelKeyPressHandler;
            _cancelKeyPressHandler = cancelKeyPressHandler;
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or IOException)
        {
            _logger.LogDebug(ex, "Ctrl+C keeps its default: the platform refused the handler");
        }

        // Windows ends the process when the handler of CTRL_SHUTDOWN_EVENT returns (see the remarks)
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            _sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                if (OnSignal(SigTerm))
                {
                    context.Cancel = true;
                }
            });
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or IOException)
        {
            _logger.LogDebug(ex, "SIGTERM keeps its default: the platform refused the handler");
        }
    }

    private void CancelInCommandContext(string signal)
    {
        if (_executionContext == null)
        {
            Cancel(signal);
            return;
        }

        ExecutionContext.Run(_executionContext, static state =>
        {
            var (listener, signal) = ((ConsoleCancellation, string))state!;
            listener.Cancel(signal);
        }, (this, signal));
    }

    private void Cancel(string signal)
    {
        // The command ended before the queued cancel ran: there is nothing to stop and nothing to announce
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // No exception may leave a pool thread: it would end the process without the stop the signal asked for.
        // RunCancellation.Cancel swallows a failed cancel callback of a provider itself; this is the safety net.
        try
        {
            LogCancelling(signal);
            _cancellation.Cancel(_action, signal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Signal} could not cancel the command", signal);
        }
    }

    private void LogCancelling(string signal)
    {
        switch (_command)
        {
            case MigrationCommand.MigrateUp when _writesRunRow:
                _logger.LogWarning("{Signal} received: cancelling at the next block boundary (action: {CancelledAction}). " + AbandonRunHint,
                    signal, OptionValueOf(_action));
                break;

            case MigrationCommand.MigrateDown when _writesRunRow:
                _logger.LogWarning("{Signal} received: cancelling at the next rollback block. " + AbandonRunHint, signal);
                break;

            case MigrationCommand.Baseline:
                _logger.LogWarning("{Signal} received: cancelling before the next file. " + AbandonRunHint, signal);
                break;

            case MigrationCommand.MigrateUp or MigrationCommand.MigrateDown:
                _logger.LogWarning("{Signal} received: cancelling at the next block boundary. " + EndAtOnceHint, signal);
                break;

            default:
                _logger.LogWarning("{Signal} received: cancelling {Command}. " + EndAtOnceHint, signal, VerbOf(_command));
                break;
        }
    }
}
