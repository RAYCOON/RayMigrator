using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class InheritanceResolverTests
{
    private readonly ProductDefaultsModel _defaults = new();

    [Fact]
    public void GetEffectiveMigrationErrorAction_NotOverridden_ReturnsDefault()
    {
        var product = new ProductModel();
        InheritanceResolver.GetEffectiveMigrationErrorAction(product, _defaults)
            .Should().Be("Terminate");
    }

    [Fact]
    public void GetEffectiveMigrationErrorAction_Overridden_ReturnsOverride()
    {
        var product = new ProductModel
        {
            MigrationErrorAction = new OverridableValue<string> { IsOverridden = true, Value = "Rollback" }
        };
        InheritanceResolver.GetEffectiveMigrationErrorAction(product, _defaults)
            .Should().Be("Rollback");
    }

    [Fact]
    public void GetEffectiveTargetMigrationOrder_NotOverridden_ReturnsDefault()
    {
        var tg = new TargetGroupModel();
        InheritanceResolver.GetEffectiveTargetMigrationOrder(tg, _defaults)
            .Should().Be("Successively");
    }

    [Fact]
    public void GetEffectiveTimeout_NotOverridden_ReturnsDefault()
    {
        var target = new TargetModel();
        InheritanceResolver.GetEffectiveTimeout(target, _defaults)
            .Should().Be(20);
    }

    [Fact]
    public void GetEffectiveTimeout_Overridden_ReturnsOverride()
    {
        var target = new TargetModel
        {
            DbCommandTimeoutInSeconds = new OverridableValue<int> { IsOverridden = true, Value = 60 }
        };
        InheritanceResolver.GetEffectiveTimeout(target, _defaults)
            .Should().Be(60);
    }

    // ── UseCliToolAlias 4-level inheritance ──────────────────────────

    [Fact]
    public void GetEffectiveUseCliToolAlias_AllNull_ReturnsNull()
    {
        var target = new TargetModel();
        var tg = new TargetGroupModel();
        var product = new ProductModel();
        var defaults = new ProductDefaultsModel();

        InheritanceResolver.GetEffectiveUseCliToolAlias(target, tg, product, defaults)
            .Should().BeNull();
    }

    [Fact]
    public void GetEffectiveUseCliToolAlias_DefaultsSet_ReturnsDefaults()
    {
        var target = new TargetModel();
        var tg = new TargetGroupModel();
        var product = new ProductModel();
        var defaults = new ProductDefaultsModel { UseCliToolAlias = "sqlcmd" };

        InheritanceResolver.GetEffectiveUseCliToolAlias(target, tg, product, defaults)
            .Should().Be("sqlcmd");
    }

    [Fact]
    public void GetEffectiveUseCliToolAlias_ProductOverrides_ReturnsProductValue()
    {
        var target = new TargetModel();
        var tg = new TargetGroupModel();
        var product = new ProductModel
        {
            UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "psql" }
        };
        var defaults = new ProductDefaultsModel { UseCliToolAlias = "sqlcmd" };

        InheritanceResolver.GetEffectiveUseCliToolAlias(target, tg, product, defaults)
            .Should().Be("psql");
    }

    [Fact]
    public void GetEffectiveUseCliToolAlias_TargetGroupOverrides_ReturnsTargetGroupValue()
    {
        var target = new TargetModel();
        var tg = new TargetGroupModel
        {
            UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "mariadb" }
        };
        var product = new ProductModel
        {
            UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "psql" }
        };
        var defaults = new ProductDefaultsModel { UseCliToolAlias = "sqlcmd" };

        InheritanceResolver.GetEffectiveUseCliToolAlias(target, tg, product, defaults)
            .Should().Be("mariadb");
    }

    [Fact]
    public void GetEffectiveUseCliToolAlias_TargetOverrides_ReturnsTargetValue()
    {
        var target = new TargetModel
        {
            UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "custom" }
        };
        var tg = new TargetGroupModel
        {
            UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "mariadb" }
        };
        var product = new ProductModel();
        var defaults = new ProductDefaultsModel { UseCliToolAlias = "sqlcmd" };

        InheritanceResolver.GetEffectiveUseCliToolAlias(target, tg, product, defaults)
            .Should().Be("custom");
    }

    // ── Effective Config Entries ──────────────────────────────────

    [Fact]
    public void GetEffectiveTargetConfig_ReturnsAllEntries()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();
        var defaults = new ProductDefaultsModel();

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, defaults);

        entries.Should().Contain(e => e.Property == "Alias");
        entries.Should().Contain(e => e.Property == "ConnectionString");
        entries.Should().Contain(e => e.Property == "DatabaseType");
        entries.Should().Contain(e => e.Property == "UseCliToolAlias");
    }
}
