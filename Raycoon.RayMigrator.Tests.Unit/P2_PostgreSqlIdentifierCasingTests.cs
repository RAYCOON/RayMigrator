using System.Text.RegularExpressions;
using FluentAssertions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Regression guard for DAL-017 PostgreSQL snake_case identifier discipline.
///
/// Assertions:
/// 1. Zero double-quoted PascalCase identifiers outside TOML/comment blocks and SELECT aliases in
///    any of the 18 PG templates — prevents regression to the quoted-PascalCase style that DAL-017
///    eliminated.
/// 2. The three reader-output SELECT templates expose the exact expected number of PascalCase output
///    aliases (Strategy B contract: storage is snake_case, reader output is aliased back to PascalCase
///    so that cross-engine C# consumers continue using row["PascalCaseKey"] without branching).
/// </summary>
public class PostgreSqlIdentifierCasingTests
{
    // -------------------------------------------------------------------------
    // Template file names — enumerate all 18 PG templates explicitly so that
    // adding a 19th file without updating the test surface is caught.
    // -------------------------------------------------------------------------
    private static readonly string[] AllPgTemplates =
    [
        "Repository_CheckCreate.sql",
        "Repository_Drop.sql",
        "Repository_Environment_CheckInsert.sql",
        "Repository_Product_CheckInsert.sql",
        "Repository_MigrationRecord_Insert.sql",
        "Repository_MigrationRecord_Update.sql",
        "Repository_MigrationRecord_UpdateHash.sql",
        "Repository_MigrationRecord_UpdateRollback.sql",
        "Repository_MigrationRecord_Select.sql",
        "Repository_MigrationRecord_GetInterrupted.sql",
        "Repository_MigrationRecord_FixOrphaned.sql",
        "Repository_MigrationRun_Insert.sql",
        "Repository_MigrationRun_Update.sql",
        "Repository_MigrationRun_Select.sql",
        "Repository_MigrationRun_SelectOrphaned.sql",
        "Repository_MigrationRun_FixOrphaned.sql",
        "DatabaseLogging_CheckCreate.sql",
        "DatabaseLogging_Insert.sql"
    ];

    // -------------------------------------------------------------------------
    // 1. Zero stray quoted PascalCase identifiers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Every PG template must have zero double-quoted identifiers of the form "PascalCase"
    /// outside of TOML/comment blocks and SELECT output aliases. Specifically:
    ///   - Strip the leading /* ... */ TOML comment block.
    ///   - Strip single-line -- comments.
    ///   - Assert that no remaining token matches "QuotedUpperCamelCase" that is not
    ///     immediately preceded by AS (case-insensitive), which would be a SELECT alias.
    ///
    /// This guards against regression: any accidental re-introduction of quoted PascalCase
    /// column/table/constraint references in DDL or DML will fail this test.
    /// </summary>
    [Theory]
    [InlineData("Repository_CheckCreate.sql")]
    [InlineData("Repository_Drop.sql")]
    [InlineData("Repository_Environment_CheckInsert.sql")]
    [InlineData("Repository_Product_CheckInsert.sql")]
    [InlineData("Repository_MigrationRecord_Insert.sql")]
    [InlineData("Repository_MigrationRecord_Update.sql")]
    [InlineData("Repository_MigrationRecord_UpdateHash.sql")]
    [InlineData("Repository_MigrationRecord_UpdateRollback.sql")]
    [InlineData("Repository_MigrationRecord_Select.sql")]
    [InlineData("Repository_MigrationRecord_GetInterrupted.sql")]
    [InlineData("Repository_MigrationRecord_FixOrphaned.sql")]
    [InlineData("Repository_MigrationRun_Insert.sql")]
    [InlineData("Repository_MigrationRun_Update.sql")]
    [InlineData("Repository_MigrationRun_Select.sql")]
    [InlineData("Repository_MigrationRun_SelectOrphaned.sql")]
    [InlineData("Repository_MigrationRun_FixOrphaned.sql")]
    [InlineData("DatabaseLogging_CheckCreate.sql")]
    [InlineData("DatabaseLogging_Insert.sql")]
    public void PgTemplate_HasNoStrayQuotedPascalCaseIdentifiers(string templateFile)
    {
        var raw = ReadTemplate("PostgreSQL", templateFile);

        // Strip leading /* ... */ block comment (the TOML header).
        // All 18 templates start with a block comment; strip ALL block comments for safety.
        string stripped = Regex.Replace(raw, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        // Strip single-line -- comments (so lines like "-- Created by RayMigrator" don't trigger).
        stripped = Regex.Replace(stripped, @"--[^\r\n]*", string.Empty);

        // Find "QuotedUpperCamelCase" tokens NOT immediately preceded by AS (with optional whitespace).
        // Regex explanation:
        //   (?<!AS\s{0,10})  — negative lookbehind: must NOT be preceded by AS (up to 10 spaces allowed)
        //   "([A-Z][a-zA-Z]+)"  — double-quoted token starting with uppercase
        // Note: we allow "Environment" in RAISE strings which have single-quotes, not double-quotes.
        var strayPattern = new Regex(
            @"(?<![Aa][Ss]\s{0,10})" + "\"" + @"([A-Z][a-zA-Z]+)" + "\"");

        var strayMatches = strayPattern.Matches(stripped)
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

        strayMatches.Should().BeEmpty(
            $"{templateFile} must not contain quoted PascalCase identifiers outside TOML/comment blocks and SELECT aliases after DAL-017. " +
            $"Found: {string.Join(", ", strayMatches)}");
    }

    /// <summary>
    /// Confirms the count of PG templates: exactly 18. If a new template is added without
    /// updating this test class the count assertion fails, prompting a review.
    /// </summary>
    [Fact]
    public void PgTemplates_TotalCount_IsEighteen()
    {
        var templatesDir = GetTemplatesDir("PostgreSQL");
        var actualFiles = Directory.GetFiles(templatesDir, "*.sql");

        actualFiles.Should().HaveCount(18,
            "the PostgreSQL Templates directory must contain exactly 18 .sql files after DAL-017; " +
            "if you added a new template, also add it to P2_PostgreSqlIdentifierCasingTests");
    }

    // -------------------------------------------------------------------------
    // 2. Reader-output SELECT alias counts (Strategy B contract)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Repository_MigrationRecord_Select.sql must expose exactly 21 PascalCase output aliases
    /// (one per column consumed by TemplateExecutor.RepositoryMigrationSelect and the
    /// cross-engine row["PascalCaseKey"] reader contract).
    /// </summary>
    [Fact]
    public void MigrationSelect_Has21PascalCaseAliases()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_MigrationRecord_Select.sql");

        var aliasPattern = new Regex(@"\bAS\s+""[A-Z][a-zA-Z]+""");
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(21,
            "Repository_MigrationRecord_Select.sql must have exactly 21 AS \"PascalCase\" aliases to satisfy " +
            "the cross-engine row[\"PascalCase\"] reader contract (DAL-017 Strategy B)");
    }

