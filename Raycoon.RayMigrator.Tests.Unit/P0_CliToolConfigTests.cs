using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: CliToolOptions configuration model tests.
/// InputMode has no default: an unset value resolves to Undefined and is rejected by configuration validation (#19).
/// </summary>
public class CliToolOptionsInputModeTests
{
    [Fact]
    public void InputModeEnum_NullInputMode_IsUndefined()
    {
        var tool = new CliToolOptions { InputMode = null };

        tool.InputModeEnum.Should().Be(CliToolInputMode.Undefined);
    }

    [Fact]
    public void InputModeEnum_EmptyStringInputMode_IsUndefined()
    {
        var tool = new CliToolOptions { InputMode = string.Empty };

        tool.InputModeEnum.Should().Be(CliToolInputMode.Undefined);
    }

    [Fact]
    public void InputModeEnum_FileString_ParsedCorrectly()
    {
        var tool = new CliToolOptions { InputMode = "File" };

        tool.InputModeEnum.Should().Be(CliToolInputMode.File);
    }

    [Fact]
    public void InputModeEnum_StdinString_ParsedCorrectly()
    {
        var tool = new CliToolOptions { InputMode = "Stdin" };

        tool.InputModeEnum.Should().Be(CliToolInputMode.Stdin);
    }

    [Fact]
    public void InputModeEnum_InvalidValue_Throws()
    {
        var tool = new CliToolOptions { InputMode = "Pipe" };

        var act = () => tool.InputModeEnum;

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Invalid value [Pipe] for property [InputMode]. Allowed values: [File, Stdin].*");
    }

    [Fact]
    public void InputModeEnum_CaseVariant_FileUppercase_ParsesCorrectly()
    {
        var tool = new CliToolOptions { InputMode = "FILE" };

        tool.InputModeEnum.Should().Be(CliToolInputMode.File);
    }

    [Fact]
    public void InputModeEnum_CaseVariant_StdinLowercase_ParsesCorrectly()
    {
        var tool = new CliToolOptions { InputMode = "stdin" };

        tool.InputModeEnum.Should().Be(CliToolInputMode.Stdin);
    }

    [Fact]
    public void InputModeEnum_UndefinedString_Throws()
    {
        // "Undefined" is the enum's "not set" sentinel, not a configurable value. The validator
        // rejects it (RayEnumAttribute skips the first member) and so does the getter.
        var tool = new CliToolOptions { InputMode = "Undefined" };

        var act = () => tool.InputModeEnum;

        act.Should().Throw<ConfigurationValidationException>();
    }

    [Fact]
    public void InputModeEnum_CalledTwice_ReturnsCachedValue()
    {
        var tool = new CliToolOptions { InputMode = "Stdin" };

        var first = tool.InputModeEnum;
        var second = tool.InputModeEnum;

        first.Should().Be(CliToolInputMode.Stdin);
        second.Should().Be(CliToolInputMode.Stdin);
    }
}
