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
/// P1: Structural SQL-template tests for the EnvironmentId FK feature.
/// Verifies that Environment text column is replaced with EnvironmentId INT FK across all 5 engines,
/// and that all INSERT/SELECT templates bind @EnvironmentId (not @Environment).
/// Parallel pattern to P1_EnvironmentCheckInsertTests.cs.
/// </summary>
public class EnvironmentIdFkTests
{
    #region Repository_CheckCreate — EnvironmentId INT column presence

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void CheckCreate_MigrationRun_ContainsEnvironmentIdColumn(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");
        var tableName = IsSnakeCaseEngine(engine) ? "migration_run" : "MigrationRun";
        var ddlBlock = ExtractTableDdl(content, tableName);

        ddlBlock.Should().NotBeEmpty(
            $"{engine} Repository_CheckCreate.sql must define a {tableName} table");

        var hasColumn = ContainsEnvironmentIdColumn(ddlBlock);
        hasColumn.Should().BeTrue(
            $"{engine} MigrationRun table DDL block must contain an EnvironmentId/environment_id INT column");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void CheckCreate_MigrationRecord_ContainsEnvironmentIdColumn(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");
        var tableName = IsSnakeCaseEngine(engine) ? "migration_record" : "MigrationRecord";
        var ddlBlock = ExtractTableDdl(content, tableName);

        ddlBlock.Should().NotBeEmpty(
            $"{engine} Repository_CheckCreate.sql must define a {tableName} table");

        var hasColumn = ContainsEnvironmentIdColumn(ddlBlock);
        hasColumn.Should().BeTrue(
            $"{engine} MigrationRecord table DDL block must contain an EnvironmentId/environment_id INT column");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void CheckCreate_MigrationRecordHistory_ContainsEnvironmentIdColumn(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");
        var tableName = IsSnakeCaseEngine(engine) ? "migration_record_history" : "MigrationRecordHistory";
        var ddlBlock = ExtractTableDdl(content, tableName);

        ddlBlock.Should().NotBeEmpty(
            $"{engine} Repository_CheckCreate.sql must define a {tableName} table");

        var hasColumn = ContainsEnvironmentIdColumn(ddlBlock);
        hasColumn.Should().BeTrue(
            $"{engine} MigrationRecordHistory table DDL block must contain an EnvironmentId/environment_id INT column");
    }

    #endregion

    #region Repository_CheckCreate — no standalone text Environment column

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Sqlite")]
    public void CheckCreate_PascalCaseEngines_NoTextEnvironmentColumn(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");

        // Matches: Environment NVARCHAR(...) or Environment nvarchar(...) as a column definition
        var hasTextEnvColumn = Regex.IsMatch(content, @"\bEnvironment\b\s+(NVARCHAR|nvarchar|VARCHAR|varchar|TEXT|text)\s*[\(\(]",
            RegexOptions.IgnoreCase);

        hasTextEnvColumn.Should().BeFalse(
            $"{engine} must NOT contain a standalone text Environment column — only EnvironmentId INT FK is allowed");
    }

    [Theory]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void CheckCreate_SnakeCaseEngines_NoTextEnvironmentColumn(string engine)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");

        // Matches: environment TEXT or environment varchar(...) as a column definition (not inside a table name or comment)
        // We look for the pattern within table DDL blocks, not in table names like "environment"
        // Specifically: the column definition pattern "environment    TEXT" or "environment    VARCHAR"
        var hasTextEnvColumn = Regex.IsMatch(content,
            @"^\s+environment\s+(TEXT|VARCHAR|text|varchar)\s*",
            RegexOptions.Multiline);

        hasTextEnvColumn.Should().BeFalse(
            $"{engine} must NOT contain a standalone text 'environment' column — only environment_id INT FK is allowed");
    }

    #endregion

    #region Repository_CheckCreate — FK constraints for Environment

    [Theory]
    [InlineData("SqlServer", "fk_MigrationRun_Environment")]
    [InlineData("SqlServer", "fk_MigrationRecord_Environment")]
    [InlineData("SqlServer", "fk_MigrationRecordHistory_Environment")]
    [InlineData("Sqlite", "fk_MigrationRun_Environment")]
    [InlineData("Sqlite", "fk_MigrationRecord_Environment")]
    [InlineData("Sqlite", "fk_MigrationRecordHistory_Environment")]
    public void CheckCreate_PascalCaseEngines_ContainsEnvironmentFkConstraints(string engine, string constraintName)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");

        content.Should().Contain(constraintName,
            $"{engine} Repository_CheckCreate.sql must contain FK constraint '{constraintName}'");
    }

