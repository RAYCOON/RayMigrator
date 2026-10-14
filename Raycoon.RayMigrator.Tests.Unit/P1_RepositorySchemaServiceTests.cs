using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <see cref="RepositorySchemaService"/> creates and upgrades a repository from the repository options alone
/// (no product, no context), on a SQLite file: a fresh repository gets every schema step and the engine version, a
/// second call changes nothing, a newer repository is refused, the documented defaults land in the options, and
/// incomplete or unknown options are refused before the database is touched.
/// </summary>
public class RepositorySchemaServiceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"raymigrator-schema-service-{Guid.NewGuid():N}.sqlite");

    private string ConnectionString => $"Data Source={_file}";

    private RepositoryOptions Repository() => new() { DatabaseType = "Sqlite", ConnectionString = ConnectionString };

    private RepositoryQueryHelper Query() => new("Sqlite", ConnectionString, "");

    private static RepositorySchemaService CreateService() =>
        new(new TemplateCache(false, NullLogger<TemplateCache>.Instance), NullLogger<RepositorySchemaService>.Instance);

    public void Dispose()
    {
        if (File.Exists(_file))
            File.Delete(_file);
    }

    [Fact]
    public void Ensure_FreshRepository_CreatesEveryStepAndRegistersTheEngineVersion()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.Ensure(Repository());

        // Assert
        result.Created.Should().BeTrue();
        result.Upgraded.Should().BeFalse();
        result.VersionBefore.Should().Be(0);
        result.VersionAfter.Should().Be(RepositorySchema.CurrentVersion);
        result.MigratorMetaId.Should().BePositive();
        result.DatabaseType.Should().Be("Sqlite");
        Query().CountRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        Query().CountRows("MigratorMeta").Should().Be(1);
        Query().TableExists("MigrationRecordHistory").Should().BeTrue();
    }

    [Fact]
    public void Ensure_Twice_LeavesTheRepositoryUnchanged()
    {
        // Arrange
        var service = CreateService();
        var first = service.Ensure(Repository());

        // Act
        var second = service.Ensure(Repository());

        // Assert
        second.Created.Should().BeFalse();
        second.Upgraded.Should().BeFalse();
        second.VersionBefore.Should().Be(RepositorySchema.CurrentVersion);
        second.VersionAfter.Should().Be(RepositorySchema.CurrentVersion);
        second.MigratorMetaId.Should().Be(first.MigratorMetaId);
        Query().CountRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        Query().CountRows("MigratorMeta").Should().Be(1);
    }

    [Fact]
    public void Ensure_RepositoryNewerThanTheEngine_IsRefused()
    {
        // Arrange
        var service = CreateService();
        service.Ensure(Repository());
        Query().ExecuteOnConnection(ConnectionString,
            "INSERT INTO \"RepositorySchemaVersion\" (\"Version\", \"AppliedAt\", \"RayMigratorVersion\") VALUES (99, datetime('now'), 'future')");

        // Act
        var act = () => service.Ensure(Repository());

        // Assert
        var ex = act.Should().Throw<RepositorySchemaException>().Which;
        ex.RepositoryVersion.Should().Be(99);
        ex.SupportedVersion.Should().Be(RepositorySchema.CurrentVersion);
    }

    [Fact]
    public void Ensure_WritesTheDocumentedDefaultsIntoTheOptions()
    {
        // Arrange
        var repository = Repository();

        // Act
        CreateService().Ensure(repository);

        // Assert
        repository.DbCommandTimeoutInSeconds.Should().Be(60);
        repository.DbCommandMaxRetries.Should().Be(100);
        repository.DbCommandWaitTimeInMsBeforeRetry.Should().Be(250);
        repository.HeartbeatIntervalInSeconds.Should().Be(30);
        repository.OrphanedRunThresholdInMinutes.Should().Be(2);
        repository.TableBaseName.Should().Be(string.Empty);
    }

    [Fact]
    public void Ensure_LogsTheCreatedRepositoryAtInformationLevel()
    {
        // Arrange
        var logger = new Helpers.CapturingLogger<RepositorySchemaService>();
        var service = new RepositorySchemaService(new TemplateCache(false, NullLogger<TemplateCache>.Instance), logger);

        // Act
        service.Ensure(Repository());

        // Assert
        logger.Entries.Should().ContainSingle(e => e.LogLevel == LogLevel.Information)
            .Which.Message.Should().Contain("Repository created").And.Contain($"schema version [{RepositorySchema.CurrentVersion}]");
    }

    [Fact]
    public void Ensure_WithoutDatabaseType_IsRefusedBeforeTheDatabaseIsTouched()
    {
        // Arrange
        var repository = new RepositoryOptions { ConnectionString = ConnectionString };

        // Act
        var act = () => CreateService().Ensure(repository);

        // Assert
        act.Should().Throw<ConfigurationValidationException>().Which.Message.Should().Contain("DatabaseType");
        File.Exists(_file).Should().BeFalse();
    }

    [Fact]
    public void Ensure_WithoutConnectionString_IsRefused()
    {
        // Arrange
        var repository = new RepositoryOptions { DatabaseType = "Sqlite" };

        // Act
        var act = () => CreateService().Ensure(repository);

        // Assert
        act.Should().Throw<ConfigurationValidationException>().Which.Message.Should().Contain("ConnectionString");
    }

    [Fact]
    public void Ensure_UnknownDatabaseType_IsRefusedWithTheAvailableTypes()
    {
        // Arrange
        var repository = new RepositoryOptions { DatabaseType = "Oracle", ConnectionString = ConnectionString };

        // Act
        var act = () => CreateService().Ensure(repository);

        // Assert
        act.Should().Throw<ConfigurationValidationException>().Which.Message.Should().Contain("[Oracle]").And.Contain("Sqlite");
    }

    [Fact]
    public void AddRayMigratorServices_RegistersTheSchemaService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new TemplateCache(false, NullLogger<TemplateCache>.Instance));
        services.AddRayMigratorServices(RayMigratorHostMode.Api);

        // Act
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IRepositorySchemaService>();

        // Assert
        service.Should().BeOfType<RepositorySchemaService>();
        provider.GetRequiredService<IRepositorySchemaService>().Should().BeSameAs(service, because: "the service is a singleton");
    }

    [Fact]
    public void Ensure_RefusesAPrefixTheRunsWouldRefuse()
    {
        // Arrange: longer than RULE_4_4 allows
        var tooLong = new RepositoryOptions { DatabaseType = "Sqlite", ConnectionString = ConnectionString, TableBaseName = "seventeen_chars__" };

        // Act
        var act = () => CreateService().Ensure(tooLong);

        // Assert
        act.Should().Throw<ConfigurationValidationException>().Which.Message.Should().Contain("RULE_4_4").And.Contain("TableBaseName");
        File.Exists(_file).Should().BeFalse("nothing is created for options a run would refuse");
    }

    [Fact]
    public void Ensure_UnknownDatabaseTypeInAnotherCase_NamesTheExactName()
    {
        var repository = new RepositoryOptions { DatabaseType = "sqlite", ConnectionString = ConnectionString };

        var act = () => CreateService().Ensure(repository);

        act.Should().Throw<ConfigurationValidationException>().Which.Message.Should().Contain("Did you mean [Sqlite]");
    }
}
