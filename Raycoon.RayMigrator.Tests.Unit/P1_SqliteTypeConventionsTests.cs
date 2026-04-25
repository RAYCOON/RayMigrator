
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: SQLite type-convention assertions covering DAL-020 (no AUTOINCREMENT keyword).
///
/// In SQLite, INTEGER PRIMARY KEY already creates a rowid alias (auto-incrementing).
/// The AUTOINCREMENT keyword adds an unnecessary overhead guarantee and must never appear
/// in RayMigrator SQLite templates. These tests prevent silent reintroduction.
/// </summary>
public class SqliteTypeConventionsTests
{
    #region DAL-020: No AUTOINCREMENT

    [Fact]
    public void Repository_CheckCreate_ContainsNoAutoincrement()
    {
        var content = ReadTemplate("Sqlite", "Repository_CheckCreate.sql");

        var pattern = new Regex(@"\bAUTOINCREMENT\b");
        pattern.IsMatch(content).Should().BeFalse(
            "Repository_CheckCreate.sql must not contain the AUTOINCREMENT keyword after DAL-020; " +
            "INTEGER PRIMARY KEY already provides rowid aliasing in SQLite without the overhead guarantee");
    }

    [Fact]
    public void DatabaseLogging_CheckCreate_ContainsNoAutoincrement()
    {
        var content = ReadTemplate("Sqlite", "DatabaseLogging_CheckCreate.sql");

        var pattern = new Regex(@"\bAUTOINCREMENT\b");
        pattern.IsMatch(content).Should().BeFalse(
            "DatabaseLogging_CheckCreate.sql must not contain the AUTOINCREMENT keyword after DAL-020; " +
            "INTEGER PRIMARY KEY already provides rowid aliasing in SQLite without the overhead guarantee");
    }

    [Fact]
    public void AllSqliteTemplates_ContainNoAutoincrement()
    {
        var templatesDir = GetTemplatesDir("Sqlite");
        var sqlFiles = Directory.GetFiles(templatesDir, "*.sql");

        sqlFiles.Should().NotBeEmpty("the SQLite Templates folder must contain SQL template files");

        var pattern = new Regex(@"\bAUTOINCREMENT\b");
        foreach (var file in sqlFiles)
        {
            var content = File.ReadAllText(file);
            pattern.IsMatch(content).Should().BeFalse(
                $"{Path.GetFileName(file)} must not contain the AUTOINCREMENT keyword (DAL-020 regression guard); " +
                "INTEGER PRIMARY KEY is the correct SQLite idiom");
        }
    }

    #endregion

    #region DAL-022: STRICT tables

