using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// Pins which <see cref="MigrationRunMode"/> each CLI verb runs in (#5, #6).
/// Only migrate-up and migrate-down expose --run-mode; every other verb runs in
/// <see cref="MigrationRunMode.Migrate"/> and decides its side effects through its command profile (#6).
/// The mapping is implicit in <see cref="CommandLineConfiguration"/> and used to be
/// unobservable — until validate-hash's former <see cref="MigrationRunMode.Validate"/> leaked into the
/// repository query filter (#5). Any change to this mapping must be a conscious one.
/// </summary>
public class CommandRunModeMappingTests
{
    private static async Task<(CommandLineConfiguration Config, System.CommandLine.ParseResult Parse)> ParseAsync(params string[] args)
    {
        var config = new CommandLineConfiguration("RayMigrator Test");
        var parse = config.RootCommand.Parse(args);
        await parse.InvokeAsync(null, TestContext.Current.CancellationToken);
        return (config, parse);
    }

    [Theory]
    [InlineData("validate-hash", MigrationCommand.ValidateHash, MigrationRunMode.Migrate)]
    [InlineData("update-hash", MigrationCommand.UpdateHash, MigrationRunMode.Migrate)]
    [InlineData("info", MigrationCommand.Info, MigrationRunMode.Migrate)]
    [InlineData("baseline", MigrationCommand.Baseline, MigrationRunMode.Migrate)]
    [InlineData("fix", MigrationCommand.FixIssues, MigrationRunMode.Migrate)]
    public async Task NonMigrateVerb_RunsInFixedRunMode(string verb, MigrationCommand expectedCommand, MigrationRunMode expectedRunMode)
    {
        var (config, _) = await ParseAsync(verb, "-p", "P", "-env", "Dev");

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.Command.Should().Be(expectedCommand);
        config.ParsedOptions.RunMode.Should().Be(expectedRunMode,
            $"the run mode of '{verb}' is fixed by its CLI handler and must not change unnoticed");
    }

    [Fact]
    public async Task FixDryRun_KeepsMigrateRunMode_AndSetsFixDryRun()
    {
        // fix --dry-run is the fix command's own dry-run concept; it does not borrow --run-mode simulate (#6).
        var (config, _) = await ParseAsync("fix", "-p", "P", "-env", "Dev", "--dry-run");

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.Command.Should().Be(MigrationCommand.FixIssues);
        config.ParsedOptions.RunMode.Should().Be(MigrationRunMode.Migrate);
        config.ParsedOptions.FixDryRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("migrate-up", MigrationCommand.MigrateUp)]
    [InlineData("migrate-down", MigrationCommand.MigrateDown)]
    public async Task MigrateVerb_DefaultsToMigrateRunMode(string verb, MigrationCommand expectedCommand)
    {
        var args = verb == "migrate-down"
            ? new[] { verb, "-p", "P", "-env", "Dev", "-tr", "1.0" }
            : new[] { verb, "-p", "P", "-env", "Dev" };

        var (config, _) = await ParseAsync(args);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.Command.Should().Be(expectedCommand);
        config.ParsedOptions.RunMode.Should().Be(MigrationRunMode.Migrate, "--run-mode defaults to migrate");
    }

    [Theory]
    [InlineData("migrate-up", "simulate", MigrationRunMode.Simulate)]
    [InlineData("migrate-up", "validate", MigrationRunMode.Validate)]
    [InlineData("migrate-down", "simulate", MigrationRunMode.Simulate)]
    [InlineData("migrate-down", "validate", MigrationRunMode.Validate)]
    public async Task MigrateVerb_HonoursRunModeOption(string verb, string runMode, MigrationRunMode expected)
    {
        var args = verb == "migrate-down"
            ? new[] { verb, "-p", "P", "-env", "Dev", "-tr", "1.0", "-rm", runMode }
            : new[] { verb, "-p", "P", "-env", "Dev", "-rm", runMode };

        var (config, _) = await ParseAsync(args);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.RunMode.Should().Be(expected);
    }

    [Theory]
    [InlineData("validate-hash")]
    [InlineData("update-hash")]
    [InlineData("info")]
    [InlineData("baseline")]
    [InlineData("fix")]
    public async Task NonMigrateVerb_DoesNotAcceptRunModeOption(string verb)
    {
        var (config, parse) = await ParseAsync(verb, "-p", "P", "-env", "Dev", "-rm", "simulate");

        parse.Errors.Should().NotBeEmpty($"'{verb}' has no --run-mode option; its run mode is not a user choice");
        config.ParsedOptions.Should().BeNull();
    }
}
