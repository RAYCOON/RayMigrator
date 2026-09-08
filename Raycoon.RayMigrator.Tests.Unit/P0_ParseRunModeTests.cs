using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: <c>--run-mode</c> has no silent default (#6). An unknown value is a parse error on the CLI, and a direct
/// call to the parser throws instead of falling back to <see cref="MigrationRunMode.Migrate"/>, the most dangerous mode.
/// </summary>
public class ParseRunModeTests
{
    private static async Task<(CommandLineConfiguration Config, System.CommandLine.ParseResult Parse)> ParseAsync(params string[] args)
    {
        var config = new CommandLineConfiguration("RayMigrator Test");
        var parse = config.RootCommand.Parse(args);
        await parse.InvokeAsync(null, TestContext.Current.CancellationToken);
        return (config, parse);
    }

    [Theory]
    [InlineData("migrate", MigrationRunMode.Migrate)]
    [InlineData("Migrate", MigrationRunMode.Migrate)]
    [InlineData("SIMULATE", MigrationRunMode.Simulate)]
    [InlineData("simulate", MigrationRunMode.Simulate)]
    [InlineData("Validate", MigrationRunMode.Validate)]
    [InlineData("validate", MigrationRunMode.Validate)]
    public void ParseRunMode_AcceptsKnownValuesCaseInsensitively(string value, MigrationRunMode expected)
    {
        CommandLineConfiguration.ParseRunMode(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("migrat")]
    [InlineData("dry-run")]
    public void ParseRunMode_ThrowsForUnknownValue(string value)
    {
        var act = () => CommandLineConfiguration.ParseRunMode(value);

        act.Should().Throw<ConfigurationValidationException>(
                "an unknown run mode must never silently become Migrate")
           .WithMessage("*Invalid value*--run-mode*migrate, simulate, validate*");
    }

    [Theory]
    [InlineData("migrate-up")]
    [InlineData("migrate-down")]
    public async Task MigrateVerb_WithUnknownRunMode_IsAParseError(string verb)
    {
        var args = verb == "migrate-down"
            ? new[] { verb, "-p", "P", "-env", "Dev", "-tr", "1.0", "-rm", "bogus" }
            : new[] { verb, "-p", "P", "-env", "Dev", "-rm", "bogus" };

        var (config, parse) = await ParseAsync(args);

        parse.Errors.Should().Contain(e => e.Message.Contains("--run-mode") && e.Message.Contains("bogus"),
            "the option validator reports unknown run modes before any handler runs");
        config.ParsedOptions.Should().BeNull("a rejected --run-mode must not produce console options");
    }
}
