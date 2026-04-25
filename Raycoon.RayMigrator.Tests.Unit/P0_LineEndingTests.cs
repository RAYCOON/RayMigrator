
using FluentAssertions;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0-6: Line Ending tests for CRLF (Windows) compatibility.
/// Verifies that TOML extraction, parsing, and SQL splitting work correctly
/// with \r\n line endings, since all other tests use \n exclusively.
/// Errors here would mean Windows-authored migration files are parsed incorrectly.
/// </summary>
public class LineEndingExtractTomlAndSqlTests
{
    private const string Crlf = "\r\n";

    [Fact]
    public void Crlf_FullTomlBlock_ExtractedCorrectly()
    {
        var input = $"/*{Crlf}[RayMigrator]{Crlf}UseTransaction = true{Crlf}*/{Crlf}SELECT 1";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().Contain("UseTransaction = true");
        sql.Should().Be("SELECT 1");
    }

    [Fact]
    public void Crlf_MixedLineEndings_TomlCrlfSqlLf_BothExtracted()
    {
        var input = $"/*{Crlf}[RayMigrator]{Crlf}RunAlways = true{Crlf}*/\nSELECT 1;\nSELECT 2;";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().Contain("RunAlways = true");
        sql.Should().Contain("SELECT 1;");
        sql.Should().Contain("SELECT 2;");
    }

    [Fact]
    public void Crlf_EmptyLinesInTomlBlock_ExtractedCorrectly()
    {
        var input = $"/*{Crlf}[RayMigrator]{Crlf}{Crlf}UseTransaction = false{Crlf}{Crlf}*/{Crlf}SELECT 1";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().Contain("UseTransaction = false");
        sql.Should().Be("SELECT 1");
    }

    [Fact]
    public void Crlf_OnlyToml_NoSql_ReturnEmptySql()
    {
        var input = $"/*{Crlf}[RayMigrator]{Crlf}Description = \"Windows file\"{Crlf}*/";

        MigrationService.ExtractTomlAndSql(input, out var toml, out var sql);

        toml.Should().NotBeNull();
        toml.Should().Contain("Description = \"Windows file\"");
        sql.Should().BeEmpty();
    }
}

public class LineEndingParseTomlConfigTests
{
    private const string Crlf = "\r\n";

    [Fact]
    public void Crlf_AllProperties_ParsedCorrectly()
    {
        var toml = $"UseTransaction = false{Crlf}Description = \"Test\"{Crlf}Environments = [\"Docker\", \"Production\"]{Crlf}Targets = [\"Backend\"]{Crlf}RunAlways = true";

        MigrationService.ParseTomlConfig(toml,
            out var useTransaction, out var description,
            out var environments, out var targets, out var runAlways, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeFalse();
        description.Should().Be("Test");
        environments.Should().HaveCount(2);
        environments.Should().Contain("Docker");
        environments.Should().Contain("Production");
        targets.Should().HaveCount(1);
        targets.Should().Contain("Backend");
        runAlways.Should().BeTrue();
    }

    [Fact]
    public void Crlf_CommentLine_Ignored()
    {
        var toml = $"# This is a comment{Crlf}UseTransaction = false";

        MigrationService.ParseTomlConfig(toml,
            out var useTransaction, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeFalse();
    }

    [Fact]
    public void Crlf_EmptyLines_Ignored()
    {
        var toml = $"{Crlf}{Crlf}UseTransaction = false{Crlf}{Crlf}";

        MigrationService.ParseTomlConfig(toml,
            out var useTransaction, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        useTransaction.Should().BeFalse();
    }
}

public class LineEndingSplitSqlIntoBlocksTests
{
    private const string Crlf = "\r\n";

    [Fact]
    public void Crlf_GoDelimiter_SplitsCorrectly()
    {
        var sql = $"SELECT 1{Crlf}GO{Crlf}SELECT 2";

        var result = MigrationService.SplitSqlIntoBlocks(sql, "GO");

        result.Should().HaveCount(2);
        result[0].Should().Be("SELECT 1");
        result[1].Should().Be("SELECT 2");
    }

    [Fact]
    public void Crlf_MixedLineEndings_SplitsCorrectly()
    {
        var sql = $"SELECT 1{Crlf}GO\nSELECT 2{Crlf}GO{Crlf}SELECT 3";

        var result = MigrationService.SplitSqlIntoBlocks(sql, "GO");

        result.Should().HaveCount(3);
        result[0].Should().Be("SELECT 1");
        result[1].Should().Be("SELECT 2");
        result[2].Should().Be("SELECT 3");
    }

    [Fact]
    public void Crlf_MultipleGoBlocks_AllSplit()
    {
        var sql = $"CREATE TABLE T1 (Id INT){Crlf}GO{Crlf}CREATE TABLE T2 (Id INT){Crlf}GO{Crlf}INSERT INTO T1 VALUES (1)";

        var result = MigrationService.SplitSqlIntoBlocks(sql, "GO");

        result.Should().HaveCount(3);
        result[0].Should().Contain("T1");
        result[1].Should().Contain("T2");
        result[2].Should().Contain("INSERT");
    }
}

public class LineEndingHashSensitivityTests
{
    [Fact]
    public void SameContent_DifferentLineEndings_ProduceDifferentHashes()
    {
        // Hash is computed on raw strings, so LF vs CRLF produces different hashes.
        // This is by design - the hash reflects the exact file content.
        var contentLf = "SELECT 1\nSELECT 2";
        var contentCrlf = "SELECT 1\r\nSELECT 2";

        var hashLf = contentLf.GenerateSha256();
        var hashCrlf = contentCrlf.GenerateSha256();

        hashLf.Should().NotBe(hashCrlf,
            "hash is computed on raw bytes, so different line endings produce different hashes (by design)");
    }

    [Fact]
    public void ExtractedSql_PreservesOriginalLineEndings_AffectsHash()
    {
        // After TOML extraction, the SQL retains its original line endings.
        // This means the same SQL content with different line endings will hash differently.
        var inputLf = "/*\n[RayMigrator]\nRunAlways=true\n*/\nSELECT 1\nSELECT 2";
        var inputCrlf = "/*\r\n[RayMigrator]\r\nRunAlways=true\r\n*/\r\nSELECT 1\r\nSELECT 2";

        MigrationService.ExtractTomlAndSql(inputLf, out _, out var sqlLf);
        MigrationService.ExtractTomlAndSql(inputCrlf, out _, out var sqlCrlf);

        var hashLf = sqlLf.GenerateSha256();
        var hashCrlf = sqlCrlf.GenerateSha256();

        hashLf.Should().NotBe(hashCrlf,
            "extracted SQL preserves original line endings, producing different hashes");
    }
}
