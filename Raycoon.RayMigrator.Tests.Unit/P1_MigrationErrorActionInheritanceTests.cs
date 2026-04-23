// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: MigrationErrorAction inheritance hierarchy tests.
/// Verifies the full resolution chain:
///   ProductDefaults (appsettings) → Product (appsettings) → migsettings.txt hierarchy → TOML header.
/// Tests the merge logic that ParseMigrationFile performs internally, by composing
/// the internal building blocks (ParseTomlConfig, ResolveMigSettingsForFile, etc.).
/// </summary>
public class MigrationErrorActionOverrideResolutionTests
{
    [Fact]
    public void MigrationFileInfo_MigrationErrorActionOverride_DefaultIsNull()
    {
        var info = new MigrationFileInfo();

        info.MigrationErrorActionOverride.Should().BeNull();
    }

    [Fact]
    public void OverrideNull_UsesProductDefault()
    {
        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Terminate" };
        var file = new MigrationFileInfo { MigrationErrorActionOverride = null };

        var resolved = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        resolved.Should().Be(MigrationErrorAction.Terminate);
    }

    [Fact]
    public void OverrideSet_OverridesProductDefault()
    {
        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Terminate" };
        var file = new MigrationFileInfo { MigrationErrorActionOverride = MigrationErrorAction.Rollback };

        var resolved = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        resolved.Should().Be(MigrationErrorAction.Rollback);
    }

    [Theory]
    [InlineData("Terminate", MigrationErrorAction.Rollback, MigrationErrorAction.Rollback)]
    [InlineData("Rollback", MigrationErrorAction.Terminate, MigrationErrorAction.Terminate)]
    [InlineData("RollbackErrorOnly", MigrationErrorAction.RollbackRelease, MigrationErrorAction.RollbackRelease)]
    [InlineData("RollbackRelease", MigrationErrorAction.RollbackErrorOnly, MigrationErrorAction.RollbackErrorOnly)]
    [InlineData("Terminate", MigrationErrorAction.Ignore, MigrationErrorAction.Ignore)]
    [InlineData("Ignore", MigrationErrorAction.Terminate, MigrationErrorAction.Terminate)]
    [InlineData("Ignore", MigrationErrorAction.Rollback, MigrationErrorAction.Rollback)]
    public void OverrideAlwaysWins_RegardlessOfProductValue(
        string productAction, MigrationErrorAction fileOverride, MigrationErrorAction expected)
    {
        var productOptions = new ProductOptions(null) { MigrationErrorAction = productAction };
        var file = new MigrationFileInfo { MigrationErrorActionOverride = fileOverride };

        var resolved = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        resolved.Should().Be(expected);
    }

    [Fact]
    public void OverrideNull_ProductNotSet_ResolvesToUndefined()
    {
        var productOptions = new ProductOptions(null); // MigrationErrorAction is null
        var file = new MigrationFileInfo { MigrationErrorActionOverride = null };

        var resolved = file.MigrationErrorActionOverride ?? productOptions.MigrationErrorActionEnum;

        resolved.Should().Be(MigrationErrorAction.Undefined);
    }
}

