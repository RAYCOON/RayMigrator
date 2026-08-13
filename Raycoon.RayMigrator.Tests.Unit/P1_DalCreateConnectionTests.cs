using System.Data.Common;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Raycoon.RayMigrator.Database.Example;
using Raycoon.RayMigrator.Database.MariaDb;
using Raycoon.RayMigrator.Database.MySql;
using Raycoon.RayMigrator.Database.PostgreSQL;
using Raycoon.RayMigrator.Database.Sqlite;
using Raycoon.RayMigrator.Database.SqlServer;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for DAL CreateConnection method (G7 — new shared-connection members).
/// Verifies that each DAL's CreateConnection returns the correct concrete ADO.NET
/// connection type and that the returned connection is in an unopened/closed state.
/// Also verifies that DalExample stubs throw NotImplementedException.
/// Tests do not open connections — no database required.
/// </summary>
public class DalCreateConnectionTests
{
    #region SqlServer

    [Fact]
    public void SqlServer_CreateConnection_ReturnsSqlConnection()
    {
        var dal = new DalSqlServer("Data Source=.;Initial Catalog=Test;TrustServerCertificate=True");

        DbConnection connection = dal.CreateConnection();

        connection.Should().BeOfType<SqlConnection>();
    }

    [Fact]
    public void SqlServer_CreateConnection_ReturnsClosedConnection()
    {
        var dal = new DalSqlServer("Data Source=.;Initial Catalog=Test;TrustServerCertificate=True");

        using DbConnection connection = dal.CreateConnection();

        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void SqlServer_CreateConnection_IsNotNull()
    {
        var dal = new DalSqlServer("Data Source=.;Initial Catalog=Test;TrustServerCertificate=True");

        DbConnection connection = dal.CreateConnection();

        connection.Should().NotBeNull();
        connection.Dispose();
    }

    #endregion

    #region PostgreSQL

    [Fact]
    public void PostgreSql_CreateConnection_ReturnsNpgsqlConnection()
    {
        var dal = new DalPostgreSql("Host=localhost;Database=testdb;Username=user;Password=pass");

        DbConnection connection = dal.CreateConnection();

        connection.Should().BeOfType<NpgsqlConnection>();
    }

    [Fact]
    public void PostgreSql_CreateConnection_ReturnsClosedConnection()
    {
        var dal = new DalPostgreSql("Host=localhost;Database=testdb;Username=user;Password=pass");

        using DbConnection connection = dal.CreateConnection();

        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void PostgreSql_CreateConnection_IsNotNull()
    {
        var dal = new DalPostgreSql("Host=localhost;Database=testdb;Username=user;Password=pass");

        DbConnection connection = dal.CreateConnection();

        connection.Should().NotBeNull();
        connection.Dispose();
    }

    #endregion

    #region MariaDb

    [Fact]
    public void MariaDb_CreateConnection_ReturnsMySqlConnection()
    {
        // DalMariaDb uses MySqlConnector.MySqlConnection (same driver supports MariaDB)
        var dal = new DalMariaDb("Server=localhost;Database=testdb;User=user;Password=pass;");

        DbConnection connection = dal.CreateConnection();

        connection.Should().BeOfType<MySqlConnection>();
    }

    [Fact]
    public void MariaDb_CreateConnection_ReturnsClosedConnection()
    {
        var dal = new DalMariaDb("Server=localhost;Database=testdb;User=user;Password=pass;");

        using DbConnection connection = dal.CreateConnection();

        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void MariaDb_CreateConnection_IsNotNull()
    {
        var dal = new DalMariaDb("Server=localhost;Database=testdb;User=user;Password=pass;");

        DbConnection connection = dal.CreateConnection();

        connection.Should().NotBeNull();
        connection.Dispose();
    }

    #endregion

    #region MySql

    [Fact]
    public void MySql_CreateConnection_ReturnsMySqlConnection()
    {
        var dal = new DalMySql("Server=localhost;Database=testdb;User=user;Password=pass;");

        DbConnection connection = dal.CreateConnection();

        connection.Should().BeOfType<MySqlConnection>();
    }

    [Fact]
    public void MySql_CreateConnection_ReturnsClosedConnection()
    {
        var dal = new DalMySql("Server=localhost;Database=testdb;User=user;Password=pass;");

        using DbConnection connection = dal.CreateConnection();

        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void MySql_CreateConnection_IsNotNull()
    {
        var dal = new DalMySql("Server=localhost;Database=testdb;User=user;Password=pass;");

        DbConnection connection = dal.CreateConnection();

        connection.Should().NotBeNull();
        connection.Dispose();
    }

    #endregion

    #region SQLite

    [Fact]
    public void Sqlite_CreateConnection_ReturnsSqliteConnection()
    {
        var dal = new DalSqlite("Data Source=:memory:");

        DbConnection connection = dal.CreateConnection();

        connection.Should().BeOfType<SqliteConnection>();
    }

    [Fact]
    public void Sqlite_CreateConnection_ReturnsClosedConnection()
    {
        var dal = new DalSqlite("Data Source=:memory:");

        using DbConnection connection = dal.CreateConnection();

        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void Sqlite_CreateConnection_IsNotNull()
    {
        var dal = new DalSqlite("Data Source=:memory:");

        DbConnection connection = dal.CreateConnection();

        connection.Should().NotBeNull();
        connection.Dispose();
    }

    #endregion

    #region DalExample stubs — NotImplementedException

    [Fact]
    public void DalExample_CreateConnection_ThrowsNotImplementedException()
    {
        var dal = new DalExample("Server=example;Database=db");

        Action act = () => dal.CreateConnection();

        act.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public async Task DalExample_ExecuteNonQueryAsync_SharedConnection_ThrowsNotImplementedException()
    {
        var dal = new DalExample("Server=example;Database=db");

        // We only need to verify the method throws; connection/transaction can be null
        // because the method throws before using them.
        Func<Task> act = () => dal.ExecuteNonQueryAsync(
            "SELECT 1",
            connection: null!,
            transaction: null!,
            commandTimeoutInSeconds: 30);

        await act.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task DalExample_ExecuteScalarAsync_SharedConnection_ThrowsNotImplementedException()
    {
        var dal = new DalExample("Server=example;Database=db");

        Func<Task> act = () => dal.ExecuteScalarAsync(
            "SELECT 1",
            connection: null!,
            transaction: null!,
            commandTimeoutInSeconds: 30);

        await act.Should().ThrowAsync<NotImplementedException>();
    }

    #endregion
}
