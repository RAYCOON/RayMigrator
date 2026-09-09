using System.Text.RegularExpressions;
using AwesomeAssertions;
using Raycoon.RayMigrator.Shared;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: the version string RayMigrator shows and records in MigratorMeta. Release builds carry the bare tag
/// version; builds outside the release workflows are pre-releases ("-dev") and keep a short commit hash so
/// that a repository created from develop names the exact commit whose templates created it.
/// </summary>
public class AssemblyInfoHelperVersionTests
{
    [Theory]
    [InlineData("0.14.0+33e1c023e81222a480d37bac0a922d76a73cc31b", "0.14.0")]
    [InlineData("0.14.0", "0.14.0")]
    [InlineData("0.14.0-dev+39b6fa3c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a", "0.14.0-dev+39b6fa3")]
    [InlineData("0.14.0-dev+abc", "0.14.0-dev+abc")]
    [InlineData("0.14.0-dev", "0.14.0-dev")]
    [InlineData("0.14.0-dev+", "0.14.0-dev")]
    [InlineData("", "")]
    public void FormatVersion_KeepsShortHashOnlyForPreReleases(string informationalVersion, string expected)
    {
        AssemblyInfoHelper.FormatVersion(informationalVersion).Should().Be(expected);
    }

    [Fact]
    public void GetRayMigratorVersion_ReadsTheEngineAssembly_NotTheEntryAssembly()
    {
        // The test host is the entry assembly; the value must still be the engine's version, which under
        // Directory.Build.props is either a release tag or "<prefix>-dev[+<7-char hash>]".
        var version = AssemblyInfoHelper.GetRayMigratorVersion();

        version.Should().MatchRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+(\+[0-9a-f]{1,7})?)?$");
        version.Should().NotStartWith("15.", "the xunit test host version must not leak in");
    }

    [Fact]
    public void GetRayMigratorVersion_MatchesTheInformationalVersionOfTheSharedAssembly()
    {
        var attribute = typeof(AssemblyInfoHelper).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Single();

        AssemblyInfoHelper.GetRayMigratorVersion().Should().Be(AssemblyInfoHelper.FormatVersion(attribute.InformationalVersion));
        Regex.IsMatch(attribute.InformationalVersion, @"^\d+\.\d+\.\d+").Should().BeTrue();
    }
}
