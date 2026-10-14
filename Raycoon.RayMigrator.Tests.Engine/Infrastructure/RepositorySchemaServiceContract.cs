using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// What the repository schema service promises on every engine: from the repository options alone it creates a
/// fresh repository with every schema step and the engine version, and a second call finds the current version and
/// changes nothing.
/// </summary>
internal static class RepositorySchemaServiceContract
{
    public static void VerifyEnsure(EngineConfig config)
    {
        DatabaseCleanupHelper.CleanDatabase(config.DatabaseType, config.ConnectionString, config.SchemaName);
        var service = new RepositorySchemaService(new TemplateCache(false, NullLogger<TemplateCache>.Instance), NullLogger<RepositorySchemaService>.Instance);
        var query = new RepositoryQueryHelper(config.DatabaseType, config.ConnectionString, config.SchemaName);

        var created = service.Ensure(Repository(config));

        created.Created.Should().BeTrue();
        created.Upgraded.Should().BeFalse();
        created.VersionBefore.Should().Be(0);
        created.VersionAfter.Should().Be(RepositorySchema.CurrentVersion);
        created.MigratorMetaId.Should().BePositive();
        created.DatabaseType.Should().Be(config.DatabaseType);
        query.CountRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion, "one row per applied step");
        query.CountRows("MigratorMeta").Should().Be(1);
        query.TableExists("MigrationRecordHistory").Should().BeTrue();

        var again = service.Ensure(Repository(config));

        again.Created.Should().BeFalse();
        again.Upgraded.Should().BeFalse();
        again.VersionBefore.Should().Be(RepositorySchema.CurrentVersion);
        again.MigratorMetaId.Should().Be(created.MigratorMetaId, "the engine version is registered once");
        query.CountRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        query.CountRows("MigratorMeta").Should().Be(1);
    }

    private static RepositoryOptions Repository(EngineConfig config) => new()
    {
        DatabaseType = config.DatabaseType,
        ConnectionString = config.ConnectionString,
        SchemaName = config.SchemaName,
        TableBaseName = ""
    };
}
