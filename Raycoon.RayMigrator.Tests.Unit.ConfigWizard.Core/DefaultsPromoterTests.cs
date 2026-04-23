// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class DefaultsPromoterTests
{
    [Fact]
    public void Promote_NoProducts_ReturnsEmpty()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var results = DefaultsPromoter.Promote(model);
        results.Should().BeEmpty();
    }

    [Fact]
    public void Promote_NoOverrides_ReturnsEmpty()
    {
        var model = TestModelFactory.CreateValidModel();
        var results = DefaultsPromoter.Promote(model);
        results.Should().BeEmpty();
    }

    [Fact]
    public void Promote_AllProductsSameOverride_PromotesToDefaults()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();
        model.Products.Add(CreateProductWithOverride("App1", "Rollback"));
        model.Products.Add(CreateProductWithOverride("App2", "Rollback"));

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r => r.PropertyName == "MigrationErrorAction" && r.PromotedValue == "Rollback");
        model.ProductDefaults.MigrationErrorAction.Should().Be("Rollback");
        model.Products[0].MigrationErrorAction.IsOverridden.Should().BeFalse();
        model.Products[1].MigrationErrorAction.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public void Promote_MixedOverrides_DoesNotPromote()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();
        model.Products.Add(CreateProductWithOverride("App1", "Rollback"));
        model.Products.Add(CreateProductWithOverride("App2", "Terminate"));

        var results = DefaultsPromoter.Promote(model);

        results.Should().NotContain(r => r.PropertyName == "MigrationErrorAction");
        model.Products[0].MigrationErrorAction.IsOverridden.Should().BeTrue();
    }

    [Fact]
    public void Promote_SingleProduct_AllOverridden_Promotes()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();
        model.Products.Add(CreateProductWithOverride("App1", "Ignore"));

        var results = DefaultsPromoter.Promote(model);
        results.Should().Contain(r => r.PropertyName == "MigrationErrorAction");
    }

    [Fact]
    public void Promote_PartialOverride_DoesNotPromote()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();
        model.Products.Add(CreateProductWithOverride("App1", "Rollback"));
        model.Products.Add(TestModelFactory.CreateValidProduct("App2")); // no override

        var results = DefaultsPromoter.Promote(model);
        results.Should().NotContain(r => r.PropertyName == "MigrationErrorAction");
    }

    [Fact]
    public void Promote_TargetGroupLevel_TargetMigrationOrder()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.TargetGroups[0].TargetMigrationOrder = new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.TargetGroups[0].TargetMigrationOrder = new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);
        results.Should().Contain(r => r.PropertyName == "TargetMigrationOrder" && r.Level == "TargetGroupDefaults");
        model.ProductDefaults.TargetGroupDefaults.TargetMigrationOrder.Should().Be("Simultaneously");
    }

    [Fact]
    public void Promote_TargetLevel_Timeout()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds = new OverridableValue<int> { IsOverridden = true, Value = 60 };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.TargetGroups[0].Targets[0].DbCommandTimeoutInSeconds = new OverridableValue<int> { IsOverridden = true, Value = 60 };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);
        results.Should().Contain(r => r.PropertyName == "DbCommandTimeoutInSeconds");
        model.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds.Should().Be(60);
    }

    [Fact]
    public void Promote_BoolOverride_RequireRollbackFile()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.RequireRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.RequireRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);
        results.Should().Contain(r => r.PropertyName == "RequireRollbackFile");
        model.ProductDefaults.RequireRollbackFile.Should().BeFalse();
    }

    private static ProductModel CreateProductWithOverride(string alias, string migrationErrorAction)
    {
        var product = TestModelFactory.CreateValidProduct(alias);
        product.MigrationErrorAction = new OverridableValue<string> { IsOverridden = true, Value = migrationErrorAction };
        return product;
    }
}
