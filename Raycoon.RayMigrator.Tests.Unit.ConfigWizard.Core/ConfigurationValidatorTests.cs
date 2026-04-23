// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class ConfigurationValidatorTests
{
    // ── DatabaseType Validation ──────────────────────────────────

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ValidateRepository_AllFiveDbTypes_Accepted(string dbType)
    {
        var repo = new RepositoryModel { DatabaseType = dbType, SchemaName = "migrations" };
        var result = ConfigurationValidator.ValidateRepository(repo);
        result.Errors.Should().NotContain(e => e.Path.Contains("DatabaseType"));
    }

    [Fact]
    public void ValidateRepository_InvalidDbType_ReportsError()
    {
        var repo = new RepositoryModel { DatabaseType = "Oracle" };
        var result = ConfigurationValidator.ValidateRepository(repo);
        result.Errors.Should().Contain(e => e.Path.Contains("DatabaseType"));
    }

    [Fact]
    public void ValidateRepository_EmptyDbType_ReportsError()
    {
        var repo = new RepositoryModel { DatabaseType = "" };
        var result = ConfigurationValidator.ValidateRepository(repo);
        result.Errors.Should().Contain(e => e.Path.Contains("DatabaseType"));
    }

    // ── Sqlite Schema Validation ─────────────────────────────────

    [Fact]
    public void ValidateRepository_Sqlite_SkipsSchemaRequirement()
    {
        var repo = new RepositoryModel { DatabaseType = "Sqlite", SchemaName = "" };
        var result = ConfigurationValidator.ValidateRepository(repo);
        result.Errors.Should().NotContain(e => e.Path.Contains("SchemaName"));
    }

    [Fact]
    public void ValidateRepository_Sqlite_WarnsIfSchemaSet()
    {
        var repo = new RepositoryModel { DatabaseType = "Sqlite", SchemaName = "myschema" };
        var result = ConfigurationValidator.ValidateRepository(repo);
        result.Warnings.Should().Contain(e => e.Path.Contains("SchemaName"));
    }

    [Fact]
    public void ValidateDatabaseLogging_Sqlite_WarnsIfSchemaSet()
    {
        var dbLog = new DatabaseLoggingModel { DatabaseType = "Sqlite", SchemaName = "logs" };
        var result = ConfigurationValidator.ValidateDatabaseLogging(dbLog);
        result.Warnings.Should().Contain(e => e.Path.Contains("SchemaName"));
    }

    // ── Schema Required for SqlServer/PostgreSQL ─────────────────

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    public void ValidateRepository_SchemaRequired_ReportsError(string dbType)
    {
        var repo = new RepositoryModel { DatabaseType = dbType, SchemaName = "" };
        var result = ConfigurationValidator.ValidateRepository(repo);
        result.Errors.Should().Contain(e => e.Path.Contains("SchemaName"));
    }

    // ── Connection String Validation ─────────────────────────────

    [Fact]
    public void ValidateTarget_RequiredConnectionString_EmptyReportsError()
    {
        var target = new TargetModel { Alias = "MainDB", ConnectionString = "" };
        var result = ConfigurationValidator.ValidateTarget(target, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("ConnectionString"));
    }

    [Fact]
    public void ValidateTarget_EnvPlaceholder_SkipsValidation()
    {
        var target = new TargetModel { Alias = "MainDB", ConnectionString = "{ENV:MY_CONN}" };
        var result = ConfigurationValidator.ValidateTarget(target, "Test");
        result.Errors.Should().NotContain(e => e.Path.Contains("ConnectionString"));
    }

    [Fact]
    public void ValidateTarget_ValidKeyValueConnectionString_NoError()
    {
        var target = new TargetModel { Alias = "MainDB", ConnectionString = "Server=localhost;Database=MyDB" };
        var result = ConfigurationValidator.ValidateTarget(target, "Test");
        result.Errors.Should().NotContain(e => e.Path.Contains("ConnectionString"));
    }

    [Fact]
    public void ValidateTarget_InvalidConnectionString_ReportsError()
    {
        var target = new TargetModel { Alias = "MainDB", ConnectionString = "not-a-connection-string" };
        var result = ConfigurationValidator.ValidateTarget(target, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("ConnectionString"));
    }

    // ── TargetMigrationOrder Lock ──────────────────────────────────────

    [Fact]
    public void ValidateTargetGroup_SingleTarget_OverriddenTargetMigrationOrder_Warns()
    {
        var tg = TestModelFactory.CreateValidTargetGroup();
        tg.TargetMigrationOrder = new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };
        // tg.Targets has exactly 1 target

        var result = ConfigurationValidator.ValidateTargetGroup(tg, "Test");
        result.Warnings.Should().Contain(e => e.Path.Contains("TargetMigrationOrder"));
    }

    [Fact]
    public void ValidateTargetGroup_MultipleTargets_OverriddenTargetMigrationOrder_NoWarning()
    {
        var tg = TestModelFactory.CreateValidTargetGroup();
        tg.Targets.Add(TestModelFactory.CreateValidTarget("ReplicaDB"));
        tg.TargetMigrationOrder = new OverridableValue<string> { IsOverridden = true, Value = "Simultaneously" };

        var result = ConfigurationValidator.ValidateTargetGroup(tg, "Test");
        result.Warnings.Should().NotContain(e => e.Path.Contains("TargetMigrationOrder"));
    }

    // ── CliTool Validation ───────────────────────────────────────

    [Fact]
    public void ValidateCliTools_EmptyList_NoErrors()
    {
        var result = ConfigurationValidator.ValidateCliTools(new List<CliToolModel>());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_ValidTool_NoErrors()
    {
        var tools = new List<CliToolModel> { TestModelFactory.CreateValidCliTool() };
        var result = ConfigurationValidator.ValidateCliTools(tools);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateCliTools_DuplicateAlias_ReportsError()
    {
        var tools = new List<CliToolModel>
        {
            TestModelFactory.CreateValidCliTool("sqlcmd"),
            TestModelFactory.CreateValidCliTool("sqlcmd"),
        };
        var result = ConfigurationValidator.ValidateCliTools(tools);
        result.Errors.Should().Contain(e => e.Message.Contains("Duplicate"));
    }

    [Fact]
    public void ValidateCliTool_EmptyAlias_ReportsError()
    {
        var tool = TestModelFactory.CreateValidCliTool();
        tool.Alias = "";
        var result = ConfigurationValidator.ValidateCliTool(tool, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("Alias"));
    }

    [Fact]
    public void ValidateCliTool_EmptyExecutablePath_ReportsError()
    {
        var tool = TestModelFactory.CreateValidCliTool();
        tool.ExecutablePath = "";
        var result = ConfigurationValidator.ValidateCliTool(tool, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("ExecutablePath"));
    }

    [Fact]
    public void ValidateCliTool_InvalidInputMode_ReportsError()
    {
        var tool = TestModelFactory.CreateValidCliTool();
        tool.InputMode = "Pipe";
        var result = ConfigurationValidator.ValidateCliTool(tool, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("InputMode"));
    }

    [Fact]
    public void ValidateCliTool_ZeroTimeout_ReportsError()
    {
        var tool = TestModelFactory.CreateValidCliTool();
        tool.CliToolTimeoutInSeconds = 0;
        var result = ConfigurationValidator.ValidateCliTool(tool, "Test");
        result.Errors.Should().Contain(e => e.Path.Contains("CliToolTimeoutInSeconds"));
    }

    [Fact]
    public void ValidateCliTool_HyphenInAlias_Allowed()
    {
        var tool = TestModelFactory.CreateValidCliTool("sqlcmd-docker");
        var result = ConfigurationValidator.ValidateCliTool(tool, "Test");
        result.Errors.Should().NotContain(e => e.Path.Contains("Alias"));
    }

    // ── UseCliToolAlias Cross-Reference ──────────────────────────────

    [Fact]
    public void ValidateUseCliToolAliasReferences_ValidReference_NoErrors()
    {
        var model = TestModelFactory.CreateValidModel();
        model.CliTools.Add(TestModelFactory.CreateValidCliTool("sqlcmd"));
        model.ProductDefaults.UseCliToolAlias = "sqlcmd";

        var result = ConfigurationValidator.ValidateUseCliToolAliasReferences(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateUseCliToolAliasReferences_InvalidReference_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.CliTools.Add(TestModelFactory.CreateValidCliTool("sqlcmd"));
        model.ProductDefaults.UseCliToolAlias = "nonexistent";

        var result = ConfigurationValidator.ValidateUseCliToolAliasReferences(model);
        result.Errors.Should().Contain(e => e.Code == "RULE_3_3");
    }

    [Fact]
    public void ValidateUseCliToolAliasReferences_NoToolsDefined_WithReferences_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.ProductDefaults.UseCliToolAlias = "sqlcmd";

        var result = ConfigurationValidator.ValidateUseCliToolAliasReferences(model);
        result.Errors.Should().Contain(e => e.Code == "RULE_3_3");
    }

    [Fact]
    public void ValidateUseCliToolAliasReferences_CaseInsensitive()
    {
        var model = TestModelFactory.CreateValidModel();
        model.CliTools.Add(TestModelFactory.CreateValidCliTool("SqlCmd"));
        model.ProductDefaults.UseCliToolAlias = "sqlcmd";

        var result = ConfigurationValidator.ValidateUseCliToolAliasReferences(model);
        result.IsValid.Should().BeTrue();
    }

    // ── ValidateAll Integration ──────────────────────────────────

    [Fact]
    public void ValidateAll_ValidModel_NoErrors()
    {
        var model = TestModelFactory.CreateValidModel();
        var result = ConfigurationValidator.ValidateAll(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAll_NoProducts_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();
        var result = ConfigurationValidator.ValidateAll(model);
        result.Errors.Should().Contain(e => e.Path == "Products");
    }

    [Fact]
    public void ValidateAll_BaseFileRoleNoProducts_WarnsInsteadOfError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Clear();
        model.FileRole = ConfigFileRole.Base;
        var result = ConfigurationValidator.ValidateAll(model);
        result.Errors.Should().NotContain(e => e.Path == "Products");
        result.Warnings.Should().Contain(e => e.Path == "Products");
    }

    [Fact]
    public void ValidateAll_NegativeTimeout_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Repository.DbCommandTimeoutInSeconds = -1;
        var result = ConfigurationValidator.ValidateAll(model);
        result.Errors.Should().Contain(e => e.Path.Contains("DbCommandTimeoutInSeconds"));
    }

    [Fact]
    public void ValidateAll_DuplicateProductAliases_ReportsError()
    {
        var model = TestModelFactory.CreateValidModel();
        model.Products.Add(TestModelFactory.CreateValidProduct("MyProduct"));
        var result = ConfigurationValidator.ValidateAll(model);
        result.Errors.Should().Contain(e => e.Code == "RULE_1_8");
    }
}
