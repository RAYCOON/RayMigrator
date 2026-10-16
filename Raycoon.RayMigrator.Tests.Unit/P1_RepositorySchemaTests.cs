using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Logging;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Shared.Constants;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The repository schema version contract (ADR-024): <see cref="RepositorySchema.CurrentVersion"/> matches the
/// <c>Repository_Upgrade_{n}</c> members of <see cref="TemplateType"/>, every engine ships the templates of the
/// versioned repository, the new log events exist and are seeded, the log template indexes the log, and the table
/// count codes of the former <c>Repository_CheckCreate</c> are gone from the catalog.
/// </summary>
public class RepositorySchemaTests
{
    public static readonly TheoryData<string> Engines = new() { "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite" };

    [Fact]
    public void CurrentVersion_IsAtLeastOne()
    {
        RepositorySchema.CurrentVersion.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void UpgradeTemplate_EveryVersionUpToCurrent_IsATemplateType()
    {
        for (int version = 1; version <= RepositorySchema.CurrentVersion; version++)
        {
            var templateType = RepositorySchema.UpgradeTemplate(version);

            templateType.ToString().Should().Be($"Repository_Upgrade_{version}");
            Enum.IsDefined(templateType).Should().BeTrue();
        }

        Enum.TryParse<TemplateType>($"Repository_Upgrade_{RepositorySchema.CurrentVersion + 1}", out _)
            .Should().BeFalse(because: "a step beyond CurrentVersion must not exist, or CurrentVersion is stale");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RepositorySchema.CurrentVersion + 1)]
    public void UpgradeTemplate_OutOfRange_Throws(int version)
    {
        var act = () => RepositorySchema.UpgradeTemplate(version);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TemplateType_ContainsMigratorMetaCheckInsert()
    {
        Enum.IsDefined(TemplateType.Repository_MigratorMeta_CheckInsert).Should().BeTrue();
    }

    [Fact]
    public void MigrationEvent_RepositoryUpgradeAndMigratorMetaCheckInsert_HaveIds101And102()
    {
        MigrationEvent.TemplateExecutionRepositoryUpgrade.Id.Should().Be(101);
        MigrationEvent.TemplateExecutionRepositoryUpgrade.Name.Should().Be("TemplateExecutionRepositoryUpgrade");
        MigrationEvent.TemplateExecutionRepositoryMigratorMetaCheckInsert.Id.Should().Be(102);
        MigrationEvent.TemplateExecutionRepositoryMigratorMetaCheckInsert.Name.Should().Be("TemplateExecutionRepositoryMigratorMetaCheckInsert");
    }

    [Fact]
    public void TemplateResultCode_TableCountCodes_AreNoLongerKnown()
    {
        TemplateResultCode.IsKnown(-10).Should().BeFalse(because: "the table count check of the former Repository_CheckCreate is gone");
        TemplateResultCode.IsKnown(-11).Should().BeFalse();
        TemplateResultCode.IsKnown(TemplateResultCode.RepositoryMultipleVersionEntries).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Templates_EveryEngine_ShipsCheckCreateEveryStepAndMigratorMetaCheckInsert(string engine)
    {
        File.Exists(GetTemplatePath(engine, "Repository_CheckCreate.sql")).Should().BeTrue();
        File.Exists(GetTemplatePath(engine, "Repository_MigratorMeta_CheckInsert.sql")).Should().BeTrue();
        for (int version = 1; version <= RepositorySchema.CurrentVersion; version++)
            File.Exists(GetTemplatePath(engine, $"Repository_Upgrade_{version}.sql")).Should().BeTrue($"engine {engine} must ship step {version}");
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Templates_EveryEngine_DeclareTheirTypeAndParameters(string engine)
    {
        ReadTemplate(engine, "Repository_CheckCreate.sql").Should().Contain("TemplateType   = \"Repository_CheckCreate\"")
            .And.NotContain("@RayMigratorVersion", because: "the check takes no parameters");

        var meta = ReadTemplate(engine, "Repository_MigratorMeta_CheckInsert.sql");
        meta.Should().Contain("TemplateType   = \"Repository_MigratorMeta_CheckInsert\"").And.Contain("@RayMigratorVersion").And.Contain("@RepositoryDatabaseType").And.Contain("-12,");

        for (int version = 1; version <= RepositorySchema.CurrentVersion; version++)
        {
            var step = ReadTemplate(engine, $"Repository_Upgrade_{version}.sql");
            step.Should().Contain($"TemplateType   = \"Repository_Upgrade_{version}\"").And.Contain("@RayMigratorVersion");
        }
    }

    [Theory]
    [InlineData("SqlServer", "ix_{CFG:TableBaseName}MigrationRun_Product_Environment_StartedAt", "ix_{CFG:TableBaseName}MigrationRecord_Product_Environment")]
    [InlineData("Sqlite", "ix_{CFG:TableBaseName}MigrationRun_Product_Environment_StartedAt", "ix_{CFG:TableBaseName}MigrationRecord_Product_Environment")]
    [InlineData("PostgreSQL", "ix_{CFG:TableBaseName}migration_run_product_environment_started_at", "ix_{CFG:TableBaseName}migration_record_product_environment")]
    [InlineData("MySql", "ix_{CFG:TableBaseName}migration_run_product_environment_started_at", "ix_{CFG:TableBaseName}migration_record_product_environment")]
    [InlineData("MariaDb", "ix_{CFG:TableBaseName}migration_run_product_environment_started_at", "ix_{CFG:TableBaseName}migration_record_product_environment")]
    public void StepThree_EveryEngine_AddsCancelledAndTheReaderIndexes(string engine, string runIndex, string recordIndex)
    {
        // Act
        var step = ReadTemplate(engine, "Repository_Upgrade_3.sql");

        // Assert
        step.Should().Contain("(70, 'Cancelled', 'Migration(s) stopped on request before completion')",
            because: "the run result Cancelled needs its lookup row before a run can record it");
        step.Should().Contain(runIndex).And.Contain(recordIndex);
        step.Should().Contain("3,RayMigrator repository schema version [3] applied", because: "the step reports its number");
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void DatabaseLoggingCheckCreate_EveryEngine_SeedsTheNewEvents(string engine)
    {
        var template = ReadTemplate(engine, "DatabaseLogging_CheckCreate.sql");

        template.Should().Contain("101, 'TemplateExecutionRepositoryUpgrade'");
        template.Should().Contain("102, 'TemplateExecutionRepositoryMigratorMetaCheckInsert'");
    }

    /// <summary>
    /// The log template indexes MigrationLog by run and by time and looks each index up under its own name before it
    /// creates it, so that a start that finds both runs no DDL: PostgreSQL demands ownership of the table and MariaDB
    /// the INDEX privilege for CREATE INDEX IF NOT EXISTS even when the index exists, MySQL has no IF NOT EXISTS, and
    /// after a lookup under another name every start would try to create the index again. SQLite has no privileges and
    /// creates with IF NOT EXISTS.
    /// </summary>
    [Theory]
    [InlineData("SqlServer", "ix_{CFG:TableBaseName}MigrationLog_MigrationRunId", "ix_{CFG:TableBaseName}MigrationLog_CreatedAt", "WHERE name = N'{0}'")]
    [InlineData("Sqlite", "ix_{CFG:TableBaseName}MigrationLog_MigrationRunId", "ix_{CFG:TableBaseName}MigrationLog_CreatedAt", "CREATE INDEX IF NOT EXISTS \"{0}\"")]
    [InlineData("PostgreSQL", "ix_{CFG:TableBaseName}migration_log_migration_run_id", "ix_{CFG:TableBaseName}migration_log_created_at", "indexname = '{0}'")]
    [InlineData("MySql", "ix_{CFG:TableBaseName}migration_log_migration_run_id", "ix_{CFG:TableBaseName}migration_log_created_at", "INDEX_NAME = '{0}'")]
    [InlineData("MariaDb", "ix_{CFG:TableBaseName}migration_log_migration_run_id", "ix_{CFG:TableBaseName}migration_log_created_at", "INDEX_NAME = '{0}'")]
    public void DatabaseLoggingCheckCreate_EveryEngine_CreatesTheTwoLogIndexesOnlyWhenMissing(string engine, string runIdIndex, string createdAtIndex, string lookup)
    {
        // Arrange: the SQL without the TOML header and the line comments
        string sql = Regex.Replace(ReadTemplate(engine, "DatabaseLogging_CheckCreate.sql"), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"--[^\r\n]*", string.Empty);

        // Act
        var created = Regex.Matches(sql, @"CREATE\s+INDEX\s+(?:IF\s+NOT\s+EXISTS\s+)?""?(?<name>ix_\{CFG:TableBaseName\}\w+)")
            .Select(m => m.Groups["name"].Value)
            .ToList();

        // Assert
        created.Should().BeEquivalentTo(new[] { runIdIndex, createdAtIndex }, because: "the readers of the log filter by run and its cleanup by age");
        foreach (string index in created)
            sql.Should().Contain(string.Format(CultureInfo.InvariantCulture, lookup, index), because: $"{index} must be looked up under its own name before it is created");
    }

    #region Helpers

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
