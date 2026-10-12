using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: EnvironmentResolver decides which environment a run uses. An empty console argument must not fall back to
/// DOTNET_ENVIRONMENT, because the rest of the run reads the raw console option and would run with an empty
/// environment (#32).
/// </summary>
public class EnvironmentResolverTests
{
    [Fact]
    public void Evaluate_ConsoleValueWithoutVariable_ReturnsConsoleValue()
    {
        // Act
        var (environment, origin, errorCode, errorMessage) = EnvironmentResolver.Evaluate("Docker", null, MigrationCommand.Info);

        // Assert
        environment.Should().Be("Docker");
        origin.Should().Be("console parameter [-Environment]");
        errorCode.Should().BeNull();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ConsoleValueEqualToVariable_ReturnsConsoleValue()
    {
        // Act
        var (environment, origin, errorCode, errorMessage) = EnvironmentResolver.Evaluate("Docker", "Docker", MigrationCommand.MigrateUp);

        // Assert
        environment.Should().Be("Docker");
        origin.Should().Be("console parameter [-Environment]");
        errorCode.Should().BeNull();
        errorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("Docker", "Production")]
    [InlineData("Docker", "docker")]
    public void Evaluate_ConsoleValueDiffersFromVariable_ReturnsErrorCode2(string consoleValue, string variable)
    {
        // Act
        var (environment, origin, errorCode, errorMessage) = EnvironmentResolver.Evaluate(consoleValue, variable, MigrationCommand.MigrateUp);

        // Assert
        errorCode.Should().Be(2, because: "the cross-check stays ordinal, as it was before #32");
        environment.Should().BeNull();
        origin.Should().BeNull();
        errorMessage.Should().Contain($"[{consoleValue}]").And.Contain($"[{variable}]");
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("", "Workshop")]
    [InlineData("   ", "Workshop")]
    public void Evaluate_EmptyConsoleValue_ReturnsErrorCode3WhetherOrNotTheVariableIsSet(string consoleValue, string? variable)
    {
        // Act
        var (environment, origin, errorCode, errorMessage) = EnvironmentResolver.Evaluate(consoleValue, variable, MigrationCommand.Info);

        // Assert
        errorCode.Should().Be(3, because: "the variable is only cross-checked and must not replace an empty argument (#32)");
        environment.Should().BeNull();
        origin.Should().BeNull();
        errorMessage.Should().Contain("Info command").And.Contain("[-Environment] is empty");
    }

    [Fact]
    public void Evaluate_EmptyConsoleValueWithVariable_MessageSaysTheVariableDoesNotReplaceTheArgument()
    {
        // Act
        var (_, _, _, errorMessage) = EnvironmentResolver.Evaluate("", "Workshop", MigrationCommand.Info);

        // Assert
        errorMessage.Should().Contain("[DOTNET_ENVIRONMENT] is set to value [Workshop]").And.Contain("does not replace it");
    }

    [Fact]
    public void Evaluate_EmptyConsoleValueWithoutVariable_MessageDoesNotMentionAVariableValue()
    {
        // Act
        var (_, _, _, errorMessage) = EnvironmentResolver.Evaluate("", null, MigrationCommand.Info);

        // Assert
        errorMessage.Should().NotContain("is set to value");
    }

    [Fact]
    public void Resolve_EmptyConsoleValue_ReturnsErrorCode3WhateverTheProcessVariableIs()
    {
        // Arrange
        var options = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.Info,
            Product = "BookStore",
            Environment = "",
            RunMode = MigrationRunMode.Migrate,
            ShowStartupInfo = false,
            RevealSensitiveData = false,
        };

        // Act
        var (environment, origin, errorCode) = EnvironmentResolver.Resolve(options, assemblyInfo: "banner");

        // Assert
        errorCode.Should().Be(3, because: "Resolve reads the real DOTNET_ENVIRONMENT of the test process and the outcome must not depend on it (#32)");
        environment.Should().BeNull();
        origin.Should().BeNull();
    }
}
