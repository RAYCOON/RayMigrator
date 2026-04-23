// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Tests.Unit.Validation;

public class RuleCatalogTests
{
    [Fact]
    public void RunAll_WithEmptyInput_ReportsMissingRepository()
    {
        // An empty ValidationInput has no Repository section — SchemaRule flags this as Error (RULE_4_2).
        var input = new ValidationInput();
        var report = RuleCatalog.RunAll(input);
        report.Errors.Should().Contain(i => i.Code == RuleIds.RULE_4_2 && i.Path == "Repository");
    }

    [Fact]
    public void RunAll_WithOnlyRepository_ReturnsEmptyReport()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;Database=x;",
                SchemaName = "dbo",
            },
        };
        var report = RuleCatalog.RunAll(input);
        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RunAll_WithNullInput_Throws()
    {
        Action act = () => RuleCatalog.RunAll(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
