
using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Extensions;

public static class MigrationRunModeExtensions
{
    /// <summary>
    /// Whether SQL should be executed against target databases.
    /// Only true for Migrate mode.
    /// </summary>
    public static bool ShouldExecuteSql(this MigrationRunMode runMode)
        => runMode == MigrationRunMode.Migrate;

    /// <summary>
    /// Whether repository records should be written.
    /// Only true for Migrate mode — Simulate is side-effect-free.
    /// </summary>
    public static bool ShouldWriteRepository(this MigrationRunMode runMode)
        => runMode == MigrationRunMode.Migrate;

    /// <summary>
    /// Whether repository records should be read.
    /// True for Simulate and Migrate (both >= Simulate=20).
    /// </summary>
    public static bool ShouldReadRepository(this MigrationRunMode runMode)
        => runMode >= MigrationRunMode.Simulate;
}
