using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Two repositories with different table prefixes share one database (and one schema where the engine has schemas):
/// after a run against the repository with prefix <c>a_</c>, a second repository with prefix <c>b_</c> is created in
/// the same place, and neither touches the other. Before 0.18.0 the primary key and foreign key names carried no
/// prefix and the second repository failed on the first name that already existed.
/// </summary>
internal static class RepositoryPrefixContract
{
    public const string FirstPrefix = "a_";
    public const string SecondPrefix = "b_";

    public static async Task VerifySecondPrefixBesideTheFirstAsync(ScenarioContext first, EngineConfig config)
    {
        first.CountMigrationRuns().Should().Be(1, "the first repository recorded its run");

        var service = new RepositorySchemaService(new TemplateCache(false, NullLogger<TemplateCache>.Instance), NullLogger<RepositorySchemaService>.Instance);
        var second = new RepositoryQueryHelper(config.DatabaseType, config.ConnectionString, config.SchemaName, SecondPrefix);
        try
        {
            var created = await service.EnsureAsync(new RepositoryOptions
            {
                DatabaseType = config.DatabaseType,
                ConnectionString = config.ConnectionString,
                SchemaName = config.SchemaName,
                TableBaseName = SecondPrefix
            });

            created.Created.Should().BeTrue("the second prefix is a repository of its own");
            created.VersionAfter.Should().Be(RepositorySchema.CurrentVersion);
            second.CountRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
            second.CountRows("MigratorMeta").Should().Be(1);
            second.CountRows("MigrationRun").Should().Be(0, "the run belongs to the first repository");
            first.CountMigrationRuns().Should().Be(1, "the second repository left the first one alone");
            first.CountRepoRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        }
        finally
        {
            DatabaseCleanupHelper.CleanDatabase(config.DatabaseType, config.ConnectionString, config.SchemaName, SecondPrefix);
        }
    }
}
