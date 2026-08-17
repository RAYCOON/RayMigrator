namespace Raycoon.RayMigrator.ConfigWizard.Web.Services;

/// <summary>
/// Tracks whether the user has accepted the Config Wizard Terms of Use in
/// the current browser session (click-wrap shown before the first export).
///
/// Deliberately in-memory only: the wizard promises that nothing the user
/// does is transmitted or persisted, so acceptance is neither sent to a
/// server nor written to local storage. The trade-off is accepted — a fresh
/// confirmation per session is legally the stronger incorporation anyway.
/// </summary>
public class TermsAcceptanceService
{
    /// <summary>Whether the terms have been accepted in this session.</summary>
    public bool IsAccepted { get; private set; }

    /// <summary>UTC timestamp of the acceptance, for display purposes only.</summary>
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>Records the acceptance for the current session.</summary>
    public void Accept()
    {
        if (IsAccepted)
            return;

        IsAccepted = true;
        AcceptedAtUtc = DateTimeOffset.UtcNow;
    }
}
