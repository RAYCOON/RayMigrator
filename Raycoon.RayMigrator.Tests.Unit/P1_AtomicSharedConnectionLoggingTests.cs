using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for the once-per-run Information line that names the targets on which the atomic shared-connection
/// path applies (#27), for the target-level guard TargetSharesRepositoryConnection behind it, and for the
/// InvolvedTargets helper that limits the line to the targets a run touches.
/// </summary>
public class AtomicSharedConnectionLoggingTests
{
    private const string SharedConnectionString = "Server=myserver;Database=mydb;User=sa;Password=pass";
    private const string OtherConnectionString = "Server=other;Database=other;User=sa;Password=pass";

    private static RepositoryOptions CreateRepository(string databaseType = "SqlServer", string connectionString = SharedConnectionString) =>
        new() { DatabaseType = databaseType, ConnectionString = connectionString };

    private static ProductOptions CreateProduct(string databaseType, params (string Alias, string ConnectionString)[] targets) =>
        new()
        {
            Alias = "BookStore",
            TargetGroups =
            [
                new TargetGroupOptions
                {
                    Alias = "Backend",
                    DatabaseType = databaseType,
                    Targets = targets.Select(t => new TargetOptions { Alias = t.Alias, ConnectionString = t.ConnectionString }).ToList()
                }
            ]
        };

    private static List<LogEntry> InformationLines(CapturingLogger<MigrationService> logger) =>
        logger.Entries.Where(e => e.LogLevel == LogLevel.Information).ToList();

    [Fact]
    public void LogAtomicSharedConnectionTargets_InvolvedTargetSharesRepositoryConnection_LogsOneLineNamingTheTarget()
    {
        // Arrange
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        var product = CreateProduct("SqlServer", ("MainDB", SharedConnectionString));

        // Act
        service.LogAtomicSharedConnectionTargets([("Backend", "MainDB")], product, CreateRepository());

        // Assert
        var lines = InformationLines(logger);
        lines.Should().ContainSingle(because: "one Information line per sharing target is logged (#27)");
        lines[0].Message.Should().Contain("[MainDB]").And.Contain("[Backend]").And.Contain("atomic shared connection path");
    }

    [Fact]
    public void LogAtomicSharedConnectionTargets_NoInvolvedTargets_LogsNothing()
    {
        // Arrange
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        var product = CreateProduct("SqlServer", ("MainDB", SharedConnectionString));

        // Act
        service.LogAtomicSharedConnectionTargets([], product, CreateRepository());

        // Assert
        InformationLines(logger).Should().BeEmpty(because: "targets the run does not touch are not named");
    }

    [Fact]
    public void LogAtomicSharedConnectionTargets_OnlyOneOfTwoSharingTargetsInvolved_NamesOnlyThatTarget()
    {
        // Arrange
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        var product = CreateProduct("SqlServer", ("MainDB", SharedConnectionString), ("ReportingDB", SharedConnectionString));

        // Act
        service.LogAtomicSharedConnectionTargets([("Backend", "ReportingDB")], product, CreateRepository());

        // Assert
        InformationLines(logger).Should().ContainSingle()
            .Which.Message.Should().Contain("[ReportingDB]").And.NotContain("[MainDB]");
    }

    [Fact]
    public void LogAtomicSharedConnectionTargets_TargetWithOtherConnectionString_LogsNothing()
    {
        // Arrange
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        var product = CreateProduct("SqlServer", ("MainDB", OtherConnectionString));

        // Act
        service.LogAtomicSharedConnectionTargets([("Backend", "MainDB")], product, CreateRepository());

        // Assert
        InformationLines(logger).Should().BeEmpty();
    }

    [Fact]
    public void LogAtomicSharedConnectionTargets_DifferentDatabaseType_LogsNothing()
    {
        // Arrange
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        var product = CreateProduct("PostgreSQL", ("MainDB", SharedConnectionString));

        // Act
        service.LogAtomicSharedConnectionTargets([("Backend", "MainDB")], product, CreateRepository(databaseType: "SqlServer"));

        // Assert
        InformationLines(logger).Should().BeEmpty(because: "the atomic path requires the same DatabaseType");
    }

