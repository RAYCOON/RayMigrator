namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// The click-wrap before the first export of a session hinges on this
/// service: the consent dialog is shown while <c>IsAccepted</c> is false and
/// skipped afterwards. Acceptance is deliberately session-only (in-memory,
/// never transmitted or persisted — the wizard's privacy promise).
/// </summary>
public class TermsAcceptanceServiceTests
{
    [Fact]
    public void NewInstance_IsNotAccepted()
    {
        var svc = new TermsAcceptanceService();

        svc.IsAccepted.Should().BeFalse();
        svc.AcceptedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Accept_SetsAcceptedAndTimestamp()
    {
        var svc = new TermsAcceptanceService();
        var before = DateTimeOffset.UtcNow;

        svc.Accept();

        svc.IsAccepted.Should().BeTrue();
        svc.AcceptedAtUtc.Should().NotBeNull();
        svc.AcceptedAtUtc!.Value.Should().BeOnOrAfter(before);
        svc.AcceptedAtUtc.Value.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Accept_Twice_KeepsFirstTimestamp()
    {
        var svc = new TermsAcceptanceService();

        svc.Accept();
        var first = svc.AcceptedAtUtc;
        svc.Accept();

        svc.IsAccepted.Should().BeTrue();
        svc.AcceptedAtUtc.Should().Be(first);
    }

    // ── Acceptance note ───────────────────────────────────────────

    [Fact]
    public void BuildAcceptanceNote_WithoutAcceptance_Throws()
    {
        var svc = new TermsAcceptanceService();

        var act = () => svc.BuildAcceptanceNote();

        act.Should().Throw<InvalidOperationException>(
            "the note documents a fact and must never fabricate one");
    }

    [Fact]
    public void BuildAcceptanceNote_AfterAcceptance_ContainsAllEvidenceFields()
    {
        var svc = new TermsAcceptanceService();
        svc.Accept();

        var note = svc.BuildAcceptanceNote();

        note.Should().Contain(TermsAcceptanceService.TermsVersion);
        note.Should().Contain(TermsAcceptanceService.TermsUrlDe);
        note.Should().Contain(TermsAcceptanceService.TermsUrlEn);
        note.Should().Contain(
            svc.AcceptedAtUtc!.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        // Bilingual — German is the authoritative language of the terms.
        note.Should().Contain("Annahmevermerk");
        note.Should().Contain("Acceptance Note");
        note.Should().Contain("The German version prevails.");
    }

    [Theory]
    [InlineData("de", TermsAcceptanceService.TermsUrlDe)]
    [InlineData("en", TermsAcceptanceService.TermsUrlEn)]
    public void TermsUrls_StayInSyncWithFooterLocalization(string language, string expectedUrl)
    {
        // Drift guard: the URLs baked into the acceptance note must be the
        // same ones the footer links to in the respective UI language.
        var localization = new LocalizationService { Language = language };

        localization.Get("Footer.TermsUrl").Should().Be(expectedUrl);
    }
}
