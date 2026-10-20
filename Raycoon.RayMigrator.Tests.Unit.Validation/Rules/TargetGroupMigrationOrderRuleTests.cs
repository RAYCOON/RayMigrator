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
    public void OrderWithCaseMismatch_IsErrorWithHintAndNoMissingAlias()
    {
        // Arrange
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
                InputFactory.TargetGroup("TG2"),
            }, targetGroupMigrationOrder: "tg1, TG2"),
        });

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().ContainSingle(i => i.Code == RuleIds.RULE_1_10,
                because: "migrate-up compares the aliases exactly and refuses 'tg1' for TargetGroup 'TG1'")
            .Which.Should().BeEquivalentTo(new
            {
                Severity = ValidationSeverity.Error,
                Message = "TargetGroupMigrationOrder for Product 'App' references alias 'tg1', which matches TargetGroup 'TG1' " +
                    "only case-insensitively. Use the exact alias 'TG1'."
            }, because: "the error names the exact alias to use");
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_1_11,
            because: "the hint of RULE_1_10 already names TargetGroup 'TG1'");
    }

    [Fact]
    public void OrderWithSingleTargetGroup_IsWarning()
    {
        // Arrange
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
            }, targetGroupMigrationOrder: "TG1"),
        });

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().ContainSingle(i => i.Code == RuleIds.RULE_1_13)
            .Which.Should().BeEquivalentTo(new
            {
                Severity = ValidationSeverity.Warning,
                Message = "TargetGroupMigrationOrder is ignored because the product has only one TargetGroup."
            }, because: "the commands ignore an order that names exactly the only TargetGroup");
        report.Issues.Should().NotContain(i =>
            i.Code == RuleIds.RULE_1_10 ||
            i.Code == RuleIds.RULE_1_11 ||
            i.Code == RuleIds.RULE_1_12,
            because: "the order names the alias of the only TargetGroup exactly once");
    }

    [Theory]
    [InlineData("Other", RuleIds.RULE_1_10)]
    [InlineData("tg1", RuleIds.RULE_1_10)]
    [InlineData("TG1, TG1", RuleIds.RULE_1_12)]
    [InlineData("TG1, Other", RuleIds.RULE_1_10)]
    public void SingleTargetGroupWrongAlias_IsError(string order, string expectedCode)
    {
        // Arrange
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG1"),
            }, targetGroupMigrationOrder: order),
        });

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().Contain(i => i.Code == expectedCode && i.Severity == ValidationSeverity.Error,
            because: $"migrate-up refuses the order '{order}' on a product whose only TargetGroup is 'TG1'");
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_1_13,
            because: "only an order that the commands ignore gets the warning, not one that they refuse");
    }
}
