using FluentAssertions;
using MySqlConnector;
using Raycoon.RayMigrator.Database.MariaDb;
using Raycoon.RayMigrator.Database.MySql;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Tests for EnsureConnectionStringOptions in MariaDb and MySql DALs (G10).
/// Validates that AllowUserVariables=true is injected into connection strings.
/// Uses MySqlConnectionStringBuilder to parse results since the builder normalizes key names.
/// </summary>
public class EnsureConnectionStringOptionsTests
{
    #region MariaDb

    [Fact]
    public void MariaDb_EnsureConnectionStringOptions_AddsAllowUserVariables()
    {
        var input = "Server=localhost;Database=test;User=root;Password=pass";

        var result = DalMariaDb.EnsureConnectionStringOptions(input);

        var builder = new MySqlConnectionStringBuilder(result);
        builder.AllowUserVariables.Should().BeTrue("AllowUserVariables must be injected");
    }

    [Fact]
    public void MariaDb_EnsureConnectionStringOptions_PreservesExistingOptions()
    {
        var input = "Server=myserver;Database=mydb;User=myuser;Password=mypass;Port=3307";

        var result = DalMariaDb.EnsureConnectionStringOptions(input);

        var builder = new MySqlConnectionStringBuilder(result);
        builder.Server.Should().Be("myserver");
        builder.Database.Should().Be("mydb");
        builder.UserID.Should().Be("myuser");
        builder.Port.Should().Be(3307);
        builder.AllowUserVariables.Should().BeTrue();
    }

    [Fact]
    public void MariaDb_EnsureConnectionStringOptions_AlreadyTrue_StaysTrue()
    {
        var input = "Server=localhost;AllowUserVariables=True";

        var result = DalMariaDb.EnsureConnectionStringOptions(input);

        var builder = new MySqlConnectionStringBuilder(result);
        builder.AllowUserVariables.Should().BeTrue();
    }

    [Fact]
    public void MariaDb_EnsureConnectionStringOptions_FalseOverriddenToTrue()
    {
        var input = "Server=localhost;AllowUserVariables=False";

        var result = DalMariaDb.EnsureConnectionStringOptions(input);

        var builder = new MySqlConnectionStringBuilder(result);
        builder.AllowUserVariables.Should().BeTrue();
    }

    #endregion

    #region MySql

    [Fact]
    public void MySql_EnsureConnectionStringOptions_AddsAllowUserVariables()
    {
        var input = "Server=localhost;Database=test;User=root;Password=pass";

        var result = DalMySql.EnsureConnectionStringOptions(input);

        var builder = new MySqlConnectionStringBuilder(result);
        builder.AllowUserVariables.Should().BeTrue("AllowUserVariables must be injected");
    }

    [Fact]
    public void MySql_EnsureConnectionStringOptions_PreservesExistingOptions()
    {
        var input = "Server=myserver;Database=mydb;User=myuser;Password=mypass;Port=3307";

        var result = DalMySql.EnsureConnectionStringOptions(input);

        var builder = new MySqlConnectionStringBuilder(result);
        builder.Server.Should().Be("myserver");
        builder.Database.Should().Be("mydb");
        builder.UserID.Should().Be("myuser");
        builder.Port.Should().Be(3307);
        builder.AllowUserVariables.Should().BeTrue();
    }

    [Fact]
    public void MySql_EnsureConnectionStringOptions_AlreadyTrue_StaysTrue()
    {
        var input = "Server=localhost;AllowUserVariables=True";

        var result = DalMySql.EnsureConnectionStringOptions(input);

        var builder = new MySqlConnectionStringBuilder(result);
        builder.AllowUserVariables.Should().BeTrue();
    }

    [Fact]
    public void MySql_EnsureConnectionStringOptions_FalseOverriddenToTrue()
    {
        var input = "Server=localhost;AllowUserVariables=False";

        var result = DalMySql.EnsureConnectionStringOptions(input);

        var builder = new MySqlConnectionStringBuilder(result);
        builder.AllowUserVariables.Should().BeTrue();
    }

    #endregion
}
