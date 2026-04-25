
using Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class TargetGroupMigrationOrderRuleTests
{
    [Fact]
    public void NoOrderSpecified_IsAccepted()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
                InputFactory.TargetGroup("TG2"),
            }, targetGroupMigrationOrder: null),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i =>
            i.Code == RuleIds.RULE_1_10 ||
            i.Code == RuleIds.RULE_1_11 ||
            i.Code == RuleIds.RULE_1_12 ||
            i.Code == RuleIds.RULE_1_13);
    }

    [Fact]
    public void ValidOrderAllAliasesPresent_IsAccepted()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
                InputFactory.TargetGroup("TG2"),
            }, targetGroupMigrationOrder: "TG2, TG1"),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i =>
            i.Code == RuleIds.RULE_1_10 ||
            i.Code == RuleIds.RULE_1_11 ||
            i.Code == RuleIds.RULE_1_12);
    }

    [Fact]
    public void OrderReferencesUnknownAlias_IsReported()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
            }, targetGroupMigrationOrder: "Typo, TG1"),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_1_10 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void OrderMissesAnActualTargetGroup_IsReported()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
                InputFactory.TargetGroup("TG2"),
            }, targetGroupMigrationOrder: "TG1"),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_1_11 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void OrderHasDuplicateAlias_IsReported()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
                InputFactory.TargetGroup("TG2"),
            }, targetGroupMigrationOrder: "TG1, TG1, TG2"),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_1_12 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void OrderWithSingleTargetGroup_IsWarning()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
            }, targetGroupMigrationOrder: "TG1"),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_1_13 &&
            i.Severity == ValidationSeverity.Warning);
    }
}
