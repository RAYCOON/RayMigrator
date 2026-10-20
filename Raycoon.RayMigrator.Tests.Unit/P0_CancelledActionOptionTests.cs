using System.CommandLine;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: <c>--cancelled-action</c> (<c>-ca</c>) decides what a run does when Ctrl+C or SIGTERM cancels the CLI. The three
/// run commands take it with the same values, so that a script can pass it to each of them; no other command takes it.
/// An unknown value is a parse error and never silently becomes Terminate, like an unknown <c>--run-mode</c>.
/// </summary>
public class CancelledActionOptionTests
{
    private static readonly string[] RunVerbs = ["migrate-up", "migrate-down", "baseline"];

    private static async Task<(CommandLineConfiguration Config, ParseResult Parse)> ParseAsync(params string[] args)
    {
        var config = new CommandLineConfiguration("RayMigrator Test");
        var parse = config.RootCommand.Parse(args);
        await parse.InvokeAsync(null, TestContext.Current.CancellationToken);
        return (config, parse);
    }

    /// <summary>The arguments of a run command that parse on their own (migrate-down needs --to-release), plus <paramref name="extra"/>.</summary>
    private static string[] RunArgs(string verb, params string[] extra)
    {
        string[] required = verb == "migrate-down"
            ? [verb, "-p", "P", "-env", "Dev", "-tr", "1.0"]
            : [verb, "-p", "P", "-env", "Dev"];
        return [.. required, .. extra];
    }