    [Fact]
    public void Repository_CheckCreate_AllCreateTableStatementsAreStrict()
    {
        var content = ReadTemplate("Sqlite", "Repository_CheckCreate.sql");
        var sqlOnly = StripBlockComments(content);

        var createTableRegex = new Regex(
            @"CREATE\s+(?:TEMP\s+)?TABLE\b(?:\s+IF\s+NOT\s+EXISTS)?[^;]*?\)\s*(STRICT\s*)?;",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var matches = createTableRegex.Matches(sqlOnly);
        matches.Should().HaveCount(12,
            "Repository_CheckCreate.sql must contain exactly 12 CREATE TABLE / CREATE TEMP TABLE statements after DAL-022");

        foreach (Match m in matches)
            m.Groups[1].Success.Should().BeTrue(
                $"CREATE TABLE at position {m.Index} in Repository_CheckCreate.sql must be declared STRICT (DAL-022)");
    }

    [Fact]
    public void DatabaseLogging_CheckCreate_AllCreateTableStatementsAreStrict()
    {
        var content = ReadTemplate("Sqlite", "DatabaseLogging_CheckCreate.sql");
        var sqlOnly = StripBlockComments(content);

        var createTableRegex = new Regex(
            @"CREATE\s+(?:TEMP\s+)?TABLE\b(?:\s+IF\s+NOT\s+EXISTS)?[^;]*?\)\s*(STRICT\s*)?;",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var matches = createTableRegex.Matches(sqlOnly);
        matches.Should().HaveCount(3,
            "DatabaseLogging_CheckCreate.sql must contain exactly 3 CREATE TABLE / CREATE TEMP TABLE statements after DAL-022");

        foreach (Match m in matches)
            m.Groups[1].Success.Should().BeTrue(
                $"CREATE TABLE at position {m.Index} in DatabaseLogging_CheckCreate.sql must be declared STRICT (DAL-022)");
    }

    [Fact]
    public void AllSqliteTemplates_EveryPersistentCreateTableIsStrict()
    {
        var templatesDir = GetTemplatesDir("Sqlite");
        var sqlFiles = Directory.GetFiles(templatesDir, "*.sql");

        sqlFiles.Should().NotBeEmpty("the SQLite Templates folder must contain SQL template files");

        // Two-step approach: find all CREATE TABLE (any kind), then skip TEMP ones.
        // DML-helper temp tables (single-row state holders in non-CheckCreate templates)
        // are out of DAL-022 scope; only persistent tables must be STRICT.
        var anyCreateTableRegex = new Regex(
            @"CREATE\s+(TEMP\s+)?TABLE\b(?:\s+IF\s+NOT\s+EXISTS)?[^;]*?\)\s*(STRICT\s*)?;",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (var file in sqlFiles)
        {
            var content = File.ReadAllText(file);
            var sqlOnly = StripBlockComments(content);
            var matches = anyCreateTableRegex.Matches(sqlOnly);

            foreach (Match m in matches)
            {
                var isTemp = m.Groups[1].Success; // Group 1 captures "TEMP " when present
                if (isTemp) continue;             // DML-helper temp tables are out of DAL-022 scope

                var hasStrict = m.Groups[2].Success;
                hasStrict.Should().BeTrue(
                    $"CREATE TABLE at position {m.Index} in {Path.GetFileName(file)} must be declared STRICT (DAL-022 regression guard)");
            }
        }
    }

    #endregion

    #region DAL-021: ISO-8601 datetime CHECK constraints

    [Fact]
    public void Repository_CheckCreate_NotNullDatetimeColumns_HaveStrictCheck()
    {
        var content = ReadTemplate("Sqlite", "Repository_CheckCreate.sql");

        // Strict CHECK: datetime("X") IS NOT NULL AND datetime("X") = "X"
        // Expected >= 5 occurrences: MigratorMeta.CreatedAt, Product.CreatedAt,
        // Environment.CreatedAt, MigrationRun.StartedAt, MigrationRecordHistory.HistorizedAt.
        var strictPattern = new Regex(
            @"datetime\(""(?<col>[A-Za-z]+)""\)\s+IS\s+NOT\s+NULL\s+AND\s+datetime\(""\k<col>""\)\s*=\s*""\k<col>""",
            RegexOptions.IgnoreCase);

        strictPattern.Matches(content).Count.Should().BeGreaterThanOrEqualTo(5,
            "DAL-021: Repository_CheckCreate.sql must carry strict-ISO-8601 CHECK on every NOT NULL datetime column");
    }

    [Fact]
    public void Repository_CheckCreate_NullableDatetimeColumns_HaveNullTolerantCheck()
    {
        var content = ReadTemplate("Sqlite", "Repository_CheckCreate.sql");

        // Null-tolerant CHECK: "X" IS NULL OR (datetime("X") IS NOT NULL AND datetime("X") = "X")
        // Expected >= 5 occurrences: MigrationRun.FinishedAt, MigrationRecord.StartedAt/FinishedAt,
        // MigrationRecordHistory.StartedAt/FinishedAt.
        var nullTolerantPattern = new Regex(
            @"""(?<col>[A-Za-z]+)""\s+IS\s+NULL\s+OR\s+\(\s*datetime\(""\k<col>""\)\s+IS\s+NOT\s+NULL\s+AND\s+datetime\(""\k<col>""\)\s*=\s*""\k<col>""\s*\)",
            RegexOptions.IgnoreCase);

        nullTolerantPattern.Matches(content).Count.Should().BeGreaterThanOrEqualTo(5,
            "DAL-021: Repository_CheckCreate.sql must carry null-tolerant CHECK on every nullable datetime column");
    }

    [Fact]
    public void DatabaseLogging_CheckCreate_CreatedAt_HasStrictCheck()
    {
        var content = ReadTemplate("Sqlite", "DatabaseLogging_CheckCreate.sql");

        var pattern = new Regex(
            @"datetime\(""CreatedAt""\)\s+IS\s+NOT\s+NULL\s+AND\s+datetime\(""CreatedAt""\)\s*=\s*""CreatedAt""",
            RegexOptions.IgnoreCase);

        pattern.IsMatch(content).Should().BeTrue(
            "DAL-021: MigrationLog.CreatedAt in DatabaseLogging_CheckCreate.sql must carry a strict-ISO-8601 CHECK constraint");
    }

    [Fact]
    public void AllSqliteCheckCreateTemplates_DatetimeColumns_CarryCheck()
    {
        // Regression guard: every datetime column (TEXT column whose name ends in 'At') in the two
        // CheckCreate templates must carry a CHECK clause on the same column statement.
        string[] templates = ["Repository_CheckCreate.sql", "DatabaseLogging_CheckCreate.sql"];

        foreach (var templateName in templates)
        {
            var raw = ReadTemplate("Sqlite", templateName);
            var body = StripBlockComments(raw);

            var columnPattern = new Regex(
                @"""(?<col>[A-Za-z]+At)""\s+TEXT\b[^,\n]*",
                RegexOptions.IgnoreCase);

            foreach (Match m in columnPattern.Matches(body))
            {
                var col = m.Groups["col"].Value;
                m.Value.Should().Contain("CHECK (",
                    $"DAL-021 regression guard: column {col} in {templateName} must carry a CHECK constraint. Line: {m.Value}");
            }
        }
    }

    #endregion

    #region DAL-024: Multi-statement transaction comment

    private const string TransactionMarker = "Transaction requirement (DAL-024):";

    public static IEnumerable<object[]> MultiStatementTemplates => new[]
    {
        new object[] { "DatabaseLogging_CheckCreate.sql" },
        new object[] { "Repository_CheckCreate.sql" },
        new object[] { "Repository_Environment_CheckInsert.sql" },
        new object[] { "Repository_MigrationRecord_FixOrphaned.sql" },
        new object[] { "Repository_MigrationRecord_Insert.sql" },
        new object[] { "Repository_MigrationRecord_Update.sql" },
        new object[] { "Repository_MigrationRecord_UpdateHash.sql" },
        new object[] { "Repository_MigrationRecord_UpdateRollback.sql" },
        new object[] { "Repository_MigrationRun_FixOrphaned.sql" },
        new object[] { "Repository_MigrationRun_Insert.sql" },
        new object[] { "Repository_MigrationRun_Update.sql" },
        new object[] { "Repository_Product_CheckInsert.sql" },
    };

    public static IEnumerable<object[]> SingleStatementTemplates => new[]
    {
        new object[] { "DatabaseLogging_Insert.sql" },
        new object[] { "Repository_Drop.sql" },
        new object[] { "Repository_MigrationRecord_GetInterrupted.sql" },
        new object[] { "Repository_MigrationRecord_Select.sql" },
        new object[] { "Repository_MigrationRun_Select.sql" },
        new object[] { "Repository_MigrationRun_SelectOrphaned.sql" },
    };

    [Theory]
    [MemberData(nameof(MultiStatementTemplates))]
    public void MultiStatementTemplate_CarriesTransactionComment(string templateFile)
    {
        var content = ReadTemplate("Sqlite", templateFile);

        content.Should().Contain(TransactionMarker,
            $"'{templateFile}' is a multi-statement template and must carry the DAL-024 transaction requirement comment " +
            "to warn operators that manual sqlite3 CLI execution requires BEGIN TRANSACTION/COMMIT; " +
            "see .implement/03-templates.md for the classification rationale");
    }

    [Theory]
    [MemberData(nameof(SingleStatementTemplates))]
    public void SingleStatementTemplate_DoesNotCarryTransactionComment(string templateFile)
    {
        var content = ReadTemplate("Sqlite", templateFile);

        content.Should().NotContain(TransactionMarker,
            $"'{templateFile}' is a single-statement template and must NOT carry the DAL-024 transaction comment; " +
            "adding the comment to single-statement files produces noise and dilutes the signal value of the marker");
    }

    [Fact]
    public void AllSqliteTemplates_AreAccountedForInDal024Classification()
    {
        var templatesDir = GetTemplatesDir("Sqlite");
        var discovered = Directory.GetFiles(templatesDir, "*.sql")
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var classified = MultiStatementTemplates.Select(row => (string)row[0])
            .Concat(SingleStatementTemplates.Select(row => (string)row[0]))
            .ToList();

        discovered.Should().BeEquivalentTo(classified,
            "every SQLite template must be explicitly classified as multi-statement or single-statement in the DAL-024 test lists; " +
            "if a template was added or removed, update MultiStatementTemplates and SingleStatementTemplates in P1_SqliteTypeConventionsTests.cs");
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

    private static string StripBlockComments(string sql)
    {
        return new Regex(@"/\*[\s\S]*?\*/", RegexOptions.Singleline).Replace(sql, string.Empty);
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

    #endregion
}
