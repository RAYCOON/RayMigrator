
namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// Tests for the enum-based stepper logic in WizardHost.razor.
///
/// The redesigned component uses WizardStepId (enum) instead of int display indices.
/// Display indices are derived from the enum via StepIdToDisplayIndex/DisplayIndexToStepId.
/// OnExpertModeChanged only adjusts if the user is on the CLI Tools step.
/// </summary>
public class WizardHostStepIndexTests
{
    // ── Helpers replicating WizardHost logic ─────────────────────────

    private static bool IsCliToolsStepVisible(WizardStateService wizard, ConfigurationModel model)
        => wizard.IsExpertMode || model.CliTools.Count > 0;

    private static int StepIdToDisplayIndex(WizardStepId stepId, bool cliToolsVisible)
    {
        int id = (int)stepId;
        if (!cliToolsVisible)
        {
            if (stepId == WizardStepId.CliTools) return 0;
            return id > (int)WizardStepId.CliTools ? id - 1 : id;
        }
        return id;
    }

    private static WizardStepId DisplayIndexToStepId(int displayIndex, bool cliToolsVisible)
    {
        if (!cliToolsVisible && displayIndex >= (int)WizardStepId.CliTools)
            return (WizardStepId)(displayIndex + 1);
        return (WizardStepId)displayIndex;
    }

    private static void SimulateExpertModeChanged(
        WizardStateService wizard,
        ConfigurationModel model,
        bool newValue)
    {
        wizard.IsExpertMode = newValue;
        bool isVisible = IsCliToolsStepVisible(wizard, model);
        if (wizard.CurrentStepId == WizardStepId.CliTools && !isVisible)
            wizard.CurrentStepId = WizardStepId.ProductDefaults;
    }

    private static string GetStepperKey(WizardStateService wizard, ConfigurationModel model)
        => $"stepper-{IsCliToolsStepVisible(wizard, model)}";

    private static WizardStateService CreateWizard(bool isExpertMode = false, WizardStepId currentStep = WizardStepId.Repository)
    {
        return new WizardStateService { IsExpertMode = isExpertMode, CurrentStepId = currentStep };
    }

    private static ConfigurationModel EmptyCliTools() => new ConfigurationModel();

    private static ConfigurationModel WithOneCliTool() => new ConfigurationModel
    {
        CliTools = { new CliToolModel { Alias = "psql" } }
    };

    // ── StepperKey ────────────────────────────────────────────────────

    [Fact]
    public void StepperKey_EasyMode_NoCliTools_StepNotVisible()
    {
        var wizard = CreateWizard(isExpertMode: false);
        GetStepperKey(wizard, EmptyCliTools()).Should().Be("stepper-False");
    }

    [Fact]
    public void StepperKey_ExpertMode_NoCliTools_StepVisible()
    {
        var wizard = CreateWizard(isExpertMode: true);
        GetStepperKey(wizard, EmptyCliTools()).Should().Be("stepper-True");
    }

    [Fact]
    public void StepperKey_EasyMode_WithCliTools_StepVisible()
    {
        var wizard = CreateWizard(isExpertMode: false);
        GetStepperKey(wizard, WithOneCliTool()).Should().Be("stepper-True");
    }

    [Fact]
    public void StepperKey_ExpertMode_WithCliTools_StepVisible()
    {
        var wizard = CreateWizard(isExpertMode: true);
        GetStepperKey(wizard, WithOneCliTool()).Should().Be("stepper-True");
    }

    [Fact]
    public void StepperKey_ChangesWhenExpertModeTogglesOn_NoCliTools()
    {
        var wizard = CreateWizard(isExpertMode: false);
        var model = EmptyCliTools();
        var keyBefore = GetStepperKey(wizard, model);
        wizard.IsExpertMode = true;
        var keyAfter = GetStepperKey(wizard, model);
        keyBefore.Should().Be("stepper-False");
        keyAfter.Should().Be("stepper-True");
    }

    [Fact]
    public void StepperKey_DoesNotChange_WhenExpertModeToggles_WithCliTools()
    {
        var wizard = CreateWizard(isExpertMode: false);
        var model = WithOneCliTool();
        var keyBefore = GetStepperKey(wizard, model);
        wizard.IsExpertMode = true;
        var keyAfter = GetStepperKey(wizard, model);
        keyBefore.Should().Be("stepper-True");
        keyAfter.Should().Be("stepper-True");
    }

    // ── StepIdToDisplayIndex / DisplayIndexToStepId ───────────────────

