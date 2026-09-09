using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The start-up connection checks follow the command profile, not the run mode (#6).
/// A read-only or repository-only command (info, validate-hash, update-hash, fix, baseline) must start even when a
/// target database is unreachable; migrate-up/migrate-down in Migrate or Simulate mode must still abort.
/// The target is a Sqlite file in a directory that does not exist, so opening it fails deterministically.
/// </summary>
public class ConnectionValidatorProfileTests
{
    [Theory]
    [InlineData(MigrationCommand.Info, MigrationRunMode.Migrate, false)]
    [InlineData(MigrationCommand.ValidateHash, MigrationRunMode.Migrate, false)]
    [InlineData(MigrationCommand.UpdateHash, MigrationRunMode.Migrate, false)]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Migrate, false)]
    [InlineData(MigrationCommand.Baseline, MigrationRunMode.Migrate, false)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Validate, false)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Validate, false)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Simulate, true)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, true)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Simulate, true)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, true)]
    public void ValidateTargetConnections_WithUnreachableTarget_ThrowsOnlyWhenProfileConnects(
        MigrationCommand command, MigrationRunMode runMode, bool expectThrow)
    {
        var ctx = ProfileTestContext.CreateContext(command, runMode, ProfileTestContext.UnreachableSqliteConnectionString());

        var act = () => ConnectionValidator.ValidateTargetConnections(ctx, NullLogger.Instance);

        if (expectThrow)
        {
            act.Should().Throw<ApplicationStartupException>(
                $"'{command}' in '{runMode}' mode touches the targets and must not start against an unreachable one")
               .WithMessage($"*Failed to validate or connect to DatabaseType [Sqlite]*[{ProfileTestContext.TargetGroupAlias}]*");
        }
        else
        {
            act.Should().NotThrow($"'{command}' in '{runMode}' mode never touches a target, so an unreachable target is irrelevant");
        }
    }

    [Fact]
    public void ValidateTargetConnections_WithReachableTarget_DoesNotThrowForMigrate()
    {
        // Control: the only reason the migrate rows above throw is the unreachable target.
        string reachable = $"Data Source={Path.Combine(Path.GetTempPath(), "RayMigrator_Reachable_" + Guid.NewGuid().ToString("N") + ".db")}";
        try
        {
            var ctx = ProfileTestContext.CreateContext(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, reachable);

            var act = () => ConnectionValidator.ValidateTargetConnections(ctx, NullLogger.Instance);

            act.Should().NotThrow();
        }
        finally
        {
            try { File.Delete(reachable["Data Source=".Length..]); } catch { /* best effort */ }
        }
    }

    [Theory]
    [InlineData(MigrationCommand.Info, MigrationRunMode.Migrate, false)]
    [InlineData(MigrationCommand.ValidateHash, MigrationRunMode.Migrate, false)]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Simulate, false)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Simulate, false)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Validate, false)]
    [InlineData(MigrationCommand.UpdateHash, MigrationRunMode.Migrate, true)]
    [InlineData(MigrationCommand.Baseline, MigrationRunMode.Migrate, true)]
    [InlineData(MigrationCommand.FixIssues, MigrationRunMode.Migrate, true)]
    [InlineData(MigrationCommand.MigrateUp, MigrationRunMode.Migrate, true)]
    [InlineData(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, true)]
    public void ValidateDatabaseLoggerConnection_WithUnreachableLogDatabase_ThrowsOnlyWhenProfileLogs(
        MigrationCommand command, MigrationRunMode runMode, bool expectThrow)
    {
        string unreachable = ProfileTestContext.UnreachableSqliteConnectionString();
        var options = ProfileTestContext.CreateOptions("Data Source=:memory:", databaseLoggingConnectionString: unreachable);
        var consoleOptions = ProfileTestContext.CreateConsoleOptions(command, runMode);
        DalFactory.TryGetDal("Sqlite", unreachable, out var dal).Should().BeTrue("the Sqlite DAL is referenced by the unit test project");

        var act = () => ConnectionValidator.ValidateDatabaseLoggerConnection(options, dal!, consoleOptions);

        if (expectThrow)
        {
            act.Should().Throw<ApplicationStartupException>(
                $"'{command}' writes database-log rows and must fail early when the log database is unreachable")
               .WithMessage("*DatabaseLogging*");
        }
        else
        {
            act.Should().NotThrow($"'{command}' in '{runMode}' mode writes no database-log rows");
        }
    }
}
