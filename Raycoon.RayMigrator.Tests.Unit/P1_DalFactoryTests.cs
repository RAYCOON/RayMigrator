using System.Reflection;
using AwesomeAssertions;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for DalFactory discovery, caching, and error handling (G1).
/// DalFactory's static constructor runs once; tests verify post-init state.
/// </summary>
public class DalFactoryTests
{
    [Fact]
    public void TryGetDal_SqlServer_ReturnsTrue()
    {
        var result = DalFactory.TryGetDal("SqlServer", "Server=test_sqlserver;TrustServerCertificate=True", out var dal);

        result.Should().BeTrue();
        dal.Should().NotBeNull();
    }

    [Fact]
    public void TryGetDal_PostgreSQL_ReturnsTrue()
    {
        var result = DalFactory.TryGetDal("PostgreSQL", "Host=test_postgresql", out var dal);

        result.Should().BeTrue();
        dal.Should().NotBeNull();
    }

    [Fact]
    public void TryGetDal_MariaDb_ReturnsTrue()
    {
        var result = DalFactory.TryGetDal("MariaDb", "Server=test_mariadb", out var dal);

        result.Should().BeTrue();
        dal.Should().NotBeNull();
    }

    [Fact]
    public void TryGetDal_MySql_ReturnsTrue()
    {
        var result = DalFactory.TryGetDal("MySql", "Server=test_mysql", out var dal);

        result.Should().BeTrue();
        dal.Should().NotBeNull();
    }

    [Fact]
    public void TryGetDal_Sqlite_ReturnsTrue()
    {
        var result = DalFactory.TryGetDal("Sqlite", "Data Source=test_sqlite.db", out var dal);

        result.Should().BeTrue();
        dal.Should().NotBeNull();
    }

    [Fact]
    public void TryGetDal_UnknownType_ThrowsConfigurationValidationException()
    {
        var act = () => DalFactory.TryGetDal("Oracle", "conn=test", out var dal);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Unknown DataAccessLayer*Oracle*");
    }

    [Fact]
    public void TryGetDal_SameTypeAndConnection_ReturnsCachedInstance()
    {
        string connStr = "Server=test_cache_same_" + Guid.NewGuid().ToString("N");
        DalFactory.TryGetDal("SqlServer", connStr, out var dal1);
        DalFactory.TryGetDal("SqlServer", connStr, out var dal2);

        dal1.Should().NotBeNull();
        ReferenceEquals(dal1, dal2).Should().BeTrue();
    }

    [Fact]
    public void TryGetDal_SameTypeDifferentConnection_ReturnsDifferentInstances()
    {
        var suffix = Guid.NewGuid().ToString("N");
        DalFactory.TryGetDal("SqlServer", $"Server=test_diff_a_{suffix};TrustServerCertificate=True", out var dal1);
        DalFactory.TryGetDal("SqlServer", $"Server=test_diff_b_{suffix};TrustServerCertificate=True", out var dal2);

        dal1.Should().NotBeNull();
        dal2.Should().NotBeNull();
        ReferenceEquals(dal1, dal2).Should().BeFalse();
    }

    [Fact]
    public void TryGetDal_ReturnedInstance_HasCorrectDatabaseType()
    {
        DalFactory.TryGetDal("SqlServer", "Server=test_dbtype_" + Guid.NewGuid().ToString("N") + ";TrustServerCertificate=True", out var dal);

        dal.Should().NotBeNull();
        dal!.DatabaseType.Should().Be("SqlServer");
    }

    [Fact]
    public void RegisteredDalTypes_ContainsAllFiveBuiltInTypes()
    {
        var registered = DalFactory.RegisteredDalTypes;

        registered.Should().ContainKey("SqlServer");
        registered.Should().ContainKey("PostgreSQL");
        registered.Should().ContainKey("MariaDb");
        registered.Should().ContainKey("MySql");
        registered.Should().ContainKey("Sqlite");
    }

    [Fact]
    public void ScanAssemblyForDals_AssemblyWithoutDals_DoesNotThrow()
    {
        // mscorlib / System.Private.CoreLib has no IDal implementations
        var act = () => DalFactory.ScanAssemblyForDals(typeof(object).Assembly);

        act.Should().NotThrow();
    }

    [Fact]
    public void ScanAssemblyForDals_DalAssembly_FindsType()
    {
        // Database.SqlServer assembly contains DalSqlServer
        var assembly = typeof(Raycoon.RayMigrator.Database.SqlServer.DalSqlServer).Assembly;

        // This should not throw and should find the type (already registered by static ctor)
        var act = () => DalFactory.ScanAssemblyForDals(assembly);

        act.Should().NotThrow();
        DalFactory.RegisteredDalTypes.Should().ContainKey("SqlServer");
    }
}
