using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2-4: Lazy-caching enum property tests on Options classes.
/// Tests the pattern: string property -> case-insensitive parse -> cached enum property. Null falls back to
/// Undefined, an unparseable value throws (see OptionsEnumCaseInsensitivityTests for the full matrix).
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
    public void TargetMigrationOrderEnum_InvalidString_Throws()
    {
        var options = new TargetGroupOptions { TargetMigrationOrder = "InvalidValue" };

        var act = () => options.TargetMigrationOrderEnum;

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Invalid value [InvalidValue] for property [TargetMigrationOrder]. Allowed values: [Simultaneously, Successively].*");
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
    public void HashValidationScopeEnum_InvalidString_Throws()
    {
        var options = new TargetGroupOptions { HashValidationScope = "NotAScope" };

        var act = () => options.HashValidationScopeEnum;

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Invalid value [NotAScope] for property [HashValidationScope]. Allowed values: [File, SqlBlocks, Disabled].*");
    }

    #endregion
}
