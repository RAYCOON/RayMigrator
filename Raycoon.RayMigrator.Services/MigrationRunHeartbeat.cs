using Microsoft.Extensions.Logging;

namespace Raycoon.RayMigrator.Services;

/// <summary>
/// Keeps <c>MigrationRun.LastHeartbeatAt</c> of the current run fresh while the run executes: a
/// <see cref="PeriodicTimer"/> loop calls <paramref name="beat"/> every interval until the heartbeat is stopped.
/// A beat that fails is logged as a warning and never ends the run; the run row's heartbeat age then decides
/// whether another process may treat the run as orphaned (ADR-024, schema version 2).
/// </summary>
internal sealed class MigrationRunHeartbeat : IAsyncDisposable
{
    private readonly Func<Task<bool>> _beat;
    private readonly ILogger _logger;
    private readonly int _migrationRunId;
    private readonly CancellationTokenSource _stop = new();
    private readonly TimeSpan _stopTimeout;
    private readonly Task _loop;

    /// <summary>How long <see cref="DisposeAsync"/> waits for a beat in progress before it lets the run go on without it.</summary>
    public static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The number of beats that reached the repository.</summary>
    public int Beats { get; private set; }

    /// <summary>
    /// Starts the heartbeat loop.
    /// </summary>
    /// <param name="migrationRunId">The run the heartbeat belongs to, for the log.</param>
    /// <param name="interval">The time between two beats; must be positive.</param>
    /// <param name="beat">Writes one heartbeat and returns whether the run row was still open. It gets no token: a beat
    /// in progress when the heartbeat stops is left to finish (see <see cref="DisposeAsync"/>).</param>
    /// <param name="logger">Receives a warning for every failed beat.</param>
    /// <param name="stopTimeout">How long the stop waits for a beat in progress; <see cref="DefaultStopTimeout"/> when null.</param>
    public MigrationRunHeartbeat(int migrationRunId, TimeSpan interval, Func<Task<bool>> beat, ILogger logger, TimeSpan? stopTimeout = null)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The heartbeat interval must be positive.");
        _stopTimeout = stopTimeout ?? DefaultStopTimeout;

        _migrationRunId = migrationRunId;
        _beat = beat ?? throw new ArgumentNullException(nameof(beat));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loop = Task.Run(() => RunAsync(interval));
    }

    private async Task RunAsync(TimeSpan interval)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                try
                {
                    if (await _beat())
                        Beats++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Heartbeat of MigrationRun {MigrationRunId} failed; the run continues", _migrationRunId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // stopped
        }
    }

    /// <summary>
    /// Stops the loop and waits up to the stop timeout for a beat in progress to finish. A beat that takes longer
    /// (a repository that answers slowly) is left to finish on its own: the run's closing update does not wait for
    /// a liveness signal, and a beat that lands after the run closed changes nothing (<c>FinishedAt IS NULL</c>).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_stop.IsCancellationRequested)
            await _stop.CancelAsync();

        var finished = await Task.WhenAny(_loop, Task.Delay(_stopTimeout));
        if (finished != _loop)
        {
            _logger.LogWarning("Heartbeat of MigrationRun {MigrationRunId}: a beat is still in progress after {Seconds} s; the run goes on without waiting for it",
                _migrationRunId, _stopTimeout.TotalSeconds);
            return; // the loop ends when the beat returns and still needs the token source
        }

        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // stopped
        }

        _stop.Dispose();
    }
}
