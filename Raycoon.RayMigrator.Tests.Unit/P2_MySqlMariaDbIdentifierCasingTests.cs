using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Regression guard for DAL-018 MySQL/MariaDB snake_case identifier discipline.
///
/// Assertions:
/// 1. Zero backtick-quoted PascalCase identifiers outside TOML/comment blocks and SELECT aliases in
///    any of the 18 MySQL/MariaDB templates — prevents regression to the backtick-quoted PascalCase
///    style that DAL-018 eliminated.
/// 2. The three reader-output SELECT templates expose the exact expected number of PascalCase output
///    aliases (Strategy B contract: storage is snake_case, reader output is aliased back to PascalCase
///    so that cross-engine C# consumers continue using row["PascalCaseKey"] without branching).
/// </summary>
public class MySqlMariaDbIdentifierCasingTests
{
    // -------------------------------------------------------------------------
    // Template file names — enumerate all 18 MySQL/MariaDB templates explicitly
    // so that adding a 19th file without updating the test surface is caught.
    // -------------------------------------------------------------------------
    private static readonly string[] AllTemplates =
    [
        "Repository_CheckCreate.sql",
        "Repository_Upgrade_1.sql",
        "Repository_Upgrade_2.sql",
        "Repository_Upgrade_3.sql",
        "Repository_MigratorMeta_CheckInsert.sql",
        "Repository_Drop.sql",
        "Repository_Environment_CheckInsert.sql",
        "Repository_Product_CheckInsert.sql",
        "Repository_MigrationRecord_Insert.sql",
        "Repository_MigrationRecord_Update.sql",
        "Repository_MigrationRecord_UpdateHash.sql",
        "Repository_MigrationRecord_UpdateRollback.sql",
        "Repository_MigrationRecord_Select.sql",
        "Repository_MigrationRecordHistory_Select.sql",
        "Repository_MigrationRecordHistory_Delete.sql",
        "Repository_MigrationRecordHistory_Count.sql",
        "Repository_MigrationRecord_GetInterrupted.sql",
        "Repository_MigrationRecord_FixOrphaned.sql",
        "Repository_MigrationRun_Insert.sql",
        "Repository_MigrationRun_Update.sql",
        "Repository_MigrationRun_Select.sql",
        "Repository_MigrationRun_SelectOrphaned.sql",
        "Repository_MigrationRun_FixOrphaned.sql",
        "Repository_MigrationRun_Heartbeat.sql",
        "DatabaseLogging_CheckCreate.sql",
        "DatabaseLogging_Insert.sql",
        "DatabaseLogging_Delete.sql",
        "DatabaseLogging_Count.sql"
    ];

