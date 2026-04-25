
using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: RayMigratorOptionsValidator CLI tool validation tests.
/// Incorrect validation allows misconfigured CLI tools to reach runtime, causing execution failures.
/// </summary>
public class CliToolValidationTests
{
    private readonly RayMigratorOptionsValidator _validator = new();

    private static RayMigratorOptions CreateBaseOptions() => new RayMigratorOptions
    {
        Repository = new RepositoryOptions
        {
            DatabaseType = "SqlServer",
            ConnectionString = "Server=localhost;Database=Repo;",
            SchemaName = "ray" // required for SqlServer per RULE_4_2
        }
    };

    private static ProductOptions CreateProduct(string alias, string? useCliToolAlias = null) =>
        new ProductOptions
        {
            Alias = alias,
            MigrationFilesRootDirectory = "/tmp",
            MigrationErrorAction = "Terminate",
            UseCliToolAlias = useCliToolAlias,
            TargetGroups = new List<TargetGroupOptions>
            {
                new TargetGroupOptions
                {
                    Alias = "Backend",
                    DatabaseType = "SqlServer",
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    UseCliToolAlias = useCliToolAlias,
                    Targets = new List<TargetOptions>
                    {
                        new TargetOptions
                        {
                            Alias = "MainDB",
                            ConnectionString = "Server=localhost;Database=Test;",
                            UseCliToolAlias = useCliToolAlias,
                            CliToolParameters = useCliToolAlias != null
                                ? new Dictionary<string, string> { ["Server"] = "localhost" }
                                : null,
                        }
                    }
                }
            }
        };

    private static CliToolOptions CreateCliTool(string alias) => new CliToolOptions
    {
        Alias = alias,
        ExecutablePath = "sqlcmd",
        ArgumentTemplate = "-S {Server} -i {FilePath}"
    };

    [Fact]
    public void ValidateCliTools_NoCliTools_NoProducts_ReturnsSuccess()
    {
        var options = CreateBaseOptions();

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_CliToolsNull_ReturnsSuccess()
    {
        var options = CreateBaseOptions();
        options.CliTools = null;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_SingleValidTool_ReturnsSuccess()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions> { CreateCliTool("sqlcmd") };
        options.Products = new List<ProductOptions> { CreateProduct("Prod1", "sqlcmd") };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_DuplicateAliases_ReturnsFail()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions>
        {
            CreateCliTool("sqlcmd"),
            CreateCliTool("sqlcmd")
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("sqlcmd");
        result.FailureMessage.Should().Contain("Duplicate");
    }

    [Fact]
    public void ValidateCliTools_DuplicateAliasesCaseInsensitive_ReturnsFail()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions>
        {
            CreateCliTool("sqlcmd"),
            CreateCliTool("SQLCMD")
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_UseCliToolAliasReferencingNonExistentTool_ReturnsFail()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions> { CreateCliTool("sqlcmd") };
        options.Products = new List<ProductOptions> { CreateProduct("Prod1", "nonexistent-tool") };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("nonexistent-tool");
    }

    [Fact]
    public void ValidateCliTools_UseCliToolAliasReferencingExistingTool_ReturnsSuccess()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions> { CreateCliTool("sqlcmd") };
        options.Products = new List<ProductOptions> { CreateProduct("Prod1", "sqlcmd") };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_UseCliToolAliasOnProductOnly_NoCliToolsDefined_ReturnsFail()
    {
        var options = CreateBaseOptions();
        options.CliTools = null;
        var product = CreateProduct("Prod1");
        product.UseCliToolAlias = "sqlcmd";
        // Only product level, not cascaded to tg/target
        product.TargetGroups![0].UseCliToolAlias = null;
        product.TargetGroups![0].Targets![0].UseCliToolAlias = null;
        options.Products = new List<ProductOptions> { product };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("sqlcmd");
    }

    [Fact]
    public void ValidateCliTools_MultipleToolsWithUniqueAliases_ReturnsSuccess()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions>
        {
            CreateCliTool("sqlcmd"),
            CreateCliTool("psql"),
            CreateCliTool("sqlite3")
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_NullUseCliToolAlias_NotValidated()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions> { CreateCliTool("sqlcmd") };
        options.Products = new List<ProductOptions> { CreateProduct("Prod1", useCliToolAlias: null) };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_ErrorMessageContainsAvailableAliases()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions>
        {
            CreateCliTool("sqlcmd"),
            CreateCliTool("psql")
        };
        options.Products = new List<ProductOptions> { CreateProduct("Prod1", "nonexistent") };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("sqlcmd");
        result.FailureMessage.Should().Contain("psql");
    }

    [Fact]
    public void ValidateCliTools_ProductsNull_NoValidation_ReturnsSuccess()
    {
        var options = CreateBaseOptions();
        options.CliTools = new List<CliToolOptions> { CreateCliTool("sqlcmd") };
        options.Products = null;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }
}
