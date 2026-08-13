using AwesomeAssertions;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.Sqlite;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for SQLite parameter escaping (G6).
/// Validates FormatParameterValue and SubstituteParameters for DalSqlite.
/// </summary>
public class DalSqliteParameterTests
{
    #region FormatParameterValue

    [Fact]
    public void Sqlite_FormatParameterValue_NullValue_ReturnsNullString()
    {
        var param = new DalParameter("test", null, typeof(string));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be("NULL");
    }

    [Fact]
    public void Sqlite_FormatParameterValue_DBNullValue_ReturnsNullString()
    {
        var param = new DalParameter("test", DBNull.Value, typeof(string));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be("NULL");
    }

    [Fact]
    public void Sqlite_FormatParameterValue_SimpleString_WrapsInSingleQuotes()
    {
        var param = new DalParameter("test", "hello", typeof(string));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be("'hello'");
    }

    [Fact]
    public void Sqlite_FormatParameterValue_StringWithSingleQuote_EscapesSingleQuote()
    {
        var param = new DalParameter("test", "it's a test", typeof(string));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be("'it''s a test'");
    }

    [Fact]
    public void Sqlite_FormatParameterValue_StringWithBackslash_NoEscaping()
    {
        // SQLite does NOT escape backslashes (unlike PostgreSQL and MariaDB/MySQL)
        var param = new DalParameter("test", @"path\to\file", typeof(string));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be(@"'path\to\file'");
    }

    [Fact]
    public void Sqlite_FormatParameterValue_BoolTrue_Returns1()
    {
        var param = new DalParameter("test", true, typeof(bool));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be("1");
    }

    [Fact]
    public void Sqlite_FormatParameterValue_BoolFalse_Returns0()
    {
        var param = new DalParameter("test", false, typeof(bool));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be("0");
    }

    [Fact]
    public void Sqlite_FormatParameterValue_IntValue_ReturnsNumber()
    {
        var param = new DalParameter("test", 42, typeof(int));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be("42");
    }

    [Fact]
    public void Sqlite_FormatParameterValue_EmptyString_WrapsInSingleQuotes()
    {
        var param = new DalParameter("test", "", typeof(string));

        var result = DalSqlite.FormatParameterValue(param);

        result.Should().Be("''");
    }

    #endregion

    #region SubstituteParameters

    [Fact]
    public void Sqlite_SubstituteParameters_ReplacesPlaceholders()
    {
        var sql = "SELECT * FROM t WHERE id = @Id AND name = @Name";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Id", 42, typeof(int)));
        paramList.AddParameter(new DalParameter("Name", "test", typeof(string)));

        var result = DalSqlite.SubstituteParameters(sql, paramList);

        result.Should().Be("SELECT * FROM t WHERE id = 42 AND name = 'test'");
    }

    [Fact]
    public void Sqlite_SubstituteParameters_LongerParamNameReplacedFirst()
    {
        var sql = "SELECT @ProductId, @Product";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("ProductId", 1, typeof(int)));
        paramList.AddParameter(new DalParameter("Product", "MyProduct", typeof(string)));

        var result = DalSqlite.SubstituteParameters(sql, paramList);

        result.Should().Be("SELECT 1, 'MyProduct'");
    }

    [Fact]
    public void Sqlite_SubstituteParameters_NullParameter_ReplacesWithNULL()
    {
        var sql = "INSERT INTO t (name) VALUES (@Name)";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Name", null, typeof(string)));

        var result = DalSqlite.SubstituteParameters(sql, paramList);

        result.Should().Be("INSERT INTO t (name) VALUES (NULL)");
    }

    [Fact]
    public void Sqlite_SubstituteParameters_BoolParameter_ReplacesWithNumeric()
    {
        var sql = "UPDATE t SET active = @Active";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Active", true, typeof(bool)));

        var result = DalSqlite.SubstituteParameters(sql, paramList);

        result.Should().Be("UPDATE t SET active = 1");
    }

    #endregion
}
