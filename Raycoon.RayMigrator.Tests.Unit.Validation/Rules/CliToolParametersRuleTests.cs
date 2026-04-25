using Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class CliToolParametersRuleTests
{
    // -- RULE_3_4: params set but no alias resolves ------------------------

    [Fact]
    public void CliParamsWithoutAlias_IsWarning()
    {
        var input = InputFactory.Minimal(
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main",
                            cliToolParameters: new Dictionary<string, string> { ["Server"] = "localhost" }),
                    }),
                }),
            });

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_4 &&
            i.Severity == ValidationSeverity.Warning);
    }

    // -- RULE_3_8: missing required keys (Error) ---------------------------

    [Fact]
    public void AllRequiredKeysProvided_IsAccepted()
    {
        var input = InputFactory.Minimal(
            cliTools: new[]
            {
                InputFactory.CliTool("sqlcmd", argumentTemplate: "-S {Server} -d {Database} -i {FilePath}"),
            },
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main",
                            useCliToolAlias: "sqlcmd",
                            cliToolParameters: new Dictionary<string, string> { ["Server"] = "localhost", ["Database"] = "db1" }),
                    }),
                }),
            });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_3_8);
    }

    [Fact]
    public void MissingRequiredKey_IsError()
    {
        var input = InputFactory.Minimal(
            cliTools: new[]
            {
                InputFactory.CliTool("sqlcmd", argumentTemplate: "-S {Server} -d {Database} -i {FilePath}"),
            },
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main",
                            useCliToolAlias: "sqlcmd",
                            cliToolParameters: new Dictionary<string, string> { ["Server"] = "localhost" }),
                    }),
                }),
            });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_8 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void EmptyValueForRequiredKey_IsError()
    {
        var input = InputFactory.Minimal(
            cliTools: new[]
            {
                InputFactory.CliTool("sqlcmd", argumentTemplate: "-S {Server} -i {FilePath}"),
            },
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main",
                            useCliToolAlias: "sqlcmd",
                            cliToolParameters: new Dictionary<string, string> { ["Server"] = "" }),
                    }),
                }),
            });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i => i.Code == RuleIds.RULE_3_8);
    }

    // -- RULE_3_9: reserved key collision (Error) --------------------------

    [Fact]
    public void ReservedKeyFilePath_IsError()
    {
        var input = InputFactory.Minimal(
            cliTools: new[]
            {
                InputFactory.CliTool("sqlcmd", argumentTemplate: "-i {FilePath}"),
            },
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main",
                            useCliToolAlias: "sqlcmd",
                            cliToolParameters: new Dictionary<string, string> { ["FilePath"] = "/etc/passwd" }),
                    }),
                }),
            });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_9 &&
            i.Severity == ValidationSeverity.Error);
    }

    // -- RULE_3_10: unused keys (Warning) ----------------------------------

    [Fact]
    public void UnusedParameterKey_IsWarning()
    {
        var input = InputFactory.Minimal(
            cliTools: new[]
            {
                InputFactory.CliTool("sqlcmd", argumentTemplate: "-S {Server} -i {FilePath}"),
            },
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main",
                            useCliToolAlias: "sqlcmd",
                            cliToolParameters: new Dictionary<string, string> { ["Server"] = "localhost", ["Typo"] = "value" }),
                    }),
                }),
            });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_10 &&
            i.Severity == ValidationSeverity.Warning);
    }
}
