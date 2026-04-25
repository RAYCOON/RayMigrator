
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Raycoon.RayMigrator.ConfigWizard.Web.Services;

/// <summary>
/// Custom MudLocalizer that translates MudBlazor component strings (e.g., Stepper Previous/Next)
/// using the wizard's LocalizationService.
/// </summary>
public class WizardMudLocalizer : MudLocalizer
{
    private readonly LocalizationService _l;

    private static readonly Dictionary<string, Func<LocalizationService, string>> Translations = new()
    {
        ["MudStepper_Previous"] = l => l.Get("Common.Back"),
        ["MudStepper_Next"] = l => l.Get("Common.Next"),
        ["MudStepper_Complete"] = l => l.Get("Phase2.GoToOverview"),
        ["MudStepper_Skip"] = l => l.Get("Common.Next"),
        ["MudStepper_Reset"] = l => l.Get("Summary.Restart"),
    };

    public WizardMudLocalizer(LocalizationService l)
    {
        _l = l;
    }

    public override LocalizedString this[string key]
    {
        get
        {
            if (Translations.TryGetValue(key, out var resolver))
                return new LocalizedString(key, resolver(_l));

            return new LocalizedString(key, key, resourceNotFound: true);
        }
    }
}
