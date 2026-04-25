
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: PostgreSQL type-convention assertions covering DAL-012 (TIMESTAMPTZ, NOW() cleanup)
/// and DAL-013 (TEXT replaces arbitrary VARCHAR(n) in CREATE TABLE column declarations).
/// Also enforces the TOML Version / v_repository_version consistency invariant (Note4).
/// </summary>
public class PostgreSqlTypeConventionsTests
{
    #region DAL-012: TIMESTAMPTZ + plain NOW()

    [Fact]
    public void Repository_CheckCreate_AllAuditColumns_UseTimestampTz()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_CheckCreate.sql");

        // DAL-017: PostgreSQL repository columns use unquoted snake_case identifiers.
        foreach (var column in new[] { "created_at", "started_at", "finished_at", "historized_at" })
        {
            var pattern = new Regex(@"\b" + Regex.Escape(column) + @"\s+TIMESTAMPTZ\b");
            pattern.IsMatch(content).Should().BeTrue(
                $"column {column} in Repository_CheckCreate.sql must be declared as TIMESTAMPTZ after DAL-012");
        }
    }

    [Fact]
    public void Repository_CheckCreate_ContainsNoBareTimestamp()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_CheckCreate.sql");

        // Match TIMESTAMP followed by whitespace then (NOT)? NULL.
        // TIMESTAMPTZ does NOT match: \bTIMESTAMP\b requires a word boundary after the final P,
        // but in TIMESTAMPTZ the P is followed by T (another word character), so no \b exists there.
        // The \s+ after TIMESTAMP also excludes TIMESTAMPTZ because TIMESTAMPTZ has no whitespace
        // between the keyword and the next token.
        var pattern = new Regex(@"\bTIMESTAMP\s+(?:NOT\s+)?NULL\b");
        pattern.IsMatch(content).Should().BeFalse(
            "Repository_CheckCreate.sql must not contain bare TIMESTAMP column declarations after DAL-012");
    }

    [Fact]
    public void DatabaseLogging_CheckCreate_CreatedAt_IsTimestampTz()
    {
        var content = ReadTemplate("PostgreSQL", "DatabaseLogging_CheckCreate.sql");

        // DAL-017: PostgreSQL column uses unquoted snake_case identifier.
        var pattern = new Regex(@"\bcreated_at\s+TIMESTAMPTZ\b");
        pattern.IsMatch(content).Should().BeTrue(
            "DatabaseLogging_CheckCreate.sql created_at column must use TIMESTAMPTZ after DAL-012");
    }

    [Fact]
    public void AllPgTemplates_NoAtTimeZoneUtc()
    {
        var templatesDir = GetTemplatesDir("PostgreSQL");
        var sqlFiles = Directory.GetFiles(templatesDir, "*.sql");

        sqlFiles.Should().NotBeEmpty("the PostgreSQL Templates folder must contain SQL template files");

        foreach (var file in sqlFiles)
        {
            var content = File.ReadAllText(file);
            content.Should().NotContain("AT TIME ZONE 'UTC'",
                $"{Path.GetFileName(file)} must not reference AT TIME ZONE 'UTC' after DAL-012 (TIMESTAMPTZ columns need plain NOW())");
        }
    }

    [Fact]
    public void MigrationRun_Update_EpochExtract_UsesPlainNow()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_MigrationRun_Update.sql");

        // DAL-017: PostgreSQL columns use unquoted snake_case identifiers.
        content.Should().Contain("EXTRACT(EPOCH FROM (NOW() - started_at))",
            "Repository_MigrationRun_Update.sql must extract epoch from plain NOW() minus started_at (type-arithmetic correctness with TIMESTAMPTZ)");
    }

    [Fact]
    public void MigrationRun_FixOrphaned_EpochExtract_UsesPlainNow()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_MigrationRun_FixOrphaned.sql");

        content.Should().Contain("EXTRACT(EPOCH FROM (NOW() - started_at))",
            "Repository_MigrationRun_FixOrphaned.sql must extract epoch from plain NOW() minus started_at");
    }

    [Fact]
    public void MigrationRun_SelectOrphaned_EpochExtract_UsesPlainNow()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_MigrationRun_SelectOrphaned.sql");

        content.Should().Contain("EXTRACT(EPOCH FROM (NOW() - started_at))",
            "Repository_MigrationRun_SelectOrphaned.sql must extract epoch from plain NOW() minus started_at");
    }

    [Fact]
    public void Migration_Update_EpochExtract_UsesPlainNow()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_MigrationRecord_Update.sql");

        content.Should().Contain("EXTRACT(EPOCH FROM (NOW() - started_at))",
            "Repository_MigrationRecord_Update.sql must extract epoch from plain NOW() minus started_at");
    }

    [Fact]
    public void Migration_UpdateRollback_EpochExtract_UsesPlainNow()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_MigrationRecord_UpdateRollback.sql");

        content.Should().Contain("EXTRACT(EPOCH FROM (NOW() - started_at))",
            "Repository_MigrationRecord_UpdateRollback.sql must extract epoch from plain NOW() minus started_at");
    }

    #endregion

    #region DAL-013: TEXT replaces VARCHAR(n) in column declarations

    [Fact]
    public void Repository_CheckCreate_ColumnDeclarations_UseText()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_CheckCreate.sql");

        // DAL-017: PostgreSQL columns use unquoted snake_case identifiers.
        // Representative columns from various tables that were formerly VARCHAR(n).
        var sampleColumns = new[]
        {
            "name",
            "description",
            "name_lower",
            "repository_version",
            "repository_database_type",
            "created_by_raymigrator_version",
            "release_version",
            "target_group_alias",
            "target_alias",
            "filename",
            "file_up_hash",
            "file_down_hash",
            "from_release_version",
            "to_release_version"
        };

        foreach (var column in sampleColumns)
        {
            // Use word boundaries to avoid matching substrings (e.g., 'name' inside 'name_lower').
            var pattern = new Regex(@"\b" + Regex.Escape(column) + @"\b\s+TEXT\b");
            pattern.IsMatch(content).Should().BeTrue(
                $"column {column} in Repository_CheckCreate.sql must be declared as TEXT after DAL-013");
        }
    }

    [Fact]
    public void Repository_CheckCreate_NoVarcharInColumnDecls()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_CheckCreate.sql");
        var blocks = ExtractCreateTableColumnSections(content);

        blocks.Should().NotBeEmpty("Repository_CheckCreate.sql must contain at least one CREATE TABLE block");

        foreach (var block in blocks)
        {
            var match = Regex.Match(block.ColumnsText, @"VARCHAR\(\d+\)");
            match.Success.Should().BeFalse(
                $"CREATE TABLE block for {block.TableName} must not contain VARCHAR(n) column declarations after DAL-013 (match: {(match.Success ? match.Value : "")})");
        }
    }

    [Fact]
    public void DatabaseLogging_CheckCreate_NoVarcharInColumnDecls()
    {
        var content = ReadTemplate("PostgreSQL", "DatabaseLogging_CheckCreate.sql");
        var blocks = ExtractCreateTableColumnSections(content);

        blocks.Should().NotBeEmpty("DatabaseLogging_CheckCreate.sql must contain at least one CREATE TABLE block");

        foreach (var block in blocks)
        {
            var match = Regex.Match(block.ColumnsText, @"VARCHAR\(\d+\)");
            match.Success.Should().BeFalse(
                $"CREATE TABLE block for {block.TableName} must not contain VARCHAR(n) column declarations after DAL-013 (match: {(match.Success ? match.Value : "")})");
        }
    }

    #endregion

    #region Consistency invariants

    [Fact]
    public void Repository_CheckCreate_TomlVersionMatchesConstant()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_CheckCreate.sql");

        // Parse the TOML Version header, located between [RayMigratorTemplate] and the next section
        var tomlSectionMatch = Regex.Match(content,
            @"\[RayMigratorTemplate\](.*?)\[(?!RayMigratorTemplate)",
            RegexOptions.Singleline);
        tomlSectionMatch.Success.Should().BeTrue("Repository_CheckCreate.sql must have a [RayMigratorTemplate] TOML section");

        var tomlSection = tomlSectionMatch.Groups[1].Value;
        var versionMatch = Regex.Match(tomlSection, @"Version\s*=\s*""([^""]+)""");
        versionMatch.Success.Should().BeTrue("the [RayMigratorTemplate] section must declare a Version");
        var tomlVersion = versionMatch.Groups[1].Value;

        // Parse the v_repository_version PL/pgSQL constant
        var constantMatch = Regex.Match(content, @"v_repository_version\s+VARCHAR\(\d+\)\s*:=\s*'([^']+)'");
        constantMatch.Success.Should().BeTrue("Repository_CheckCreate.sql must declare a v_repository_version constant");
        var constantVersion = constantMatch.Groups[1].Value;

        constantVersion.Should().Be(tomlVersion,
            "v_repository_version PL/pgSQL constant must match the TOML Version header (Note4 invariant)");
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

    /// <summary>
    /// Extracts column-declaration sections from every CREATE TABLE block in the content.
    /// The returned text spans from the opening parenthesis up to (but excluding) the first
    /// CONSTRAINT / PRIMARY KEY token, so that PL/pgSQL code outside CREATE TABLE (variable
    /// decls, CAST expressions) is not scanned.
    /// </summary>
    private static List<(string TableName, string ColumnsText)> ExtractCreateTableColumnSections(string content)
    {
        var results = new List<(string, string)>();
        var createTableRegex = new Regex(
            @"CREATE\s+TABLE\s+(?<name>[""\w{}:.]+)\s*\((?<body>.*?)(?:CONSTRAINT\s+pk_|PRIMARY\s+KEY)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in createTableRegex.Matches(content))
        {
            var name = match.Groups["name"].Value;
            var body = match.Groups["body"].Value;
            results.Add((name, body));
        }

        return results;
    }

    #endregion
}
