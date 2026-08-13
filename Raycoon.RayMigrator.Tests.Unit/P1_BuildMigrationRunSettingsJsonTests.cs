using System.Text.Json;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for BuildMigrationRunSettingsJson.
/// Ensures the JSON snapshot of migration settings is correctly structured, masked, and serialized.
/// </summary>
[Collection("SensitiveDataMasker")]
public class BuildMigrationRunSettingsJsonTests : IDisposable
{
    public BuildMigrationRunSettingsJsonTests()
    {
        SensitiveDataMasker.Reset();
    }

    public void Dispose()
    {
        SensitiveDataMasker.Reset();
    }

    private static MigrationContext CreateTestContext(
        bool revealSensitiveData = false,
        string product = "TestProduct",
        string environment = "Docker",
        MigrationCommand command = MigrationCommand.MigrateUp,
        MigrationRunMode runMode = MigrationRunMode.Migrate,
        string? targetReleaseVersion = null,
        string repositoryConnectionString = "Server=prod;Password=secret123",
        string targetConnectionString = "Server=target;Password=targetSecret")
    {
        var rayOptions = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "SqlServer",
                ConnectionString = repositoryConnectionString,
                SchemaName = "ray",
                TableBaseName = "",
                DbCommandTimeoutInSeconds = 60,
                DbCommandMaxRetries = 100,
                DbCommandWaitTimeInMsBeforeRetry = 250
            },
            ProductDefaults = new ProductDefaultOptions("UTF-8")
            {
                MigrationErrorAction = "Terminate",
                MigrationFilesExtension = "sql",
                MigrationRollbackFilesPreExtension = "rollback",
                MigrationFilesEncoding = "UTF-8",
                RequireRollbackFile = false,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Simultaneously",
                    HashValidationScope = "File",
                    TargetDefaults = new TargetDefaultsOptions
                    {
                        DbCommandTimeoutInSeconds = 20,
                        DbCommandMaxRetries = 0,
                        DbCommandWaitTimeInMsBeforeRetry = 250
                    }
                }
            },
            Products = new List<ProductOptions>
            {
                new("rollback")
                {
                    Alias = product,
                    MigrationFilesRootDirectory = "/path/to/migrations",
                    MigrationErrorAction = "Terminate",
                    MigrationFilesExtension = "sql",
                    MigrationRollbackFilesPreExtension = "rollback",
                    MigrationFilesEncoding = "UTF-8",
                    RequireRollbackFile = false,
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new()
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            TargetMigrationOrder = "Simultaneously",
                            HashValidationScope = "File",
                            Targets = new List<TargetOptions>
                            {
                                new()
                                {
                                    Alias = "MainDB",
                                    ConnectionString = targetConnectionString,
                                    DbCommandTimeoutInSeconds = 20,
                                    DbCommandMaxRetries = 0,
                                    DbCommandWaitTimeInMsBeforeRetry = 250
                                }
                            }
                        }
                    }
                },
                new("rollback")
                {
                    Alias = "OtherProduct",
                    MigrationFilesRootDirectory = "/path/to/other",
                    MigrationErrorAction = "Terminate",
                    MigrationFilesExtension = "sql",
                    MigrationRollbackFilesPreExtension = "rollback",
                    MigrationFilesEncoding = "UTF-8",
                    RequireRollbackFile = false,
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new()
                        {
                            Alias = "Other",
                            DatabaseType = "PostgreSQL",
                            TargetMigrationOrder = "Successively",
                            HashValidationScope = "File",
                            Targets = new List<TargetOptions>
                            {
                                new()
                                {
                                    Alias = "OtherDB",
                                    ConnectionString = "Host=other;Password=otherSecret",
                                    DbCommandTimeoutInSeconds = 30,
                                    DbCommandMaxRetries = 1,
                                    DbCommandWaitTimeInMsBeforeRetry = 500
                                }
                            }
                        }
                    }
                }
            }
        };

        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = command,
            Product = product,
            Environment = environment,
            RunMode = runMode,
            TargetReleaseVersion = targetReleaseVersion,
            ShowStartupInfo = true,
            RevealSensitiveData = revealSensitiveData,
            AllowOutOfOrder = false
        };

        return new MigrationContext(rayOptions, consoleOptions, "3.0.0");
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldContainAllTopLevelKeys()
    {
        var ctx = CreateTestContext();
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("RayMigratorVersion", out _).Should().BeTrue();
        root.TryGetProperty("ConsoleOptions", out _).Should().BeTrue();
        root.TryGetProperty("Repository", out _).Should().BeTrue();
        root.TryGetProperty("ProductDefaults", out _).Should().BeTrue();
        root.TryGetProperty("Product", out _).Should().BeTrue();
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldContainCorrectVersion()
    {
        var ctx = CreateTestContext();
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("RayMigratorVersion").GetString().Should().Be("3.0.0");
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ConnectionStringsMasked_WhenRevealSensitiveDataFalse()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("Server=prod;Password=secret123");
        SensitiveDataMasker.RegisterSensitiveValue("Server=target;Password=targetSecret");

        var ctx = CreateTestContext(revealSensitiveData: false);
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var repoConnStr = doc.RootElement.GetProperty("Repository").GetProperty("ConnectionString").GetString();
        repoConnStr.Should().Be(SensitiveDataMasker.MaskString);

        var targetConnStr = doc.RootElement.GetProperty("Product")
            .GetProperty("TargetGroups")[0]
            .GetProperty("Targets")[0]
            .GetProperty("ConnectionString").GetString();
        targetConnStr.Should().Be(SensitiveDataMasker.MaskString);
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ConnectionStringsVisible_WhenRevealSensitiveDataTrue()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: true);
        SensitiveDataMasker.RegisterSensitiveValue("Server=prod;Password=secret123");

        var ctx = CreateTestContext(revealSensitiveData: true);
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var repoConnStr = doc.RootElement.GetProperty("Repository").GetProperty("ConnectionString").GetString();
        repoConnStr.Should().Be("Server=prod;Password=secret123");

        var targetConnStr = doc.RootElement.GetProperty("Product")
            .GetProperty("TargetGroups")[0]
            .GetProperty("Targets")[0]
            .GetProperty("ConnectionString").GetString();
        targetConnStr.Should().Be("Server=target;Password=targetSecret");
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldOnlyContainSelectedProduct()
    {
        var ctx = CreateTestContext(product: "TestProduct");
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var productAlias = doc.RootElement.GetProperty("Product").GetProperty("Alias").GetString();
        productAlias.Should().Be("TestProduct");

        // No "Products" array - only the selected "Product"
        doc.RootElement.TryGetProperty("Products", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldContainAllTargetGroupsAndTargets()
    {
        var ctx = CreateTestContext();
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var targetGroups = doc.RootElement.GetProperty("Product").GetProperty("TargetGroups");
        targetGroups.GetArrayLength().Should().Be(1);

        var firstGroup = targetGroups[0];
        firstGroup.GetProperty("Alias").GetString().Should().Be("Backend");
        firstGroup.GetProperty("DatabaseType").GetString().Should().Be("SqlServer");

        var targets = firstGroup.GetProperty("Targets");
        targets.GetArrayLength().Should().Be(1);
        targets[0].GetProperty("Alias").GetString().Should().Be("MainDB");
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_EnumsShouldBeSerializedAsStrings()
    {
        var ctx = CreateTestContext(command: MigrationCommand.MigrateUp, runMode: MigrationRunMode.Migrate);
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var consoleOpts = doc.RootElement.GetProperty("ConsoleOptions");

        consoleOpts.GetProperty("Command").GetString().Should().Be("MigrateUp");
        consoleOpts.GetProperty("RunMode").GetString().Should().Be("Migrate");
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_NullValues_ShouldBeHandledCorrectly()
    {
        var ctx = CreateTestContext(targetReleaseVersion: null);
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var consoleOpts = doc.RootElement.GetProperty("ConsoleOptions");

        consoleOpts.GetProperty("TargetReleaseVersion").ValueKind.Should().Be(JsonValueKind.Null);
        consoleOpts.GetProperty("HashValidationScope").ValueKind.Should().Be(JsonValueKind.Null);
        consoleOpts.GetProperty("FixIssues").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldContainConsoleOptionsCorrectly()
    {
        var ctx = CreateTestContext(
            product: "TestProduct",
            environment: "Docker",
            command: MigrationCommand.MigrateDown,
            runMode: MigrationRunMode.Simulate,
            targetReleaseVersion: "Release 1.0");
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var consoleOpts = doc.RootElement.GetProperty("ConsoleOptions");

        consoleOpts.GetProperty("Command").GetString().Should().Be("MigrateDown");
        consoleOpts.GetProperty("Product").GetString().Should().Be("TestProduct");
        consoleOpts.GetProperty("Environment").GetString().Should().Be("Docker");
        consoleOpts.GetProperty("RunMode").GetString().Should().Be("Simulate");
        consoleOpts.GetProperty("TargetReleaseVersion").GetString().Should().Be("Release 1.0");
        consoleOpts.GetProperty("ShowStartupInfo").GetBoolean().Should().BeTrue();
        consoleOpts.GetProperty("RevealSensitiveData").GetBoolean().Should().BeFalse();
        consoleOpts.GetProperty("AllowOutOfOrder").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldContainRepositorySettings()
    {
        var ctx = CreateTestContext();
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var repo = doc.RootElement.GetProperty("Repository");

        repo.GetProperty("DatabaseType").GetString().Should().Be("SqlServer");
        repo.GetProperty("SchemaName").GetString().Should().Be("ray");
        repo.GetProperty("DbCommandTimeoutInSeconds").GetInt32().Should().Be(60);
        repo.GetProperty("DbCommandMaxRetries").GetInt32().Should().Be(100);
        repo.GetProperty("DbCommandWaitTimeInMsBeforeRetry").GetInt32().Should().Be(250);
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldContainProductDefaultsWithNestedDefaults()
    {
        var ctx = CreateTestContext();
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var doc = JsonDocument.Parse(json);
        var defaults = doc.RootElement.GetProperty("ProductDefaults");

        defaults.GetProperty("MigrationErrorAction").GetString().Should().Be("Terminate");
        defaults.GetProperty("MigrationFilesExtension").GetString().Should().Be("sql");
        defaults.GetProperty("RequireRollbackFile").GetBoolean().Should().BeFalse();

        var tgDefaults = defaults.GetProperty("TargetGroupDefaults");
        tgDefaults.GetProperty("TargetMigrationOrder").GetString().Should().Be("Simultaneously");
        tgDefaults.GetProperty("HashValidationScope").GetString().Should().Be("File");

        var tDefaults = tgDefaults.GetProperty("TargetDefaults");
        tDefaults.GetProperty("DbCommandTimeoutInSeconds").GetInt32().Should().Be(20);
        tDefaults.GetProperty("DbCommandMaxRetries").GetInt32().Should().Be(0);
        tDefaults.GetProperty("DbCommandWaitTimeInMsBeforeRetry").GetInt32().Should().Be(250);
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldProduceCompactJson()
    {
        var ctx = CreateTestContext();
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        // Compact JSON should not contain newlines
        json.Should().NotContain("\n");
        json.Should().NotContain("\r");
    }

    [Fact]
    public void BuildMigrationRunSettingsJson_ShouldBeValidJson()
    {
        var ctx = CreateTestContext();
        var json = MigrationService.BuildMigrationRunSettingsJson(ctx);

        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow("the output should be valid JSON");
    }
}