    [Theory]
    [InlineData("PostgreSQL", "fk_migration_run_environment")]
    [InlineData("PostgreSQL", "fk_migration_record_environment")]
    [InlineData("PostgreSQL", "fk_migration_record_history_environment")]
    [InlineData("MariaDb", "fk_migration_run_environment")]
    [InlineData("MariaDb", "fk_migration_record_environment")]
    [InlineData("MariaDb", "fk_migration_record_history_environment")]
    [InlineData("MySql", "fk_migration_run_environment")]
    [InlineData("MySql", "fk_migration_record_environment")]
    [InlineData("MySql", "fk_migration_record_history_environment")]
    public void CheckCreate_SnakeCaseEngines_ContainsEnvironmentFkConstraints(string engine, string constraintName)
    {
        var content = ReadTemplate(engine, "Repository_CheckCreate.sql");

        content.Should().Contain(constraintName,
            $"{engine} Repository_CheckCreate.sql must contain FK constraint '{constraintName}'");
    }

    #endregion

    #region DatabaseLogging_CheckCreate — no FK for Environment

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void DatabaseLoggingCheckCreate_DoesNotContainEnvironmentFkConstraint(string engine)
    {
        var content = ReadTemplate(engine, "DatabaseLogging_CheckCreate.sql");

        // DatabaseLogging tables store environment_id as a plain nullable int, no FK per design decision
        var hasFkEnvironment = content.Contains("fk_MigrationLog_Environment") ||
                               content.Contains("fk_migration_log_environment");

        hasFkEnvironment.Should().BeFalse(
            $"{engine} DatabaseLogging_CheckCreate must NOT contain FK constraint for Environment (mirrors ProductId no-FK precedent)");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void DatabaseLoggingCheckCreate_ContainsEnvironmentIdColumn(string engine)
    {
        var content = ReadTemplate(engine, "DatabaseLogging_CheckCreate.sql");

        var hasColumn = content.Contains("EnvironmentId") || content.Contains("environment_id");
        hasColumn.Should().BeTrue($"{engine} MigrationLog table must contain EnvironmentId/environment_id column");
    }

    #endregion

    #region INSERT templates — bind @EnvironmentId (not @Environment)

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void MigrationRunInsert_BindsEnvironmentId(string engine)
    {
        var content = ReadTemplate(engine, "Repository_MigrationRun_Insert.sql");

        content.Should().Contain("@EnvironmentId",
            $"{engine} Repository_MigrationRun_Insert.sql must bind @EnvironmentId parameter");
        content.Should().NotContain("@Environment,",
            $"{engine} Repository_MigrationRun_Insert.sql must NOT bind a text @Environment parameter");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void MigrationInsert_BindsEnvironmentId(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Migration_Insert.sql");

        content.Should().Contain("@EnvironmentId",
            $"{engine} Repository_Migration_Insert.sql must bind @EnvironmentId parameter");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void MigrationUpdate_BindsEnvironmentIdInHistoryCopy(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Migration_Update.sql");

        // History copy INSERT must carry EnvironmentId — scope the check to the INSERT INTO ... history block
        var historyBlock = ExtractHistoryInsertBlock(engine, content);
        historyBlock.Should().NotBeEmpty(
            $"{engine} Repository_Migration_Update.sql must contain an INSERT INTO MigrationRecordHistory block");

        var hasEnvironmentId = ContainsEnvironmentIdIdentifier(historyBlock);
        hasEnvironmentId.Should().BeTrue(
            $"{engine} Repository_Migration_Update.sql history INSERT block must include EnvironmentId/environment_id column");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void MigrationUpdateRollback_BindsEnvironmentIdInHistoryCopy(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Migration_UpdateRollback.sql");

        var historyBlock = ExtractHistoryInsertBlock(engine, content);
        historyBlock.Should().NotBeEmpty(
            $"{engine} Repository_Migration_UpdateRollback.sql must contain an INSERT INTO MigrationRecordHistory block");

        var hasEnvironmentId = ContainsEnvironmentIdIdentifier(historyBlock);
        hasEnvironmentId.Should().BeTrue(
            $"{engine} Repository_Migration_UpdateRollback.sql history INSERT block must include EnvironmentId/environment_id column");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void DatabaseLoggingInsert_BindsEnvironmentId(string engine)
    {
        var content = ReadTemplate(engine, "DatabaseLogging_Insert.sql");

        content.Should().Contain("@EnvironmentId",
            $"{engine} DatabaseLogging_Insert.sql must bind @EnvironmentId parameter");
    }

    #endregion

    #region SELECT templates — filter on EnvironmentId

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void MigrationSelect_FiltersOnEnvironmentId(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Migration_Select.sql");

        content.Should().Contain("@EnvironmentId",
            $"{engine} Repository_Migration_Select.sql must filter on @EnvironmentId parameter");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void MigrationGetInterrupted_FiltersOnEnvironmentId(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Migration_GetInterrupted.sql");

        content.Should().Contain("@EnvironmentId",
            $"{engine} Repository_Migration_GetInterrupted.sql must filter on @EnvironmentId parameter");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void MigrationRunSelectOrphaned_FiltersOnEnvironmentId(string engine)
    {
        var content = ReadTemplate(engine, "Repository_MigrationRun_SelectOrphaned.sql");

        content.Should().Contain("@EnvironmentId",
            $"{engine} Repository_MigrationRun_SelectOrphaned.sql must filter on @EnvironmentId parameter");
    }

    #endregion

    #region MigrationGetInterrupted — EnvironmentId pipe position is int

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void MigrationGetInterrupted_PipePositionSix_IsEnvironmentId(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Migration_GetInterrupted.sql");

        // The pipe-separated result format must include EnvironmentId at position 6 (0-based).
        // Format: MigrationId|MigrationRunId|ReleaseVersion|Filename|FileUpBlocksMigrated|FileUpBlocksTotal|EnvironmentId|TargetGroupAlias|TargetAlias
        //
        // Each engine concatenates the 9 output fields differently. We verify that the 7th concatenated
        // output token (0-based index 6) is the EnvironmentId field — not a text environment column or
        // any other placeholder.
        var sixthToken = ExtractPipePositionSixOutputToken(engine, content);
        sixthToken.Should().NotBeEmpty(
            $"{engine} Repository_Migration_GetInterrupted.sql must produce a parseable pipe-separated result string");

        var matchesEnvironmentId = Regex.IsMatch(
            sixthToken,
            @"(?i)\b(?:v_found_environment_id|FoundEnvironmentId|EnvironmentId|environment_id)\b");
        matchesEnvironmentId.Should().BeTrue(
            $"{engine} Repository_Migration_GetInterrupted.sql must emit EnvironmentId at pipe position 6 " +
            $"(found token: \"{sixthToken}\")");
    }

    #endregion

    #region Repository_Environment_CheckInsert — still uses @Name + @NameLower (guardrail)

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void EnvironmentCheckInsert_StillBindsNameAndNameLower(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("@Name",
            $"{engine} Repository_Environment_CheckInsert.sql must still use @Name (unchanged)");
        content.Should().Contain("@NameLower",
            $"{engine} Repository_Environment_CheckInsert.sql must still use @NameLower (unchanged)");
    }

    #endregion

    #region Helpers

    private static bool IsSnakeCaseEngine(string engine) =>
        engine is "PostgreSQL" or "MariaDb" or "MySql";

    /// <summary>
    /// Returns true if the given DDL text contains an EnvironmentId / environment_id column
    /// definition (as opposed to merely mentioning the name in a FK clause or INSERT list).
    /// Matches e.g. "EnvironmentId int NOT NULL" or "environment_id INT NOT NULL" or the
    /// Sqlite STRICT form "\"EnvironmentId\" INTEGER NOT NULL".
    /// </summary>
    private static bool ContainsEnvironmentIdColumn(string ddl)
    {
        // Matches: (optional quote/backtick/bracket) EnvironmentId|environment_id (optional quote/backtick/bracket)
        // followed by whitespace and an int-ish type keyword.
        return Regex.IsMatch(
            ddl,
            @"[""`\[]?\b(?:EnvironmentId|environment_id)\b[""`\]]?\s+(?:INT|INTEGER|BIGINT|SMALLINT|TINYINT)\b",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Returns true if the given text references EnvironmentId/environment_id as a bare identifier
    /// (e.g., in an INSERT column list or SELECT list). Used by history-copy tests where the
    /// column is not accompanied by a type keyword.
    /// </summary>
    private static bool ContainsEnvironmentIdIdentifier(string sql)
    {
        return Regex.IsMatch(sql, @"\b(?:EnvironmentId|environment_id)\b", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Extracts the DDL text for a single CREATE TABLE statement matching the given table name.
    /// Returns from the "CREATE" keyword through the terminating semicolon, tracking parenthesis
    /// depth so that trailing clauses like "ENGINE=InnoDB ... COLLATE=..." or "STRICT" are
    /// included. Works across SqlServer (brackets), PostgreSQL/MariaDb/MySql (unquoted), and
    /// Sqlite (double-quoted STRICT form).
    /// </summary>
    private static string ExtractTableDdl(string sql, string tableName)
    {
        // The table identifier in our templates takes one of these forms:
        //   [{CFG:SchemaName}].[{CFG:TableBaseName}MigrationRun]  (SqlServer, brackets)
        //   {CFG:SchemaName}.{CFG:TableBaseName}migration_run     (PostgreSQL, unquoted)
        //   {CFG:TableBaseName}migration_run                      (MariaDb/MySql, no schema)
        //   "{CFG:TableBaseName}MigrationRun"                     (Sqlite, double quotes)
        var headerPattern =
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?" +
            @"[""`\[]?(?:\{CFG:SchemaName\}[""`\]]?\.[""`\[]?)?" +
            @"(?:\{CFG:TableBaseName\})?" +
            Regex.Escape(tableName) +
            @"[""`\]]?\s*\(";

        var headerMatch = Regex.Match(sql, headerPattern, RegexOptions.IgnoreCase);
        if (!headerMatch.Success) return string.Empty;

        // Walk forward from the opening "(" and find the matching ")", then the terminating ";".
        int start = headerMatch.Index;
        int openParenIndex = headerMatch.Index + headerMatch.Length - 1;
        int depth = 1;
        for (int i = openParenIndex + 1; i < sql.Length; i++)
        {
            char c = sql[i];
            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    // Find the terminating semicolon after this ")" (consume any trailing clauses).
                    int semi = sql.IndexOf(';', i);
                    if (semi < 0) return string.Empty;
                    return sql.Substring(start, semi - start + 1);
                }
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Extracts the "INSERT INTO ...MigrationRecordHistory ( col-list ) SELECT col-list FROM
    /// ...MigrationRecord ... ;" block. Used by the history-copy tests to avoid matching
    /// references to EnvironmentId elsewhere in the file.
    /// </summary>
    private static string ExtractHistoryInsertBlock(string engine, string sql)
    {
        var historyTable = IsSnakeCaseEngine(engine) ? "migration_record_history" : "MigrationRecordHistory";
        var headerPattern =
            @"INSERT\s+INTO\s+" +
            @"[""`\[]?(?:\{CFG:SchemaName\}[""`\]]?\.[""`\[]?)?" +
            @"(?:\{CFG:TableBaseName\})?" +
            Regex.Escape(historyTable) +
            @"[""`\]]?";

        var headerMatch = Regex.Match(sql, headerPattern, RegexOptions.IgnoreCase);
        if (!headerMatch.Success) return string.Empty;

        // Consume through the next ";" after the header.
        int start = headerMatch.Index;
        int semi = sql.IndexOf(';', start);
        if (semi < 0) return string.Empty;
        return sql.Substring(start, semi - start + 1);
    }

    /// <summary>
    /// Returns the 7th (0-based index 6) output token that the GetInterrupted template emits
    /// in its pipe-separated result string. Token names vary by engine (SqlServer uses
    /// <c>@FoundEnvironmentId</c>, PostgreSQL uses <c>v_found_environment_id</c>, MariaDb/MySql
    /// use <c>@v_found_environment_id</c>, Sqlite uses <c>m."EnvironmentId"</c>).
    /// </summary>
    private static string ExtractPipePositionSixOutputToken(string engine, string sql)
    {
        return engine switch
        {
            // SqlServer: CAST(@X AS VARCHAR(10)) + '|' + ... — the seventh CAST(@X ...) token is position 6.
            "SqlServer" => ExtractNthCastToken(sql, 6, @"CAST\s*\(\s*@(\w+)\s+AS"),
            // PostgreSQL: RAISE NOTICE '%,%|%|%|%|%|%|%|%|%', v_a, v_b, ..., v_i;
            // The 10 params correspond to: v_mig_id(code), v_mig_id, v_run_id, version, filename, blocks_mig, blocks_total, env_id, tg_alias, target_alias.
            // Position 6 in the pipe-separated message is the 8th RAISE-NOTICE parameter (index 7 after the leading code arg).
            "PostgreSQL" => ExtractPostgresRaiseNoticeToken(sql, 7),
            // MariaDb / MySql: CONCAT(CAST(@v_X AS CHAR), ...) — extract the 7th CAST token.
            "MariaDb" or "MySql" => ExtractNthCastToken(sql, 6, @"CAST\s*\(\s*@(\w+)\s+AS"),
            // Sqlite uses a mixed CAST(m."FIELD" AS TEXT) and IFNULL(m."FIELD", '') pipeline.
            // Extract the Nth m."FIELD" reference instead (first reference is the "code" token).
            "Sqlite" => ExtractNthSqliteColumnToken(sql, 6),
            _ => string.Empty
        };
    }

    private static string ExtractNthCastToken(string sql, int zeroBasedIndex, string tokenPattern)
    {
        var matches = Regex.Matches(sql, tokenPattern);
        // The first CAST token is the "code" part (before the comma); pipe fields start from the second.
        // Pipe position N (0-based) => matches[N + 1].
        var matchIndex = zeroBasedIndex + 1;
        if (matches.Count <= matchIndex) return string.Empty;
        return matches[matchIndex].Groups[1].Value;
    }

    private static string ExtractNthSqliteColumnToken(string sql, int zeroBasedIndex)
    {
        // Sqlite wraps either CAST(m."Id" AS TEXT) or IFNULL(m."Filename", '') around each field.
        // The column references appear in the SELECT list exactly once per pipe field.
        // The first reference is the "code" (m."Id"); pipe fields start from the second.
        var matches = Regex.Matches(sql, @"m\.""(\w+)""");
        var matchIndex = zeroBasedIndex + 1;
        if (matches.Count <= matchIndex) return string.Empty;
        return matches[matchIndex].Groups[1].Value;
    }

    private static string ExtractPostgresRaiseNoticeToken(string sql, int zeroBasedArgIndex)
    {
        // Find the second RAISE NOTICE (the positive-result one).
        // Format: RAISE NOTICE '%,%|%|%|%|%|%|%|%|%', arg0, arg1, arg2, ..., arg9;
        // arg0 is the code (v_migration_id); pipe fields start at arg1.
        // pipe position N (0-based) => arg(N + 1).
        var matches = Regex.Matches(
            sql,
            @"RAISE\s+NOTICE\s+'[^']+',\s*((?:[^;]+))\s*;",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (matches.Count < 2) return string.Empty;

        var argList = matches[1].Groups[1].Value;
        // Split on top-level commas (our args are simple identifiers or COALESCE(x,'')).
        var args = SplitTopLevelCommas(argList);
        var targetIndex = zeroBasedArgIndex;
        if (args.Count <= targetIndex) return string.Empty;
        return args[targetIndex].Trim();
    }

    private static List<string> SplitTopLevelCommas(string input)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(input.Substring(start, i - start));
                start = i + 1;
            }
        }
        if (start < input.Length) result.Add(input.Substring(start));
        return result;
    }

    private static string ReadTemplate(string engine, string templateFile)
    {
        var path = GetTemplatePath(engine, templateFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Template file not found for engine '{engine}': {path}");
        return File.ReadAllText(path);
    }

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
