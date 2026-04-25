using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Validation;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// Covers Fix 1: WizardStateService.ValidateAll() aggregates validation across BaseModel
/// and every fully-merged ProductEnvironmentModel, prefixing per-PE issues with the
/// combination key so users can trace errors back to their source file.
/// </summary>
public class WizardStateServiceValidateAllPeModelsTests
{
    [Fact]
    public void ValidateAll_FlagsRule38_ForPeModel_WhenTargetHasAliasButNoParams()
    {
        // Exact scenario from Bug 1 — config split across files; the merged PE model is
        // what gets saved/executed, but BaseModel alone has no UseCliToolAlias.
        var svc = new WizardStateService();

        // Base: minimal valid config, no CliTool anywhere.
        svc.State.BaseModel = BuildBaseModel();

        // PE overlay (imported, merged): adds CliTool alias on target + the tool itself.
        var peModel = BuildMergedPeModel(aliasOnTarget: "sqlcmd", includeTool: true);
        svc.State.ProductEnvironmentModels["MyApp.development"] = peModel;

        var result = svc.ValidateAll();

        var rule38 = result.Errors.FirstOrDefault(e => e.Code == RuleIds.RULE_3_8);
        rule38.Should().NotBeNull("RULE_3_8 must fire for the PE-model where the user's misconfig actually lives");
        rule38!.Path.Should().StartWith("[MyApp.development] ", "issues from PE models must be tagged with their combination key");
    }

    [Fact]
    public void ValidateAll_SkipsEmptyShellPeModels()
    {
        // Empty shells from ConfigurationScaffolder.Scaffold() have Products.Count == 0 and
        // are placeholders that the user has not populated yet (no visit to detailed configuration).
        // Validating them would produce "no products"-style false positives.
        var svc = new WizardStateService();
        svc.State.BaseModel = BuildBaseModel();

        var emptyShell = new ConfigurationModel();   // default: Products is empty
        svc.State.ProductEnvironmentModels["MyApp.development"] = emptyShell;

        var result = svc.ValidateAll();

        result.Errors.Should().NotContain(e => e.Path.StartsWith("[MyApp.development] "),
            "empty shell PE models (Products.Count == 0) must be skipped to avoid false positives");
    }

    [Fact]
    public void ValidateAll_KeepsBaseIssuesWithoutPrefix()
    {
        // Sanity check: base-model issues are aggregated as before, without any bracketed prefix.
        var svc = new WizardStateService();
        // Break base by removing schema on a SchemaName-requiring DB type.
        svc.State.BaseModel = BuildBaseModel();
        svc.State.BaseModel.Repository.SchemaName = "";

        var result = svc.ValidateAll();

        result.Errors.Should().Contain(e => e.Path.Contains("SchemaName"));
        result.Errors.Should().NotContain(e => e.Path.StartsWith("["),
            "base-model issues must not carry a PE-combination prefix");
    }

    [Fact]
    public void ValidateAll_MergesBaseAndMultiplePeModelResults()
    {
        var svc = new WizardStateService();
        svc.State.BaseModel = BuildBaseModel();

        svc.State.ProductEnvironmentModels["MyApp.development"] =
            BuildMergedPeModel(aliasOnTarget: "sqlcmd", includeTool: true);
        svc.State.ProductEnvironmentModels["MyApp.production"] =
            BuildMergedPeModel(aliasOnTarget: "sqlcmd", includeTool: true);

        var result = svc.ValidateAll();

        result.Errors.Should().Contain(e => e.Path.StartsWith("[MyApp.development] ") && e.Code == RuleIds.RULE_3_8);
        result.Errors.Should().Contain(e => e.Path.StartsWith("[MyApp.production] ") && e.Code == RuleIds.RULE_3_8);
    }

    [Fact]
    public void NewConfig_ScaffoldedPeModel_WithAliasAndAllParams_NoRule38()
    {
        // Positive pendant: scaffolded PE model with alias + all required params → must be valid.
        // Regression guard against RULE_3_8 firing when the user configured everything correctly.
        var svc = new WizardStateService();
        svc.StartNewConfiguration();
        svc.StartDetailedConfiguration("MyApp", "Development");

        var peModel = svc.State.ProductEnvironmentModels["MyApp.Development"];
        var target = peModel.Products[0].TargetGroups[0].Targets[0];
        target.UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "sqlcmd" };
        target.CliToolParameters = new Dictionary<string, string>
        {
            ["Server"] = "localhost", ["User"] = "sa", ["Password"] = "secret", ["Database"] = "MyApp"
        };
        peModel.CliTools.Add(new CliToolModel
        {
            Alias = "sqlcmd",
            ExecutablePath = "sqlcmd",
            ArgumentTemplate = "-S {Server} -U {User} -P {Password} -d {Database} -i \"{FilePath}\" -b",
            InputMode = "File",
            SuccessExitCodes = { "0" },
            CliToolTimeoutInSeconds = 120,
        });

