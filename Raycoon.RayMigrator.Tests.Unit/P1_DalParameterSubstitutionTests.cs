using AwesomeAssertions;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <see cref="DalParameterSubstitution"/>, the single-pass placeholder substitution shared by the
/// PostgreSQL, MariaDB, MySQL and SQLite DALs. A placeholder is matched once, as a whole identifier, in the
/// template text only; identifiers that are not parameters stay as they are.
/// </summary>
public class DalParameterSubstitutionTests
{
    private static string Quote(DalParameter parameter)
    {
        if (parameter.ParameterValue == null)
            return "NULL";
        if (parameter.ParameterType == typeof(string))
            return "'" + parameter.ParameterValue.ToString()!.Replace("'", "''") + "'";
        return parameter.ParameterValue.ToString()!;
    }

    private static DalParameterList Parameters(params (string Name, object? Value)[] parameters)
    {
        var list = new DalParameterList();
        foreach (var (name, value) in parameters)
            list.AddParameter(new DalParameter(name, value, value?.GetType() ?? typeof(string)));
        return list;
    }

    [Fact]
    public void Substitute_KnownPlaceholders_ReplacesEachOnce()
    {
        var sql = "SELECT * FROM t WHERE id = @Id AND name = @Name";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Id", 42), ("Name", "test")), Quote);

        result.Should().Be("SELECT * FROM t WHERE id = 42 AND name = 'test'");
    }

    [Fact]
    public void Substitute_ValueContainingAnotherPlaceholder_IsNotSubstitutedAgain()
    {
        var sql = "INSERT INTO t (message, id) VALUES (@Message, @Id)";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Message", "see @Id"), ("Id", 42)), Quote);

        result.Should().Be("INSERT INTO t (message, id) VALUES ('see @Id', 42)");
    }

    [Fact]
    public void Substitute_ValueContainingPlaceholderAndOtherValueWithQuote_StayTwoLiterals()
    {
        // Sequential replacement put the second value inside the first literal, where its quote ended the literal.
        var sql = "SELECT @Message, @Name";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Message", "@Name"), ("Name", "x' OR 1=1 --")), Quote);

        result.Should().Be("SELECT '@Name', 'x'' OR 1=1 --'");
    }

    [Fact]
    public void Substitute_PlaceholderThatPrefixesLongerIdentifier_LeavesLongerIdentifierAlone()
    {
        var sql = "SELECT @ProductId, @Product";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Product", "P")), Quote);

        result.Should().Be("SELECT @ProductId, 'P'");
    }

    [Fact]
    public void Substitute_LongerAndShorterName_ReplacesBothWhole()
    {
        var sql = "SELECT @ProductId, @Product";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("ProductId", 1), ("Product", "P")), Quote);

        result.Should().Be("SELECT 1, 'P'");
    }

    [Fact]
    public void Substitute_SessionVariable_StaysUnchanged()
    {
        var sql = "SET @v_count = @Id; SELECT @v_count";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Id", 42)), Quote);

        result.Should().Be("SET @v_count = 42; SELECT @v_count");
    }

    [Fact]
    public void Substitute_NameComparison_IsCaseSensitive()
    {
        var sql = "SELECT @name, @Name";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Name", "x")), Quote);

        result.Should().Be("SELECT @name, 'x'");
    }

    [Fact]
    public void Substitute_SystemVariableAndAtInsideWord_StayUnchanged()
    {
        var sql = "SELECT @@Id, 'user@Id.example', @Id";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Id", 42)), Quote);

        result.Should().Be("SELECT @@Id, 'user@Id.example', 42");
    }

    [Fact]
    public void Substitute_PlaceholderUsedTwice_ReplacesBothOccurrences()
    {
        var sql = "SELECT @Id WHERE @Id > 0";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Id", 42)), Quote);

        result.Should().Be("SELECT 42 WHERE 42 > 0");
    }

    [Fact]
    public void Substitute_PlaceholderFollowedByCastOperator_IsReplaced()
    {
        var sql = "SELECT @Id::text, CAST(@Id AS TEXT)";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Id", 42)), Quote);

        result.Should().Be("SELECT 42::text, CAST(42 AS TEXT)");
    }

    [Fact]
    public void Substitute_NullValue_UsesFormatter()
    {
        var sql = "INSERT INTO t (name) VALUES (@Name)";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Name", null)), Quote);

        result.Should().Be("INSERT INTO t (name) VALUES (NULL)");
    }

    [Fact]
    public void Substitute_NoPlaceholders_ReturnsTextUnchanged()
    {
        var sql = "SELECT 1";

        var result = DalParameterSubstitution.Substitute(sql, Parameters(("Id", 42)), Quote);

        result.Should().Be("SELECT 1");
    }

    [Fact]
    public void Substitute_NullArguments_Throw()
    {
        var parameters = Parameters(("Id", 42));

        var sqlNull = () => DalParameterSubstitution.Substitute(null!, parameters, Quote);
        var parametersNull = () => DalParameterSubstitution.Substitute("SELECT 1", null!, Quote);
        var formatterNull = () => DalParameterSubstitution.Substitute("SELECT 1", parameters, null!);

        sqlNull.Should().Throw<ArgumentNullException>();
        parametersNull.Should().Throw<ArgumentNullException>();
        formatterNull.Should().Throw<ArgumentNullException>();
    }
}
