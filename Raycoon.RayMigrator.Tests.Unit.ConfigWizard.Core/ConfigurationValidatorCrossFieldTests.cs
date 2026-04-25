
using FluentAssertions;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;
using Xunit;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Smoke tests for <see cref="ConfigurationValidator.ValidateAll(ConfigurationModel, ValidationCapability)"/>.
/// Detailed per-rule coverage lives in <c>Raycoon.RayMigrator.Tests.Unit.Validation/Rules/*Tests.cs</c>
/// (one test class per rule category — see validation-rules.md appendix).
/// </summary>
public class ConfigurationValidatorCrossFieldTests
{
    [Fact]
    public void ValidateAll_MinimalValidModel_ReturnsValid()
    {
        var model = BuildValidModel();
        var result = ConfigurationValidator.ValidateAll(model);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAll_DuplicateTargetGroupAlias_ReportsRule1_1()
    {
        var model = BuildValidModel();
        model.Products[0].TargetGroups.Add(new TargetGroupModel
        {
            Alias = model.Products[0].TargetGroups[0].Alias,
            DatabaseType = "SqlServer",
            Targets = { new TargetModel { Alias = "Other", ConnectionString = "Server=.;Database=other;Integrated Security=true;" } },
        });

        var result = ConfigurationValidator.ValidateAll(model);

        result.Errors.Should().Contain(e => e.Code == "RULE_1_1");
    }

    [Fact]
    public void ValidateAll_MissingEffectiveMigrationErrorAction_ReportsRule8_1()
    {
        var model = BuildValidModel();
        model.ProductDefaults.MigrationErrorAction = "";
        model.Products[0].MigrationErrorAction = new OverridableValue<string>();

        var result = ConfigurationValidator.ValidateAll(model);

        result.Errors.Should().Contain(e => e.Code == "RULE_8_1");
    }

    [Fact]
    public void ValidateAll_CapabilityGating_FilesystemWarningOnlyWhenFlagSet()
    {
        var model = BuildValidModel();
        model.Products[0].MigrationFilesRootDirectory = "/definitely/does/not/exist/anywhere";

        var structural = ConfigurationValidator.ValidateAll(model, ValidationCapability.Structural);
        structural.Warnings.Should().NotContain(w => w.Message.Contains("does not exist"));

        var withFilesystem = ConfigurationValidator.ValidateAll(model, ValidationCapability.Filesystem);
        withFilesystem.Warnings.Should().Contain(w => w.Message.Contains("does not exist"));
    }

    private static ConfigurationModel BuildValidModel() => new()
    {
        Repository = new RepositoryModel
        {
            DatabaseType = "SqlServer",
            ConnectionString = "Server=.;Database=x;Integrated Security=true;",
            SchemaName = "dbo",
        },
        Serilog = new SerilogModel { MinimumLevelDefault = "Information" },
        Products =
        {
            new ProductModel
            {
                Alias = "MyApp",
                MigrationFilesRootDirectory = "./Migrations",
                TargetGroups =
                {
                    new TargetGroupModel
                    {
                        Alias = "Backend",
                        DatabaseType = "SqlServer",
                        Targets =
                        {
                            new TargetModel
                            {
                                Alias = "Main",
                                ConnectionString = "Server=.;Database=app;Integrated Security=true;",
                            },
                        },
                    },
                },
            },
        },
    };
}
