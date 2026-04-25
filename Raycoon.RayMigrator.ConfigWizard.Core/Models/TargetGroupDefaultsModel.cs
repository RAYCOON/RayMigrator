
namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class TargetGroupDefaultsModel
{
    public string TargetMigrationOrder { get; set; } = "Successively";
    public string HashValidationScope { get; set; } = "File";
    public bool StopRollbackOnMissingRollbackFile { get; set; } = true;
    public TargetDefaultsModel TargetDefaults { get; set; } = new();
}
