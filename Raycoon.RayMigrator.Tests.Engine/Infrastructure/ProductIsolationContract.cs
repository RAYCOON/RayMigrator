using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// A run sees its own product only (ADR-026). The configuration of these scenarios carries a second product that
/// fails two data annotations of the engine: its target has no connection string and its migration files directory
/// does not exist. A run of the main product loads, validates and executes as if the broken product were not
/// there; a host built for the broken product fails on the broken product's own findings and nothing else.
/// </summary>
internal static class ProductIsolationContract
{
    public const string BrokenAlias = "Broken";

    /// <summary>A scenario with the main product and the broken one in its configuration file.</summary>
    public static ScenarioBuilder WithBrokenProduct(ScenarioBuilder builder) =>
        builder.WithSecondProduct(BrokenAlias, broken: true);

    /// <summary>
    /// migrate-up and info of the main product succeed beside the broken product: the host was built, the files
    /// were migrated, the product row was written, and the status reads nothing pending.
    /// </summary>
    public static async Task VerifyRunBesideBrokenProduct(ScenarioContext ctx)
    {
        ctx.RepositoryOptions.Should().NotBeNull("the host was built although the configuration carries a broken product");

        await ctx.MigrateUpAsync();

        ctx.AssertSuccess(true);
        ctx.CountMigrations().Should().BeGreaterThan(0, "the run migrated the files of its product");
        ctx.AssertProductExists(true);

        await ctx.RebuildForAsync(MigrationCommand.Info, MigrationRunMode.Migrate);
        var status = await ctx.InfoAsync();

        status.PendingMigrations.Should().Be(0, "every file of the product was migrated");
        status.TotalMigrationsExecuted.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// A host for the broken product refuses the configuration on the broken product's own findings: the options
    /// validation names the missing connection string of the one product the host loaded, and not the main one.
    /// </summary>
    public static async Task VerifyBrokenProductFailsOnItsOwn(ScenarioBuilder builder)
    {
        var act = () => builder.ForProduct(BrokenAlias).BuildAsync();

        var exception = (await act.Should().ThrowAsync<OptionsValidationException>("the broken product fails its own data annotations")).Which;
        exception.Message.Should().Contain("ConnectionString", "the target of the broken product has none")
            .And.Contain("MigrationFilesRootDirectory", "the directory of the broken product does not exist")
            .And.Contain("Products[0]", "the broken product is the only product the host loaded")
            .And.NotContain("Products[1]", "the main product is not part of this run");
    }
}
