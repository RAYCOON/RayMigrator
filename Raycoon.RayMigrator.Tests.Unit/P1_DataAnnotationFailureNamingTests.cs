using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: a data annotation failure names the elements by alias (ADR-026). The options framework names a list element
/// by its position, and since a run binds its own product only, <c>Products[0]</c> is the run's product whatever
/// position it has in the files; the alias tells the user which product failed.
/// </summary>
public class DataAnnotationFailureNamingTests
{
    private static RayMigratorOptions Options()
    {
        var options = ProfileTestContext.CreateOptions("Data Source=:memory:");
        options.Products![0].Alias = "Crm";
        options.CliTools = [new CliToolOptions { Alias = "psql", ExecutablePath = "psql", ArgumentTemplate = "{FilePath}", InputMode = "File" }];
        return options;
    }

    [Theory]
    [InlineData("DataAnnotation validation failed for 'RayMigratorOptions.Products[0].TargetGroups[0].Targets[0]' members: 'ConnectionString'",
        "DataAnnotation validation failed for 'RayMigratorOptions.Products[Crm].TargetGroups[Backend].Targets[MainDB]' members: 'ConnectionString'")]
    [InlineData("'RayMigratorOptions.Products[0].TargetGroups[0]' members: 'DatabaseType'", "'RayMigratorOptions.Products[Crm].TargetGroups[Backend]' members: 'DatabaseType'")]
    [InlineData("'RayMigratorOptions.Products[0]' members: 'MigrationFilesRootDirectory'", "'RayMigratorOptions.Products[Crm]' members: 'MigrationFilesRootDirectory'")]
    [InlineData("'RayMigratorOptions.CliTools[0]' members: 'InputMode'", "'RayMigratorOptions.CliTools[psql]' members: 'InputMode'")]
    [InlineData("'RayMigratorOptions.Products[3].TargetGroups[2].Targets[1]' members: 'Alias'", "'RayMigratorOptions.Products[3].TargetGroups[2].Targets[1]' members: 'Alias'")]
    [InlineData("'RayMigratorOptions.ProductDefaults' members: 'MigrationErrorAction'", "'RayMigratorOptions.ProductDefaults' members: 'MigrationErrorAction'")]
    public void NameElements_ReplacesPositionsByAliases_AndKeepsUnknownPositions(string failure, string expected)
    {
        RayMigratorDataAnnotationValidateOptions.NameElements(failure, Options()).Should().Be(expected);
    }

    [Fact]
    public void NameElements_KeepsThePositionOfAnElementWithoutAlias()
    {
        var options = Options();
        options.Products![0].TargetGroups![0].Targets![0].Alias = " ";

        RayMigratorDataAnnotationValidateOptions.NameElements("'RayMigratorOptions.Products[0].TargetGroups[0].Targets[0]' members: 'Alias'", options)
            .Should().Be("'RayMigratorOptions.Products[Crm].TargetGroups[Backend].Targets[0]' members: 'Alias'");
    }

    [Fact]
    public void Validate_NamesTheFailedElementsByAlias()
    {
        var options = Options();
        options.Products![0].TargetGroups![0].Targets![0].ConnectionString = null;

        var result = new RayMigratorDataAnnotationValidateOptions().Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle().Which.Should().Contain("RayMigratorOptions.Products[Crm].TargetGroups[Backend].Targets[MainDB]")
            .And.Contain("ConnectionString")
            .And.NotContain("Products[0]");
    }

    [Fact]
    public void Prepare_NamesTheFailedElementsByAlias()
    {
        using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = Options();
        options.Products![0].TargetGroups![0].Targets![0].ConnectionString = null;

        var act = () => RayMigratorOptionsPreparer.Prepare(options);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*RayMigratorOptions.Products[Crm].TargetGroups[Backend].Targets[MainDB]*ConnectionString*");
    }
}
