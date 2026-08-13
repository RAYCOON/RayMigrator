using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0-2: TOML Metadata Parsing tests.
/// Errors here lead to wrong transaction settings (data loss) or wrong environment filtering.
/// </summary>
public class ExtractTomlAndSqlTests
{
    [Fact]
    public void ValidTomlBlock_IsExtracted()
    {
        var input = "/*\n[RayMigrator]\nUseTransaction = true\n*/\nSELECT 1";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().Contain("UseTransaction = true");
        sql.Should().Be("SELECT 1");
    }

    [Fact]
    public void NoTomlPresent_ReturnsNullToml()
    {
        var input = "SELECT 1;\nSELECT 2;";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().BeNull();
        sql.Should().Be(input);
    }

    [Fact]
    public void EmptyTomlBlock_ReturnsEmptyToml()
    {
        var input = "/*\n[RayMigrator]\n*/\nSELECT 1";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().BeEmpty();
        sql.Should().Be("SELECT 1");
    }

    [Fact]
    public void MissingClosingComment_ReturnsNullToml()
    {
        var input = "/*\n[RayMigrator]\nSELECT 1";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().BeNull();
        sql.Should().Be(input);
    }

    [Fact]
    public void OnlyToml_NoSql_ReturnEmptySql()
    {
        var input = "/*\n[RayMigrator]\nRunAlways=true\n*/";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().Contain("RunAlways=true");
        sql.Should().BeEmpty();
    }

    [Fact]
    public void TomlWithSpecialCharacters_ExtractsCorrectly()
    {
        var input = "/*\n[RayMigrator]\nDescription = \"Täble with Ümlauts\"\n*/\nSELECT 1";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().Contain("Ümlauts");
    }

    [Fact]
    public void MultipleCommentBlocks_OnlyFirstTomlMatched()
    {
        var input = "/*\n[RayMigrator]\nRunAlways=true\n*/\n/* other comment */\nSELECT 1";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().Contain("RunAlways=true");
        sql.Should().Contain("SELECT 1");
    }
}

public class ParseTomlConfigTests
{
    [Fact]
    public void AllPropertiesCorrectlyParsed()
    {
        var toml = "UseTransaction = false\nDescription = \"Test migration\"\nEnvironments = [\"Docker\", \"Production\"]\nTargets = [\"Backend\"]\nRunAlways = true";

        MigrationService.ParseTomlConfig(toml,
            out var useTransaction, out var description,
            out var environments, out var targets, out var runAlways, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeFalse();
        description.Should().Be("Test migration");
        environments.Should().HaveCount(2);
        environments.Should().Contain("Docker");
        environments.Should().Contain("Production");
        targets.Should().HaveCount(1);
        targets.Should().Contain("Backend");
        runAlways.Should().BeTrue();
    }

    [Fact]
    public void UseTransactionFalse_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("UseTransaction = false",
            out var useTransaction, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeFalse();
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData("true")]
    public void UseTransactionCaseInsensitive_ParsedCorrectly(string value)
    {
        MigrationService.ParseTomlConfig($"UseTransaction = {value}",
            out var useTransaction, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeTrue();
    }

    [Fact]
    public void UseTransactionInvalidValue_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlConfig("UseTransaction = maybe",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*maybe*")
            .WithMessage("*UseTransaction*");
    }

    [Fact]
    public void RunAlwaysTrue_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("RunAlways = true",
            out _, out _, out _, out _, out var runAlways, out _, out _, out _, out _, out _, out _);

        runAlways.Should().BeTrue();
    }

    [Fact]
    public void DescriptionWithQuotes_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("Description = \"Meine Beschreibung\"",
            out _, out var description, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        description.Should().Be("Meine Beschreibung");
    }

    [Fact]
    public void DescriptionWithoutQuotes_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("Description = plain text",
            out _, out var description, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        description.Should().Be("plain text");
    }

    [Fact]
    public void EnvironmentsWildcard_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("Environments = [\"*\"]",
            out _, out _, out var environments, out _, out _, out _, out _, out _, out _, out _, out _);

        environments.Should().NotBeNull();
        environments.Should().Contain("*");
    }

    [Fact]
    public void EnvironmentsSpecific_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("Environments = [\"Docker\", \"Production\"]",
            out _, out _, out var environments, out _, out _, out _, out _, out _, out _, out _, out _);

