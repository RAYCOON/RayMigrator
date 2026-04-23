// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-4: MigSettings inheritance tests.
/// ParseMigSettingsFile parses migsettings.txt files (direct [RayMigrator] header, no /* */ wrapper).
/// Errors here lead to wrong default settings being applied to migration files.
/// </summary>
public class ParseMigSettingsFileTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MigrationService _service;

    public ParseMigSettingsFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MigSettingsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _service = TestFactories.CreateUninitializedMigrationService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateSettingsFile(string content, string filename = "migsettings.txt",
        string? subDir = null)
    {
        var dir = subDir != null ? Path.Combine(_tempDir, subDir) : _tempDir;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void UseTransaction_ParsedCorrectly()
    {
        var path = CreateSettingsFile("[RayMigrator]\nUseTransaction = false\n");

        var result = _service.ParseMigSettingsFile(path);

        result.UseTransaction.Should().Be(false);
    }

    [Fact]
    public void RunAlways_ParsedCorrectly()
    {
        var path = CreateSettingsFile("[RayMigrator]\nRunAlways = true\n");

        var result = _service.ParseMigSettingsFile(path);

        result.RunAlways.Should().Be(true);
    }

    [Fact]
    public void Environments_ParsedCorrectly()
    {
        var path = CreateSettingsFile("[RayMigrator]\nEnvironments = [\"Docker\", \"Production\"]\n");

        var result = _service.ParseMigSettingsFile(path);

        result.Environments.Should().NotBeNull();
        result.Environments.Should().HaveCount(2);
        result.Environments.Should().Contain("Docker");
        result.Environments.Should().Contain("Production");
    }

    [Fact]
    public void Targets_ParsedCorrectly()
    {
        var path = CreateSettingsFile("[RayMigrator]\nTargets = [\"Backend\"]\n");

        var result = _service.ParseMigSettingsFile(path);

        result.Targets.Should().NotBeNull();
        result.Targets.Should().HaveCount(1);
        result.Targets.Should().Contain("Backend");
    }

    [Fact]
    public void EmptySection_ReturnsAllNulls()
    {
        var path = CreateSettingsFile("[RayMigrator]\n");

        var result = _service.ParseMigSettingsFile(path);

        result.UseTransaction.Should().BeNull();
        result.RunAlways.Should().BeNull();
        result.Environments.Should().BeNull();
        result.Targets.Should().BeNull();
        result.MigrationErrorAction.Should().BeNull();
    }

    [Fact]
    public void NoSection_ReturnsAllNulls()
    {
        var path = CreateSettingsFile("just some text without section header\n");

        var result = _service.ParseMigSettingsFile(path);

        result.UseTransaction.Should().BeNull();
        result.RunAlways.Should().BeNull();
        result.Environments.Should().BeNull();
        result.Targets.Should().BeNull();
        result.MigrationErrorAction.Should().BeNull();
    }

    [Fact]
    public void AllProperties_ParsedCorrectly()
    {
        var path = CreateSettingsFile(
            "[RayMigrator]\nUseTransaction = true\nRunAlways = false\nEnvironments = [\"*\"]\nTargets = [\"Backend\", \"Frontend\"]\n");

        var result = _service.ParseMigSettingsFile(path);

        result.UseTransaction.Should().Be(true);
        result.RunAlways.Should().Be(false);
        result.Environments.Should().Contain("*");
        result.Targets.Should().HaveCount(2);
    }

    [Fact]
    public void OnlyExplicitKeysAreSet()
    {
        // Only UseTransaction is set — other properties should remain null
        var path = CreateSettingsFile("[RayMigrator]\nUseTransaction = false\n");

        var result = _service.ParseMigSettingsFile(path);

        result.UseTransaction.Should().Be(false);
        result.RunAlways.Should().BeNull();
        result.Environments.Should().BeNull();
        result.Targets.Should().BeNull();
        result.MigrationErrorAction.Should().BeNull();
    }

    [Fact]
    public void RequireRollbackFile_ParsedCorrectly()
    {
        var path = CreateSettingsFile("[RayMigrator]\nRequireRollbackFile = false\n");

        var result = _service.ParseMigSettingsFile(path);

        result.RequireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void RequireRollbackFile_NotPresent_IsNull()
    {
        var path = CreateSettingsFile("[RayMigrator]\nUseTransaction = true\n");

        var result = _service.ParseMigSettingsFile(path);

        result.RequireRollbackFile.Should().BeNull();
    }

    [Fact]
    public void MigrationErrorAction_ParsedCorrectly()
    {
        var path = CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");

        var result = _service.ParseMigSettingsFile(path);

        result.MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void MigrationErrorAction_NotPresent_IsNull()
    {
        var path = CreateSettingsFile("[RayMigrator]\nUseTransaction = true\n");

        var result = _service.ParseMigSettingsFile(path);

        result.MigrationErrorAction.Should().BeNull();
    }

    [Fact]
    public void MigrationErrorAction_Undefined_ThrowsException()
    {
        var path = CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Undefined\n");

        var act = () => _service.ParseMigSettingsFile(path);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*Undefined*")
            .WithMessage("*not allowed*");
    }
}

/// <summary>
/// P1-5: LoadMigSettingsDefaults tests.
/// Scans directories for migsettings files and merges base + environment-specific variants.
/// </summary>
public class LoadMigSettingsDefaultsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MigrationService _service;

    public LoadMigSettingsDefaultsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MigSettingsLoad_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _service = TestFactories.CreateUninitializedMigrationService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void NoSettingsFiles_ReturnsEmptyDictionary()
    {
        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().BeEmpty();
    }

    [Fact]
    public void BaseSettingsOnly_LoadedForDirectory()
    {
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nUseTransaction = false\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_tempDir].UseTransaction.Should().Be(false);
    }

    [Fact]
    public void EnvironmentSpecific_OverridesBase()
    {
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nUseTransaction = true\nRunAlways = false\n");
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.Docker.txt"),
            "[RayMigrator]\nUseTransaction = false\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_tempDir].UseTransaction.Should().Be(false); // overridden by Docker
        result[_tempDir].RunAlways.Should().Be(false);       // from base
    }

    [Fact]
    public void NonMatchingEnvironment_Ignored()
    {
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nUseTransaction = true\n");
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.Production.txt"),
            "[RayMigrator]\nUseTransaction = false\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_tempDir].UseTransaction.Should().Be(true); // Production variant not applied
    }

    [Fact]
    public void SubDirectories_LoadedSeparately()
    {
        // Root settings
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nRunAlways = false\n");

        // Subdirectory settings
        var subDir = Path.Combine(_tempDir, "Release 1.0");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "migsettings.txt"),
            "[RayMigrator]\nRunAlways = true\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(2);
        result[_tempDir].RunAlways.Should().Be(false);
        result[subDir].RunAlways.Should().Be(true);
    }

    [Fact]
    public void EnvironmentOnly_NoBase_StillLoaded()
    {
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.Docker.txt"),
            "[RayMigrator]\nUseTransaction = false\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_tempDir].UseTransaction.Should().Be(false);
    }

    [Fact]
    public void ThreeDirectoryLevels_AllLoaded()
    {
        // Root
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nUseTransaction = true\n");

        // Release level
        var releaseDir = Path.Combine(_tempDir, "Release 1.0");
        Directory.CreateDirectory(releaseDir);
        File.WriteAllText(Path.Combine(releaseDir, "migsettings.txt"),
            "[RayMigrator]\nRunAlways = true\n");

        // TargetGroup level
        var tgDir = Path.Combine(releaseDir, "Backend");
        Directory.CreateDirectory(tgDir);
        File.WriteAllText(Path.Combine(tgDir, "migsettings.txt"),
            "[RayMigrator]\nEnvironments = [\"Docker\"]\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(3);
    }

    [Fact]
    public void RequireRollbackFile_BaseLoaded()
    {
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nRequireRollbackFile = false\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_tempDir].RequireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void RequireRollbackFile_EnvOverridesBase()
    {
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nRequireRollbackFile = true\n");
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.Docker.txt"),
            "[RayMigrator]\nRequireRollbackFile = false\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_tempDir].RequireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void MigrationErrorAction_BaseLoaded()
    {
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nMigrationErrorAction = Terminate\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_tempDir].MigrationErrorAction.Should().Be(MigrationErrorAction.Terminate);
    }

    [Fact]
    public void MigrationErrorAction_EnvOverridesBase()
    {
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.txt"),
            "[RayMigrator]\nMigrationErrorAction = Terminate\n");
        File.WriteAllText(Path.Combine(_tempDir, "migsettings.Docker.txt"),
            "[RayMigrator]\nMigrationErrorAction = Rollback\n");

        var result = _service.LoadMigSettingsDefaults(_tempDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_tempDir].MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback);
    }
}

