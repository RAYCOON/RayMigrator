using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Pipeline;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the exit-code-4 message of <see cref="DirectModePipeline"/>. A missing Serilog section must be named as such
/// and must not be blamed on the optional product and environment files of the hierarchy, which are absent in most
/// setups (#31).
/// </summary>
public class MissingSerilogSectionMessageTests
{
    private const string BaseFile = "/cfg/appsettings.json";
    private const string EnvironmentFile = "/cfg/appsettings.Workshop.json";
    private const string ProductFile = "/cfg/appsettings.BookStore.json";
    private const string ProductEnvironmentFile = "/cfg/appsettings.BookStore.Workshop.json";

    /// <summary>The hierarchy of most setups: base and environment file present, the two product files absent.</summary>
    private static List<(string Filename, bool Found)> TypicalHierarchy() =>
    [
        (BaseFile, true),
        (EnvironmentFile, true),
        (ProductFile, false),
        (ProductEnvironmentFile, false),
    ];

    [Fact]
    public void Build_SerilogMissingInsideRayMigratorSection_NamesTheSerilogSectionAndListsTheLoadedFiles()
    {
        // Act
        string message = DirectModePipeline.BuildMissingSerilogSectionMessage(
            hasRayMigratorSection: true, TypicalHierarchy(), "BookStore", "Workshop");

        // Assert
        message.Should().StartWith("No 'Serilog' section found within the 'RayMigrator' section");
        message.Should().Contain("[BookStore]").And.Contain("[Workshop]");
        message.Should().Contain(BaseFile).And.Contain(EnvironmentFile);
        message.Should().Contain("add a Serilog logger configuration");
    }

    [Fact]
    public void Build_SerilogMissingInsideRayMigratorSection_DoesNotBlameTheOptionalFilesThatWereNotFound()
    {
        // Act
        string message = DirectModePipeline.BuildMissingSerilogSectionMessage(
            hasRayMigratorSection: true, TypicalHierarchy(), "BookStore", "Workshop");

        // Assert
        message.Should().NotContain(ProductFile, because: "the product and product+environment files are optional layers that are absent in most setups (#31)");
        message.Should().NotContain(ProductEnvironmentFile);
        message.Should().NotContain("not found").And.NotContain("verify the product and environment names");
    }

    [Fact]
    public void Build_SerilogMissingInsideRayMigratorSection_ListsTheLoadedFilesOnePerLineInHierarchyOrder()
    {
        // Act
        string message = DirectModePipeline.BuildMissingSerilogSectionMessage(
            hasRayMigratorSection: true, TypicalHierarchy(), "BookStore", "Workshop");

        // Assert
        string[] lines = message.Split(Environment.NewLine);
        lines.Should().ContainInOrder($"  - {BaseFile}", $"  - {EnvironmentFile}");
    }

    [Fact]
    public void Build_NoRayMigratorSectionInAnyLoadedFile_ListsLoadedAndMissingFilesAndSuggestsCheckingTheNames()
    {
        // Act
        string message = DirectModePipeline.BuildMissingSerilogSectionMessage(
            hasRayMigratorSection: false, TypicalHierarchy(), "BookStore", "Workshop");

        // Assert
        message.Should().StartWith("No 'RayMigrator' section found");
        message.Should().Contain("[BookStore]").And.Contain("[Workshop]");
        message.Should().Contain(BaseFile).And.Contain(EnvironmentFile);
        message.Should().Contain("not found").And.Contain(ProductFile).And.Contain(ProductEnvironmentFile);
        message.Should().Contain("verify the product and environment names", because: "a wrong name is the likely cause when no loaded file carries the section");
    }

    [Fact]
    public void Build_NoRayMigratorSectionAndEveryFileFound_DoesNotMentionMissingFiles()
    {
        // Arrange
        List<(string Filename, bool Found)> diagnostics =
        [
            (BaseFile, true),
            (EnvironmentFile, true),
            (ProductFile, true),
            (ProductEnvironmentFile, true),
        ];

        // Act
        string message = DirectModePipeline.BuildMissingSerilogSectionMessage(
            hasRayMigratorSection: false, diagnostics, "BookStore", "Workshop");

        // Assert
        message.Should().StartWith("No 'RayMigrator' section found");
        message.Should().NotContain("not found").And.NotContain("verify the product and environment names");
        message.Should().Contain("add a 'RayMigrator' section");
    }

    [Fact]
    public void Build_WithoutFileDiagnostics_ReturnsTheBootstrapMessage()
    {
        // Act
        string message = DirectModePipeline.BuildMissingSerilogSectionMessage(
            hasRayMigratorSection: true, configFileDiagnostics: null, "BookStore", "Workshop");

        // Assert
        message.Should().Contain("bootstrap configuration", because: "a source without file diagnostics gets the bootstrap message");
        message.Should().NotContain("[BookStore]");
    }

    [Fact]
    public async Task ExecuteAsync_LoadedConfigurationWithoutSerilogSection_ReturnsExitCode4()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"),
                """
                {
                    "RayMigrator": {
                        "Repository": { "DatabaseType": "Sqlite", "ConnectionString": "Data Source=:memory:" }
                    }
                }
                """);
            var sourceResult = await new JsonOptionsSource(tempDir).LoadAsync("BookStore", "Workshop");
            var options = new RayMigratorConsoleOptions
            {
                Command = MigrationCommand.Info,
                Product = "BookStore",
                Environment = "Workshop",
                RunMode = MigrationRunMode.Migrate,
                ShowStartupInfo = false,
                RevealSensitiveData = false,
            };

            // Act
            int exitCode = await DirectModePipeline.ExecuteAsync(
                [], sourceResult, options, "banner", "Workshop", "console parameter [-Environment]");

            // Assert
            exitCode.Should().Be(4, because: "a loaded configuration without a Serilog section is refused before the host is built");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
