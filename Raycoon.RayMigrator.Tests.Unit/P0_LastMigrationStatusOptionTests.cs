using System.CommandLine;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: <c>fix --last-migration-status</c> (<c>-lms</c>) has no default. Without the option every open record of an orphaned
/// run keeps its progress (the console options carry no status, and the engine applies its rule); <c>migrated</c> and
/// <c>not-migrated</c> set that status on every open record. An unknown value is a parse error, and a direct call to the
/// parser throws instead of falling back to a status.
/// </summary>
public class LastMigrationStatusOptionTests
{
    private static async Task<(CommandLineConfiguration Config, ParseResult Parse)> ParseAsync(params string[] args)
    {
        var config = new CommandLineConfiguration("RayMigrator Test");
        var parse = config.RootCommand.Parse(args);
        await parse.InvokeAsync(null, TestContext.Current.CancellationToken);
        return (config, parse);
    }

    [Fact]
    public async Task Fix_WithoutLastMigrationStatus_LeavesTheStatusToTheRepairRule()
    {
        // Act
        var (config, parse) = await ParseAsync("fix", "-p", "P", "-env", "Dev");

        // Assert
        parse.Errors.Should().BeEmpty();
        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.FixAssumedMigrationStatus.Should().BeNull(
            because: "without the option every open record keeps its progress instead of becoming NotMigrated");
    }

    [Theory]
    [InlineData("--last-migration-status", "migrated", MigrationStatus.Migrated)]
    [InlineData("--last-migration-status", "not-migrated", MigrationStatus.NotMigrated)]
    [InlineData("-lms", "Migrated", MigrationStatus.Migrated)]
    [InlineData("-lms", "NOT-MIGRATED", MigrationStatus.NotMigrated)]
    public async Task Fix_WithLastMigrationStatus_SetsThatStatus(string optionForm, string value, MigrationStatus expected)
    {
        // Act
        var (config, parse) = await ParseAsync("fix", "-p", "P", "-env", "Dev", optionForm, value);

        // Assert
        parse.Errors.Should().BeEmpty();
        config.ParsedOptions!.FixAssumedMigrationStatus.Should().Be(expected);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("bogus")]
    public async Task Fix_WithUnknownLastMigrationStatus_IsAParseError(string value)
    {
        // Act
        var (config, parse) = await ParseAsync("fix", "-p", "P", "-env", "Dev", "--last-migration-status", value);

        // Assert
        parse.Errors.Should().ContainSingle(e => e.Message.Contains("--last-migration-status") && e.Message.Contains(value));
        config.ParsedOptions.Should().BeNull(because: "a rejected --last-migration-status must not produce console options");
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("failed")]
    public void ParseLastMigrationStatus_ThrowsForUnknownValue(string value)
    {
        // Act
        var act = () => CommandLineConfiguration.ParseLastMigrationStatus(value);

        // Assert
        act.Should().Throw<ConfigurationValidationException>(because: "an unknown value must never silently become a status")
            .WithMessage("*--last-migration-status*migrated, not-migrated*");
    }
}
