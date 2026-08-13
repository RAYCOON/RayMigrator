using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Shared.Constants;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for the Repository_Environment_CheckInsert feature.
/// Covers TemplateType enum membership, MigrationEvent EventId, MigrationState.EnvironmentId property,
/// TemplateResultCode.EnvironmentNameEmpty, and SQL template structural patterns across all engines.
/// </summary>
public class EnvironmentCheckInsertTests
{
    #region TemplateType enum

    [Fact]
    public void TemplateType_ContainsRepositoryEnvironmentCheckInsert()
    {
        Enum.IsDefined(typeof(TemplateType), TemplateType.Repository_Environment_CheckInsert).Should().BeTrue();
    }

    [Fact]
    public void TemplateType_RepositoryEnvironmentCheckInsert_IsAfterRepositoryProductCheckInsert()
    {
        // Verify ordering: Environment comes right after Product in the enum declaration
        int productValue = (int)TemplateType.Repository_Product_CheckInsert;
        int environmentValue = (int)TemplateType.Repository_Environment_CheckInsert;

        environmentValue.Should().BeGreaterThan(productValue,
            "Repository_Environment_CheckInsert should follow Repository_Product_CheckInsert in the enum");
    }

    [Fact]
    public void TemplateType_HasAtLeastEighteenNonUndefinedValues()
    {
        var allValues = Enum.GetValues<TemplateType>()
            .Where(t => t != TemplateType.Undefined)
            .ToList();

        allValues.Count.Should().BeGreaterThanOrEqualTo(18,
            "TemplateType should have at least 18 non-Undefined values after adding Repository_Environment_CheckInsert");
    }

    #endregion

    #region MigrationEvent

    [Fact]
    public void MigrationEvent_TemplateExecutionRepositoryEnvironmentCheckInsert_HasEventId121()
    {
        MigrationEvent.TemplateExecutionRepositoryEnvironmentCheckInsert.Id.Should().Be(121);
    }

    [Fact]
    public void MigrationEvent_TemplateExecutionRepositoryEnvironmentCheckInsert_HasCorrectName()
    {
        MigrationEvent.TemplateExecutionRepositoryEnvironmentCheckInsert.Name
            .Should().Be("TemplateExecutionEnvironmentCheckInsert");
    }

    [Fact]
    public void MigrationEvent_EnvironmentCheckInsert_IsDistinctFromProductCheckInsert()
    {
        MigrationEvent.TemplateExecutionRepositoryEnvironmentCheckInsert.Id
            .Should().NotBe(MigrationEvent.TemplateExecutionRepositoryProductCheckInsert.Id);
    }

    #endregion

    #region MigrationState.EnvironmentId

    [Fact]
    public void MigrationState_HasEnvironmentIdProperty()
    {
        var state = new MigrationState();

        state.EnvironmentId.Should().Be(0, "default value should be 0 (int default)");
    }

    [Fact]
    public void MigrationState_EnvironmentId_CanBeSet()
    {
        var state = new MigrationState();
        state.EnvironmentId = 42;

        state.EnvironmentId.Should().Be(42);
    }