        environments.Should().HaveCount(2);
        environments.Should().Contain("Docker");
        environments.Should().Contain("Production");
    }

    [Fact]
    public void EnvironmentsEmptyArray_ParsedAsEmptyList()
    {
        MigrationService.ParseTomlConfig("Environments = []",
            out _, out _, out var environments, out _, out _, out _, out _, out _, out _, out _, out _);

        environments.Should().NotBeNull();
        environments.Should().BeEmpty();
    }

    [Fact]
    public void TargetsArray_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("Targets = [\"Backend\"]",
            out _, out _, out _, out var targets, out _, out _, out _, out _, out _, out _, out _);

        targets.Should().HaveCount(1);
        targets.Should().Contain("Backend");
    }

    [Fact]
    public void CommentLineIgnored()
    {
        MigrationService.ParseTomlConfig("# comment\nUseTransaction = false",
            out var useTransaction, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeFalse();
    }

    [Fact]
    public void EmptyLinesIgnored()
    {
        MigrationService.ParseTomlConfig("\n\nUseTransaction = false\n\n",
            out var useTransaction, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeFalse();
    }

    [Fact]
    public void UnknownKey_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlConfig("UnknownKey = value\nUseTransaction = false",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*UnknownKey*");
    }

    [Fact]
    public void MultipleEqualsSignsInValue_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("Description = \"a=b=c\"",
            out _, out var description, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        description.Should().Be("a=b=c");
    }

    [Fact]
    public void EmptyTomlContent_AllDefaults()
    {
        MigrationService.ParseTomlConfig("",
            out var useTransaction, out var description,
            out var environments, out var targets, out var runAlways, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeTrue();
        description.Should().BeEmpty();
        environments.Should().BeNull();
        targets.Should().BeNull();
        runAlways.Should().BeFalse();
    }

    [Fact]
    public void RequireRollbackFile_True_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("RequireRollbackFile = true",
            out _, out _, out _, out _, out _, out bool? requireRollbackFile, out _, out _, out _, out _, out _);

        requireRollbackFile.Should().Be(true);
    }

    [Fact]
    public void RequireRollbackFile_False_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("RequireRollbackFile = false",
            out _, out _, out _, out _, out _, out bool? requireRollbackFile, out _, out _, out _, out _, out _);

        requireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void RequireRollbackFile_NotPresent_IsNull()
    {
        MigrationService.ParseTomlConfig("UseTransaction = true\nRunAlways = false",
            out _, out _, out _, out _, out _, out bool? requireRollbackFile, out _, out _, out _, out _, out _);

        requireRollbackFile.Should().BeNull();
    }

    [Fact]
    public void RequireRollbackFile_CaseInsensitive()
    {
        MigrationService.ParseTomlConfig("requirerollbackfile = false",
            out _, out _, out _, out _, out _, out bool? requireRollbackFile, out _, out _, out _, out _, out _);

        requireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void RequireRollbackFile_WithOtherProperties()
    {
        MigrationService.ParseTomlConfig(
            "Description = \"Test\"\nUseTransaction = true\nRequireRollbackFile = false\nRunAlways = true",
            out bool useTransaction, out string description, out _, out _,
            out bool runAlways, out bool? requireRollbackFile, out _, out _, out _, out _, out _);

        useTransaction.Should().BeTrue();
        description.Should().Be("Test");
        runAlways.Should().BeTrue();
        requireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void StopRollbackOnMissingRollbackFile_True_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("StopRollbackOnMissingRollbackFile = true",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out bool? stopRollback);

        stopRollback.Should().Be(true);
    }

    [Fact]
    public void StopRollbackOnMissingRollbackFile_False_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("StopRollbackOnMissingRollbackFile = false",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out bool? stopRollback);

        stopRollback.Should().Be(false);
    }

    [Fact]
    public void StopRollbackOnMissingRollbackFile_NotPresent_IsNull()
    {
        MigrationService.ParseTomlConfig("UseTransaction = true",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out bool? stopRollback);

        stopRollback.Should().BeNull();
    }

    [Fact]
    public void StopRollbackOnMissingRollbackFile_CaseInsensitive()
    {
        MigrationService.ParseTomlConfig("stoprollbackonmissingrollbackfile = true",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out bool? stopRollback);

        stopRollback.Should().Be(true);
    }

    [Fact]
    public void UnknownKey_ExceptionContainsKeyName()
    {
        var act = () => MigrationService.ParseTomlConfig("UseTransaktion = true",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*UseTransaktion*");
    }

    [Fact]
    public void UnknownKey_ExceptionListsValidKeys()
    {
        var act = () => MigrationService.ParseTomlConfig("Foo = bar",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        var ex = act.Should().Throw<MigrationFileParsingException>().Which;
        ex.Message.Should().Contain("UseTransaction");
        ex.Message.Should().Contain("Description");
        ex.Message.Should().Contain("Environments");
        ex.Message.Should().Contain("Targets");
        ex.Message.Should().Contain("RunAlways");
        ex.Message.Should().Contain("RequireRollbackFile");
        ex.Message.Should().Contain("StopRollbackOnMissingRollbackFile");
        ex.Message.Should().Contain("MigrationErrorAction");
        ex.Message.Should().Contain("RollbackErrorAction");
        ex.Message.Should().Contain("UseCliToolAlias");
        ex.Message.Should().Contain("TargetGroupMigrationOrder");
    }

    [Fact]
    public void BoolInvalidValue_ExceptionContainsValueAndKeyName()
    {
        var act = () => MigrationService.ParseTomlConfig("UseTransaction = yes",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*yes*")
            .WithMessage("*UseTransaction*");
    }

    [Fact]
    public void RunAlwaysInvalidValue_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlConfig("RunAlways = yes",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*yes*")
            .WithMessage("*RunAlways*");
    }

    [Fact]
    public void RequireRollbackFileInvalidValue_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlConfig("RequireRollbackFile = 1",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*1*")
            .WithMessage("*RequireRollbackFile*");
    }

    [Fact]
    public void EnvironmentsInvalidFormat_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlConfig("Environments = Docker, Production",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*Environments*")
            .WithMessage("*array format*");
    }

    [Fact]
    public void TargetsInvalidFormat_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlConfig("Targets = Backend",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*Targets*")
            .WithMessage("*array format*");
    }

    [Fact]
    public void MultipleErrors_FirstErrorThrows()
    {
        // First line has unknown key, second line has invalid bool — first error wins
        var act = () => MigrationService.ParseTomlConfig("BadKey = value\nUseTransaction = maybe",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*BadKey*");
    }

    // --- MigrationErrorAction Tests ---

    [Theory]
    [InlineData("Terminate", MigrationErrorAction.Terminate)]
    [InlineData("Rollback", MigrationErrorAction.Rollback)]
    [InlineData("RollbackErrorOnly", MigrationErrorAction.RollbackErrorOnly)]
    [InlineData("RollbackRelease", MigrationErrorAction.RollbackRelease)]
    public void MigrationErrorAction_ValidValues_ParsedCorrectly(string value, MigrationErrorAction expected)
    {
        MigrationService.ParseTomlConfig($"MigrationErrorAction = {value}",
            out _, out _, out _, out _, out _, out _, out MigrationErrorAction? result, out _, out _, out _, out _);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("terminate")]
    [InlineData("TERMINATE")]
    [InlineData("Terminate")]
    [InlineData("rollback")]
    [InlineData("ROLLBACK")]
    public void MigrationErrorAction_CaseInsensitive(string value)
    {
        MigrationService.ParseTomlConfig($"MigrationErrorAction = {value}",
            out _, out _, out _, out _, out _, out _, out MigrationErrorAction? result, out _, out _, out _, out _);

        result.Should().NotBeNull();
    }

    [Fact]
    public void MigrationErrorAction_Quoted_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("MigrationErrorAction = \"Rollback\"",
            out _, out _, out _, out _, out _, out _, out MigrationErrorAction? result, out _, out _, out _, out _);

        result.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void MigrationErrorAction_Undefined_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlConfig("MigrationErrorAction = Undefined",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*Undefined*")
            .WithMessage("*not allowed*");
    }

    [Fact]
    public void MigrationErrorAction_InvalidValue_ThrowsExceptionWithValidValues()
    {
        var act = () => MigrationService.ParseTomlConfig("MigrationErrorAction = Stop",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        var ex = act.Should().Throw<MigrationFileParsingException>().Which;
        ex.Message.Should().Contain("Stop");
        ex.Message.Should().Contain("Terminate");
        ex.Message.Should().Contain("Rollback");
        ex.Message.Should().Contain("RollbackErrorOnly");
        ex.Message.Should().Contain("RollbackRelease");
    }

    [Fact]
    public void MigrationErrorAction_NotPresent_IsNull()
    {
        MigrationService.ParseTomlConfig("UseTransaction = true",
            out _, out _, out _, out _, out _, out _, out MigrationErrorAction? result, out _, out _, out _, out _);

        result.Should().BeNull();
    }

    [Fact]
    public void MigrationErrorAction_WithOtherProperties()
    {
        MigrationService.ParseTomlConfig(
            "Description = \"Test\"\nUseTransaction = false\nMigrationErrorAction = RollbackRelease\nRunAlways = true",
            out bool useTransaction, out string description, out _, out _,
            out bool runAlways, out _, out MigrationErrorAction? errorAction, out _, out _, out _, out _);

        useTransaction.Should().BeFalse();
        description.Should().Be("Test");
        runAlways.Should().BeTrue();
        errorAction.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void UseCliToolAlias_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("UseCliToolAlias = \"sqlcmd\"",
            out _, out _, out _, out _, out _, out _, out _, out _, out string? useCliToolAlias, out _, out _);

        useCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void UseCliToolAlias_NotPresent_IsNull()
    {
        MigrationService.ParseTomlConfig("UseTransaction = true",
            out _, out _, out _, out _, out _, out _, out _, out _, out string? useCliToolAlias, out _, out _);

        useCliToolAlias.Should().BeNull();
    }

    [Fact]
    public void UseCliToolAlias_WithoutQuotes_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("UseCliToolAlias = psql",
            out _, out _, out _, out _, out _, out _, out _, out _, out string? useCliToolAlias, out _, out _);

        useCliToolAlias.Should().Be("psql");
    }
}

public class ParseTomlEnumTests
{
    [Theory]
    [InlineData("Terminate", MigrationErrorAction.Terminate)]
    [InlineData("Rollback", MigrationErrorAction.Rollback)]
    [InlineData("RollbackErrorOnly", MigrationErrorAction.RollbackErrorOnly)]
    [InlineData("RollbackRelease", MigrationErrorAction.RollbackRelease)]
    public void ValidValues_ParsedCorrectly(string value, MigrationErrorAction expected)
    {
        var result = MigrationService.ParseTomlEnum<MigrationErrorAction>(value, "MigrationErrorAction");

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("terminate")]
    [InlineData("TERMINATE")]
    [InlineData("rollback")]
    [InlineData("rollbackerroronly")]
    public void CaseInsensitive_ParsedCorrectly(string value)
    {
        var result = MigrationService.ParseTomlEnum<MigrationErrorAction>(value, "MigrationErrorAction");

        result.Should().NotBe(MigrationErrorAction.Undefined);
    }

    [Fact]
    public void QuotedValue_ParsedCorrectly()
    {
        var result = MigrationService.ParseTomlEnum<MigrationErrorAction>("\"Terminate\"", "MigrationErrorAction");

        result.Should().Be(MigrationErrorAction.Terminate);
    }

    [Fact]
    public void Undefined_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlEnum<MigrationErrorAction>("Undefined", "MigrationErrorAction");

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*Undefined*")
            .WithMessage("*not allowed*");
    }

    [Fact]
    public void InvalidValue_ThrowsExceptionWithValidValues()
    {
        var act = () => MigrationService.ParseTomlEnum<MigrationErrorAction>("InvalidValue", "MigrationErrorAction");

        var ex = act.Should().Throw<MigrationFileParsingException>().Which;
        ex.Message.Should().Contain("InvalidValue");
        ex.Message.Should().Contain("Terminate");
        ex.Message.Should().Contain("Rollback");
        ex.Message.Should().Contain("RollbackErrorOnly");
        ex.Message.Should().Contain("RollbackRelease");
    }
}

public class GetValidEnumValuesTests
{
    [Fact]
    public void MigrationErrorAction_ExcludesUndefined()
    {
        var values = MigrationService.GetValidEnumValues<MigrationErrorAction>();

        values.Should().NotContain("Undefined");
        values.Should().Contain("Terminate");
        values.Should().Contain("Rollback");
        values.Should().Contain("RollbackErrorOnly");
        values.Should().Contain("RollbackRelease");
        values.Should().Contain("Ignore");
        values.Should().HaveCount(5);
    }
}
