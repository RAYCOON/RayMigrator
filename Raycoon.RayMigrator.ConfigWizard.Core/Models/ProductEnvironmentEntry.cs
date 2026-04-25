namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>Tracks wizard completion status for a product+environment combination.</summary>
public class ProductEnvironmentEntry
{
    /// <summary>Whether the Detailed Configuration wizard has been fully completed for this combination.</summary>
    public bool WizardCompleted { get; set; }
}
