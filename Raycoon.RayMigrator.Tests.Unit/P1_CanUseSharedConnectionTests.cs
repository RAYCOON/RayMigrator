using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for MigrationService.CanUseSharedConnection static guard function.
/// CanUseSharedConnection determines whether a migration file can use the atomic
/// shared-connection path where target SQL blocks and repository status updates
/// execute within a single transaction. All five guard conditions must be satisfied.
/// </summary>
public class CanUseSharedConnectionTests
{
    private const string SharedConnectionString = "Server=myserver;Database=mydb;User=sa;Password=pass";

    private static MigrationFileInfo CreateFile(bool useTransaction = true)
    {
        return new MigrationFileInfo
        {
            Filename = "10_Migration.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            UseTransaction = useTransaction
        };
    }

    private static TargetOptions CreateTarget(
        string connectionString = SharedConnectionString,
        int? maxRetries = 0)
    {
        return new TargetOptions
        {
            Alias = "MainDB",
            ConnectionString = connectionString,
            DbCommandMaxRetries = maxRetries
        };
    }

    private static RepositoryOptions CreateRepository(
        string databaseType = "SqlServer",
        string connectionString = SharedConnectionString)
    {
        return new RepositoryOptions
        {
            DatabaseType = databaseType,
            ConnectionString = connectionString
        };
    }

    #region All conditions satisfied

    [Fact]
    public void AllConditionsMet_ReturnsTrue()
    {
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 0);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeTrue();
    }

    #endregion

    #region UseTransaction guard

    [Fact]
    public void UseTransaction_False_ReturnsFalse()
    {
        var file = CreateFile(useTransaction: false);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 0);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeFalse();
    }

    #endregion

    #region MaxRetries does NOT affect guard (retries handled at file level in atomic path)

    [Fact]
    public void MaxRetries_GreaterThanZero_StillReturnsTrue()
    {
        // MaxRetries > 0 is compatible with shared connection: retries operate at file level
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 1);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void MaxRetries_Null_ReturnsTrue()
    {
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: null);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void MaxRetries_LargeValue_StillReturnsTrue()
    {
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 100);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeTrue();
    }

    #endregion

    #region IgnoreBlockErrors guard

    [Fact]
    public void IgnoreBlockErrors_True_ReturnsFalse()
    {
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 0);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: true);

        result.Should().BeFalse();
    }

    #endregion

    #region DatabaseType guard

    [Fact]
    public void DatabaseType_DifferentRepositoryVsTargetGroup_ReturnsFalse()
    {
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 0);
        var repository = CreateRepository(databaseType: "PostgreSQL", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void DatabaseType_SameButDifferentCasing_ReturnsTrue()
    {
        // DatabaseType comparison is OrdinalIgnoreCase per the implementation
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 0);
        var repository = CreateRepository(databaseType: "sqlserver", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void DatabaseType_AllUpperCase_ReturnsTrue()
    {
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 0);
        var repository = CreateRepository(databaseType: "SQLSERVER", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeTrue();
    }

    #endregion

    #region ConnectionString guard

    [Fact]
    public void ConnectionString_DifferentTargetVsRepository_ReturnsFalse()
    {
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: "Server=target;Database=mydb;", maxRetries: 0);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: "Server=repo;Database=mydb;");

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void ConnectionString_SameValueButDifferentCasing_ReturnsFalse()
    {
        // ConnectionString comparison is Ordinal (case-sensitive) per the implementation
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: "Server=MyServer;Database=MyDb;", maxRetries: 0);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: "Server=myserver;Database=mydb;");

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void ConnectionString_ExactMatch_ReturnsTrue()
    {
        const string cs = "Server=prod01;Database=AppDb;User=svc;Password=secret";
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: cs, maxRetries: 0);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: cs);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeTrue();
    }

    #endregion

    #region Multiple conditions false simultaneously

    [Fact]
    public void AllConditionsFalse_ReturnsFalse()
    {
        var file = CreateFile(useTransaction: false);
        var target = CreateTarget(connectionString: "Server=target;", maxRetries: 3);
        var repository = CreateRepository(databaseType: "PostgreSQL", connectionString: "Server=repo;");

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void UseTransactionFalseAndIgnoreBlockErrorsTrue_ReturnsFalse()
    {
        var file = CreateFile(useTransaction: false);
        var target = CreateTarget(connectionString: SharedConnectionString, maxRetries: 0);
        var repository = CreateRepository(databaseType: "SqlServer", connectionString: SharedConnectionString);

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void DifferentDatabaseTypeAndDifferentConnectionString_ReturnsFalse()
    {
        var file = CreateFile(useTransaction: true);
        var target = CreateTarget(connectionString: "Server=target;", maxRetries: 0);
        var repository = CreateRepository(databaseType: "PostgreSQL", connectionString: "Host=repo;");

        var result = MigrationService.CanUseSharedConnection(
            file, target, repository, targetGroupDatabaseType: "SqlServer", ignoreBlockErrors: false);

        result.Should().BeFalse();
    }

    #endregion
}
