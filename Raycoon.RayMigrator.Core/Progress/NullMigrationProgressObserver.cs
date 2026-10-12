namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// The observer of a context whose host did not set one. Ignores every event.
/// </summary>
public sealed class NullMigrationProgressObserver : IMigrationProgressObserver
{
    public static NullMigrationProgressObserver Instance { get; } = new();

    private NullMigrationProgressObserver()
    {
    }

    public void OnRunStarted(RunStartedEvent e)
    {
    }

    public void OnFileStarted(FileStartedEvent e)
    {
    }

    public void OnBlockCompleted(BlockCompletedEvent e)
    {
    }

    public void OnFileCompleted(FileCompletedEvent e)
    {
    }

    public void OnRunCompleted(RunCompletedEvent e)
    {
    }
}
