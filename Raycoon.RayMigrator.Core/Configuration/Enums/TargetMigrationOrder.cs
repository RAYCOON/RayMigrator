namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// The loop order in which the files of a TargetGroup are applied to its targets. Execution is sequential in both
/// orders, one connection at a time; the members only decide which loop is the outer one. The former names
/// <c>Simultaneously</c> and <c>Successively</c> suggested concurrency that never existed and are rejected (#19).
/// </summary>
public enum TargetMigrationOrder : byte
{
    /// <summary>
    /// Invalid value. Rejected by configuration validation; the engine falls back to <see cref="TargetByTarget"/>.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// File-major: each migration file is applied to every target of the group before the next file starts
    /// (File1 -> Target1, Target2; File2 -> Target1, Target2). Keeps the targets in step file by file.
    /// </summary>
    FileByFile = 1,

    /// <summary>
    /// Target-major: all migration files are applied to one target before the next target starts
    /// (Target1 -> File1, File2; Target2 -> File1, File2). The default.
    /// </summary>
    TargetByTarget = 2,
}
