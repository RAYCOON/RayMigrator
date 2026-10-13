using System.Collections.Concurrent;

namespace Raycoon.RayMigrator.Infrastructure.Logging;

public class DatabaseLoggerQueue
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Task _processingTask;
    private int _pending;

    public bool HasLogEntries => _queue.Count > 0;

    /// <summary>True when nothing is queued and no entry is being written.</summary>
    public bool IsIdle => Volatile.Read(ref _pending) == 0;

    public DatabaseLoggerQueue()
    {
        _processingTask = Task.Factory.StartNew(
            ProcessQueue, TaskCreationOptions.LongRunning);
    }

    private void ProcessQueue()
    {
        foreach (Action logAction in _queue.GetConsumingEnumerable())
        {
            try
            {
                logAction();
            }
            catch (Exception ex)
            {
                // Fallback to stderr — cannot use ILogger here (would cause infinite recursion)
                Console.Error.WriteLine($"[DatabaseLoggerQueue] Error during database logging: {ex.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _pending);
            }
        }
    }

    public void EnqueueLog(Action logAction)
    {
        Interlocked.Increment(ref _pending);
        try
        {
            _queue.Add(logAction);
        }
        catch (InvalidOperationException)
        {
            // Queue was completed via Flush() — silently drop late entries
            Interlocked.Decrement(ref _pending);
        }
    }

    /// <summary>
    /// Waits until the queue is idle (nothing queued, nothing being written) without closing it, polling every
    /// 20 milliseconds. Returns false when <paramref name="timeout"/> elapses first.
    /// </summary>
    public async Task<bool> WaitForEmptyAsync(TimeSpan? timeout = null)
    {
        var deadline = timeout.HasValue ? DateTime.UtcNow + timeout.Value : DateTime.MaxValue;
        while (!IsIdle)
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(20);
        }
        return true;
    }

    /// <summary>
    /// Signals that no more entries will be added and waits for all pending
    /// entries to be processed. Safe to call multiple times.
    /// </summary>
    public void Flush(TimeSpan? timeout = null)
    {
        try
        {
            _queue.CompleteAdding();
        }
        catch (InvalidOperationException)
        {
            // Already completed from a previous Flush() call
        }

        try
        {
            if (timeout.HasValue)
                _processingTask.Wait(timeout.Value);
            else
                _processingTask.Wait();
        }
        catch (AggregateException)
        {
            // Processing task faulted — nothing more we can do
        }
    }
}
