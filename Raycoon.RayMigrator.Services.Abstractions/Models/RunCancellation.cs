using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Services.Abstractions;

/// <summary>
/// The handle a host cancels one run with, and the action the run takes when it stops. A
/// <see cref="CancellationToken"/> carries no payload, so the host passes this handle in the request
/// (<see cref="MigrateUpRequest.Cancellation"/>, <see cref="MigrateDownRequest.Cancellation"/>,
/// <see cref="BaselineRequest.Cancellation"/>) and calls <see cref="Cancel"/> when the user stops the run. The engine
/// links <see cref="Token"/> with the token of the call and stops at the next block boundary; the first
/// <see cref="Cancel"/> decides the action, later calls change nothing. A run cancelled through the token of the call
/// alone takes <see cref="MigrationCancelledAction.Terminate"/>.
/// </summary>
/// <remarks>
/// One handle belongs to one run. It is safe to call <see cref="Cancel"/> from another thread while the run reads
/// the action; the host disposes the handle after the run returned.
/// </remarks>
public sealed class RunCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private CancelRequest? _request;
    private int _disposed;

    /// <summary>Creates a handle that has not been cancelled.</summary>
    public RunCancellation()
    {
        Token = _source.Token;
    }

    /// <summary>The token the engine observes; it is cancelled by the first <see cref="Cancel"/>.</summary>
    public CancellationToken Token { get; }

    /// <summary>The action the first <see cref="Cancel"/> chose, or null while the run was not cancelled.</summary>
    public MigrationCancelledAction? Action => Volatile.Read(ref _request)?.Action;

    /// <summary>The reason the first <see cref="Cancel"/> gave, or null.</summary>
    public string? Reason => Volatile.Read(ref _request)?.Reason;

    /// <summary>When the first <see cref="Cancel"/> was called (UTC), or null while the run was not cancelled.</summary>
    public DateTime? RequestedAtUtc => Volatile.Read(ref _request)?.RequestedAtUtc;

    /// <summary>Whether <see cref="Cancel"/> was called.</summary>
    public bool IsCancellationRequested => Volatile.Read(ref _request) != null;

    /// <summary>
    /// Cancels the run with <paramref name="action"/>. The run stops at the next block boundary and then takes the
    /// action; during a migrate-down, a baseline or a running error recovery it stops and ignores the action.
    /// </summary>
    /// <remarks>
    /// The call that cancels runs the callbacks registered on <see cref="Token"/> synchronously on the calling thread,
    /// among them those of the database providers (MySqlConnector opens a connection of its own for <c>KILL QUERY</c>),
    /// so it may take as long as they do. An exception of a callback is swallowed: the action and the token are set
    /// before the callbacks run, so the run stops at its next checkpoint all the same, even when the statement in
    /// progress could not be interrupted.
    /// </remarks>
    /// <param name="action">What the run does after it stopped.</param>
    /// <param name="reason">Why the run is cancelled, for the log and the error message of the run; may be null.</param>
    /// <returns>True for the call that cancelled the run; false when it was cancelled before or the handle is disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="action"/> is <see cref="MigrationCancelledAction.Undefined"/> or not a member.</exception>
    public bool Cancel(MigrationCancelledAction action, string? reason = null)
    {
        if (action == MigrationCancelledAction.Undefined || !Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action,
                $"Cancel needs one of [{string.Join(", ", Enum.GetValues<MigrationCancelledAction>().Where(a => a != MigrationCancelledAction.Undefined))}].");
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (Interlocked.CompareExchange(ref _request, new CancelRequest(action, reason, DateTime.UtcNow), null) != null)
        {
            return false;
        }

        try
        {
            _source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Disposed by the host between the check above and here: the run has returned, there is nothing to stop.
        }
        catch (AggregateException)
        {
            // A callback failed after every callback ran; the token is cancelled, the run stops at its next checkpoint
        }

        return true;
    }

    /// <summary>Releases the token source. The <see cref="Token"/> stays readable; a later <see cref="Cancel"/> returns false.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _source.Dispose();
        }
    }

    private sealed record CancelRequest(MigrationCancelledAction Action, string? Reason, DateTime RequestedAtUtc);
}
