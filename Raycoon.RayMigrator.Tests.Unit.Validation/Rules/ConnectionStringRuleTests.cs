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
    public void HardcodedPassword_MessageShowsSingleBracePlaceholder()
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
        var issue = report.Issues.Single(i => i.Code == RuleIds.RULE_7_3);
        issue.Message.Should().Contain("{ENV:VARIABLE}",
            because: "the message template escapes its braces for string.Format and must be formatted before it is reported (#26)");
        issue.Message.Should().NotContain("{{",
            because: "doubled braces in the CLI log and the Config Wizard were the symptom of #26");
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

    // === Configured value versus effective value (#30) ===

    [Fact]
    public void HardcodedCredentials_PlaceholderConfiguredAndCredentialInEffectiveValue_IsNotReported()
    {
        // Arrange
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "{ENV:REPO_CONNECTION}",
                EffectiveConnectionString = "Server=.;User Id=sa;Password=secret;",
                SchemaName = "dbo",
            },
        };

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_7_3,
            because: "a credential that enters through a placeholder is not hardcoded, whatever the variable resolves to (#30)");
    }

    [Fact]
    public void HardcodedCredentials_PlaceholderPasswordAndCredentialInEffectiveValue_IsNotReported()
    {
        // Arrange
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;User Id=sa;Password={ENV:SA_PASSWORD};",
                EffectiveConnectionString = "Server=.;User Id=sa;Password=secret;",
                SchemaName = "dbo",
            },
            DatabaseLogging = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;User Id=sa;Pwd={ENV:SA_PASSWORD};",
                EffectiveConnectionString = "Server=.;User Id=sa;Pwd=secret;",
                SchemaName = "dbo",
            },
        };

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_7_3,
            because: "RULE_7_3 inspects the value as configured, for Repository and DatabaseLogging alike");
    }

    [Fact]
    public void HardcodedCredentials_LiteralPasswordNextToPlaceholder_IsReported()
    {
        // Arrange
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server={ENV:DB_HOST};User Id=sa;Password=secret;",
                EffectiveConnectionString = "Server=db;User Id=sa;Password=secret;",
                SchemaName = "dbo",
            },
        };

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().ContainSingle(i => i.Code == RuleIds.RULE_7_3,
            because: "a placeholder elsewhere in the value does not make the literal password any less hardcoded");
    }

    [Fact]
    public void HardcodedCredentials_TargetConfiguredAsPlaceholder_IsNotReported()
    {
        // Arrange
        var input = InputFactory.Minimal(
            repository: new RepositoryInput { DatabaseType = "SqlServer", ConnectionString = "Server=.;Database=repo;", SchemaName = "dbo" },
            products: new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main",
                            connectionString: "{ENV:APP_CONNECTION}",
                            effectiveConnectionString: "Server=.;Database=app;User Id=sa;Password=secret;"),
                    }),
                }),
            });

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_7_3,
            because: "the target path is checked on the configured value like the repository (#30)");
    }

    [Fact]
    public void RepoSharesConnectionWithTarget_DifferentPlaceholdersWithSameEffectiveValue_IsWarning()
    {
        // Arrange
        const string effective = "Server=.;Database=app;";
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "{ENV:REPO_CONNECTION}",
                EffectiveConnectionString = effective,
                SchemaName = "dbo",
            },
            Products = new[]
            {
                InputFactory.Product("App", targetGroups: new[]
                {
                    InputFactory.TargetGroup("TG", targets: new[]
                    {
                        InputFactory.Target("Main", connectionString: "{ENV:APP_CONNECTION}", effectiveConnectionString: effective),
                    }),
                }),
            },
        };

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().ContainSingle(i => i.Code == RuleIds.RULE_7_1,
            because: "RULE_7_1 compares the effective values, so two variables naming one database are detected");
    }

    [Fact]
    public void RepoSharesConnectionWithTarget_SamePlaceholderWithoutEffectiveValue_IsWarning()
    {
        // Arrange
        const string placeholder = "{ENV:BOOKSTORE_CONNECTION}";
        var input = new ValidationInput
        {
            Repository = new RepositoryInput { DatabaseType = "SqlServer", ConnectionString = placeholder, SchemaName = "dbo" },
            Products = new[]
            {
                InputFactory.Product("BookStore", targetGroups: new[]
                {
                    InputFactory.TargetGroup("Backend", targets: new[]
                    {
                        InputFactory.Target("MainDB", connectionString: placeholder),
                    }),
                }),
            },
        };

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().ContainSingle(i => i.Code == RuleIds.RULE_7_1,
            because: "without an effective value the configured values are compared, as in the Config Wizard");
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_7_3);
    }

    [Fact]
    public void DuplicateTargetConnection_DifferentPlaceholdersWithSameEffectiveValue_IsWarning()
    {
        // Arrange
        const string effective = "Server=.;Database=app;";
        var input = InputFactory.Minimal(products: new[]
        {
            InputFactory.Product("App", targetGroups: new[]
            {
                InputFactory.TargetGroup("TG", targets: new[]
                {
                    InputFactory.Target("A", connectionString: "{ENV:TARGET_A}", effectiveConnectionString: effective),
                    InputFactory.Target("B", connectionString: "{ENV:TARGET_B}", effectiveConnectionString: effective),
                }),
            }),
        });

        // Act
        var report = RuleCatalog.RunAll(input);

        // Assert
        report.Issues.Should().ContainSingle(i => i.Code == RuleIds.RULE_7_2,
            because: "RULE_7_2 compares the effective values, so migrations running twice on one database are detected");
    }
}
