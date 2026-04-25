namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

public class WizardSetupAnswersTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var answers = new WizardSetupAnswers();
        answers.RepositoryDatabaseType.Should().Be("SqlServer");
        answers.Products.Should().BeEmpty();
        answers.UseDatabaseLogging.Should().BeTrue();
        answers.UseCliTools.Should().BeFalse();
    }

    [Fact]
    public void ProductSetup_DefaultValues()
    {
        var setup = new ProductSetup();
        setup.Alias.Should().BeEmpty();
        setup.Environments.Should().BeEmpty();
        setup.TargetGroups.Should().BeEmpty();
    }

    [Fact]
    public void TargetGroupSetup_DefaultValues()
    {
        var setup = new TargetGroupSetup();
        setup.Alias.Should().BeEmpty();
        setup.DatabaseType.Should().Be("SqlServer");
        setup.TargetAliases.Should().BeEmpty();
    }
}
