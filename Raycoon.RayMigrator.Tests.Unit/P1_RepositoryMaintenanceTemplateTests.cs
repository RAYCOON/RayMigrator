using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the retention templates of ADR-027. Every <see cref="TemplateType"/> member of the retention has a file in every
/// DAL, the Example skeleton included, that declares its type and parameters, spares the rows of an open run and limits
/// the delete batch in the dialect of its engine; the template cache loads them with the placeholders of the repository.
/// The executor formats the cutoff invariantly to the whole second and reads a row count beyond the int range of the
/// result contract.
/// </summary>
public class RepositoryMaintenanceTemplateTests
{
    private static readonly string[] Engines = ["SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite"];

    private static readonly TemplateType[] RetentionTemplates =
    [
        TemplateType.Repository_MigrationRecordHistory_Delete,
        TemplateType.Repository_MigrationRecordHistory_Count,
        TemplateType.DatabaseLogging_Delete,
        TemplateType.DatabaseLogging_Count
    ];

    public static TheoryData<string> AllDals => new() { "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite", "Example" };

    public static TheoryData<string, TemplateType> EngineTemplates
    {
        get
        {
            var data = new TheoryData<string, TemplateType>();
            foreach (string engine in Engines)
                foreach (var templateType in RetentionTemplates)
                    data.Add(engine, templateType);
            return data;
        }
    }

    [Fact]
    public void TemplateType_HasTheFourRetentionMembers()
    {
        // Act
        var names = RetentionTemplates.Select(t => t.ToString());

        // Assert
        RetentionTemplates.Should().OnlyContain(t => Enum.IsDefined(t));
        names.Should().Equal(
            "Repository_MigrationRecordHistory_Delete", "Repository_MigrationRecordHistory_Count", "DatabaseLogging_Delete", "DatabaseLogging_Count");
    }

    [Theory]
    [MemberData(nameof(AllDals))]
    public void EveryDal_ShipsAFilePerRetentionTemplate(string dal)
    {
        foreach (var templateType in RetentionTemplates)
            File.Exists(TemplatePath(dal, templateType)).Should().BeTrue(because: $"the TemplateCache refuses DAL [{dal}] without {templateType}.sql");
    }

    [Fact]
    public void DatabaseExample_ShipsNoArchiveTemplate()
    {
        File.Exists(Path.Combine(AppContext.BaseDirectory, "DataAccessLayers", "Example", "Repository_MigrationRecordHistory_Archive.sql")).Should().BeFalse(
            because: "the archive placeholder had no TemplateType: the history is written by the update templates and cleaned by the retention templates");
    }

    [Theory]
    [MemberData(nameof(EngineTemplates))]
    public void RetentionTemplate_DeclaresItsTypeAndParameters(string engine, TemplateType templateType)
    {
        // Act
        string template = File.ReadAllText(TemplatePath(engine, templateType));
        string sql = Body(template);

        // Assert
        template.Should().Contain($"TemplateType   = \"{templateType}\"").And.Contain($"DatabaseType   = \"{engine}\"");
        sql.Should().Contain("@CutoffUtc", because: "every retention template filters by the cutoff");
        if (templateType.ToString().EndsWith("_Delete", StringComparison.Ordinal))
            sql.Should().Contain("@BatchSize", because: "a delete template removes one batch");
        else
            sql.Should().NotContain("@BatchSize", because: "a count template counts every matching row");
    }

