using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2-4: Lazy-caching enum property tests on Options classes.
/// Tests the pattern: string property -> Enum.TryParse -> cached enum property with Undefined fallback.
/// </summary>
public class OptionsEnumPropertyTests
{
    #region TargetGroupOptions.TargetMigrationOrderEnum

    [Fact]
    public void TargetMigrationOrderEnum_ValidString_ReturnsCorrectEnum()
    {
        var options = new TargetGroupOptions { TargetMigrationOrder = "Simultaneously" };

        options.TargetMigrationOrderEnum.Should().Be(TargetMigrationOrder.Simultaneously);
    }

    [Fact]
    public void TargetMigrationOrderEnum_InvalidString_ReturnsUndefined()
    {
        var options = new TargetGroupOptions { TargetMigrationOrder = "InvalidValue" };

        options.TargetMigrationOrderEnum.Should().Be(TargetMigrationOrder.Undefined);
    }

    [Fact]
    public void TargetMigrationOrderEnum_NullString_ReturnsUndefined()
    {
        var options = new TargetGroupOptions { TargetMigrationOrder = null };

        options.TargetMigrationOrderEnum.Should().Be(TargetMigrationOrder.Undefined);
    }

    [Fact]
    public void TargetMigrationOrderEnum_SecondAccess_ReturnsCachedValue()
    {
        var options = new TargetGroupOptions { TargetMigrationOrder = "Successively" };

        var first = options.TargetMigrationOrderEnum;
        var second = options.TargetMigrationOrderEnum;

        first.Should().Be(TargetMigrationOrder.Successively);
        second.Should().Be(first);
    }

    #endregion

    #region TargetGroupOptions.HashValidationScopeEnum

    [Fact]
    public void HashValidationScopeEnum_ValidString_ReturnsCorrectEnum()
    {
        var options = new TargetGroupOptions { HashValidationScope = "File" };

        options.HashValidationScopeEnum.Should().Be(HashValidationScope.File);
    }

    [Fact]
    public void HashValidationScopeEnum_InvalidString_ReturnsUndefined()
    {
        var options = new TargetGroupOptions { HashValidationScope = "NotAScope" };

        options.HashValidationScopeEnum.Should().Be(HashValidationScope.Undefined);
    }

    #endregion
}
