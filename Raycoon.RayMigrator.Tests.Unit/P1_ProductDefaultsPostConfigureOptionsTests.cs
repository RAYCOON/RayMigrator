using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-4: ProductDefaultsPostConfigureOptions tests.
/// Default values not propagated => missing required fields => runtime crashes.
/// </summary>
public class ProductDefaultsPostConfigureOptionsTests
{
    private static RayMigratorOptions CreateOptionsWithDefaults(
        string? migrationErrorAction = "Terminate",
        string? targetMigrationOrder = "Successively",
        string? hashValidationScope = "FileHash",
        int? dbTimeout = 30)
    {
        return new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                MigrationErrorAction = migrationErrorAction,
                MigrationFilesExtension = "sql",
                MigrationRollbackFilesPreExtension = "rollback",
                MigrationFilesEncoding = "UTF-8",
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = targetMigrationOrder,
                    HashValidationScope = hashValidationScope,
                    TargetDefaults = new TargetDefaultsOptions
                    {
                        DbCommandTimeoutInSeconds = dbTimeout,
                        DbCommandMaxRetries = 3,
                        DbCommandWaitTimeInMsBeforeRetry = 500
                    }
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            Targets = new List<TargetOptions>
                            {
                                new TargetOptions
                                {
                                    Alias = "MainDB",
                                    ConnectionString = "Server=localhost;Database=Test;"
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    [Fact]
    public void DefaultMigrationErrorAction_IsCopiedToProduct()
    {
        var options = CreateOptionsWithDefaults(migrationErrorAction: "Terminate");
        var postConfigure = new ProductDefaultsPostConfigureOptions();

        postConfigure.PostConfigure(null, options);

        options.Products!.First().MigrationErrorAction.Should().Be("Terminate");
    }

    [Fact]
    public void ExplicitMigrationErrorAction_IsNotOverwritten()
    {
        var options = CreateOptionsWithDefaults(migrationErrorAction: "Terminate");
        var product = options.Products!.First() as ProductOptions;
        product!.MigrationErrorAction = "Rollback";

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        product.MigrationErrorAction.Should().Be("Rollback");
    }

    [Fact]
    public void DefaultTargetMigrationOrder_IsCopiedToTargetGroup()
    {
        var options = CreateOptionsWithDefaults(targetMigrationOrder: "Successively");
        var postConfigure = new ProductDefaultsPostConfigureOptions();

        postConfigure.PostConfigure(null, options);

        var targetGroup = options.Products!.First().TargetGroups!.First();
        targetGroup.TargetMigrationOrder.Should().Be("Successively");
    }

    [Fact]
    public void DefaultDbTimeout_IsCopiedToTarget()
    {
        var options = CreateOptionsWithDefaults(dbTimeout: 30);
        var postConfigure = new ProductDefaultsPostConfigureOptions();

        postConfigure.PostConfigure(null, options);

        var target = options.Products!.First().TargetGroups!.First().Targets!.First();
        target.DbCommandTimeoutInSeconds.Should().Be(30);
    }

    [Fact]
    public void ExplicitDbTimeout_IsNotOverwritten()
    {
        var options = CreateOptionsWithDefaults(dbTimeout: 30);
        var target = options.Products!.First().TargetGroups!.First().Targets!.First();
        target.DbCommandTimeoutInSeconds = 60;

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        target.DbCommandTimeoutInSeconds.Should().Be(60);
    }

    [Fact]
    public void MultipleProducts_AllReceiveDefaults()
    {
        var options = CreateOptionsWithDefaults();
        var product2 = new ProductOptions(null)
        {
            Alias = "Product2",
            MigrationFilesRootDirectory = "/tmp",
            TargetGroups = new List<TargetGroupOptions>
            {
                new TargetGroupOptions
                {
                    Alias = "Frontend",
                    DatabaseType = "PostgreSql",
                    Targets = new List<TargetOptions>
                    {
                        new TargetOptions
                        {
                            Alias = "FrontDB",
                            ConnectionString = "Host=localhost;Database=Test;"
                        }
                    }
                }
            }
        };
        options.Products!.Add(product2);

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        foreach (var product in options.Products)
        {
            product.MigrationErrorAction.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void InvalidDefaultEnum_IsNotCopied()
    {
        var options = CreateOptionsWithDefaults(migrationErrorAction: "InvalidAction");
        var postConfigure = new ProductDefaultsPostConfigureOptions();

        postConfigure.PostConfigure(null, options);

        // The invalid default should not be copied; the product keeps its null/empty value
        options.Products!.First().MigrationErrorAction.Should().BeNullOrEmpty();
    }

    [Fact]
    public void RequireRollbackFile_Default_CopiedToProduct()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                MigrationErrorAction = "Terminate",
                RequireRollbackFile = false,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    TargetDefaults = new TargetDefaultsOptions()
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    TargetGroups = new List<TargetGroupOptions>()
                }
            }
        };

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().RequireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void RequireRollbackFile_ExplicitOnProduct_NotOverwritten()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                MigrationErrorAction = "Terminate",
                RequireRollbackFile = true,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    TargetDefaults = new TargetDefaultsOptions()
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    RequireRollbackFile = false,
                    TargetGroups = new List<TargetGroupOptions>()
                }
            }
        };

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().RequireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void RequireRollbackFile_NullDefault_ProductRemainsNull()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                MigrationErrorAction = "Terminate",
                RequireRollbackFile = null,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    TargetDefaults = new TargetDefaultsOptions()
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    TargetGroups = new List<TargetGroupOptions>()
                }
            }
        };

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().RequireRollbackFile.Should().BeNull();
    }

    [Fact]
    public void StopRollbackOnMissingRollbackFile_Default_CopiedToProduct()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                MigrationErrorAction = "Terminate",
                StopRollbackOnMissingRollbackFile = true,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    TargetDefaults = new TargetDefaultsOptions()
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    TargetGroups = new List<TargetGroupOptions>()
                }
            }
        };

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().StopRollbackOnMissingRollbackFile.Should().Be(true);
    }

    [Fact]
    public void StopRollbackOnMissingRollbackFile_ExplicitOnProduct_NotOverwritten()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                MigrationErrorAction = "Terminate",
                StopRollbackOnMissingRollbackFile = true,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    TargetDefaults = new TargetDefaultsOptions()
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    StopRollbackOnMissingRollbackFile = false,
                    TargetGroups = new List<TargetGroupOptions>()
                }
            }
        };

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().StopRollbackOnMissingRollbackFile.Should().Be(false);
    }

    [Fact]
    public void StopRollbackOnMissingRollbackFile_CascadesToTargetGroup()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                MigrationErrorAction = "Terminate",
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    StopRollbackOnMissingRollbackFile = false,
                    TargetDefaults = new TargetDefaultsOptions()
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            Targets = new List<TargetOptions>()
                        }
                    }
                }
            }
        };

        var postConfigure = new ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().TargetGroups!.First().StopRollbackOnMissingRollbackFile.Should().Be(false);
    }

    [Fact]
    public void InvalidDefaultEncoding_ThrowsConfigurationValidationException()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                MigrationErrorAction = "Terminate",
                MigrationFilesEncoding = "NOT-A-VALID-ENCODING",
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    TargetDefaults = new TargetDefaultsOptions()
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    TargetGroups = new List<TargetGroupOptions>()
                }
            }
        };

        var postConfigure = new ProductDefaultsPostConfigureOptions();

        var act = () => postConfigure.PostConfigure(null, options);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*NOT-A-VALID-ENCODING*");
    }
}
