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
    private readonly Func<bool> _beat;
    private readonly ILogger _logger;
    private readonly int _migrationRunId;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    /// <summary>The number of beats that reached the repository.</summary>
    public int Beats { get; private set; }

    /// <summary>
    /// Starts the heartbeat loop.
    /// </summary>
    /// <param name="migrationRunId">The run the heartbeat belongs to, for the log.</param>
    /// <param name="interval">The time between two beats; must be positive.</param>
    /// <param name="beat">Writes one heartbeat and returns whether the run row was still open.</param>
    /// <param name="logger">Receives a warning for every failed beat.</param>
    public MigrationRunHeartbeat(int migrationRunId, TimeSpan interval, Func<bool> beat, ILogger logger)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The heartbeat interval must be positive.");

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
                    if (_beat())
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

    /// <summary>Stops the loop and waits for a beat in progress to finish.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!_stop.IsCancellationRequested)
            await _stop.CancelAsync();
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
