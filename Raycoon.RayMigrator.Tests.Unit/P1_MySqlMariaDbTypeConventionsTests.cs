// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Text.RegularExpressions;
using FluentAssertions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: MySQL + MariaDB type-convention assertions covering DAL-014 (DATETIME → TIMESTAMP,
/// UTC_TIMESTAMP() → CURRENT_TIMESTAMP) and DAL-015 (explicit CHARSET/COLLATE per engine).
/// Also enforces the TOML Version / @v_repository_version consistency invariant (Note4).
///
/// Engines under test: "MySql" (MySQL 8.0+, utf8mb4_0900_ai_ci) and "MariaDb"
/// (MariaDB 10.5+ LTS, utf8mb4_unicode_ci). The two names map to the project
/// directories "Raycoon.RayMigrator.Database.MySql" and "Raycoon.RayMigrator.Database.MariaDb".
/// </summary>
public class MySqlMariaDbTypeConventionsTests
{
    #region DAL-014: TIMESTAMP + CURRENT_TIMESTAMP

    [Theory]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    public void Repository_CheckCreate_AllAuditColumns_UseTimestamp(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");

        // DAL-018: columns are now snake_case and unquoted (no backticks around identifiers).
        foreach (var column in new[] { "created_at", "started_at", "finished_at", "historized_at" })
        {
            // Match column name followed by whitespace, then TIMESTAMP, then whitespace, then optional NOT + NULL.
            var pattern = new Regex(@"\b" + Regex.Escape(column) + @"\s+TIMESTAMP\s+(?:NOT\s+)?NULL\b", RegexOptions.IgnoreCase);
            pattern.IsMatch(content).Should().BeTrue(
                $"column {column} in {engine} Repository_CheckCreate.sql must be declared as TIMESTAMP after DAL-014 and use snake_case after DAL-018");
        }
    }

    [Theory]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    public void Repository_CheckCreate_ContainsNoDatetime(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");

        var pattern = new Regex(@"\bDATETIME\b");
        pattern.IsMatch(content).Should().BeFalse(
            $"{engine} Repository_CheckCreate.sql must not contain any DATETIME token after DAL-014");
    }

    [Theory]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    public void DatabaseLogging_CheckCreate_CreatedAt_IsTimestamp(string engine)
    {
        var content = ReadTemplate(engine, "DatabaseLogging_CheckCreate.sql");

        // DAL-018: column is now unquoted snake_case.
        var pattern = new Regex(@"\bcreated_at\s+TIMESTAMP\b", RegexOptions.IgnoreCase);
        pattern.IsMatch(content).Should().BeTrue(
            $"{engine} DatabaseLogging_CheckCreate.sql created_at column must use TIMESTAMP after DAL-014 and use snake_case after DAL-018");
    }

    [Theory]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    public void AllTemplates_NoUtcTimestampCalls(string engine)
    {
        var templatesDir = GetTemplatesDir(engine);
        var sqlFiles = Directory.GetFiles(templatesDir, "*.sql");

        sqlFiles.Should().NotBeEmpty($"the {engine} Templates folder must contain SQL template files");

        foreach (var file in sqlFiles)
        {
            var content = File.ReadAllText(file);
            content.Should().NotContain("UTC_TIMESTAMP",
                $"{Path.GetFileName(file)} ({engine}) must not reference UTC_TIMESTAMP after DAL-014 (CURRENT_TIMESTAMP is the SQL-standard equivalent once session time_zone='+00:00')");
        }
    }

    [Theory]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    public void Repository_CheckCreate_HistorizedAt_DefaultIsCurrentTimestamp(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");

        // DAL-018: column is now unquoted snake_case. Must be bare `DEFAULT CURRENT_TIMESTAMP` without parentheses.
        var pattern = new Regex(@"\bhistorized_at\s+TIMESTAMP\s+NOT\s+NULL\s+DEFAULT\s+CURRENT_TIMESTAMP\b(?!\s*\()", RegexOptions.IgnoreCase);
        pattern.IsMatch(content).Should().BeTrue(
            $"{engine} Repository_CheckCreate.sql historized_at default must be bare CURRENT_TIMESTAMP (no parentheses) after DAL-014 and use snake_case after DAL-018");
    }

    #endregion

    #region DAL-015: Explicit CHARSET / COLLATE per engine

    [Theory]
    [InlineData("MySql", "utf8mb4_0900_ai_ci", 11)]
    [InlineData("MariaDb", "utf8mb4_unicode_ci", 11)]
    public void Repository_CheckCreate_AllCreateTables_EndWithCorrectEngineCharsetCollate(string engine, string expectedCollation, int expectedCount)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");
        var needle = $") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE={expectedCollation};";

        var actual = CountOccurrences(content, needle);
        actual.Should().Be(expectedCount,
            $"{engine} Repository_CheckCreate.sql must contain exactly {expectedCount} occurrences of '{needle}' after DAL-015");
    }

    [Theory]
    [InlineData("MySql", "utf8mb4_0900_ai_ci", 2)]
    [InlineData("MariaDb", "utf8mb4_unicode_ci", 2)]
    public void DatabaseLogging_CheckCreate_AllCreateTables_EndWithCorrectEngineCharsetCollate(string engine, string expectedCollation, int expectedCount)
    {
        var content = ReadTemplate(engine, "DatabaseLogging_CheckCreate.sql");
        var needle = $") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE={expectedCollation};";

        var actual = CountOccurrences(content, needle);
        actual.Should().Be(expectedCount,
            $"{engine} DatabaseLogging_CheckCreate.sql must contain exactly {expectedCount} occurrences of '{needle}' after DAL-015");
    }

    [Theory]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    public void AllTemplates_NoBareEngineInnoDb(string engine)
    {
        foreach (var templateName in new[] { "Repository_CheckCreate.sql", "DatabaseLogging_CheckCreate.sql" })
        {
            var content = ReadTemplate(engine, templateName);
            var pattern = new Regex(@"\)\s+ENGINE=InnoDB\s*;");
            pattern.IsMatch(content).Should().BeFalse(
                $"{engine} {templateName} must not contain bare `) ENGINE=InnoDB;` (without CHARSET/COLLATE) after DAL-015");
        }
    }

    [Fact]
    public void MySql_NoMariaDbCollation()
    {
        var templatesDir = GetTemplatesDir("MySql");
        var sqlFiles = Directory.GetFiles(templatesDir, "*.sql");

        foreach (var file in sqlFiles)
        {
            var content = File.ReadAllText(file);
            content.Should().NotContain("utf8mb4_unicode_ci",
                $"{Path.GetFileName(file)} (MySql) must not reference the MariaDB-specific collation utf8mb4_unicode_ci");
        }
    }

    [Fact]
    public void MariaDb_NoMySqlCollation()
    {
        var templatesDir = GetTemplatesDir("MariaDb");
        var sqlFiles = Directory.GetFiles(templatesDir, "*.sql");

        foreach (var file in sqlFiles)
        {
            var content = File.ReadAllText(file);
            content.Should().NotContain("utf8mb4_0900_ai_ci",
                $"{Path.GetFileName(file)} (MariaDb) must not reference the MySQL-specific collation utf8mb4_0900_ai_ci (MariaDB raises ERROR 1273 for unknown collation)");
        }
    }

    #endregion

    #region Consistency invariants

    [Theory]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    public void Repository_CheckCreate_TomlVersionMatchesConstant(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");

        var tomlSectionMatch = Regex.Match(content,
            @"\[RayMigratorTemplate\](.*?)\[(?!RayMigratorTemplate)",
            RegexOptions.Singleline);
        tomlSectionMatch.Success.Should().BeTrue($"{engine} Repository_CheckCreate.sql must have a [RayMigratorTemplate] TOML section");

        var tomlSection = tomlSectionMatch.Groups[1].Value;
        var versionMatch = Regex.Match(tomlSection, @"Version\s*=\s*""([^""]+)""");
        versionMatch.Success.Should().BeTrue($"{engine} [RayMigratorTemplate] section must declare a Version");
        var tomlVersion = versionMatch.Groups[1].Value;

        var constantMatch = Regex.Match(content, @"SET\s+@v_repository_version\s*=\s*'([^']+)'\s*;");
        constantMatch.Success.Should().BeTrue($"{engine} Repository_CheckCreate.sql must declare a @v_repository_version session variable");
        var constantVersion = constantMatch.Groups[1].Value;

        constantVersion.Should().Be(tomlVersion,
            $"{engine}: @v_repository_version SET value must equal the TOML Version header (Note4 invariant)");
    }

    #endregion

    #region Helpers

    private static string GetTemplatePath(string engine, string templateFile)
    {
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var templatePath = Path.Combine(assemblyDir, "DataAccessLayers", engine, templateFile);

        if (!File.Exists(templatePath))
        {
            var solutionDir = FindSolutionDir(assemblyDir);
            if (solutionDir != null)
            {
                templatePath = Path.Combine(solutionDir, $"Raycoon.RayMigrator.Database.{engine}", "Templates", templateFile);
            }
        }

        return templatePath;
    }

    private static string ReadTemplate(string engine, string templateFile)
    {
        var path = GetTemplatePath(engine, templateFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Template file not found for engine '{engine}': {path}");
        return File.ReadAllText(path);
    }

    private static string GetTemplatesDir(string engine)
    {
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var templatesDir = Path.Combine(assemblyDir, "DataAccessLayers", engine);

        if (!Directory.Exists(templatesDir))
        {
            var solutionDir = FindSolutionDir(assemblyDir);
            if (solutionDir != null)
            {
                templatesDir = Path.Combine(solutionDir, $"Raycoon.RayMigrator.Database.{engine}", "Templates");
            }
        }

        if (!Directory.Exists(templatesDir))
            throw new DirectoryNotFoundException($"Templates directory not found for engine '{engine}': {templatesDir}");

        return templatesDir;
    }

    private static string? FindSolutionDir(string startDir)
    {
        var dir = startDir;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle))
            return 0;

        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    #endregion
}
