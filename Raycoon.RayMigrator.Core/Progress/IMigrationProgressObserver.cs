namespace Raycoon.RayMigrator.Core.Progress;

/// <summary>
/// Receives the lifecycle events of one run: the run itself, every file on every target, and every SQL block.
/// A host sets its observer on <see cref="MigrationContext.ProgressObserver"/> before it calls the service; the
/// engine calls the methods synchronously on the thread that executes the run, so an implementation must be fast
/// and must not throw. Logging stays with Serilog; these events exist so that a host can show progress without
/// parsing log text.
/// </summary>
public interface IMigrationProgressObserver
{
    /// <summary>A migrate-up, migrate-down or baseline run has started. Raised after the run row exists.</summary>
    void OnRunStarted(RunStartedEvent e);

    /// <summary>A file is about to run on a target (migration, rollback or baseline).</summary>
    void OnFileStarted(FileStartedEvent e);

    /// <summary>A SQL block of a file was executed on a target. Not raised for files that run through a CLI tool.</summary>
    void OnBlockCompleted(BlockCompletedEvent e);

    /// <summary>A file finished on a target, with its outcome.</summary>
    void OnFileCompleted(FileCompletedEvent e);

    /// <summary>The run finished, with its result. Raised exactly once per <see cref="OnRunStarted"/>.</summary>
    void OnRunCompleted(RunCompletedEvent e);
}
