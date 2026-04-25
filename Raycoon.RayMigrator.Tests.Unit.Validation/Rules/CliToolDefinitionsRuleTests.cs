using Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class CliToolDefinitionsRuleTests
{
    // -- RULE_3_1: File mode must have {FilePath} --------------------------

    [Fact]
    public void FileMode_WithFilePath_IsAccepted()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("sqlcmd", argumentTemplate: "-S {Server} -i {FilePath}", inputMode: "File"),
        });
        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_3_1);
    }

    [Fact]
    public void FileMode_WithoutFilePath_IsReported()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("sqlcmd", argumentTemplate: "-S {Server}", inputMode: "File"),
        });
        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_1 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void UnsetInputMode_DefaultsToFile_AndRequiresFilePath()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("sqlcmd", argumentTemplate: "-S {Server}", inputMode: null),
        });
        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i => i.Code == RuleIds.RULE_3_1);
    }

    // -- RULE_3_2: Stdin mode should not have {FilePath} -------------------

    [Fact]
    public void StdinMode_WithoutFilePath_IsAccepted()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("psql", argumentTemplate: "-U {User}", inputMode: "Stdin"),
        });
        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_3_2);
    }

    [Fact]
    public void StdinMode_WithFilePath_IsWarning()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("psql", argumentTemplate: "-U {User} -f {FilePath}", inputMode: "Stdin"),
        });
        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_2 &&
            i.Severity == ValidationSeverity.Warning);
    }

    // -- RULE_3_7: SuccessExitCodes must parse ----------------------------

    [Fact]
    public void ValidExitCodes_AreAccepted()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("tool", successExitCodes: new[] { "0", "1..5", "10..", "..-1" }),
        });
        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_3_7);
    }

    [Fact]
    public void InvalidExitCode_NotAnInt_IsReported()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("tool", successExitCodes: new[] { "abc" }),
        });
        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_3_7 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void InvalidExitCode_ReversedRange_IsReported()
    {
        var input = InputFactory.Minimal(cliTools: new[]
        {
            InputFactory.CliTool("tool", successExitCodes: new[] { "5..1" }),
        });
        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i => i.Code == RuleIds.RULE_3_7);
    }
}
