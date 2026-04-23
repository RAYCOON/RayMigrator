// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Collections.Concurrent;

namespace Raycoon.RayMigrator.Infrastructure.Logging;

public class DatabaseLoggerQueue
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Task _processingTask;

    public bool HasLogEntries => _queue.Count > 0;

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
        }
    }

    public void EnqueueLog(Action logAction)
    {
        try
        {
            _queue.Add(logAction);
        }
        catch (InvalidOperationException)
        {
            // Queue was completed via Flush() — silently drop late entries
        }
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
