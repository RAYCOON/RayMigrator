using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: SQL template structure validations (O2-O7).
/// Validates that SQL templates for all database engines contain expected locking, row-count, and error handling patterns.
/// </summary>
public class SqlTemplateStructureTests
{
    #region O2: MigrationRun_Insert Template Structure Tests

    [Fact]
    public void SqlServer_MigrationRunInsert_ContainsAtomicLocking()
    {
        var templatePath = GetTemplatePath("SqlServer", "Repository_MigrationRun_Insert.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("UPDLOCK, HOLDLOCK", "SqlServer template should use UPDLOCK, HOLDLOCK for atomic check");
        content.Should().Contain("BEGIN TRANSACTION", "SqlServer template should use explicit transaction");
        content.Should().Contain("COMMIT TRANSACTION", "SqlServer template should commit transaction");
    }

    [Fact]
    public void PostgreSql_MigrationRunInsert_ContainsAdvisoryLock()
    {
        var templatePath = GetTemplatePath("PostgreSQL", "Repository_MigrationRun_Insert.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("pg_advisory_xact_lock", "PostgreSQL template should use advisory lock for race condition prevention");
    }

    [Theory]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void MariaDbMySql_MigrationRunInsert_ContainsGetLockAndRowCount(string engine)
    {
        var templatePath = GetTemplatePath(engine, "Repository_MigrationRun_Insert.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("GET_LOCK(", $"{engine} template should use GET_LOCK() for advisory locking");
        content.Should().Contain("RELEASE_LOCK(", $"{engine} template should release the advisory lock");
        content.Should().Contain("ROW_COUNT()", $"{engine} template should validate INSERT with ROW_COUNT()");
    }

    #endregion

    #region O5: Migration_Update Template Structure Tests

    [Fact]
    public void SqlServer_MigrationUpdate_UsesRowCountAfterUpdate()
    {
        var templatePath = GetTemplatePath("SqlServer", "Repository_MigrationRecord_Update.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("@@ROWCOUNT", "SqlServer template should use @@ROWCOUNT after UPDATE");
        content.Should().NotContain("IF NOT EXISTS", "SqlServer template should not have separate EXISTS check before UPDATE");
    }

    [Fact]
    public void PostgreSql_MigrationUpdate_UsesGetDiagnosticsAfterUpdate()
    {
        var templatePath = GetTemplatePath("PostgreSQL", "Repository_MigrationRecord_Update.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("GET DIAGNOSTICS", "PostgreSQL template should use GET DIAGNOSTICS after UPDATE");
        content.Should().NotContain("SELECT COUNT(*) INTO v_exists", "PostgreSQL template should not have separate COUNT before UPDATE");
    }

    #endregion

    #region O6: MigratorMeta registration Lock Hint Test

    [Fact]
    public void SqlServer_MigratorMetaCheckInsert_UsesRowLevelLocking()
    {
        var templatePath = GetTemplatePath("SqlServer", "Repository_MigratorMeta_CheckInsert.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("UPDLOCK, ROWLOCK, HOLDLOCK", "SqlServer template should use row-level locking");
        content.Should().NotContain("TABLOCKX", "SqlServer template should not use exclusive table lock");
    }

    #endregion

    #region O7: MariaDb/MySQL LAST_INSERT_ID Validation

    [Theory]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void MariaDbMySql_MigrationRunInsert_ValidatesInsertResult(string engine)
    {
        var templatePath = GetTemplatePath(engine, "Repository_MigrationRun_Insert.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("@v_inserted = 0", $"{engine} template should check for failed INSERT");
        content.Should().Contain("Failed to create MigrationRun", $"{engine} template should have error message for failed INSERT");
    }

    #endregion

    #region Product and Environment registration under parallel first starts

    [Theory]
    [InlineData("Repository_Product_CheckInsert.sql")]
    [InlineData("Repository_Environment_CheckInsert.sql")]
    public void SqlServer_CheckInsert_LocksTheNameInATransaction(string templateFile)
    {
        var sql = StripComments(File.ReadAllText(GetTemplatePath("SqlServer", templateFile)));

        sql.Should().Contain("BEGIN TRANSACTION", $"{templateFile} should look the name up and insert it in one transaction");
        sql.Should().Contain("WITH (UPDLOCK, ROWLOCK, HOLDLOCK)",
            $"{templateFile} should lock the name also while it has no row, so that a parallel first start waits instead of failing on the unique index");
        sql.Should().Contain("COMMIT TRANSACTION", $"{templateFile} should commit the transaction");
    }

    [Theory]
    [InlineData("Repository_Product_CheckInsert.sql")]
    [InlineData("Repository_Environment_CheckInsert.sql")]
    public void PostgreSql_CheckInsert_ToleratesAParallelFirstRegistration(string templateFile)
    {
        var sql = StripComments(File.ReadAllText(GetTemplatePath("PostgreSQL", templateFile)));

        sql.Should().Contain("ON CONFLICT (name_lower) DO NOTHING",
            $"{templateFile} should keep the row of a parallel first start instead of failing on the unique index");
    }

    [Theory]
    [InlineData("MariaDb", "Repository_Product_CheckInsert.sql")]
    [InlineData("MariaDb", "Repository_Environment_CheckInsert.sql")]
    [InlineData("MySql", "Repository_Product_CheckInsert.sql")]
    [InlineData("MySql", "Repository_Environment_CheckInsert.sql")]
    public void MariaDbMySql_CheckInsert_ToleratesAParallelFirstRegistration(string engine, string templateFile)
    {
        var sql = StripComments(File.ReadAllText(GetTemplatePath(engine, templateFile)));

        sql.Should().Contain("ON DUPLICATE KEY UPDATE",
            $"{engine} {templateFile} should keep the row of a parallel first start instead of failing on the duplicate key");
        sql.Should().NotContain("INSERT IGNORE", $"{engine} {templateFile} should not turn errors of the strict mode into warnings");
    }

    [Theory]
    [InlineData("Repository_Product_CheckInsert.sql")]
    [InlineData("Repository_Environment_CheckInsert.sql")]
    public void Sqlite_CheckInsert_ToleratesAParallelFirstRegistration(string templateFile)
    {
        var sql = StripComments(File.ReadAllText(GetTemplatePath("Sqlite", templateFile)));

        sql.Should().Contain("ON CONFLICT (\"NameLower\") DO NOTHING",
            $"{templateFile} should keep the row of a parallel first start instead of failing on the unique index");
    }

    #endregion

    #region Helper Methods

    /// <summary>The SQL of a template without its block comments and its line comments.</summary>
    private static string StripComments(string sql) => Regex.Replace(sql, @"/\*.*?\*/|--[^\n]*", string.Empty, RegexOptions.Singleline);

    /// <summary>
    /// Gets the path to a SQL template file relative to the Database project output.
    /// </summary>
    private static string GetTemplatePath(string engine, string templateFile)
    {
        // Templates are copied to the output directory during build
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var templatePath = Path.Combine(assemblyDir, "DataAccessLayers", engine, templateFile);

        if (!File.Exists(templatePath))
        {
            // Fallback: try to find the file relative to the solution root
            var solutionDir = FindSolutionDir(assemblyDir);
            if (solutionDir != null)
            {
                templatePath = Path.Combine(solutionDir, "Raycoon.RayMigrator.Database", "DataAccessLayers", engine, templateFile);
            }
        }

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}");
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
