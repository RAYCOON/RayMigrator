using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for RollbackErrorAction enum values, TOML parsing, and override resolution.
/// </summary>
public class RollbackErrorActionEnumTests
{
    [Fact]
    public void Undefined_Value_Is0()
    {
        ((byte)RollbackErrorAction.Undefined).Should().Be(0);
    }

    [Fact]
    public void Terminate_Value_Is10()
    {
        ((byte)RollbackErrorAction.Terminate).Should().Be(10);
    }

    [Fact]
    public void Ignore_Value_Is30()
    {
        ((byte)RollbackErrorAction.Ignore).Should().Be(30);
    }

    [Fact]
    public void GetValidEnumValues_ExcludesUndefined()
    {
        var values = MigrationService.GetValidEnumValues<RollbackErrorAction>();

        values.Should().NotContain("Undefined");
        values.Should().Contain("Terminate");
        values.Should().Contain("Ignore");
        values.Should().HaveCount(2);
    }
}

/// <summary>
/// P1: Tests for TOML parsing of RollbackErrorAction.
/// </summary>
public class RollbackErrorActionTomlParsingTests
{
    [Theory]
    [InlineData("Terminate", RollbackErrorAction.Terminate)]
    [InlineData("Ignore", RollbackErrorAction.Ignore)]
    public void ValidValues_ParsedCorrectly(string value, RollbackErrorAction expected)
    {
        MigrationService.ParseTomlConfig($"RollbackErrorAction = {value}",
            out _, out _, out _, out _, out _, out _, out _, out RollbackErrorAction? result, out _, out _, out _);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("terminate")]
    [InlineData("TERMINATE")]
    [InlineData("Terminate")]
    [InlineData("ignore")]
    [InlineData("IGNORE")]
    public void CaseInsensitive_ParsedCorrectly(string value)
    {
        MigrationService.ParseTomlConfig($"RollbackErrorAction = {value}",
            out _, out _, out _, out _, out _, out _, out _, out RollbackErrorAction? result, out _, out _, out _);

        result.Should().NotBeNull();
        result.Should().NotBe(RollbackErrorAction.Undefined);
    }

    [Fact]
    public void QuotedValue_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig("RollbackErrorAction = \"Terminate\"",
            out _, out _, out _, out _, out _, out _, out _, out RollbackErrorAction? result, out _, out _, out _);

