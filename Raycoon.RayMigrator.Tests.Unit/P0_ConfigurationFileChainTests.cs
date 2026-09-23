using AwesomeAssertions;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: names and order of the appsettings hierarchy, shared by the engine and the Config Wizard (#23).
/// </summary>
public class ConfigurationFileChainTests
{
    [Fact]
    public void FileNamesFor_ReturnsFourNamesInMergeOrder()
    {
        var files = ConfigurationFileChain.FileNamesFor("Shop", "Production");

        files.Select(f => f.FileName).Should().Equal(
            "appsettings.json", "appsettings.Production.json", "appsettings.Shop.json", "appsettings.Shop.Production.json");
        files.Select(f => f.Role).Should().Equal(
            ConfigFileRole.Base, ConfigFileRole.Environment, ConfigFileRole.Product, ConfigFileRole.ProductEnvironment);
    }

    [Fact]
    public void FileNamesFor_BlankEnvironmentOrProduct_OmitsTheirFiles()
    {
        ConfigurationFileChain.FileNamesFor("Shop", "").Select(f => f.FileName)
            .Should().Equal("appsettings.json", "appsettings.Shop.json");
        ConfigurationFileChain.FileNamesFor(null, "Production").Select(f => f.FileName)
            .Should().Equal("appsettings.json", "appsettings.Production.json");
    }

    [Fact]
    public void TryClassify_RoundTrips_FileNamesFor()
    {
        foreach (var (role, fileName) in ConfigurationFileChain.FileNamesFor("Shop", "Production"))
        {
            ConfigurationFileChain.TryClassify(fileName, out var classified, out var product, out var environment, new[] { "Shop" })
                .Should().BeTrue(fileName);
            classified.Should().Be(role, fileName);
            (product ?? "").Should().Be(role is ConfigFileRole.Product or ConfigFileRole.ProductEnvironment ? "Shop" : "");
            (environment ?? "").Should().Be(role is ConfigFileRole.Environment or ConfigFileRole.ProductEnvironment ? "Production" : "");
        }
    }

    [Fact]
    public void TryClassify_SingleSegment_IsProductOnlyWhenKnown()
    {
        ConfigurationFileChain.TryClassify("appsettings.Shop.json", out var role, out var product, out var environment, new[] { "shop" }).Should().BeTrue();
        role.Should().Be(ConfigFileRole.Product);
        product.Should().Be("Shop");
        environment.Should().BeNull();

        ConfigurationFileChain.TryClassify("appsettings.Shop.json", out role, out product, out environment).Should().BeTrue();
        role.Should().Be(ConfigFileRole.Environment);
        environment.Should().Be("Shop");
        product.Should().BeNull();
    }

    [Fact]
    public void TryClassify_AcceptsPathsAndAnyCasing_RejectsUnrelatedNames()
    {
        ConfigurationFileChain.TryClassify(@"C:\cfg\APPSETTINGS.Prod.JSON", out var role, out _, out var environment).Should().BeTrue();
        role.Should().Be(ConfigFileRole.Environment);
        environment.Should().Be("Prod");

        ConfigurationFileChain.TryClassify("appsettings.Shop.Sub.Production.json", out role, out var product, out environment).Should().BeTrue();
        role.Should().Be(ConfigFileRole.ProductEnvironment);
        product.Should().Be("Shop.Sub");
        environment.Should().Be("Production");

        ConfigurationFileChain.TryClassify("settings.json", out _, out _, out _).Should().BeFalse();
        ConfigurationFileChain.TryClassify("appsettings.txt", out _, out _, out _).Should().BeFalse();
        ConfigurationFileChain.TryClassify("appsettingsX.json", out _, out _, out _).Should().BeFalse();
        ConfigurationFileChain.TryClassify("appsettings..json", out _, out _, out _).Should().BeFalse();
        ConfigurationFileChain.TryClassify("", out _, out _, out _).Should().BeFalse();
    }
}