/// <summary>
/// P1: Tests the TOML → migsettings merge logic for MigrationErrorAction.
/// Simulates the merge that ParseMigrationFile performs by composing ParseTomlConfig
/// and ResolveMigSettingsForFile — the same pattern used in the actual code.
/// </summary>
public class MigrationErrorActionTomlMigSettingsMergeTests
{
    /// <summary>
    /// Simulates the merge logic from ParseMigrationFile:
    /// 1. Parse TOML to get file-level MigrationErrorAction
    /// 2. Apply migsettings defaults where TOML didn't set the value
    /// Returns the effective MigrationErrorAction? as it would be set on MigrationFileInfo.
    /// </summary>
    private static MigrationErrorAction? SimulateParseMigrationFileMerge(
        string? tomlContent,
        MigrationService.MigSettingsEntry? migSettingsDefaults)
    {
        MigrationErrorAction? migrationErrorAction = null;
        bool fileHasMigrationErrorAction = false;

        if (!string.IsNullOrWhiteSpace(tomlContent))
        {
            MigrationService.ParseTomlConfig(tomlContent,
                out _, out _, out _, out _, out _, out _, out migrationErrorAction, out _, out _, out _, out _);

            // Track if MigrationErrorAction was explicitly set in TOML
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

        // Apply migsettings defaults (same logic as ParseMigrationFile)
        if (migSettingsDefaults != null)
        {
            if (!fileHasMigrationErrorAction && migSettingsDefaults.MigrationErrorAction.HasValue)
                migrationErrorAction = migSettingsDefaults.MigrationErrorAction;
        }

        return migrationErrorAction;
    }

    [Fact]
    public void NoToml_NoMigSettings_ReturnsNull()
    {
        var result = SimulateParseMigrationFileMerge(null, null);

        result.Should().BeNull();
    }

    [Fact]
    public void TomlSetsValue_NoMigSettings_UsesToml()
    {
        var result = SimulateParseMigrationFileMerge(
            "MigrationErrorAction = Rollback", null);

        result.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void NoToml_MigSettingsSetsValue_UsesMigSettings()
    {
        var defaults = new MigrationService.MigSettingsEntry
        {
            MigrationErrorAction = MigrationErrorAction.Terminate
        };

        var result = SimulateParseMigrationFileMerge(null, defaults);

        result.Should().Be(MigrationErrorAction.Terminate);
    }

    [Fact]
    public void TomlAndMigSettingsBothSet_TomlWins()
    {
        var defaults = new MigrationService.MigSettingsEntry
        {
            MigrationErrorAction = MigrationErrorAction.Terminate
        };

        var result = SimulateParseMigrationFileMerge(
            "MigrationErrorAction = RollbackRelease", defaults);

        result.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void TomlSetsOtherProperties_MigSettingsMigrationErrorAction_Applied()
    {
        var defaults = new MigrationService.MigSettingsEntry
        {
            MigrationErrorAction = MigrationErrorAction.Rollback
        };

        // TOML sets UseTransaction but NOT MigrationErrorAction
        var result = SimulateParseMigrationFileMerge(
            "UseTransaction = false", defaults);

        result.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void EmptyToml_MigSettingsSetsValue_UsesMigSettings()
    {
        var defaults = new MigrationService.MigSettingsEntry
        {
            MigrationErrorAction = MigrationErrorAction.RollbackErrorOnly
        };

        var result = SimulateParseMigrationFileMerge("", defaults);

        result.Should().Be(MigrationErrorAction.RollbackErrorOnly);
    }

    [Fact]
    public void TomlSetsValue_MigSettingsNull_UsesToml()
    {
        var defaults = new MigrationService.MigSettingsEntry(); // MigrationErrorAction is null

        var result = SimulateParseMigrationFileMerge(
            "MigrationErrorAction = Terminate", defaults);

        result.Should().Be(MigrationErrorAction.Terminate);
    }

    [Theory]
    [InlineData(MigrationErrorAction.Terminate, MigrationErrorAction.Rollback, MigrationErrorAction.Rollback)]
    [InlineData(MigrationErrorAction.Rollback, MigrationErrorAction.Terminate, MigrationErrorAction.Terminate)]
    [InlineData(MigrationErrorAction.RollbackErrorOnly, MigrationErrorAction.RollbackRelease, MigrationErrorAction.RollbackRelease)]
    [InlineData(MigrationErrorAction.Terminate, MigrationErrorAction.Ignore, MigrationErrorAction.Ignore)]
    [InlineData(MigrationErrorAction.Ignore, MigrationErrorAction.Rollback, MigrationErrorAction.Rollback)]
    public void TomlAlwaysOverridesMigSettings(
        MigrationErrorAction migSettingsValue, MigrationErrorAction tomlValue, MigrationErrorAction expected)
    {
        var defaults = new MigrationService.MigSettingsEntry
        {
            MigrationErrorAction = migSettingsValue
        };

        var result = SimulateParseMigrationFileMerge(
            $"MigrationErrorAction = {tomlValue}", defaults);

        result.Should().Be(expected);
    }
}

/// <summary>
/// P1: Multi-level migsettings hierarchy tests for MigrationErrorAction.
/// Tests directory-based inheritance: root → release → targetGroup,
/// combined with environment-specific overrides at each level.
/// Uses LoadMigSettingsDefaults and ResolveMigSettingsForFile (actual internal methods).
/// </summary>
public class MigrationErrorActionMultiLevelMigSettingsTests : IDisposable
{
    private readonly string _rootDir;
    private readonly MigrationService _service;

    public MigrationErrorActionMultiLevelMigSettingsTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"MEAHierarchy_{Guid.NewGuid():N}");
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
    public void RootOnly_PropagatedToDeepFile()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void ReleaseOverridesRoot()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n", "Release 1.0");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void TargetGroupOverridesRelease()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n", "Release 1.0");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackErrorOnly\n",
            Path.Combine("Release 1.0", "Backend"));

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.RollbackErrorOnly);
    }

    [Fact]
    public void ThreeLevels_MostSpecificWins()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n", "Release 1.0");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackRelease\n",
            Path.Combine("Release 1.0", "Backend"));

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void MiddleLevelOnly_InheritedByDeeper()
    {
        // Only Release level sets MigrationErrorAction, not root or TargetGroup
        CreateSettingsFile("[RayMigrator]\nUseTransaction = false\n"); // root: no MigrationErrorAction
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackRelease\n", "Release 1.0");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.RollbackRelease);
        resolved.UseTransaction.Should().Be(false); // inherited from root
    }

    [Fact]
    public void NullChildLevel_InheritsFromParent()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        // Release level sets other properties but NOT MigrationErrorAction
        CreateSettingsFile("[RayMigrator]\nRunAlways = true\n", "Release 1.0");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback); // inherited from root
        resolved.RunAlways.Should().Be(true); // from release level
    }

    [Fact]
    public void EnvironmentSpecific_OverridesBase()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n",
            filename: "migsettings.Docker.txt");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_rootDir].MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void EnvironmentSpecific_NonMatchingEnv_Ignored()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n",
            filename: "migsettings.Production.txt");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_rootDir].MigrationErrorAction.Should().Be(MigrationErrorAction.Terminate);
    }

    [Fact]
    public void TargetGroupEnvSpecific_OverridesTargetGroupBase()
    {
        var tgDir = Path.Combine("Release 1.0", "Backend");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n", tgDir);
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackRelease\n", tgDir,
            "migsettings.Docker.txt");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void NoSettingsAtAnyLevel_ReturnsNull()
    {
        // No migsettings files at all
        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");

        result.Should().BeEmpty();
    }

    [Fact]
    public void UnrelatedRelease_DoesNotAffectOtherRelease()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n", "Release 2.0");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        // No settings apply to Release 1.0
        resolved.Should().BeNull();
    }

    [Fact]
    public void MixedProperties_MergedCorrectly()
    {
        // Root: MigrationErrorAction + UseTransaction
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\nUseTransaction = true\n");
        // Release: different MigrationErrorAction + RunAlways
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\nRunAlways = true\n", "Release 1.0");
        // TargetGroup: only Environments
        CreateSettingsFile("[RayMigrator]\nEnvironments = [\"Docker\"]\n",
            Path.Combine("Release 1.0", "Backend"));

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback); // from release
        resolved.UseTransaction.Should().Be(true);  // from root
        resolved.RunAlways.Should().Be(true);        // from release
        resolved.Environments.Should().Contain("Docker"); // from targetGroup
    }
}

