using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Sources;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Validation;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the loader hands the pipeline the product a run is for and nothing of the other products (ADR-026):
/// real files, real loader, assertions on the bound section, the section as written and the placeholder report.
/// </summary>
[Collection("EnvironmentVariableTests")]
public class JsonOptionsSourceProductScopeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RayMigrator_ProductScope_" + Guid.NewGuid().ToString("N"));

    public JsonOptionsSourceProductScopeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private void Write(string fileName, string content) => File.WriteAllText(Path.Combine(_dir, fileName), content);

    private const string BaseTwoProducts = """
        {"RayMigrator":{
          "Repository":{"DatabaseType":"Sqlite","ConnectionString":"Data Source=repo.sqlite","TableBaseName":""},
          "ProductDefaults":{"MigrationErrorAction":"Terminate"},
          "Products":[
            {"Alias":"Shop","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"Data Source=shop.sqlite"}]}]},
            {"Alias":"Crm","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main"}]}]}
          ],
          "CliTools":[{"Alias":"sqlite","ExecutablePath":"sqlite3","ArgumentTemplate":"{FilePath}","InputMode":"File"}],
          "Serilog":{"Using":["Serilog.Sinks.Console"],"MinimumLevel":{"Default":"Warning"},"WriteTo":[{"Name":"Console"}]}}}
        """;

    [Fact]
    public async Task LoadAsync_LoadsTheProductAndTheSharedSections_AndNotTheOtherProduct()
    {
        Write("appsettings.json", BaseTwoProducts);
        Write("appsettings.Prod.json", """{"RayMigrator":{"Products":[{"Alias":"Crm","TargetGroups":[{"Alias":"Backend","Targets":[{"Alias":"Main","ConnectionString":"Data Source=crm-prod.sqlite"}]}]}]}}""");

        var result = await new JsonOptionsSource(_dir).LoadAsync("Shop", "Prod");

        var section = result.RayMigratorConfigSection;
        section["Products:0:Alias"].Should().Be("Shop");
        section["Products:0:TargetGroups:0:Targets:0:ConnectionString"].Should().Be("Data Source=shop.sqlite");
        section["Products:1:Alias"].Should().BeNull("the other product is not part of this run");
        section["Repository:DatabaseType"].Should().Be("Sqlite");
        section["ProductDefaults:MigrationErrorAction"].Should().Be("Terminate");
        section["CliTools:0:Alias"].Should().Be("sqlite", "the CLI tools are shared: a migration file may name any of them");
        section["Serilog:MinimumLevel:Default"].Should().Be("Warning");
        result.RayMigratorConfigSectionAsWritten!["Products:0:Alias"].Should().Be("Shop", "the section as written pairs with the bound lists by index");
        result.RayMigratorConfigSectionAsWritten["Products:1:Alias"].Should().BeNull();
        result.HostConfiguration!["RayMigrator:Products:1:Alias"].Should().BeNull();
        result.ConfigFileDiagnostics.Should().HaveCount(4, "the file diagnostics stay complete");
    }

    [Fact]
    public async Task LoadAsync_ForTheOtherProduct_LoadsItsOverrides()
    {
        Write("appsettings.json", BaseTwoProducts);
        Write("appsettings.Prod.json", """{"RayMigrator":{"Products":[{"Alias":"Crm","TargetGroups":[{"Alias":"Backend","Targets":[{"Alias":"Main","ConnectionString":"Data Source=crm-prod.sqlite"}]}]}]}}""");

        var section = (await new JsonOptionsSource(_dir).LoadAsync("Crm", "Prod")).RayMigratorConfigSection;

        section["Products:0:Alias"].Should().Be("Crm");
        section["Products:0:TargetGroups:0:Targets:0:ConnectionString"].Should().Be("Data Source=crm-prod.sqlite", "the merge by alias happened before the product was isolated");
        section["Products:1:Alias"].Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_MissingVariableOfAnotherProduct_IsNeitherResolvedNorReported()
    {
        Write("appsettings.json", BaseTwoProducts);
        Write("appsettings.Dev.json", """{"RayMigrator":{"Products":[{"Alias":"Crm","MigrationFilesRootDirectory":"{ENV:RM_SCOPE_DOES_NOT_EXIST}"}]}}""");

        var result = await new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev");

        result.ReplacedEnvironmentVariables.Should().BeEmpty("the placeholder belongs to a product this run does not load");
        result.RayMigratorConfigSection["Products:0:Alias"].Should().Be("Shop");
    }

    [Fact]
    public async Task LoadAsync_UnknownProduct_FailsWhenLoading_AndNamesTheAliases()
    {
        Write("appsettings.json", BaseTwoProducts);

        var act = () => new JsonOptionsSource(_dir).LoadAsync("Erp", "Dev");

        (await act.Should().ThrowAsync<ConfigurationValidationException>()).Which.Message
            .Should().Contain("Product alias [Erp] not found")
            .And.Contain("Available product aliases: Shop, Crm")
            .And.Contain("case-sensitive");
    }

    [Fact]
    public async Task LoadAsync_ProductInAnotherCase_IsLoaded_ForTheGuardToJudge()
    {
        Write("appsettings.json", BaseTwoProducts);

        var section = (await new JsonOptionsSource(_dir).LoadAsync("crm", "Dev")).RayMigratorConfigSection;

        section["Products:0:Alias"].Should().Be("Crm", "the loader compares like the merger, the context's guard compares with case and hints at the spelling");
        section["Products:1:Alias"].Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_PairsTheSectionAsWrittenWithTheBoundProduct_ForTheCredentialRule()
    {
        const string variable = "RM_SCOPE_CRM_PASSWORD";
        Write("appsettings.json", """
            {"RayMigrator":{
              "Repository":{"DatabaseType":"Sqlite","ConnectionString":"Data Source=repo.sqlite","TableBaseName":""},
              "ProductDefaults":{"MigrationErrorAction":"Terminate","TargetGroupDefaults":{"TargetMigrationOrder":"FileByFile","HashValidationScope":"File","TargetDefaults":{"DbCommandTimeoutInSeconds":20}}},
              "Products":[
                {"Alias":"Shop","MigrationFilesRootDirectory":"/tmp","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"Data Source=shop.sqlite;Password=literal"}]}]},
                {"Alias":"Crm","MigrationFilesRootDirectory":"/tmp","TargetGroups":[{"Alias":"Backend","DatabaseType":"Sqlite","Targets":[{"Alias":"Main","ConnectionString":"Data Source=crm.sqlite;Password={ENV:RM_SCOPE_CRM_PASSWORD}"}]}]}
              ]}}
            """);
        Environment.SetEnvironmentVariable(variable, "s3cr3t");
        try
        {
            var shop = await new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev");
            var shopReport = RayMigratorOptionsValidator.GetReport(Bind(shop), shop.RayMigratorConfigSectionAsWritten);
            shopReport.Warnings.Should().ContainSingle(w => w.Code == RuleIds.RULE_7_3).Which.Path
                .Should().Be("Products > Shop > TargetGroups > Backend > Targets > Main > ConnectionString", "the literal password of the run's product is reported at its own path");
            shopReport.Issues.Should().NotContain(i => i.Path.Contains("Crm"), "the other product is not part of this run");

            var crm = await new JsonOptionsSource(_dir).LoadAsync("Crm", "Dev");
            var crmReport = RayMigratorOptionsValidator.GetReport(Bind(crm), crm.RayMigratorConfigSectionAsWritten);
            crmReport.Warnings.Should().NotContain(w => w.Code == RuleIds.RULE_7_3, "the credential enters through a placeholder, which the section as written shows after the scope was applied");
            crmReport.Issues.Should().NotContain(i => i.Path.Contains("Shop"));
            Bind(crm).Products![0].TargetGroups![0].Targets![0].ConnectionString.Should().Be("Data Source=crm.sqlite;Password=s3cr3t");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    private static RayMigratorOptions Bind(OptionsSourceResult result)
    {
        var options = new RayMigratorOptions();
        result.RayMigratorConfigSection.Bind(options);
        return options;
    }

    [Fact]
    public async Task LoadAsync_AliasWrittenAsPlaceholder_IsResolvedForTheComparison()
    {
        const string variable = "RM_SCOPE_PRODUCT_ALIAS";
        Write("appsettings.json", """{"RayMigrator":{"Repository":{"DatabaseType":"Sqlite"},"Products":[{"Alias":"{ENV:RM_SCOPE_PRODUCT_ALIAS}","MigrationErrorAction":"Terminate"},{"Alias":"Crm"}]}}""");

        var unset = () => new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev");
        (await unset.Should().ThrowAsync<ConfigurationValidationException>("the variable is not set, so the alias stays the placeholder")).Which.Message
            .Should().Contain("Available product aliases: {ENV:RM_SCOPE_PRODUCT_ALIAS}, Crm");

        Environment.SetEnvironmentVariable(variable, "Shop");
        try
        {
            var result = await new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev");

            result.RayMigratorConfigSection["Products:0:Alias"].Should().Be("Shop", "the section is resolved after the product was chosen");
            result.RayMigratorConfigSection["Products:0:MigrationErrorAction"].Should().Be("Terminate");
            result.RayMigratorConfigSection["Products:1:Alias"].Should().BeNull();
            result.RayMigratorConfigSectionAsWritten!["Products:0:Alias"].Should().Be("{ENV:RM_SCOPE_PRODUCT_ALIAS}", "the section as written keeps the placeholder");
            result.ReplacedEnvironmentVariables.Should().ContainSingle(v => v.EnvironmentVariableName == variable);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task LoadAsync_ElementsWithoutAlias_AreLeftOut()
    {
        Write("appsettings.json", """{"RayMigrator":{"Repository":{"DatabaseType":"Sqlite"},"Products":[{"MigrationErrorAction":"Terminate"},{"Alias":"Shop"}]}}""");

        var section = (await new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev")).RayMigratorConfigSection;

        section["Products:0:Alias"].Should().Be("Shop");
        section["Products:1:MigrationErrorAction"].Should().BeNull("no --product can select an element without an alias");
    }

    [Theory]
    [InlineData("""{"RayMigrator":{"Repository":{"DatabaseType":"Sqlite"}}}""")]
    [InlineData("""{"RayMigrator":{"Repository":{"DatabaseType":"Sqlite"},"Products":[]}}""")]
    [InlineData("""{"RayMigrator":{"Repository":{"DatabaseType":"Sqlite"},"Products":[{"MigrationErrorAction":"Terminate"}]}}""")]
    public async Task LoadAsync_FilesWithoutAnyProductAlias_LoadAsBefore(string baseJson)
    {
        Write("appsettings.json", baseJson);

        var result = await new JsonOptionsSource(_dir).LoadAsync("Shop", "Dev");

        result.RayMigratorConfigSection["Repository:DatabaseType"].Should().Be("Sqlite", "the pipeline reports the missing products, not the loader");
        result.RayMigratorConfigSection["Products:0:Alias"].Should().BeNull();
    }
}
