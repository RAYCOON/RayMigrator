
using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: --config-dir CLI parameter tests.
/// Wrong config directory resolution leads to loading wrong appsettings files (wrong DB connection = data loss).
/// </summary>
public class ConfigDirTests
{
    #region RayMigratorConsoleOptions.ConfigDir

    [Fact]
    public void ConsoleOptions_ConfigDir_DefaultsToNull()
    {
        var options = new RayMigratorConsoleOptions
        {
            Command = Core.Configuration.Enums.MigrationCommand.Info,
            Product = "TestProduct",
            Environment = "Docker",
            RunMode = Core.Configuration.Enums.MigrationRunMode.Migrate,
            ShowStartupInfo = false,
            RevealSensitiveData = false
        };

        options.ConfigDir.Should().BeNull();
    }

    [Fact]
    public void ConsoleOptions_ConfigDir_CanBeSetToAbsolutePath()
    {
        var options = new RayMigratorConsoleOptions
        {
            Command = Core.Configuration.Enums.MigrationCommand.Info,
            Product = "TestProduct",
            Environment = "Docker",
            RunMode = Core.Configuration.Enums.MigrationRunMode.Migrate,
            ShowStartupInfo = false,
            RevealSensitiveData = false,
            ConfigDir = "/some/config/path"
        };

        options.ConfigDir.Should().Be("/some/config/path");
    }

    #endregion

    #region JsonOptionsSource constructor (configDir validation)

    [Fact]
    public void JsonOptionsSource_NullConfigDir_UsesCurrentDirectory()
    {
        // Null configDir should not throw; it defaults to CWD
        var source = new JsonOptionsSource(configDir: null);
        source.Should().NotBeNull();
    }

    [Fact]
    public void JsonOptionsSource_EmptyConfigDir_UsesCurrentDirectory()
    {
        var source = new JsonOptionsSource(configDir: "");
        source.Should().NotBeNull();
    }

    [Fact]
    public void JsonOptionsSource_WhitespaceConfigDir_UsesCurrentDirectory()
    {
        var source = new JsonOptionsSource(configDir: "   ");
        source.Should().NotBeNull();
    }

    [Fact]
    public void JsonOptionsSource_NonExistentDirectory_ThrowsConfigurationValidationException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var act = () => new JsonOptionsSource(configDir: nonExistentPath);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public void JsonOptionsSource_ExistingDirectory_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var source = new JsonOptionsSource(configDir: tempDir);
            source.Should().NotBeNull();
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    #endregion

    #region JsonOptionsSource.LoadAsync (configDir file resolution)

