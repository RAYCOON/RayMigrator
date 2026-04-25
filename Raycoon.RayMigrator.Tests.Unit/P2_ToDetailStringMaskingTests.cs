
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Extensions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2-3: ToDetailString masking tests.
/// Verifies that ToDetailString correctly masks sensitive configuration values when RevealSensitiveData is false.
/// </summary>
public class ToDetailStringMaskingTests
{
    private static IConfigurationSection BuildConfigSection(
        string connectionString = "Server=prod;Database=mydb;Password=secret",
        string schemaName = "ray",
        string tableBaseName = "Migration",
        string rootDirectory = "/opt/migrations/production",
        string targetConnectionString = "Server=target-prod;Database=appdb;Password=targetpass")
    {
        var json = JsonSerializer.Serialize(new
        {
            RayMigrator = new
            {
                Environment = "Production",
                RunMode = "Migrate",
                MigrationErrorAction = "Terminate",
                MigrationFilesRootDirectory = rootDirectory,
                MigrationFilesExtension = "sql",
                MigrationRollbackFilesPreExtension = "rollback",
                MigrationFilesEncoding = "UTF-8",
                DatabaseAccessLayersRootDirectory = "/opt/dal",
                ShowStartupInfo = "true",
                RevealSensitiveData = "false",
                Repository = new
                {
                    DatabaseType = "SqlServer",
                    ConnectionString = connectionString,
                    SchemaName = schemaName,
                    TableBaseName = tableBaseName,
                    DbCommandTimeoutInSeconds = "30"
                },
                TargetGroups = new[]
                {
                    new
                    {
                        Alias = "Backend",
                        DatabaseType = "SqlServer",
                        TargetMigrationOrder = "Successively",
                        HashValidationScope = "File",
                        Targets = new[]
                        {
                            new
                            {
                                Alias = "MainDB",
                                ConnectionString = targetConnectionString,
                                DbCommandTimeoutInSeconds = "20",
                                DbCommandMaxRetries = "3",
                                DbCommandWaitTimeInMsBeforeRetry = "250"
                            }
                        }
                    }
                }
            }
        });

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        return config.GetSection("RayMigrator");
    }

    [Fact]
    public void ToDetailString_RevealFalse_MasksConnectionString()
    {
        var section = BuildConfigSection();

        var result = section.ToDetailString(revealSensitiveData: false);

        result.Should().Contain(ConfigurationConstants.NotRevealSensitiveDataString);
        result.Should().NotContain("Server=prod;Database=mydb;Password=secret");
        result.Should().NotContain("Server=target-prod;Database=appdb;Password=targetpass");
    }

    [Fact]
    public void ToDetailString_RevealTrue_ShowsConnectionString()
    {
        var section = BuildConfigSection();

        var result = section.ToDetailString(revealSensitiveData: true);

        result.Should().Contain("Server=prod;Database=mydb;Password=secret");
        result.Should().Contain("Server=target-prod;Database=appdb;Password=targetpass");
        result.Should().NotContain(ConfigurationConstants.NotRevealSensitiveDataString);
    }

    [Fact]
    public void ToDetailString_RevealFalse_MasksDirectoryPaths()
    {
        var section = BuildConfigSection();

        var result = section.ToDetailString(revealSensitiveData: false);

        result.Should().NotContain("/opt/migrations/production");
        result.Should().NotContain("/opt/dal");
    }

    [Fact]
    public void ToDetailString_RevealFalse_MasksSchemaName()
    {
        var section = BuildConfigSection(schemaName: "my_secret_schema");

        var result = section.ToDetailString(revealSensitiveData: false);

        result.Should().NotContain("my_secret_schema");
    }

    [Fact]
    public void ToDetailString_RevealFalse_DoesNotMaskNonSensitiveValues()
    {
        var section = BuildConfigSection();

        var result = section.ToDetailString(revealSensitiveData: false);

        // Non-sensitive values should still be visible
        result.Should().Contain("Production");
        result.Should().Contain("Migrate");
        result.Should().Contain("Terminate");
        result.Should().Contain("sql");
        result.Should().Contain("SqlServer");
        result.Should().Contain("30");
        result.Should().Contain("Backend");
        result.Should().Contain("MainDB");
    }
}
