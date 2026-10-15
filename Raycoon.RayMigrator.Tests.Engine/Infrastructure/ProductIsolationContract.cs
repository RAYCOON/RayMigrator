using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// A run sees its own product only (ADR-026). The configuration of these scenarios carries a second product that
/// a run cannot load: its target has no connection string, its target group names a database type no template
/// serves, and its migration files directory is a <c>{ENV:...}</c> placeholder whose variable is not set. A run of
/// the main product loads, validates and executes as if the broken product were not there; a host built for the
/// broken product fails on the broken product's own findings and nothing else.
/// </summary>
internal static class ProductIsolationContract
{
    public const string BrokenAlias = "Broken";

    /// <summary>A scenario with the main product and the broken one in its configuration file.</summary>
    public static ScenarioBuilder WithBrokenProduct(ScenarioBuilder builder) =>
        builder.WithSecondProduct(BrokenAlias, broken: true);

    /// <summary>
    /// migrate-up and info of the main product succeed beside the broken product: the host was built although the
    /// other product's variable is not set, its database type unknown and its connection string missing; the files
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
    /// A host for the broken product refuses the configuration on the broken product's own findings, in the order
    /// of the pipeline: first the placeholder whose variable is not set stops the start; once the variable names a
    /// directory that does not exist, the data annotations name the broken product, its missing connection string
    /// and its directory, and not the main product.
    /// </summary>
    public static async Task VerifyBrokenProductFailsOnItsOwn(ScenarioBuilder builder)
    {
        builder.ForProduct(BrokenAlias);

        var unresolved = () => builder.BuildAsync();
        (await unresolved.Should().ThrowAsync<ApplicationStartupException>("the variable of the broken product is not set")).Which.Message
            .Should().Contain(builder.UnsetVariableName);

        Environment.SetEnvironmentVariable(builder.UnsetVariableName, Path.Combine(Path.GetTempPath(), "RayMigrator_DoesNotExist_" + Guid.NewGuid().ToString("N")));
        try
        {
            var invalid = () => builder.BuildAsync();

            var exception = (await invalid.Should().ThrowAsync<OptionsValidationException>("the broken product fails its own data annotations")).Which;
            exception.Message.Should().Contain("ConnectionString", "the target of the broken product has none")
                .And.Contain("MigrationFilesRootDirectory", "the directory of the broken product does not exist")
                .And.Contain($"Products[{BrokenAlias}]", "a failure names the product by alias, not by its position in the reduced list")
                .And.NotContain($"Products[{ScenarioBuilder.MainProductAlias}]", "the main product is not part of this run");
        }
        finally
        {
            Environment.SetEnvironmentVariable(builder.UnsetVariableName, null);
        }
    }
}
