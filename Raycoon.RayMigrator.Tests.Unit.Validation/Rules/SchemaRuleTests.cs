// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class SchemaRuleTests
{
    [Fact]
    public void SqliteWithSchemaName_IsWarning()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "Sqlite",
                ConnectionString = "Data Source=x.db",
                SchemaName = "foo",
            },
        };

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_4_1 &&
            i.Severity == ValidationSeverity.Warning &&
            i.Path == "Repository > SchemaName");
    }

    [Fact]
    public void SqlServerWithoutSchemaName_IsError()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;Database=x;",
                SchemaName = null,
            },
        };

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_4_2 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void PostgreSqlWithoutSchemaName_IsError()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=x;Database=y;",
                SchemaName = "",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i => i.Code == RuleIds.RULE_4_2);
    }

    [Fact]
    public void UppercaseTableBaseName_OnPostgres_IsError()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=x;",
                SchemaName = "ray",
                TableBaseName = "Rm_",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_4_3 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void LowercaseTableBaseName_OnMariaDb_IsAccepted()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "MariaDb",
                ConnectionString = "Host=x;",
                TableBaseName = "rm_",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_4_3);
    }

    [Fact]
    public void UppercaseTableBaseName_OnSqlServer_IsAccepted()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;Database=x;",
                SchemaName = "dbo",
                TableBaseName = "Rm_",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_4_3);
    }
}
