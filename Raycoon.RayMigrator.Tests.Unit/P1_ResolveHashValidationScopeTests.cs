using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-2: ResolveHashValidationScope tests.
/// Verifies that the static helper correctly resolves the effective scope from ProductOptions.
/// </summary>
public class ResolveHashValidationScopeTests
{
    private static ProductOptions CreateProductOptions(string? hashValidationScope, string alias = "Backend")
    {
        var tg = new TargetGroupOptions { Alias = alias, DatabaseType = "SqlServer" };
        if (hashValidationScope != null)
            tg.HashValidationScope = hashValidationScope;

        return new ProductOptions
        {
            Alias = "TestProduct",
            TargetGroups = new List<TargetGroupOptions> { tg }
        };
    }

    [Fact]
    public void ExplicitSqlBlocksScope_IsReturned()
    {
        var result = MigrationService.ResolveHashValidationScope("Backend", CreateProductOptions("SqlBlocks"));

        result.Should().Be(HashValidationScope.SqlBlocks);
    }

    [Fact]
    public void ExplicitDisabledScope_IsReturned()
    {
        var result = MigrationService.ResolveHashValidationScope("Backend", CreateProductOptions("Disabled"));

        result.Should().Be(HashValidationScope.Disabled);
    }

    [Fact]
    public void ExplicitFileScope_IsReturned()
    {
        var result = MigrationService.ResolveHashValidationScope("Backend", CreateProductOptions("File"));

        result.Should().Be(HashValidationScope.File);
    }

    [Fact]
    public void TargetGroupNotFound_FallsBackToFile()
    {
        var result = MigrationService.ResolveHashValidationScope("Unknown", CreateProductOptions("SqlBlocks"));

        result.Should().Be(HashValidationScope.File);
    }

    [Fact]
    public void UndefinedScope_FallsBackToFile()
    {
        // No HashValidationScope set → property defaults to null → enum parses as Undefined
        var result = MigrationService.ResolveHashValidationScope("Backend", CreateProductOptions(null));

        result.Should().Be(HashValidationScope.File);
    }

    [Fact]
    public void CaseInsensitiveAlias_Matches()
    {
        var result = MigrationService.ResolveHashValidationScope("backend", CreateProductOptions("SqlBlocks", "Backend"));

        result.Should().Be(HashValidationScope.SqlBlocks);
    }

    [Fact]
    public void NullTargetGroups_FallsBackToFile()
    {
        var productOptions = new ProductOptions { Alias = "TestProduct", TargetGroups = null };

        var result = MigrationService.ResolveHashValidationScope("Backend", productOptions);

        result.Should().Be(HashValidationScope.File);
    }
}
