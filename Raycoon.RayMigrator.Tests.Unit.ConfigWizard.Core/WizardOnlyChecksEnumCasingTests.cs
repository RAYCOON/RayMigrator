namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// The wizard's enum checks must accept what the engine accepts (#3): enum-typed configuration values are
/// matched case-insensitively, so a file the engine runs must not be flagged as invalid on import.
/// </summary>
public class WizardOnlyChecksEnumCasingTests
{
    [Theory]
    [InlineData("Rollback", "Terminate", "Successively", "File")]
    [InlineData("rollback", "terminate", "successively", "file")]
    [InlineData("ROLLBACK", "IGNORE", "SIMULTANEOUSLY", "SQLBLOCKS")]
    public void ProductDefaults_EnumValues_AreAcceptedInAnyCase(string mea, string rea, string tmo, string hvs)
    {
        var defaults = new ProductDefaultsModel { MigrationErrorAction = mea, RollbackErrorAction = rea };
        defaults.TargetGroupDefaults.TargetMigrationOrder = tmo;
        defaults.TargetGroupDefaults.HashValidationScope = hvs;
        var result = new WizardValidationResult();

        WizardOnlyChecks.RunProductDefaultsChecks(defaults, result);

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ProductDefaults_Typo_IsStillRejected()
    {
        var defaults = new ProductDefaultsModel { MigrationErrorAction = "Rollbak" };
        var result = new WizardValidationResult();

        WizardOnlyChecks.RunProductDefaultsChecks(defaults, result);

        result.Errors.Should().ContainSingle(e => e.Path == "ProductDefaults > MigrationErrorAction");
    }

    [Theory]
    [InlineData("File")]
    [InlineData("stdin")]
    [InlineData("STDIN")]
    public void CliTool_InputMode_IsAcceptedInAnyCase(string inputMode)
    {
        var tool = new CliToolModel { Alias = "sqlcmd", ExecutablePath = "sqlcmd", ArgumentTemplate = "-i {FilePath}", InputMode = inputMode };
        var result = new WizardValidationResult();

        WizardOnlyChecks.RunCliToolChecks(tool, "CliTools[0]", result);

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void CliTool_InvalidInputMode_IsStillRejected()
    {
        var tool = new CliToolModel { Alias = "sqlcmd", ExecutablePath = "sqlcmd", ArgumentTemplate = "-i {FilePath}", InputMode = "Pipe" };
        var result = new WizardValidationResult();

        WizardOnlyChecks.RunCliToolChecks(tool, "CliTools[0]", result);

        result.Errors.Should().ContainSingle(e => e.Path == "CliTools[0] > InputMode");
    }
}
