using Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class CliToolReferencesRuleTests
{
    [Fact]
    public void ValidReference_IsAccepted()
    {
        var input = InputFactory.Minimal(
            cliTools: new[] { InputFactory.CliTool("sqlcmd") },
            products: new[] { InputFactory.Product("App", useCliToolAlias: "sqlcmd") });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_3_3);
    }

    [Fact]
    public void UnknownProductReference_IsReported()
    {
        var input = InputFactory.Minimal(
            cliTools: new[] { InputFactory.CliTool("sqlcmd") },
            products: new[] { InputFactory.Product("App", useCliToolAlias: "typo") });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_3 &&
            i.Severity == ValidationSeverity.Error &&
            i.Path == "Products > App");
    }

    [Fact]
    public void UnknownTargetGroupReference_IsReported()
    {
        var input = InputFactory.Minimal(
            cliTools: new[] { InputFactory.CliTool("sqlcmd") },
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", useCliToolAlias: "typo"),
                }),
            });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_3 &&
            i.Path == "Products > App > TargetGroups > TG");
    }

    [Fact]
    public void UnknownTargetReference_IsReported()
    {
        var input = InputFactory.Minimal(
            cliTools: new[] { InputFactory.CliTool("sqlcmd") },
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main", useCliToolAlias: "typo"),
                    }),
                }),
            });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_3 &&
            i.Path == "Products > App > TargetGroups > TG > Targets > Main");
    }

    [Fact]
    public void CaseInsensitive_MatchIsAccepted()
    {
        var input = InputFactory.Minimal(
            cliTools: new[] { InputFactory.CliTool("SqlCmd") },
            products: new[] { InputFactory.Product("App", useCliToolAlias: "sqlcmd") });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_3_3);
    }

    [Fact]
    public void UnknownProductDefaultsReference_IsReported()
    {
        var input = InputFactory.Minimal(
            cliTools: new[] { InputFactory.CliTool("sqlcmd") },
            defaults: new ProductDefaultsInput { UseCliToolAlias = "typo" });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_3 &&
            i.Path == "ProductDefaults");
    }
}
