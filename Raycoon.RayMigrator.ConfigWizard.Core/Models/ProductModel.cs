
namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class ProductModel
{
    public string Alias { get; set; } = "";
    public string MigrationFilesRootDirectory { get; set; } = "";
    public OverridableValue<string> MigrationErrorAction { get; set; } = new();
    public OverridableValue<string> RollbackErrorAction { get; set; } = new();
    public OverridableValue<string> MigrationFilesExtension { get; set; } = new();
    public OverridableValue<string> MigrationRollbackFilesPreExtension { get; set; } = new();
    public OverridableValue<string> MigrationFilesEncoding { get; set; } = new();
    public OverridableValue<bool> RequireRollbackFile { get; set; } = new();
    public OverridableValue<bool> StopRollbackOnMissingRollbackFile { get; set; } = new();

    /// <summary>Overridable per product. Corresponds to Core's ProductOptions.UseCliToolAlias.</summary>
    public OverridableValue<string> UseCliToolAlias { get; set; } = new();

    /// <summary>
    /// Wizard-only: CLI tool parameters for this product level.
    /// Inherited by TargetGroups and Targets that don't define their own.
    /// Propagated to Targets during serialization (runtime only supports Target-level).
    /// </summary>
    public Dictionary<string, string>? CliToolParameters { get; set; }

    /// <summary>Defines the execution order of target groups within this product (comma-separated aliases).</summary>
    public string? TargetGroupMigrationOrder { get; set; }

    public List<TargetGroupModel> TargetGroups { get; set; } = new();

    /// <summary>UI-only flag for scaffold generation. Not serialized to JSON.</summary>
    public bool GenerateScaffold { get; set; }
}
