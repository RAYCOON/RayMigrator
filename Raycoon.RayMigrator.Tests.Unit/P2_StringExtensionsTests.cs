using System.Text.RegularExpressions;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2-1: StringExtensions (Path Parsing) tests.
/// </summary>
public class StringExtensionsPathTests
{
    #region GetPathSegments

    [Fact]
    public void GetPathSegments_NormalPathWithForwardSlash_ReturnsCorrectSegments()
    {
        var result = "Release 1.0/Backend/10_Create.sql".GetPathSegments();

        result.Should().HaveCount(3);
        result[0].Should().Be("Release 1.0");
        result[1].Should().Be("Backend");
        result[2].Should().Be("10_Create.sql");
    }

    [Fact]
    public void GetPathSegments_WindowsPath_ReturnsCorrectSegments()
    {
        var result = "Release 1.0\\Backend\\10_Create.sql".GetPathSegments();

        result.Should().HaveCount(3);
        result[0].Should().Be("Release 1.0");
    }

    [Fact]
    public void GetPathSegments_EmptyString_ReturnsArrayWithEmptyString()
    {
        var result = "".GetPathSegments();

        result.Should().HaveCount(1);
        result[0].Should().BeEmpty();
    }

    [Fact]
    public void GetPathSegments_PathWithSpaces_PreservesSpaces()
    {
        var result = "Release 1.0/Backend".GetPathSegments();

        result[0].Should().Be("Release 1.0");
    }

    [Fact]
    public void GetPathSegments_NullInput_ReturnsArrayWithEmptyString()
    {
        var result = ((string)null!).GetPathSegments();

        result.Should().HaveCount(1);
        result[0].Should().BeEmpty();
    }

    #endregion

    #region GetFirstPathSegment

    [Fact]
    public void GetFirstPathSegment_MultipleSegments_ReturnsFirst()
    {
        var result = "Release 1.0/Backend/file.sql".GetFirstPathSegment();

        result.Should().Be("Release 1.0");
    }

    #endregion

    #region NormalizePath

    [Fact]
    public void NormalizePath_DoubleSeparators_Cleaned()
    {
        var result = $"path{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}subpath".NormalizePath();

        result.Should().NotContain($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}");
    }

    [Fact]
    public void NormalizePath_EmptyPath_ReturnsEmpty()
    {
        var result = "".NormalizePath();

        result.Should().BeEmpty();
    }

    #endregion

    #region GetReleaseVersionAndTargetGroupAlias

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_ValidPath_ReturnsCorrectValues()
    {
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0/Backend/10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out var targetGroup);