/// <summary>
/// P1: Full end-to-end hierarchy tests combining appsettings (Product) + migsettings + TOML.
/// Verifies the complete priority chain: ProductDefaults → Product → migsettings → TOML.
/// </summary>
public class MigrationErrorActionFullHierarchyTests : IDisposable
{
    private readonly string _rootDir;
    private readonly MigrationService _service;

    public MigrationErrorActionFullHierarchyTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"MEAFullHier_{Guid.NewGuid():N}");
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

    /// <summary>
    /// Simulates the full resolution chain:
    /// 1. Product default (appsettings)
    /// 2. migsettings hierarchy
    /// 3. TOML header
    /// Returns the effective MigrationErrorAction.
    /// </summary>
    private MigrationErrorAction ResolveFullHierarchy(
        string productMigrationErrorAction,
        string fileDirectory,
        string? tomlContent)
    {
        var productOptions = new ProductOptions(null)
        {
            MigrationErrorAction = productMigrationErrorAction
        };

        // Step 1: Load and resolve migsettings
        var migSettings = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        MigrationErrorAction? migrationErrorAction = null;
        bool fileHasMigrationErrorAction = false;

        // Step 2: Parse TOML (if present)
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

        // Step 3: Apply migsettings defaults where TOML didn't set the value
        if (migSettings.Count > 0)
        {
            var defaults = _service.ResolveMigSettingsForFile(fileDirectory, _rootDir, migSettings);
            if (defaults != null && !fileHasMigrationErrorAction && defaults.MigrationErrorAction.HasValue)
            {
                migrationErrorAction = defaults.MigrationErrorAction;
            }
        }

        // Step 4: Final resolution: file override ?? product default
        return migrationErrorAction ?? productOptions.MigrationErrorActionEnum;
    }

    [Fact]
    public void NoOverrides_UsesProductDefault()
    {
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(MigrationErrorAction.Terminate);
    }

    [Fact]
    public void MigSettingsOnly_OverridesProduct()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void TomlOnly_OverridesProduct()
    {
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir,
            "MigrationErrorAction = RollbackRelease");

        result.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void TomlOverridesMigSettings_OverridesProduct()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir,
            "MigrationErrorAction = RollbackErrorOnly");

        result.Should().Be(MigrationErrorAction.RollbackErrorOnly);
    }

    [Fact]
    public void FullThreeLevelChain_TomlWins()
    {
        // Product: Terminate, MigSettings root: Rollback, TOML: RollbackRelease
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir,
            "MigrationErrorAction = RollbackRelease");

        result.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void FullThreeLevelChain_MigSettingsWinsOverProduct_WhenNoToml()
    {
        // Product: Terminate, MigSettings root: RollbackRelease, no TOML
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackRelease\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void TomlWithOtherProperties_DoesNotOverrideMigSettingsMigrationErrorAction()
    {
        // MigSettings sets MigrationErrorAction, TOML sets UseTransaction but NOT MigrationErrorAction
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir,
            "UseTransaction = false");

        result.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void MultiLevelMigSettings_DeepestWins_ThenFallsToProduct()
    {
        // Root: Terminate, Release: Rollback — file is in Backend
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n", "Release 1.0");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("RollbackErrorOnly", fileDir, null);

        result.Should().Be(MigrationErrorAction.Rollback); // from release migsettings
    }

    [Fact]
    public void TargetGroupMigSettings_OverridesReleaseAndRoot()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n", "Release 1.0");
        var tgDir = Path.Combine("Release 1.0", "Backend");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackRelease\n", tgDir);
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void EnvSpecificMigSettings_OverridesBase_ThenFallsToProduct()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n",
            filename: "migsettings.Docker.txt");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("RollbackErrorOnly", fileDir, null);

        result.Should().Be(MigrationErrorAction.Rollback); // Docker env override
    }

    [Fact]
    public void AllFourLevels_EachOverridesPrevious()
    {
        // Product: Terminate
        // Root migsettings: Rollback
        // Release migsettings: RollbackErrorOnly
        // TOML: RollbackRelease
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackErrorOnly\n", "Release 1.0");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir,
            "MigrationErrorAction = RollbackRelease");

        result.Should().Be(MigrationErrorAction.RollbackRelease);
    }

    [Fact]
    public void AllFourLevels_RemoveToml_ReleaseWins()
    {
        // Same as above but without TOML override
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackErrorOnly\n", "Release 1.0");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(MigrationErrorAction.RollbackErrorOnly);
    }

    [Fact]
    public void AllFourLevels_RemoveTomlAndRelease_RootMigSettingsWins()
    {
        // Same as above but without Release migsettings
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(MigrationErrorAction.Rollback);
    }

    [Fact]
    public void AllFourLevels_OnlyProduct_ProductWins()
    {
        // No migsettings, no TOML
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(MigrationErrorAction.Terminate);
    }

    [Fact]
    public void DifferentReleasesCanHaveDifferentValues()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\n", "Release 1.0");
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = RollbackRelease\n", "Release 2.0");

        var fileDir10 = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir10);
        var fileDir20 = Path.Combine(_rootDir, "Release 2.0", "Backend");
        Directory.CreateDirectory(fileDir20);

        var result10 = ResolveFullHierarchy("Terminate", fileDir10, null);
        var result20 = ResolveFullHierarchy("Terminate", fileDir20, null);

        result10.Should().Be(MigrationErrorAction.Rollback);
        result20.Should().Be(MigrationErrorAction.RollbackRelease);
    }
}
