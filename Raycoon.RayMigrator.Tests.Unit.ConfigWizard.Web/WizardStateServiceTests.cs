using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// Tests for WizardStateService — central state management for the web wizard.
/// </summary>
public class WizardStateServiceTests
{
    // ── Initial state ─────────────────────────────────────────────

    [Fact]
    public void InitialState_IsStartPhase()
    {
        var svc = new WizardStateService();

        svc.CurrentPhase.Should().Be(WizardPhase.Start);
    }

    [Fact]
    public void InitialState_HasEmptyAnswers()
    {
        var svc = new WizardStateService();

        svc.Answers.Products.Should().BeEmpty();
    }

    [Fact]
    public void InitialState_NotImported()
    {
        var svc = new WizardStateService();

        svc.IsImported.Should().BeFalse();
    }

    [Fact]
    public void InitialState_LastValidationResultIsNull()
    {
        var svc = new WizardStateService();

        svc.LastValidationResult.Should().BeNull();
    }

    [Fact]
    public void InitialState_CurrentStepId_IsRepository()
    {
        var svc = new WizardStateService();

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    // ── StartNewConfiguration ─────────────────────────────────────

    [Fact]
    public void StartNewConfiguration_TransitionsToHubPhase()
    {
        var svc = new WizardStateService();

        svc.StartNewConfiguration();

        svc.CurrentPhase.Should().Be(WizardPhase.Hub);
    }

    [Fact]
    public void StartNewConfiguration_ScaffoldsMinimalState()
    {
        var svc = new WizardStateService();

        svc.StartNewConfiguration();

        svc.State.BaseModel.Repository.DatabaseType.Should().Be("SqlServer");
        svc.State.BaseModel.Products.Should().ContainSingle();
        svc.State.BaseModel.Products[0].Alias.Should().Be("MyApp");
    }

    [Fact]
    public void StartNewConfiguration_ResetsCurrentStepId()
    {
        var svc = new WizardStateService();
        svc.CurrentStepId = WizardStepId.Serilog;

        svc.StartNewConfiguration();

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    [Fact]
    public void StartNewConfiguration_ClearsLastValidationResult()
    {
        var svc = new WizardStateService();
        svc.ValidateAll();

        svc.StartNewConfiguration();

        svc.LastValidationResult.Should().BeNull();
    }

    [Fact]
    public void StartNewConfiguration_FiresStateChanged()
    {
        var svc = new WizardStateService();
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.StartNewConfiguration();

        fired.Should().BeTrue();
    }

    [Fact]
    public void StartNewConfiguration_DatabaseLoggingEnabled()
    {
        var svc = new WizardStateService();

        svc.StartNewConfiguration();

        svc.State.BaseModel.DatabaseLogging.Should().NotBeNull();
    }

    [Fact]
    public void StartNewConfiguration_SyncsAnswers()
    {
        var svc = new WizardStateService();

        svc.StartNewConfiguration();

        svc.Answers.Should().BeSameAs(svc.State.SetupAnswers);
    }

    [Fact]
    public void StartNewConfiguration_HasOneEnvironmentModel()
    {
        var svc = new WizardStateService();

        svc.StartNewConfiguration();

        svc.EnvironmentKeys.Should().ContainSingle().Which.Should().Be("Development");
    }

    // ── Phase transitions ─────────────────────────────────────────

    [Fact]
    public void GoToOverview_SetsOverviewPhase()
    {
        var svc = new WizardStateService();

        svc.GoToOverview();

        svc.CurrentPhase.Should().Be(WizardPhase.Overview);
    }

    [Fact]
    public void GoToOverview_ResetsCurrentStepId()
    {
        var svc = new WizardStateService();
        svc.CurrentStepId = WizardStepId.ProductDefaults;

        svc.GoToOverview();

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    [Fact]
    public void GoToOverview_FiresStateChanged()
    {
        var svc = new WizardStateService();
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.GoToOverview();

        fired.Should().BeTrue();
    }

    [Fact]
    public void GoToGuidedConfig_SetsGuidedConfigPhase()
    {
        var svc = new WizardStateService();
        svc.GoToOverview();

        svc.GoToGuidedConfig();

        svc.CurrentPhase.Should().Be(WizardPhase.GuidedConfig);
    }

    [Fact]
    public void GoToGuidedConfig_ResetsCurrentStepId()
    {
        var svc = new WizardStateService();
        svc.CurrentStepId = WizardStepId.CliTools;

        svc.GoToGuidedConfig();

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    [Fact]
    public void GoToStart_SetsStartPhase()
    {
        var svc = new WizardStateService();
        svc.GoToOverview();

        svc.GoToStart();

        svc.CurrentPhase.Should().Be(WizardPhase.Start);
    }

    [Fact]
    public void GoToStart_ResetsCurrentStepId()
    {
        var svc = new WizardStateService();
        svc.CurrentStepId = WizardStepId.ProductSettings;

        svc.GoToStart();

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    // ── SyncStructure ─────────────────────────────────────────────

    [Fact]
    public void SyncStructure_CreatesEnvironmentModels()
    {
        var svc = new WizardStateService();
        svc.StartNewConfiguration();

        svc.SyncStructure(new List<string> { "Docker", "Production" });

        svc.EnvironmentKeys.Should().Contain("Docker").And.Contain("Production");
    }

    [Fact]
    public void SyncStructure_PreservesExistingEnvironmentModels()
    {
        var svc = new WizardStateService();
        svc.StartNewConfiguration();
        svc.SyncStructure(new List<string> { "Docker" });
        var original = svc.GetEnvironmentModel("Docker");

        svc.SyncStructure(new List<string> { "Docker", "Production" });

        svc.GetEnvironmentModel("Docker").Should().BeSameAs(original);
    }

    [Fact]
    public void SyncStructure_RemovesUnreferencedEnvironments()
    {
        var svc = new WizardStateService();
        svc.StartNewConfiguration();
        svc.SyncStructure(new List<string> { "Docker", "Production" });

        svc.SyncStructure(new List<string> { "Docker" });

        svc.EnvironmentKeys.Should().ContainSingle().Which.Should().Be("Docker");
    }

    // ── ImportFiles ───────────────────────────────────────────────

    [Fact]
    public void ImportFiles_SetsIsImportedTrue()
    {
        var svc = new WizardStateService();
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "SqlServer", "ConnectionString": "", "SchemaName": "migrations"}}}"""
        };

        svc.ImportFiles(files);

        svc.IsImported.Should().BeTrue();
    }

    [Fact]
    public void ImportFiles_ParsesBaseModelFromJson()
    {
        var svc = new WizardStateService();
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "MariaDb", "ConnectionString": "", "SchemaName": ""}}}"""
        };

        svc.ImportFiles(files);

        svc.State.BaseModel.Repository.DatabaseType.Should().Be("MariaDb");
    }

    [Fact]
    public void ImportFiles_SyncsAnswersFromParsedState()
    {
        var svc = new WizardStateService();
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "Sqlite", "ConnectionString": "", "SchemaName": ""}}}"""
        };

        svc.ImportFiles(files);

        svc.Answers.Should().BeSameAs(svc.State.SetupAnswers);
    }

    [Fact]
    public void ImportFiles_ClearsLastValidationResult()
    {
        var svc = new WizardStateService();
        svc.ValidateAll();
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "SqlServer", "ConnectionString": "", "SchemaName": "migrations"}}}"""
        };

        svc.ImportFiles(files);

        svc.LastValidationResult.Should().BeNull();
    }

    [Fact]
    public void ImportFiles_FiresStateChanged()
    {
        var svc = new WizardStateService();
        bool fired = false;
        svc.StateChanged += () => fired = true;
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "SqlServer", "ConnectionString": "", "SchemaName": "migrations"}}}"""
        };

        svc.ImportFiles(files);

        fired.Should().BeTrue();
    }

    // ── ValidateSection ───────────────────────────────────────────

    [Fact]
    public void ValidateSection_Repository_ReturnsResult()
    {
        var svc = new WizardStateService();

        var result = svc.ValidateSection("Repository");

        result.Should().NotBeNull();
    }

    [Fact]
    public void ValidateSection_ProductDefaults_ReturnsResult()
    {
        var svc = new WizardStateService();

        var result = svc.ValidateSection("ProductDefaults");

        result.Should().NotBeNull();
    }

    [Fact]
    public void ValidateSection_CliTools_ReturnsResult()
    {
        var svc = new WizardStateService();

        var result = svc.ValidateSection("CliTools");

        result.Should().NotBeNull();
    }

    [Fact]
    public void ValidateSection_DatabaseLogging_WhenNull_ReturnsEmptyResult()
    {
        var svc = new WizardStateService();
        svc.State.BaseModel.DatabaseLogging = null;

        var result = svc.ValidateSection("DatabaseLogging");

        result.IsValid.Should().BeTrue();
        result.TotalIssues.Should().Be(0);
    }

    [Fact]
    public void ValidateSection_DatabaseLogging_WhenPresent_ReturnsResult()
    {
        var svc = new WizardStateService();
        svc.State.BaseModel.DatabaseLogging = new DatabaseLoggingModel();

        var result = svc.ValidateSection("DatabaseLogging");

        result.Should().NotBeNull();
    }

    [Fact]
    public void ValidateSection_UnknownKey_ReturnsEmptyValidResult()
    {
        var svc = new WizardStateService();

        var result = svc.ValidateSection("UnknownSection");

        result.IsValid.Should().BeTrue();
        result.TotalIssues.Should().Be(0);
    }

    // ── ValidateAll ───────────────────────────────────────────────

    [Fact]
    public void ValidateAll_StoresResultInLastValidationResult()
    {
        var svc = new WizardStateService();

        var result = svc.ValidateAll();

        svc.LastValidationResult.Should().BeSameAs(result);
    }

    [Fact]
    public void ValidateAll_FiresStateChanged()
    {
        var svc = new WizardStateService();
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.ValidateAll();

        fired.Should().BeTrue();
    }

    [Fact]
    public void ValidateAll_ValidConfiguration_IsValid()
    {
        var svc = BuildServiceWithValidScaffoldedState();

        var result = svc.ValidateAll();

        result.Should().NotBeNull();
        result.Errors.Should().NotContain(e => e.Path.Contains("Repository > DatabaseType"));
    }

    // ── PromoteDefaults ───────────────────────────────────────────

    [Fact]
    public void PromoteDefaults_NoProducts_ReturnsEmptyList()
    {
        var svc = new WizardStateService();

        var results = svc.PromoteDefaults();

        results.Should().BeEmpty();
    }

    [Fact]
    public void PromoteDefaults_WhenPromotionOccurs_SetsIsModified()
    {
        var svc = BuildServiceWithTwoProducts();
        foreach (var product in svc.State.BaseModel.Products)
        {
            product.MigrationErrorAction.IsOverridden = true;
            product.MigrationErrorAction.Value = "Rollback";
        }

        var results = svc.PromoteDefaults();

        results.Should().NotBeEmpty();
        svc.State.BaseModel.IsModified.Should().BeTrue();
    }

    [Fact]
    public void PromoteDefaults_WhenNoPromotion_DoesNotSetIsModified()
    {
        var svc = new WizardStateService();

        svc.PromoteDefaults();

        svc.State.BaseModel.IsModified.Should().BeFalse();
    }

    [Fact]
    public void PromoteDefaults_WhenPromotionOccurs_FiresStateChanged()
    {
        var svc = BuildServiceWithTwoProducts();
        foreach (var product in svc.State.BaseModel.Products)
        {
            product.MigrationErrorAction.IsOverridden = true;
            product.MigrationErrorAction.Value = "Ignore";
        }
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.PromoteDefaults();

        fired.Should().BeTrue();
    }

    // ── Model access helpers ──────────────────────────────────────

    [Fact]
    public void BaseModel_ReturnsSameAsStateBaseModel()
    {
        var svc = new WizardStateService();

        svc.BaseModel.Should().BeSameAs(svc.State.BaseModel);
    }

    [Fact]
    public void EnvironmentKeys_EmptyByDefault()
    {
        var svc = new WizardStateService();

        svc.EnvironmentKeys.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentKeys_ReturnsKeysAfterScaffold()
    {
        var svc = BuildServiceWithScaffoldedState(new WizardSetupAnswers
        {
            Products = new List<ProductSetup>
            {
                new() { Alias = "P1", Environments = new() { "Docker", "Production" } }
            }
        });

        svc.EnvironmentKeys.Should().Contain("Docker").And.Contain("Production");
    }

    [Fact]
    public void GetEnvironmentModel_ExistingKey_ReturnsModel()
    {
        var svc = BuildServiceWithScaffoldedState(new WizardSetupAnswers
        {
            Products = new List<ProductSetup>
            {
                new() { Alias = "P1", Environments = new() { "Docker" } }
            }
        });

        var model = svc.GetEnvironmentModel("Docker");

        model.Should().NotBeNull();
    }

    [Fact]
    public void GetEnvironmentModel_NonExistingKey_ReturnsNull()
    {
        var svc = new WizardStateService();

        var model = svc.GetEnvironmentModel("NonExistent");

        model.Should().BeNull();
    }

    [Fact]
    public void GetProductEnvironmentModel_ExistingKey_ReturnsModel()
    {
        var svc = BuildServiceWithScaffoldedState(new WizardSetupAnswers
        {
            Products = new List<ProductSetup>
            {
                new()
                {
                    Alias = "MyProduct",
                    Environments = new() { "Docker" },
                    TargetGroups = new() { new() { Alias = "TG1", DatabaseType = "SqlServer" } }
                }
            }
        });

        var model = svc.GetProductEnvironmentModel("MyProduct.Docker");

        model.Should().NotBeNull();
    }

    [Fact]
    public void GetProductEnvironmentModel_NonExistingKey_ReturnsNull()
    {
        var svc = new WizardStateService();

        var model = svc.GetProductEnvironmentModel("NoProduct.NoEnv");

        model.Should().BeNull();
    }

    // ── JSON generation ───────────────────────────────────────────

    [Fact]
    public void GetBaseJson_ReturnsNonEmptyString()
    {
        var svc = new WizardStateService();

        var json = svc.GetBaseJson();

        json.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetBaseJson_ContainsRayMigratorKey()
    {
        var svc = new WizardStateService();

        var json = svc.GetBaseJson();

        json.Should().Contain("RayMigrator");
    }

    [Fact]
    public void GetEnvironmentJson_ExistingKey_ReturnsJson()
    {
        var svc = BuildServiceWithScaffoldedState(new WizardSetupAnswers
        {
            Products = new List<ProductSetup>
            {
                new() { Alias = "P1", Environments = new() { "Staging" } }
            }
        });

        var json = svc.GetEnvironmentJson("Staging");

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().NotBe("{}");
    }

    [Fact]
    public void GetEnvironmentJson_NonExistingKey_ReturnsFallback()
    {
        var svc = new WizardStateService();

        var json = svc.GetEnvironmentJson("NoEnv");

        json.Should().Be("{}");
    }

    [Fact]
    public void GetProductEnvironmentJson_NonExistingKey_ReturnsFallback()
    {
        var svc = new WizardStateService();

        var json = svc.GetProductEnvironmentJson("NoProduct.NoEnv");

        json.Should().Be("{}");
    }

    // ── Reset ─────────────────────────────────────────────────────

    [Fact]
    public void Reset_ResetsToStartPhase()
    {
        var svc = new WizardStateService();
        svc.GoToOverview();

        svc.Reset();

        svc.CurrentPhase.Should().Be(WizardPhase.Start);
    }

    [Fact]
    public void Reset_ClearsIsImported()
    {
        var svc = new WizardStateService();
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "SqlServer", "ConnectionString": "", "SchemaName": "migrations"}}}"""
        };
        svc.ImportFiles(files);

        svc.Reset();

        svc.IsImported.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsLastValidationResult()
    {
        var svc = new WizardStateService();
        svc.ValidateAll();

        svc.Reset();

        svc.LastValidationResult.Should().BeNull();
    }

    [Fact]
    public void Reset_ResetsCurrentStepId()
    {
        var svc = new WizardStateService();
        svc.CurrentStepId = (WizardStepId)7;

        svc.Reset();

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    [Fact]
    public void Reset_ClearsProducts()
    {
        var svc = new WizardStateService();
        svc.Answers = new WizardSetupAnswers
        {
            Products = new List<ProductSetup>
            {
                new() { Alias = "P1" }
            }
        };

        svc.Reset();

        svc.Answers.Products.Should().BeEmpty();
    }

    [Fact]
    public void Reset_FiresStateChanged()
    {
        var svc = new WizardStateService();
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.Reset();

        fired.Should().BeTrue();
    }

    [Fact]
    public void Reset_CreatesNewState()
    {
        var svc = new WizardStateService();
        var originalState = svc.State;
        svc.StartNewConfiguration();

        svc.Reset();

        svc.State.Should().NotBeSameAs(originalState);
    }

    // ── NotifyStateChanged ────────────────────────────────────────

    [Fact]
    public void NotifyStateChanged_WhenNoSubscribers_DoesNotThrow()
    {
        var svc = new WizardStateService();

        var act = () => svc.NotifyStateChanged();

        act.Should().NotThrow();
    }

    [Fact]
    public void NotifyStateChanged_WithSubscriber_InvokesHandler()
    {
        var svc = new WizardStateService();
        int count = 0;
        svc.StateChanged += () => count++;

        svc.NotifyStateChanged();
        svc.NotifyStateChanged();

        count.Should().Be(2);
    }

    // ── IsExpertMode ────────────────────────────────────────────

    [Fact]
    public void InitialState_IsNotExpertMode()
    {
        var svc = new WizardStateService();

        svc.IsExpertMode.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsExpertMode()
    {
        var svc = new WizardStateService();
        svc.IsExpertMode = true;

        svc.Reset();

        svc.IsExpertMode.Should().BeFalse();
    }

    // ── GoToHub ───────────────────────────────────────────────────

    [Fact]
    public void GoToHub_SetsHubPhase()
    {
        var svc = new WizardStateService();
        svc.GoToOverview();

        svc.GoToHub();

        svc.CurrentPhase.Should().Be(WizardPhase.Hub);
    }

    [Fact]
    public void GoToHub_ResetsCurrentStepId()
    {
        var svc = new WizardStateService();
        svc.CurrentStepId = WizardStepId.ProductDefaults;

        svc.GoToHub();

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    [Fact]
    public void GoToHub_FiresStateChanged()
    {
        var svc = new WizardStateService();
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.GoToHub();

        fired.Should().BeTrue();
    }

    // ── StartDetailedConfiguration ────────────────────────────────

    [Fact]
    public void StartDetailedConfiguration_TransitionsToGuidedConfigPhase()
    {
        var svc = new WizardStateService();

        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.CurrentPhase.Should().Be(WizardPhase.GuidedConfig);
    }

    [Fact]
    public void StartDetailedConfiguration_SetsSelectedProductAlias()
    {
        var svc = new WizardStateService();

        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.SelectedProductAlias.Should().Be("ProductA");
    }

    [Fact]
    public void StartDetailedConfiguration_SetsSelectedEnvironmentName()
    {
        var svc = new WizardStateService();

        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.SelectedEnvironmentName.Should().Be("Docker");
    }

    [Fact]
    public void StartDetailedConfiguration_ScaffoldsCombinationModelWhenNotExists()
    {
        var svc = new WizardStateService();

        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.State.ProductEnvironmentModels.Should().ContainKey("ProductA.Docker");
        svc.State.ProductEnvironmentModels["ProductA.Docker"].FileRole.Should().Be(ConfigFileRole.ProductEnvironment);
    }

    [Fact]
    public void StartDetailedConfiguration_PreservesExistingPopulatedCombinationModel()
    {
        var svc = new WizardStateService();
        var existingModel = new ConfigurationModel { FileRole = ConfigFileRole.ProductEnvironment };
        existingModel.Products.Add(new ProductModel { Alias = "ProductA" });
        svc.State.ProductEnvironmentModels["ProductA.Docker"] = existingModel;

        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.State.ProductEnvironmentModels["ProductA.Docker"].Should().BeSameAs(existingModel);
    }

    [Fact]
    public void StartDetailedConfiguration_CreatesCombinationEntryWhenNotExists()
    {
        var svc = new WizardStateService();

        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.State.CombinationEntries.Should().ContainKey("ProductA.Docker");
    }

    [Fact]
    public void StartDetailedConfiguration_ResetsCurrentStepId()
    {
        var svc = new WizardStateService();
        svc.CurrentStepId = WizardStepId.ProductSettings;

        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    [Fact]
    public void StartDetailedConfiguration_FiresStateChanged()
    {
        var svc = new WizardStateService();
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.StartDetailedConfiguration("ProductA", "Docker");

        fired.Should().BeTrue();
    }

    // ── CompleteDetailedConfiguration ─────────────────────────────

    [Fact]
    public void CompleteDetailedConfiguration_TransitionsToHubPhase()
    {
        var svc = new WizardStateService();
        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.CompleteDetailedConfiguration();

        svc.CurrentPhase.Should().Be(WizardPhase.Hub);
    }

    [Fact]
    public void CompleteDetailedConfiguration_MarksWizardCompleted()
    {
        var svc = new WizardStateService();
        svc.StartDetailedConfiguration("ProductA", "Docker");

        svc.CompleteDetailedConfiguration();

        svc.State.CombinationEntries["ProductA.Docker"].WizardCompleted.Should().BeTrue();
    }

    [Fact]
    public void CompleteDetailedConfiguration_ResetsCurrentStepId()
    {
        var svc = new WizardStateService();
        svc.StartDetailedConfiguration("ProductA", "Docker");
        svc.CurrentStepId = WizardStepId.Serilog;

        svc.CompleteDetailedConfiguration();

        svc.CurrentStepId.Should().Be(WizardStepId.Repository);
    }

    [Fact]
    public void CompleteDetailedConfiguration_FiresStateChanged()
    {
        var svc = new WizardStateService();
        svc.StartDetailedConfiguration("ProductA", "Docker");
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.CompleteDetailedConfiguration();

        fired.Should().BeTrue();
    }

    [Fact]
    public void CompleteDetailedConfiguration_WhenNoSelectionSet_DoesNotThrow()
    {
        var svc = new WizardStateService();

        var act = () => svc.CompleteDetailedConfiguration();

        act.Should().NotThrow();
    }

    // ── GetSelectedCombinationModel ───────────────────────────────

    [Fact]
    public void GetSelectedCombinationModel_WhenSelectionSet_ReturnsCorrectModel()
    {
        var svc = new WizardStateService();
        svc.StartDetailedConfiguration("ProductA", "Docker");

        var model = svc.GetSelectedCombinationModel();

        model.Should().NotBeNull();
        model!.FileRole.Should().Be(ConfigFileRole.ProductEnvironment);
    }

    [Fact]
    public void GetSelectedCombinationModel_WhenNoSelection_ReturnsNull()
    {
        var svc = new WizardStateService();

        var model = svc.GetSelectedCombinationModel();

        model.Should().BeNull();
    }

    [Fact]
    public void GetSelectedCombinationModel_WhenModelNotInState_ReturnsNull()
    {
        var svc = new WizardStateService();
        svc.SelectedProductAlias = "ProductA";
        svc.SelectedEnvironmentName = "Docker";

        var model = svc.GetSelectedCombinationModel();

        model.Should().BeNull();
    }

    // ── AddProduct ────────────────────────────────────────────────

    [Fact]
    public void AddProduct_AddsToBaseModelProducts()
    {
        var svc = new WizardStateService();

        svc.AddProduct("NewProduct");

        svc.State.BaseModel.Products.Should().Contain(p => p.Alias == "NewProduct");
    }

    [Fact]
    public void AddProduct_AddsToAnswersProducts()
    {
        var svc = new WizardStateService();

        svc.AddProduct("NewProduct");

        svc.Answers.Products.Should().Contain(p => p.Alias == "NewProduct");
    }

    [Fact]
    public void AddProduct_SetsMigrationFilesRootDirectory()
    {
        var svc = new WizardStateService();

        svc.AddProduct("NewProduct");

        svc.State.BaseModel.Products.Single(p => p.Alias == "NewProduct")
            .MigrationFilesRootDirectory.Should().Be("./Migrations/NewProduct");
    }

    [Fact]
    public void AddProduct_DuplicateAlias_IsIgnored()
    {
        var svc = new WizardStateService();
        svc.AddProduct("DuplicateProduct");

        svc.AddProduct("DuplicateProduct");

        svc.State.BaseModel.Products.Count(p => p.Alias == "DuplicateProduct").Should().Be(1);
    }

    [Fact]
    public void AddProduct_DuplicateAlias_CaseInsensitive_IsIgnored()
    {
        var svc = new WizardStateService();
        svc.AddProduct("MyProduct");

        svc.AddProduct("myproduct");

        svc.State.BaseModel.Products.Count(p =>
            string.Equals(p.Alias, "MyProduct", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }

    [Fact]
    public void AddProduct_EmptyAlias_IsIgnored()
    {
        var svc = new WizardStateService();

        svc.AddProduct("");

        svc.State.BaseModel.Products.Should().BeEmpty();
    }

    [Fact]
    public void AddProduct_WhitespaceAlias_IsIgnored()
    {
        var svc = new WizardStateService();

        svc.AddProduct("   ");

        svc.State.BaseModel.Products.Should().BeEmpty();
    }

    [Fact]
    public void AddProduct_FiresStateChanged()
    {
        var svc = new WizardStateService();
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.AddProduct("NewProduct");

        fired.Should().BeTrue();
    }

    [Fact]
    public void AddProduct_SetsConnectionStringWithEnvPlaceholder()
    {
        var svc = new WizardStateService();

        svc.AddProduct("App2");

        var target = svc.State.BaseModel.Products
            .Single(p => p.Alias == "App2")
            .TargetGroups.Single(tg => tg.Alias == "Backend")
            .Targets.Single(t => t.Alias == "MainDB");

        target.ConnectionString.Should().Be("{ENV:APP2_BACKEND_MAINDB_CONNECTION_STRING}");
    }

    [Fact]
    public void AddProduct_ConnectionStringHandlesSpecialCharacters()
    {
        var svc = new WizardStateService();

        svc.AddProduct("My-App.2");

        var target = svc.State.BaseModel.Products
            .Single(p => p.Alias == "My-App.2")
            .TargetGroups.Single(tg => tg.Alias == "Backend")
            .Targets.Single(t => t.Alias == "MainDB");

        target.ConnectionString.Should().Be("{ENV:MY_APP_2_BACKEND_MAINDB_CONNECTION_STRING}");
    }

    [Fact]
    public void AddProduct_ValidateAll_NoConnectionStringError()
    {
        var svc = new WizardStateService();

        svc.AddProduct("App2");

        var result = ConfigurationValidator.ValidateAll(svc.State.BaseModel);

        result.Errors.Should().NotContain(e =>
            e.Path.Contains("App2") && e.Message.Contains("ConnectionString"));
    }

    // ── RemoveProduct ─────────────────────────────────────────────

    [Fact]
    public void RemoveProduct_RemovesFromBaseModelProducts()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductToRemove");

        svc.RemoveProduct("ProductToRemove");

        svc.State.BaseModel.Products.Should().NotContain(p => p.Alias == "ProductToRemove");
    }

    [Fact]
    public void RemoveProduct_RemovesFromAnswersProducts()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductToRemove");

        svc.RemoveProduct("ProductToRemove");

        svc.Answers.Products.Should().NotContain(p => p.Alias == "ProductToRemove");
    }

    [Fact]
    public void RemoveProduct_CascadesProductEnvironmentModels()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");
        svc.AddEnvironment("ProductA", "Production");

        svc.RemoveProduct("ProductA");

        svc.State.ProductEnvironmentModels.Keys.Should().NotContain(k => k.StartsWith("ProductA."));
    }

    [Fact]
    public void RemoveProduct_CascadesCombinationEntries()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");

        svc.RemoveProduct("ProductA");

        svc.State.CombinationEntries.Keys.Should().NotContain(k => k.StartsWith("ProductA."));
    }

    [Fact]
    public void RemoveProduct_CleansUpOrphanedEnvironmentModels()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");

        svc.RemoveProduct("ProductA");

        svc.State.EnvironmentModels.Should().NotContainKey("Docker");
    }

    [Fact]
    public void RemoveProduct_KeepsEnvironmentModelsUsedByOtherProducts()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddProduct("ProductB");
        svc.AddEnvironment("ProductA", "Docker");
        svc.AddEnvironment("ProductB", "Docker");

        svc.RemoveProduct("ProductA");

        svc.State.EnvironmentModels.Should().ContainKey("Docker");
    }

    [Fact]
    public void RemoveProduct_FiresStateChanged()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductToRemove");
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.RemoveProduct("ProductToRemove");

        fired.Should().BeTrue();
    }

    // ── AddEnvironment ────────────────────────────────────────────

    [Fact]
    public void AddEnvironment_AddsToAnswersProductEnvironments()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");

        svc.AddEnvironment("ProductA", "Docker");

        svc.Answers.Products.Single(p => p.Alias == "ProductA")
            .Environments.Should().Contain("Docker");
    }

    [Fact]
    public void AddEnvironment_ScaffoldsCombinationModel()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");

        svc.AddEnvironment("ProductA", "Docker");

        svc.State.ProductEnvironmentModels.Should().ContainKey("ProductA.Docker");
    }

    [Fact]
    public void AddEnvironment_CreatesCombinationEntry()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");

        svc.AddEnvironment("ProductA", "Docker");

        svc.State.CombinationEntries.Should().ContainKey("ProductA.Docker");
        svc.State.CombinationEntries["ProductA.Docker"].WizardCompleted.Should().BeFalse();
    }

    [Fact]
    public void AddEnvironment_CreatesEnvironmentModel()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");

        svc.AddEnvironment("ProductA", "Docker");

        svc.State.EnvironmentModels.Should().ContainKey("Docker");
    }

    [Fact]
    public void AddEnvironment_DuplicateEnvironment_IsIgnored()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");

        svc.AddEnvironment("ProductA", "Docker");

        svc.Answers.Products.Single(p => p.Alias == "ProductA")
            .Environments.Count(e => e == "Docker").Should().Be(1);
    }

    [Fact]
    public void AddEnvironment_EmptyEnvironmentName_IsIgnored()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");

        svc.AddEnvironment("ProductA", "");

        svc.Answers.Products.Single(p => p.Alias == "ProductA").Environments.Should().BeEmpty();
    }

    [Fact]
    public void AddEnvironment_UnknownProduct_IsIgnored()
    {
        var svc = new WizardStateService();

        svc.AddEnvironment("NonExistent", "Docker");

        svc.State.ProductEnvironmentModels.Should().NotContainKey("NonExistent.Docker");
    }

    [Fact]
    public void AddEnvironment_FiresStateChanged()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.AddEnvironment("ProductA", "Docker");

        fired.Should().BeTrue();
    }

    // ── RemoveEnvironment ─────────────────────────────────────────

    [Fact]
    public void RemoveEnvironment_RemovesFromAnswersProductEnvironments()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");

        svc.RemoveEnvironment("ProductA", "Docker");

        svc.Answers.Products.Single(p => p.Alias == "ProductA")
            .Environments.Should().NotContain("Docker");
    }

    [Fact]
    public void RemoveEnvironment_RemovesCombinationModel()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");

        svc.RemoveEnvironment("ProductA", "Docker");

        svc.State.ProductEnvironmentModels.Should().NotContainKey("ProductA.Docker");
    }

    [Fact]
    public void RemoveEnvironment_RemovesCombinationEntry()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");

        svc.RemoveEnvironment("ProductA", "Docker");

        svc.State.CombinationEntries.Should().NotContainKey("ProductA.Docker");
    }

    [Fact]
    public void RemoveEnvironment_CleansUpOrphanedEnvironmentModel()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");

        svc.RemoveEnvironment("ProductA", "Docker");

        svc.State.EnvironmentModels.Should().NotContainKey("Docker");
    }

    [Fact]
    public void RemoveEnvironment_KeepsEnvironmentModelUsedByOtherProduct()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddProduct("ProductB");
        svc.AddEnvironment("ProductA", "Docker");
        svc.AddEnvironment("ProductB", "Docker");

        svc.RemoveEnvironment("ProductA", "Docker");

        svc.State.EnvironmentModels.Should().ContainKey("Docker");
    }

    [Fact]
    public void RemoveEnvironment_FiresStateChanged()
    {
        var svc = new WizardStateService();
        svc.AddProduct("ProductA");
        svc.AddEnvironment("ProductA", "Docker");
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.RemoveEnvironment("ProductA", "Docker");

        fired.Should().BeTrue();
    }

    // ── ValidateCombination ───────────────────────────────────────

    [Fact]
    public void ValidateCombination_WhenModelExists_ReturnsResult()
    {
        var svc = new WizardStateService();
        svc.StartDetailedConfiguration("ProductA", "Docker");

        var result = svc.ValidateCombination("ProductA", "Docker");

        result.Should().NotBeNull();
    }

    [Fact]
    public void ValidateCombination_WhenModelNotExists_ReturnsEmptyResult()
    {
        var svc = new WizardStateService();

        var result = svc.ValidateCombination("NonExistent", "Missing");

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.TotalIssues.Should().Be(0);
    }

    // ── ImportFiles populates CombinationEntries ──────────────────

    [Fact]
    public void ImportFiles_PopulatesCombinationEntriesFromPeModelKeys()
    {
        var svc = new WizardStateService();
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "SqlServer", "ConnectionString": "", "SchemaName": "migrations"}}}""",
            ["appsettings.ProductA.Docker.json"] = """{"RayMigrator": {}}""",
            ["appsettings.ProductA.Production.json"] = """{"RayMigrator": {}}"""
        };

        svc.ImportFiles(files);

        svc.State.CombinationEntries.Should().ContainKey("ProductA.Docker");
        svc.State.CombinationEntries.Should().ContainKey("ProductA.Production");
    }

    [Fact]
    public void ImportFiles_CombinationEntries_WizardCompletedIsFalse()
    {
        var svc = new WizardStateService();
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "SqlServer", "ConnectionString": "", "SchemaName": "migrations"}}}""",
            ["appsettings.ProductA.Docker.json"] = """{"RayMigrator": {}}"""
        };

        svc.ImportFiles(files);

        svc.State.CombinationEntries["ProductA.Docker"].WizardCompleted.Should().BeFalse();
    }

    [Fact]
    public void ImportFiles_ClearsSelectedProduct()
    {
        var svc = new WizardStateService();
        svc.SelectedProductAlias = "OldProduct";
        svc.SelectedEnvironmentName = "OldEnv";
        var files = new Dictionary<string, string>
        {
            ["appsettings.json"] = """{"RayMigrator": {"Repository": {"DatabaseType": "SqlServer", "ConnectionString": "", "SchemaName": "migrations"}}}"""
        };

        svc.ImportFiles(files);

        svc.SelectedProductAlias.Should().BeNull();
        svc.SelectedEnvironmentName.Should().BeNull();
    }

    // ── Reset clears selection ────────────────────────────────────

    [Fact]
    public void Reset_ClearsSelectedProductAlias()
    {
        var svc = new WizardStateService();
        svc.SelectedProductAlias = "ProductA";

        svc.Reset();

        svc.SelectedProductAlias.Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsSelectedEnvironmentName()
    {
        var svc = new WizardStateService();
        svc.SelectedEnvironmentName = "Docker";

        svc.Reset();

        svc.SelectedEnvironmentName.Should().BeNull();
    }

    // ── StartNewConfiguration clears selection ────────────────────

    [Fact]
    public void StartNewConfiguration_ClearsSelectedProductAlias()
    {
        var svc = new WizardStateService();
        svc.SelectedProductAlias = "OldProduct";

        svc.StartNewConfiguration();

        svc.SelectedProductAlias.Should().BeNull();
    }

    [Fact]
    public void StartNewConfiguration_ClearsSelectedEnvironmentName()
    {
        var svc = new WizardStateService();
        svc.SelectedEnvironmentName = "OldEnv";

        svc.StartNewConfiguration();

        svc.SelectedEnvironmentName.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static WizardStateService BuildServiceWithValidScaffoldedState()
    {
        return BuildServiceWithScaffoldedState(new WizardSetupAnswers
        {
            RepositoryDatabaseType = "SqlServer",
            UseDatabaseLogging = false,
            Products = new List<ProductSetup>
            {
                new()
                {
                    Alias = "MyProduct",
                    TargetGroups = new List<TargetGroupSetup>
                    {
                        new()
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            TargetAliases = new List<string> { "MainDB" }
                        }
                    }
                }
            }
        });
    }

    private static WizardStateService BuildServiceWithTwoProducts()
    {
        return BuildServiceWithScaffoldedState(new WizardSetupAnswers
        {
            RepositoryDatabaseType = "SqlServer",
            Products = new List<ProductSetup>
            {
                new() { Alias = "P1", TargetGroups = new() { new() { Alias = "TG1", DatabaseType = "SqlServer" } } },
                new() { Alias = "P2", TargetGroups = new() { new() { Alias = "TG2", DatabaseType = "SqlServer" } } }
            }
        });
    }

    /// <summary>
    /// Helper: scaffolds state from WizardSetupAnswers using the Core scaffolder directly.
    /// This replaces the old CompleteInterview() pattern in tests.
    /// </summary>
    private static WizardStateService BuildServiceWithScaffoldedState(WizardSetupAnswers answers)
    {
        var svc = new WizardStateService();
        svc.State = ConfigurationScaffolder.Scaffold(answers);
        svc.Answers = svc.State.SetupAnswers;
        return svc;
    }
}
