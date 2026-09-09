using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
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
        string? targetMigrationOrder = "TargetByTarget",
        string? hashValidationScope = "File",
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
        var options = CreateOptionsWithDefaults(targetMigrationOrder: "TargetByTarget");
        var postConfigure = new ProductDefaultsPostConfigureOptions();

        postConfigure.PostConfigure(null, options);

        var targetGroup = options.Products!.First().TargetGroups!.First();
        targetGroup.TargetMigrationOrder.Should().Be("TargetByTarget");
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

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        // The invalid default should not be copied; the product keeps its null/empty value
        options.Products!.First().MigrationErrorAction.Should().BeNullOrEmpty();
    }

    [Fact]
    public void InvalidDefaultEnum_PostConfigure_FailsFastWithLocation()
    {
        // After merging, PostConfigure probes every enum getter so that DataAnnotation validation never
        // reflects over a getter that throws (#3).
        var options = CreateOptionsWithDefaults(migrationErrorAction: "InvalidAction");
        var postConfigure = new ProductDefaultsPostConfigureOptions();

        var act = () => postConfigure.PostConfigure(null, options);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*ProductDefaults: Invalid value [InvalidAction] for property [MigrationErrorAction]*");
    }

    [Theory]
    [InlineData("Undefined")]
    [InlineData("1")]
    [InlineData("Rollback,Ignore")]
    public void SentinelNumericOrFlagsDefault_IsNotCopied_AndReportedOnce(string value)
    {
        // Enum.TryParse(ignoreCase: true) would accept these; the merge gate must apply the same rule as the
        // getters, otherwise the invalid default is fanned out to every product and reported N+1 times.
        var options = CreateOptionsWithDefaults(migrationErrorAction: value);
        var postConfigure = new ProductDefaultsPostConfigureOptions();

        var act = () => postConfigure.PostConfigure(null, options);

        var message = act.Should().Throw<ConfigurationValidationException>().Which.Message;
        message.Should().Contain("1 enum-typed configuration value(s) could not be parsed");
        message.Should().Contain($"ProductDefaults: Invalid value [{value}] for property [MigrationErrorAction]");
        options.Products!.First().MigrationErrorAction.Should().BeNullOrEmpty();
    }

    [Fact]
    public void CaseVariantDefaultEnum_IsCopiedAndParses()
    {
        var options = CreateOptionsWithDefaults(migrationErrorAction: "rollback", targetMigrationOrder: "FILEBYFILE", hashValidationScope: "sqlblocks");
        var postConfigure = new ProductDefaultsPostConfigureOptions();

        postConfigure.PostConfigure(null, options);

        var product = options.Products!.First();
        product.MigrationErrorAction.Should().Be("rollback", "the raw string is copied verbatim");
        product.MigrationErrorActionEnum.Should().Be(MigrationErrorAction.Rollback);
        var targetGroup = product.TargetGroups!.First();
        targetGroup.TargetMigrationOrderEnum.Should().Be(TargetMigrationOrder.FileByFile);
        targetGroup.HashValidationScopeEnum.Should().Be(HashValidationScope.SqlBlocks);
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
                    TargetMigrationOrder = "TargetByTarget",
                    HashValidationScope = "File",
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
                    TargetMigrationOrder = "TargetByTarget",
                    HashValidationScope = "File",
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
                    TargetMigrationOrder = "TargetByTarget",
                    HashValidationScope = "File",
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
                    TargetMigrationOrder = "TargetByTarget",
                    HashValidationScope = "File",
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
                    TargetMigrationOrder = "TargetByTarget",
                    HashValidationScope = "File",
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
                    TargetMigrationOrder = "TargetByTarget",
                    HashValidationScope = "File",
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
                    TargetMigrationOrder = "TargetByTarget",
                    HashValidationScope = "File",
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
            .WithMessage("*NOT-A-VALID-ENCODING*")
            .WithMessage("*windows-1252*", "the message names valid encodings (#4)")
            .Which.Message.Should().NotContain("RegisterProvider", "the product registers the code-page provider itself (#4)");
    }

    #region MigrationFilesEncoding merge (#4)

    private static RayMigratorOptions CreateOptionsWithEncodings(string? defaultEncoding, string? productEncoding)
    {
        var options = CreateOptionsWithDefaults();
        options.ProductDefaults!.MigrationFilesEncoding = defaultEncoding;
        options.Products!.First().MigrationFilesEncoding = productEncoding;
        return options;
    }

    [Fact]
    public void CodePageDefaultEncoding_IsAcceptedAndCopiedToProductWithoutEncoding()
    {
        // windows-1252 used to throw here because the code-page provider was not registered (#4)
        var options = CreateOptionsWithEncodings("windows-1252", null);

        new ProductDefaultsPostConfigureOptions().PostConfigure(null, options);

        options.Products!.First().MigrationFilesEncoding.Should().Be("windows-1252");
    }

    [Fact]
    public void DefaultEncoding_DoesNotOverrideProductEncoding()
    {
        var options = CreateOptionsWithEncodings("windows-1252", "iso-8859-1");

        new ProductDefaultsPostConfigureOptions().PostConfigure(null, options);

        options.Products!.First().MigrationFilesEncoding.Should().Be("iso-8859-1");
    }

    [Theory]
    [InlineData("ANSI")]
    [InlineData("UTF-8-BOM")]
    public void NotAnEncodingNameAsDefault_ThrowsConfigurationValidationException(string encodingName)
    {
        var options = CreateOptionsWithEncodings(encodingName, null);

        var act = () => new ProductDefaultsPostConfigureOptions().PostConfigure(null, options);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage($"*{encodingName}*")
            .WithMessage("*windows-1252*");
    }

    #endregion
}
