using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Validation;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the engine checks RULE_7_3 on the configuration as written (#30). Real files, the real loader and the real
/// validator: a credential that enters through an {ENV:...} placeholder is not a hardcoded credential, whatever
/// the variable resolves to, while a literal credential in the file is still reported.
/// Shares the environment-variable collection because it sets a process-wide variable.
/// </summary>
[Collection("EnvironmentVariableTests")]
public class ConnectionStringPlaceholderValidationTests : IDisposable
{
    private const string Variable = "RM_TEST_30_CONNECTION";
    private const string ResolvedConnection = "Server=localhost;Database=BookStore;User Id=sa;Password=secret;";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RayMigrator_Issue30_" + Guid.NewGuid().ToString("N"));

    public ConnectionStringPlaceholderValidationTests()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable(Variable, ResolvedConnection);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Variable, null);
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    // Not an interpolated raw string: the JSON ends with three closing braces, which collide with the delimiters.
    private static string Configuration(string repositoryConnection, string targetConnection) => """
        {"RayMigrator":{
          "Repository":{"DatabaseType":"SqlServer","ConnectionString":"@@REPOSITORY@@","SchemaName":"ray"},
          "Products":[
            {"Alias":"BookStore","MigrationErrorAction":"Terminate","MigrationFilesRootDirectory":"/tmp",
             "TargetGroups":[{"Alias":"Backend","DatabaseType":"SqlServer","TargetMigrationOrder":"FileByFile","HashValidationScope":"File",
               "Targets":[{"Alias":"MainDB","ConnectionString":"@@TARGET@@"}]}]}
          ],
          "Serilog":{"Using":["Serilog.Sinks.Console"],"MinimumLevel":{"Default":"Warning"},"WriteTo":[{"Name":"Console"}]}}}
        """
        .Replace("@@REPOSITORY@@", repositoryConnection, StringComparison.Ordinal)
        .Replace("@@TARGET@@", targetConnection, StringComparison.Ordinal);

    private async Task<(RayMigratorOptions Options, IConfigurationSection? SectionAsWritten)> LoadAsync(string json)
    {
        File.WriteAllText(Path.Combine(_dir, "appsettings.json"), json);

        var sourceResult = await new JsonOptionsSource(_dir).LoadAsync("BookStore", "Workshop");
        var options = new RayMigratorOptions();
        sourceResult.RayMigratorConfigSection.Bind(options);

        return (options, sourceResult.RayMigratorConfigSectionAsWritten);
    }

    [Fact]
    public async Task GetReport_PlaceholdersInBothConnectionStrings_ReportsNoHardcodedCredentialButTheSharedDatabase()
    {
        // Arrange
        var (options, sectionAsWritten) = await LoadAsync(Configuration("{ENV:" + Variable + "}", "{ENV:" + Variable + "}"));

        // Act
        var report = RayMigratorOptionsValidator.GetReport(options, sectionAsWritten);

        // Assert
        options.Repository!.ConnectionString.Should().Be(ResolvedConnection, because: "the engine binds the resolved values");
        report.Warnings.Should().NotContain(w => w.Code == RuleIds.RULE_7_3,
            because: "both connection strings are placeholders in the file (#30)");
        report.Warnings.Should().ContainSingle(w => w.Code == RuleIds.RULE_7_1,
            because: "repository and target still name the same database");
        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetReport_LiteralCredentialsInTheFile_ReportsHardcodedCredentialsAtBothPaths()
    {
        // Arrange
        var (options, sectionAsWritten) = await LoadAsync(Configuration(ResolvedConnection, ResolvedConnection));

        // Act
        var report = RayMigratorOptionsValidator.GetReport(options, sectionAsWritten);

        // Assert
        report.Warnings.Where(w => w.Code == RuleIds.RULE_7_3).Select(w => w.Path).Should().BeEquivalentTo(
            "Repository > ConnectionString",
            "Products > BookStore > TargetGroups > Backend > Targets > MainDB > ConnectionString");
    }

    [Fact]
    public async Task GetReport_PlaceholderForThePasswordOnly_ReportsNoHardcodedCredential()
    {
        // Arrange
        var (options, sectionAsWritten) = await LoadAsync(Configuration(
            "Server=localhost;Database=BookStore;User Id=sa;Password={ENV:" + Variable + "};",
            "{ENV:" + Variable + "}"));

        // Act
        var report = RayMigratorOptionsValidator.GetReport(options, sectionAsWritten);

        // Assert
        options.Repository!.ConnectionString.Should().Contain("Password=" + ResolvedConnection,
            because: "the placeholder is replaced inside the value");
        report.Warnings.Should().NotContain(w => w.Code == RuleIds.RULE_7_3);
    }
}
