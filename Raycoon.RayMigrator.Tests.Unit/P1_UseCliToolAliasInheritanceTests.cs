using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: UseCliToolAlias inheritance cascade tests.
/// ProductDefaultsPostConfigureOptions must propagate UseCliToolAlias from
/// ProductDefaults -> Product -> TargetGroup -> Target.
/// Failures here mean the CLI tool is silently ignored at runtime.
/// </summary>
public class UseCliToolAliasInheritanceTests
{
    private static RayMigratorOptions BuildOptions(
        string? defaultsAlias = null,
        string? productAlias = null,
        string? targetGroupAlias = null,
        string? targetAlias = null)
    {
        return new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions
            {
                MigrationErrorAction = "Terminate",
                UseCliToolAlias = defaultsAlias,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "FileHash",
                    TargetDefaults = new TargetDefaultsOptions
                    {
                        DbCommandTimeoutInSeconds = 20,
                        DbCommandMaxRetries = 0,
                        DbCommandWaitTimeInMsBeforeRetry = 250
                    }
                }
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    UseCliToolAlias = productAlias,
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            UseCliToolAlias = targetGroupAlias,
                            Targets = new List<TargetOptions>
                            {
                                new TargetOptions
                                {
                                    Alias = "MainDB",
                                    ConnectionString = "Server=localhost;Database=Test;",
                                    UseCliToolAlias = targetAlias
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    [Fact]
    public void UseCliToolAlias_CascadesFromProductDefaults_ToProduct()
    {
        var options = BuildOptions(defaultsAlias: "sqlcmd");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].UseCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void UseCliToolAlias_CascadesFromProduct_ToTargetGroup()
    {
        var options = BuildOptions(productAlias: "sqlcmd");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].TargetGroups![0].UseCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void UseCliToolAlias_CascadesFromTargetGroup_ToTarget()
    {
        var options = BuildOptions(targetGroupAlias: "sqlcmd");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].TargetGroups![0].Targets![0].UseCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void UseCliToolAlias_ProductLevel_DoesNotOverrideExplicitTargetGroupValue()
    {
        var options = BuildOptions(productAlias: "sqlcmd", targetGroupAlias: "psql");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].TargetGroups![0].UseCliToolAlias.Should().Be("psql");
    }

    [Fact]
    public void UseCliToolAlias_TargetGroupLevel_DoesNotOverrideExplicitTargetValue()
    {
        var options = BuildOptions(targetGroupAlias: "sqlcmd", targetAlias: "mariadb-cli");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].TargetGroups![0].Targets![0].UseCliToolAlias.Should().Be("mariadb-cli");
    }

    [Fact]
    public void UseCliToolAlias_ProductDefaults_DoesNotOverrideExplicitProductValue()
    {
        var options = BuildOptions(defaultsAlias: "sqlcmd", productAlias: "psql");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].UseCliToolAlias.Should().Be("psql");
    }

    [Fact]
    public void UseCliToolAlias_NullAtAllLevels_RemainsNull()
    {
        var options = BuildOptions(
            defaultsAlias: null,
            productAlias: null,
            targetGroupAlias: null,
            targetAlias: null);

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].UseCliToolAlias.Should().BeNull();
        options.Products![0].TargetGroups![0].UseCliToolAlias.Should().BeNull();
        options.Products![0].TargetGroups![0].Targets![0].UseCliToolAlias.Should().BeNull();
    }

    [Fact]
    public void UseCliToolAlias_FullCascadeChain_ProductDefaultsReachesTarget()
    {
        var options = BuildOptions(defaultsAlias: "sqlcmd");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        var target = options.Products![0].TargetGroups![0].Targets![0];
        target.UseCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void UseCliToolAlias_MultipleTargetGroups_EachReceivesCascadedValue()
    {
        var options = BuildOptions(productAlias: "sqlcmd");
        options.Products![0].TargetGroups!.Add(new TargetGroupOptions
        {
            Alias = "Frontend",
            DatabaseType = "PostgreSQL",
            Targets = new List<TargetOptions>
            {
                new TargetOptions
                {
                    Alias = "FrontDB",
                    ConnectionString = "Host=localhost;Database=Front;"
                }
            }
        });

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].TargetGroups![0].UseCliToolAlias.Should().Be("sqlcmd");
        options.Products![0].TargetGroups![1].UseCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void UseCliToolAlias_MultipleTargets_EachReceivesCascadedValue()
    {
        var options = BuildOptions(targetGroupAlias: "sqlcmd");
        options.Products![0].TargetGroups![0].Targets!.Add(new TargetOptions
        {
            Alias = "SecondDB",
            ConnectionString = "Server=localhost2;Database=Test2;"
        });

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].TargetGroups![0].Targets![0].UseCliToolAlias.Should().Be("sqlcmd");
        options.Products![0].TargetGroups![0].Targets![1].UseCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void UseCliToolAlias_EmptyStringAtProductLevel_CascadesFromDefaults()
    {
        // Empty string is treated the same as null for IsNullOrWhiteSpace check
        var options = BuildOptions(defaultsAlias: "sqlcmd", productAlias: "");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].UseCliToolAlias.Should().Be("sqlcmd");
    }

    [Fact]
    public void UseCliToolAlias_WhitespaceAtTargetGroupLevel_CascadesFromProduct()
    {
        var options = BuildOptions(productAlias: "sqlcmd", targetGroupAlias: "   ");

        ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        options.Products![0].TargetGroups![0].UseCliToolAlias.Should().Be("sqlcmd");
    }
}
