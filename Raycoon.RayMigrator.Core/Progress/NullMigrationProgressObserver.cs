namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// The observer of a context whose host did not set one. Ignores every event.
/// </summary>
public sealed class NullMigrationProgressObserver : IMigrationProgressObserver
{
    /// <summary>The single instance.</summary>
    public static NullMigrationProgressObserver Instance { get; } = new();

    private NullMigrationProgressObserver()
    {
    }

    /// <inheritdoc/>
    public void OnRunStarted(RunStartedEvent e)
    {
    }

    /// <inheritdoc/>
    public void OnFileStarted(FileStartedEvent e)
    {
    }

    /// <inheritdoc/>
    public void OnBlockCompleted(BlockCompletedEvent e)
    {
    }

    /// <inheritdoc/>
    public void OnFileCompleted(FileCompletedEvent e)
    {
    }

    /// <inheritdoc/>
    public void OnRunCompleted(RunCompletedEvent e)
    {
    }
}