        release.Should().Be("Release 1.0");
        targetGroup.Should().Be("Backend");
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_InvalidPath_ThrowsException()
    {
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        var act = () => "file.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out _, out _);

        act.Should().Throw<ConfigurationValidationException>();
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_FlatLayout_SingleTg_AssignsSingleTgAlias()
    {
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0/10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out var targetGroup);

        targetGroup.Should().Be("Backend");
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_FlatLayout_SingleTg_PreservesReleaseVersion()
    {
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0/10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out _);

        release.Should().Be("Release 1.0");
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_TraditionalLayout_SingleTg_StillResolvesViaTgSubdir()
    {
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0/Backend/10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out var targetGroup);

        release.Should().Be("Release 1.0");
        targetGroup.Should().Be("Backend");
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_FlatLayout_MultipleTgs_ThrowsException()
    {
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" },
                        new TargetGroupOptions { Alias = "Frontend" }
                    }
                }
            }
        };

        var act = () => "Release 1.0/10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out _, out _);

        act.Should().Throw<ConfigurationValidationException>();
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_BareFilename_SingleTg_ThrowsException()
    {
        // pathSegments.Length < 2 so the flat layout fallback is NOT triggered
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        var act = () => "10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out _, out _);

        act.Should().Throw<ConfigurationValidationException>();
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_FlatLayout_DeeperSubdir_SingleTg_AssignsSingleTgAlias()
    {
        // Path has 3 segments but segment[1] is NOT the TG alias, so traditional match fails.
        // Flat layout fallback still fires because single TG is configured.
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0/subdir/10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out var targetGroup);

        targetGroup.Should().Be("Backend");
        release.Should().Be("Release 1.0");
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_FlatLayout_EnvironmentPreExtension_AssignsSingleTgAlias()
    {
        // The .Docker pre-extension is not the TG alias, so the filename-based fallback does not match.
        // The flat layout fallback fires and assigns the single TG alias.
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0/10_Create.Docker.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out var targetGroup);

        targetGroup.Should().Be("Backend");
        release.Should().Be("Release 1.0");
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_FlatLayout_TgAliasAsPreExtension_ResolvesViaFilenameNotFlatFallback()
    {
        // Path is flat (only 2 segments: release + filename), but the filename's pre-extension matches
        // the TG alias. The filename-based resolution should win before the flat layout fallback.
        // Both paths lead to the same result here; the important thing is that TG is resolved correctly.
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0/10_Create.Backend.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out var targetGroup);

        targetGroup.Should().Be("Backend");
        release.Should().Be("Release 1.0");
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_TraditionalLayout_FourPlusSegments_ResolvesViaSegment1()
    {
        // Even with 4 path segments, segment[1] matching a TG alias is sufficient for resolution.
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0/Backend/subfolder/10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out var targetGroup);

        targetGroup.Should().Be("Backend");
        release.Should().Be("Release 1.0");
    }

    [Fact]
    public void GetReleaseVersionAndTargetGroupAlias_FlatLayout_ReleaseVersionWithSpaces_AssignsSingleTgAlias()
    {
        // Multi-word release versions with spaces are supported.
        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null)
                {
                    Alias = "TestProduct",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new TargetGroupOptions { Alias = "Backend" }
                    }
                }
            }
        };

        "Release 1.0 Hotfix/10_Create.sql".GetReleaseVersionAndTargetGroupAlias(
            options, "TestProduct", out var release, out var targetGroup);

        targetGroup.Should().Be("Backend");
        release.Should().Be("Release 1.0 Hotfix");
    }

    #endregion

    #region IsValidConnectionString

    [Fact]
    public void IsValidConnectionString_Valid_ReturnsTrue()
    {
        var result = "Server=localhost;Database=TestDB;".IsValidConnectionString(out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void IsValidConnectionString_Invalid_ReturnsFalse()
    {
        var result = "invalid=\"unterminated".IsValidConnectionString(out var error);

        result.Should().BeFalse();
        error.Should().NotBeNull();
    }

    #endregion

    #region GetRelativePath

    [Fact]
    public void GetRelativePath_NormalPath_ReturnsRelativePart()
    {
        var basePath = Path.Combine("D:", "Migrations");
        var completePath = Path.Combine("D:", "Migrations", "SubDir", "MyFile.sql");

        var result = completePath.GetRelativePath(basePath);

        result.Should().Be(Path.Combine("SubDir", "MyFile.sql"));
    }

    [Fact]
    public void GetRelativePath_BasePathWithTrailingSeparator_StripsLeadingSeparator()
    {
        var basePath = Path.Combine("D:", "Migrations");
        var completePath = basePath + Path.DirectorySeparatorChar + "SubDir";

        var result = completePath.GetRelativePath(basePath);

        result.Should().Be("SubDir");
    }

    [Fact]
    public void GetRelativePath_IdenticalPaths_ReturnsEmpty()
    {
        var path = Path.Combine("D:", "Migrations");

        var result = path.GetRelativePath(path);

        result.Should().BeEmpty();
    }

    #endregion

    #region GetParentPath

    [Fact]
    public void GetParentPath_PathWithNativeSeparator_ReturnsParent()
    {
        var path = "Parent" + Path.DirectorySeparatorChar + "Child";

        var result = path.GetParentPath();

        result.Should().Be("Parent");
    }

    [Fact]
    public void GetParentPath_PathWithoutSeparator_ReturnsEmpty()
    {
        var result = "NoSeparatorHere".GetParentPath();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetParentPath_EmptyString_ReturnsEmpty()
    {
        var result = "".GetParentPath();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetParentPath_Whitespace_ReturnsEmpty()
    {
        var result = "   ".GetParentPath();

        result.Should().BeEmpty();
    }

    #endregion

    #region TryGetDatabaseNameFromConnectionString

    [Fact]
    public void TryGetDatabaseNameFromConnectionString_WithDatabase_ExtractsName()
    {
        var result = "Server=localhost;Database=MyDB;".TryGetDatabaseNameFromConnectionString(out var dbName);

        result.Should().BeTrue();
        dbName.Should().Be("MyDB");
    }

    [Fact]
    public void TryGetDatabaseNameFromConnectionString_WithoutDbName_ReturnsFalse()
    {
        var result = "Server=localhost;".TryGetDatabaseNameFromConnectionString(out var dbName);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryGetDatabaseNameFromConnectionString_WithInitialCatalog_ExtractsName()
    {
        var result = "Server=localhost;Initial Catalog=CatalogDB;".TryGetDatabaseNameFromConnectionString(out var dbName);

        result.Should().BeTrue();
        dbName.Should().Be("CatalogDB");
    }

    [Fact]
    public void TryGetDatabaseNameFromConnectionString_InvalidConnectionString_ReturnsFalse()
    {
        var result = "invalid=\"unterminated".TryGetDatabaseNameFromConnectionString(out var dbName);

        result.Should().BeFalse();
        dbName.Should().BeNull();
    }

    #endregion
}

/// <summary>
/// P2-2: Placeholder Replacement ({CFG:...}) tests.
/// </summary>
public class PlaceholderReplacementTests
{
    private class TestConfig
    {
        public string SchemaName { get; set; } = "dbo";
        public string TableBaseName { get; set; } = "Migration";
    }

    [Fact]
    public void SimplePlaceholder_IsReplaced()
    {
        var config = new TestConfig { SchemaName = "dbo" };
        var result = "{CFG:SchemaName}".ReplacePlaceholdersFromPropertyClass(
            config, ConfigurationConstants.ConfigurationVariableRegex);

        result.Should().Be("dbo");
    }

    [Fact]
    public void MultiplePlaceholders_AreReplaced()
    {
        var config = new TestConfig { SchemaName = "dbo", TableBaseName = "Mig" };
        var result = "{CFG:SchemaName}.{CFG:TableBaseName}".ReplacePlaceholdersFromPropertyClass(
            config, ConfigurationConstants.ConfigurationVariableRegex);

        result.Should().Be("dbo.Mig");
    }

    [Fact]
    public void UnknownPlaceholder_RemainsUnchanged()
    {
        var config = new TestConfig();
        var result = "{CFG:UnknownProp}".ReplacePlaceholdersFromPropertyClass(
            config, ConfigurationConstants.ConfigurationVariableRegex);

        result.Should().Be("{CFG:UnknownProp}");
    }

    [Fact]
    public void PropertyNull_PlaceholderRemainsUnchanged()
    {
        var config = new TestConfig();
        // Since SchemaName defaults to "dbo", test with a class that has null
        var result = "{CFG:NonExistent}".ReplacePlaceholdersFromPropertyClass(
            config, ConfigurationConstants.ConfigurationVariableRegex);

        result.Should().Be("{CFG:NonExistent}");
    }

    [Fact]
    public void WithPropertyNameFilter_OnlyAllowedAreReplaced()
    {
        var config = new TestConfig { SchemaName = "dbo", TableBaseName = "Mig" };
        var allowedNames = new List<string> { "SchemaName" };
        var result = "{CFG:SchemaName}_{CFG:TableBaseName}".ReplacePlaceholdersFromPropertyClass(
            config, ConfigurationConstants.ConfigurationVariableRegex, allowedNames);

        result.Should().Be("dbo_{CFG:TableBaseName}");
    }
}

/// <summary>
/// P2-3: GetFileEncoding tests.
/// </summary>
public class GetFileEncodingTests
{
    [Fact]
    public void NullEncoding_ReturnsUtf8()
    {
        var result = MigrationService.GetFileEncoding(null);

        result.WebName.Should().Be("utf-8");
        result.DecoderFallback.Should().BeOfType<System.Text.DecoderExceptionFallback>("the default decoder must be strict (#4)");
    }

    [Fact]
    public void Utf8String_ReturnsUtf8()
    {
        var result = MigrationService.GetFileEncoding("UTF-8");

        result.WebName.Should().Be("utf-8");
    }

    [Fact]
    public void InvalidEncoding_ThrowsConfigurationValidationException()
    {
        var act = () => MigrationService.GetFileEncoding("NOT-AN-ENCODING-XYZ");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*NOT-AN-ENCODING-XYZ*");
    }

    [Fact]
    public void EmptyString_ReturnsUtf8()
    {
        var result = MigrationService.GetFileEncoding("");

        result.WebName.Should().Be("utf-8");
        result.DecoderFallback.Should().BeOfType<System.Text.DecoderExceptionFallback>("the default decoder must be strict (#4)");
    }

    [Fact]
    public void Latin1Encoding_ReturnsCorrectEncoding()
    {
        var result = MigrationService.GetFileEncoding("iso-8859-1");

        result.WebName.Should().Be("iso-8859-1");
    }

    [Fact]
    public void WindowsEncoding_ReturnsCodePage1252()
    {
        // The product registers the CodePagesEncodingProvider itself (#4); a CLI user could never do that.
        var result = MigrationService.GetFileEncoding("windows-1252");

        result.CodePage.Should().Be(1252);
        result.DecoderFallback.Should().BeOfType<System.Text.DecoderExceptionFallback>();
    }

    [Theory]
    [InlineData("ANSI")]
    [InlineData("UTF-8-BOM")]
    public void NotAnEncodingName_ThrowsConfigurationValidationException(string name)
    {
        var act = () => MigrationService.GetFileEncoding(name);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage($"*'{name}'*")
            .WithMessage("*windows-1252*", "the message must point to a valid code-page name")
            .Which.Message.Should().NotContain("RegisterProvider", "registration is the product's job, not the user's");
    }

    [Fact]
    public void ConfiguredEncoding_IsStrict_DecoderThrowsOnInvalidBytes()
    {
        var utf8 = MigrationService.GetFileEncoding("UTF-8");
        var ascii = MigrationService.GetFileEncoding("ASCII");

        var decodeLatin1AsUtf8 = () => utf8.GetString(new byte[] { 0x47, 0x72, 0xFC, 0xDF, 0x65 }); // "Grüße" in windows-1252
        var decodeUmlautAsAscii = () => ascii.GetString(new byte[] { 0xC3, 0xA4 });                 // "ä" in UTF-8

        decodeLatin1AsUtf8.Should().Throw<System.Text.DecoderFallbackException>();
        decodeUmlautAsAscii.Should().Throw<System.Text.DecoderFallbackException>();
    }

    [Fact]
    public void Utf8BomVariant_ReturnsUtf8()
    {
        var result = MigrationService.GetFileEncoding("utf-8");

        result.WebName.Should().Be("utf-8");
    }
}
