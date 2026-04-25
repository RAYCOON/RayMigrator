namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Enum for RayMigratorOptions
/// </summary>
public enum HashValidationScope : byte
{
    Undefined = 0,
    File = 1,
    SqlBlocks = 2,
    Disabled = 3,
}