        result.Should().Be(RollbackErrorAction.Terminate);
    }

    [Fact]
    public void NotPresent_IsNull()
    {
        MigrationService.ParseTomlConfig("UseTransaction = true",
            out _, out _, out _, out _, out _, out _, out _, out RollbackErrorAction? result, out _, out _, out _);

        result.Should().BeNull();
    }

    [Fact]
    public void Undefined_ThrowsException()
    {
        var act = () => MigrationService.ParseTomlConfig("RollbackErrorAction = Undefined",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*Undefined*")
            .WithMessage("*not allowed*");
    }

    [Fact]
    public void InvalidValue_ThrowsExceptionWithValidValues()
    {
        var act = () => MigrationService.ParseTomlConfig("RollbackErrorAction = Stop",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        var ex = act.Should().Throw<MigrationFileParsingException>().Which;
        ex.Message.Should().Contain("Stop");
        ex.Message.Should().Contain("Terminate");
        ex.Message.Should().Contain("Ignore");
    }

    [Fact]
    public void Rollback_NotValid_ThrowsException()
    {
        // Rollback is a MigrationErrorAction value, not valid for RollbackErrorAction
        var act = () => MigrationService.ParseTomlConfig("RollbackErrorAction = Rollback",
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out _, out _);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*Rollback*");
    }

    [Fact]
    public void WithOtherProperties_ParsedCorrectly()
    {
        MigrationService.ParseTomlConfig(
            "Description = \"Test\"\nUseTransaction = false\nRollbackErrorAction = Ignore\nMigrationErrorAction = Terminate",
            out bool useTransaction, out string description, out _, out _,
            out _, out _, out MigrationErrorAction? migrationErrorAction, out RollbackErrorAction? rollbackErrorAction, out _, out _, out _);

        useTransaction.Should().BeFalse();
        description.Should().Be("Test");
        migrationErrorAction.Should().Be(MigrationErrorAction.Terminate);
        rollbackErrorAction.Should().Be(RollbackErrorAction.Ignore);
    }
}

/// <summary>
/// P1: Tests for RollbackErrorAction override resolution (file override vs product default).
/// </summary>
public class RollbackErrorActionOverrideResolutionTests
{
    [Fact]
    public void FileOverrideIgnore_WinsOverProductTerminate()
    {
        var productOptions = new ProductOptions(null) { RollbackErrorAction = "Terminate" };
        var file = new MigrationFileInfo { RollbackErrorActionOverride = RollbackErrorAction.Ignore };

        var resolved = file.RollbackErrorActionOverride ?? productOptions.RollbackErrorActionEnum;

        resolved.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void FileOverrideTerminate_WinsOverProductIgnore()
    {
        var productOptions = new ProductOptions(null) { RollbackErrorAction = "Ignore" };
        var file = new MigrationFileInfo { RollbackErrorActionOverride = RollbackErrorAction.Terminate };

        var resolved = file.RollbackErrorActionOverride ?? productOptions.RollbackErrorActionEnum;

        resolved.Should().Be(RollbackErrorAction.Terminate);
    }

    [Fact]
    public void NoOverride_UsesProductDefault()
    {
        var productOptions = new ProductOptions(null) { RollbackErrorAction = "Ignore" };
        var file = new MigrationFileInfo { RollbackErrorActionOverride = null };

        var resolved = file.RollbackErrorActionOverride ?? productOptions.RollbackErrorActionEnum;

        resolved.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void ProductUndefined_FallbackToTerminate()
    {
        var productOptions = new ProductOptions(null) { RollbackErrorAction = null };
        var file = new MigrationFileInfo { RollbackErrorActionOverride = null };

        var resolved = file.RollbackErrorActionOverride ?? productOptions.RollbackErrorActionEnum;
        if (resolved == RollbackErrorAction.Undefined)
            resolved = RollbackErrorAction.Terminate;

        resolved.Should().Be(RollbackErrorAction.Terminate);
    }

    [Theory]
    [InlineData(null, "Terminate", RollbackErrorAction.Terminate)]
    [InlineData(null, "Ignore", RollbackErrorAction.Ignore)]
    [InlineData(RollbackErrorAction.Ignore, "Terminate", RollbackErrorAction.Ignore)]
    [InlineData(RollbackErrorAction.Terminate, "Ignore", RollbackErrorAction.Terminate)]
    public void TheoryTest_OverrideResolution(
        RollbackErrorAction? fileOverride, string productValue, RollbackErrorAction expected)
    {
        var productOptions = new ProductOptions(null) { RollbackErrorAction = productValue };
        var file = new MigrationFileInfo { RollbackErrorActionOverride = fileOverride };

        var resolved = file.RollbackErrorActionOverride ?? productOptions.RollbackErrorActionEnum;

        resolved.Should().Be(expected);
    }
}

/// <summary>
/// P1: Tests for RollbackErrorAction in migsettings hierarchy.
/// </summary>
public class RollbackErrorActionMigSettingsTests : IDisposable
{
    private readonly string _rootDir;
    private readonly MigrationService _service;

    public RollbackErrorActionMigSettingsTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"REAMigSettings_{Guid.NewGuid():N}");
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
        CreateSettingsFile("[RayMigrator]\nRollbackErrorAction = Ignore\n");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.RollbackErrorAction.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void TargetGroupMigSettings_Ignore_OverridesRootTerminate()
    {
        CreateSettingsFile("[RayMigrator]\nRollbackErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nRollbackErrorAction = Ignore\n",
            Path.Combine("Release 1.0", "Backend"));

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(
            Path.Combine(_rootDir, "Release 1.0", "Backend"), _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.RollbackErrorAction.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void EnvironmentSpecific_Ignore_OverridesBaseTerminate()
    {
        CreateSettingsFile("[RayMigrator]\nRollbackErrorAction = Terminate\n");
        CreateSettingsFile("[RayMigrator]\nRollbackErrorAction = Ignore\n",
            filename: "migsettings.Docker.txt");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");

        result.Should().HaveCount(1);
        result[_rootDir].RollbackErrorAction.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void MigSettings_NotSet_IsNull()
    {
        CreateSettingsFile("[RayMigrator]\nUseTransaction = true\n");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(_rootDir, _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.RollbackErrorAction.Should().BeNull();
    }

    [Fact]
    public void RollbackErrorAction_CoexistsWithMigrationErrorAction()
    {
        CreateSettingsFile("[RayMigrator]\nMigrationErrorAction = Rollback\nRollbackErrorAction = Ignore\n");

        var result = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        var resolved = _service.ResolveMigSettingsForFile(_rootDir, _rootDir, result);

        resolved.Should().NotBeNull();
        resolved!.MigrationErrorAction.Should().Be(MigrationErrorAction.Rollback);
        resolved!.RollbackErrorAction.Should().Be(RollbackErrorAction.Ignore);
    }
}

/// <summary>
/// P1: Tests for the full hierarchy combining Product + migsettings + TOML with RollbackErrorAction.
/// </summary>
public class RollbackErrorActionFullHierarchyTests : IDisposable
{
    private readonly string _rootDir;
    private readonly MigrationService _service;

    public RollbackErrorActionFullHierarchyTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"REAFullHierarchy_{Guid.NewGuid():N}");
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

    private RollbackErrorAction ResolveFullHierarchy(
        string productRollbackErrorAction,
        string fileDirectory,
        string? tomlContent)
    {
        var productOptions = new ProductOptions(null)
        {
            RollbackErrorAction = productRollbackErrorAction
        };

        var migSettings = _service.LoadMigSettingsDefaults(_rootDir, "Docker", "sql");
        RollbackErrorAction? rollbackErrorAction = null;
        bool fileHasRollbackErrorAction = false;

        if (!string.IsNullOrWhiteSpace(tomlContent))
        {
            MigrationService.ParseTomlConfig(tomlContent,
                out _, out _, out _, out _, out _, out _, out _, out rollbackErrorAction, out _, out _, out _);

            foreach (var line in tomlContent.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 0) continue;
                var key = trimmed.Substring(0, eqIdx).Trim().ToLowerInvariant();
                if (key == "rollbackerroraction") fileHasRollbackErrorAction = true;
            }
        }

        if (migSettings.Count > 0)
        {
            var defaults = _service.ResolveMigSettingsForFile(fileDirectory, _rootDir, migSettings);
            if (defaults != null && !fileHasRollbackErrorAction && defaults.RollbackErrorAction.HasValue)
            {
                rollbackErrorAction = defaults.RollbackErrorAction;
            }
        }

        var resolved = rollbackErrorAction ?? productOptions.RollbackErrorActionEnum;
        if (resolved == RollbackErrorAction.Undefined)
            resolved = RollbackErrorAction.Terminate;

        return resolved;
    }

    [Fact]
    public void ProductIgnore_NoOverrides_ResolvesToIgnore()
    {
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Ignore", fileDir, null);

        result.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void ProductTerminate_NoOverrides_ResolvesToTerminate()
    {
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(RollbackErrorAction.Terminate);
    }

    [Fact]
    public void TomlIgnore_OverridesProductTerminate()
    {
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, "RollbackErrorAction = Ignore");

        result.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void TomlTerminate_OverridesProductIgnore()
    {
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Ignore", fileDir, "RollbackErrorAction = Terminate");

        result.Should().Be(RollbackErrorAction.Terminate);
    }

    [Fact]
    public void MigSettingsIgnore_OverridesProductTerminate()
    {
        CreateSettingsFile("[RayMigrator]\nRollbackErrorAction = Ignore\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, null);

        result.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void TomlIgnore_OverridesMigSettingsTerminate_OverridesProductTerminate()
    {
        CreateSettingsFile("[RayMigrator]\nRollbackErrorAction = Terminate\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Terminate", fileDir, "RollbackErrorAction = Ignore");

        result.Should().Be(RollbackErrorAction.Ignore);
    }

    [Fact]
    public void TomlTerminate_OverridesMigSettingsIgnore()
    {
        CreateSettingsFile("[RayMigrator]\nRollbackErrorAction = Ignore\n");
        var fileDir = Path.Combine(_rootDir, "Release 1.0", "Backend");
        Directory.CreateDirectory(fileDir);

        var result = ResolveFullHierarchy("Ignore", fileDir, "RollbackErrorAction = Terminate");

        result.Should().Be(RollbackErrorAction.Terminate);
    }
}

/// <summary>
/// P1: Tests for ProductDefaultsPostConfigureOptions copying RollbackErrorAction from defaults.
/// </summary>
public class RollbackErrorActionDefaultsCopyTests
{
    [Fact]
    public void ProductDefaultsTerminate_CopiedToEmptyProduct()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                RollbackErrorAction = "Terminate"
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    RollbackErrorAction = null
                }
            }
        };

        var postConfigure = new Raycoon.RayMigrator.Core.Configuration.Validation.ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().RollbackErrorAction.Should().Be("Terminate");
    }

    [Fact]
    public void ProductDefaultsIgnore_CopiedToEmptyProduct()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                RollbackErrorAction = "Ignore"
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    RollbackErrorAction = null
                }
            }
        };

        var postConfigure = new Raycoon.RayMigrator.Core.Configuration.Validation.ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().RollbackErrorAction.Should().Be("Ignore");
    }

    [Fact]
    public void ProductExplicitValue_NotOverriddenByDefaults()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                RollbackErrorAction = "Terminate"
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    RollbackErrorAction = "Ignore"
                }
            }
        };

        var postConfigure = new Raycoon.RayMigrator.Core.Configuration.Validation.ProductDefaultsPostConfigureOptions();
        postConfigure.PostConfigure(null, options);

        options.Products!.First().RollbackErrorAction.Should().Be("Ignore");
    }

    [Fact]
    public void InvalidDefaultValue_NotCopied()
    {
        var options = new RayMigratorOptions
        {
            ProductDefaults = new ProductDefaultOptions(null)
            {
                RollbackErrorAction = "InvalidValue"
            },
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    RollbackErrorAction = null
                }
            }
        };

        // MergeDefaults alone: the invalid default is not copied. (PostConfigure would additionally
        // fail fast on the invalid ProductDefaults value itself — see ProductDefaultsPostConfigureOptionsTests.)
        Raycoon.RayMigrator.Core.Configuration.Validation.ProductDefaultsPostConfigureOptions.MergeDefaults(options);

        // Invalid default should not be copied
        options.Products!.First().RollbackErrorAction.Should().BeNull();
    }
}