    // -------------------------------------------------------------------------
    // 1. Zero stray backtick-quoted PascalCase identifiers (MySQL)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Every MySQL template must have zero backtick-quoted identifiers of the form `PascalCase`
    /// outside of TOML/comment blocks and SELECT output aliases. Specifically:
    ///   - Strip the leading /* ... */ TOML comment block.
    ///   - Strip single-line -- comments.
    ///   - Assert that no remaining token matches `QuotedUpperCamelCase` that is not
    ///     immediately preceded by AS (case-insensitive), which would be a SELECT alias.
    ///
    /// This guards against regression: any accidental re-introduction of backtick-quoted PascalCase
    /// column/table/constraint references in DDL or DML will fail this test. DAL-018 converted all
    /// MySQL/MariaDB identifiers from backtick-quoted PascalCase to unquoted snake_case.
    /// </summary>
    [Theory]
    [InlineData("Repository_CheckCreate.sql")]
    [InlineData("Repository_Upgrade_1.sql")]
    [InlineData("Repository_Upgrade_2.sql")]
    [InlineData("Repository_Upgrade_3.sql")]
    [InlineData("Repository_MigratorMeta_CheckInsert.sql")]
    [InlineData("Repository_Drop.sql")]
    [InlineData("Repository_Environment_CheckInsert.sql")]
    [InlineData("Repository_Product_CheckInsert.sql")]
    [InlineData("Repository_Product_Select.sql")]
    [InlineData("Repository_Environment_Select.sql")]
    [InlineData("Repository_MigrationRecord_Insert.sql")]
    [InlineData("Repository_MigrationRecord_Update.sql")]
    [InlineData("Repository_MigrationRecord_UpdateHash.sql")]
    [InlineData("Repository_MigrationRecord_UpdateRollback.sql")]
    [InlineData("Repository_MigrationRecord_Select.sql")]
    [InlineData("Repository_MigrationRecordHistory_Select.sql")]
    [InlineData("Repository_MigrationRecord_GetInterrupted.sql")]
    [InlineData("Repository_MigrationRecord_FixOrphaned.sql")]
    [InlineData("Repository_MigrationRun_Insert.sql")]
    [InlineData("Repository_MigrationRun_Update.sql")]
    [InlineData("Repository_MigrationRun_Select.sql")]
    [InlineData("Repository_MigrationRun_SelectOrphaned.sql")]
    [InlineData("Repository_MigrationRun_FixOrphaned.sql")]
    [InlineData("Repository_MigrationRun_Heartbeat.sql")]
    [InlineData("DatabaseLogging_CheckCreate.sql")]
    [InlineData("DatabaseLogging_Insert.sql")]
    [InlineData("Repository_MigrationRecordHistory_Delete.sql")]
    [InlineData("Repository_MigrationRecordHistory_Count.sql")]
    [InlineData("DatabaseLogging_Delete.sql")]
    [InlineData("DatabaseLogging_Count.sql")]
    public void MySqlTemplate_HasNoStrayBacktickedPascalCaseIdentifiers(string templateFile)
    {
        var raw = ReadTemplate("MySql", templateFile);

        // Strip leading /* ... */ block comment (the TOML header).
        // All 20 templates start with a block comment; strip ALL block comments for safety.
        string stripped = Regex.Replace(raw, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        // Strip single-line -- comments (so lines like "-- DAL-018: snake_case identifiers" don't trigger).
        stripped = Regex.Replace(stripped, @"--[^\r\n]*", string.Empty);

        // Find `QuotedUpperCamelCase` tokens NOT immediately preceded by AS (with optional whitespace).
        // Regex explanation:
        //   (?<![Aa][Ss]\s{0,10})  — negative lookbehind: must NOT be preceded by AS (up to 10 spaces allowed)
        //   `([A-Z][a-zA-Z]+)`     — backtick-quoted token starting with uppercase
        var strayPattern = new Regex(
            @"(?<![Aa][Ss]\s{0,10})`([A-Z][a-zA-Z]+)`");

        var strayMatches = strayPattern.Matches(stripped)
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

        strayMatches.Should().BeEmpty(
            $"{templateFile} (MySql) must not contain backtick-quoted PascalCase identifiers outside " +
            $"TOML/comment blocks and SELECT aliases after DAL-018. " +
            $"Found: {string.Join(", ", strayMatches)}");
    }

    // -------------------------------------------------------------------------
    // 1. Zero stray backtick-quoted PascalCase identifiers (MariaDB)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Every MariaDB template must have zero backtick-quoted identifiers of the form `PascalCase`
    /// outside of TOML/comment blocks and SELECT output aliases. Mirrors the MySQL theory with
    /// the MariaDb engine name. DAL-018 applied the same snake_case conversion to both engines.
    /// </summary>
    [Theory]
    [InlineData("Repository_CheckCreate.sql")]
    [InlineData("Repository_Upgrade_1.sql")]
    [InlineData("Repository_Upgrade_2.sql")]
    [InlineData("Repository_Upgrade_3.sql")]
    [InlineData("Repository_MigratorMeta_CheckInsert.sql")]
    [InlineData("Repository_Drop.sql")]
    [InlineData("Repository_Environment_CheckInsert.sql")]
    [InlineData("Repository_Product_CheckInsert.sql")]
    [InlineData("Repository_Product_Select.sql")]
    [InlineData("Repository_Environment_Select.sql")]
    [InlineData("Repository_MigrationRecord_Insert.sql")]
    [InlineData("Repository_MigrationRecord_Update.sql")]
    [InlineData("Repository_MigrationRecord_UpdateHash.sql")]
    [InlineData("Repository_MigrationRecord_UpdateRollback.sql")]
    [InlineData("Repository_MigrationRecord_Select.sql")]
    [InlineData("Repository_MigrationRecordHistory_Select.sql")]
    [InlineData("Repository_MigrationRecord_GetInterrupted.sql")]
    [InlineData("Repository_MigrationRecord_FixOrphaned.sql")]
    [InlineData("Repository_MigrationRun_Insert.sql")]
    [InlineData("Repository_MigrationRun_Update.sql")]
    [InlineData("Repository_MigrationRun_Select.sql")]
    [InlineData("Repository_MigrationRun_SelectOrphaned.sql")]
    [InlineData("Repository_MigrationRun_FixOrphaned.sql")]
    [InlineData("Repository_MigrationRun_Heartbeat.sql")]
    [InlineData("DatabaseLogging_CheckCreate.sql")]
    [InlineData("DatabaseLogging_Insert.sql")]
    [InlineData("Repository_MigrationRecordHistory_Delete.sql")]
    [InlineData("Repository_MigrationRecordHistory_Count.sql")]
    [InlineData("DatabaseLogging_Delete.sql")]
    [InlineData("DatabaseLogging_Count.sql")]
    public void MariaDbTemplate_HasNoStrayBacktickedPascalCaseIdentifiers(string templateFile)
    {
        var raw = ReadTemplate("MariaDb", templateFile);

        // Strip leading /* ... */ block comment (the TOML header).
        string stripped = Regex.Replace(raw, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        // Strip single-line -- comments.
        stripped = Regex.Replace(stripped, @"--[^\r\n]*", string.Empty);

        // Find `QuotedUpperCamelCase` tokens NOT immediately preceded by AS (with optional whitespace).
        var strayPattern = new Regex(
            @"(?<![Aa][Ss]\s{0,10})`([A-Z][a-zA-Z]+)`");

        var strayMatches = strayPattern.Matches(stripped)
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

        strayMatches.Should().BeEmpty(
            $"{templateFile} (MariaDb) must not contain backtick-quoted PascalCase identifiers outside " +
            $"TOML/comment blocks and SELECT aliases after DAL-018. " +
            $"Found: {string.Join(", ", strayMatches)}");
    }

    /// <summary>
    /// Every index and constraint name of the schema templates is snake_case after its prefix (DAL-018). The guards
    /// above only find backticked PascalCase, so an unquoted PascalCase name would pass them. information_schema
    /// compares index names without case on both engines, so this is the convention of the templates, not a lookup
    /// that would fail.
    /// </summary>
    [Theory]
    [InlineData("MySql", "Repository_Upgrade_1.sql")]
    [InlineData("MySql", "Repository_Upgrade_2.sql")]
    [InlineData("MySql", "Repository_Upgrade_3.sql")]
    [InlineData("MySql", "DatabaseLogging_CheckCreate.sql")]
    [InlineData("MariaDb", "Repository_Upgrade_1.sql")]
    [InlineData("MariaDb", "Repository_Upgrade_2.sql")]
    [InlineData("MariaDb", "Repository_Upgrade_3.sql")]
    [InlineData("MariaDb", "DatabaseLogging_CheckCreate.sql")]
    public void Template_IndexAndConstraintNames_AreLowerCase(string engine, string templateFile)
    {
        // Arrange: the SQL without the TOML header and the line comments
        string sql = Regex.Replace(ReadTemplate(engine, templateFile), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"--[^\r\n]*", string.Empty);

        // Act
        var names = Regex.Matches(sql, @"\b(?:pk|fk|ix|uix)_\{CFG:TableBaseName\}(?<rest>\w+)").ToList();

        // Assert
        names.Should().NotBeEmpty(because: $"{engine}/{templateFile} names at least one index or constraint");
        names.Where(m => m.Groups["rest"].Value.Any(char.IsUpper)).Select(m => m.Value).Should().BeEmpty(
            because: $"{engine}/{templateFile} must write every index and constraint name in snake_case (DAL-018)");
    }

    // -------------------------------------------------------------------------
    // 2. Template counts
    // -------------------------------------------------------------------------

    /// <summary>
    /// Confirms the count of MySQL templates: exactly 30. If a new template is added without
    /// updating this test class the count assertion fails, prompting a review.
    /// </summary>
    [Fact]
    public void MySqlTemplates_TotalCount_IsThirty()
    {
        var templatesDir = GetTemplatesDir("MySql");
        var actualFiles = Directory.GetFiles(templatesDir, "*.sql");

        actualFiles.Should().HaveCount(30,
            "the MySql Templates directory must contain exactly 30 .sql files after DAL-018 (+2 read-only Select templates, #7; +1 history Select template, #13; +4 schema version templates, ADR-024; +1 heartbeat template; +4 retention templates, ADR-027); " +
            "if you added a new template, also add it to P2_MySqlMariaDbIdentifierCasingTests");
    }

    /// <summary>
    /// Confirms the count of MariaDB templates: exactly 30. Mirrors MySqlTemplates_TotalCount_IsThirty.
    /// </summary>
    [Fact]
    public void MariaDbTemplates_TotalCount_IsThirty()
    {
        var templatesDir = GetTemplatesDir("MariaDb");
        var actualFiles = Directory.GetFiles(templatesDir, "*.sql");

        actualFiles.Should().HaveCount(30,
            "the MariaDb Templates directory must contain exactly 30 .sql files after DAL-018 (+2 read-only Select templates, #7; +1 history Select template, #13; +4 schema version templates, ADR-024; +1 heartbeat template; +4 retention templates, ADR-027); " +
            "if you added a new template, also add it to P2_MySqlMariaDbIdentifierCasingTests");
    }

    // -------------------------------------------------------------------------
    // 3. Reader-output SELECT alias counts (Strategy B contract)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Repository_MigrationRecord_Select.sql (MySQL) must expose exactly 23 PascalCase output aliases
    /// (one per column consumed by TemplateExecutor.RepositoryMigrationSelect and the
    /// cross-engine row["PascalCaseKey"] reader contract: 21 plus StartedAt and FinishedAt of 0.20.0). Aliases use
    /// backtick quoting (AS `PascalCase`) which is the MySQL/MariaDB equivalent of PG's AS "PascalCase".
    /// </summary>
    [Fact]
    public void MigrationSelect_Has23Aliases_MySql()
    {
        var content = ReadTemplate("MySql", "Repository_MigrationRecord_Select.sql");

        var aliasPattern = new Regex(@"\bAS\s+`[A-Z][a-zA-Z]+`", RegexOptions.IgnoreCase);
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(23,
            "Repository_MigrationRecord_Select.sql (MySql) must have exactly 23 AS `PascalCase` aliases to " +
            "satisfy the cross-engine row[\"PascalCase\"] reader contract (DAL-018 Strategy B)");
    }

    /// <summary>
    /// Repository_MigrationRecord_Select.sql (MariaDB) must expose exactly 23 PascalCase output aliases.
    /// </summary>
    [Fact]
    public void MigrationSelect_Has23Aliases_MariaDb()
    {
        var content = ReadTemplate("MariaDb", "Repository_MigrationRecord_Select.sql");

        var aliasPattern = new Regex(@"\bAS\s+`[A-Z][a-zA-Z]+`", RegexOptions.IgnoreCase);
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(23,
            "Repository_MigrationRecord_Select.sql (MariaDb) must have exactly 23 AS `PascalCase` aliases to " +
            "satisfy the cross-engine row[\"PascalCase\"] reader contract (DAL-018 Strategy B)");
    }

    /// <summary>
    /// Repository_MigrationRecordHistory_Select.sql returns the columns of Repository_MigrationRecord_Select under the
    /// same names and in the same order: TemplateExecutor.MapMigrationRecord maps the rows of both.
    /// </summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    public void MigrationRecordHistorySelect_HasTheAliasesOfTheRecordSelect(string engine)
    {
        var aliasPattern = new Regex(@"\bAS\s+`([A-Z][a-zA-Z]+)`");

        var record = aliasPattern.Matches(ReadTemplate(engine, "Repository_MigrationRecord_Select.sql")).Select(m => m.Groups[1].Value);
        var history = aliasPattern.Matches(ReadTemplate(engine, "Repository_MigrationRecordHistory_Select.sql")).Select(m => m.Groups[1].Value);

        history.Should().Equal(record, $"both {engine} selects feed the same reader contract");
    }

    /// <summary>
    /// Repository_MigrationRun_Select.sql (MySQL) must expose exactly 18 PascalCase output aliases (10 of
    /// schema version 1 plus the 8 run details of schema version 2).
    /// </summary>
    [Fact]
    public void MigrationRunSelect_Has18Aliases_MySql()
    {
        var content = ReadTemplate("MySql", "Repository_MigrationRun_Select.sql");

        var aliasPattern = new Regex(@"\bAS\s+`[A-Z][a-zA-Z]+`", RegexOptions.IgnoreCase);
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(18,
            "Repository_MigrationRun_Select.sql (MySql) must have exactly 18 AS `PascalCase` aliases " +
            "(Id, ProductId, MigrationRunModeId, MigrationRunResultId, EnvironmentId, FromReleaseVersion, ToReleaseVersion, StartedAt, FinishedAt, DurationInMs, MigrationOperationId, MigrationRunSourceId, InitiatedBy, InitiatedBySubject, Host, ErrorCode, ErrorMessage, LastHeartbeatAt)");
    }

    /// <summary>
    /// Repository_MigrationRun_Select.sql (MariaDB) must expose exactly 18 PascalCase output aliases (10 of
    /// schema version 1 plus the 8 run details of schema version 2).
    /// </summary>
    [Fact]
    public void MigrationRunSelect_Has18Aliases_MariaDb()
    {
        var content = ReadTemplate("MariaDb", "Repository_MigrationRun_Select.sql");

        var aliasPattern = new Regex(@"\bAS\s+`[A-Z][a-zA-Z]+`", RegexOptions.IgnoreCase);
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(18,
            "Repository_MigrationRun_Select.sql (MariaDb) must have exactly 18 AS `PascalCase` aliases " +
            "(Id, ProductId, MigrationRunModeId, MigrationRunResultId, EnvironmentId, FromReleaseVersion, ToReleaseVersion, StartedAt, FinishedAt, DurationInMs, MigrationOperationId, MigrationRunSourceId, InitiatedBy, InitiatedBySubject, Host, ErrorCode, ErrorMessage, LastHeartbeatAt)");
    }

    /// <summary>
    /// Repository_MigrationRun_SelectOrphaned.sql (MySQL) must expose exactly 9 PascalCase output aliases
    /// consumed by MigrationService.FixIssuesAsync.
    /// </summary>
    [Fact]
    public void MigrationRunSelectOrphaned_Has9Aliases_MySql()
    {
        var content = ReadTemplate("MySql", "Repository_MigrationRun_SelectOrphaned.sql");

        var aliasPattern = new Regex(@"\bAS\s+`[A-Z][a-zA-Z]+`", RegexOptions.IgnoreCase);
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(9,
            "Repository_MigrationRun_SelectOrphaned.sql (MySql) must have exactly 9 AS `PascalCase` aliases " +
            "(MigrationRunId, EnvironmentId, StartedAt, MigrationRunModeId, MinutesRunning, LastHeartbeatAt, MinutesSinceHeartbeat, InitiatedBy, Host)");
    }

    /// <summary>
    /// Repository_MigrationRun_SelectOrphaned.sql (MariaDB) must expose exactly 9 PascalCase output aliases.
    /// </summary>
    [Fact]
    public void MigrationRunSelectOrphaned_Has9Aliases_MariaDb()
    {
        var content = ReadTemplate("MariaDb", "Repository_MigrationRun_SelectOrphaned.sql");

        var aliasPattern = new Regex(@"\bAS\s+`[A-Z][a-zA-Z]+`", RegexOptions.IgnoreCase);
        var count = aliasPattern.Matches(content).Count;

        count.Should().Be(9,
            "Repository_MigrationRun_SelectOrphaned.sql (MariaDb) must have exactly 9 AS `PascalCase` aliases " +
            "(MigrationRunId, EnvironmentId, StartedAt, MigrationRunModeId, MinutesRunning, LastHeartbeatAt, MinutesSinceHeartbeat, InitiatedBy, Host)");
    }

    // -------------------------------------------------------------------------
    // 4. Aggregate alias count (Strategy B contract — total across all 3 reader SELECTs)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Aggregated alias count (MySQL): all three reader SELECT templates together must supply at least 36
    /// aliases (21 + 10 + 5). A count below 36 means at least one reader column is missing an alias
    /// and the C# row["PascalCaseKey"] lookup would return null at runtime.
    /// </summary>
    [Fact]
    public void AllReaderSelects_TotalAliasCount_IsAtLeast36_MySql()
    {
        var readerTemplates = new[]
        {
            "Repository_MigrationRecord_Select.sql",
            "Repository_MigrationRecordHistory_Select.sql",
        "Repository_MigrationRecordHistory_Select.sql",
            "Repository_MigrationRun_Select.sql",
            "Repository_MigrationRun_SelectOrphaned.sql"
        };

        var aliasPattern = new Regex(@"\bAS\s+`[A-Z][a-zA-Z]+`", RegexOptions.IgnoreCase);
        int total = readerTemplates.Sum(t => aliasPattern.Matches(ReadTemplate("MySql", t)).Count);

        total.Should().BeGreaterThanOrEqualTo(36,
            "the three MySQL reader-output SELECT templates must collectively expose at least 36 PascalCase " +
            "AS `...` aliases to satisfy the cross-engine reader contract (DAL-018)");
    }

    /// <summary>
    /// Aggregated alias count (MariaDB): all three reader SELECT templates together must supply at least
    /// 36 aliases (21 + 10 + 5). Mirrors AllReaderSelects_TotalAliasCount_IsAtLeast36_MySql.
    /// </summary>
    [Fact]
    public void AllReaderSelects_TotalAliasCount_IsAtLeast36_MariaDb()
    {
        var readerTemplates = new[]
        {
            "Repository_MigrationRecord_Select.sql",
            "Repository_MigrationRecordHistory_Select.sql",
        "Repository_MigrationRecordHistory_Select.sql",
            "Repository_MigrationRun_Select.sql",
            "Repository_MigrationRun_SelectOrphaned.sql"
        };

        var aliasPattern = new Regex(@"\bAS\s+`[A-Z][a-zA-Z]+`", RegexOptions.IgnoreCase);
        int total = readerTemplates.Sum(t => aliasPattern.Matches(ReadTemplate("MariaDb", t)).Count);

        total.Should().BeGreaterThanOrEqualTo(36,
            "the three MariaDB reader-output SELECT templates must collectively expose at least 36 PascalCase " +
            "AS `...` aliases to satisfy the cross-engine reader contract (DAL-018)");
    }

    // -------------------------------------------------------------------------
    // Helpers (same pattern as P2_PostgreSqlIdentifierCasingTests)
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