    [Theory]
    [MemberData(nameof(EngineTemplates))]
    public void RetentionTemplate_SparesTheRowsOfOpenRuns(string engine, TemplateType templateType)
    {
        // Act
        string sql = Body(File.ReadAllText(TemplatePath(engine, templateType)));

        // Assert
        string guard = engine switch
        {
            "SqlServer" => "r.[FinishedAt] IS NULL",
            "Sqlite" => "r.\"FinishedAt\" IS NULL",
            _ => "r.finished_at IS NULL"
        };
        Regex.IsMatch(sql, @"NOT EXISTS\s*\(", RegexOptions.IgnoreCase).Should().BeTrue(because: "a row is spared while its run is open");
        sql.Should().Contain(guard, because: $"{engine}/{templateType} must test MigrationRun.FinishedAt IS NULL");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MySql")]
    [InlineData("MariaDb")]
    [InlineData("Sqlite")]
    public void LogCount_AnswersTheExistenceOfTheLogWithoutCounting(string engine)
    {
        // Act
        string template = File.ReadAllText(TemplatePath(engine, TemplateType.DatabaseLogging_Count));
        string parameters = template[template.IndexOf("[Parameters]", StringComparison.Ordinal)..template.IndexOf("[ReturnValues]", StringComparison.Ordinal)];

        // Assert
        parameters.Should().Contain("ExistsOnly", because: "the cleanup asks whether the log table exists before it deletes, which needs no count of a large log");
        Body(template).Should().Contain("@ExistsOnly");
    }

    [Theory]
    [InlineData("SqlServer", "TOP (@BatchSize)")]
    [InlineData("PostgreSQL", "LIMIT @BatchSize")]
    [InlineData("MySql", "LIMIT @BatchSize")]
    [InlineData("MariaDb", "LIMIT @BatchSize")]
    [InlineData("Sqlite", "LIMIT @BatchSize")]
    public void DeleteTemplates_LimitTheBatchInTheDialectOfTheEngine(string engine, string limit)
    {
        foreach (var templateType in new[] { TemplateType.Repository_MigrationRecordHistory_Delete, TemplateType.DatabaseLogging_Delete })
        {
            // Act
            string sql = Body(File.ReadAllText(TemplatePath(engine, templateType)));

            // Assert
            sql.Should().Contain(limit);
            if (engine == "Sqlite")
                sql.Should().Contain("WHERE rowid IN (", because: "DELETE ... LIMIT needs SQLITE_ENABLE_UPDATE_DELETE_LIMIT");
            if (engine == "PostgreSQL")
                sql.Should().Contain("WHERE id IN (SELECT id FROM batch)", because: "PostgreSQL has no DELETE ... LIMIT");
            Regex.Matches(sql, @"\bDELETE\b", RegexOptions.IgnoreCase).Should().ContainSingle(because: "a batch is one statement, which a cancel rolls back whole");
        }
    }

    [Fact]
    public void TemplateCache_LoadsTheRetentionTemplatesWithThePlaceholdersOfTheRepository()
    {
        // Arrange
        var cache = new TemplateCache(false, NullLogger<TemplateCache>.Instance);

        foreach (string engine in Engines)
        {
            var repository = new RepositoryOptions { DatabaseType = engine, SchemaName = "ray", TableBaseName = "rm_" };
            foreach (var templateType in RetentionTemplates)
            {
                // Act
                var template = cache.GetRepositoryTemplate(templateType, repository);

                // Assert
                template.Content.Should().NotContain("{CFG:", because: $"{engine}/{templateType} uses only the placeholders of the repository");
                template.Content.Should().Contain("rm_", because: "the tables carry the prefix of the repository");
            }
        }
    }

    [Theory]
    [InlineData("th-TH")] // the Thai Buddhist calendar would write the year 2569 and delete every row
    [InlineData("fi-FI")] // the time separator is a dot
    public void FormatCutoff_CutsToTheWholeSecondInvariantCulture(string cultureName)
    {
        // Arrange
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(cultureName);
        try
        {
            // Act
            string text = RepositoryMaintenanceExecutor.FormatCutoff(new DateTime(2026, 10, 16, 4, 17, 32, 999, DateTimeKind.Utc));

            // Assert
            text.Should().Be("2026-10-16 04:17:32",
                because: "the cutoff counts to the whole second in the Gregorian calendar and with colons, whatever the culture of the host");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void FormatCutoff_TimeThatIsNotUtc_Throws()
    {
        var act = () => RepositoryMaintenanceExecutor.FormatCutoff(new DateTime(2026, 10, 16, 4, 17, 32, DateTimeKind.Local));

        act.Should().Throw<ArgumentException>().WithMessage("*DateTimeKind.Utc*");
    }

    [Fact]
    public void ParseCount_CountBeyondTheIntRange_IsReadAsALong()
    {
        // Act
        var (rows, codes) = RepositoryMaintenanceExecutor.ParseCount("3000000000,1,[3000000000] MigrationLog rows can be deleted", Template(TemplateType.DatabaseLogging_Count));

        // Assert
        rows.Should().Be(3_000_000_000L);
        codes.Should().Equal([int.MaxValue, 1], because: "the further codes keep the indexes of the result contract");
    }

    [Fact]
    public void ParseCount_NegativeCode_RaisesTheErrorOfTheContract()
    {
        var act = () => RepositoryMaintenanceExecutor.ParseCount("-1,the count failed", Template(TemplateType.Repository_MigrationRecordHistory_Count));

        act.Should().Throw<TemplateResultException>().Which.ResultCode.Should().Be(-1);
    }

    [Fact]
    public void ParseCount_ResultWithoutCount_Throws()
    {
        var act = () => RepositoryMaintenanceExecutor.ParseCount("no count here", Template(TemplateType.Repository_MigrationRecordHistory_Count));

        act.Should().Throw<TemplateResultException>();
    }

    private static Template Template(TemplateType templateType) => new() { TemplateType = templateType, DatabaseType = "Sqlite", Filename = $"{templateType}.sql" };

    private static string TemplatePath(string dal, TemplateType templateType) =>
        Path.Combine(AppContext.BaseDirectory, "DataAccessLayers", dal, $"{templateType}.sql");

    /// <summary>The SQL of a template without its TOML header, block comments and line comments.</summary>
    private static string Body(string template)
    {
        string sql = Regex.Replace(template, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(sql, @"--[^\r\n]*", string.Empty);
    }
}