    [Theory]
    [InlineData(WizardStepId.Repository, true, 0)]
    [InlineData(WizardStepId.DatabaseLogging, true, 1)]
    [InlineData(WizardStepId.CliTools, true, 2)]
    [InlineData(WizardStepId.ProductDefaults, true, 3)]
    [InlineData(WizardStepId.ProductSettings, true, 4)]
    [InlineData(WizardStepId.Serilog, true, 5)]
    [InlineData(WizardStepId.Repository, false, 0)]
    [InlineData(WizardStepId.DatabaseLogging, false, 1)]
    [InlineData(WizardStepId.ProductDefaults, false, 2)]
    [InlineData(WizardStepId.ProductSettings, false, 3)]
    [InlineData(WizardStepId.Serilog, false, 4)]
    public void StepIdToDisplayIndex_ReturnsCorrectIndex(WizardStepId stepId, bool cliVisible, int expected)
    {
        StepIdToDisplayIndex(stepId, cliVisible).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, true, WizardStepId.Repository)]
    [InlineData(1, true, WizardStepId.DatabaseLogging)]
    [InlineData(2, true, WizardStepId.CliTools)]
    [InlineData(3, true, WizardStepId.ProductDefaults)]
    [InlineData(4, true, WizardStepId.ProductSettings)]
    [InlineData(5, true, WizardStepId.Serilog)]
    [InlineData(0, false, WizardStepId.Repository)]
    [InlineData(1, false, WizardStepId.DatabaseLogging)]
    [InlineData(2, false, WizardStepId.ProductDefaults)]
    [InlineData(3, false, WizardStepId.ProductSettings)]
    [InlineData(4, false, WizardStepId.Serilog)]
    public void DisplayIndexToStepId_ReturnsCorrectEnum(int displayIndex, bool cliVisible, WizardStepId expected)
    {
        DisplayIndexToStepId(displayIndex, cliVisible).Should().Be(expected);
    }

    // ── OnExpertModeChanged: no step change in most cases ────────────

    [Theory]
    [InlineData(WizardStepId.Repository)]
    [InlineData(WizardStepId.DatabaseLogging)]
    [InlineData(WizardStepId.ProductDefaults)]
    [InlineData(WizardStepId.ProductSettings)]
    [InlineData(WizardStepId.Serilog)]
    public void OnExpertModeChanged_TurnOn_NoCliTools_DoesNotChangeStep(WizardStepId step)
    {
        var wizard = CreateWizard(isExpertMode: false, currentStep: step);
        SimulateExpertModeChanged(wizard, EmptyCliTools(), newValue: true);
        wizard.CurrentStepId.Should().Be(step);
    }

    [Theory]
    [InlineData(WizardStepId.Repository)]
    [InlineData(WizardStepId.DatabaseLogging)]
    [InlineData(WizardStepId.ProductDefaults)]
    [InlineData(WizardStepId.ProductSettings)]
    [InlineData(WizardStepId.Serilog)]
    public void OnExpertModeChanged_TurnOff_NoCliTools_DoesNotChangeStep(WizardStepId step)
    {
        var wizard = CreateWizard(isExpertMode: true, currentStep: step);
        SimulateExpertModeChanged(wizard, EmptyCliTools(), newValue: false);
        wizard.CurrentStepId.Should().Be(step);
    }

    [Fact]
    public void OnExpertModeChanged_TurnOff_NoCliTools_OnCliTools_JumpsToProductDefaults()
    {
        var wizard = CreateWizard(isExpertMode: true, currentStep: WizardStepId.CliTools);
        SimulateExpertModeChanged(wizard, EmptyCliTools(), newValue: false);
        wizard.CurrentStepId.Should().Be(WizardStepId.ProductDefaults);
    }

    [Fact]
    public void OnExpertModeChanged_TurnOff_WithCliTools_OnCliTools_StaysOnCliTools()
    {
        // CLI Tools visible because CliTools.Count > 0 -- step stays visible even in Easy Mode
        var wizard = CreateWizard(isExpertMode: true, currentStep: WizardStepId.CliTools);
        SimulateExpertModeChanged(wizard, WithOneCliTool(), newValue: false);
        wizard.CurrentStepId.Should().Be(WizardStepId.CliTools);
    }

    [Theory]
    [InlineData(WizardStepId.Repository)]
    [InlineData(WizardStepId.CliTools)]
    [InlineData(WizardStepId.Serilog)]
    public void OnExpertModeChanged_TurnOn_WithCliTools_DoesNotChangeStep(WizardStepId step)
    {
        var wizard = CreateWizard(isExpertMode: false, currentStep: step);
        SimulateExpertModeChanged(wizard, WithOneCliTool(), newValue: true);
        wizard.CurrentStepId.Should().Be(step);
    }

    [Theory]
    [InlineData(WizardStepId.Repository)]
    [InlineData(WizardStepId.CliTools)]
    [InlineData(WizardStepId.Serilog)]
    public void OnExpertModeChanged_TurnOff_WithCliTools_DoesNotChangeStep(WizardStepId step)
    {
        var wizard = CreateWizard(isExpertMode: true, currentStep: step);
        SimulateExpertModeChanged(wizard, WithOneCliTool(), newValue: false);
        wizard.CurrentStepId.Should().Be(step);
    }

    // ── IsLastStep ────────────────────────────────────────────────────

    [Fact]
    public void IsLastStep_Serilog_IsTrue()
    {
        var wizard = CreateWizard(currentStep: WizardStepId.Serilog);
        (wizard.CurrentStepId == WizardStepId.Serilog).Should().BeTrue();
    }

    [Theory]
    [InlineData(WizardStepId.Repository)]
    [InlineData(WizardStepId.DatabaseLogging)]
    [InlineData(WizardStepId.CliTools)]
    [InlineData(WizardStepId.ProductDefaults)]
    [InlineData(WizardStepId.ProductSettings)]
    public void IsLastStep_NotSerilog_IsFalse(WizardStepId step)
    {
        var wizard = CreateWizard(currentStep: step);
        (wizard.CurrentStepId == WizardStepId.Serilog).Should().BeFalse();
    }

    // ── Idempotency ──────────────────────────────────────────────────

    [Fact]
    public void OnExpertModeChanged_TurnOn_AlreadyExpert_NoChange()
    {
        var wizard = CreateWizard(isExpertMode: true, currentStep: WizardStepId.ProductSettings);
        SimulateExpertModeChanged(wizard, EmptyCliTools(), newValue: true);
        wizard.CurrentStepId.Should().Be(WizardStepId.ProductSettings);
    }

    [Fact]
    public void OnExpertModeChanged_TurnOff_AlreadyEasy_NoChange()
    {
        var wizard = CreateWizard(isExpertMode: false, currentStep: WizardStepId.ProductSettings);
        SimulateExpertModeChanged(wizard, EmptyCliTools(), newValue: false);
        wizard.CurrentStepId.Should().Be(WizardStepId.ProductSettings);
    }
}
