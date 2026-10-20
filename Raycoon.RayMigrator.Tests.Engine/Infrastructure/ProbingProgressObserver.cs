using Raycoon.RayMigrator.Core.Progress;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Records every progress event like <see cref="RecordingProgressObserver"/> and hands each one to a probe right after it
/// is recorded. The engine raises the events synchronously in its own flow, so a probe that reads the repository sees
/// what the run has written up to that event, for example the record of a file after one of its blocks.
/// </summary>
public sealed class ProbingProgressObserver : IMigrationProgressObserver
{
    private readonly RecordingProgressObserver _recorder = new();
    private readonly Action<object> _probe;

    /// <param name="probe">Runs for every event, in the flow of the run, after the event is recorded.</param>
    public ProbingProgressObserver(Action<object> probe)
    {
        _probe = probe;
    }

    /// <summary>Every event in the order it was raised.</summary>
    public List<object> Events => _recorder.Events;

    /// <summary>The recorded events of one type.</summary>
    public IReadOnlyList<T> Of<T>() => _recorder.Of<T>();

    public void OnRunStarted(RunStartedEvent e) => Handle(e, _recorder.OnRunStarted);

    public void OnFileStarted(FileStartedEvent e) => Handle(e, _recorder.OnFileStarted);

    public void OnBlockCompleted(BlockCompletedEvent e) => Handle(e, _recorder.OnBlockCompleted);

    public void OnFileCompleted(FileCompletedEvent e) => Handle(e, _recorder.OnFileCompleted);

    public void OnRunCompleted(RunCompletedEvent e) => Handle(e, _recorder.OnRunCompleted);

    private void Handle<T>(T e, Action<T> record) where T : notnull
    {
        record(e);
        _probe(e);
    }
}
