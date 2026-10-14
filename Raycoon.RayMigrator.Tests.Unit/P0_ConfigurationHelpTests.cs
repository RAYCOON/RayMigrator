using System.Globalization;
using AwesomeAssertions;
using Raycoon.RayMigrator.Shared.Configuration.Help;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: the help texts of the configuration (English and German) are complete for every declared key.
/// </summary>
public class ConfigurationHelpTests
{
    [Theory]
    [InlineData("Welcome")]
    [InlineData("Root")]
    [InlineData("Repository")]
    [InlineData("DatabaseLogging")]
    [InlineData("ProductDefaults")]
    [InlineData("Product")]
    [InlineData("TargetGroup")]
    [InlineData("Target")]
    [InlineData("Serilog")]
    [InlineData("CliTools")]
    [InlineData("Products")]
    [InlineData("TargetGroups")]
    [InlineData("Targets")]
    public void GetSectionHelp_AllKeys_ReturnNonNull_English(string key)
    {
        var help = ConfigurationHelp.GetSectionHelp(key, CultureInfo.InvariantCulture);
        help.Should().NotBeNull();
        help!.Title.Should().NotBeNullOrWhiteSpace();
        help.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("Welcome")]
    [InlineData("Repository")]
    [InlineData("CliTools")]
    public void GetSectionHelp_GermanLocale_ReturnsDifferentText(string key)
    {
        var enHelp = ConfigurationHelp.GetSectionHelp(key, CultureInfo.InvariantCulture);
        var deHelp = ConfigurationHelp.GetSectionHelp(key, new CultureInfo("de"));

        deHelp.Should().NotBeNull();
        deHelp!.Title.Should().NotBeNullOrWhiteSpace();
        // German and English should differ (at least for keys with translations)
        if (key != "Repository" && key != "Serilog")
            deHelp.Title.Should().NotBe(enHelp!.Title);
    }

    [Fact]
    public void GetSectionHelp_UnknownKey_ReturnsNull()
    {
        var help = ConfigurationHelp.GetSectionHelp("NonExistentSection");
        help.Should().BeNull();
    }

    [Fact]
    public void GetAllSectionKeys_Returns13Keys()
    {
        ConfigurationHelp.GetAllSectionKeys().Should().HaveCount(13);
    }

    [Fact]
    public void GetAllFieldKeys_ReturnsAtLeast40Keys()
    {
        ConfigurationHelp.GetAllFieldKeys().Count.Should().BeGreaterThanOrEqualTo(40);
    }

    [Fact]
    public void GetFieldHelp_RepositoryDatabaseType_ReturnsNonNull()
    {
        var help = ConfigurationHelp.GetFieldHelp("Repository_DatabaseType", CultureInfo.InvariantCulture);
        help.Should().NotBeNull();
        help!.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetFieldHelp_CliToolAlias_ReturnsNonNull()
    {
        var help = ConfigurationHelp.GetFieldHelp("CliTool_Alias", CultureInfo.InvariantCulture);
        help.Should().NotBeNull();
        help!.Title.Should().Contain("CLI Tool");
    }

    [Fact]
    public void GetFieldHelp_TargetUseCliToolAlias_ReturnsNonNull()
    {
        var help = ConfigurationHelp.GetFieldHelp("Target_UseCliToolAlias", CultureInfo.InvariantCulture);
        help.Should().NotBeNull();
    }

    [Fact]
    public void GetFieldHelp_UnknownKey_ReturnsNull()
    {
        var help = ConfigurationHelp.GetFieldHelp("NonExistent_Field");
        help.Should().BeNull();
    }

    [Fact]
    public void GetFieldHelp_RepositoryDatabaseType_HasJsonPath()
    {
        var help = ConfigurationHelp.GetFieldHelp("Repository_DatabaseType", CultureInfo.InvariantCulture);

        help.Should().NotBeNull();
        help!.JsonPath.Should().NotBeNull();
        help.JsonPath!.ConfigPath.Should().Be("Repository.DatabaseType");
    }

    [Fact]
    public void GetFieldHelp_FieldWithoutRegisteredPath_HasNullJsonPath()
    {
        var help = ConfigurationHelp.GetFieldHelp("Concept_Environment", CultureInfo.InvariantCulture);

        help.Should().NotBeNull();
        help!.JsonPath.Should().BeNull();
    }

    [Fact]
    public void GetFieldHelp_AllDeclaredKeys_ReturnNonNull()
    {
        foreach (var key in ConfigurationHelp.GetAllFieldKeys())
        {
            var help = ConfigurationHelp.GetFieldHelp(key, CultureInfo.InvariantCulture);
            help.Should().NotBeNull($"Field help for key '{key}' should be defined in the resource file");
        }
    }

    [Fact]
    public void GetFieldHelp_AllDeclaredKeys_ReturnNonNull_German()
    {
        var culture = new CultureInfo("de");
        foreach (var key in ConfigurationHelp.GetAllFieldKeys())
        {
            var help = ConfigurationHelp.GetFieldHelp(key, culture);
            help.Should().NotBeNull($"German field help for key '{key}' should be defined in FieldHelp.de.resx");
            help!.Title.Should().NotBeNullOrWhiteSpace($"German title for key '{key}' should not be empty");
            help.Description.Should().NotBeNullOrWhiteSpace($"German description for key '{key}' should not be empty");
        }
    }
}
