
using FluentAssertions;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-5: RayMigratorOptionsValidator tests.
/// </summary>
public class RayMigratorOptionsValidatorTests
{
    private readonly RayMigratorOptionsValidator _validator = new();

    private static RayMigratorOptions CreateValidOptions()
    {
        return new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=localhost;Database=Repo;",
                SchemaName = "dbo"
            }
        };
    }

    [Fact]
    public void ValidOptions_ReturnsSuccess()
    {
        var options = CreateValidOptions();

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void RepositoryNull_ReturnsFail()
    {
        var options = new RayMigratorOptions { Repository = null };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void RepositoryWithoutDatabaseType_ReturnsFail()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = null,
                ConnectionString = "Server=localhost;Database=Repo;",
                SchemaName = "dbo"
            }
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void RepositoryWithoutConnectionString_ReturnsFail()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "SqlServer",
                ConnectionString = null,
                SchemaName = "dbo"
            }
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void DatabaseLoggingWithoutDatabaseType_ReturnsFail()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = null,
            ConnectionString = "Server=localhost;Database=Log;",
            SchemaName = "dbo"
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void DatabaseLoggingWithoutConnectionString_ReturnsFail()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "SqlServer",
            ConnectionString = null,
            SchemaName = "dbo"
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void DatabaseLoggingNull_IsOptional_ReturnsSuccess()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = null;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void RepositoryWithoutSchemaName_ReturnsFail()
    {
        // RULE_4_2 now requires SchemaName for SqlServer and PostgreSQL.
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=localhost;Database=Repo;",
                SchemaName = null
            }
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue("RULE_4_2 requires SchemaName for SqlServer");
    }

    [Fact]
    public void DatabaseLoggingWithoutSchemaName_ReturnsSuccess()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "SqlServer",
            ConnectionString = "Server=localhost;Database=Log;",
            SchemaName = null
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue("SchemaName validation moved to pipeline where DalSpecificProperties is available");
    }

    // === DAL-017 — TableBaseName lowercase guard for PostgreSQL ===

    [Fact]
    public void Repository_PostgreSql_UppercaseTableBaseName_Fails()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=localhost;Database=repo;",
                SchemaName = "ray",
                TableBaseName = "RM_"
            }
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TableBaseName");
        result.FailureMessage.Should().Contain("RM_");
        result.FailureMessage.Should().Contain("lowercase");
    }

    [Fact]
    public void Repository_PostgreSql_LowercaseTableBaseName_Succeeds()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=localhost;Database=repo;",
                SchemaName = "ray",
                TableBaseName = "rm_"
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Repository_PostgreSql_NullTableBaseName_Succeeds()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=localhost;Database=repo;",
                SchemaName = "ray",
                TableBaseName = null
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Repository_PostgreSql_EmptyTableBaseName_Succeeds()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=localhost;Database=repo;",
                SchemaName = "ray",
                TableBaseName = ""
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Repository_SqlServer_UppercaseTableBaseName_Succeeds()
    {
        // DAL-017 guard applies only to PostgreSQL; other engines are unaffected.
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=localhost;Database=Repo;",
                SchemaName = "dbo",
                TableBaseName = "RM_"
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void DatabaseLogging_PostgreSql_UppercaseTableBaseName_Fails()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "PostgreSQL",
            ConnectionString = "Host=localhost;Database=log;",
            SchemaName = "ray",
            TableBaseName = "Log_"
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TableBaseName");
        result.FailureMessage.Should().Contain("Log_");
        result.FailureMessage.Should().Contain("lowercase");
    }

    [Fact]
    public void DatabaseLogging_PostgreSql_LowercaseTableBaseName_Succeeds()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "PostgreSQL",
            ConnectionString = "Host=localhost;Database=log;",
            SchemaName = "ray",
            TableBaseName = "log_"
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void DatabaseLogging_SqlServer_UppercaseTableBaseName_Succeeds()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "SqlServer",
            ConnectionString = "Server=localhost;Database=Log;",
            SchemaName = "dbo",
            TableBaseName = "Log_"
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    // === DAL-018 — TableBaseName lowercase guard for MariaDB + MySQL ===

    [Fact]
    public void Repository_MariaDb_UppercaseTableBaseName_Fails()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "MariaDb",
                ConnectionString = "Host=localhost;Database=repo;",
                SchemaName = "ray",
                TableBaseName = "RM_"
            }
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TableBaseName");
        result.FailureMessage.Should().Contain("RM_");
        result.FailureMessage.Should().Contain("lowercase");
    }

    [Fact]
    public void Repository_MariaDb_LowercaseTableBaseName_Succeeds()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "MariaDb",
                ConnectionString = "Host=localhost;Database=repo;",
                SchemaName = "ray",
                TableBaseName = "rm_"
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Repository_MySql_UppercaseTableBaseName_Fails()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "MySql",
                ConnectionString = "Host=localhost;Database=repo;",
                SchemaName = "ray",
                TableBaseName = "RM_"
            }
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TableBaseName");
        result.FailureMessage.Should().Contain("RM_");
        result.FailureMessage.Should().Contain("lowercase");
    }

    [Fact]
    public void Repository_MySql_LowercaseTableBaseName_Succeeds()
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "MySql",
                ConnectionString = "Host=localhost;Database=repo;",
                SchemaName = "ray",
                TableBaseName = "rm_"
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void DatabaseLogging_MariaDb_UppercaseTableBaseName_Fails()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "MariaDb",
            ConnectionString = "Host=localhost;Database=log;",
            SchemaName = "ray",
            TableBaseName = "Log_"
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TableBaseName");
        result.FailureMessage.Should().Contain("Log_");
        result.FailureMessage.Should().Contain("lowercase");
    }

    [Fact]
    public void DatabaseLogging_MariaDb_LowercaseTableBaseName_Succeeds()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "MariaDb",
            ConnectionString = "Host=localhost;Database=log;",
            SchemaName = "ray",
            TableBaseName = "log_"
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void DatabaseLogging_MySql_UppercaseTableBaseName_Fails()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "MySql",
            ConnectionString = "Host=localhost;Database=log;",
            SchemaName = "ray",
            TableBaseName = "Log_"
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TableBaseName");
        result.FailureMessage.Should().Contain("Log_");
        result.FailureMessage.Should().Contain("lowercase");
    }

    [Fact]
    public void DatabaseLogging_MySql_LowercaseTableBaseName_Succeeds()
    {
        var options = CreateValidOptions();
        options.DatabaseLogging = new DatabaseLoggingOptions
        {
            DatabaseType = "MySql",
            ConnectionString = "Host=localhost;Database=log;",
            SchemaName = "ray",
            TableBaseName = "log_"
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    // --- Helper methods for Rule 1.x / 7.x tests ---

    private static ProductOptions MakeProduct(string alias, params TargetGroupOptions[] tgs) =>
        new()
        {
            Alias = alias,
            MigrationFilesRootDirectory = "/tmp",
            // Effective values required by RULE_8_1 (normally merged by ProductDefaultsPostConfigureOptions,
            // which does not run in these unit tests).
            MigrationErrorAction = "Terminate",
            TargetGroups = tgs.ToList(),
        };

    private static TargetGroupOptions MakeTG(string alias, string dbType = "SqlServer", params TargetOptions[] targets) =>
        new()
        {
            Alias = alias,
            DatabaseType = dbType,
            // Effective values required by RULE_8_2/8_3.
            TargetMigrationOrder = "Simultaneously",
            HashValidationScope = "File",
            Targets = targets.ToList(),
        };

    private static TargetOptions MakeTarget(string alias, string? cs = "Server=localhost;Database=MyDb;") =>
        new() { Alias = alias, ConnectionString = cs };

    // === Rule 1.8 — Duplicate Product Alias ===

    [Fact]
    public void DuplicateProductAlias_SameAlias_Fails()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Alpha", MakeTG("TG1", targets: MakeTarget("T1"))),
            MakeProduct("Alpha", MakeTG("TG2", targets: MakeTarget("T2")))
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Alpha");
    }

    [Fact]
    public void DuplicateProductAlias_CaseInsensitive_Fails()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Alpha", MakeTG("TG1", targets: MakeTarget("T1"))),
            MakeProduct("alpha", MakeTG("TG2", targets: MakeTarget("T2")))
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void DuplicateProductAlias_UniqueAliases_Succeeds()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Alpha", MakeTG("TG1", targets: MakeTarget("T1"))),
            MakeProduct("Beta", MakeTG("TG2", targets: MakeTarget("T2")))
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void DuplicateProductAlias_NullProducts_Succeeds()
    {
        var options = CreateValidOptions();
        options.Products = null;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    // === Rule 1.1 — Duplicate TargetGroup Alias ===

    [Fact]
    public void DuplicateTargetGroupAlias_SameProduct_Fails()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Prod1", MakeTG("Backend", targets: MakeTarget("T1")), MakeTG("Backend", targets: MakeTarget("T2")))
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void DuplicateTargetGroupAlias_DifferentProducts_Succeeds()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Prod1", MakeTG("Backend", targets: MakeTarget("T1"))),
            MakeProduct("Prod2", MakeTG("Backend", targets: MakeTarget("T2")))
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void DuplicateTargetGroupAlias_UniqueAliases_Succeeds()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Prod1", MakeTG("Backend", targets: MakeTarget("T1")), MakeTG("Frontend", targets: MakeTarget("T2")))
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    // === Rule 1.2 — Duplicate Target Alias ===

    [Fact]
    public void DuplicateTargetAlias_SameTargetGroup_Fails()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Prod1", MakeTG("TG1", targets: new[] { MakeTarget("DB1"), MakeTarget("DB1") }))
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void DuplicateTargetAlias_DifferentTargetGroups_Succeeds()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Prod1", MakeTG("TG1", targets: MakeTarget("DB1")), MakeTG("TG2", targets: MakeTarget("DB1")))
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void DuplicateTargetAlias_UniqueAliases_Succeeds()
    {
        var options = CreateValidOptions();
        options.Products = new List<ProductOptions>
        {
            MakeProduct("Prod1", MakeTG("TG1", targets: new[] { MakeTarget("DB1"), MakeTarget("DB2") }))
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }
}
