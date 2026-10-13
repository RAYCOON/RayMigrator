using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: A <see cref="MigrationContext"/> rejects the "not set" sentinels <see cref="MigrationCommand.None"/> and
/// <see cref="MigrationRunMode.Undefined"/>, and <see cref="MigrationContextFactory"/> stamps the command it is
/// asked for instead of hard-coding MigrateUp (#6).
/// </summary>
public class MigrationContextGuardTests
{
    [Fact]
    public void Constructor_WithRunModeUndefined_Throws()
    {
        var options = ProfileTestContext.CreateOptions("Data Source=:memory:");
        var consoleOptions = ProfileTestContext.CreateConsoleOptions(MigrationCommand.MigrateUp, MigrationRunMode.Undefined);

        var act = () => new MigrationContext(options, consoleOptions, "0.0.0-test");

        act.Should().Throw<ConfigurationValidationException>(
                "RunMode = Undefined would behave like Validate and stamp 0 into MigrationRun")
           .WithMessage("*RunMode is [Undefined]*");
    }

    [Fact]
    public void Constructor_WithCommandNone_Throws()
    {
        var options = ProfileTestContext.CreateOptions("Data Source=:memory:");
        var consoleOptions = ProfileTestContext.CreateConsoleOptions(MigrationCommand.None, MigrationRunMode.Migrate);

        var act = () => new MigrationContext(options, consoleOptions, "0.0.0-test");

        act.Should().Throw<ConfigurationValidationException>("Command = None has no command profile")
           .WithMessage("*Command is [None]*");
    }

    [Theory]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Validate)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Simulate)]
    [InlineData(MigrationCommand.Info, MigrationRunMode.Migrate)]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Migrate)]
    public void Constructor_WithValidCommandAndRunMode_Succeeds(MigrationCommand command, MigrationRunMode runMode)
    {
        var ctx = ProfileTestContext.CreateContext(command, runMode, "Data Source=:memory:");

        ctx.RayMigratorConsoleOptions.Command.Should().Be(command);
        ctx.RayMigratorConsoleOptions.RunMode.Should().Be(runMode);
        ctx.Clone.RayMigratorConsoleOptions.Command.Should().Be(command, "Clone rebuilds the context through the guarded constructor");
    }

    [Theory]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Migrate)]
    [InlineData(MigrationCommand.Info, MigrationRunMode.Migrate)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Simulate)]
    public void Factory_StampsTheRequestedCommand(MigrationCommand command, MigrationRunMode runMode)
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = ProfileTestContext.CreateOptions("Data Source=:memory:");

        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = command, Product = ProfileTestContext.ProductAlias, Environment = "Docker", RunMode = runMode,
            TargetReleaseVersion = "1.0", ShowStartupInfo = false, RevealSensitiveData = false
        };

        var ctx = CreateFactory().CreateFromValidatedOptions(options, consoleOptions);

        ctx.RayMigratorConsoleOptions.Command.Should().Be(command, "a programmatic MigrateDown must not be recorded as MigrateUp");
        ctx.RayMigratorConsoleOptions.RunMode.Should().Be(runMode);
        ctx.RayMigratorConsoleOptions.TargetReleaseVersion.Should().Be("1.0");
        ctx.RayMigratorConsoleOptions.Product.Should().Be(ProfileTestContext.ProductAlias);
    }

    [Fact]
    public void Factory_WithCommandNone_Throws()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = ProfileTestContext.CreateOptions("Data Source=:memory:");

        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.None, Product = ProfileTestContext.ProductAlias, Environment = "Docker",
            RunMode = MigrationRunMode.Migrate, ShowStartupInfo = false, RevealSensitiveData = false
        };

        var act = () => CreateFactory().CreateFromValidatedOptions(options, consoleOptions);

        act.Should().Throw<ConfigurationValidationException>();
    }

    private static MigrationContextFactory CreateFactory()
        => new(new SingletonMigrationContextAccessor(), NullLogger<MigrationContextFactory>.Instance);
}
