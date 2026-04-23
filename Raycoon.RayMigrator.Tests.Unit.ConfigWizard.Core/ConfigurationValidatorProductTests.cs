// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Tests for ValidateProduct, ValidateTargetGroup (structural errors),
/// ValidateProductDefaults, and ValidateSerilog paths not covered elsewhere.
/// </summary>
public class ConfigurationValidatorProductTests
{
    // ── ValidateProduct ──────────────────────────────────────────────

    [Fact]
    public void ValidateProduct_ValidProduct_NoErrors()
    {
        var product = TestModelFactory.CreateValidProduct();
        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateProduct_EmptyAlias_ReportsError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.Alias = "";
        var result = ConfigurationValidator.ValidateProduct(product, "Products > ");
        result.Errors.Should().Contain(e => e.Path.Contains("Alias"));
    }

    [Fact]
    public void ValidateProduct_InvalidAliasPattern_ReportsError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.Alias = "has space";
        var result = ConfigurationValidator.ValidateProduct(product, "Products > has space");
        result.Errors.Should().Contain(e => e.Path.Contains("Alias"));
    }

    [Fact]
    public void ValidateProduct_EmptyMigrationFilesRootDirectory_ReportsError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.MigrationFilesRootDirectory = "";
        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");
        result.Errors.Should().Contain(e => e.Path.Contains("MigrationFilesRootDirectory"));
    }

    [Fact]
    public void ValidateProduct_NoTargetGroups_ReportsError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.TargetGroups.Clear();
        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");
        result.Errors.Should().Contain(e => e.Path.Contains("TargetGroups"));
    }

    // ── TargetGroupMigrationOrder validation ─────────────────────────

    [Fact]
    public void ValidateProduct_TargetGroupMigrationOrder_ValidAliases_NoError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.TargetGroups.Add(TestModelFactory.CreateValidTargetGroup("Frontend"));
        product.TargetGroupMigrationOrder = "Backend,Frontend";

        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");

        result.Errors.Should().NotContain(e => e.Path.Contains("TargetGroupMigrationOrder"));
    }

    [Fact]
    public void ValidateProduct_TargetGroupMigrationOrder_MissingAlias_ReportsError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.TargetGroups.Add(TestModelFactory.CreateValidTargetGroup("Frontend"));
        // Backend and Frontend exist, but "API" does not
        product.TargetGroupMigrationOrder = "Backend,Frontend,API";

        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");

        result.Errors.Should().Contain(e =>
            e.Path.Contains("TargetGroupMigrationOrder") && e.Message.Contains("API"));
    }

    [Fact]
    public void ValidateProduct_TargetGroupMigrationOrder_TargetGroupAbsent_ReportsError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.TargetGroups.Add(TestModelFactory.CreateValidTargetGroup("Frontend"));
        // Frontend is missing from the order
        product.TargetGroupMigrationOrder = "Backend";

        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");

        result.Errors.Should().Contain(e =>
            e.Path.Contains("TargetGroupMigrationOrder") && e.Message.Contains("Frontend"));
    }

    [Fact]
    public void ValidateProduct_TargetGroupMigrationOrder_DuplicateAlias_ReportsError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.TargetGroups.Add(TestModelFactory.CreateValidTargetGroup("Frontend"));
        // Backend appears twice
        product.TargetGroupMigrationOrder = "Backend,Backend,Frontend";

        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");

        result.Errors.Should().Contain(e =>
            e.Path.Contains("TargetGroupMigrationOrder") && e.Message.Contains("Backend"));
    }

    [Fact]
    public void ValidateProduct_TargetGroupMigrationOrder_SingleTargetGroup_ReportsWarning()
    {
        var product = TestModelFactory.CreateValidProduct();
        // Only one TG: "Backend"
        product.TargetGroupMigrationOrder = "Backend";

        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");

        result.Warnings.Should().Contain(e => e.Path.Contains("TargetGroupMigrationOrder"));
    }

    [Fact]
    public void ValidateProduct_TargetGroupMigrationOrder_NotSet_NoError()
    {
        var product = TestModelFactory.CreateValidProduct();
        product.TargetGroups.Add(TestModelFactory.CreateValidTargetGroup("Frontend"));
        product.TargetGroupMigrationOrder = null;

        var result = ConfigurationValidator.ValidateProduct(product, "Products > MyProduct");

        result.Errors.Should().NotContain(e => e.Path.Contains("TargetGroupMigrationOrder"));
        result.Warnings.Should().NotContain(e => e.Path.Contains("TargetGroupMigrationOrder"));
    }

    // ── ValidateTargetGroup ──────────────────────────────────────────

    [Fact]
    public void ValidateTargetGroup_EmptyAlias_ReportsError()
    {
        var tg = TestModelFactory.CreateValidTargetGroup();
        tg.Alias = "";
        var result = ConfigurationValidator.ValidateTargetGroup(tg, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("Alias"));
    }

    [Fact]
    public void ValidateTargetGroup_InvalidAlias_ReportsError()
    {
        var tg = TestModelFactory.CreateValidTargetGroup();
        tg.Alias = "has-hyphen";
        var result = ConfigurationValidator.ValidateTargetGroup(tg, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("Alias"));
    }

    [Fact]
    public void ValidateTargetGroup_EmptyDatabaseType_ReportsError()
    {
        var tg = TestModelFactory.CreateValidTargetGroup();
        tg.DatabaseType = "";
        var result = ConfigurationValidator.ValidateTargetGroup(tg, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("DatabaseType"));
    }

    [Fact]
    public void ValidateTargetGroup_InvalidDatabaseType_ReportsError()
    {
        var tg = TestModelFactory.CreateValidTargetGroup();
        tg.DatabaseType = "Oracle";
        var result = ConfigurationValidator.ValidateTargetGroup(tg, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("DatabaseType"));
    }

    [Fact]
    public void ValidateTargetGroup_NoTargets_ReportsError()
    {
        var tg = TestModelFactory.CreateValidTargetGroup();
        tg.Targets.Clear();
        var result = ConfigurationValidator.ValidateTargetGroup(tg, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("Targets"));
    }

    // ── ValidateTarget ───────────────────────────────────────────────

    [Fact]
    public void ValidateTarget_EmptyAlias_ReportsError()
    {
        var target = TestModelFactory.CreateValidTarget();
        target.Alias = "";
        var result = ConfigurationValidator.ValidateTarget(target, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("Alias"));
    }

    [Fact]
    public void ValidateTarget_InvalidAlias_ReportsError()
    {
        var target = TestModelFactory.CreateValidTarget();
        target.Alias = "has-hyphen";
        var result = ConfigurationValidator.ValidateTarget(target, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("Alias"));
    }

    // ── ValidateProductDefaults ──────────────────────────────────────

    [Fact]
    public void ValidateProductDefaults_InvalidMigrationErrorAction_ReportsError()
    {
        var defaults = new ProductDefaultsModel { MigrationErrorAction = "Explode" };
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().Contain(e => e.Path.Contains("MigrationErrorAction"));
    }

    [Theory]
    [InlineData("Terminate")]
    [InlineData("Rollback")]
    [InlineData("RollbackErrorOnly")]
    [InlineData("RollbackRelease")]
    [InlineData("Ignore")]
    public void ValidateProductDefaults_ValidMigrationErrorActions_NoError(string action)
    {
        var defaults = new ProductDefaultsModel { MigrationErrorAction = action };
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().NotContain(e => e.Path.Contains("MigrationErrorAction"));
    }

    [Fact]
    public void ValidateProductDefaults_InvalidRollbackErrorAction_ReportsError()
    {
        var defaults = new ProductDefaultsModel { RollbackErrorAction = "Invalid" };
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().Contain(e => e.Path.Contains("RollbackErrorAction"));
    }

    [Theory]
    [InlineData("Terminate")]
    [InlineData("Ignore")]
    public void ValidateProductDefaults_ValidRollbackErrorActions_NoError(string action)
    {
        var defaults = new ProductDefaultsModel { RollbackErrorAction = action };
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().NotContain(e => e.Path.Contains("RollbackErrorAction"));
    }

    [Fact]
    public void ValidateProductDefaults_InvalidTargetMigrationOrder_ReportsError()
    {
        var defaults = new ProductDefaultsModel();
        defaults.TargetGroupDefaults.TargetMigrationOrder = "Parallel";
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().Contain(e => e.Path.Contains("TargetMigrationOrder"));
    }

    [Theory]
    [InlineData("Simultaneously")]
    [InlineData("Successively")]
    public void ValidateProductDefaults_ValidTargetMigrationOrders_NoError(string order)
    {
        var defaults = new ProductDefaultsModel();
        defaults.TargetGroupDefaults.TargetMigrationOrder = order;
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().NotContain(e => e.Path.Contains("TargetMigrationOrder"));
    }

    [Fact]
    public void ValidateProductDefaults_InvalidHashValidationScope_ReportsError()
    {
        var defaults = new ProductDefaultsModel();
        defaults.TargetGroupDefaults.HashValidationScope = "Invalid";
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().Contain(e => e.Path.Contains("HashValidationScope"));
    }

    [Theory]
    [InlineData("File")]
    [InlineData("SqlBlocks")]
    [InlineData("Disabled")]
    public void ValidateProductDefaults_ValidHashValidationScopes_NoError(string scope)
    {
        var defaults = new ProductDefaultsModel();
        defaults.TargetGroupDefaults.HashValidationScope = scope;
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().NotContain(e => e.Path.Contains("HashValidationScope"));
    }

    [Fact]
    public void ValidateProductDefaults_InvalidFileExtension_ReportsError()
    {
        var defaults = new ProductDefaultsModel { MigrationFilesExtension = "sql.v2" };
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().Contain(e => e.Path.Contains("MigrationFilesExtension"));
    }

    [Fact]
    public void ValidateProductDefaults_InvalidEncoding_ReportsError()
    {
        var defaults = new ProductDefaultsModel { MigrationFilesEncoding = "not-an-encoding" };
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().Contain(e => e.Path.Contains("MigrationFilesEncoding"));
    }

    [Fact]
    public void ValidateProductDefaults_ValidEncoding_NoError()
    {
        var defaults = new ProductDefaultsModel { MigrationFilesEncoding = "UTF-8" };
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().NotContain(e => e.Path.Contains("MigrationFilesEncoding"));
    }

    [Fact]
    public void ValidateProductDefaults_NegativeTimeout_ReportsError()
    {
        var defaults = new ProductDefaultsModel();
        defaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds = -1;
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().Contain(e => e.Path.Contains("DbCommandTimeoutInSeconds"));
    }

    [Fact]
    public void ValidateProductDefaults_NegativeMaxRetries_ReportsError()
    {
        var defaults = new ProductDefaultsModel();
        defaults.TargetGroupDefaults.TargetDefaults.DbCommandMaxRetries = -5;
        var result = ConfigurationValidator.ValidateProductDefaults(defaults);
        result.Errors.Should().Contain(e => e.Path.Contains("DbCommandMaxRetries"));
    }

    // ── ValidateAll — Serilog paths ──────────────────────────────────

    [Fact]
    public void ValidateAll_NoSerilogSinks_Warns()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Serilog.WriteTo.Clear();
        var result = ConfigurationValidator.ValidateAll(model);
        result.Warnings.Should().Contain(e => e.Path.Contains("WriteTo"));
    }

    [Fact]
    public void ValidateAll_InvalidSerilogLevel_Warns()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Serilog.MinimumLevelDefault = "Loud";
        var result = ConfigurationValidator.ValidateAll(model);
        result.Warnings.Should().Contain(e => e.Path.Contains("MinimumLevel"));
    }

    // ── ValidateAll — DatabaseLogging paths ─────────────────────────

    [Fact]
    public void ValidateAll_DatabaseLogging_InvalidDbType_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.DatabaseLogging = new DatabaseLoggingModel { DatabaseType = "FakeDB", SchemaName = "logs" };
        var result = ConfigurationValidator.ValidateAll(model);
        result.Errors.Should().Contain(e => e.Path.Contains("DatabaseLogging") && e.Path.Contains("DatabaseType"));
    }

    [Fact]
    public void ValidateDatabaseLogging_InvalidMinimumLevel_ReportsError()
    {
        var dbLog = new DatabaseLoggingModel { DatabaseType = "SqlServer", MinimumLevel = "Loud" };
        var result = ConfigurationValidator.ValidateDatabaseLogging(dbLog);
        result.Errors.Should().Contain(e => e.Path.Contains("MinimumLevel"));
    }

    [Theory]
    [InlineData("Trace")]
    [InlineData("Debug")]
    [InlineData("Information")]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Critical")]
    [InlineData("None")]
    public void ValidateDatabaseLogging_ValidLogLevels_NoError(string level)
    {
        var dbLog = new DatabaseLoggingModel { DatabaseType = "SqlServer", MinimumLevel = level };
        var result = ConfigurationValidator.ValidateDatabaseLogging(dbLog);
        result.Errors.Should().NotContain(e => e.Path.Contains("MinimumLevel"));
    }

    // ── ValidateRepository — positive int fields ─────────────────────

    [Fact]
    public void ValidateRepository_NegativeMaxRetries_ReportsError()
    {
        var repo = new RepositoryModel { DatabaseType = "SqlServer", SchemaName = "migrations", DbCommandMaxRetries = -1 };
        var result = ConfigurationValidator.ValidateRepository(repo);
        result.Errors.Should().Contain(e => e.Path.Contains("DbCommandMaxRetries"));
    }

    [Fact]
    public void ValidateRepository_ZeroRetries_NoError()
    {
        var repo = new RepositoryModel { DatabaseType = "SqlServer", SchemaName = "migrations", DbCommandMaxRetries = 0 };
        var result = ConfigurationValidator.ValidateRepository(repo);
        result.Errors.Should().NotContain(e => e.Path.Contains("DbCommandMaxRetries"));
    }

    [Fact]
    public void ValidateCliTool_EmptyArgumentTemplate_ReportsError()
    {
        var tool = TestModelFactory.CreateValidCliTool();
        tool.ArgumentTemplate = "";
        var result = ConfigurationValidator.ValidateCliTool(tool, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("ArgumentTemplate"));
    }

    // ── ValidateUseCliToolAliasReferences — TargetGroup/Target levels ────

    [Fact]
    public void ValidateUseCliToolAliasReferences_TargetGroupInvalidReference_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.CliTools.Add(TestModelFactory.CreateValidCliTool("sqlcmd"));
        model.Products[0].TargetGroups[0].UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "nonexistent" };

        var result = ConfigurationValidator.ValidateUseCliToolAliasReferences(model);
        result.Errors.Should().Contain(e => e.Code == "RULE_3_3");
    }

    [Fact]
    public void ValidateUseCliToolAliasReferences_TargetInvalidReference_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.CliTools.Add(TestModelFactory.CreateValidCliTool("sqlcmd"));
        model.Products[0].TargetGroups[0].Targets[0].UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "missing-tool" };

        var result = ConfigurationValidator.ValidateUseCliToolAliasReferences(model);
        result.Errors.Should().Contain(e => e.Code == "RULE_3_3");
    }

    [Fact]
    public void ValidateUseCliToolAliasReferences_TargetGroupWithReference_NoToolsDefined_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products[0].TargetGroups[0].UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "sqlcmd" };

        var result = ConfigurationValidator.ValidateUseCliToolAliasReferences(model);
        result.Errors.Should().Contain(e => e.Code == "RULE_3_3");
    }

    // ── ValidateAll — Environment file role ──────────────────────────

    [Fact]
    public void ValidateAll_EnvironmentFileRoleNoProducts_WarnsInsteadOfError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();
        model.FileRole = ConfigFileRole.Environment;
        var result = ConfigurationValidator.ValidateAll(model);
        result.Errors.Should().NotContain(e => e.Path == "Products");
        result.Warnings.Should().Contain(e => e.Path == "Products");
    }
}