/// <summary>
/// P1-6: ResolveMigSettingsForFile tests.
/// Resolves the effective migsettings by walking up from file directory to root.
/// More specific directories override less specific ones.
/// </summary>
public class ResolveMigSettingsForFileTests
{
    private readonly MigrationService _service;

    public ResolveMigSettingsForFileTests()
    {
        _service = TestFactories.CreateUninitializedMigrationService();
    }

    private static MigrationService.MigSettingsEntry Entry(
        bool? useTransaction = null, bool? runAlways = null,
        List<string>? environments = null, List<string>? targets = null)
    {
        return new MigrationService.MigSettingsEntry
        {
            UseTransaction = useTransaction,
            RunAlways = runAlways,
            Environments = environments,
            Targets = targets
        };
    }

    [Fact]
    public void EmptySettings_ReturnsNull()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase);

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().BeNull();
    }

    [Fact]
    public void RootOnly_AppliedToSubdirectory()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = Entry(useTransaction: false)
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.UseTransaction.Should().Be(false);
    }

    [Fact]
    public void SubdirectoryOverridesRoot()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = Entry(useTransaction: true, runAlways: false),
            ["/root/Release 1.0"] = Entry(useTransaction: false)
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.UseTransaction.Should().Be(false); // overridden by release level
        result.RunAlways.Should().Be(false);        // inherited from root
    }

    [Fact]
    public void ThreeLevels_MostSpecificWins()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = Entry(useTransaction: true, runAlways: false,
                environments: new List<string> { "*" }),
            ["/root/Release 1.0"] = Entry(runAlways: true),
            ["/root/Release 1.0/Backend"] = Entry(environments: new List<string> { "Docker" })
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.UseTransaction.Should().Be(true);                      // from root
        result.RunAlways.Should().Be(true);                             // from release
        result.Environments.Should().Contain("Docker");                 // from targetgroup
        result.Environments.Should().NotContain("*");                   // overridden
    }

    [Fact]
    public void NoSettingsInFileDirectory_InheritsFromParent()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = Entry(useTransaction: false, runAlways: true)
        };

        // File is in /root/Release 1.0/Backend, but only root has settings
        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.UseTransaction.Should().Be(false);
        result.RunAlways.Should().Be(true);
    }

    [Fact]
    public void SettingsOnlyInMiddleLevel_Applied()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root/Release 1.0"] = Entry(useTransaction: false)
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.UseTransaction.Should().Be(false);
    }

    [Fact]
    public void UnrelatedDirectorySettings_NotApplied()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root/Release 2.0"] = Entry(useTransaction: false)
        };

        // File is in Release 1.0, not Release 2.0
        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().BeNull();
    }

    [Fact]
    public void NullValuesNotOverridden()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = Entry(useTransaction: true, runAlways: true),
            ["/root/Release 1.0"] = Entry() // all nulls
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.UseTransaction.Should().Be(true); // preserved from root
        result.RunAlways.Should().Be(true);        // preserved from root
    }

    [Fact]
    public void TargetsListOverridden_NotMerged()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = Entry(targets: new List<string> { "Target1", "Target2" }),
            ["/root/Release 1.0"] = Entry(targets: new List<string> { "Target3" })
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.Targets.Should().HaveCount(1);
        result.Targets.Should().Contain("Target3");
        result.Targets.Should().NotContain("Target1");
    }

    [Fact]
    public void RequireRollbackFile_InheritedFromParent()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = new MigrationService.MigSettingsEntry { RequireRollbackFile = false }
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.RequireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void RequireRollbackFile_OverriddenByChild()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = new MigrationService.MigSettingsEntry { RequireRollbackFile = true },
            ["/root/Release 1.0"] = new MigrationService.MigSettingsEntry { RequireRollbackFile = false }
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.RequireRollbackFile.Should().Be(false);
    }

    [Fact]
    public void RequireRollbackFile_NullChild_InheritsFromParent()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = new MigrationService.MigSettingsEntry { RequireRollbackFile = false },
            ["/root/Release 1.0"] = new MigrationService.MigSettingsEntry() // RequireRollbackFile is null
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.RequireRollbackFile.Should().Be(false); // inherited from root
    }

    [Fact]
    public void MigrationErrorAction_InheritedFromParent()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = new MigrationService.MigSettingsEntry { MigrationErrorAction = MigrationErrorAction.Terminate }
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.MigrationErrorAction.Should().Be(MigrationErrorAction.Terminate);
    }

    [Fact]
    public void MigrationErrorAction_OverriddenByChild()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = new MigrationService.MigSettingsEntry { MigrationErrorAction = MigrationErrorAction.Terminate },
            ["/root/Release 1.0"] = new MigrationService.MigSettingsEntry { MigrationErrorAction = MigrationErrorAction.Rollback }
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void MigrationErrorAction_NullChild_InheritsFromParent()
    {
        var settings = new Dictionary<string, MigrationService.MigSettingsEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["/root"] = new MigrationService.MigSettingsEntry { MigrationErrorAction = MigrationErrorAction.RollbackRelease },
            ["/root/Release 1.0"] = new MigrationService.MigSettingsEntry() // MigrationErrorAction is null
        };

        var result = _service.ResolveMigSettingsForFile("/root/Release 1.0/Backend", "/root", settings);

        result.Should().NotBeNull();
        result!.MigrationErrorAction.Should().Be(MigrationErrorAction.RollbackRelease); // inherited from root
    }
}
