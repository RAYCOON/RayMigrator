using AwesomeAssertions;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.PostgreSQL;
using Raycoon.RayMigrator.Database.MariaDb;
using Raycoon.RayMigrator.Database.MySql;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for DAL parameter value escaping (O3).
/// Validates that FormatParameterValue correctly escapes special characters
/// across PostgreSQL, MariaDB, and MySQL DALs.
/// </summary>
public class DalParameterEscapingTests
{
    #region PostgreSQL FormatParameterValue

    [Fact]
    public void PostgreSql_FormatParameterValue_NullValue_ReturnsNullString()
    {
        var param = new DalParameter("test", null, typeof(string));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be("NULL");
    }

    [Fact]
    public void PostgreSql_FormatParameterValue_SimpleString_WrapsInSingleQuotes()
    {
        var param = new DalParameter("test", "hello", typeof(string));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be("'hello'");
    }

    [Fact]
    public void PostgreSql_FormatParameterValue_StringWithSingleQuote_EscapesSingleQuote()
    {
        var param = new DalParameter("test", "it's a test", typeof(string));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be("'it''s a test'");
    }

    [Fact]
    public void PostgreSql_FormatParameterValue_StringWithBackslash_EscapesBackslash()
    {
        var param = new DalParameter("test", @"path\to\file", typeof(string));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be(@"'path\\to\\file'");
    }

    [Fact]
    public void PostgreSql_FormatParameterValue_StringWithBothQuoteAndBackslash_EscapesBoth()
    {
        var param = new DalParameter("test", @"it's a\path", typeof(string));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be(@"'it''s a\\path'");
    }

    [Fact]
    public void PostgreSql_FormatParameterValue_BoolTrue_ReturnsTRUE()
    {
        var param = new DalParameter("test", true, typeof(bool));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be("TRUE");
    }

    [Fact]
    public void PostgreSql_FormatParameterValue_BoolFalse_ReturnsFALSE()
    {
        var param = new DalParameter("test", false, typeof(bool));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be("FALSE");
    }

    [Fact]
    public void PostgreSql_FormatParameterValue_IntValue_ReturnsNumber()
    {
        var param = new DalParameter("test", 42, typeof(int));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be("42");
    }

    #endregion

    #region MariaDb FormatParameterValue

    [Fact]
    public void MariaDb_FormatParameterValue_NullValue_ReturnsNullString()
    {
        var param = new DalParameter("test", null, typeof(string));

        var result = DalMariaDb.FormatParameterValue(param);

        result.Should().Be("NULL");
    }

    [Fact]
    public void MariaDb_FormatParameterValue_StringWithSingleQuote_EscapesSingleQuote()
    {
        var param = new DalParameter("test", "it's a test", typeof(string));

        var result = DalMariaDb.FormatParameterValue(param);

        result.Should().Be("'it''s a test'");
    }

    [Fact]
    public void MariaDb_FormatParameterValue_StringWithBackslash_EscapesBackslash()
    {
        var param = new DalParameter("test", @"path\to\file", typeof(string));

        var result = DalMariaDb.FormatParameterValue(param);

        result.Should().Be(@"'path\\to\\file'");
    }

    [Fact]
    public void MariaDb_FormatParameterValue_BoolTrue_Returns1()
    {
        var param = new DalParameter("test", true, typeof(bool));

        var result = DalMariaDb.FormatParameterValue(param);

        result.Should().Be("1");
    }

    [Fact]
    public void MariaDb_FormatParameterValue_BoolFalse_Returns0()
    {
        var param = new DalParameter("test", false, typeof(bool));

        var result = DalMariaDb.FormatParameterValue(param);

        result.Should().Be("0");
    }

    #endregion

    #region MySQL FormatParameterValue

    [Fact]
    public void MySql_FormatParameterValue_NullValue_ReturnsNullString()
    {
        var param = new DalParameter("test", null, typeof(string));

        var result = DalMySql.FormatParameterValue(param);

        result.Should().Be("NULL");
    }

    [Fact]
    public void MySql_FormatParameterValue_StringWithSingleQuote_EscapesSingleQuote()
    {
        var param = new DalParameter("test", "it's a test", typeof(string));

        var result = DalMySql.FormatParameterValue(param);

        result.Should().Be("'it''s a test'");
    }

    [Fact]
    public void MySql_FormatParameterValue_StringWithBackslash_EscapesBackslash()
    {
        var param = new DalParameter("test", @"path\to\file", typeof(string));

        var result = DalMySql.FormatParameterValue(param);

        result.Should().Be(@"'path\\to\\file'");
    }

    [Fact]
    public void MySql_FormatParameterValue_BoolTrue_Returns1()
    {
        var param = new DalParameter("test", true, typeof(bool));

        var result = DalMySql.FormatParameterValue(param);

        result.Should().Be("1");
    }

    [Fact]
    public void MySql_FormatParameterValue_BoolFalse_Returns0()
    {
        var param = new DalParameter("test", false, typeof(bool));

        var result = DalMySql.FormatParameterValue(param);

        result.Should().Be("0");
    }

    #endregion

    #region SubstituteParameters

    [Fact]
    public void PostgreSql_SubstituteParameters_ReplacesParameterPlaceholders()
    {
        var sql = "SELECT * FROM t WHERE id = @Id AND name = @Name";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Id", 42, typeof(int)));
        paramList.AddParameter(new DalParameter("Name", "test", typeof(string)));

        var result = DalPostgreSql.SubstituteParameters(sql, paramList);

        result.Should().Be("SELECT * FROM t WHERE id = 42 AND name = 'test'");
    }

    [Fact]
    public void PostgreSql_SubstituteParameters_LongerParamNameReplacedFirst()
    {
        // @ProductId should be replaced before @Product to avoid partial matches
        var sql = "SELECT @ProductId, @Product";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("ProductId", 1, typeof(int)));
        paramList.AddParameter(new DalParameter("Product", "MyProduct", typeof(string)));

        var result = DalPostgreSql.SubstituteParameters(sql, paramList);

        result.Should().Be("SELECT 1, 'MyProduct'");
    }

    [Fact]
    public void MariaDb_SubstituteParameters_ReplacesParameterPlaceholders()
    {
        var sql = "SELECT * FROM t WHERE id = @Id AND name = @Name";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Id", 42, typeof(int)));
        paramList.AddParameter(new DalParameter("Name", "test", typeof(string)));

        var result = DalMariaDb.SubstituteParameters(sql, paramList);

        result.Should().Be("SELECT * FROM t WHERE id = 42 AND name = 'test'");
    }

    #endregion
}
