using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Testing;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the testing package addresses repository tables through the engine's naming, prefix included: the query
/// helper counts the rows of a prefixed repository, and the cleanup helper drops the tables of one prefix and
/// leaves the other prefix alone.
/// </summary>
public class TestingHelpersPrefixTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"raymigrator-prefix-{Guid.NewGuid():N}.sqlite");

    private string ConnectionString => $"Data Source={_file}";

    public void Dispose()
    {
        if (File.Exists(_file))
            File.Delete(_file);
    }

    private async Task CreateRepositoryAsync(string prefix)
    {
        var service = new RepositorySchemaService(new TemplateCache(false, NullLogger<TemplateCache>.Instance), NullLogger<RepositorySchemaService>.Instance);
        await service.EnsureAsync(new RepositoryOptions { DatabaseType = "Sqlite", ConnectionString = ConnectionString, TableBaseName = prefix });
    }

    [Fact]
    public async Task QueryHelper_WithPrefix_ReadsThePrefixedTables()
    {
        // Arrange
        await CreateRepositoryAsync("a_");
        var withPrefix = new RepositoryQueryHelper("Sqlite", ConnectionString, "", "a_");
        var withoutPrefix = new RepositoryQueryHelper("Sqlite", ConnectionString, "");

        // Act & Assert
        withPrefix.TableExists("RepositorySchemaVersion").Should().BeTrue();
        withPrefix.CountRows("RepositorySchemaVersion").Should().Be(RepositorySchema.CurrentVersion);
        withoutPrefix.TableExists("RepositorySchemaVersion").Should().BeFalse("the unprefixed tables do not exist");
    }

    [Fact]
    public async Task CleanDatabase_WithPrefix_DropsOnlyThatRepository()
    {
        // Arrange: two repositories with different prefixes in one database
        await CreateRepositoryAsync("a_");
        await CreateRepositoryAsync("b_");
        var a = new RepositoryQueryHelper("Sqlite", ConnectionString, "", "a_");
        var b = new RepositoryQueryHelper("Sqlite", ConnectionString, "", "b_");

        // Act
        DatabaseCleanupHelper.CleanDatabase("Sqlite", ConnectionString, "", "a_");

        // Assert
        a.TableExists("RepositorySchemaVersion").Should().BeFalse();
        a.TableExists("MigrationRecordHistory").Should().BeFalse();
        b.TableExists("RepositorySchemaVersion").Should().BeTrue("the other prefix is another repository");
        b.CountRows("MigratorMeta").Should().Be(1);
    }
}
