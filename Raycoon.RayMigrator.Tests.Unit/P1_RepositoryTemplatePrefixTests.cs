using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: every index and constraint name of the repository and log tables carries the table prefix
/// (<c>{CFG:TableBaseName}</c>) on every engine, so that two repositories with different prefixes can share one
/// database or schema: SQL Server scopes constraint names per schema, PostgreSQL the index behind a primary key,
/// MySQL and MariaDB foreign key names per database.
/// </summary>
public class RepositoryTemplatePrefixTests
{
    private static readonly string[] Engines = ["SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite"];
    private static readonly string[] Templates = ["Repository_CheckCreate.sql", "Repository_Upgrade_1.sql", "Repository_Upgrade_2.sql", "DatabaseLogging_CheckCreate.sql"];

    public static IEnumerable<object[]> EngineTemplates =>
        Engines.SelectMany(engine => Templates.Select(template => new object[] { engine, template }));

    [Theory]
    [MemberData(nameof(EngineTemplates))]
    public void SchemaTemplate_EveryIndexAndConstraintName_CarriesTheTablePrefix(string engine, string template)
    {
        // Arrange: the SQL without the TOML header and the line comments
        string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DataAccessLayers", engine, template));
        sql = Regex.Replace(sql, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"--[^\r\n]*", string.Empty);

        // Act
        var unprefixed = Regex.Matches(sql, @"\b(?:pk|fk|ix|uix)_(?!\{CFG:TableBaseName\})\w+").Select(m => m.Value).Distinct().ToList();

        // Assert
        unprefixed.Should().BeEmpty($"{engine}/{template} must put {{CFG:TableBaseName}} into every pk_, fk_, ix_ and uix_ name");
    }

    [Theory]
    [InlineData("SqlServer", "Repository_Upgrade_1.sql", "fk_{CFG:TableBaseName}MigrationRun_Product")]
    [InlineData("PostgreSQL", "Repository_Upgrade_1.sql", "pk_{CFG:TableBaseName}migration_run")]
    [InlineData("MySql", "Repository_Upgrade_2.sql", "fk_{CFG:TableBaseName}migration_run_migration_run_source")]
    [InlineData("MariaDb", "Repository_Upgrade_1.sql", "fk_{CFG:TableBaseName}migration_record_history_product")]
    [InlineData("Sqlite", "Repository_Upgrade_1.sql", "fk_{CFG:TableBaseName}MigrationRecord_MigrationRun")]
    [InlineData("SqlServer", "DatabaseLogging_CheckCreate.sql", "pk_{CFG:TableBaseName}MigrationLog")]
    [InlineData("PostgreSQL", "DatabaseLogging_CheckCreate.sql", "pk_{CFG:TableBaseName}migration_log")]
    public void SchemaTemplate_NamesTheConstraintWithThePrefix(string engine, string template, string expectedName)
    {
        string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DataAccessLayers", engine, template));

        sql.Should().Contain(expectedName);
    }
}
