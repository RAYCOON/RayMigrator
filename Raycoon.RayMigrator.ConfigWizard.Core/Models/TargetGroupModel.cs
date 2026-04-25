namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class TargetGroupModel
{
    public string Alias { get; set; } = "";
    public string DatabaseType { get; set; } = "SqlServer";
    public OverridableValue<string> TargetMigrationOrder { get; set; } = new();
    public OverridableValue<string> HashValidationScope { get; set; } = new();

    /// <summary>Overridable per target group. Corresponds to Core's TargetGroupOptions.UseCliToolAlias.</summary>
    public OverridableValue<string> UseCliToolAlias { get; set; } = new();

    /// <summary>
    /// Wizard-only: CLI tool parameters for this target group level.
    /// Inherited by Targets that don't define their own.
    /// Propagated to Targets during serialization (runtime only supports Target-level).
    /// </summary>
    public Dictionary<string, string>? CliToolParameters { get; set; }

    public OverridableValue<bool> StopRollbackOnMissingRollbackFile { get; set; } = new();

    public List<TargetModel> Targets { get; set; } = new();
}
