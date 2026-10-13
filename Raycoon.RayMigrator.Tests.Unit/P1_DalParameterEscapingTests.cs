using AwesomeAssertions;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.PostgreSQL;
using Raycoon.RayMigrator.Database.MariaDb;
using Raycoon.RayMigrator.Database.MySql;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for DAL parameter value escaping (O3).
/// Validates that FormatParameterValue produces a correct literal for PostgreSQL, MariaDB and MySQL, and that
/// SubstituteParameters applies the shared single-pass substitution, with the PostgreSQL DO-block tag on top.
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
    public void PostgreSql_FormatParameterValue_StringWithBackslash_KeepsBackslash()
    {
        // A standard literal treats a backslash as an ordinary character (standard_conforming_strings).
        var param = new DalParameter("test", @"path\to\file", typeof(string));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be(@"'path\to\file'");
    }

    [Fact]
    public void PostgreSql_FormatParameterValue_StringWithBothQuoteAndBackslash_DoublesQuoteOnly()
    {
        var param = new DalParameter("test", @"it's a\path", typeof(string));

        var result = DalPostgreSql.FormatParameterValue(param);

        result.Should().Be(@"'it''s a\path'");
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
    public void PostgreSql_SubstituteParameters_ValueContainingPlaceholder_IsNotSubstitutedAgain()
    {
        var sql = "INSERT INTO t (message, id) VALUES (@Message, @Id)";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Message", "see @Id", typeof(string)));
        paramList.AddParameter(new DalParameter("Id", 42, typeof(int)));

        var result = DalPostgreSql.SubstituteParameters(sql, paramList);

        result.Should().Be("INSERT INTO t (message, id) VALUES ('see @Id', 42)");
    }

    [Fact]
    public void PostgreSql_SubstituteParameters_DoBlock_GetsOneTagUniqueToTheCall()
    {
        var sql = "DO $$ BEGIN PERFORM @Id; END $$;";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Id", 42, typeof(int)));

        var result = DalPostgreSql.SubstituteParameters(sql, paramList);
        var again = DalPostgreSql.SubstituteParameters(sql, paramList);

        var match = System.Text.RegularExpressions.Regex.Match(result, @"^DO (\$rm[0-9a-f]{32}\$) BEGIN PERFORM 42; END (\$rm[0-9a-f]{32}\$);$");
        match.Success.Should().BeTrue(result);
        match.Groups[1].Value.Should().Be(match.Groups[2].Value);
        again.Should().NotBe(result);
    }

    [Fact]
    public void PostgreSql_SubstituteParameters_ValueWithDollarQuotes_CannotEndDoBlock()
    {
        var sql = "DO $$ BEGIN RAISE NOTICE '%', @Message; END $$;";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Message", "ends $$ here", typeof(string)));

        var result = DalPostgreSql.SubstituteParameters(sql, paramList);

        var tag = System.Text.RegularExpressions.Regex.Match(result, @"\$rm[0-9a-f]{32}\$").Value;
        tag.Should().NotBeEmpty();
        result.Should().Be($"DO {tag} BEGIN RAISE NOTICE '%', 'ends $$ here'; END {tag};");
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

    [Fact]
    public void MariaDb_SubstituteParameters_SessionVariable_StaysUnchanged()
    {
        var sql = "SET @v_count = @Id; SELECT @v_count, @NameLower";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Id", 42, typeof(int)));
        paramList.AddParameter(new DalParameter("Name", "Test", typeof(string)));

        var result = DalMariaDb.SubstituteParameters(sql, paramList);

        result.Should().Be("SET @v_count = 42; SELECT @v_count, @NameLower");
    }

    [Fact]
    public void MySql_SubstituteParameters_ValueContainingPlaceholder_IsNotSubstitutedAgain()
    {
        var sql = "INSERT INTO t (message, id) VALUES (@Message, @Id)";
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Message", "see @Id", typeof(string)));
        paramList.AddParameter(new DalParameter("Id", 42, typeof(int)));

        var result = DalMySql.SubstituteParameters(sql, paramList);

        result.Should().Be("INSERT INTO t (message, id) VALUES ('see @Id', 42)");
    }

    #endregion
}
