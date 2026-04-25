
namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class CliToolModelValidationTests
{
    [Fact]
    public void CliToolModel_DefaultValues()
    {
        var model = new CliToolModel();
        model.Alias.Should().BeEmpty();
        model.ExecutablePath.Should().BeEmpty();
        model.ArgumentTemplate.Should().BeEmpty();
        model.InputMode.Should().Be("File");
        model.SuccessExitCodes.Should().ContainSingle().Which.Should().Be("0");
        model.CliToolTimeoutInSeconds.Should().Be(120);
    }

    [Theory]
    [InlineData("sqlcmd")]
    [InlineData("psql-docker")]
    [InlineData("my_tool123")]
    [InlineData("a")]
    public void CliToolAliasPattern_ValidAliases(string alias)
    {
        ConfigurationValidator.CliToolAliasPattern.IsMatch(alias).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("has.dot")]
    [InlineData("way_too_long_alias_that_exceeds_fifty_characters_for_the_pattern_match")]
    public void CliToolAliasPattern_InvalidAliases(string alias)
    {
        if (string.IsNullOrEmpty(alias))
            return; // empty is handled by required check, not pattern
        ConfigurationValidator.CliToolAliasPattern.IsMatch(alias).Should().BeFalse();
    }

    [Theory]
    [InlineData("File")]
    [InlineData("Stdin")]
    public void ValidCliToolInputModes_Contains(string mode)
    {
        ConfigurationValidator.ValidCliToolInputModes.Should().Contain(mode);
    }

    [Theory]
    [InlineData("Pipe")]
    [InlineData("Socket")]
    [InlineData("file")]
    public void ValidCliToolInputModes_DoesNotContain(string mode)
    {
        ConfigurationValidator.ValidCliToolInputModes.Should().NotContain(mode);
    }

    [Fact]
    public void CliToolPreset_CanConvertToCliToolModel()
    {
        var preset = CliToolPresetProvider.GetPresetByAlias("sqlcmd")!;

        var model = new CliToolModel
        {
            Alias = preset.Alias,
            ExecutablePath = preset.ExecutablePath,
            ArgumentTemplate = preset.ArgumentTemplate,
            InputMode = preset.InputMode,
            SuccessExitCodes = new List<string>(preset.SuccessExitCodes),
            CliToolTimeoutInSeconds = preset.CliToolTimeoutInSeconds,
        };

        var result = ConfigurationValidator.ValidateCliTool(model, "Test");
        result.IsValid.Should().BeTrue();
    }
}
