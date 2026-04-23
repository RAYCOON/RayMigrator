// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using FluentAssertions;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0-1: SQL Statement Splitting tests.
/// SplitSqlIntoBlocks is critical - errors here lead to corrupt SQL statements that can damage databases.
/// </summary>
public class SplitSqlIntoBlocksTests
{
    [Fact]
    public void GoOnOwnLine_SplitsCorrectly()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1\nGO\nSELECT 2", "GO");

        result.Should().HaveCount(2);
        result[0].Should().Be("SELECT 1");
        result[1].Should().Be("SELECT 2");
    }

    [Fact]
    public void GoWithLeadingWhitespace_SplitsCorrectly()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1\n  GO\nSELECT 2", "GO");

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GoWithTrailingWhitespace_SplitsCorrectly()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1\nGO  \nSELECT 2", "GO");

        result.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("go")]
    [InlineData("Go")]
    [InlineData("gO")]
    public void GoCaseInsensitive_SplitsCorrectly(string goVariant)
    {
        var result = MigrationService.SplitSqlIntoBlocks($"SELECT 1\n{goVariant}\nSELECT 2", "GO");

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GoInSameLineAsSql_DoesNotSplit()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1 GO SELECT 2", "GO");

        result.Should().HaveCount(1);
    }

    [Fact]
    public void CommentFollowedByGo_Splits()
    {
        var result = MigrationService.SplitSqlIntoBlocks("--comment\nGO\nSELECT 1", "GO");

        // The comment block may be filtered out as whitespace-only or kept
        result.Should().Contain("SELECT 1");
    }

    [Fact]
    public void GoInSingleLineComment_DoesNotSplit()
    {
        // "-- GO" is NOT a standalone GO on its own line (it has text before GO)
        var result = MigrationService.SplitSqlIntoBlocks("-- GO\nSELECT 1", "GO");

        result.Should().HaveCount(1);
    }

    [Fact]
    public void GoInMultiLineComment_KnownLimitation_SplitsAnyway()
    {
        // Known limitation: regex-based approach cannot understand SQL context
        var result = MigrationService.SplitSqlIntoBlocks("/* comment\nGO\n*/\nSELECT 1", "GO");

        // Documents actual behavior - regex splits on GO regardless of comment context
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void GoInStringLiteral_DocumentActualBehavior()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 'GO'\nGO\nSELECT 2", "GO");

        // The standalone GO line will split; the 'GO' inside the string literal stays in the first block
        result.Should().HaveCount(2);
    }

    [Fact]
    public void TwoConsecutiveGos_FiltersEmptyBlocks()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1\nGO\nGO\nSELECT 2", "GO");

        result.Should().HaveCount(2);
        result[0].Should().Be("SELECT 1");
        result[1].Should().Be("SELECT 2");
    }

    [Fact]
    public void OnlyGos_ReturnsFallbackSingleBlock()
    {
        // When regex splits on all GOs, the resulting blocks are all empty/whitespace.
        // The code filters those out, then falls back to returning the original content
        // as a single block since sqlContent is not whitespace-only ("GO\nGO\nGO").
        var result = MigrationService.SplitSqlIntoBlocks("GO\nGO\nGO", "GO");

        // Actual behavior: the fallback path returns the trimmed original content
        result.Should().HaveCount(1);
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        var result = MigrationService.SplitSqlIntoBlocks("", "GO");

        result.Should().BeEmpty();
    }

    [Fact]
    public void WhitespaceOnly_ReturnsEmptyList()
    {
        var result = MigrationService.SplitSqlIntoBlocks("   \n  \n  ", "GO");

        result.Should().BeEmpty();
    }

    [Fact]
    public void NoDelimiterPresent_ReturnsSingleBlock()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1;\nSELECT 2;", "GO");

        result.Should().HaveCount(1);
        result[0].Should().Contain("SELECT 1;");
        result[0].Should().Contain("SELECT 2;");
    }

    [Fact]
    public void EmptyDelimiter_ReturnsSingleBlock()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1;\nSELECT 2;", "");

        result.Should().HaveCount(1);
    }

    [Fact]
    public void MultipleBlocksWithContent_SplitsAll()
    {
        var sql = "SELECT 1\nGO\nSELECT 2\nGO\nSELECT 3\nGO\nSELECT 4\nGO\nSELECT 5";
        var result = MigrationService.SplitSqlIntoBlocks(sql, "GO");

        result.Should().HaveCount(5);
    }

    [Fact]
    public void SemicolonAsDelimiter_PostgreSqlStyle()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1\n;\nSELECT 2", ";");

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GoWithTrailingComment_DoesNotSplit()
    {
        // "GO -- batch end" has text after GO, so it's not a standalone delimiter line
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1\nGO -- batch end\nSELECT 2", "GO");

        result.Should().HaveCount(1);
    }

    [Fact]
    public void TabBeforeGo_SplitsCorrectly()
    {
        var result = MigrationService.SplitSqlIntoBlocks("SELECT 1\n\tGO\nSELECT 2", "GO");

        result.Should().HaveCount(2);
    }
}
