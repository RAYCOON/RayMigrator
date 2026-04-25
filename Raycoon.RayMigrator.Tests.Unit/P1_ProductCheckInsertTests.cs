
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Shared.Constants;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for the Repository_Product_CheckInsert feature after the NameLower refactor.
/// Covers TemplateType enum membership, MigrationEvent EventId, MigrationState.ProductId property,
/// TemplateResultCode.ProductNameEmpty, and SQL template structural patterns across all engines
/// including the NameLower parameter and the anti-regression check for the removed Description column.
/// </summary>
public class ProductCheckInsertTests
{
    #region TemplateType enum

    [Fact]
    public void TemplateType_ContainsRepositoryProductCheckInsert()
    {
        Enum.IsDefined(typeof(TemplateType), TemplateType.Repository_Product_CheckInsert).Should().BeTrue();
    }

    [Fact]
    public void TemplateType_RepositoryProductCheckInsert_IsBeforeRepositoryEnvironmentCheckInsert()
    {
        // Verify ordering: Product comes before Environment in the enum declaration
        int productValue = (int)TemplateType.Repository_Product_CheckInsert;
        int environmentValue = (int)TemplateType.Repository_Environment_CheckInsert;

        productValue.Should().BeLessThan(environmentValue,
            "Repository_Product_CheckInsert should precede Repository_Environment_CheckInsert in the enum");
    }

    #endregion

    #region MigrationEvent

    [Fact]
    public void MigrationEvent_TemplateExecutionRepositoryProductCheckInsert_HasEventId120()
    {
        MigrationEvent.TemplateExecutionRepositoryProductCheckInsert.Id.Should().Be(120);
    }

    [Fact]
    public void MigrationEvent_TemplateExecutionRepositoryProductCheckInsert_HasCorrectName()
    {
        MigrationEvent.TemplateExecutionRepositoryProductCheckInsert.Name
            .Should().Be("TemplateExecutionProductCheckInsert");
    }

    [Fact]
    public void MigrationEvent_ProductCheckInsert_IsDistinctFromEnvironmentCheckInsert()
    {
        MigrationEvent.TemplateExecutionRepositoryProductCheckInsert.Id
            .Should().NotBe(MigrationEvent.TemplateExecutionRepositoryEnvironmentCheckInsert.Id);
    }

    #endregion

    #region MigrationState.ProductId

    [Fact]
    public void MigrationState_HasProductIdProperty()
    {
        var state = new MigrationState();

        state.ProductId.Should().Be(0, "default value should be 0 (int default)");
    }

    [Fact]
    public void MigrationState_ProductId_CanBeSet()
    {
        var state = new MigrationState();
        state.ProductId = 42;

        state.ProductId.Should().Be(42);
    }

