namespace Raycoon.RayMigrator.ConfigWizard.Web.Services;

/// <summary>
/// Tracks whether the user has accepted the Config Wizard Terms of Use in
/// the current browser session (click-wrap shown before the first export)
/// and builds the acceptance note that is written into the exported ZIP.
///
/// Deliberately in-memory only: the wizard promises that nothing the user
/// does is transmitted or persisted, so acceptance is neither sent to a
/// server nor written to local storage. The acceptance note inside the
/// exported ZIP is the privacy-compatible record — generated locally at
/// export time and staying with the user.
/// </summary>
public class TermsAcceptanceService
{
    /// <summary>
    /// Version identifier of the published Terms of Use. MUST be bumped
    /// whenever the terms published on the website change — the acceptance
    /// note records this value as the accepted version.
    /// </summary>
    public const string TermsVersion = "2026-08-17";

    /// <summary>Authoritative German terms URL. Keep in sync with Footer.TermsUrl (de).</summary>
    public const string TermsUrlDe = "https://raymigrator.com/de/nutzungsbedingungen";

    /// <summary>English translation URL. Keep in sync with Footer.TermsUrl (en).</summary>
    public const string TermsUrlEn = "https://raymigrator.com/en/terms-of-use";

    /// <summary>Whether the terms have been accepted in this session.</summary>
    public bool IsAccepted { get; private set; }

    /// <summary>UTC timestamp of the acceptance.</summary>
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>Records the acceptance for the current session.</summary>
    public void Accept()
    {
        if (IsAccepted)
            return;

        IsAccepted = true;
        AcceptedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Builds the bilingual acceptance note for the exported ZIP. German
    /// first — it is the authoritative language of the terms.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the terms have not been accepted — the note documents a
    /// fact and must never fabricate one.
    /// </exception>
    public string BuildAcceptanceNote()
    {
        if (!IsAccepted || AcceptedAtUtc is null)
            throw new InvalidOperationException(
                "The acceptance note can only be built after the terms have been accepted.");

        string acceptedAt = AcceptedAtUtc.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        string wizardVersion =
            typeof(TermsAcceptanceService).Assembly.GetName().Version?.ToString(3) ?? "unknown";

        return $"""
            RayMigrator Config Wizard — Annahmevermerk Nutzungsbedingungen
            RayMigrator Config Wizard — Terms of Use Acceptance Note
            ================================================================

            [DE]
            Die Nutzungsbedingungen des RayMigrator Config Wizard wurden vor dem
            Export dieser Konfiguration per Klickbestätigung akzeptiert
            (Checkbox und Bestätigungsschaltfläche im Export-Dialog).
            Maßgebliche Fassung (deutsch): {TermsUrlDe}

            [EN]
            The RayMigrator Config Wizard Terms of Use were accepted via click
            confirmation (checkbox and confirm button in the export dialog)
            before this configuration was exported.
            English translation: {TermsUrlEn}
            The German version prevails.

            Angenommen am (UTC) / Accepted at (UTC): {acceptedAt}
            Fassung der Bedingungen / Terms version: {TermsVersion}
            Wizard-Version / Wizard version:         {wizardVersion}

            Dieser Vermerk wurde beim Export lokal im Browser erzeugt. Es wurden
            keine Daten übertragen; der Wizard arbeitet vollständig clientseitig.
            This note was generated locally in the browser at export time. No
            data was transmitted; the wizard processes everything client-side.
            """;
    }
}
