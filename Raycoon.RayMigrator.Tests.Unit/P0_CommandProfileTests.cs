using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;
using Raycoon.RayMigrator.Core.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// Pins the command x side-effect matrix of <see cref="MigrationCommandExtensions.GetProfile(MigrationCommand, MigrationRunMode, bool)"/> (#6).
/// The rows are the documented table in execution-modes.md; a change to any cell is a conscious product decision.
/// </summary>
public class CommandProfileTests
{
    [Theory]
    //          command                     run mode                   dry  conn   write  dblog
    [InlineData(MigrationCommand.MigrateUp,    MigrationRunMode.Migrate,  true, true, true)]
    [InlineData(MigrationCommand.MigrateUp,    MigrationRunMode.Simulate, true, false, false)]
    [InlineData(MigrationCommand.MigrateUp,    MigrationRunMode.Validate, false, false, false)]
    [InlineData(MigrationCommand.MigrateDown,  MigrationRunMode.Migrate,  true, true, true)]
    [InlineData(MigrationCommand.MigrateDown,  MigrationRunMode.Simulate, true, false, false)]
    [InlineData(MigrationCommand.MigrateDown,  MigrationRunMode.Validate, false, false, false)]
    [InlineData(MigrationCommand.Baseline,     MigrationRunMode.Migrate,  false, true, true)]
    [InlineData(MigrationCommand.UpdateHash,   MigrationRunMode.Migrate,  false, true, true)]
    [InlineData(MigrationCommand.FixIssues,    MigrationRunMode.Migrate,  false, true, true)]
    [InlineData(MigrationCommand.FixIssues,    MigrationRunMode.Simulate, false, false, false)]
    [InlineData(MigrationCommand.Info,         MigrationRunMode.Migrate,  false, false, false)]
    [InlineData(MigrationCommand.ValidateHash, MigrationRunMode.Migrate,  false, false, false)]
    public void GetProfile_MatchesSideEffectMatrix(
        MigrationCommand command, MigrationRunMode runMode,
        bool connectsToTargets, bool writesRepository, bool writesDatabaseLog)
    {
        var profile = MigrationCommandExtensions.GetProfile(command, runMode);

        profile.Should().Be(new CommandProfile(connectsToTargets, writesRepository, writesDatabaseLog),
            $"the side effects of '{command}' in '{runMode}' mode are part of the product contract");
    }

    [Theory]
    [InlineData(MigrationCommand.Info)]
    [InlineData(MigrationCommand.ValidateHash)]
    [InlineData(MigrationCommand.UpdateHash)]
    [InlineData(MigrationCommand.Baseline)]
    public void NonMigrateCommand_IgnoresRunMode(MigrationCommand command)
    {
        // --run-mode is a migrate-up/migrate-down/fix concept. Whatever run mode another command carries,
        // its profile stays the same, so no hidden run mode can steer its side effects any more.
        var migrate = MigrationCommandExtensions.GetProfile(command, MigrationRunMode.Migrate);
        var simulate = MigrationCommandExtensions.GetProfile(command, MigrationRunMode.Simulate);
        var validate = MigrationCommandExtensions.GetProfile(command, MigrationRunMode.Validate);

        simulate.Should().Be(migrate);
        validate.Should().Be(migrate);
    }

    [Fact]
    public void EveryCommandExceptNone_HasAProfile()
    {
        // A new MigrationCommand value without a profile must fail here, not in production.
        foreach (var command in Enum.GetValues<MigrationCommand>().Where(c => c != MigrationCommand.None))
        {
            var act = () => MigrationCommandExtensions.GetProfile(command, MigrationRunMode.Migrate);
            act.Should().NotThrow($"MigrationCommand.{command} needs a row in MigrationCommandExtensions.GetProfile");
        }
    }

    [Fact]
    public void GetProfile_ForNone_Throws()
    {
        var act = () => MigrationCommandExtensions.GetProfile(MigrationCommand.None, MigrationRunMode.Migrate);

        act.Should().Throw<ConfigurationValidationException>().WithMessage("*No command profile*None*");
    }

    [Theory]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Undefined)]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Validate)]
    public void GetProfile_ForFixWithUnsupportedRunMode_Throws(MigrationCommand command, MigrationRunMode runMode)
    {
        // fix repairs (Migrate) or lists (Simulate); Validate has no meaning for it and Undefined is the sentinel (#22).
        var act = () => MigrationCommandExtensions.GetProfile(command, runMode);
        act.Should().Throw<ConfigurationValidationException>().WithMessage("*Choose migrate or simulate*");
    }

    [Theory]
    [InlineData(MigrationCommand.MigrateUp)]
    [InlineData(MigrationCommand.MigrateDown)]
    public void GetProfile_ForMigrateCommandWithUndefinedRunMode_Throws(MigrationCommand command)
    {
        // Undefined is the "not set" sentinel. Deriving a profile from it would silently yield a validate-like
        // profile (no connect, no read, no write) for a command that the caller meant to run for real.
        var act = () => MigrationCommandExtensions.GetProfile(command, MigrationRunMode.Undefined);

        act.Should().Throw<ConfigurationValidationException>().WithMessage("*Undefined*");
    }

    [Fact]
    public void GetProfile_FromConsoleOptions_UsesCommandAndRunMode()
    {
        var fix = ProfileTestContext.CreateConsoleOptions(MigrationCommand.FixIssues, MigrationRunMode.Simulate);
        var simulate = ProfileTestContext.CreateConsoleOptions(MigrationCommand.MigrateUp, MigrationRunMode.Simulate);
        var info = ProfileTestContext.CreateConsoleOptions(MigrationCommand.Info, MigrationRunMode.Migrate);

        fix.GetProfile().Should().Be(MigrationCommandExtensions.GetProfile(MigrationCommand.FixIssues, MigrationRunMode.Simulate));
        simulate.GetProfile().Should().Be(MigrationCommandExtensions.GetProfile(MigrationCommand.MigrateUp, MigrationRunMode.Simulate));
        info.GetProfile().Should().Be(MigrationCommandExtensions.GetProfile(MigrationCommand.Info, MigrationRunMode.Migrate));
    }

    [Fact]
    public void MigrateProfiles_ReproduceMigrationRunModeExtensions()
    {
        // The migrate commands' cells are built from the existing run-mode primitives; they must never drift apart.
        foreach (var runMode in new[] { MigrationRunMode.Validate, MigrationRunMode.Simulate, MigrationRunMode.Migrate })
        {
            var profile = MigrationCommandExtensions.GetProfile(MigrationCommand.MigrateUp, runMode);

            profile.ConnectsToTargets.Should().Be(runMode.ShouldConnectToTargets());
            profile.WritesRepository.Should().Be(runMode.ShouldWriteRepository());
            profile.WritesDatabaseLog.Should().Be(runMode.ShouldWriteRepository());
        }
    }
}