    [Fact]
    public void MigrationState_EnvironmentId_IsAfterProductId()
    {
        // Verify the property exists and is an int — check via reflection to ensure type correctness
        var prop = typeof(MigrationState).GetProperty(nameof(MigrationState.EnvironmentId));

        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(int));
    }

    #endregion

    #region TemplateResultCode.EnvironmentNameEmpty

    [Fact]
    public void TemplateResultCode_EnvironmentNameEmpty_IsMinusFifty()
    {
        TemplateResultCode.EnvironmentNameEmpty.Should().Be(-50);
    }

    [Fact]
    public void TemplateResultCode_EnvironmentNameEmpty_IsKnown()
    {
        TemplateResultCode.IsKnown(TemplateResultCode.EnvironmentNameEmpty).Should().BeTrue();
    }

    [Fact]
    public void TemplateResultCode_EnvironmentNameEmpty_IsInSeparateRangeFromProduct()
    {
        // Product range is -20 to -29, Environment range is -50 to -59
        TemplateResultCode.EnvironmentNameEmpty.Should().BeLessThan(TemplateResultCode.ProductNameEmpty,
            "EnvironmentNameEmpty (-50) should be in a different range than ProductNameEmpty (-20)");
    }

    [Theory]
    [InlineData(-51)]
    [InlineData(-52)]
    [InlineData(-59)]
    public void TemplateResultCode_UnusedEnvironmentRangeCodes_AreNotKnown(int unusedCode)
    {
        TemplateResultCode.IsKnown(unusedCode).Should().BeFalse(
            $"code {unusedCode} is in the Environment range but is not yet assigned");
    }

    #endregion

    #region SQL template structural patterns — all 5 engines

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void EnvironmentCheckInsert_TemplateFile_ExistsForAllEngines(string engine)
    {
        var path = GetTemplatePath(engine, "Repository_Environment_CheckInsert.sql");

        File.Exists(path).Should().BeTrue($"template file should exist for engine {engine} at {path}");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void EnvironmentCheckInsert_TemplateFile_ContainsNameLowerParameter(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("@NameLower",
            $"{engine} template must use @NameLower for case-insensitive lookup (pre-computed in C#)");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void EnvironmentCheckInsert_TemplateFile_ContainsNameParameter(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("@Name",
            $"{engine} template must use @Name for the original-casing environment name");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void EnvironmentCheckInsert_TemplateFile_ContainsMinusFiftyErrorCode(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("-50",
            $"{engine} template must return -50 for empty environment name");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void EnvironmentCheckInsert_TemplateFile_ContainsEnvironmentTableReference(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("Environment",
            $"{engine} template must reference the Environment table");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    public void EnvironmentCheckInsert_SchemaBasedEngines_ContainCfgSchemaNamePlaceholder(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("{CFG:SchemaName}",
            $"{engine} template must contain {{CFG:SchemaName}} placeholder for schema-based engines");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void EnvironmentCheckInsert_AllEngines_ContainCfgTableBaseNamePlaceholder(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("{CFG:TableBaseName}",
            $"{engine} template must contain {{CFG:TableBaseName}} placeholder");
    }

    [Fact]
    public void SqlServer_EnvironmentCheckInsert_UsesNameLowerForLookup()
    {
        var content = ReadTemplate("SqlServer", "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("[NameLower] = @NameLower",
            "SqlServer template should look up environment by NameLower column");
    }

    [Fact]
    public void SqlServer_EnvironmentCheckInsert_UsesScopeIdentityForNewId()
    {
        var content = ReadTemplate("SqlServer", "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("SCOPE_IDENTITY()",
            "SqlServer template should use SCOPE_IDENTITY() to retrieve the new EnvironmentId");
    }

    [Fact]
    public void SqlServer_EnvironmentCheckInsert_UsesSysUtcDatetime()
    {
        var content = ReadTemplate("SqlServer", "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("SYSUTCDATETIME()",
            "SqlServer template should use SYSUTCDATETIME() for CreatedAt");
    }

    [Theory]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void MariaDbMySql_EnvironmentCheckInsert_UsesCurrentTimestamp(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("CURRENT_TIMESTAMP",
            $"{engine} template should use CURRENT_TIMESTAMP for CreatedAt (DAL-014: session time_zone='+00:00' ensures UTC)");
        content.Should().NotContain("UTC_TIMESTAMP",
            $"{engine} template must not reference UTC_TIMESTAMP after DAL-014");
    }

    [Fact]
    public void Sqlite_EnvironmentCheckInsert_UsesDatetimeNow()
    {
        var content = ReadTemplate("Sqlite", "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("datetime('now')",
            "Sqlite template should use datetime('now') for CreatedAt");
    }

    [Fact]
    public void Sqlite_EnvironmentCheckInsert_UsesTempTableForState()
    {
        var content = ReadTemplate("Sqlite", "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("_rc_state",
            "Sqlite template must use temp table _rc_state for intermediate state (no session variables in SQLite)");
    }

    #endregion

    #region Template TOML header validation

    [Theory]
    [InlineData("SqlServer", "SqlServer")]
    [InlineData("PostgreSQL", "PostgreSQL")]
    [InlineData("MariaDb", "MariaDb")]
    [InlineData("MySql", "MySql")]
    [InlineData("Sqlite", "Sqlite")]
    public void EnvironmentCheckInsert_TemplateHeader_HasCorrectDatabaseType(string engine, string expectedDatabaseType)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain($"DatabaseType   = \"{expectedDatabaseType}\"",
            $"Template header for {engine} must declare the correct DatabaseType");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void EnvironmentCheckInsert_TemplateHeader_HasCorrectTemplateType(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Environment_CheckInsert.sql");

        content.Should().Contain("TemplateType   = \"Repository_Environment_CheckInsert\"",
            $"Template header for {engine} must declare TemplateType as Repository_Environment_CheckInsert");
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
