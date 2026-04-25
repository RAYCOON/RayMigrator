using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Additional DefaultsPromoter tests for promotion paths not covered in DefaultsPromoterTests.
/// </summary>
public class DefaultsPromoterAdditionalTests
{
    // ── UseCliToolAlias at product level ──────────────────────────────────

    [Fact]
    public void Promote_UseCliToolAlias_AllProductsSame_PromotesToDefaults()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "sqlcmd" };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "sqlcmd" };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r => r.PropertyName == "UseCliToolAlias" && r.PromotedValue == "sqlcmd");
        model.ProductDefaults.UseCliToolAlias.Should().Be("sqlcmd");
        model.Products[0].UseCliToolAlias.IsOverridden.Should().BeFalse();
        model.Products[1].UseCliToolAlias.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public void Promote_UseCliToolAlias_MixedValues_DoesNotPromote()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "sqlcmd" };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "psql" };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().NotContain(r => r.PropertyName == "UseCliToolAlias");
    }

    // ── RollbackErrorAction promotion ─────────────────────────────────

    [Fact]
    public void Promote_RollbackErrorAction_AllProductsSame_Promotes()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.RollbackErrorAction = new OverridableValue<string> { IsOverridden = true, Value = "Ignore" };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.RollbackErrorAction = new OverridableValue<string> { IsOverridden = true, Value = "Ignore" };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r => r.PropertyName == "RollbackErrorAction" && r.PromotedValue == "Ignore");
        model.ProductDefaults.RollbackErrorAction.Should().Be("Ignore");
    }

    // ── MigrationFilesEncoding promotion ──────────────────────────────

    [Fact]
    public void Promote_MigrationFilesEncoding_AllProductsSame_Promotes()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.MigrationFilesEncoding = new OverridableValue<string> { IsOverridden = true, Value = "ASCII" };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.MigrationFilesEncoding = new OverridableValue<string> { IsOverridden = true, Value = "ASCII" };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r => r.PropertyName == "MigrationFilesEncoding");
        model.ProductDefaults.MigrationFilesEncoding.Should().Be("ASCII");
    }

    // ── DbCommandMaxRetries promotion ─────────────────────────────────

    [Fact]
    public void Promote_DbCommandMaxRetries_AllTargetsSame_Promotes()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.TargetGroups[0].Targets[0].DbCommandMaxRetries = new OverridableValue<int> { IsOverridden = true, Value = 3 };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.TargetGroups[0].Targets[0].DbCommandMaxRetries = new OverridableValue<int> { IsOverridden = true, Value = 3 };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r => r.PropertyName == "DbCommandMaxRetries");
        model.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandMaxRetries.Should().Be(3);
    }

    // ── DbCommandWaitTimeInMsBeforeRetry promotion ────────────────────

    [Fact]
    public void Promote_DbCommandWaitTime_AllTargetsSame_Promotes()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.TargetGroups[0].Targets[0].DbCommandWaitTimeInMsBeforeRetry = new OverridableValue<int> { IsOverridden = true, Value = 500 };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.TargetGroups[0].Targets[0].DbCommandWaitTimeInMsBeforeRetry = new OverridableValue<int> { IsOverridden = true, Value = 500 };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r => r.PropertyName == "DbCommandWaitTimeInMsBeforeRetry");
        model.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandWaitTimeInMsBeforeRetry.Should().Be(500);
    }

    // ── HashValidationScope promotion ─────────────────────────────────

    [Fact]
    public void Promote_HashValidationScope_AllTgsSame_Promotes()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.TargetGroups[0].HashValidationScope = new OverridableValue<string> { IsOverridden = true, Value = "SqlBlocks" };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.TargetGroups[0].HashValidationScope = new OverridableValue<string> { IsOverridden = true, Value = "SqlBlocks" };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r => r.PropertyName == "HashValidationScope");
        model.ProductDefaults.TargetGroupDefaults.HashValidationScope.Should().Be("SqlBlocks");
    }

    // ── PromotionResult properties ────────────────────────────────────

    [Fact]
    public void Promote_AffectedProductsCount_IsSetCorrectly()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        for (int i = 1; i <= 3; i++)
        {
            var p = TestModelFactory.CreateValidProduct($"App{i}");
            p.MigrationErrorAction = new OverridableValue<string> { IsOverridden = true, Value = "Rollback" };
            model.Products.Add(p);
        }

        var results = DefaultsPromoter.Promote(model);
        var promotion = results.First(r => r.PropertyName == "MigrationErrorAction");

        promotion.AffectedProducts.Should().Be(3);
        promotion.Level.Should().Be("ProductDefaults");
    }

    // ── StopRollbackOnMissingRollbackFile at product level ────────────

    [Fact]
    public void Promote_StopRollbackOnMissingRollbackFile_AllProductsOverrideTrue_PromotesToProductDefaults()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.StopRollbackOnMissingRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = true };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.StopRollbackOnMissingRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = true };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r => r.PropertyName == "StopRollbackOnMissingRollbackFile" && r.PromotedValue == "True");
        model.ProductDefaults.StopRollbackOnMissingRollbackFile.Should().BeTrue();
        model.Products[0].StopRollbackOnMissingRollbackFile.IsOverridden.Should().BeFalse();
        model.Products[1].StopRollbackOnMissingRollbackFile.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public void Promote_StopRollbackOnMissingRollbackFile_MixedValues_DoesNotPromote()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.StopRollbackOnMissingRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = true };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.StopRollbackOnMissingRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().NotContain(r => r.PropertyName == "StopRollbackOnMissingRollbackFile");
    }

    // ── StopRollbackOnMissingRollbackFile at target group level ──────

    [Fact]
    public void Promote_StopRollbackOnMissingRollbackFile_AllTargetGroupsOverrideTrue_PromotesToTargetGroupDefaults()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.TargetGroups[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.TargetGroups[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().Contain(r =>
            r.PropertyName == "StopRollbackOnMissingRollbackFile (TargetGroup)" && r.PromotedValue == "True");
        model.ProductDefaults.TargetGroupDefaults.StopRollbackOnMissingRollbackFile.Should().BeTrue();
        model.Products[0].TargetGroups[0].StopRollbackOnMissingRollbackFile.IsOverridden.Should().BeFalse();
        model.Products[1].TargetGroups[0].StopRollbackOnMissingRollbackFile.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public void Promote_StopRollbackOnMissingRollbackFile_MixedTargetGroupValues_DoesNotPromote()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();

        var p1 = TestModelFactory.CreateValidProduct("App1");
        p1.TargetGroups[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };
        model.Products.Add(p1);

        var p2 = TestModelFactory.CreateValidProduct("App2");
        p2.TargetGroups[0].StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = false };
        model.Products.Add(p2);

        var results = DefaultsPromoter.Promote(model);

        results.Should().NotContain(r => r.PropertyName == "StopRollbackOnMissingRollbackFile (TargetGroup)");
    }
}