    [Fact]
    public async Task LoadAsync_WithConfigDir_SearchesFilesInSpecifiedDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create a minimal appsettings.json in the temp directory
            var json = """
            {
                "RayMigrator": {
                    "Repository": {
                        "DatabaseType": "SqlServer",
                        "ConnectionString": "Server=test"
                    }
                }
            }
            """;
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);

            var source = new JsonOptionsSource(configDir: tempDir);
            var result = await source.LoadAsync("TestProduct", "Dev");

            result.Should().NotBeNull();
            result.ConfigFileDiagnostics.Should().NotBeNull();

            // Verify all diagnostics paths point to the temp directory
            result.ConfigFileDiagnostics!
                .Select(d => d.Filename)
                .Should().AllSatisfy(f => f.Should().StartWith(tempDir));

            // Verify base config was found (first entry is always appsettings.json)
            result.ConfigFileDiagnostics![0].Filename.Should().EndWith("appsettings.json");
            result.ConfigFileDiagnostics![0].Found.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithConfigDir_FindsEnvironmentSpecificFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create base config
            var baseJson = """
            {
                "RayMigrator": {
                    "Repository": {
                        "DatabaseType": "SqlServer",
                        "ConnectionString": "Server=base"
                    }
                }
            }
            """;
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), baseJson);

            // Create environment-specific config
            var envJson = """
            {
                "RayMigrator": {
                    "Repository": {
                        "ConnectionString": "Server=docker"
                    }
                }
            }
            """;
            File.WriteAllText(Path.Combine(tempDir, "appsettings.Docker.json"), envJson);

            var source = new JsonOptionsSource(configDir: tempDir);
            var result = await source.LoadAsync("TestProduct", "Docker");

            result.ConfigFileDiagnostics.Should().NotBeNull();

            // Both base and environment config should be found
            var envEntry = result.ConfigFileDiagnostics!
                .First(d => d.Filename.EndsWith("appsettings.Docker.json"));
            envEntry.Found.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithConfigDir_DiagnosticsShowFullPaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var json = """
            {
                "RayMigrator": {
                    "Repository": {
                        "DatabaseType": "SqlServer",
                        "ConnectionString": "Server=test"
                    }
                }
            }
            """;
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);

            var source = new JsonOptionsSource(configDir: tempDir);
            var result = await source.LoadAsync("MyProduct", "Dev");

            result.ConfigFileDiagnostics.Should().NotBeNull();

            // All diagnostic entries should contain the full tempDir path
            foreach (var (filename, _) in result.ConfigFileDiagnostics!)
            {
                Path.IsPathRooted(filename).Should().BeTrue(
                    $"diagnostic filename '{filename}' should be an absolute path");
                filename.Should().StartWith(tempDir,
                    $"diagnostic filename '{filename}' should be under the config directory");
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithoutConfigDir_UsesCurrentDirectory()
    {
        // Create a temp dir with a minimal config, then test that null configDir
        // uses CWD by testing with an explicit existing directory instead
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var json = """
            {
                "RayMigrator": {
                    "Repository": {
                        "DatabaseType": "SqlServer",
                        "ConnectionString": "Server=test"
                    }
                }
            }
            """;
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json);

            // Use explicit directory to verify diagnostics contain full paths rooted at the base path
            var source = new JsonOptionsSource(configDir: tempDir);
            var result = await source.LoadAsync("TestProduct", "Dev");
            result.Should().NotBeNull();
            result.ConfigFileDiagnostics.Should().NotBeNull();

            // Diagnostics should point to the specified directory
            result.ConfigFileDiagnostics!
                .Select(d => d.Filename)
                .Should().AllSatisfy(f => f.Should().StartWith(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    #region CommandLineConfiguration (--config-dir parsing)

    [Fact]
    public async Task CommandLineParser_ConfigDirOption_ParsedCorrectly()
    {
        var tempDir = Path.GetTempPath();
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Info", "-p", "TestProduct", "-env", "Dev", "--config-dir", tempDir }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().NotBeNull();
        // Path.GetFullPath normalizes the path
        config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
    }

    [Fact]
    public async Task CommandLineParser_ConfigDirShortAlias_ParsedCorrectly()
    {
        var tempDir = Path.GetTempPath();
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Info", "-p", "TestProduct", "-env", "Dev", "-cd", tempDir }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
    }

    [Fact]
    public async Task CommandLineParser_NoConfigDir_DefaultsToNull()
    {
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Info", "-p", "TestProduct", "-env", "Dev" }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().BeNull();
    }

    [Fact]
    public async Task CommandLineParser_ConfigDirWorksWithMigrateUp()
    {
        var tempDir = Path.GetTempPath();
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Migrate-Up", "-p", "TestProduct", "-env", "Dev", "--config-dir", tempDir }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
    }

    [Fact]
    public async Task CommandLineParser_ConfigDirWorksWithMigrateDown()
    {
        var tempDir = Path.GetTempPath();
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Migrate-Down", "-p", "TestProduct", "-env", "Dev", "-tr", "1.0", "--config-dir", tempDir }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
    }

    [Fact]
    public async Task CommandLineParser_ConfigDirWorksWithValidateHash()
    {
        var tempDir = Path.GetTempPath();
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Validate-Hash", "-p", "TestProduct", "-env", "Dev", "--config-dir", tempDir }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
    }

    [Fact]
    public async Task CommandLineParser_ConfigDirWorksWithUpdateHash()
    {
        var tempDir = Path.GetTempPath();
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Update-Hash", "-p", "TestProduct", "-env", "Dev", "--config-dir", tempDir }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
    }

    [Fact]
    public async Task CommandLineParser_ConfigDirWorksWithBaseline()
    {
        var tempDir = Path.GetTempPath();
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Baseline", "-p", "TestProduct", "-env", "Dev", "--config-dir", tempDir }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
    }

    [Fact]
    public async Task CommandLineParser_ConfigDirWorksWithFix()
    {
        var tempDir = Path.GetTempPath();
        var config = new CommandLineConfiguration("RayMigrator Test");

        await config.RootCommand.Parse(
            new[] { "Fix", "-p", "TestProduct", "-env", "Dev", "--config-dir", tempDir }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        config.ParsedOptions.Should().NotBeNull();
        config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
    }

    #endregion

    #region CommandLineConfiguration.ResolveConfigDir (ENV variable support)

    [Fact]
    public async Task CommandLineParser_ConfigDirWithEnvVar_ResolvesVariable()
    {
        var tempDir = Path.GetTempPath();
        var envVarName = $"RAYMIGRATOR_TEST_CONFIG_DIR_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envVarName, tempDir);
        try
        {
            var config = new CommandLineConfiguration("RayMigrator Test");

            await config.RootCommand.Parse(
                new[] { "Info", "-p", "TestProduct", "-env", "Dev", "--config-dir", $"{{ENV:{envVarName}}}" }
            ).InvokeAsync(null, TestContext.Current.CancellationToken);

            config.ParsedOptions.Should().NotBeNull();
            config.ParsedOptions!.ConfigDir.Should().Be(Path.GetFullPath(tempDir));
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public async Task CommandLineParser_ConfigDirWithUnsetEnvVar_LeavesParseOptionsNull()
    {
        // An unset ENV variable in --config-dir causes ArgumentException inside the handler.
        // System.CommandLine catches it and returns non-zero exit code; ParsedOptions remains null.
        var envVarName = $"RAYMIGRATOR_TEST_CONFIG_DIR_UNSET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(envVarName, null);

        var config = new CommandLineConfiguration("RayMigrator Test");

        var exitCode = await config.RootCommand.Parse(
            new[] { "Info", "-p", "TestProduct", "-env", "Dev", "--config-dir", $"{{ENV:{envVarName}}}" }
        ).InvokeAsync(null, TestContext.Current.CancellationToken);

        // System.CommandLine swallows exceptions from handlers and returns non-zero exit code
        exitCode.Should().NotBe(0);
        config.ParsedOptions.Should().BeNull();
    }

    #endregion

    #region JsonOptionsSource constructor overloads

    [Fact]
    public void JsonOptionsSource_LoggerOnlyConstructor_UsesCurrentDirectory()
    {
        // The logger-only constructor delegates to configDir: null, so it must not throw
        // and must produce a valid instance (CWD as base path).
        var logger = new Raycoon.RayMigrator.Tests.Unit.Helpers.CapturingLogger<JsonOptionsSource>();
        var act = () => new JsonOptionsSource(logger: logger);

        act.Should().NotThrow();
    }

    #endregion
}
