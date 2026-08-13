using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for MigrationErrorAction.Ignore — TOML parsing, migsettings resolution,
/// override hierarchy, and HandleMigrationError safety net.
/// </summary>
public class MigrationErrorActionIgnoreParsingTests
{
    [Fact]
    public void ParseTomlConfig_IgnoreValue_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig(
            "MigrationErrorAction = Ignore",
            out _, out _, out _, out _, out _, out _, out var migrationErrorAction, out _, out _, out _, out _);

        migrationErrorAction.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void MigrationFileInfo_OverrideSetToIgnore_ResolvesCorrectly()
    {
        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Terminate" };
        var file = new MigrationFileInfo { MigrationErrorActionOverride = MigrationErrorAction.Ignore };

        var resolved = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        resolved.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void ProductDefault_Ignore_ResolvedWhenNoOverride()
    {
        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Ignore" };
        var file = new MigrationFileInfo { MigrationErrorActionOverride = null };

        var resolved = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        resolved.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void IgnoreOverride_WinsOverProductRollback()
    {
        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Rollback" };
        var file = new MigrationFileInfo { MigrationErrorActionOverride = MigrationErrorAction.Ignore };

        var resolved = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        resolved.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void RollbackOverride_WinsOverProductIgnore()
    {
        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Ignore" };
        var file = new MigrationFileInfo { MigrationErrorActionOverride = MigrationErrorAction.Rollback };

        var resolved = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        resolved.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void Ignore_EnumValue_Is30()
    {
        ((byte)MigrationErrorAction.Ignore).Should().Be(30);
    }
}

/// <summary>
/// P1: Tests for MigrationErrorAction.Ignore in migsettings hierarchy.
/// </summary>
public class MigrationErrorActionIgnoreMigSettingsTests : IDisposable
{
    private readonly string _rootDir;
    private readonly MigrationService _service;

    public MigrationErrorActionIgnoreMigSettingsTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"MEAIgnore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        _service = TestFactories.CreateUninitializedMigrationService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    private void CreateSettingsFile(string content, string? subDir = null, string filename = "migsettings.txt")
    {
        var dir = subDir != null ? Path.Combine(_rootDir, subDir) : _rootDir;
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, filename), content);
    }

    [Fact]
    public void RootMigSettings_Ignore_PropagatedToDeepFile()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Ignore\n");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void TargetGroupMigSettings_Ignore_OverridesRootRollback()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Ignore\n",
            Path.Combine("Release 1.0", "Backend"));

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void TomlIgnore_OverridesMigSettingsRollback()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");

        MigrationService.ParseTomlConfig(
            "MigrationErrorAction = Ignore",
            out _, out _, out _, out _, out _, out _, out var migrationErrorAction, out _, out _, out _, out _);

        migrationErrorAction.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void EnvironmentSpecific_Ignore_OverridesBaseTerminate()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Ignore\n",
            filename: "migsettings.Docker.txt");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_rootDir].MigrationErrorAction.Should().Be(MigrationErrorAction.Ignore);
    }
}

/// <summary>
/// P1: Tests for HandleMigrationError with Ignore — safety net, should not throw or rollback.
/// </summary>
public class MigrationErrorActionIgnoreHandleMigrationErrorTests
{
    [Fact]
    public async Task HandleMigrationError_Ignore_LogsDebugAndDoesNotThrow()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Terminate" };
        var file = new MigrationFileInfo
        {
            Filename = "10_Test.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            MigrationErrorActionOverride = MigrationErrorAction.Ignore
        };

        var successRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>();

        // HandleMigrationError is private — invoke via reflection
        var method = typeof(MigrationService).GetMethod("HandleMigrationError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("HandleMigrationError should exist");

        // It's async, so we get a Task back
        var task = (Task)method!.Invoke(service, new object[] { productOptions, file, 42, successRecords })!;
        await task;

        // Should have logged the debug message
        logger.Entries.Should().Contain(e =>
            e.Message.Contains("Ignore") && e.Message.Contains("10_Test.sql"));
    }

    [Fact]
    public async Task HandleMigrationError_Ignore_NoRollbackAttempted()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Ignore" };
        var file = new MigrationFileInfo
        {
            Filename = "20_Seed.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            MigrationErrorActionOverride = null // Uses product default = Ignore
        };

        var successRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>
        {
            (new MigrationFileInfo { Filename = "10_Schema.sql", ReleaseVersion = "Release 1.0", TargetGroupAlias = "Backend" }, 1, "T1")
        };

        var method = typeof(MigrationService).GetMethod("HandleMigrationError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(service, new object[] { productOptions, file, 42, successRecords })!;
        await task;

        // Should NOT contain any rollback-related log messages
        logger.Entries.Should().NotContain(e =>
            e.Message.Contains("Rolling back"));
    }
}

/// <summary>
/// P1: Tests for the full hierarchy combining Product + migsettings + TOML with Ignore.
/// </summary>
public class MigrationErrorActionIgnoreFullHierarchyTests : IDisposable
{
    private readonly string _rootDir;
    private readonly MigrationService _service;

    public MigrationErrorActionIgnoreFullHierarchyTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"MEAIgnoreFull_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        _service = TestFactories.CreateUninitializedMigrationService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    private void CreateSettingsFile(string content, string? subDir = null, string filename = "migsettings.txt")
    {
        var dir = subDir != null ? Path.Combine(_rootDir, subDir) : _rootDir;
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, filename), content);
    }

    private MigrationErrorAction ResolveFullHierarchy(
        string productMigrationErrorAction,
        string fileDirectory,
        string? tomlContent)
    {
        var productOptions = new ProductOptions(null)
        {
            MigrationErrorAction = productMigrationErrorAction
        };

        var migSettings = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        MigrationErrorAction? migrationErrorAction = null;
        bool fileHasMigrationErrorAction = false;

        if (!string.IsNullOrWhiteSpace(tomlContent))
        {
            MigrationService.ParseTomlConfig(tomlContent,
                out _, out _, out _, out _, out _, out _, out migrationErrorAction, out _, out _, out _, out _);

            foreach (var line in tomlContent.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 0) continue;
                var key = trimmed.Substring(0, eqIdx).Trim().ToLowerInvariant();
                if (key == "migrationerroraction") fileHasMigrationErrorAction = true;
            }
        }

        if (migSettings.Count > 0)
        {
            var defaults = _service.ResolveMigSettingsForFile(fileDirectory, _rootDir, migSettings);
            if (defaults != null && !fileHasMigrationErrorAction && defaults.MigrationErrorAction.HasValue)
            {
                migrationErrorAction = defaults.MigrationErrorAction;
            }
        }

        return migrationErrorAction ?? productOptions.MigrationErrorActionEnum;
    }

    [Fact]
    public void ProductIgnore_NoOverrides_ResolvesToIgnore()
    {
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Ignore", fileDir, null);

        result.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void TomlIgnore_OverridesProductTerminate()
    {
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, "MigrationErrorAction = Ignore");

        result.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void MigSettingsIgnore_OverridesProductTerminate()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Ignore\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void TomlIgnore_OverridesMigSettingsRollback_OverridesProductTerminate()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, "MigrationErrorAction = Ignore");

        result.Should().Be(MigrationErrorAction.Ignore);
    }

    [Fact]
    public void TomlRollback_OverridesMigSettingsIgnore()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Ignore\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, "MigrationErrorAction = Rollback");

        result.Should().Be(MigrationErrorAction.Rollback);
    }
}