        var result = svc.ValidateAll();

        result.Errors.Should().NotContain(e => e.Code == RuleIds.RULE_3_8,
            $"all required params are present; actual errors: {string.Join(" | ", result.Errors.Select(e => $"{e.Code}@{e.Path}"))}");
    }

    [Fact]
    public void NewConfig_ScaffoldedPeModel_WithAliasAndNoParams_FiresRule38()
    {
        // Replicates the user's real scenario: started a new configuration via the wizard
        // (not import), walked into detailed configuration for a combination, set
        // UseCliToolAlias on the target and defined the CliTool — but forgot to set
        // CliToolParameters. The PE model is scaffolded (PreservedDocument == null) but fully
        // populated via ScaffoldCombination. Before the fix, ValidateAll skipped scaffolded
        // PE models based on PreservedDocument, hiding this error.
        var svc = new WizardStateService();
        svc.StartNewConfiguration();                                 // Scaffold → empty PE shell
        svc.StartDetailedConfiguration("MyApp", "Development");      // ScaffoldCombination → filled

        var peModel = svc.State.ProductEnvironmentModels["MyApp.Development"];
        peModel.Products.Should().NotBeEmpty("sanity: ScaffoldCombination must populate Products");
        peModel.PreservedDocument.Should().BeNull("sanity: scaffolded PE models have no PreservedDocument");

        // Simulate the user's edits in Detailed Configuration:
        var target = peModel.Products[0].TargetGroups[0].Targets[0];
        target.UseCliToolAlias = new OverridableValue<string> { IsOverridden = true, Value = "sqlcmd" };
        peModel.CliTools.Add(new CliToolModel
        {
            Alias = "sqlcmd",
            ExecutablePath = "sqlcmd",
            ArgumentTemplate = "-S {Server} -U {User} -P {Password} -d {Database} -i \"{FilePath}\" -b",
            InputMode = "File",
            SuccessExitCodes = { "0" },
            CliToolTimeoutInSeconds = 120,
        });

        var result = svc.ValidateAll();

        var rule38 = result.Errors.FirstOrDefault(e => e.Code == RuleIds.RULE_3_8);
        rule38.Should().NotBeNull(
            "scaffolded PE model must be validated because ScaffoldCombination produces a full model. " +
            $"Actual errors: {string.Join(" | ", result.Errors.Select(e => $"{e.Code}@{e.Path}"))}");
        rule38!.Path.Should().StartWith("[MyApp.Development] ");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static ConfigurationModel BuildBaseModel() => new()
    {
        Repository = new RepositoryModel
        {
            DatabaseType = "SqlServer",
            ConnectionString = "Server=localhost;Database=Repo",
            SchemaName = "migrations",
        },
        ProductDefaults = new ProductDefaultsModel(),
        Products =
        {
            new ProductModel
            {
                Alias = "MyApp",
                MigrationFilesRootDirectory = "./Migrations/MyApp",
                TargetGroups =
                {
                    new TargetGroupModel
                    {
                        Alias = "Backend",
                        DatabaseType = "SqlServer",
                        Targets = { new TargetModel { Alias = "BackendDB", ConnectionString = "Server=localhost" } }
                    }
                }
            }
        },
        Serilog = new SerilogModel
        {
            MinimumLevelDefault = "Information",
            WriteTo = { new SerilogSinkModel { Name = "Console" } }
        }
    };

    private static ConfigurationModel BuildMergedPeModel(string aliasOnTarget, bool includeTool)
    {
        var m = BuildBaseModel();
        m.PreservedDocument = JsonNode.Parse("""{"RayMigrator": {}}""");

        m.Products[0].TargetGroups[0].Targets[0].UseCliToolAlias = new OverridableValue<string>
        {
            IsOverridden = true,
            Value = aliasOnTarget
        };

        if (includeTool)
        {
            m.CliTools.Add(new CliToolModel
            {
                Alias = aliasOnTarget,
                ExecutablePath = aliasOnTarget,
                ArgumentTemplate = "-S {Server} -U {User} -P {Password} -d {Database} -i \"{FilePath}\" -b",
                InputMode = "File",
                SuccessExitCodes = { "0" },
                CliToolTimeoutInSeconds = 60,
            });
        }

        return m;
    }
}
