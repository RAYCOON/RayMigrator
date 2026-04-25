
namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Describes a single defaults promotion applied by DefaultsPromoter.
/// </summary>
public class PromotionResult
{
    public string PropertyName { get; set; } = "";
    public string PromotedValue { get; set; } = "";
    public int AffectedProducts { get; set; }

    /// <summary>"ProductDefaults" or "TargetGroupDefaults"</summary>
    public string Level { get; set; } = "";
}
