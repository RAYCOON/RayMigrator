namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Which stored hash decides whether a migrated file is still the file on disk. Configured per TargetGroup
/// (<c>TargetGroupDefaults.HashValidationScope</c> / <c>TargetGroups[].HashValidationScope</c>); the
/// <c>validate-hash --scope</c> option overrides it for all TargetGroups of that command.
/// </summary>
public enum HashValidationScope : byte
{
    /// <summary>
    /// Invalid value. Rejected by configuration validation; the engine falls back to <see cref="File"/>.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Compares the hash of the whole file, including the <c>[RayMigrator]</c> header (<c>FileUpHash</c>).
    /// Any change to the file makes it pending again.
    /// </summary>
    File = 1,

    /// <summary>
    /// Compares the hash of the SQL blocks only (<c>FileUpBlocksHash</c>). Edits to the TOML header do not make
    /// the file pending again.
    /// </summary>
    SqlBlocks = 2,

    /// <summary>
    /// No hash comparison when deciding what is pending: a record with status Migrated counts as applied, whatever
    /// the file looks like now. It does not switch off hashing elsewhere: the block-level resume and the
    /// recovery of interrupted files still require an unchanged <c>FileUpBlocksHash</c>, <c>update-hash</c> compares
    /// all three hashes, and <c>validate-hash</c> still reports missing files (#19).
    /// </summary>
    Disabled = 3,
}
