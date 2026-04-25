using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: ShouldSkipBlockSplitting tests.
/// CLI tools execute the entire file as a single unit — splitting is unnecessary.
/// Errors here would cause CLI-executed SQL files to be split incorrectly, breaking execution.
/// </summary>
public class ShouldSkipBlockSplittingTests
{
    private static ProductOptions BuildProductOptions(
        string targetGroupAlias = "Backend",
        List<TargetOptions>? targets = null)
    {
        return new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = targetGroupAlias,
                    DatabaseType = "SqlServer",
                    Targets = targets ?? new List<TargetOptions>
                    {
                        new() { Alias = "DB1", ConnectionString = "Server=localhost;" }
                    }
                }
            }
        };
    }

    [Fact]
    public void FileUseCliToolAlias_Set_ReturnsTrue()
    {
        var productOptions = BuildProductOptions();

        var result = MigrationService.ShouldSkipBlockSplitting("sqlcmd", "Backend", productOptions);

        result.Should().BeTrue();
    }

    [Fact]
    public void FileUseCliToolAlias_Null_AllTargetsHaveCliAlias_ReturnsTrue()
    {
        var productOptions = new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetOptions>
                    {
                        new() { Alias = "DB1", UseCliToolAlias = "sqlcmd" },
                        new() { Alias = "DB2", UseCliToolAlias = "sqlcmd" }
                    }
                }
            }
        };

        var result = MigrationService.ShouldSkipBlockSplitting(null, "Backend", productOptions);

        result.Should().BeTrue();
    }

    [Fact]
    public void FileUseCliToolAlias_Null_SomeTargetsHaveCliAlias_ReturnsFalse()
    {
        var productOptions = new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetOptions>
                    {
                        new() { Alias = "DB1", UseCliToolAlias = "sqlcmd" },
                        new() { Alias = "DB2", UseCliToolAlias = null }
                    }
                }
            }
        };

        var result = MigrationService.ShouldSkipBlockSplitting(null, "Backend", productOptions);

        result.Should().BeFalse();
    }

    [Fact]
    public void FileUseCliToolAlias_Null_NoTargetsHaveCliAlias_ReturnsFalse()
    {
        var productOptions = new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetOptions>
                    {
                        new() { Alias = "DB1", UseCliToolAlias = null },
                        new() { Alias = "DB2", UseCliToolAlias = null }
                    }
                }
            }
        };

        var result = MigrationService.ShouldSkipBlockSplitting(null, "Backend", productOptions);

        result.Should().BeFalse();
    }

    [Fact]
    public void FileUseCliToolAlias_Null_EmptyTargetsList_ReturnsFalse()
    {
        var productOptions = new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetOptions>()
                }
            }
        };

        var result = MigrationService.ShouldSkipBlockSplitting(null, "Backend", productOptions);

        result.Should().BeFalse();
    }

    [Fact]
    public void FileUseCliToolAlias_Null_TargetGroupNotFound_ReturnsFalse()
    {
        var productOptions = BuildProductOptions(targetGroupAlias: "Backend");

        var result = MigrationService.ShouldSkipBlockSplitting(null, "NonExistent", productOptions);

        result.Should().BeFalse();
    }

    [Fact]
    public void FileUseCliToolAlias_EmptyString_ReturnsFalse()
    {
        var productOptions = new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetOptions>
                    {
                        new() { Alias = "DB1", UseCliToolAlias = null }
                    }
                }
            }
        };

        var result = MigrationService.ShouldSkipBlockSplitting("", "Backend", productOptions);

        result.Should().BeFalse();
    }

    [Fact]
    public void FileUseCliToolAlias_Whitespace_ReturnsFalse()
    {
        var productOptions = new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = "Backend",
                    Targets = new List<TargetOptions>
                    {
                        new() { Alias = "DB1", UseCliToolAlias = null }
                    }
                }
            }
        };

        var result = MigrationService.ShouldSkipBlockSplitting("   ", "Backend", productOptions);

        result.Should().BeFalse();
    }

    [Fact]
    public void FileUseCliToolAlias_Null_NullTargetGroups_ReturnsFalse()
    {
        var productOptions = new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = null
        };

        var result = MigrationService.ShouldSkipBlockSplitting(null, "Backend", productOptions);

        result.Should().BeFalse();
    }

    [Fact]
    public void FileUseCliToolAlias_Set_TargetGroupNotFound_StillReturnsTrue()
    {
        var productOptions = BuildProductOptions(targetGroupAlias: "Backend");

        var result = MigrationService.ShouldSkipBlockSplitting("sqlcmd", "NonExistent", productOptions);

        result.Should().BeTrue();
    }
}
