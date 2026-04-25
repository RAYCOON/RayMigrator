using Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class ConnectionStringRuleTests
{
    [Fact]
    public void HardcodedPassword_IsWarning()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;User Id=sa;Password=secret;",
                SchemaName = "dbo",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_7_3 &&
            i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void EnvPlaceholderConnectionString_IsAccepted()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "{ENV:REPO_CONNECTION}",
                SchemaName = "dbo",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_7_3);
    }

    [Fact]
    public void EnvPlaceholderPassword_IsAccepted()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;User Id=sa;Password={ENV:SA_PASSWORD};",
                SchemaName = "dbo",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_7_3);
    }

    [Fact]
    public void RepoSharesConnectionWithTarget_IsWarning()
    {
        const string cs = "Server=.;Database=app;";
        var input = new ValidationInput
        {
            Repository = new RepositoryInput { DatabaseType = "SqlServer", ConnectionString = cs, SchemaName = "dbo" },
            Products = new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main", connectionString: cs),
                    }),
                }),
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_7_1 &&
            i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void DuplicateTargetConnection_IsWarning()
    {
        const string cs = "Server=.;Database=app;";
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG", targets: new[]
                {
                    InputFactory.Target("A", connectionString: cs),
                    InputFactory.Target("B", connectionString: cs),
                }),
            }),
        });

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_7_2 &&
            i.Severity == ValidationSeverity.Warning);
    }
}
