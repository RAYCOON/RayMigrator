
using Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class DefaultCascadeRuleTests
{
    [Fact]
    public void MissingEffectiveErrorAction_IsError()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", effectiveErrorAction: null),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_8_1 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void SetEffectiveErrorAction_IsAccepted()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", effectiveErrorAction: "Terminate"),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_8_1);
    }

    [Fact]
    public void MissingEffectiveTargetMigrationOrder_IsError()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG", effectiveTargetMigrationOrder: null),
            }),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_8_2 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void MissingEffectiveHashValidationScope_IsError()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG", effectiveHashValidationScope: ""),
            }),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_8_3 &&
            i.Severity == ValidationSeverity.Error);
    }
}
