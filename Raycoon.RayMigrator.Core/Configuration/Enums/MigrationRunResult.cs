namespace Raycoon.RayMigrator.Core.Configuration.Enums;

public enum MigrationRunResult : byte
{
    /// <summary>
    /// Invalid ResultId value. ResultId has not been set properly.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Migration process is currently running.
    /// </summary>
    Running = 10,

    /// <summary>
    /// Migration(s) stopped due to error(s).
    /// </summary>
    Error = 90,

    /// <summary>
    /// Migration(s) successfully executed and finished.
    /// </summary>
    Ok = 100,
}
