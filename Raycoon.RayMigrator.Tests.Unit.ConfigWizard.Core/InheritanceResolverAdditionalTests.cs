// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Additional InheritanceResolver tests for methods not covered in InheritanceResolverTests.
/// </summary>
public class InheritanceResolverAdditionalTests
{
    private readonly ProductDefaultsModel _defaults = new();

    // ── Product-level methods ─────────────────────────────────────────

    [Fact]
    public void GetEffectiveRollbackErrorAction_NotOverridden_ReturnsDefault()
    {
        var product = new ProductModel();
        InheritanceResolver.GetEffectiveRollbackErrorAction(product, _defaults)
            .Should().Be("Terminate");
    }

    [Fact]
    public void GetEffectiveRollbackErrorAction_Overridden_ReturnsOverride()
    {
        var product = new ProductModel
        {
            RollbackErrorAction = new OverridableValue<string> { IsOverridden = true, Value = "Ignore" }
        };
        InheritanceResolver.GetEffectiveRollbackErrorAction(product, _defaults)
            .Should().Be("Ignore");
    }

    [Fact]
    public void GetEffectiveMigrationFilesExtension_NotOverridden_ReturnsDefault()
    {
        var product = new ProductModel();
        InheritanceResolver.GetEffectiveMigrationFilesExtension(product, _defaults)
            .Should().Be("sql");
    }

    [Fact]
    public void GetEffectiveMigrationFilesExtension_Overridden_ReturnsOverride()
    {
        var product = new ProductModel
        {
            MigrationFilesExtension = new OverridableValue<string> { IsOverridden = true, Value = "psql" }
        };
        InheritanceResolver.GetEffectiveMigrationFilesExtension(product, _defaults)
            .Should().Be("psql");
    }

    [Fact]
    public void GetEffectiveMigrationRollbackFilesPreExtension_NotOverridden_ReturnsDefault()
    {
        var product = new ProductModel();
        InheritanceResolver.GetEffectiveMigrationRollbackFilesPreExtension(product, _defaults)
            .Should().Be("rollback");
    }

    [Fact]
    public void GetEffectiveMigrationFilesEncoding_NotOverridden_ReturnsDefault()
    {
        var product = new ProductModel();
        InheritanceResolver.GetEffectiveMigrationFilesEncoding(product, _defaults)
            .Should().Be("UTF-8");
    }

    [Fact]
    public void GetEffectiveMigrationFilesEncoding_Overridden_ReturnsOverride()
    {
        var product = new ProductModel
        {
            MigrationFilesEncoding = new OverridableValue<string> { IsOverridden = true, Value = "ASCII" }
        };
        InheritanceResolver.GetEffectiveMigrationFilesEncoding(product, _defaults)
            .Should().Be("ASCII");
    }

    [Fact]
    public void GetEffectiveRequireRollbackFile_NotOverridden_ReturnsDefault()
    {
        var product = new ProductModel();
        InheritanceResolver.GetEffectiveRequireRollbackFile(product, _defaults)
            .Should().BeTrue();
    }

