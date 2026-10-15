using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Progress;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Records every progress event like <see cref="RecordingProgressObserver"/> and cancels the run once, on the first
/// event that matches the trigger. The engine raises the events synchronously in its own flow, so the cancel lands
/// at an exact point of the run (after a block, after a file, when a file starts) without any timing.
/// </summary>
public sealed class CancellingProgressObserver : IMigrationProgressObserver
{
    private readonly RecordingProgressObserver _recorder = new();
    private readonly Func<object, bool> _trigger;
    private readonly Action _cancel;

    /// <param name="trigger">Picks the event after which the run is cancelled.</param>
    /// <param name="cancel">Cancels the run, e.g. <c>RunCancellation.Cancel</c> or a token source.</param>
    public CancellingProgressObserver(Func<object, bool> trigger, Action cancel)
    {
        _trigger = trigger;
        _cancel = cancel;
    }

    /// <summary>Whether the trigger matched and the run was cancelled.</summary>
    public bool Fired { get; private set; }

    /// <summary>Every event in the order it was raised.</summary>
    public List<object> Events => _recorder.Events;

    /// <summary>The recorded events of one type.</summary>
    public IReadOnlyList<T> Of<T>() => _recorder.Of<T>();

    /// <summary>Cancels after block <paramref name="blockNumber"/> of <paramref name="fileName"/> completed in <paramref name="operation"/>.</summary>
    public static Func<object, bool> AfterBlock(string fileName, int blockNumber, MigrationOperation operation)
        => e => e is BlockCompletedEvent b && b.FileName == fileName && b.BlockNumber == blockNumber && b.Operation == operation;

    /// <summary>Cancels after <paramref name="fileName"/> completed in <paramref name="operation"/>.</summary>
    public static Func<object, bool> AfterFile(string fileName, MigrationOperation operation)
        => e => e is FileCompletedEvent f && f.FileName == fileName && f.Operation == operation;

    /// <summary>Cancels when <paramref name="fileName"/> starts in <paramref name="operation"/>, before its first block.</summary>
    public static Func<object, bool> WhenFileStarts(string fileName, MigrationOperation operation)
        => e => e is FileStartedEvent f && f.FileName == fileName && f.Operation == operation;

    public void OnRunStarted(RunStartedEvent e) => Handle(e, _recorder.OnRunStarted);

    public void OnFileStarted(FileStartedEvent e) => Handle(e, _recorder.OnFileStarted);

    public void OnBlockCompleted(BlockCompletedEvent e) => Handle(e, _recorder.OnBlockCompleted);

    public void OnFileCompleted(FileCompletedEvent e) => Handle(e, _recorder.OnFileCompleted);

    public void OnRunCompleted(RunCompletedEvent e) => Handle(e, _recorder.OnRunCompleted);

    private void Handle<T>(T e, Action<T> record) where T : notnull
    {
        record(e);
        if (!Fired && _trigger(e))
        {
            Fired = true;
            _cancel();
        }
    }
}
