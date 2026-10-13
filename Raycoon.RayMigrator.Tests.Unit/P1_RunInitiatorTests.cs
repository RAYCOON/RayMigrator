using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The initiator of a run (schema version 2): <see cref="RunInitiator"/> detects a CI/CD pipeline from its
/// environment variables and names the user and the machine, the CLI parser records them in the console options,
/// <see cref="MigrationRunSource"/> matches the lookup table every DAL seeds in schema step 2.
/// </summary>
public class RunInitiatorTests
{
    public static readonly TheoryData<string> Engines = new() { "SqlServer", "PostgreSQL", "MariaDb", "MySql", "Sqlite" };

    private static Func<string, string?> Env(params (string Name, string? Value)[] variables) =>
        name => variables.FirstOrDefault(v => v.Name == name).Value;

    [Fact]
    public void DetectSource_WithoutPipelineVariables_IsCli()
    {
        RunInitiator.DetectSource(Env()).Should().Be(MigrationRunSource.Cli);
    }

    [Theory]
    [InlineData("CI", "true")]
    [InlineData("CI", "1")]
    [InlineData("GITHUB_ACTIONS", "true")]
    [InlineData("GITLAB_CI", "true")]
    [InlineData("TF_BUILD", "True")]
    [InlineData("JENKINS_URL", "http://jenkins.example/")]
    [InlineData("TEAMCITY_VERSION", "2025.03")]
    public void DetectSource_WithAPipelineVariable_IsCi(string variable, string value)
    {
        RunInitiator.DetectSource(Env((variable, value))).Should().Be(MigrationRunSource.Ci);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("   ")]
    public void DetectSource_WithCiSetToAFalseValue_IsCli(string value)
    {
        RunInitiator.DetectSource(Env(("CI", value))).Should().Be(MigrationRunSource.Cli);
    }

    [Fact]
    public void DetectSource_NullLookup_Throws()
    {
        var act = () => RunInitiator.DetectSource(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CurrentUserAndHost_NameTheProcessOwnerAndMachine()
    {
        RunInitiator.CurrentUserName.Should().Be(Environment.UserName);
        RunInitiator.CurrentHost.Should().Be(Environment.MachineName);
    }

    [Fact]
    public void MigrationRunSource_ValuesMatchTheLookupSeeds()
    {
        ((byte)MigrationRunSource.Cli).Should().Be(10);
        ((byte)MigrationRunSource.Ci).Should().Be(20);
        ((byte)MigrationRunSource.Studio).Should().Be(30);
        ((byte)MigrationRunSource.Undefined).Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void SchemaStepTwo_EveryEngine_SeedsEveryMigrationRunSource(string engine)
    {
        var template = File.ReadAllText(GetTemplatePath(engine, "Repository_Upgrade_2.sql"));

        foreach (var source in Enum.GetValues<MigrationRunSource>().Where(v => v != MigrationRunSource.Undefined))
            template.Should().Contain($"({(byte)source}, '{source}'", $"Repository_Upgrade_2.sql of {engine} must seed MigrationRunSource.{source}");
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void SchemaStepTwo_EveryEngine_AddsTheRunDetailColumns(string engine)
    {
        var template = File.ReadAllText(GetTemplatePath(engine, "Repository_Upgrade_2.sql"));
        bool snakeCase = engine is "PostgreSQL" or "MariaDb" or "MySql";

        string[] columns = snakeCase
            ? ["migration_operation_id", "migration_run_source_id", "initiated_by", "initiated_by_subject", "host", "error_code", "error_message", "last_heartbeat_at"]
            : ["MigrationOperationId", "MigrationRunSourceId", "InitiatedBy", "InitiatedBySubject", "Host", "ErrorCode", "ErrorMessage", "LastHeartbeatAt"];
        foreach (var column in columns)
            template.Should().Contain(column, $"Repository_Upgrade_2.sql of {engine} must add the column {column}");
        template.Should().Contain("TemplateType   = \"Repository_Upgrade_2\"").And.Contain("@RayMigratorVersion");
    }

    [Theory]
    [InlineData("migrate-up")]
    [InlineData("info")]
    public async Task CommandLine_EveryCommand_RecordsSourceUserAndHost(string verb)
    {
        var config = new CommandLineConfiguration("RayMigrator Test");
        var parse = config.RootCommand.Parse(new[] { verb, "-p", "P", "-env", "Dev" });
        await parse.InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.Source.Should().Be(RunInitiator.DetectSource(), "the CLI reports Cli, or Ci inside a pipeline");
        config.ParsedOptions.InitiatedBy.Should().Be(Environment.UserName);
        config.ParsedOptions.Host.Should().Be(Environment.MachineName);
        config.ParsedOptions.InitiatedBySubject.Should().BeNull("the CLI has no identity provider subject");
    }

    #region Helpers

    private static string GetTemplatePath(string engine, string templateFile)
    {
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var templatePath = Path.Combine(assemblyDir, "DataAccessLayers", engine, templateFile);

        if (!File.Exists(templatePath))
        {
            var solutionDir = FindSolutionDir(assemblyDir);
            if (solutionDir != null)
                templatePath = Path.Combine(solutionDir, $"Raycoon.RayMigrator.Database.{engine}", "Templates", templateFile);
        }

        return templatePath;
    }

    private static string? FindSolutionDir(string startDir)
    {
        var dir = startDir;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    #endregion
}
