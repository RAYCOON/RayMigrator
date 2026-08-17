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
}