    /// <summary>
    /// Repository_MigrationRun_Select.sql must expose exactly 10 PascalCase output aliases.
    /// </summary>
    [Fact]
    public void MigrationRunSelect_Has10PascalCaseAliases()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_MigrationRun_Select.sql");

        var aliasPattern = new Regex(@"\bAS\s+""[A-Z][a-zA-Z]+""");
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(10,
            "Repository_MigrationRun_Select.sql must have exactly 10 AS \"PascalCase\" aliases " +
            "(Id, ProductId, MigrationRunModeId, MigrationRunResultId, EnvironmentId, " +
            "FromReleaseVersion, ToReleaseVersion, StartedAt, FinishedAt, DurationInMs)");
    }

    /// <summary>
    /// Repository_MigrationRun_SelectOrphaned.sql must expose exactly 5 PascalCase output aliases
    /// consumed by MigrationService.FixIssuesAsync.
    /// </summary>
    [Fact]
    public void MigrationRunSelectOrphaned_Has5PascalCaseAliases()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_MigrationRun_SelectOrphaned.sql");

        var aliasPattern = new Regex(@"\bAS\s+""[A-Z][a-zA-Z]+""");
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(5,
            "Repository_MigrationRun_SelectOrphaned.sql must have exactly 5 AS \"PascalCase\" aliases " +
            "(MigrationRunId, EnvironmentId, StartedAt, MigrationRunModeId, MinutesRunning)");
    }

    /// <summary>
    /// Aggregated alias count: all three reader SELECT templates together must supply at least 36
    /// aliases (21 + 10 + 5). A count below 36 means at least one reader column is missing an alias
    /// and the C# row["PascalCaseKey"] lookup would return null at runtime.
    /// </summary>
    [Fact]
    public void AllReaderSelects_TotalAliasCount_IsAtLeast36()
    {
        var readerTemplates = new[]
        {
            "Repository_MigrationRecord_Select.sql",
            "Repository_MigrationRun_Select.sql",
            "Repository_MigrationRun_SelectOrphaned.sql"
        };

        var aliasPattern = new Regex(@"\bAS\s+""[A-Z][a-zA-Z]+""");
        int total = readerTemplates.Sum(t => aliasPattern.Matches(ReadTemplate("PostgreSQL", t)).Count);

        total.Should().BeGreaterThanOrEqualTo(36,
            "the three reader-output SELECT templates must collectively expose at least 36 PascalCase " +
            "AS \"...\" aliases to satisfy the cross-engine reader contract");
    }

    // -------------------------------------------------------------------------
    // Helpers (same pattern as P1_PostgreSqlTypeConventionsTests)
    // -------------------------------------------------------------------------

    private static string GetTemplatePath(string engine, string templateFile)
    {
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var templatePath = Path.Combine(assemblyDir, "DataAccessLayers", engine, templateFile);

        if (!File.Exists(templatePath))
        {
            var solutionDir = FindSolutionDir(assemblyDir);
            if (solutionDir != null)
                templatePath = Path.Combine(solutionDir, $"Raycoon.RayMigrator.Database.{engine}", "Templates", templateFile);
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
                templatesDir = Path.Combine(solutionDir, $"Raycoon.RayMigrator.Database.{engine}", "Templates");
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
}