    [Fact]
    public void MigrationState_ProductId_IsIntProperty()
    {
        // Verify the property exists and is an int — check via reflection to ensure type correctness
        var prop = typeof(MigrationState).GetProperty(nameof(MigrationState.ProductId));

        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(int));
    }

    #endregion

    #region TemplateResultCode.ProductNameEmpty

    [Fact]
    public void TemplateResultCode_ProductNameEmpty_IsMinusTwenty()
    {
        TemplateResultCode.ProductNameEmpty.Should().Be(-20);
    }

    [Fact]
    public void TemplateResultCode_ProductNameEmpty_IsKnown()
    {
        TemplateResultCode.IsKnown(TemplateResultCode.ProductNameEmpty).Should().BeTrue();
    }

    [Fact]
    public void TemplateResultCode_ProductNameEmpty_IsInSeparateRangeFromEnvironment()
    {
        // Product range is -20 to -29, Environment range is -50 to -59
        TemplateResultCode.ProductNameEmpty.Should().BeGreaterThan(TemplateResultCode.EnvironmentNameEmpty,
            "ProductNameEmpty (-20) should be in a different range than EnvironmentNameEmpty (-50)");
    }

    [Theory]
    [InlineData(-21)]
    [InlineData(-22)]
    [InlineData(-29)]
    public void TemplateResultCode_UnusedProductRangeCodes_AreNotKnown(int unusedCode)
    {
        TemplateResultCode.IsKnown(unusedCode).Should().BeFalse(
            $"code {unusedCode} is in the Product range but is not yet assigned");
    }

    #endregion

    #region SQL template structural patterns — all 5 engines

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_TemplateFile_ExistsForAllEngines(string engine)
    {
        var path = GetTemplatePath(engine, "Repository_Product_CheckInsert.sql");

        File.Exists(path).Should().BeTrue($"template file should exist for engine {engine} at {path}");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_TemplateFile_IsNonEmpty(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().NotBeNullOrWhiteSpace($"{engine} template must not be empty");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_TemplateFile_ContainsNameLowerParameter(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain("@NameLower",
            $"{engine} template must use @NameLower for case-insensitive lookup (pre-computed in C#)");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_TemplateFile_ContainsNameParameter(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain("@Name",
            $"{engine} template must use @Name for the original-casing product name");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_TemplateFile_DoesNotContainDescriptionParameter(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().NotContain("@Description",
            $"{engine} template must not reference @Description after the NameLower refactor (anti-regression)");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_TemplateFile_ContainsMinusTwentyErrorCode(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain("-20",
            $"{engine} template must return -20 for empty product name");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_TemplateFile_ContainsProductTableReference(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain("Product",
            $"{engine} template must reference the Product table");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    public void ProductCheckInsert_SchemaBasedEngines_ContainCfgSchemaNamePlaceholder(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain("{CFG:SchemaName}",
            $"{engine} template must contain {{CFG:SchemaName}} placeholder for schema-based engines");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_AllEngines_ContainCfgTableBaseNamePlaceholder(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain("{CFG:TableBaseName}",
            $"{engine} template must contain {{CFG:TableBaseName}} placeholder");
    }

    [Fact]
    public void SqlServer_ProductCheckInsert_UsesNameLowerForLookup()
    {
        var content = ReadTemplate("SqlServer", "Repository_Product_CheckInsert.sql");

        content.Should().Contain("[NameLower] = @NameLower",
            "SqlServer template should look up product by NameLower column");
    }

    [Fact]
    public void SqlServer_ProductCheckInsert_UsesScopeIdentityForNewId()
    {
        var content = ReadTemplate("SqlServer", "Repository_Product_CheckInsert.sql");

        content.Should().Contain("SCOPE_IDENTITY()",
            "SqlServer template should use SCOPE_IDENTITY() to retrieve the new ProductId");
    }

    [Fact]
    public void SqlServer_ProductCheckInsert_UsesSysUtcDatetime()
    {
        var content = ReadTemplate("SqlServer", "Repository_Product_CheckInsert.sql");

        content.Should().Contain("SYSUTCDATETIME()",
            "SqlServer template should use SYSUTCDATETIME() for CreatedAt");
    }

    [Fact]
    public void PostgreSQL_ProductCheckInsert_UsesNameLowerForLookup()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_Product_CheckInsert.sql");

        // DAL-017: PostgreSQL identifiers are unquoted snake_case.
        content.Should().Contain("name_lower = @NameLower",
            "PostgreSQL template should look up product by name_lower column");
    }

    [Fact]
    public void PostgreSQL_ProductCheckInsert_UsesUtcTimestamp()
    {
        var content = ReadTemplate("PostgreSQL", "Repository_Product_CheckInsert.sql");

        content.Should().Contain("NOW()",
            "PostgreSQL template should use NOW() for CreatedAt (writes to TIMESTAMPTZ column)");
        content.Should().NotContain("NOW() AT TIME ZONE 'UTC'",
            "NOW() AT TIME ZONE 'UTC' is obsolete after DAL-012 TIMESTAMPTZ migration");
    }

    [Theory]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void MariaDbMySql_ProductCheckInsert_UsesNameLowerForLookup(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        // DAL-018: MariaDB/MySQL identifiers are unquoted snake_case.
        content.Should().Contain("name_lower = @NameLower",
            $"{engine} template should look up product by name_lower column after DAL-018");
    }

    [Theory]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void MariaDbMySql_ProductCheckInsert_UsesCurrentTimestamp(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain("CURRENT_TIMESTAMP",
            $"{engine} template should use CURRENT_TIMESTAMP for CreatedAt (DAL-014: session time_zone='+00:00' ensures UTC)");
        content.Should().NotContain("UTC_TIMESTAMP",
            $"{engine} template must not reference UTC_TIMESTAMP after DAL-014");
    }

    [Fact]
    public void Sqlite_ProductCheckInsert_UsesNameLowerForLookup()
    {
        var content = ReadTemplate("Sqlite", "Repository_Product_CheckInsert.sql");

        content.Should().Contain("\"NameLower\" = @NameLower",
            "Sqlite template should look up product by NameLower column");
    }

    [Fact]
    public void Sqlite_ProductCheckInsert_UsesDatetimeNow()
    {
        var content = ReadTemplate("Sqlite", "Repository_Product_CheckInsert.sql");

        content.Should().Contain("datetime('now')",
            "Sqlite template should use datetime('now') for CreatedAt");
    }

    [Fact]
    public void Sqlite_ProductCheckInsert_UsesTempTableForState()
    {
        var content = ReadTemplate("Sqlite", "Repository_Product_CheckInsert.sql");

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
    public void ProductCheckInsert_TemplateHeader_HasCorrectDatabaseType(string engine, string expectedDatabaseType)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain($"DatabaseType   = \"{expectedDatabaseType}\"",
            $"Template header for {engine} must declare the correct DatabaseType");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ProductCheckInsert_TemplateHeader_HasCorrectTemplateType(string engine)
    {
        var content = ReadTemplate(engine, "Repository_Product_CheckInsert.sql");

        content.Should().Contain("TemplateType   = \"Repository_Product_CheckInsert\"",
            $"Template header for {engine} must declare TemplateType as Repository_Product_CheckInsert");
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
