using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// Pins the lowercase CLI convention (#2): command verbs are kebab-lowercase and matched
/// case-sensitively by System.CommandLine, the former --TargetGroup-MigrationOrder option is
/// --target-group-migration-order, and enum-valued option values are accepted in any case.
/// </summary>
public class CommandVerbCasingTests
{
    private static async Task<(CommandLineConfiguration Config, System.CommandLine.ParseResult Parse)> ParseAsync(params string[] args)
    {
        var config = new CommandLineConfiguration("RayMigrator Test");
        var parse = config.RootCommand.Parse(args);
        await parse.InvokeAsync(null, TestContext.Current.CancellationToken);
        return (config, parse);
    }

    [Theory]
    [InlineData("migrate-up", MigrationCommand.MigrateUp)]
    [InlineData("migrate-down", MigrationCommand.MigrateDown)]
    [InlineData("validate-hash", MigrationCommand.ValidateHash)]
    [InlineData("update-hash", MigrationCommand.UpdateHash)]
    [InlineData("info", MigrationCommand.Info)]
    [InlineData("baseline", MigrationCommand.Baseline)]
    [InlineData("fix", MigrationCommand.FixIssues)]
    public async Task LowercaseVerb_IsRecognized(string verb, MigrationCommand expected)
    {
        var args = verb == "migrate-down"
            ? new[] { verb, "-p", "P", "-env", "Dev", "-tr", "1.0" }
            : new[] { verb, "-p", "P", "-env", "Dev" };

        var (config, parse) = await ParseAsync(args);

        parse.Errors.Should().BeEmpty();
        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.Command.Should().Be(expected);
    }

    [Theory]
    [InlineData("Migrate-Up")]
    [InlineData("Migrate-Down")]
    [InlineData("Validate-Hash")]
    [InlineData("Update-Hash")]
    [InlineData("Info")]
    [InlineData("Baseline")]
    [InlineData("Fix")]
    public async Task PascalCaseVerb_IsRejected(string verb)
    {
        // No aliases for the old spelling are registered - the rename is a hard break.
        var (config, parse) = await ParseAsync(verb, "-p", "P", "-env", "Dev", "-tr", "1.0");

        parse.Errors.Should().NotBeEmpty();
        config.ParsedOptions.Should().BeNull();
    }

    [Fact]
    public void RootHelp_ListsAllVerbsInLowercase()
    {
        var config = new CommandLineConfiguration("RayMigrator Test");

        config.RootCommand.Subcommands.Select(c => c.Name).Should().BeEquivalentTo(
            "migrate-up", "migrate-down", "validate-hash", "update-hash", "info", "baseline", "fix");
        config.RootCommand.Subcommands.Should().AllSatisfy(c => c.Name.Should().Be(c.Name.ToLowerInvariant()));
    }

    [Theory]
    [InlineData("migrate-up", "--target-group-migration-order")]
    [InlineData("migrate-up", "-tgmo")]
    [InlineData("baseline", "--target-group-migration-order")]
    [InlineData("baseline", "-tgmo")]
    public async Task TargetGroupMigrationOrder_LongAndShortForm_AreResolvedByHandler(string verb, string option)
    {
        // The handlers look the option up by its literal name; a mismatch throws at invocation time,
        // not at parse time, so the assertion has to go through InvokeAsync.
        var (config, parse) = await ParseAsync(verb, "-p", "P", "-env", "Dev", option, "Frontend,Backend");

        parse.Errors.Should().BeEmpty();
        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.TargetGroupMigrationOrder.Should().Equal("Frontend", "Backend");
    }

    [Fact]
    public async Task TargetGroupMigrationOrder_OldPascalCaseName_IsRejected()
    {
        var (config, parse) = await ParseAsync("migrate-up", "-p", "P", "-env", "Dev", "--TargetGroup-MigrationOrder", "Frontend,Backend");

        parse.Errors.Should().NotBeEmpty();
        config.ParsedOptions.Should().BeNull();
    }

    [Theory]
    [InlineData("migrate", MigrationRunMode.Migrate)]
    [InlineData("Migrate", MigrationRunMode.Migrate)]
    [InlineData("MIGRATE", MigrationRunMode.Migrate)]
    [InlineData("simulate", MigrationRunMode.Simulate)]
    [InlineData("Simulate", MigrationRunMode.Simulate)]
    [InlineData("validate", MigrationRunMode.Validate)]
    [InlineData("Validate", MigrationRunMode.Validate)]
    public async Task RunMode_IsCaseInsensitive(string value, MigrationRunMode expected)
    {
        var (config, parse) = await ParseAsync("migrate-up", "-p", "P", "-env", "Dev", "--run-mode", value);

        parse.Errors.Should().BeEmpty();
        config.ParsedOptions!.RunMode.Should().Be(expected);
    }

    [Fact]
    public async Task RunMode_DefaultsToMigrate()
    {
        var (config, _) = await ParseAsync("migrate-up", "-p", "P", "-env", "Dev");

        config.ParsedOptions!.RunMode.Should().Be(MigrationRunMode.Migrate);
    }

    [Theory]
    [InlineData("file", HashValidationScope.File)]
    [InlineData("File", HashValidationScope.File)]
    [InlineData("sqlblock", HashValidationScope.SqlBlocks)]
    [InlineData("SqlBlock", HashValidationScope.SqlBlocks)]
    [InlineData("sqlblocks", HashValidationScope.SqlBlocks)]
    [InlineData("SqlBlocks", HashValidationScope.SqlBlocks)]
    [InlineData("disabled", HashValidationScope.Disabled)]
    [InlineData("Disabled", HashValidationScope.Disabled)]
    public async Task ValidateHashScope_IsCaseInsensitive(string value, HashValidationScope expected)
    {
        var (config, parse) = await ParseAsync("validate-hash", "-p", "P", "-env", "Dev", "--scope", value);

        parse.Errors.Should().BeEmpty();
        config.ParsedOptions!.HashValidationScope.Should().Be(expected);
    }

    [Theory]
    [InlineData("orphanedruns", FixIssues.OrphanedRuns)]
    [InlineData("OrphanedRuns", FixIssues.OrphanedRuns)]
    [InlineData("all", FixIssues.All)]
    [InlineData("All", FixIssues.All)]
    public async Task FixScope_IsCaseInsensitive(string value, FixIssues expected)
    {
        var (config, parse) = await ParseAsync("fix", "-p", "P", "-env", "Dev", "--scope", value);

        parse.Errors.Should().BeEmpty();
        config.ParsedOptions!.FixIssues.Should().Be(expected);
    }

    [Fact]
    public async Task RunMode_InvalidValue_ErrorListsLowercaseValues()
    {
        var (_, parse) = await ParseAsync("migrate-up", "-p", "P", "-env", "Dev", "--run-mode", "bogus");

        parse.Errors.Should().ContainSingle(e => e.Message.Contains("Valid values are: migrate, simulate, validate."));
    }
}