    [Fact]
    public void LogAtomicSharedConnectionTargets_TwoSharingTargets_LogsTwoLines()
    {
        // Arrange
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        var product = CreateProduct("SqlServer", ("MainDB", SharedConnectionString), ("ReportingDB", SharedConnectionString));

        // Act
        service.LogAtomicSharedConnectionTargets([("Backend", "MainDB"), ("Backend", "ReportingDB")], product, CreateRepository());

        // Assert
        InformationLines(logger).Should().HaveCount(2);
    }

    [Fact]
    public void LogAtomicSharedConnectionTargets_SameTargetTwiceInDifferentCase_LogsOnce()
    {
        // Arrange
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        var product = CreateProduct("SqlServer", ("MainDB", SharedConnectionString));

        // Act
        service.LogAtomicSharedConnectionTargets([("Backend", "MainDB"), ("backend", "maindb")], product, CreateRepository());

        // Assert
        InformationLines(logger).Should().ContainSingle(because: "the involved targets are made distinct case-insensitively");
    }

    [Theory]
    [InlineData("SqlServer", SharedConnectionString, true)]
    [InlineData("sqlserver", SharedConnectionString, true)]
    [InlineData("PostgreSQL", SharedConnectionString, false)]
    [InlineData("SqlServer", OtherConnectionString, false)]
    public void TargetSharesRepositoryConnection_ComparesDatabaseTypeIgnoringCaseAndConnectionStringOrdinally(
        string targetGroupDatabaseType, string targetConnectionString, bool expected)
    {
        // Arrange
        var target = new TargetOptions { Alias = "MainDB", ConnectionString = targetConnectionString };

        // Act
        bool result = MigrationService.TargetSharesRepositoryConnection(target, CreateRepository(), targetGroupDatabaseType);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void TargetSharesRepositoryConnection_ConnectionStringDiffersOnlyInCase_ReturnsFalse()
    {
        // Arrange
        var target = new TargetOptions { Alias = "MainDB", ConnectionString = SharedConnectionString.ToUpperInvariant() };

        // Act
        bool result = MigrationService.TargetSharesRepositoryConnection(target, CreateRepository(), "SqlServer");

        // Assert
        result.Should().BeFalse(because: "CanUseSharedConnection compares the connection string ordinally");
    }

    [Fact]
    public void InvolvedTargets_FilePendingOnOneTarget_ReturnsOnlyThatTarget()
    {
        // Arrange
        var product = CreateProduct("SqlServer", ("MainDB", SharedConnectionString), ("ReportingDB", SharedConnectionString));
        var file = TestFactories.CreateMigrationFile(filename: "10_Create.sql", release: "Release 1.0", targetGroup: "Backend");
        file.PendingTargetAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ReportingDB" };

        // Act
        var involved = MigrationService.InvolvedTargets([file], product).ToList();

        // Assert
        involved.Should().Equal(("Backend", "ReportingDB"));
    }

    [Fact]
    public void InvolvedTargets_FilePendingOnAllTargets_ReturnsEveryTargetOfItsGroup()
    {
        // Arrange
        var product = CreateProduct("SqlServer", ("MainDB", SharedConnectionString), ("ReportingDB", SharedConnectionString));
        var file = TestFactories.CreateMigrationFile(filename: "10_Create.sql", release: "Release 1.0", targetGroup: "Backend");
        file.PendingTargetAliases = null;

        // Act
        var involved = MigrationService.InvolvedTargets([file], product).ToList();

        // Assert
        involved.Should().Equal(("Backend", "MainDB"), ("Backend", "ReportingDB"));
    }

    [Fact]
    public void InvolvedTargets_FileOfAnotherTargetGroup_ReturnsNothing()
    {
        // Arrange
        var product = CreateProduct("SqlServer", ("MainDB", SharedConnectionString));
        var file = TestFactories.CreateMigrationFile(filename: "10_Create.sql", release: "Release 1.0", targetGroup: "Frontend");

        // Act
        var involved = MigrationService.InvolvedTargets([file], product).ToList();

        // Assert
        involved.Should().BeEmpty();
    }
}
