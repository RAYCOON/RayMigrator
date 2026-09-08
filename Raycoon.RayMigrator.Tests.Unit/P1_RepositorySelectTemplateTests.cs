using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Core.Logging;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Structural tests for the read-only Repository_Product_Select / Repository_Environment_Select
/// templates (#7). Simulate mode must resolve ProductId/EnvironmentId without inserting anything, so every
/// engine needs both templates, they must be read-only, and they must always return a 'code,message' row.
/// </summary>
public class RepositorySelectTemplateTests
{
    private static readonly string[] Engines = { "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite" };
    private static readonly string[] Entities = { "Product", "Environment" };

    public static TheoryData<string, string> EngineEntityMatrix()
    {
        var data = new TheoryData<string, string>();
        foreach (var engine in Engines)
            foreach (var entity in Entities)
                data.Add(engine, entity);
        return data;
    }

    #region TemplateType / MigrationEvent

    [Fact]
    public void TemplateType_ContainsBothSelectTemplates()
    {
        Enum.IsDefined(typeof(TemplateType), TemplateType.Repository_Product_Select).Should().BeTrue();
        Enum.IsDefined(typeof(TemplateType), TemplateType.Repository_Environment_Select).Should().BeTrue();
    }

    [Fact]
    public void MigrationEvent_SelectEventIds_FollowTheCheckInsertIds()
    {
        MigrationEvent.TemplateExecutionRepositoryProductSelect.Id.Should().Be(122);
        MigrationEvent.TemplateExecutionRepositoryProductSelect.Name.Should().Be("TemplateExecutionRepositoryProductSelect");
        MigrationEvent.TemplateExecutionRepositoryEnvironmentSelect.Id.Should().Be(123);
        MigrationEvent.TemplateExecutionRepositoryEnvironmentSelect.Name.Should().Be("TemplateExecutionRepositoryEnvironmentSelect");
    }

    #endregion

    #region Template files per engine

    [Theory]
    [MemberData(nameof(EngineEntityMatrix))]
    public void SelectTemplate_ExistsAndDeclaresItsType(string engine, string entity)
    {
        var content = ReadTemplate(engine, $"Repository_{entity}_Select.sql");

        content.Should().Contain($"TemplateType   = \"Repository_{entity}_Select\"",
            $"the {engine} template header must name the TemplateType the file is registered under (TemplateCache resolves by filename; the header is the documented contract)");
        content.Should().Contain($"DatabaseType   = \"{engine}\"");
    }

    /// <summary>
    /// Dialect contract, mirrored from the CheckInsert template tests: lookup column and quoting per engine,
    /// schema placeholder only for schema-based engines, table prefix placeholder everywhere.
    /// </summary>
    [Theory]
    [InlineData("SqlServer", "Product", "[NameLower] = @NameLower", true)]
    [InlineData("SqlServer", "Environment", "[NameLower] = @NameLower", true)]
    [InlineData("PostgreSQL", "Product", "name_lower = @NameLower", true)]
    [InlineData("PostgreSQL", "Environment", "name_lower = @NameLower", true)]
    [InlineData("MariaDb", "Product", "name_lower = @NameLower", false)]
    [InlineData("MariaDb", "Environment", "name_lower = @NameLower", false)]
    [InlineData("MySql", "Product", "name_lower = @NameLower", false)]
    [InlineData("MySql", "Environment", "name_lower = @NameLower", false)]
    [InlineData("Sqlite", "Product", "\"NameLower\" = @NameLower", false)]
    [InlineData("Sqlite", "Environment", "\"NameLower\" = @NameLower", false)]
    public void SelectTemplate_UsesTheDialectsLookupAndPlaceholders(string engine, string entity, string lookup, bool schemaBased)
    {
        var body = TemplateBody(ReadTemplate(engine, $"Repository_{entity}_Select.sql"));

        body.Should().Contain(lookup, $"the {engine} {entity} select must look up by the same column and quoting as its CheckInsert sibling");
        body.Should().Contain("{CFG:TableBaseName}", "the table prefix placeholder is mandatory on every engine");
        if (schemaBased)
            body.Should().Contain("{CFG:SchemaName}", $"{engine} tables live in the repository schema");
        else
            body.Should().NotContain("{CFG:SchemaName}", $"{engine} has no schema support; a schema placeholder would break the query");
        if (engine != "SqlServer")
            body.Should().Contain("LIMIT 1", "the scalar lookup must never return more than one row");
    }

    [Theory]
    [MemberData(nameof(EngineEntityMatrix))]
    public void SelectTemplate_IsReadOnly(string engine, string entity)
    {
        var body = TemplateBody(ReadTemplate(engine, $"Repository_{entity}_Select.sql"));

        body.Should().NotContainEquivalentOf("INSERT INTO", $"the {engine} {entity} select must never insert (#7)");
        body.Should().NotContainEquivalentOf("UPDATE ", $"the {engine} {entity} select must never update");
        body.Should().NotContainEquivalentOf("DELETE ", $"the {engine} {entity} select must never delete");
        body.Should().NotContainEquivalentOf("CREATE ", $"the {engine} {entity} select must not create objects (SQLite CheckInsert uses a temp table; the select must not)");
    }

    [Theory]
    [MemberData(nameof(EngineEntityMatrix))]
    public void SelectTemplate_LooksUpByNameLower_AndReturnsNotFoundAsZero(string engine, string entity)
    {
        var body = TemplateBody(ReadTemplate(engine, $"Repository_{entity}_Select.sql"));

        body.Should().Contain("@NameLower", "the lookup key is the pre-computed lowercase name, like the CheckInsert template");
        body.Should().MatchRegex(@"\[('|\s)*(\|\||\+|,)\s*(IFNULL\(|COALESCE\()?@Name\b",
            "the original-casing name must be concatenated into the result message (the @NameLower match above must not satisfy this)");
        body.Should().Contain($"0,{entity} [", "a missing row must be reported as result code 0, not as an error");
        body.Should().Contain("] not found");
        body.Should().Contain("] found");
    }

    #endregion

    #region Helpers

    private static string TemplateBody(string content)
    {
        // Everything after the closing "*/" of the header block; the Example DAL has no header, so fall back to the whole file.
        int end = content.IndexOf("*/", StringComparison.Ordinal);
        return end >= 0 ? content[(end + 2)..] : content;
    }

    private static string ReadTemplate(string engine, string templateFile)
    {
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(assemblyDir, "DataAccessLayers", engine, templateFile);

        if (!File.Exists(path))
        {
            var dir = new DirectoryInfo(assemblyDir);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RayMigrator.sln")))
                dir = dir.Parent;
            if (dir != null)
                path = Path.Combine(dir.FullName, $"Raycoon.RayMigrator.Database.{engine}", "Templates", templateFile);
        }

        File.Exists(path).Should().BeTrue($"template file not found for engine '{engine}': {path}");
        return File.ReadAllText(path);
    }

    #endregion
}
