using System.Text;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// #4: migration files are decoded strictly with the configured MigrationFilesEncoding. Bytes that are not
/// valid for that encoding used to be replaced by U+FFFD silently, so a windows-1252 file under the UTF-8
/// default was executed with garbled text and hashed in its garbled form. Now discovery aborts before any
/// file is executed and names the file and the encoding; the correct encoding or a byte-order mark fixes it.
/// migsettings.txt files are always UTF-8, independent of MigrationFilesEncoding.
/// </summary>
[Collection("Sqlite")]
[Trait("Engine", "Sqlite")]
[Trait("Category", "Features")]
public class SqliteFileEncodingTests : SqliteTestBase
{
    public SqliteFileEncodingTests(SqliteFixture fixture) : base(fixture) { }

    private const string SeedFile = "Release_1.0/Backend/03_SeedDataA.sql";

    /// <summary>The Release_1.0 seed file with an umlaut value, as the text an editor would show.</summary>
    private const string SeedWithUmlaut =
        "/*\n[RayMigrator]\nUseTransaction = true\nEnvironments = [\"*\"]\nRunAlways = false\n*/\n\n" +
        "INSERT INTO tablea (name, value) VALUES ('alpha', 10);\n" +
        "INSERT INTO tablea (name, value) VALUES ('beta', 20);\n" +
        "INSERT INTO tablea (name, value) VALUES ('Grüße', 30);\n";

    private static byte[] Windows1252(string text) => EncodingSupport.GetStrictEncoding("windows-1252").GetBytes(text);

    [Fact]
    public async Task MigrateUp_Windows1252FileUnderUtf8Default_AbortsBeforeExecutionAndNamesFileAndEncoding()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario()
            .WithFileBytes(SeedFile, Windows1252(SeedWithUmlaut))
            .BuildAsync();

        var result = await ctx.MigrateUpAsync("Release_1.0");

        ctx.AssertSuccess(false);
        result.ErrorMessage.Should().Contain("03_SeedDataA.sql", "the offending file is named");
        result.ErrorMessage.Should().Contain("[UTF-8]", "the encoding the file was read with is named");
        result.ErrorMessage.Should().Contain("MigrationFilesEncoding", "the user is pointed to the setting");
        result.ErrorMessage.Should().NotContain("Error parsing migration file",
            "discovery rethrows the MigrationFileParsingException instead of wrapping it a second time (#4)");
        ctx.CountMigrations().Should().Be(0, "discovery fails before any file of the release is executed (#4)");
        ctx.AssertTableExists("tablea", false);
    }

    [Fact]
    public async Task MigrateUp_Utf16BomFileUnderUtf8Default_DecodesByTheBomAndExecutes()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario()
            .WithFileBytes(SeedFile, new UnicodeEncoding(false, true).GetPreamble().Concat(new UnicodeEncoding(false, false).GetBytes(SeedWithUmlaut)).ToArray())
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");

        ctx.AssertSuccess(true);
        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.Migrated);
        ctx.AssertRowCount("tablea", 3);
        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString,
            "INSERT INTO tablea (name, value) SELECT 'marker', 1 FROM tablea WHERE name = 'Grüße';");
        ctx.AssertRowCount("tablea", 4);
    }

    [Fact]
    public async Task MigrateUp_Windows1252FileWithMatchingEncoding_ExecutesAndStoresTheUmlaut()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario()
            .WithMigrationFilesEncoding("windows-1252")
            .WithFileBytes(SeedFile, Windows1252(SeedWithUmlaut))
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");

        ctx.AssertSuccess(true);
        ctx.AssertFileStatus("03_SeedDataA.sql", MigrationStatus.Migrated);
        ctx.AssertRowCount("tablea", 3);
        ctx.ExecuteOnConnection(Fixture.EngineConfig.ConnectionString,
            "INSERT INTO tablea (name, value) SELECT 'marker', 1 FROM tablea WHERE name = 'Grüße';");
        ctx.AssertRowCount("tablea", 4);
    }

    [Fact]
    public async Task MigrateUp_Utf8BomFileUnderWindows1252_DecodesByTheBomAndHashesWithoutIt()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        byte[] utf8WithBom = new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(SeedWithUmlaut)).ToArray();
        await using var ctx = await CreateScenario()
            .WithMigrationFilesEncoding("windows-1252")
            .WithFileBytes(SeedFile, utf8WithBom)
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");
        ctx.AssertSuccess(true);
        ctx.AssertRowCount("tablea", 3);

        // Re-saving the same text without the BOM in the configured encoding must not change its hash.
        File.WriteAllBytes(Path.Combine(ctx.WorkDirectory, SeedFile), Windows1252(SeedWithUmlaut));
        await ctx.RebuildForAsync(MigrationCommand.ValidateHash, MigrationRunMode.Migrate);
        var validation = await ctx.ValidateHashAsync();

        validation.Success.Should().BeTrue($"the BOM is not part of the hashed text: {validation.ErrorMessage}");
        validation.InvalidFiles.Should().Be(0);
    }

    [Fact]
    public async Task MigrateUp_MigSettingsInWindows1252_AbortsEvenWhenMigrationFilesEncodingIsWindows1252()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        await using var ctx = await CreateScenario()
            .WithMigrationFilesEncoding("windows-1252")
            .WithFileBytes("Release_1.0/migsettings.txt", Windows1252("# Grüße\n[RayMigrator]\nRunAlways = false\n"))
            .BuildAsync();

        var result = await ctx.MigrateUpAsync("Release_1.0");

        ctx.AssertSuccess(false);
        result.ErrorMessage.Should().Contain("migsettings.txt").And.Contain("[UTF-8]",
            "settings files are always UTF-8, MigrationFilesEncoding does not apply to them (#4)");
        ctx.CountMigrations().Should().Be(0);
    }

    [Fact]
    public async Task MigrateUp_MigSettingsInUtf8WithBom_IsAccepted()
    {
        Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available");
        byte[] bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("# Grüße\n[RayMigrator]\nRunAlways = false\n")).ToArray();
        await using var ctx = await CreateScenario()
            .WithFileBytes("Release_1.0/migsettings.txt", bytes)
            .BuildAsync();

        await ctx.MigrateUpAsync("Release_1.0");

        ctx.AssertSuccess(true);
        ctx.AssertRowCount("tablea", 3);
    }
}
