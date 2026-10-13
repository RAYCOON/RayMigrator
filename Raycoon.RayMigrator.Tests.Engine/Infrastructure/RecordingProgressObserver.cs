using Raycoon.RayMigrator.Core.Progress;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Records every progress event in the order it was raised, so that a test can assert the sequence.
/// </summary>
public sealed class RecordingProgressObserver : IMigrationProgressObserver
{
    public List<object> Events { get; } = [];

    public IReadOnlyList<T> Of<T>() => Events.OfType<T>().ToList();

    public void OnRunStarted(RunStartedEvent e) => Events.Add(e);

    public void OnFileStarted(FileStartedEvent e) => Events.Add(e);

    public void OnBlockCompleted(BlockCompletedEvent e) => Events.Add(e);

    public void OnFileCompleted(FileCompletedEvent e) => Events.Add(e);

    public void OnRunCompleted(RunCompletedEvent e) => Events.Add(e);
}
