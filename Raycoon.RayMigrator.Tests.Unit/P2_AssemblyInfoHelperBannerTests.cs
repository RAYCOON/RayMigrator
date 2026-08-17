using AwesomeAssertions;
using Raycoon.RayMigrator.Shared;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// The CLI banner is the only licence-acceptance touchpoint on the dotnet-CLI
/// and GitHub-release distribution channels (neither shows any acceptance
/// prompt). These tests pin the maturity notice and the licence notice so
/// neither can silently disappear from the banner.
/// </summary>
public class P2_AssemblyInfoHelperBannerTests
{
    [Fact]
    public void GetAsciiLogoLines_ContainsMaturityNotice()
    {
        var lines = AssemblyInfoHelper.GetAsciiLogoLines("0.11.0");

        lines.Should().Contain(l => l.Contains("Pre-1.0"));
        lines.Should().Contain(l => l.Contains("Back up before every run"));
    }

    [Fact]
    public void GetAsciiLogoLines_ContainsLicenceNotice()
    {
        var lines = AssemblyInfoHelper.GetAsciiLogoLines("0.11.0");

        lines.Should().Contain(l => l.Contains("BUSL-1.1"));
        lines.Should().Contain(l => l.Contains("LICENSE.md"));
        lines.Should().Contain(l => l.Contains("constitutes acceptance"));
    }

    [Fact]
    public void GetAsciiLogoLines_ContainsRequestedVersion()
    {
        var lines = AssemblyInfoHelper.GetAsciiLogoLines("1.2.3");

        lines.Should().Contain(l => l.Contains("Version 1.2.3"));
    }
}