    public static TheoryData<string, string, string, MigrationCancelledAction> EveryRunVerbOptionFormAndValue()
    {
        (string Value, MigrationCancelledAction Action)[] values =
        [
            ("terminate", MigrationCancelledAction.Terminate),
            ("rollback-file", MigrationCancelledAction.RollbackFile),
            ("rollback-run", MigrationCancelledAction.RollbackRun),
            ("rollback-release", MigrationCancelledAction.RollbackRelease),
        ];

        var data = new TheoryData<string, string, string, MigrationCancelledAction>();
        foreach (var verb in RunVerbs)
        {
            foreach (var optionForm in new[] { "--cancelled-action", "-ca" })
            {
                foreach (var (value, action) in values)
                {
                    data.Add(verb, optionForm, value, action);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryRunVerbOptionFormAndValue))]
    public async Task RunVerb_CancelledActionValue_IsParsed(string verb, string optionForm, string value, MigrationCancelledAction expected)
    {
        // Act
        var (config, parse) = await ParseAsync(RunArgs(verb, optionForm, value));

        // Assert
        parse.Errors.Should().BeEmpty();
        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.CancelledAction.Should().Be(expected, because: $"{verb} takes {optionForm} {value}");
    }

    [Theory]
    [InlineData("migrate-up")]
    [InlineData("migrate-down")]
    [InlineData("baseline")]
    public async Task RunVerb_WithoutCancelledAction_DefaultsToTerminate(string verb)
    {
        // Act
        var (config, parse) = await ParseAsync(RunArgs(verb));

        // Assert
        parse.Errors.Should().BeEmpty();
        config.ParsedOptions!.CancelledAction.Should().Be(MigrationCancelledAction.Terminate,
            because: "a cancelled run that is not told otherwise stops and leaves the interrupted file for the next migrate-up");
    }

    [Theory]
    [InlineData("Rollback-File", MigrationCancelledAction.RollbackFile)]
    [InlineData("TERMINATE", MigrationCancelledAction.Terminate)]
    [InlineData("Rollback-Release", MigrationCancelledAction.RollbackRelease)]
    public async Task CancelledAction_IsCaseInsensitive(string value, MigrationCancelledAction expected)
    {
        // Act
        var (config, parse) = await ParseAsync(RunArgs("migrate-up", "-ca", value));

        // Assert
        parse.Errors.Should().BeEmpty();
        config.ParsedOptions!.CancelledAction.Should().Be(expected);
    }

    [Theory]
    [InlineData("migrate-up", "bogus")]
    [InlineData("migrate-up", "rollback")]
    [InlineData("migrate-down", "rollbackfile")]
    [InlineData("baseline", "ignore")]
    public async Task RunVerb_UnknownCancelledAction_IsAParseError(string verb, string value)
    {
        // Act
        var (config, parse) = await ParseAsync(RunArgs(verb, "--cancelled-action", value));

        // Assert
        parse.Errors.Should().ContainSingle(e => e.Message.Contains("--cancelled-action") && e.Message.Contains(value)
                && e.Message.Contains("terminate, rollback-file, rollback-run, rollback-release"),
            because: "the option validator rejects an unknown action before any handler runs");
        config.ParsedOptions.Should().BeNull(because: "a rejected --cancelled-action must not produce console options");
    }

    [Theory]
    [InlineData("validate-hash", "--cancelled-action")]
    [InlineData("update-hash", "-ca")]
    [InlineData("info", "--cancelled-action")]
    [InlineData("fix", "-ca")]
    public async Task OtherVerb_DoesNotAcceptCancelledAction(string verb, string optionForm)
    {
        // Act
        var (config, parse) = await ParseAsync(verb, "-p", "P", "-env", "Dev", optionForm, "terminate");

        // Assert
        parse.Errors.Should().NotBeEmpty(because: $"{verb} is not a run; it stops when cancelled and has no action to take");
        config.ParsedOptions.Should().BeNull();
    }

    [Fact]
    public void CancelledActionOption_ExistsOnTheThreeRunCommandsOnly()
    {
        // Arrange
        var config = new CommandLineConfiguration("RayMigrator Test");

        // Act
        var commandsWithOption = config.RootCommand.Subcommands
            .Where(c => c.Options.Any(o => o.Name == "--cancelled-action"))
            .Select(c => c.Name);

        // Assert
        commandsWithOption.Should().BeEquivalentTo(RunVerbs,
            because: "migrate-up, migrate-down and baseline take the same options so that scripts can set them alike");
    }

    [Fact]
    public void CancelledActionAlias_CollidesWithNoOtherOptionOfAnyCommand()
    {
        // Arrange
        var config = new CommandLineConfiguration("RayMigrator Test");
        var recursiveRootOptions = config.RootCommand.Options.Where(o => o.Recursive).ToList();

        foreach (var command in config.RootCommand.Subcommands)
        {
            // Act
            var names = command.Options.Concat(recursiveRootOptions)
                .SelectMany(o => o.Aliases.Prepend(o.Name))
                .ToList();

            // Assert
            names.Should().OnlyHaveUniqueItems(because: $"every option name and alias of {command.Name} must be unique");
            names.Count(n => n == "-ca").Should().Be(RunVerbs.Contains(command.Name) ? 1 : 0,
                because: $"-ca belongs to --cancelled-action and exists on the run commands only ({command.Name})");
        }
    }

    [Theory]
    [InlineData("migrate-up")]
    [InlineData("migrate-down")]
    [InlineData("baseline")]
    public void CancelledActionOption_HelpDefault_IsTerminate(string verb)
    {
        // Arrange
        var config = new CommandLineConfiguration("RayMigrator Test");

        // Act
        var option = config.RootCommand.Subcommands.Single(c => c.Name == verb)
            .Options.OfType<Option<string>>().Single(o => o.Name == "--cancelled-action");

        // Assert
        option.GetDefaultValue().Should().Be("terminate");
        option.Aliases.Should().Contain("-ca");
    }

    [Fact]
    public async Task CancelledAction_FromEnvironmentVariable_IsResolved()
    {
        // Arrange
        string variable = $"RM_TEST_CANCELLED_ACTION_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, "rollback-release");
        try
        {
            // Act
            var (config, parse) = await ParseAsync(RunArgs("baseline", "-ca", $"{{ENV:{variable}}}"));

            // Assert
            parse.Errors.Should().BeEmpty();
            config.ParsedOptions!.CancelledAction.Should().Be(MigrationCancelledAction.RollbackRelease,
                because: "every string option of the CLI resolves {ENV:...}");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("rollbackfile")]
    [InlineData("10")]
    public void ParseCancelledAction_ThrowsForUnknownValue(string value)
    {
        // Act
        var act = () => CommandLineConfiguration.ParseCancelledAction(value);

        // Assert
        act.Should().Throw<ConfigurationValidationException>(because: "an unknown action must never silently become Terminate")
            .WithMessage("*Invalid value*--cancelled-action*terminate, rollback-file, rollback-run, rollback-release*");
    }

    [Fact]
    public void CancelledActionValues_CoverEveryActionAndAreSpelledAlikeInTheWarning()
    {
        // Act
        var parsed = CommandLineConfiguration.CancelledActionValues.Select(CommandLineConfiguration.ParseCancelledAction).Order();

        // Assert
        parsed.Should().Equal(Enum.GetValues<MigrationCancelledAction>().Where(a => a != MigrationCancelledAction.Undefined).Order(),
            because: "every action a host can choose must be reachable from the CLI");
        CommandLineConfiguration.CancelledActionValues.Should().AllSatisfy(value =>
            ConsoleCancellation.OptionValueOf(CommandLineConfiguration.ParseCancelledAction(value)).Should().Be(value,
                because: "the warning of the first signal names the action as the option spells it"));
    }
}
