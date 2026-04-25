namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Enum for RayMigratorOptions
/// </summary>
public enum TargetMigrationOrder : byte
{
    Undefined = 0,
    Simultaneously = 1,
    Successively = 2,
}