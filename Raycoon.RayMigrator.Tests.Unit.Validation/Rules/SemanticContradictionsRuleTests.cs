
using Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class SemanticContradictionsRuleTests
{
    [Fact]
    public void RollbackWithoutRollbackErrorAction_IsError()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", effectiveErrorAction: "Rollback", effectiveRollbackErrorAction: null),
        });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_2_11 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void RollbackWithRollbackErrorAction_IsAccepted()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", effectiveErrorAction: "Rollback", effectiveRollbackErrorAction: "Terminate"),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_2_11);
    }

    [Fact]
    public void TerminateWithoutRollbackErrorAction_IsAccepted()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", effectiveErrorAction: "Terminate", effectiveRollbackErrorAction: null),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_2_11);
    }

    [Fact]
    public void ExtensionEqualsPreExtension_OnProduct_IsError()
    {
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App",
                effectiveMigrationFilesExtension: "sql",
                effectiveRollbackPreExtension: "sql"),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_2_13 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ExtensionEqualsPreExtension_OnProductDefaults_IsError()
    {
        var input = InputFactory.Minimal(
            defaults: new ProductDefaultsInput
            {
                MigrationFilesExtension = "sql",
                MigrationRollbackFilesPreExtension = "SQL",
            });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_2_13 &&
            i.Path == "ProductDefaults > MigrationFilesExtension");
    }

    [Fact]
    public void DifferentExtensions_AreAccepted()
    {
        var input = InputFactory.Minimal(
            defaults: new ProductDefaultsInput
            {
                MigrationFilesExtension = "sql",
                MigrationRollbackFilesPreExtension = "rollback",
            });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_2_13);
    }
}
