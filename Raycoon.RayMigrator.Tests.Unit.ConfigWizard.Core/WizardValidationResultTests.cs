namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class WizardValidationResultTests
{
    [Fact]
    public void IsValid_NoErrors_ReturnsTrue()
    {
        var result = new WizardValidationResult();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithErrors_ReturnsFalse()
    {
        var result = new WizardValidationResult();
        result.AddError("Path", "Something went wrong.");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TotalIssues_CountsErrorsAndWarnings()
    {
        var result = new WizardValidationResult();
        result.AddError("Path1", "Error one.");
        result.AddError("Path2", "Error two.");
        result.AddWarning("Path3", "Warning one.");
        result.TotalIssues.Should().Be(3);
    }

    [Fact]
    public void TotalIssues_Empty_ReturnsZero()
    {
        var result = new WizardValidationResult();
        result.TotalIssues.Should().Be(0);
    }

    [Fact]
    public void Merge_CombinesErrorsAndWarnings()
    {
        var a = new WizardValidationResult();
        a.AddError("PathA", "Error from A.");
        a.AddWarning("PathA", "Warning from A.");

        var b = new WizardValidationResult();
        b.AddError("PathB", "Error from B.");
        b.AddWarning("PathB", "Warning from B.");

        a.Merge(b);

        a.Errors.Should().HaveCount(2);
        a.Warnings.Should().HaveCount(2);
        a.Errors.Should().Contain(e => e.Path == "PathA");
        a.Errors.Should().Contain(e => e.Path == "PathB");
    }

    [Fact]
    public void Merge_EmptyOther_DoesNotChangeResult()
    {
        var a = new WizardValidationResult();
        a.AddError("Path", "Error.");
        a.Merge(new WizardValidationResult());

        a.Errors.Should().HaveCount(1);
    }

    [Fact]
    public void AddError_SetsCorrectSeverity()
    {
        var result = new WizardValidationResult();
        result.AddError("P", "msg");
        result.Errors[0].Severity.Should().Be(ValidationSeverity.Error);
    }

    [Fact]
    public void AddWarning_SetsCorrectSeverity()
    {
        var result = new WizardValidationResult();
        result.AddWarning("P", "msg");
        result.Warnings[0].Severity.Should().Be(ValidationSeverity.Warning);
    }

    [Fact]
    public void WarningsOnly_DoesNotAffectIsValid()
    {
        var result = new WizardValidationResult();
        result.AddWarning("Path", "Just a warning.");
        result.IsValid.Should().BeTrue();
    }
}
