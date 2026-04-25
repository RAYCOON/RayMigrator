using Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class AliasUniquenessRuleTests
{
    // -- RULE_1_8: Duplicate Product alias --------------------------------

    [Fact]
    public void UniqueProductAliases_AreAccepted()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App1"),
            InputFactory.Product("App2"),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_1_8);
    }

    [Fact]
    public void DuplicateProductAlias_CaseSensitive_IsReported()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("Backend"),
            InputFactory.Product("Backend"),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_1_8 &&
            i.Severity == ValidationSeverity.Error &&
            i.Path == "Products > Backend");
    }

    [Fact]
    public void DuplicateProductAlias_CaseInsensitive_IsReported()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("Backend"),
            InputFactory.Product("backend"),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().ContainSingle(i => i.Code == RuleIds.RULE_1_8);
    }

    // -- RULE_1_9: Duplicate CliTool alias --------------------------------

    [Fact]
    public void DuplicateCliToolAlias_IsReported()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("sqlcmd"),
            InputFactory.CliTool("sqlcmd"),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_1_9 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void UniqueCliToolAliases_AreAccepted()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("sqlcmd"),
            InputFactory.CliTool("psql"),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_1_9);
    }

    // -- RULE_1_1: Duplicate TargetGroup alias within a Product -----------

    [Fact]
    public void DuplicateTargetGroupAlias_WithinProduct_IsReported()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("Backend"),
                InputFactory.TargetGroup("Backend"),
            }),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_1_1 &&
            i.Severity == ValidationSeverity.Error &&
            i.Path == "Products > App > TargetGroups > Backend");
    }

    [Fact]
    public void SameTargetGroupAlias_AcrossDifferentProducts_IsAccepted()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App1", targetGroups: new[]
            {
                InputFactory.TargetGroup("Backend"),
            }),
            InputFactory.Product("App2", targetGroups: new[]
            {
                InputFactory.TargetGroup("Backend"),
            }),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_1_1);
    }

    // -- RULE_1_2: Duplicate Target alias within a TargetGroup ------------

    [Fact]
    public void DuplicateTargetAlias_WithinTargetGroup_IsReported()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("Backend", targets: new[]
                {
                    InputFactory.Target("Main"),
                    InputFactory.Target("Main"),
                }),
            }),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_1_2 &&
            i.Severity == ValidationSeverity.Error &&
            i.Path == "Products > App > TargetGroups > Backend > Targets > Main");
    }

    [Fact]
    public void SameTargetAlias_AcrossDifferentTargetGroups_IsAccepted()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1", targets: new[] { InputFactory.Target("Main") }),
                InputFactory.TargetGroup("TG2", targets: new[] { InputFactory.Target("Main") }),
            }),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_1_2);
    }
}