    [Fact]
    public void GetEffectiveRequireRollbackFile_Overridden_ReturnsOverride()
    {
        var product = new ProductModel
        {
            RequireRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false }
        };
        InheritanceResolver.GetEffectiveRequireRollbackFile(product, _defaults)
            .Should().BeFalse();
    }

    [Fact]
    public void GetEffectiveStopRollbackOnMissingRollbackFile_NotOverridden_ReturnsDefault()
    {
        var product = new ProductModel();
        InheritanceResolver.GetEffectiveStopRollbackOnMissingRollbackFile(product, _defaults)
            .Should().BeTrue();
    }

    [Fact]
    public void GetEffectiveStopRollbackOnMissingRollbackFile_Overridden_ReturnsOverride()
    {
        var product = new ProductModel
        {
            StopRollbackOnMissingRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false }
        };
        InheritanceResolver.GetEffectiveStopRollbackOnMissingRollbackFile(product, _defaults)
            .Should().BeFalse();
    }

    [Fact]
    public void GetEffectiveStopRollbackOnMissingRollbackFile_DefaultFalse_NotOverridden_ReturnsDefault()
    {
        var defaults = new ProductDefaultsModel { StopRollbackOnMissingRollbackFile = false };
        var product = new ProductModel();
        InheritanceResolver.GetEffectiveStopRollbackOnMissingRollbackFile(product, defaults)
            .Should().BeFalse();
    }

    // ── TargetGroup-level methods ─────────────────────────────────────

    [Fact]
    public void GetEffectiveHashValidationScope_NotOverridden_ReturnsDefault()
    {
        var tg = new TargetGroupModel();
        InheritanceResolver.GetEffectiveHashValidationScope(tg, _defaults)
            .Should().Be("File");
    }

    [Fact]
    public void GetEffectiveHashValidationScope_Overridden_ReturnsOverride()
    {
        var tg = new TargetGroupModel
        {
            HashValidationScope = new OverridableValue<string> { IsOverridden = true, Value = "SqlBlocks" }
        };
        InheritanceResolver.GetEffectiveHashValidationScope(tg, _defaults)
            .Should().Be("SqlBlocks");
    }

    [Fact]
    public void GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup_NotOverridden_ReturnsDefault()
    {
        var tg = new TargetGroupModel();
        InheritanceResolver.GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup(tg, _defaults)
            .Should().BeTrue();
    }

    [Fact]
    public void GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup_Overridden_ReturnsOverride()
    {
        var tg = new TargetGroupModel
        {
            StopRollbackOnMissingRollbackFile = new OverridableValue<bool> { IsOverridden = true, Value = false }
        };
        InheritanceResolver.GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup(tg, _defaults)
            .Should().BeFalse();
    }

    [Fact]
    public void GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup_DefaultFalse_NotOverridden_ReturnsDefault()
    {
        var defaults = new ProductDefaultsModel();
        defaults.TargetGroupDefaults.StopRollbackOnMissingRollbackFile = false;
        var tg = new TargetGroupModel();
        InheritanceResolver.GetEffectiveStopRollbackOnMissingRollbackFileForTargetGroup(tg, defaults)
            .Should().BeFalse();
    }

    // ── Target-level methods ──────────────────────────────────────────

    [Fact]
    public void GetEffectiveMaxRetries_NotOverridden_ReturnsDefault()
    {
        var target = new TargetModel();
        InheritanceResolver.GetEffectiveMaxRetries(target, _defaults)
            .Should().Be(0);
    }

    [Fact]
    public void GetEffectiveMaxRetries_Overridden_ReturnsOverride()
    {
        var target = new TargetModel
        {
            DbCommandMaxRetries = new OverridableValue<int> { IsOverridden = true, Value = 5 }
        };
        InheritanceResolver.GetEffectiveMaxRetries(target, _defaults)
            .Should().Be(5);
    }

    [Fact]
    public void GetEffectiveWaitTime_NotOverridden_ReturnsDefault()
    {
        var target = new TargetModel();
        InheritanceResolver.GetEffectiveWaitTime(target, _defaults)
            .Should().Be(250);
    }

    [Fact]
    public void GetEffectiveWaitTime_Overridden_ReturnsOverride()
    {
        var target = new TargetModel
        {
            DbCommandWaitTimeInMsBeforeRetry = new OverridableValue<int> { IsOverridden = true, Value = 1000 }
        };
        InheritanceResolver.GetEffectiveWaitTime(target, _defaults)
            .Should().Be(1000);
    }

    // ── GetEffectiveTargetConfig source tracking ──────────────────────

    [Fact]
    public void GetEffectiveTargetConfig_AllDefaults_AllSourcesAreDefault()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();
        var defaults = new ProductDefaultsModel();

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, defaults);

        entries.Should().Contain(e => e.Property == "DbCommandTimeoutInSeconds" && e.Source == "default");
        entries.Should().Contain(e => e.Property == "TargetMigrationOrder" && e.Source == "default");
        entries.Should().Contain(e => e.Property == "MigrationErrorAction" && e.Source == "default");
    }

    [Fact]
    public void GetEffectiveTargetConfig_TargetOverridesTimeout_SourceIsOverride()
    {
        var target = TestModelFactory.CreateValidTarget();
        target.DbCommandTimeoutInSeconds = new OverridableValue<int> { IsOverridden = true, Value = 60 };
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, new ProductDefaultsModel());

        entries.Should().Contain(e => e.Property == "DbCommandTimeoutInSeconds" && e.Source == "override");
    }

    [Fact]
    public void GetEffectiveTargetConfig_ProductOverridesMigrationErrorAction_SourceContainsProduct()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();
        product.MigrationErrorAction = new OverridableValue<string> { IsOverridden = true, Value = "Rollback" };

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, new ProductDefaultsModel());

        entries.Should().Contain(e => e.Property == "MigrationErrorAction" && e.Source.Contains("Product"));
    }

    [Fact]
    public void GetEffectiveTargetConfig_NoCliAlias_SourceIsNotSet()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();
        var defaults = new ProductDefaultsModel { UseCliToolAlias = null };

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, defaults);

        entries.Should().Contain(e => e.Property == "UseCliToolAlias" && e.Source == "not set");
    }

    [Fact]
    public void GetEffectiveTargetConfig_DefaultCliAlias_SourceIsDefault()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();
        var defaults = new ProductDefaultsModel { UseCliToolAlias = "sqlcmd" };

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, defaults);

        entries.Should().Contain(e => e.Property == "UseCliToolAlias" && e.Source == "default");
    }

    [Fact]
    public void GetEffectiveTargetConfig_IncludesStopRollbackAtProductLevel()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();
        var defaults = new ProductDefaultsModel();

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, defaults);

        entries.Should().Contain(e => e.Property == "StopRollbackOnMissingRollbackFile (Product)");
    }

    [Fact]
    public void GetEffectiveTargetConfig_IncludesStopRollbackAtTargetGroupLevel()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();
        var defaults = new ProductDefaultsModel();

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, defaults);

        entries.Should().Contain(e => e.Property == "StopRollbackOnMissingRollbackFile (TargetGroup)");
    }

    [Fact]
    public void GetEffectiveTargetConfig_ProductStopRollbackOverriddenTrue_SourceIsOverrideAtProduct()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        var product = TestModelFactory.CreateValidProduct();
        product.StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, new ProductDefaultsModel());

        entries.Should().Contain(e =>
            e.Property == "StopRollbackOnMissingRollbackFile (Product)" &&
            e.Value == "True" &&
            e.Source == "override at Product");
    }

    [Fact]
    public void GetEffectiveTargetConfig_TargetGroupStopRollbackOverriddenTrue_SourceIsOverride()
    {
        var target = TestModelFactory.CreateValidTarget();
        var tg = TestModelFactory.CreateValidTargetGroup();
        tg.StopRollbackOnMissingRollbackFile =
            new OverridableValue<bool> { IsOverridden = true, Value = true };
        var product = TestModelFactory.CreateValidProduct();

        var entries = InheritanceResolver.GetEffectiveTargetConfig(target, tg, product, new ProductDefaultsModel());

        entries.Should().Contain(e =>
            e.Property == "StopRollbackOnMissingRollbackFile (TargetGroup)" &&
            e.Value == "True" &&
            e.Source == "override");
    }
}
