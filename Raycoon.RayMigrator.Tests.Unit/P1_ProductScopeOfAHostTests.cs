using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: a host that builds options in memory gets the product scope of a run from the context factory (ADR-026):
/// <see cref="RayMigratorOptionsPreparer.IsolateProduct"/> removes the other products before the preparer runs,
/// so a product that cannot be prepared does not stop the run of another, and an unknown product is named with
/// the aliases the options carry.
/// </summary>
public class ProductScopeOfAHostTests
{
    private const string BrokenAlias = "Broken";

    /// <summary>The test product plus a second one that fails two data annotations: no connection string, no directory.</summary>
    private static RayMigratorOptions OptionsWithABrokenProduct()
    {
        var options = ProfileTestContext.CreateOptions("Data Source=:memory:");
        options.Products!.Add(new ProductOptions
        {
            Alias = BrokenAlias,
            MigrationFilesRootDirectory = Path.Combine(Path.GetTempPath(), "RayMigrator_DoesNotExist_" + Guid.NewGuid().ToString("N")),
            TargetGroups =
            [
                new TargetGroupOptions
                {
                    Alias = "Backend",
                    DatabaseType = "Sqlite",
                    Targets = [new TargetOptions { Alias = "MainDB" }]
                }
            ]
        });
        return options;
    }

    private static MigrationContextFactory Factory() =>
        new(new AsyncLocalMigrationContextAccessor(), NullLogger<MigrationContextFactory>.Instance);

    [Fact]
    public void IsolateProduct_RemovesTheOtherProducts_AndKeepsEveryElementOfTheAlias()
    {
        var options = OptionsWithABrokenProduct();
        options.Products!.Add(new ProductOptions { Alias = ProfileTestContext.ProductAlias.ToUpperInvariant() });

        RayMigratorOptionsPreparer.IsolateProduct(options, ProfileTestContext.ProductAlias.ToLowerInvariant());

        options.Products.Select(p => p.Alias).Should().Equal(ProfileTestContext.ProductAlias, ProfileTestContext.ProductAlias.ToUpperInvariant());
    }

    [Fact]
    public void IsolateProduct_UnknownProduct_ThrowsWithTheAliases()
    {
        var options = OptionsWithABrokenProduct();

        var act = () => RayMigratorOptionsPreparer.IsolateProduct(options, "Nope");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*Product alias [Nope] not found*")
            .WithMessage($"*Available product aliases: {ProfileTestContext.ProductAlias}, {BrokenAlias}*");
        options.Products.Should().HaveCount(2, "a refused isolation changes nothing");
    }

    [Fact]
    public void IsolateProduct_WithoutProducts_LeavesTheOptionsForThePreparer()
    {
        var withoutList = ProfileTestContext.CreateOptions("Data Source=:memory:");
        withoutList.Products = null;
        var withoutAliases = ProfileTestContext.CreateOptions("Data Source=:memory:");
        withoutAliases.Products![0].Alias = " ";

        RayMigratorOptionsPreparer.IsolateProduct(withoutList, "Shop");
        RayMigratorOptionsPreparer.IsolateProduct(withoutAliases, "Shop");

        withoutList.Products.Should().BeNull();
        withoutAliases.Products.Should().ContainSingle("an element without an alias is reported by the data annotations, not here");
    }

    [Fact]
    public void Prepare_WithoutIsolation_FailsOnTheBrokenProduct_WithIsolation_Passes()
    {
        using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);

        var unscoped = () => RayMigratorOptionsPreparer.Prepare(OptionsWithABrokenProduct());
        unscoped.Should().Throw<ConfigurationValidationException>().WithMessage("*ConnectionString*");

        var scoped = OptionsWithABrokenProduct();
        RayMigratorOptionsPreparer.IsolateProduct(scoped, ProfileTestContext.ProductAlias);
        var act = () => RayMigratorOptionsPreparer.Prepare(scoped);
        act.Should().NotThrow("the broken product is not part of this run");
    }

    [Fact]
    public void Create_RunsTheProductBesideABrokenOne()
    {
        using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = OptionsWithABrokenProduct();

        var ctx = Factory().Create(options, ProfileTestContext.CreateConsoleOptions(MigrationCommand.Info, MigrationRunMode.Migrate));

        ctx.RayMigratorOptions.Products.Should().ContainSingle().Which.Alias.Should().Be(ProfileTestContext.ProductAlias);
        ctx.ProductTargetGroupOptionsEnumerable.Should().ContainSingle().Which.Alias.Should().Be(ProfileTestContext.TargetGroupAlias);
    }

    [Fact]
    public void Create_UnknownProduct_NamesTheAliasesOfTheOptions()
    {
        using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.Info,
            Product = "Nope",
            Environment = "Docker",
            RunMode = MigrationRunMode.Migrate,
            ShowStartupInfo = false,
            RevealSensitiveData = false
        };

        var act = () => Factory().Create(OptionsWithABrokenProduct(), consoleOptions);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage($"*Available product aliases: {ProfileTestContext.ProductAlias}, {BrokenAlias}*");
    }

    [Fact]
    public void NotFoundMessage_HintsAtTheCase_ListsTheAliases_OrSaysThereAreNone()
    {
        ProductAliasGuard.NotFoundMessage("shop", ["Shop", "Crm"]).Should().Contain("Did you mean [Shop]?").And.NotContain("Available product aliases");
        ProductAliasGuard.NotFoundMessage("Erp", ["Shop", "Crm"]).Should().Contain("Available product aliases: Shop, Crm");
        ProductAliasGuard.NotFoundMessage("Erp", []).Should().Contain("No products are configured");
    }
}
