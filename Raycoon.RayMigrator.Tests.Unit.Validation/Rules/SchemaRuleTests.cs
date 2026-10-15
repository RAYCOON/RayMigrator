namespace Raycoon.RayMigrator.Tests.Unit.Validation.Rules;

public class SchemaRuleTests
{
    [Fact]
    public void SqliteWithSchemaName_IsWarning()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "Sqlite",
                ConnectionString = "Data Source=x.db",
                SchemaName = "foo",
            },
        };

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_4_1 &&
            i.Severity == ValidationSeverity.Warning &&
            i.Path == "Repository > SchemaName");
    }

    [Fact]
    public void SqlServerWithoutSchemaName_IsError()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;Database=x;",
                SchemaName = null,
            },
        };

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_4_2 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void PostgreSqlWithoutSchemaName_IsError()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=x;Database=y;",
                SchemaName = "",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i => i.Code == RuleIds.RULE_4_2);
    }

    [Fact]
    public void UppercaseTableBaseName_OnPostgres_IsError()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=x;",
                SchemaName = "ray",
                TableBaseName = "Rm_",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_4_3 &&
            i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void LowercaseTableBaseName_OnMariaDb_IsAccepted()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "MariaDb",
                ConnectionString = "Host=x;",
                TableBaseName = "rm_",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_4_3);
    }

    [Fact]
    public void UppercaseTableBaseName_OnSqlServer_IsAccepted()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;Database=x;",
                SchemaName = "dbo",
                TableBaseName = "Rm_",
            },
        };

        var report = RuleCatalog.RunAll(input);
        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_4_3);
    }

    [Fact]
    public void TableBaseNameLongerThanSixteenCharacters_IsError()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=.;Database=x;",
                SchemaName = "dbo",
                TableBaseName = "seventeen_chars__",
            },
        };

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i =>
            i.Code == RuleIds.RULE_4_4 &&
            i.Severity == ValidationSeverity.Error &&
            i.Path == "Repository > TableBaseName" &&
            i.Message.Contains("17 bytes"));
    }

    [Fact]
    public void TableBaseName_IsMeasuredInBytes_BecausePostgreSqlCountsBytes()
    {
        // Sixteen characters, nineteen bytes in UTF-8: PostgreSQL would truncate the longest index name.
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=x;Database=y;",
                SchemaName = "ray",
                TableBaseName = "größenprüfung_x_",
            },
        };

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i => i.Code == RuleIds.RULE_4_4 && i.Path == "Repository > TableBaseName" && i.Message.Contains("19 bytes"));
    }

    [Fact]
    public void TableBaseNameOfSixteenCharacters_IsAccepted()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=x;",
                SchemaName = "ray",
                TableBaseName = "sixteen_chars___",
            },
            DatabaseLogging = new RepositoryInput
            {
                DatabaseType = "PostgreSQL",
                ConnectionString = "Host=x;",
                SchemaName = "ray",
                TableBaseName = "sixteen_chars___",
            },
        };

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().NotContain(i => i.Code == RuleIds.RULE_4_4);
    }

    [Fact]
    public void TooLongTableBaseName_OnDatabaseLogging_IsError()
    {
        var input = new ValidationInput
        {
            Repository = new RepositoryInput { DatabaseType = "Sqlite", ConnectionString = "Data Source=x.db" },
            DatabaseLogging = new RepositoryInput { DatabaseType = "Sqlite", ConnectionString = "Data Source=x.db", TableBaseName = "a_very_long_prefix_" },
        };

        var report = RuleCatalog.RunAll(input);

        report.Issues.Should().Contain(i => i.Code == RuleIds.RULE_4_4 && i.Path == "DatabaseLogging > TableBaseName");
    }
}
